using System.Globalization;
using System.Text.Json;
using JobFlow.Mobile.Models;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Parses and validates the versioned JSON envelope produced by Jobflow-data.
/// Kept free of HTTP and MAUI dependencies so its contract can be tested independently.
/// </summary>
public static class DailyFeedParser
{
    private const int SupportedSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DailyFeed Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("The daily feed was empty.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The daily feed must be a JSON object.");

        if (!root.TryGetProperty("schema_version", out var schemaVersionElement)
            || !schemaVersionElement.TryGetInt32(out var schemaVersion))
            throw new InvalidOperationException("The daily feed is missing a valid schema_version.");

        if (schemaVersion != SupportedSchemaVersion)
            throw new NotSupportedException($"Feed schema version {schemaVersion} is not supported. This app supports version {SupportedSchemaVersion}.");

        if (!root.TryGetProperty("jobs", out var jobsElement) || jobsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The daily feed must contain a jobs array.");
        if (jobsElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object))
            throw new InvalidOperationException("Every item in the daily feed jobs array must be a JSON object.");

        var feed = JsonSerializer.Deserialize<DailyFeed>(json, JsonOptions)
            ?? throw new InvalidOperationException("The daily feed could not be parsed.");

        var invalidJobs = feed.Jobs
            .Where(job => string.IsNullOrWhiteSpace(job.JobId)
                || string.IsNullOrWhiteSpace(job.Title)
                || string.IsNullOrWhiteSpace(job.Company))
            .ToList();

        if (invalidJobs.Count > 0)
            throw new InvalidOperationException($"The daily feed contains {invalidJobs.Count} job(s) missing a required job_id, title, or company.");

        // Preserve the first occurrence so a malformed feed cannot duplicate a job in the UI.
        feed.Jobs = feed.Jobs
            .DistinctBy(job => job.JobId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return feed;
    }

    public static string? SelectLatestFeedFile(IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        return fileNames
            .Where(name => DateOnly.TryParseExact(
                name,
                "yyyy-MM-dd'.json'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
