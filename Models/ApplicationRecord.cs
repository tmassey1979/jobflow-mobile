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
}

public static class ApplicationStages
{
    public const string Interested = "Interested";
    public const string Preparing = "Preparing";
    public const string ReadyToApply = "Ready to apply";
    public const string Applied = "Applied";
    public const string Interviewing = "Interviewing";
    public const string Offer = "Offer";
    public const string Rejected = "Closed";

    public static IReadOnlyList<string> All { get; } =
    [
        Interested, Preparing, ReadyToApply, Applied, Interviewing, Offer, Rejected
    ];

    public static string Next(string? current)
    {
        var index = All.ToList().FindIndex(value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
        return All[(index + 1) % All.Count];
    }
}
