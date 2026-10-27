using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class IntegrationsPage : ContentPage
{
    private readonly SupabaseDataService _supabaseService = new();

    public IntegrationsPage()
    {
        InitializeComponent();
        _ = RefreshSupabaseStateAsync();
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
