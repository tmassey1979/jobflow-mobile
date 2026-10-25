using SQLite;

namespace JobFlow.Mobile.Models;

/// <summary>
/// A local-only follow-up date for a tracked application. Dates are stored as UTC instants.
/// </summary>
[Table("application_follow_ups")]
public sealed class ApplicationFollowUpRecord
{
    [PrimaryKey]
    public string JobId { get; set; } = "";

    public DateTime DueAtUtc { get; set; }
}
