using System.Text.Json.Serialization;

namespace JobFlow.Mobile.Models;

public sealed class DailyFeed
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("generated_at")]
    public string? GeneratedAt { get; set; }

    [JsonPropertyName("criteria")]
    public FeedCriteria? Criteria { get; set; }

    [JsonPropertyName("jobs")]
    public List<JobFeedItem> Jobs { get; set; } = [];
}

public sealed class FeedCriteria
{
    [JsonPropertyName("minimum_annual_compensation_usd")]
    public decimal? MinimumAnnualCompensationUsd { get; set; }

    [JsonPropertyName("remote_only")]
    public bool RemoteOnly { get; set; }
}

public sealed class JobFeedItem
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("company")]
    public string Company { get; set; } = "";

    [JsonPropertyName("salary_min_usd")]
    public decimal? SalaryMinUsd { get; set; }

    [JsonPropertyName("salary_max_usd")]
    public decimal? SalaryMaxUsd { get; set; }

    [JsonPropertyName("salary_pay_type")]
    public string? SalaryPayType { get; set; }

    [JsonPropertyName("remote_scope")]
    public string? RemoteScope { get; set; }

    [JsonPropertyName("verified_at")]
    public string? VerifiedAt { get; set; }

    [JsonPropertyName("application_url")]
    public string? ApplicationUrl { get; set; }

    [JsonPropertyName("match_score")]
    public decimal? MatchScore { get; set; }

    [JsonPropertyName("metadata_path")]
    public string? MetadataPath { get; set; }

    [JsonPropertyName("resume_path")]
    public string? ResumePath { get; set; }

    [JsonPropertyName("cover_letter_path")]
    public string? CoverLetterPath { get; set; }

    [JsonPropertyName("application_tracking_path")]
    public string? ApplicationTrackingPath { get; set; }

    public string SalaryLabel
    {
        get
        {
            if (SalaryMinUsd is null) return "Salary not listed";
            var minimum = SalaryMinUsd.Value.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            return SalaryMaxUsd is null
                ? $"{minimum}+ per year"
                : $"{minimum}–{SalaryMaxUsd.Value.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))} per year";
        }
    }
}
