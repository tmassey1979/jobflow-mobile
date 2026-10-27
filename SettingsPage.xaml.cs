using System.Text.Json;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class SettingsPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private readonly FeedCacheService _cache = new();
    private readonly SupabaseDataService _supabase = new();

    public SettingsPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var email = await _supabase.GetEmailAsync();
            CloudStatusLabel.Text = await _supabase.IsSignedInAsync()
                ? $"Signed in · {email}"
                : "Not signed in. Connect on the Integrations tab.";
            var records = await _tracker.GetAllAsync();
            var feed = await _cache.LoadLatestAsync();
            LocalDataLabel.Text = feed is null
                ? $"{records.Count} tracked applications · no cached feed"
                : $"{records.Count} tracked applications · cached feed has {feed.Jobs.Count} jobs";
        }
        catch
        {
            CloudStatusLabel.Text = "Cloud account status unavailable.";
            LocalDataLabel.Text = "Local data status unavailable.";
        }
    }

    private async void OnOpenIntegrationsClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//integrations");

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var records = await _tracker.GetAllAsync();
            var payload = records.Select(r => new
            {
                r.JobId, r.Title, r.Company, r.Status, r.Notes, r.ApplicationUrl,
                follow_up_utc = r.FollowUpAtUtc, r.CreatedAtUtc, r.UpdatedAtUtc
            });
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await Clipboard.Default.SetTextAsync(json);
            StatusLabel.Text = $"Copied {records.Count} application records to the clipboard (no credentials).";
            await DisplayAlertAsync("Export ready", $"{records.Count} application records were copied as JSON. Credentials are never included.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Export failed", "Could not export application records from this device.", "OK");
        }
    }

    private async void OnClearCacheClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync("Clear cached job feed?", "Removes the offline job list cache only. Tracked applications and your cloud session stay intact.", "Clear cache", "Cancel");
        if (!confirm) return;
        try
        {
            await _cache.ClearCachedFeedAsync();
            StatusLabel.Text = "Cached job feed cleared.";
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't clear cache", "The local feed cache could not be cleared.", "OK");
        }
    }
}
