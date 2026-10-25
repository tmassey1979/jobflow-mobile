using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobFlow-Mobile/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public async Task<DailyFeed> LoadLatestFeedAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Enter a GitHub token with read-only Contents access to the private Jobflow-data repository.", nameof(token));

        using var request = CreateRequest($"https://api.github.com/repos/{Owner}/{Repository}/contents/daily", token);
        using var response = await Http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        var listingJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var entries = JsonSerializer.Deserialize<List<GitHubContentEntry>>(listingJson, JsonOptions) ?? [];

        var latestName = DailyFeedParser.SelectLatestFeedFile(
            entries.Where(entry => entry.Type == "file").Select(entry => entry.Name));
        if (latestName is null)
            throw new InvalidOperationException("No dated JSON feeds in YYYY-MM-DD.json format were found in the repository's daily folder.");

        var feedJson = await ReadTextFileAsync($"daily/{latestName}", token, cancellationToken);
        var feed = DailyFeedParser.Parse(feedJson);
        feed.SourcePath = $"daily/{latestName}";
        return feed;
    }

    public async Task<string> ReadTextFileAsync(string relativePath, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("A GitHub read token is required.", nameof(token));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A repository-relative path is required.", nameof(relativePath));

        var normalized = relativePath.TrimStart('/');
        if (normalized.Split('/').Any(part => part is ".." or "."))
            throw new ArgumentException("Invalid repository-relative path.", nameof(relativePath));

        var escapedPath = string.Join("/", normalized.Split('/').Select(Uri.EscapeDataString));
        using var request = CreateRequest($"https://api.github.com/repos/{Owner}/{Repository}/contents/{escapedPath}", token);
        using var response = await Http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var entry = JsonSerializer.Deserialize<GitHubContentEntry>(json, JsonOptions)
            ?? throw new InvalidOperationException("GitHub returned an empty file response.");

        if (!string.Equals(entry.Encoding, "base64", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(entry.Content))
            throw new InvalidOperationException("The requested file did not contain readable base64 text. Files larger than GitHub's Contents API limit are not supported yet.");

        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(entry.Content.Replace("\n", "")));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("GitHub returned file content that was not valid base64.", ex);
        }
    }

    private static HttpRequestMessage CreateRequest(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "GitHub rejected the token. Check that it is valid and has read-only Contents access.",
            System.Net.HttpStatusCode.Forbidden => "GitHub denied access. Check the token's repository access and rate limits.",
            System.Net.HttpStatusCode.NotFound => "The private data repository or requested file was not found. Confirm the token can access Jobflow-data.",
            _ => $"GitHub request failed ({(int)response.StatusCode} {response.ReasonPhrase})."
        };
        // Avoid displaying raw API response bodies, which may include private repository details.
        throw new InvalidOperationException(message);
    }

    private sealed class GitHubContentEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("encoding")]
        public string? Encoding { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
