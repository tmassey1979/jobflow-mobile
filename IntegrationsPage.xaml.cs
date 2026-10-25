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
            await DisplayAlert("GitHub token needed",
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
            _gitHubConnection.MarkConnected(new[] { "Private repository Contents: read-only" }, DateTimeOffset.UtcNow);
            GitHubTokenEntry.Text = string.Empty;
            GitHubStateLabel.Text = "Connected · read-only";
            GitHubDetailsLabel.Text = $"Connection verified. Found {feed.Jobs.Count} jobs in the latest dated feed ({feed.GeneratedAt ?? "generation date not supplied"}). Last successful test: {DateTimeOffset.Now:g}.";
            DisconnectGitHubButton.IsVisible = true;
        }
        catch (Exception ex)
        {
            _gitHubConnection.MarkFailed("Connection failed. Check token access, repository, feed, or network.");
            GitHubStateLabel.Text = "Connection failed";
            GitHubDetailsLabel.Text = _gitHubConnection.ErrorMessage ?? "Connection failed. Try again.";
            await DisplayAlert("Couldn't connect to GitHub", ex.Message, "OK");
        }
        finally
        {
            ConnectGitHubButton.IsEnabled = true;
        }
    }

    private async void OnDisconnectGitHubClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
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
            await DisplayAlert("Couldn't disconnect", "Android Secure Storage could not remove the saved token. Check device security settings and try again.", "OK");
        }
    }
}