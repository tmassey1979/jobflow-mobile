namespace JobFlow.Mobile.Services;

/// <summary>
/// Compatibility wrapper. Prefer <see cref="GoogleCalendarService"/>.
/// </summary>
public sealed class CalendarService
{
    private readonly GoogleCalendarService _google = new();

    public string SetupMessage =>
        "Connect Google Calendar from the Integrations tab (read-only). " +
        "Configure an OAuth client ID as described in docs/INTEGRATIONS-SETUP.md.";

    public Task<IReadOnlyList<GoogleCalendarService.CalendarEventSummary>> GetUpcomingEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        _google.GetUpcomingEventsAsync(from, to, cancellationToken);
}
