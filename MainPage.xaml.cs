using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class MainPage : ContentPage
{
    private const string TokenStorageKey = "jobflow.github.readonly.token";
    private readonly GitHubDataService _dataService = new();

    public MainPage()
    {
        InitializeComponent();
        _ = RestoreSavedTokenAsync();
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

    private async void OnLoadJobsClicked(object sender, EventArgs e)
    {
        var token = TokenEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("GitHub token needed", "Enter a fine-grained token with read-only Contents access to tmassey1979/Jobflow-data.", "OK");
            return;
        }

        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Connecting to GitHub…";
        try
        {
            // Validate access and feed structure before persisting credentials.
            var feed = await _dataService.LoadLatestFeedAsync(token);
            JobsCollection.ItemsSource = feed.Jobs;
            FeedDateLabel.Text = $"Feed generated: {feed.GeneratedAt ?? "date not supplied"} · Source: {feed.SourcePath ?? "unknown"}";
            StatusLabel.Text = $"{feed.Jobs.Count} unique jobs loaded. Feed data is read-only.";

            try
            {
                await SecureStorage.Default.SetAsync(TokenStorageKey, token);
            }
            catch
            {
                StatusLabel.Text += " This session works, but Android Secure Storage could not save the token.";
            }
            TokenEntry.Text = string.Empty;
        }
        catch (Exception)
        {
            StatusLabel.Text = "Could not load the feed.";
            await DisplayAlertAsync("JobFlow couldn't connect", "Access could not be verified. Check the token permissions, repository, network, and feed structure. Technical details are hidden to avoid exposing sensitive response data.", "OK");
        }
        finally
        {
            LoadButton.IsEnabled = true;
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