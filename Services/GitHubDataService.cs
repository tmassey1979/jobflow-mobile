using System.Net.Http.Headers;
using System.Text.Json;
using JobFlow.Mobile.Models;

namespace JobFlow.Mobile.Services;

public sealed class GitHubDataService
{
    private const string Owner = "tmassey1979";
    private const string Repository = "Jobflow-data";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new();

    public async Task<DailyFeed> LoadLatestFeedAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Enter a GitHub token with read-only Contents access to the private Jobflow-data repository.", nameof(token));

        using var request = CreateRequest($"https://api.github.com/repos/{Owner}/{Repository}/contents/daily", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var listingJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var entries = JsonSerializer.Deserialize<List<GitHubContentEntry>>(listingJson, JsonOptions) ?? [];

        var latest = entries
            .Where(entry => entry.Type == "file"
                && entry.Name.StartsWith("20", StringComparison.Ordinal)
                && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latest is null)
            throw new InvalidOperationException("No dated JSON feeds were found in the repository's daily folder.");

        using var feedRequest = CreateRequest(latest.DownloadUrl
            ?? $"https://api.github.com/repos/{Owner}/{Repository}/contents/daily/{Uri.EscapeDataString(latest.Name)}", token);
        using var feedResponse = await _httpClient.SendAsync(feedRequest, cancellationToken);
        await EnsureSuccessAsync(feedResponse, cancellationToken);
        var feedJson = await feedResponse.Content.ReadAsStringAsync(cancellationToken);

        var feed = JsonSerializer.Deserialize<DailyFeed>(feedJson, JsonOptions);
        if (feed is null)
            throw new InvalidOperationException($"The feed {latest.Name} was empty or invalid.");

        feed.Jobs = feed.Jobs
            .Where(job => !string.IsNullOrWhiteSpace(job.JobId))
            .GroupBy(job => job.JobId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return feed;
    }

    public async Task<string> ReadTextFileAsync(string relativePath, string token, CancellationToken cancellationToken = default)
    {
        var normalized = relativePath.TrimStart('/');
        if (normalized.Split('/').Any(part => part == ".."))
            throw new ArgumentException("Invalid repository-relative path.", nameof(relativePath));

        var escapedPath = string.Join("/", normalized.Split('/').Select(Uri.EscapeDataString));
        using var request = CreateRequest(
            $"https://api.github.com/repos/{Owner}/{Repository}/contents/{escapedPath}", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var entry = JsonSerializer.Deserialize<GitHubContentEntry>(json, JsonOptions)
            ?? throw new InvalidOperationException("GitHub returned an empty file response.");

        if (!string.Equals(entry.Encoding, "base64", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(entry.Content))
        {
            if (!string.IsNullOrWhiteSpace(entry.DownloadUrl))
            {
                using var downloadRequest = CreateRequest(entry.DownloadUrl, token);
                using var downloadResponse = await _httpClient.SendAsync(downloadRequest, cancellationToken);
                await EnsureSuccessAsync(downloadResponse, cancellationToken);
                return await downloadResponse.Content.ReadAsStringAsync(cancellationToken);
            }
            throw new InvalidOperationException("The requested file did not contain readable text.");
        }

        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(entry.Content.Replace("\n", "")));
    }

    private static HttpRequestMessage CreateRequest(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("JobFlow-Mobile/0.1");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "GitHub rejected the token. Check that it is valid and has read-only Contents access.",
            System.Net.HttpStatusCode.Forbidden => "GitHub denied access. Check the token's repository access and rate limits.",
            System.Net.HttpStatusCode.NotFound => "The private data repository or requested file was not found. Confirm the token can access Jobflow-data.",
            _ => $"GitHub request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
        };
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(details) ? message : $"{message} {details}");
    }

    private sealed class GitHubContentEntry
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Encoding { get; set; }
        public string? Content { get; set; }
        public string? DownloadUrl { get; set; }
    }
}
