using System.Windows.Input;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class CalendarPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private readonly GoogleCalendarService _google = new();
    private bool _isRefreshing;

    public CalendarPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadCalendarAsync());
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadCalendarAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadCalendarAsync();

    private async void OnOpenTrackerClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//applications");

    private async Task LoadCalendarAsync()
    {
        IsRefreshing = true;
        RefreshButton.IsEnabled = false;
        try
        {
            var items = new List<CalendarListItem>();

            // Local follow-ups from Application Tracker
            try
            {
                var records = await _tracker.GetAllAsync();
                foreach (var r in records.Where(x => x.FollowUpAtUtc is not null))
                {
                    items.Add(new CalendarListItem
                    {
                        Kind = "Local",
                        Title = r.Title,
                        Subtitle = r.Company,
                        Detail = string.IsNullOrWhiteSpace(r.Notes)
                            ? r.FollowUpDisplay
                            : $"{r.FollowUpDisplay} · {r.Notes}",
                        WhenDisplay = r.FollowUpDisplay,
                        SortKey = r.FollowUpAtUtc ?? DateTimeOffset.MaxValue,
                        IsOverdue = r.IsFollowUpOverdue
                    });
                }
            }
            catch
            {
                // Keep Google events if local store fails.
            }

            var googleConnected = false;
            var googleCount = 0;
            try
            {
                googleConnected = await _google.IsConnectedAsync();
                if (googleConnected)
                {
                    var from = DateTimeOffset.UtcNow.AddDays(-7);
                    var to = DateTimeOffset.UtcNow.AddDays(60);
                    var events = await _google.GetUpcomingEventsAsync(from, to);
                    googleCount = events.Count;
                    foreach (var ev in events)
                    {
                        items.Add(new CalendarListItem
                        {
                            Kind = "Google",
                            Title = ev.Title,
                            Subtitle = string.IsNullOrWhiteSpace(ev.Location) ? "Primary calendar" : ev.Location,
                            Detail = null,
                            WhenDisplay = ev.WhenDisplay,
                            SortKey = ev.Start,
                            IsOverdue = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Could not load Google Calendar."
                    : ex.Message;
            }

            var ordered = items
                .OrderByDescending(i => i.IsOverdue)
                .ThenBy(i => i.SortKey)
                .ToList();

            EventsCollection.ItemsSource = ordered;

            var localCount = ordered.Count(i => i.Kind == "Local");
            if (googleConnected)
            {
                var email = await _google.GetAccountEmailAsync();
                StatusLabel.Text =
                    $"{localCount} local · {googleCount} Google" +
                    (string.IsNullOrWhiteSpace(email) ? "" : $" · {email}") +
                    " · read-only";
            }
            else
            {
                StatusLabel.Text = localCount == 0
                    ? "No local follow-ups. Connect Google Calendar on Integrations for events (read-only)."
                    : $"{localCount} local follow-up(s). Google Calendar not connected.";
            }
        }
        catch
        {
            StatusLabel.Text = "Could not load calendar.";
            EventsCollection.ItemsSource = null;
        }
        finally
        {
            IsRefreshing = false;
            RefreshButton.IsEnabled = true;
        }
    }
}
