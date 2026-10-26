namespace JobFlow.Mobile;

public partial class CalendarPage : ContentPage
{
    public CalendarPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Future: load connected Google account + events from a CalendarService.
        StatusLabel.Text = "Google Calendar is not connected yet. Connect it from Integrations when ready.";
        RefreshButton.IsEnabled = false;
        EventsCollection.IsVisible = false;
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync(
            "Calendar not connected",
            "Configure Google OAuth on the Integrations tab first. Calendar access will be read-only by default.",
            "OK");
    }
}
