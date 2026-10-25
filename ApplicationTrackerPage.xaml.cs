using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class ApplicationTrackerPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();

    public ApplicationTrackerPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var records = await _tracker.GetAllAsync();
            ApplicationsCollection.ItemsSource = records;
            SummaryLabel.Text = records.Count == 0
                ? "No applications tracked yet."
                : $"{records.Count} tracked · {records.Count(record => record.Status == Models.ApplicationStages.Applied)} marked applied · saved locally";
        }
        catch
        {
            await DisplayAlertAsync("Tracker unavailable", "JobFlow could not read the local application tracker. Your job feed is separate and has not been changed.", "OK");
        }
    }

    private async void OnAdvanceStageClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string jobId }) return;
        try
        {
            await _tracker.AdvanceAsync(jobId);
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't update stage", "The application stage could not be saved locally. Please try again.", "OK");
        }
    }

    private async void OnRemoveClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string jobId }) return;
        var confirm = await DisplayAlertAsync("Remove from tracker?", "This removes only the local tracking record. It does not delete the job feed entry or contact the employer.", "Remove", "Cancel");
        if (!confirm) return;

        try
        {
            await _tracker.RemoveAsync(jobId);
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't remove record", "The local application record could not be removed. Please try again.", "OK");
        }
    }

    private async void OnOpenApplicationClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url }
            && Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps)
        {
            await Launcher.Default.OpenAsync(parsed);
        }
        else
        {
            await DisplayAlertAsync("Application link unavailable", "This tracked job does not have a valid HTTPS application URL.", "OK");
        }
    }
}
