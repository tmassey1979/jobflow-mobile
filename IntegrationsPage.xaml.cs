using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace JobFlow.Mobile;

public partial class IntegrationsPage : ContentPage
{
    private readonly GitHubDataService _dataService = new();
    private readonly GitHubAuthService _authService = new();
    private readonly IntegrationConnection _gitHubConnection = new("github", "GitHub");
    private CancellationTokenSource? _loginCts;

    public IntegrationsPage()
    {
        InitializeComponent();
        _ = RefreshConnectionStateAsync();
    }

    private async Task RefreshConnectionStateAsync()
    {
        try
        {
            ClientIdEntry.Text = await GitHubAccountStore.GetClientIdAsync();

            var token = await GitHubAccountStore.GetAccessTokenAsync();
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                GitHubStateLabel.Text = "Not signed in";
                GitHubDetailsLabel.Text = "Tap Sign in with GitHub, approve in the browser, then choose your data repository.";
                ChooseRepoButton.IsVisible = false;
                DisconnectGitHubButton.IsVisible = false;
                DeviceCodeLabel.IsVisible = false;
                return;
            }

            GitHubStateLabel.Text = "Signed in";
            GitHubDetailsLabel.Text = $"Session saved on this device. Data repository: {owner}/{repo}. Use Choose data repository to change it.";
            ChooseRepoButton.IsVisible = true;
            DisconnectGitHubButton.IsVisible = true;
        }
        catch
        {
            GitHubStateLabel.Text = "Storage unavailable";
            GitHubDetailsLabel.Text = "Android Secure Storage could not be accessed.";
        }
    }

    private async void OnSignInGitHubClicked(object sender, EventArgs e)
    {
        var clientId = ClientIdEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = await GitHubAccountStore.GetClientIdAsync();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            await DisplayAlertAsync(
                "OAuth App client ID needed",
                "Create a GitHub OAuth App (github.com/settings/developers), enable Device Flow, and paste the public Client ID here. No client secret is used.",
                "OK");
            return;
        }

        SignInGitHubButton.IsEnabled = false;
        _loginCts?.Cancel();
        _loginCts = new CancellationTokenSource();
        var cancellation = _loginCts.Token;

        try
        {
            await GitHubAccountStore.SetClientIdAsync(clientId);
            _gitHubConnection.BeginConnect();
            GitHubStateLabel.Text = "Starting GitHub login…";
            GitHubDetailsLabel.Text = "Requesting a one-time code from GitHub…";

            var authorization = await _authService.StartDeviceLoginAsync(clientId, cancellation);
            DeviceCodeLabel.Text = authorization.UserCode;
            DeviceCodeLabel.IsVisible = true;
            CopyDeviceCodeButton.IsVisible = true;
            try
            {
                await Clipboard.Default.SetTextAsync(authorization.UserCode);
                GitHubDetailsLabel.Text = "Activation code copied. Enter it at GitHub in the browser to approve access.";
            }
            catch
            {
                GitHubDetailsLabel.Text = "Copy the activation code below, then enter it at GitHub in the browser to approve access.";
            }
            GitHubStateLabel.Text = "Approve in browser";
            GitHubDetailsLabel.Text =
                $"Enter code {authorization.UserCode} at {authorization.VerificationUri}, then approve access. JobFlow waits here automatically.";

            try
            {
                await Launcher.Default.OpenAsync(authorization.VerificationUri);
            }
            catch
            {
                // User can open the URL manually from the label text.
            }

            var progress = new Progress<string>(message => GitHubDetailsLabel.Text = message);
            var accessToken = await _authService.WaitForAccessTokenAsync(clientId, authorization, progress, cancellation);
            await GitHubAccountStore.SetAccessTokenAsync(accessToken);

            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
            GitHubStateLabel.Text = "Signed in · choose repo";
            GitHubDetailsLabel.Text = "GitHub approved the sign-in. Next, choose which repository holds your job feed.";
            ChooseRepoButton.IsVisible = true;
            DisconnectGitHubButton.IsVisible = true;

            await ChooseAndSaveRepositoryAsync(accessToken);
        }
        catch (OperationCanceledException)
        {
            GitHubStateLabel.Text = "Sign-in canceled";
            GitHubDetailsLabel.Text = "GitHub sign-in was canceled.";
            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
        }
        catch (Exception)
        {
            _gitHubConnection.MarkFailed("Sign-in failed.");
            GitHubStateLabel.Text = "Sign-in failed";
            GitHubDetailsLabel.Text = "GitHub sign-in did not complete. Check that Device Flow is enabled on the OAuth App and try again.";
            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
            await DisplayAlertAsync("Couldn't sign in", "GitHub login failed. Confirm the OAuth App has Device Flow enabled and try again.", "OK");
        }
        finally
        {
            SignInGitHubButton.IsEnabled = true;
        }
    }

    private async void OnCopyDeviceCodeClicked(object sender, EventArgs e)
    {
        var code = DeviceCodeLabel.Text;
        if (string.IsNullOrWhiteSpace(code)) return;

        try
        {
            await Clipboard.Default.SetTextAsync(code);
            GitHubDetailsLabel.Text = "Activation code copied. Return to the GitHub browser page and paste it there.";
        }
        catch
        {
            await DisplayAlertAsync("Could not copy code", "Your device clipboard was unavailable. You can read the activation code displayed above and enter it manually.", "OK");
        }
    }

    private async void OnChooseRepoClicked(object sender, EventArgs e)
    {
        var token = await GitHubAccountStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("Sign in required", "Sign in with GitHub before choosing a repository.", "OK");
            return;
        }

        ChooseRepoButton.IsEnabled = false;
        try
        {
            await ChooseAndSaveRepositoryAsync(token);
        }
        finally
        {
            ChooseRepoButton.IsEnabled = true;
        }
    }

    private async Task ChooseAndSaveRepositoryAsync(string accessToken)
    {
        GitHubDetailsLabel.Text = "Loading repositories you can access…";
        IReadOnlyList<GitHubAuthService.GitHubRepositoryRef> repos;
        try
        {
            repos = await _authService.ListRepositoriesAsync(accessToken);
        }
        catch
        {
            await DisplayAlertAsync("Couldn't list repositories", "GitHub did not return your repository list. Sign in again.", "OK");
            return;
        }

        if (repos.Count == 0)
        {
            await DisplayAlertAsync("No repositories", "No repositories were returned for this GitHub account.", "OK");
            return;
        }

        var options = repos.Take(12).Select(r => r.FullName).ToArray();
        var selected = await DisplayActionSheetAsync("Choose data repository", "Cancel", null, options);
        if (string.IsNullOrWhiteSpace(selected) || string.Equals(selected, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            var current = await GitHubAccountStore.GetSelectedRepositoryAsync();
            GitHubDetailsLabel.Text = $"Still using {current.Owner}/{current.Name}.";
            return;
        }

        var match = repos.First(r => string.Equals(r.FullName, selected, StringComparison.OrdinalIgnoreCase));
        await GitHubAccountStore.SetSelectedRepositoryAsync(match.Owner, match.Name);

        GitHubDetailsLabel.Text = $"Testing read access to {match.FullName}…";
        try
        {
            var feed = await _dataService.LoadLatestFeedAsync(accessToken, match.Owner, match.Name);
            _gitHubConnection.MarkConnected(new[] { "repo (read selected data repository)" }, DateTimeOffset.UtcNow);
            GitHubStateLabel.Text = "Connected";
            GitHubDetailsLabel.Text =
                $"Using {match.FullName}. Feed OK — {feed.Jobs.Count} jobs from {feed.SourcePath ?? "daily feed"}.";
        }
        catch
        {
            GitHubStateLabel.Text = "Repo selected · feed issue";
            GitHubDetailsLabel.Text =
                $"{match.FullName} was saved, but a JobFlow daily feed could not be read yet. Confirm the repo has daily/YYYY-MM-DD.json files.";
            await DisplayAlertAsync(
                "Repository saved",
                $"{match.FullName} is selected. JobFlow could not read a dated feed from its daily folder yet.",
                "OK");
        }
    }

    private async void OnDisconnectGitHubClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Sign out of GitHub?",
            "This removes the GitHub session and selected repository from this device only.",
            "Sign out",
            "Cancel");
        if (!confirm) return;

        _loginCts?.Cancel();
        try
        {
            await GitHubAccountStore.ClearSessionAsync();
            _gitHubConnection.Disconnect();
            DeviceCodeLabel.IsVisible = false;
            ChooseRepoButton.IsVisible = false;
            DisconnectGitHubButton.IsVisible = false;
            GitHubStateLabel.Text = "Not signed in";
            GitHubDetailsLabel.Text = "GitHub session removed from this device.";
        }
        catch
        {
            await DisplayAlertAsync("Couldn't sign out", "Secure Storage could not clear the session.", "OK");
        }
    }
}
