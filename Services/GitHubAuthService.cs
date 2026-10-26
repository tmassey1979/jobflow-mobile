using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobFlow.Mobile.Services;

/// <summary>
/// GitHub OAuth device-flow sign-in. Opens GitHub in the browser; the user approves there.
/// No client secret is used or stored. Requires an OAuth App with Device Flow enabled.
/// </summary>
public sealed class GitHubAuthService
{
    // repo scope is required for private repository contents via classic OAuth Apps.
    // The app only reads the repository the user selects afterward.
    private const string Scopes = "repo";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobFlow-Mobile/0.1");
        // Prefer JSON, but token endpoints still sometimes return form-urlencoded.
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public async Task<DeviceAuthorization> StartDeviceLoginAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("A GitHub OAuth App client id is required.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["scope"] = Scopes
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code")
        {
            Content = content
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var fields = ParseOAuthBody(body);

        if (!response.IsSuccessStatusCode || fields.ContainsKey("error"))
        {
            var error = GetField(fields, "error");
            var description = GetField(fields, "error_description") ?? GetField(fields, "error_uri");
            if (string.Equals(error, "unauthorized_client", StringComparison.OrdinalIgnoreCase)
                || body.Contains("device flow", StringComparison.OrdinalIgnoreCase)
                || (body.Contains("device_code", StringComparison.OrdinalIgnoreCase) && !fields.ContainsKey("device_code")))
            {
                throw new InvalidOperationException(
                    "GitHub rejected device login. On github.com/settings/developers open your OAuth App and enable Device Flow, then try again.");
            }

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(description)
                    ? "GitHub could not start device login. Check the OAuth App client id and that Device Flow is enabled."
                    : $"GitHub login error: {description}");
        }

        var deviceCode = GetField(fields, "device_code");
        var userCode = GetField(fields, "user_code");
        if (string.IsNullOrWhiteSpace(deviceCode) || string.IsNullOrWhiteSpace(userCode))
            throw new InvalidOperationException("GitHub did not return a device code. Confirm Device Flow is enabled on the OAuth App.");

        var verificationUri = GetField(fields, "verification_uri") ?? "https://github.com/login/device";
        // Prefill the code when opening the browser (GitHub accepts user_code query).
        var openUri = verificationUri.Contains('?', StringComparison.Ordinal)
            ? verificationUri
            : $"{verificationUri.TrimEnd('/')}?user_code={Uri.EscapeDataString(userCode)}";

        var interval = ParseIntField(fields, "interval", 5);
        var expiresIn = ParseIntField(fields, "expires_in", 900);

        return new DeviceAuthorization(
            deviceCode,
            userCode,
            openUri,
            Math.Max(5, interval),
            Math.Max(60, expiresIn));
    }

    public async Task<string> WaitForAccessTokenAsync(
        string clientId,
        DeviceAuthorization authorization,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(authorization.ExpiresInSeconds);
        var interval = TimeSpan.FromSeconds(Math.Max(5, authorization.IntervalSeconds));
        // First poll quickly so approval is detected soon after the user finishes in the browser.
        var nextDelay = TimeSpan.FromSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(nextDelay, cancellationToken);
            nextDelay = interval;

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["device_code"] = authorization.DeviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
            {
                Content = content
            };
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await Http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var fields = ParseOAuthBody(body);

            var accessToken = GetField(fields, "access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
                return accessToken;

            var error = GetField(fields, "error") ?? string.Empty;
            if (string.Equals(error, "authorization_pending", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(error))
            {
                status?.Report($"Waiting for GitHub approval… code {authorization.UserCode}");
                continue;
            }

            if (string.Equals(error, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                interval += TimeSpan.FromSeconds(5);
                nextDelay = interval;
                status?.Report("GitHub asked the app to slow down; still waiting…");
                continue;
            }

            if (string.Equals(error, "expired_token", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The GitHub sign-in code expired. Tap Sign in with GitHub again.");

            if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GitHub sign-in was denied in the browser.");

            if (string.Equals(error, "incorrect_device_code", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GitHub rejected the device code. Tap Sign in with GitHub to get a new code.");

            var description = GetField(fields, "error_description");
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(description)
                    ? $"GitHub sign-in did not complete ({error})."
                    : description);
        }

        throw new InvalidOperationException("Timed out waiting for GitHub sign-in. Tap Sign in with GitHub to start again.");
    }

    public async Task<IReadOnlyList<GitHubRepositoryRef>> ListRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("An access token is required.", nameof(accessToken));

        var results = new List<GitHubRepositoryRef>();
        for (var page = 1; page <= 5; page++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/user/repos?per_page=100&page={page}&sort=updated&affiliation=owner,collaborator,organization_member");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.UserAgent.ParseAdd("JobFlow-Mobile/0.1");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Could not list your GitHub repositories. Sign in again or check network access.");

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var pageItems = JsonSerializer.Deserialize<List<RepoDto>>(json, JsonOptions) ?? [];
            if (pageItems.Count == 0)
                break;

            foreach (var item in pageItems)
            {
                if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Owner?.Login))
                    continue;
                results.Add(new GitHubRepositoryRef(item.Owner.Login, item.Name, item.Private, item.FullName ?? $"{item.Owner.Login}/{item.Name}"));
            }

            if (pageItems.Count < 100)
                break;
        }

        return results
            .OrderByDescending(r => string.Equals(r.Name, GitHubAccountStore.DefaultRepo, StringComparison.OrdinalIgnoreCase))
            .ThenBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> ParseOAuthBody(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body))
            return result;

        var trimmed = body.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        result[property.Name] = property.Value.ValueKind switch
                        {
                            JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                            JsonValueKind.Number => property.Value.ToString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => string.Empty,
                            _ => property.Value.ToString()
                        };
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to form parsing.
            }

            if (result.Count > 0)
                return result;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;
            var key = Uri.UnescapeDataString(part[..separator].Replace('+', ' '));
            var value = Uri.UnescapeDataString(part[(separator + 1)..].Replace('+', ' '));
            result[key] = value;
        }

        return result;
    }

    private static string? GetField(Dictionary<string, string> fields, string name) =>
        fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static int ParseIntField(Dictionary<string, string> fields, string name, int fallback) =>
        fields.TryGetValue(name, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    public sealed record DeviceAuthorization(
        string DeviceCode,
        string UserCode,
        string VerificationUri,
        int IntervalSeconds,
        int ExpiresInSeconds);

    public sealed record GitHubRepositoryRef(string Owner, string Name, bool IsPrivate, string FullName);

    private sealed class RepoDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("owner")]
        public OwnerDto? Owner { get; set; }
    }

    private sealed class OwnerDto
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = "";
    }
}
