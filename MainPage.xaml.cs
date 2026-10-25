using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class MainPage : ContentPage
{
    private readonly GitHubDataService _dataService = new();
    private readonly FeedCacheService _cacheService = new();
    private readonly ApplicationTrackerService _applicationTracker = new();

    public MainPage()
    {
        InitializeComponent();
        _ = RefreshConnectionSummaryAsync();
        _ = LoadCachedFeedAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshConnectionSummaryAsync();
    }

    private async Task RefreshConnectionSummaryAsync()
    {
        try
        {
            var token = await GitHubAccountStore.GetAccessTokenAsync();
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            ConnectionSummaryLabel.Text = string.IsNullOrWhiteSpace(token)
                ? "Sign in with GitHub on the Integrations tab, then choose your data repository."
                : $"Signed in · reading {owner}/{repo}. Change this on the Integrations tab.";
        }
        catch
        {
            ConnectionSummaryLabel.Text = "Sign in with GitHub on the Integrations tab.";
        }
    }

    private async Task LoadCachedFeedAsync()
    {
        try
        {
            var cached = await _cacheService.LoadLatestAsync();
            if (cached is null) return;

            JobsCollection.ItemsSource = cached.Jobs;
            FeedDateLabel.Text = $"Feed generated: {cached.GeneratedAt ?? "date not supplied"}";
            StatusLabel.Text = $"Showing {cached.Jobs.Count} cached jobs from {cached.SourcePath ?? "the last successful sync"}. Pull a fresh feed when online.";
        }
        catch
        {
            // A broken or unavailable cache must not prevent online use.
        }
    }

    private async void OnLoadJobsClicked(object sender, EventArgs e)
    {
        var token = await GitHubAccountStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync(
                "Sign in required",
                "Open the Integrations tab, sign in with GitHub, and choose your data repository. You do not paste a token.",
                "OK");
            return;
        }

        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Loading latest feed from GitHub…";
        try
        {
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            var feed = await _dataService.LoadLatestFeedAsync(token, owner, repo);
            JobsCollection.ItemsSource = feed.Jobs;
            FeedDateLabel.Text = $"Feed generated: {feed.GeneratedAt ?? "date not supplied"}";
            StatusLabel.Text = $"{feed.Jobs.Count} unique jobs from {owner}/{repo}. Feed data is read-only.";

            try { await _cacheService.SaveLatestAsync(feed); }
            catch { StatusLabel.Text += " Local cache could not be updated."; }
        }
        catch (Exception)
        {
            var cached = await TryLoadCachedFeedAsync();
            if (cached is not null)
            {
                JobsCollection.ItemsSource = cached.Jobs;
                FeedDateLabel.Text = $"Feed generated: {cached.GeneratedAt ?? "date not supplied"}";
                StatusLabel.Text = $"Offline: showing {cached.Jobs.Count} cached jobs. GitHub refresh failed.";
            }
            await DisplayAlertAsync(
                "JobFlow couldn't refresh",
                "GitHub access or the feed could not be verified. If a saved feed exists, it remains available offline. Check sign-in, repository choice, network, and feed structure.",
                "OK");
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private async Task<DailyFeed?> TryLoadCachedFeedAsync()
    {
        try { return await _cacheService.LoadLatestAsync(); }
        catch { return null; }
    }

    private async void OnTrackApplicationClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        try
        {
            var record = await _applicationTracker.TrackAsync(job);
            await DisplayAlertAsync("Application tracker", $"Saved locally. Current stage: {record.Status}.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Couldn't track job", "JobFlow could not save this job to the local application tracker.", "OK");
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
            await DisplayAlertAsync("Application link unavailable", "This job does not have a valid HTTPS application URL.", "OK");
        }
    }
}
