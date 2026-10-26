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
        _ = InitializeConnectionAsync();
    }

    private async Task InitializeConnectionAsync()
    {
        await RefreshConnectionStateAsync();
        await ResumePendingLoginAsync();
    }

    private async Task ResumePendingLoginAsync()
    {
        try
        {
            var existingToken = await GitHubAccountStore.GetAccessTokenAsync();
            if (!string.IsNullOrWhiteSpace(existingToken)) return;

            var pending = await GitHubAccountStore.GetPendingDeviceLoginAsync();
            if (pending is null) return;
            var remaining = (int)Math.Ceiling((pending.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds);
            if (remaining <= 0)
            {
                GitHubAccountStore.ClearPendingDeviceLogin();
                GitHubStateLabel.Text = "Sign-in expired";
                GitHubDetailsLabel.Text = "The pending GitHub approval expired. Tap Sign in with GitHub to start again.";
                return;
            }

            var clientId = GitHubAccountStore.DefaultClientId;
            var authorization = new GitHubAuthService.DeviceAuthorization(
                pending.DeviceCode, pending.UserCode, pending.VerificationUri,
                pending.IntervalSeconds, remaining);

            DeviceCodeLabel.Text = pending.UserCode;
            DeviceCodeLabel.IsVisible = true;
            CopyDeviceCodeButton.IsVisible = true;
            GitHubStateLabel.Text = "Finishing GitHub sign-in…";
            GitHubDetailsLabel.Text = $"Still waiting for approval of code {pending.UserCode}. Keep this screen open.";
            SignInGitHubButton.IsEnabled = false;

            var progress = new Progress<string>(message => MainThread.BeginInvokeOnMainThread(() => GitHubDetailsLabel.Text = message));
            var token = await _authService.WaitForAccessTokenAsync(clientId, authorization, progress);
            await GitHubAccountStore.SetAccessTokenAsync(token);
            GitHubAccountStore.ClearPendingDeviceLogin();

            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
            GitHubStateLabel.Text = "Signed in · choose repo";
            GitHubDetailsLabel.Text = "GitHub approval completed. Choose your data repository.";
            ChooseRepoButton.IsVisible = true;
            DisconnectGitHubButton.IsVisible = true;
            await ChooseAndSaveRepositoryAsync(token);
        }
        catch (Exception ex)
        {
            GitHubStateLabel.Text = "Sign-in needs retry";
            GitHubDetailsLabel.Text = UserSafeMessage(ex);
            GitHubAccountStore.ClearPendingDeviceLogin();
            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
        }
        finally
        {
            SignInGitHubButton.IsEnabled = true;
        }
    }

    private async Task RefreshConnectionStateAsync()
    {
        try
        {
            var token = await GitHubAccountStore.GetAccessTokenAsync();
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                GitHubStateLabel.Text = "Not signed in";
                GitHubDetailsLabel.Text = "Tap Sign in with GitHub, approve in the browser, then choose your data repository.";
                ChooseRepoButton.IsVisible = false;
                DisconnectGitHubButton.IsVisible = false;
                DeviceCodeLabel.IsVisible = false;
                CopyDeviceCodeButton.IsVisible = false;
                return;
            }

            GitHubStateLabel.Text = "Signed in";
            GitHubDetailsLabel.Text = $"Session saved on this device. Data repository: {owner}/{repo}. Use Choose data repository to change it.";
            ChooseRepoButton.IsVisible = true;
            DisconnectGitHubButton.IsVisible = true;
        }
        catch (Exception ex)
        {
            GitHubStateLabel.Text = "Storage unavailable";
            GitHubDetailsLabel.Text = $"Android Secure Storage error: {ex.GetType().Name}. Restart the app and try again.";
        }
    }

    private async void OnSignInGitHubClicked(object sender, EventArgs e)
    {
        var clientId = GitHubAccountStore.DefaultClientId;

        SignInGitHubButton.IsEnabled = false;
        _loginCts?.Cancel();
        _loginCts = new CancellationTokenSource();
        var cancellation = _loginCts.Token;

        try
        {
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
            }
            catch
            {
                // Optional; user can still read the code on screen.
            }

            GitHubStateLabel.Text = "Approve in browser";
            GitHubDetailsLabel.Text =
                $"Code {authorization.UserCode} — enter it at github.com/login/device if the browser does not show it. Keep JobFlow open while you approve.";

            // Persist before opening the browser; Android may pause/kill this activity.
            await GitHubAccountStore.SavePendingDeviceLoginAsync(new GitHubAccountStore.PendingDeviceLogin(
                authorization.DeviceCode, authorization.UserCode, authorization.VerificationUri,
                authorization.IntervalSeconds, DateTimeOffset.UtcNow.AddSeconds(authorization.ExpiresInSeconds)));

            try
            {
                await Launcher.Default.OpenAsync(authorization.VerificationUri);
            }
            catch
            {
                GitHubDetailsLabel.Text =
                    $"Open github.com/login/device and enter code {authorization.UserCode}. Keep JobFlow open.";
            }

            var progress = new Progress<string>(message => MainThread.BeginInvokeOnMainThread(() => GitHubDetailsLabel.Text = message));
            var accessToken = await _authService.WaitForAccessTokenAsync(clientId, authorization, progress, cancellation);
            await GitHubAccountStore.SetAccessTokenAsync(accessToken);
            GitHubAccountStore.ClearPendingDeviceLogin();

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
            GitHubAccountStore.ClearPendingDeviceLogin();
            GitHubStateLabel.Text = "Sign-in canceled";
            GitHubDetailsLabel.Text = "GitHub sign-in was canceled.";
            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
        }
        catch (Exception ex)
        {
            // Keep pending login if we are still within the approval window and the failure
            // looks transient; otherwise clear so the user can start clean.
            var message = UserSafeMessage(ex);
            if (message.Contains("expired", StringComparison.OrdinalIgnoreCase)
                || message.Contains("denied", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Device Flow", StringComparison.OrdinalIgnoreCase))
            {
                GitHubAccountStore.ClearPendingDeviceLogin();
            }

            _gitHubConnection.MarkFailed("Sign-in failed.");
            GitHubStateLabel.Text = "Sign-in failed";
            GitHubDetailsLabel.Text = message;
            DeviceCodeLabel.IsVisible = false;
            CopyDeviceCodeButton.IsVisible = false;
            await DisplayAlertAsync("Couldn't complete GitHub sign-in", message, "OK");
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
            GitHubDetailsLabel.Text = "Activation code copied. Paste it on github.com/login/device.";
        }
        catch
        {
            await DisplayAlertAsync("Could not copy code", "Clipboard was unavailable. Type the code shown above into GitHub.", "OK");
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
            CopyDeviceCodeButton.IsVisible = false;
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

    private static string UserSafeMessage(Exception ex)
    {
        var text = ex.Message?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return "GitHub sign-in failed. Try again.";

        // Never surface tokens or long opaque payloads.
        if (text.Contains("gho_", StringComparison.OrdinalIgnoreCase)
            || text.Contains("access_token", StringComparison.OrdinalIgnoreCase))
            return "GitHub sign-in failed while saving the session. Try again.";

        if (text.Length > 280)
            text = text[..277] + "…";

        return text;
    }
}
