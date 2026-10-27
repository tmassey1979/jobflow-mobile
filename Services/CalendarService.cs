namespace JobFlow.Mobile.Services;

/// <summary>
/// Legacy placeholder retained for compatibility. Use <see cref="GoogleCalendarService"/> for OAuth and events.
/// </summary>
[Obsolete("Use GoogleCalendarService for read-only Google Calendar access.")]
public sealed class CalendarService
{
    private readonly GoogleCalendarService _google = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GoogleCalendarService.DefaultClientId)
        || _google.GetClientIdAsync().GetAwaiter().GetResult() is { Length: > 0 };

    public string SetupMessage =>
        "Connect Google Calendar from the Integrations tab (read-only). " +
        "Configure an OAuth client ID as described in docs/INTEGRATIONS-SETUP.md.";

    public Task<IReadOnlyList<GoogleCalendarService.CalendarEventSummary>> GetUpcomingEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        _google.GetUpcomingEventsAsync(from, to, cancellationToken);
}
