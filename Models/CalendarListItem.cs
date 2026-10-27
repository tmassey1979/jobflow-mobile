namespace JobFlow.Mobile.Models;

/// <summary>
/// Unified row for the Calendar tab: local application follow-ups or Google Calendar events.
/// </summary>
public sealed class CalendarListItem
{
    public required string Kind { get; init; } // "Local" or "Google"
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Detail { get; init; }
    public required string WhenDisplay { get; init; }
    public DateTimeOffset SortKey { get; init; }
    public bool IsOverdue { get; init; }
}
