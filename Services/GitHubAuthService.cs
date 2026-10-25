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
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public async Task<DeviceAuthorization> StartDeviceLoginAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("A GitHub OAuth App client id is required.", nameof(clientId));

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["scope"] = Scopes
        });

        using var response = await Http.PostAsync("https://github.com/login/device/code", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("GitHub could not start device login. Check the OAuth App client id and that Device Flow is enabled.");

        var payload = JsonSerializer.Deserialize<DeviceCodeResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("GitHub returned an empty device-login response.");

        if (string.IsNullOrWhiteSpace(payload.DeviceCode) || string.IsNullOrWhiteSpace(payload.UserCode))
            throw new InvalidOperationException("GitHub did not return a device code. Confirm Device Flow is enabled on the OAuth App.");

        return new DeviceAuthorization(
            payload.DeviceCode,
            payload.UserCode,
            string.IsNullOrWhiteSpace(payload.VerificationUri) ? "https://github.com/login/device" : payload.VerificationUri,
            payload.Interval <= 0 ? 5 : payload.Interval,
            payload.ExpiresIn <= 0 ? 900 : payload.ExpiresIn);
    }

    public async Task<string> WaitForAccessTokenAsync(
        string clientId,
        DeviceAuthorization authorization,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(authorization.ExpiresInSeconds);
        var interval = TimeSpan.FromSeconds(Math.Max(5, authorization.IntervalSeconds));

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(interval, cancellationToken);

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["device_code"] = authorization.DeviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            using var response = await Http.PostAsync("https://github.com/login/oauth/access_token", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<TokenPollResponse>(body, JsonOptions);

            if (!string.IsNullOrWhiteSpace(payload?.AccessToken))
                return payload.AccessToken;

            var error = payload?.Error ?? string.Empty;
            if (string.Equals(error, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                status?.Report("Waiting for you to finish signing in on GitHub…");
                continue;
            }

            if (string.Equals(error, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                interval += TimeSpan.FromSeconds(5);
                status?.Report("GitHub asked the app to slow down; still waiting…");
                continue;
            }

            if (string.Equals(error, "expired_token", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The GitHub sign-in code expired. Start again.");

            if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GitHub sign-in was denied.");

            if (!string.IsNullOrWhiteSpace(error))
                throw new InvalidOperationException("GitHub sign-in did not complete. Try again.");
        }

        throw new InvalidOperationException("Timed out waiting for GitHub sign-in.");
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

    public sealed record DeviceAuthorization(
        string DeviceCode,
        string UserCode,
        string VerificationUri,
        int IntervalSeconds,
        int ExpiresInSeconds);

    public sealed record GitHubRepositoryRef(string Owner, string Name, bool IsPrivate, string FullName);

    private sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = "";

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = "";

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = "";

        [JsonPropertyName("interval")]
        public int Interval { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class TokenPollResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

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
