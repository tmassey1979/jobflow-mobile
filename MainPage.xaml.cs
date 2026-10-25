using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class MainPage : ContentPage
{
    private const string TokenStorageKey = "jobflow.github.readonly.token";
    private readonly GitHubDataService _dataService = new();
    private readonly FeedCacheService _cacheService = new();

    public MainPage()
    {
        InitializeComponent();
        _ = RestoreSavedTokenAsync();
        _ = LoadCachedFeedAsync();
    }

    private async Task RestoreSavedTokenAsync()
    {
        try
        {
            TokenEntry.Text = await SecureStorage.Default.GetAsync(TokenStorageKey);
        }
        catch
        {
            // Secure storage may be unavailable on some emulator/device configurations.
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
            StatusLabel.Text = $"Showing {cached.Jobs.Count} cached jobs from {cached.SourcePath ?? "the last successful sync"}. Connect to refresh.";
        }
        catch
        {
            // A broken or unavailable cache must not prevent online use.
        }
    }

    private async void OnLoadJobsClicked(object sender, EventArgs e)
    {
        var token = TokenEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            try { token = await SecureStorage.Default.GetAsync(TokenStorageKey); }
            catch { /* A clear prompt is shown below if secure storage is unavailable. */ }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("GitHub token needed", "Enter a fine-grained token with read-only Contents access to tmassey1979/Jobflow-data. You can also connect it on the Integrations tab.", "OK");
            return;
        }

        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Connecting to GitHub…";
        try
        {
            // A successful validated response is displayed immediately; cache failure never loses the online result.
            var feed = await _dataService.LoadLatestFeedAsync(token);
            JobsCollection.ItemsSource = feed.Jobs;
            FeedDateLabel.Text = $"Feed generated: {feed.GeneratedAt ?? "date not supplied"}";
            StatusLabel.Text = $"{feed.Jobs.Count} unique jobs loaded. Feed data is read-only.";

            try
            {
                await _cacheService.SaveLatestAsync(feed);
            }
            catch
            {
                StatusLabel.Text += " This refresh is available now, but the local cache could not be updated.";
            }

            try
            {
                await SecureStorage.Default.SetAsync(TokenStorageKey, token);
            }
            catch
            {
                StatusLabel.Text += " Android Secure Storage could not save the token.";
            }
            TokenEntry.Text = string.Empty;
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
            await DisplayAlertAsync("JobFlow couldn't refresh", "GitHub access or the feed could not be verified. If a saved feed exists, it remains available offline. Check the token, repository, network, and feed structure.", "OK");
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
