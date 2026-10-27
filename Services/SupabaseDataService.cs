using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobFlow.Mobile.Models;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Supabase Auth, shared job-listing feed access, and private per-user application-kit lookup. The publishable key is public by design;
/// every database request also carries the signed-in user's JWT and is enforced by RLS.
/// Never put a service-role/secret key in this Android app.
/// </summary>
public sealed class SupabaseDataService
{
    public const string ProjectUrl = "https://cwrijlxpkrwtpyhvncmx.supabase.co";
    public const string PublishableKey = "sb_publishable_P3KM4EKozcHn2YQUSYQQGw_cP8g-a32";
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
        var employerPreferences = await GetEmployersAsync(cancellationToken);
        var blockedEmployers = employerPreferences
            .Where(employer => employer.DoNotPursue)
            .Select(employer => employer.NormalizedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

            // The shared feed stores the employer name as "employer", while JobFeedItem
            // expects "company". Normalize the feed contract before deserializing so valid
            // Supabase rows aren't silently discarded by the required-field check.
            CopyIfMissing(node, "company", "employer");
            CopyIfMissing(node, "job_id", "stable_job_id", row);
            CopyIfMissing(node, "verified_at", "verified_at", row);
            CopyIfMissing(node, "application_url", "direct_application_url");
            CopyIfMissing(node, "remote_scope", "remote_eligibility");
            CopyIfMissing(node, "match_score", "fit_score");
            CopyIfMissing(node, "description", "job_description");
            CopyIfMissing(node, "description", "full_description");
            CopyIfMissing(node, "description", "job_description_text");
            CopyNestedIfMissing(node, "salary_min_usd", "compensation", "min_annual");
            CopyNestedIfMissing(node, "salary_max_usd", "compensation", "max_annual");
            CopyNestedIfMissing(node, "salary_pay_type", "compensation", "type");
            CopyNestedIfMissing(node, "compensation_published_text", "compensation", "published_text");
            NormalizeRemoteEligibility(node);

            var job = node.Deserialize<JobFeedItem>(JsonOptions);
            if (job is not null && !string.IsNullOrWhiteSpace(job.JobId)
                && !string.IsNullOrWhiteSpace(job.Title) && !string.IsNullOrWhiteSpace(job.Company))
                jobs.Add(job);
        }
        var unique = jobs.DistinctBy(job => job.JobId, StringComparer.OrdinalIgnoreCase).ToList();
        if (unique.Count == 0)
            throw new InvalidOperationException("Supabase returned rows, but none matched the app's required job fields (job ID, title, and employer). Check the shared feed metadata mapping.");

        await EnsureEmployersExistAsync(unique, cancellationToken);
        var visibleJobs = unique
            .Where(job => !blockedEmployers.Contains(NormalizeEmployerName(job.Company)))
            .ToList();

