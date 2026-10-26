using System.Windows.Input;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class CalendarPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private bool _isRefreshing;

    public CalendarPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadLocalFollowUpsAsync());
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
        _ = LoadLocalFollowUpsAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e) =>
        await LoadLocalFollowUpsAsync();

    private async void OnOpenTrackerClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//applications");
    }

    private async Task LoadLocalFollowUpsAsync()
    {
        IsRefreshing = true;
        RefreshButton.IsEnabled = false;
        try
        {
            var records = await _tracker.GetAllAsync();
            var withDates = records
                .Where(r => r.FollowUpAtUtc is not null)
                .OrderByDescending(r => r.IsFollowUpOverdue)
                .ThenByDescending(r => r.IsFollowUpDueSoon)
                .ThenBy(r => r.FollowUpAtUtc)
                .ToList();

            EventsCollection.ItemsSource = withDates;

            var overdue = withDates.Count(r => r.IsFollowUpOverdue);
            var dueSoon = withDates.Count(r => r.IsFollowUpDueSoon);
            StatusLabel.Text = withDates.Count == 0
                ? "No local follow-up dates yet. Set them on Applications. Google Calendar can be added later."
                : $"{withDates.Count} local follow-up(s) · {overdue} overdue · {dueSoon} due soon. Google Calendar not connected.";
        }
        catch
        {
            StatusLabel.Text = "Could not read local follow-ups.";
            EventsCollection.ItemsSource = null;
        }
        finally
        {
            IsRefreshing = false;
            RefreshButton.IsEnabled = true;
        }
    }
}
