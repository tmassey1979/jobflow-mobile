using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class IntegrationsPage : ContentPage
{
    private readonly SupabaseDataService _supabaseService = new();
    private readonly GoogleCalendarService _googleCalendar = new();

    public IntegrationsPage()
    {
        InitializeComponent();
        _ = _supabaseService.ClearLegacyGitHubCredentialsAsync();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await RefreshSupabaseStateAsync();
        await RefreshGoogleStateAsync();
    }

    private async Task RefreshSupabaseStateAsync()
    {
        try
        {
            var email = await _supabaseService.GetEmailAsync();
            if (await _supabaseService.IsSignedInAsync())
            {
                SupabaseStateLabel.Text = "Connected";
                SupabaseDetailsLabel.Text = $"Signed in as {email}. Use Sync all on Jobs to refresh the cloud feed.";
                SupabaseEmailEntry.Text = email;
                DisconnectSupabaseButton.IsVisible = true;
            }
            else
            {
                SupabaseStateLabel.Text = "Not connected";
                SupabaseDetailsLabel.Text = "Sign in or create an account to connect your cloud account.";
                DisconnectSupabaseButton.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            SupabaseStateLabel.Text = "Check connection";
            SupabaseDetailsLabel.Text = SafeMessage(ex);
        }
    }

    private async Task RefreshGoogleStateAsync()
    {
        try
        {
            var clientId = await _googleCalendar.GetClientIdAsync();
            if (!string.IsNullOrWhiteSpace(clientId) && string.IsNullOrWhiteSpace(GoogleClientIdEntry.Text))
                GoogleClientIdEntry.Text = clientId;

            if (await _googleCalendar.IsConnectedAsync())
            {
                var email = await _googleCalendar.GetAccountEmailAsync();
                GoogleStateLabel.Text = "Connected";
                GoogleDetailsLabel.Text = string.IsNullOrWhiteSpace(email)
                    ? "Read-only calendar access is active. Open the Calendar tab to see events."
                    : $"Connected as {email}. Read-only · primary calendar.";
                DisconnectGoogleButton.IsVisible = true;
                ConnectGoogleButton.Text = "Reconnect";
            }
            else
            {
                GoogleStateLabel.Text = _googleCalendar.IsClientIdConfigured(clientId) ? "Not connected" : "Needs client ID";
                GoogleDetailsLabel.Text = _googleCalendar.IsClientIdConfigured(clientId)
                    ? "Tap Connect Google, approve calendar.readonly in the browser, then open the Calendar tab."
                    : "Paste your Google OAuth client ID below (see docs/INTEGRATIONS-SETUP.md), then Connect.";
                DisconnectGoogleButton.IsVisible = false;
                ConnectGoogleButton.Text = "Connect Google";
            }
        }
        catch (Exception ex)
        {
            GoogleStateLabel.Text = "Error";
            GoogleDetailsLabel.Text = SafeMessage(ex);
        }
    }

    private async void OnSupabaseSignInClicked(object sender, EventArgs e)
    {
        await RunSupabaseAuthAsync(async () =>
        {
            await _supabaseService.SignInAsync(SupabaseEmailEntry.Text ?? "", SupabasePasswordEntry.Text ?? "");
            SupabasePasswordEntry.Text = "";
            await RefreshSupabaseStateAsync();
        });
    }

    private async void OnSupabaseSignUpClicked(object sender, EventArgs e)
    {
        await RunSupabaseAuthAsync(async () =>
        {
            await _supabaseService.SignUpAsync(SupabaseEmailEntry.Text ?? "", SupabasePasswordEntry.Text ?? "");
            SupabasePasswordEntry.Text = "";
            await RefreshSupabaseStateAsync();
        });
    }

    private async Task RunSupabaseAuthAsync(Func<Task> action)
    {
        try
        {
            SupabaseStateLabel.Text = "Working…";
            SupabaseDetailsLabel.Text = "Connecting securely…";
            await action();
        }
        catch (Exception ex)
        {
            var message = SafeMessage(ex);
            SupabaseStateLabel.Text = message.Contains("confirm", StringComparison.OrdinalIgnoreCase)
                || message.Contains("confirmation", StringComparison.OrdinalIgnoreCase)
                ? "Check email"
                : "Not connected";
            SupabasePasswordEntry.Text = "";
            SupabaseDetailsLabel.Text = message;
            await DisplayAlertAsync("JobFlow Cloud", message, "OK");
        }
    }

    private async void OnSupabaseSignOutClicked(object sender, EventArgs e)
    {
        await _supabaseService.SignOutAsync();
        SupabasePasswordEntry.Text = "";
        await RefreshSupabaseStateAsync();
    }

    private async void OnConnectGoogleClicked(object sender, EventArgs e)
    {
        ConnectGoogleButton.IsEnabled = false;
        try
        {
            var typed = GoogleClientIdEntry.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(typed))
                await _googleCalendar.SetClientIdOverrideAsync(typed);

            GoogleStateLabel.Text = "Connecting…";
            GoogleDetailsLabel.Text = "Opening Google sign-in in the browser…";
            await _googleCalendar.ConnectAsync();
            await RefreshGoogleStateAsync();
            await DisplayAlertAsync(
                "Google Calendar connected",
                "Read-only access granted. Open the Calendar tab to see upcoming events. JobFlow will not create or change events.",
                "OK");
        }
        catch (Exception ex)
        {
            var message = SafeMessage(ex);
            GoogleStateLabel.Text = "Not connected";
            GoogleDetailsLabel.Text = message;
            await DisplayAlertAsync("Couldn't connect Google Calendar", message, "OK");
        }
        finally
        {
            ConnectGoogleButton.IsEnabled = true;
        }
    }

    private async void OnDisconnectGoogleClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Disconnect Google Calendar?",
            "Removes the Google session from this device. Local follow-ups are unchanged.",
            "Disconnect",
            "Cancel");
        if (!confirm) return;

        await _googleCalendar.DisconnectAsync();
        await RefreshGoogleStateAsync();
    }

    private async void OnOpenCalendarClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//calendar");
    }

    private static string SafeMessage(Exception ex)
    {
        var message = ex.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message)) return "The request could not be completed. Try again.";
        return message.Length > 280 ? message[..277] + "…" : message;
    }
}
