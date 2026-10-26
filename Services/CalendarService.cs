namespace JobFlow.Mobile.Services;

/// <summary>
/// Placeholder for Google Calendar read-only integration.
/// Real OAuth + event listing will be added once an Android OAuth client ID
/// with calendar.readonly scope is configured (see docs/INTEGRATIONS-SETUP.md).
/// This service never writes calendar events and never submits applications.
/// </summary>
public sealed class CalendarService
{
    public bool IsConfigured => false;

    public string SetupMessage =>
        "Google Calendar is not configured yet. " +
        "Add an Android OAuth client (calendar.readonly) and wire the client ID, " +
        "then reconnect from the Integrations tab. Until then, interview dates " +
        "can still be stored as local follow-ups on tracked applications.";

    public Task<IReadOnlyList<CalendarEventSummary>> GetUpcomingEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Intentionally empty until OAuth is configured.
        return Task.FromResult<IReadOnlyList<CalendarEventSummary>>(Array.Empty<CalendarEventSummary>());
    }

    public sealed class CalendarEventSummary
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public DateTimeOffset Start { get; init; }
        public DateTimeOffset? End { get; init; }
        public string? Location { get; init; }
        public bool IsAllDay { get; init; }
    }
}
