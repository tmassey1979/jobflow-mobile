using SQLite;

namespace JobFlow.Mobile.Models;

/// <summary>
/// Local-only application progress. It contains no credentials and is never synced automatically.
/// </summary>
[Table("applications")]
public sealed class ApplicationRecord
{
    [PrimaryKey]
    public string JobId { get; set; } = "";

    public string Title { get; set; } = "";

    public string Company { get; set; } = "";

    public string? ApplicationUrl { get; set; }

    public string Status { get; set; } = ApplicationStages.Interested;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Joined from application_follow_ups at runtime; deliberately not stored in applications.
    [Ignore]
    public DateTime? FollowUpAtUtc { get; set; }

    [Ignore]
    public string FollowUpDisplay => FollowUpAtUtc is DateTime due
        ? $"Follow-up: {due.ToLocalTime():d}"
        : "No follow-up date set";
}
