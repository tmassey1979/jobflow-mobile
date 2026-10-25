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

    [JsonPropertyName("verification_summary")]
    public FeedVerificationSummary? VerificationSummary { get; set; }

    [JsonPropertyName("jobs")]
    public List<JobFeedItem> Jobs { get; set; } = [];

    // Runtime-only troubleshooting metadata; it is not part of the JSON feed contract.
    [JsonIgnore]
    public string? SourcePath { get; set; }
}

public sealed class FeedCriteria
{
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("remote_only")]
    public bool RemoteOnly { get; set; }

    [JsonPropertyName("minimum_annual_compensation_usd")]
    public decimal? MinimumAnnualCompensationUsd { get; set; }

    [JsonPropertyName("max_jobs")]
    public int? MaxJobs { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = [];

    [JsonPropertyName("verification_policy")]
    public string? VerificationPolicy { get; set; }
}

public sealed class FeedVerificationSummary
{
    [JsonPropertyName("qualifying_jobs")]
    public int? QualifyingJobs { get; set; }

    [JsonPropertyName("duplicates_removed")]
    public int? DuplicatesRemoved { get; set; }

    [JsonPropertyName("applications_submitted")]
    public int? ApplicationsSubmitted { get; set; }

    [JsonPropertyName("employer_or_recruiter_contacts_made")]
    public int? EmployerOrRecruiterContactsMade { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
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

    [JsonPropertyName("posting_date")]
    public string? PostingDate { get; set; }

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
