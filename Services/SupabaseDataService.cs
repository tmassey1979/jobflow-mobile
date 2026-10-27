using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobFlow.Mobile.Models;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Supabase Auth + per-user JobFlow feed access. The publishable key is public by design;
/// every database request also carries the signed-in user's JWT and is enforced by RLS.
/// Never put a service-role/secret key in this Android app.
/// </summary>
public sealed class SupabaseDataService
{
    public const string ProjectUrl = "https://cwrijlxpkrwtpyhvncmx.supabase.co";
    public const string PublishableKey = "sb_publishable_fQ_4AGE0qQXblVZdJzwicQ_Y7B-_TYQ";
    private const string AccessTokenKey = "jobflow.supabase.access_token";
    private const string RefreshTokenKey = "jobflow.supabase.refresh_token";
    private const string EmailKey = "jobflow.supabase.email";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task ClearLegacyGitHubCredentialsAsync()
    {
        foreach (var key in new[]
        {
            "jobflow.github.access.token", "jobflow.github.user.login",
            "jobflow.github.repo.owner", "jobflow.github.repo.name",
            "jobflow.github.oauth.client_id", "jobflow.github.readonly.token",
            "jobflow.github.pending.device_code", "jobflow.github.pending.user_code",
            "jobflow.github.pending.verification_uri", "jobflow.github.pending.interval",
            "jobflow.github.pending.expires_at"
        })
        {
            try { SecureStorage.Default.Remove(key); } catch { }
        }
        await Task.CompletedTask;
    }

    public async Task<string?> GetEmailAsync() => await SecureStorage.Default.GetAsync(EmailKey);
    public async Task<bool> IsSignedInAsync() => !string.IsNullOrWhiteSpace(await GetValidAccessTokenAsync());

