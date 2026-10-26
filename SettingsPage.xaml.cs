using System.Text;
using System.Text.Json;
using JobFlow.Mobile.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace JobFlow.Mobile;

public partial class SettingsPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private readonly FeedCacheService _cache = new();

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var token = await GitHubAccountStore.GetAccessTokenAsync();
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            GitHubStatusLabel.Text = string.IsNullOrWhiteSpace(token)
                ? "Not signed in. Use Integrations to connect GitHub and choose a data repository."
                : $"Signed in · data repo {owner}/{repo}";

            var records = await _tracker.GetAllAsync();
            var feed = await _cache.LoadLatestAsync();
            LocalDataLabel.Text = feed is null
                ? $"{records.Count} tracked applications · no cached feed"
                : $"{records.Count} tracked applications · cached feed has {feed.Jobs.Count} jobs ({feed.SourcePath ?? "path unknown"})";
        }
        catch
        {
            GitHubStatusLabel.Text = "Could not read session or local storage.";
            LocalDataLabel.Text = "Local data status unavailable.";
        }
    }

    private async void OnOpenIntegrationsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//integrations");
    }

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Sign out of GitHub?",
            "Removes the GitHub session and selected repository from this device. Tracked applications stay local.",
            "Sign out",
            "Cancel");
        if (!confirm) return;

        try
        {
            await GitHubAccountStore.ClearSessionAsync();
            StatusLabel.Text = "Signed out.";
            await RefreshAsync();
        }
        catch
        {
            await DisplayAlertAsync("Couldn't sign out", "Secure Storage could not clear the session.", "OK");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var records = await _tracker.GetAllAsync();
            var payload = records.Select(r => new
            {
                r.JobId,
                r.Title,
                r.Company,
                r.Status,
                r.Notes,
                r.ApplicationUrl,
                follow_up_utc = r.FollowUpAtUtc,
                r.CreatedAtUtc,
                r.UpdatedAtUtc
            });
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(FileSystem.CacheDirectory, $"jobflow-applications-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);

            await Share.Default.ShareAsync(new ShareFileRequest
            {
                Title = "JobFlow applications export",
                File = new ShareFile(path)
            });
            StatusLabel.Text = $"Exported {records.Count} application records (no tokens).";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export failed", $"Could not export application records from this device. {ex.GetType().Name}", "OK");
        }
    }

    private async void OnClearCacheClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Clear cached job feed?",
            "Removes the offline job list cache only. Tracked applications and GitHub sign-in are kept.",
            "Clear cache",
            "Cancel");
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