        return new DailyFeed
        {
            SchemaVersion = 1,
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            Jobs = visibleJobs,
            SourcePath = $"Supabase · {visibleJobs.Count} jobs"
        };
    }

    private static void NormalizeRemoteEligibility(JsonObject target)
    {
        if (!target.TryGetPropertyValue("remote_scope", out var value) || value is null)
            return;

        if (value is JsonValue scalar && scalar.TryGetValue<string>(out _))
            return;

        if (value is JsonObject obj)
        {
            var parts = new List<string>();
            foreach (var key in new[] { "type", "eligible_locations", "verification_status", "eligibility", "description" })
            {
                if (obj.TryGetPropertyValue(key, out var candidate)
                    && candidate is JsonValue candidateValue
                    && candidateValue.TryGetValue<string>(out var text)
                    && !string.IsNullOrWhiteSpace(text))
                    parts.Add(text.Trim());
            }
            if (parts.Count > 0)
            {
                target["remote_scope"] = string.Join(" · ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
                return;
            }
        }

        target["remote_scope"] = value.ToJsonString();
    }

    private static void CopyNestedIfMissing(JsonObject target, string targetKey, string parentKey, string childKey)
    {
        if (target.ContainsKey(targetKey)
            || !target.TryGetPropertyValue(parentKey, out var parent)
            || parent is not JsonObject parentObject
            || !parentObject.TryGetPropertyValue(childKey, out var child)
            || child is null)
            return;
        target[targetKey] = child.DeepClone();
    }

    public async Task<List<EmployerRecord>> GetEmployersAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetValidAccessTokenAsync()
            ?? throw new InvalidOperationException("Sign in to JobFlow Cloud to load the employer directory.");
        var url = $"{ProjectUrl}/rest/v1/employers?select=normalized_name,name,website,industry,summary,pursuit_status,do_not_pursue,block_reason,notes,updated_at&order=name.asc";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddHeaders(request, token);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, "Couldn't load the employer directory.");
        return JsonSerializer.Deserialize<List<EmployerRecord>>(body, JsonOptions) ?? [];
    }

    public async Task SaveEmployerAsync(EmployerRecord employer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employer);
        if (string.IsNullOrWhiteSpace(employer.Name))
            throw new ArgumentException("An employer name is required.");
        employer.NormalizedName = NormalizeEmployerName(employer.Name);
        if (string.IsNullOrWhiteSpace(employer.NormalizedName))
            throw new ArgumentException("Enter a valid employer name.");
        if (employer.DoNotPursue && string.IsNullOrWhiteSpace(employer.BlockReason))
            throw new ArgumentException("Add a reason before blocking this employer.");
        employer.PursuitStatus = employer.DoNotPursue ? "do_not_pursue"
            : employer.PursuitStatus == "do_not_pursue" ? "undecided" : employer.PursuitStatus;
        employer.UpdatedAt = DateTimeOffset.UtcNow;
        var token = await GetValidAccessTokenAsync()
            ?? throw new InvalidOperationException("Sign in to JobFlow Cloud before saving employer decisions.");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{ProjectUrl}/rest/v1/employers?on_conflict=normalized_name");
        AddHeaders(request, token);
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = new StringContent(JsonSerializer.Serialize(employer), Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, "Couldn't save the employer decision.");
    }

    private async Task EnsureEmployersExistAsync(IReadOnlyCollection<JobFeedItem> jobs, CancellationToken cancellationToken)
    {
        var known = (await GetEmployersAsync(cancellationToken))
            .Select(employer => employer.NormalizedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var job in jobs)
        {
            var normalized = NormalizeEmployerName(job.Company);
            if (string.IsNullOrWhiteSpace(normalized) || known.Contains(normalized)) continue;
            var employer = new EmployerRecord
            {
                NormalizedName = normalized,
                Name = job.Company.Trim(),
                Summary = job.CompanySummary,
                PursuitStatus = "undecided",
                DoNotPursue = false
            };
            await SaveEmployerAsync(employer, cancellationToken);
            known.Add(normalized);
        }
    }

    public static string NormalizeEmployerName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : new string(name.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static void CopyIfMissing(JsonObject target, string targetKey, string sourceKey)
    {
        if (target.ContainsKey(targetKey) || !target.TryGetPropertyValue(sourceKey, out var source) || source is null)
            return;
        target[targetKey] = source.DeepClone();
    }

    private static void CopyIfMissing(JsonObject target, string targetKey, string sourceKey, JsonElement row)
    {
        if (target.ContainsKey(targetKey) || !row.TryGetProperty(sourceKey, out var source))
            return;
        target[targetKey] = source.ValueKind switch
        {
            JsonValueKind.String => JsonValue.Create(source.GetString()),
            JsonValueKind.Number => JsonValue.Create(source.GetRawText()),
            JsonValueKind.True => JsonValue.Create(true),
            JsonValueKind.False => JsonValue.Create(false),
            _ => null
        };
    }

    public Task UpsertApplicationTrackingAsync(ApplicationRecord record, CancellationToken cancellationToken = default) =>
        UpsertApplicationTrackingBatchAsync(new[] { record }, cancellationToken);

    public async Task UpsertApplicationTrackingBatchAsync(IReadOnlyCollection<ApplicationRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;
        var token = await GetValidAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Sign in to JobFlow Cloud before syncing application tracking.");

        var payload = records.Select(record => new
        {
            stable_job_id = record.JobId,
            status = record.Status switch
            {
                ApplicationStages.Interested => "discovered",
                ApplicationStages.Preparing => "reviewed",
                ApplicationStages.ReadyToApply => "ready_for_approval",
                ApplicationStages.Applied => "applied",
                ApplicationStages.Interviewing => "interview",
                ApplicationStages.Offer => "offer",
                ApplicationStages.Rejected => "rejected",
                _ => "discovered"
            },
            approval_required = true,
            notes = record.Notes,
            follow_up_date = record.FollowUpAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd"),
            updated_at = record.UpdatedAtUtc.ToUniversalTime().ToString("O")
        }).ToArray();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ProjectUrl}/rest/v1/application_tracking?on_conflict=user_id%2Cstable_job_id");
        AddHeaders(request, token);
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw ApiError(body, "Couldn't sync application tracking to JobFlow Cloud.");
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