    public async Task SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(email, password);
        using var response = await SendAuthAsync("signup", new { email = email.Trim(), password }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw AuthError(body, "Could not create the account.");
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var token) || string.IsNullOrWhiteSpace(token.GetString()))
        {
            throw new InvalidOperationException("Account request received. Check your email for a confirmation link, then sign in here.");
        }
        await SaveSessionAsync(doc.RootElement, email.Trim());
    }

    public async Task SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(email, password);
        using var response = await SendAuthAsync("token?grant_type=password", new { email = email.Trim(), password }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw AuthError(body, "Could not sign in.");
        using var doc = JsonDocument.Parse(body);
        await SaveSessionAsync(doc.RootElement, email.Trim());
    }

    public async Task SignOutAsync()
    {
        var token = await SecureStorage.Default.GetAsync(AccessTokenKey);
        try
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/auth/v1/logout");
                AddHeaders(request, token);
                using var response = await Http.SendAsync(request);
            }
        }
        catch { /* Local sign-out must still work offline. */ }
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(EmailKey);
    }

    public async Task<DailyFeed> LoadAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetValidAccessTokenAsync() ?? throw new InvalidOperationException("Sign in with Supabase on the Integrations tab first.");
        var url = $"{ProjectUrl}/rest/v1/shared_job_records?select=stable_job_id,metadata,discovered_at,verified_at,feed_date&order=verified_at.desc.nullslast";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request, token);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, "Couldn't load the Supabase job feed.");
        using var document = JsonDocument.Parse(body);
        var jobs = new List<JobFeedItem>();
        foreach (var row in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!row.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object) continue;
            var node = JsonNode.Parse(metadata.GetRawText())?.AsObject();
            if (node is null) continue;
            if (!node.ContainsKey("job_id") && row.TryGetProperty("stable_job_id", out var stableId))
                node["job_id"] = stableId.GetString();
            if (!node.ContainsKey("verified_at") && row.TryGetProperty("verified_at", out var verified))
                node["verified_at"] = verified.ValueKind == JsonValueKind.String ? verified.GetString() : null;
            var job = node.Deserialize<JobFeedItem>(JsonOptions);
            if (job is not null && !string.IsNullOrWhiteSpace(job.JobId)
                && !string.IsNullOrWhiteSpace(job.Title) && !string.IsNullOrWhiteSpace(job.Company))
                jobs.Add(job);
        }
        var unique = jobs.DistinctBy(job => job.JobId, StringComparer.OrdinalIgnoreCase).ToList();
        if (unique.Count == 0) throw new InvalidOperationException("Supabase returned no valid jobs for this account yet. The daily feed writer still needs to be connected.");
        return new DailyFeed
        {
            SchemaVersion = 1,
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            Jobs = unique,
            SourcePath = $"Supabase · {unique.Count} jobs"
        };
    }

    public async Task<string?> GetJobDocumentAsync(string jobId, string kind, CancellationToken cancellationToken = default)
    {
        var token = await GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return null;
        var escapedId = Uri.EscapeDataString(jobId);
        var isMetadata = kind == "metadata";
        var table = isMetadata ? "shared_job_records" : "job_records";
        var select = isMetadata ? "metadata" : "cover_letter_md,resume_md";
        var url = $"{ProjectUrl}/rest/v1/{table}?select={select}&stable_job_id=eq.{escapedId}&limit=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request, token);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, "Couldn't load the job document.");
        using var doc = JsonDocument.Parse(body);
        var row = doc.RootElement.EnumerateArray().FirstOrDefault();
        if (row.ValueKind != JsonValueKind.Object) return null;
        if (isMetadata)
        {
            if (!row.TryGetProperty("metadata", out var metadata)) return null;
            return metadata.ValueKind == JsonValueKind.String ? metadata.GetString()
                : metadata.ValueKind == JsonValueKind.Object ? metadata.GetRawText() : null;
        }
        var column = kind == "resume" ? "resume_md" : "cover_letter_md";
        if (!row.TryGetProperty(column, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }

    private async Task<string?> GetValidAccessTokenAsync()
    {
        var token = await SecureStorage.Default.GetAsync(AccessTokenKey);
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ProjectUrl}/auth/v1/user");
            AddHeaders(request, token);
            using var response = await Http.SendAsync(request);
            if (response.IsSuccessStatusCode) return token;
        }
        catch { return null; }

        var refresh = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refresh)) { await SignOutAsync(); return null; }
        try
        {
            using var response = await SendAuthAsync("token?grant_type=refresh_token", new { refresh_token = refresh }, CancellationToken.None);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) { await SignOutAsync(); return null; }
            using var doc = JsonDocument.Parse(body);
            await SaveSessionAsync(doc.RootElement, await GetEmailAsync() ?? "");
            return await SecureStorage.Default.GetAsync(AccessTokenKey);
        }
        catch { return null; }
    }

    private static async Task<HttpResponseMessage> SendAuthAsync(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/auth/v1/{path}");
        AddHeaders(request, null);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await Http.SendAsync(request, cancellationToken);
    }

    private static void AddHeaders(HttpRequestMessage request, string? accessToken)
    {
        request.Headers.Add("apikey", PublishableKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task SaveSessionAsync(JsonElement root, string email)
    {
        if (!root.TryGetProperty("access_token", out var access) || string.IsNullOrWhiteSpace(access.GetString()))
            throw new InvalidOperationException("Supabase did not return an access token. Confirm your email if required, then sign in.");
        await SecureStorage.Default.SetAsync(AccessTokenKey, access.GetString()!);
        if (root.TryGetProperty("refresh_token", out var refresh) && !string.IsNullOrWhiteSpace(refresh.GetString()))
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refresh.GetString()!);
        await SecureStorage.Default.SetAsync(EmailKey, email);
    }

    private static void ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("Enter a valid email address.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6) throw new ArgumentException("Enter a password with at least 6 characters.");
    }

    private static Exception AuthError(string body, string fallback)
    {
        try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("msg", out var msg)) return new InvalidOperationException(msg.GetString()); if (doc.RootElement.TryGetProperty("message", out var message)) return new InvalidOperationException(message.GetString()); }
        catch { }
        return new InvalidOperationException(fallback);
    }
    private static Exception ApiError(string body, string fallback)
    {
        try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("message", out var msg)) return new InvalidOperationException(msg.GetString()); if (doc.RootElement.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.String) return new InvalidOperationException(details.GetString()); }
        catch { }
        return new InvalidOperationException(fallback);
    }
}
