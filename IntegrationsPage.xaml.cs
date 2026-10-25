using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class IntegrationsPage : ContentPage
{
    private const string TokenStorageKey = "jobflow.github.readonly.token";
    private readonly GitHubDataService _dataService = new();
    private readonly IntegrationConnection _gitHubConnection = new("github", "GitHub");

    public IntegrationsPage()
    {
        InitializeComponent();
        _ = RefreshConnectionStateAsync();
    }

    private async Task RefreshConnectionStateAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenStorageKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                GitHubStateLabel.Text = "Not connected";
                GitHubDetailsLabel.Text = "No GitHub token is stored on this device.";
                DisconnectGitHubButton.IsVisible = false;
                return;
            }

            _gitHubConnection.MarkNeedsReauthentication("Stored token has not been tested in this session.");
            GitHubStateLabel.Text = "Saved · needs test";
            GitHubDetailsLabel.Text = "A token is stored on this device. Test the connection to confirm access is still valid.";
            DisconnectGitHubButton.IsVisible = true;
        }
        catch
        {
            GitHubStateLabel.Text = "Storage unavailable";
            GitHubDetailsLabel.Text = "Android Secure Storage could not be accessed. Check the device security settings and try again.";
        }
    }

    private async void OnConnectGitHubClicked(object sender, EventArgs e)
    {
        var token = GitHubTokenEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            try { token = await SecureStorage.Default.GetAsync(TokenStorageKey); }
            catch { /* Report a clear message below if no token can be retrieved. */ }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("GitHub token needed",
                "Create a fine-grained token limited to tmassey1979/Jobflow-data with Contents: Read-only, then paste it here. Never paste it into chat, an issue, or a commit.",
                "OK");
            return;
        }

        ConnectGitHubButton.IsEnabled = false;
        _gitHubConnection.BeginConnect();
        GitHubStateLabel.Text = "Connecting…";
        GitHubDetailsLabel.Text = "Testing private repository access…";
        try
        {
            var feed = await _dataService.LoadLatestFeedAsync(token);
            await SecureStorage.Default.SetAsync(TokenStorageKey, token);
            _gitHubConnection.MarkConnected(new[] { "Latest feed read succeeded; token scopes are not introspected by the app" }, DateTimeOffset.UtcNow);
            GitHubTokenEntry.Text = string.Empty;
            GitHubStateLabel.Text = "Connected · feed read tested";
            GitHubDetailsLabel.Text = $"Feed read verified. Found {feed.Jobs.Count} jobs in the latest dated feed ({feed.GeneratedAt ?? "generation date not supplied"}). Last successful test: {DateTimeOffset.Now:g}.";
            DisconnectGitHubButton.IsVisible = true;
        }
        catch (Exception)
        {
            _gitHubConnection.MarkFailed("Connection failed. Check token access, repository, feed, or network.");
            GitHubStateLabel.Text = "Connection failed";
            GitHubDetailsLabel.Text = _gitHubConnection.ErrorMessage ?? "Connection failed. Try again.";
            await DisplayAlertAsync("Couldn't connect to GitHub", "Access could not be verified. Check the token permissions, repository selection, network, and feed structure. Technical details are hidden to avoid exposing sensitive response data.", "OK");
        }
        finally
        {
            ConnectGitHubButton.IsEnabled = true;
        }
    }

    private async void OnDisconnectGitHubClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Disconnect GitHub?",
            "This removes the saved GitHub token from this device. It does not delete your repository or any job data.",
            "Disconnect",
            "Cancel");
        if (!confirm) return;

        try
        {
            SecureStorage.Default.Remove(TokenStorageKey);
            _gitHubConnection.Disconnect();
            GitHubTokenEntry.Text = string.Empty;
            GitHubStateLabel.Text = "Not connected";
            GitHubDetailsLabel.Text = "The saved GitHub token was removed from this device.";
            DisconnectGitHubButton.IsVisible = false;
        }
        catch
        {
            await DisplayAlertAsync("Couldn't disconnect", "Android Secure Storage could not remove the saved token. Check device security settings and try again.", "OK");
        }
    }
}