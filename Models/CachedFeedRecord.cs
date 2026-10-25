using SQLite;

namespace JobFlow.Mobile.Models;

/// <summary>
/// Stores only the last validated feed payload. Credentials are intentionally never persisted here.
/// </summary>
[Table("cached_feed")]
public sealed class CachedFeedRecord
{
    [PrimaryKey]
    public string Id { get; set; } = "latest";

    public string? GeneratedAt { get; set; }

    public string? SourcePath { get; set; }

    public string JsonPayload { get; set; } = "";

    public DateTime CachedAtUtc { get; set; }
}
