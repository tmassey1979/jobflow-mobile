using SQLite;

namespace JobFlow.Mobile.Models;

/// <summary>
/// A locally cached copy of a validated feed job. Imported source facts are separate
/// from application notes and are keyed case-insensitively by the feed's stable job_id.
/// </summary>
[Table("cached_jobs")]
public sealed class CachedJobRecord
{
    [PrimaryKey]
    public string JobId { get; set; } = "";

    public string SourceJobId { get; set; } = "";

    public string Title { get; set; } = "";

    public string Company { get; set; } = "";

    public decimal? SalaryMinUsd { get; set; }

    public decimal? SalaryMaxUsd { get; set; }

    public string? SalaryPayType { get; set; }

    public string? RemoteScope { get; set; }

    public string? VerifiedAt { get; set; }

    public string? PostingDate { get; set; }

    public string? ApplicationUrl { get; set; }

    public decimal? MatchScore { get; set; }

    public string? MetadataPath { get; set; }

    public string? ResumePath { get; set; }

    public string? CoverLetterPath { get; set; }

    public string? ApplicationTrackingPath { get; set; }

    public string? FeedSourcePath { get; set; }

    public string? FeedGeneratedAt { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}
