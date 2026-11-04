using System.Text;
using System.Text.Json;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class SettingsPage : ContentPage
{
    private readonly ApplicationTrackerService _tracker = new();
    private readonly FeedCacheService _cache = new();
    private readonly SupabaseDataService _supabase = new();
    private string? _masterResumeSourceFile;

    public SettingsPage() => InitializeComponent();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var email = await _supabase.GetEmailAsync();
            var signedIn = await _supabase.IsSignedInAsync();
            CloudStatusLabel.Text = signedIn
                ? $"Signed in · {email}"
                : "Not signed in. Connect on the Integrations tab.";
            if (!signedIn)
                MasterResumeStatusLabel.Text = "Sign in to load or save your private master resume.";
            var records = await _tracker.GetAllAsync();
            var feed = await _cache.LoadLatestAsync();
            LocalDataLabel.Text = feed is null
                ? $"{records.Count} tracked applications · no cached feed"
                : $"{records.Count} tracked applications · cached feed has {feed.Jobs.Count} jobs";
        }
        catch
        {
            CloudStatusLabel.Text = "Cloud account status unavailable.";
            LocalDataLabel.Text = "Local data status unavailable.";
        }
    }

    private async void OnOpenIntegrationsClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//integrations");

    private async void OnImportMasterResumeClicked(object sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose your master resume (.txt or .md)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.Android] = new[] { "text/plain", "text/markdown", "text/x-markdown" },
                    [DevicePlatform.WinUI] = new[] { ".txt", ".md" },
                    [DevicePlatform.macOS] = new[] { "txt", "md" },
                    [DevicePlatform.iOS] = new[] { "public.plain-text", "net.daringfireball.markdown" }
                })
            });
            if (file is null) return;
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("That file is empty.");
            MasterResumeEditor.Text = content;
            _masterResumeSourceFile = Path.GetFileName(file.FileName);
            MasterResumeStatusLabel.Text = $"Imported {_masterResumeSourceFile} · review before saving";
            StatusLabel.Text = "Resume imported into the editor only. It has not been uploaded yet.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import failed", ex.Message, "OK");
        }
    }

    private async void OnLoadMasterResumeClicked(object sender, EventArgs e)
    {
        try
        {
            if (!await _supabase.IsSignedInAsync())
                throw new InvalidOperationException("Sign in to JobFlow Cloud first.");
            var resume = await _supabase.GetMasterResumeAsync();
            if (resume is null)
            {
                MasterResumeStatusLabel.Text = "No master resume is saved in this cloud account yet.";
                return;
            }
            MasterResumeEditor.Text = resume.Content;
            _masterResumeSourceFile = resume.SourceFilename;
            MasterResumeStatusLabel.Text = $"Loaded private master resume · updated {resume.UpdatedAt.ToLocalTime():g}";
            StatusLabel.Text = "Loaded the current cloud copy. It has not been changed.";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't load master resume", ex.Message, "OK");
        }
    }

    private async void OnSaveMasterResumeClicked(object sender, EventArgs e)
    {
        var content = MasterResumeEditor.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlertAsync("Master resume is empty", "Import or paste your resume before saving.", "OK");
            return;
        }
        try
        {
            if (!await _supabase.IsSignedInAsync())
                throw new InvalidOperationException("Sign in to JobFlow Cloud first.");
            var confirm = await DisplayAlertAsync(
                "Replace private master resume?",
                "This will replace the existing master resume for your signed-in account. Existing job-specific resumes and cover letters will not be changed.",
                "Replace and save", "Cancel");
            if (!confirm) return;
            await _supabase.SaveMasterResumeAsync(content, _masterResumeSourceFile);
            MasterResumeStatusLabel.Text = $"Saved privately · {content.Length:N0} characters · {DateTime.Now:g}";
            StatusLabel.Text = "Master resume saved to your private JobFlow Cloud account.";
            await DisplayAlertAsync("Saved", "Your master resume is now stored privately in JobFlow Cloud.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't save master resume", ex.Message, "OK");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var records = await _tracker.GetAllAsync();
            var payload = records.Select(r => new
            {
                r.JobId, r.Title, r.Company, r.Status, r.Notes, r.ApplicationUrl,
                follow_up_utc = r.FollowUpAtUtc, r.CreatedAtUtc, r.UpdatedAtUtc
            });
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await Clipboard.Default.SetTextAsync(json);
            StatusLabel.Text = $"Copied {records.Count} application records to the clipboard (no credentials).";
            await DisplayAlertAsync("Export ready", $"{records.Count} application records were copied as JSON. Credentials are never included.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Export failed", "Could not export application records from this device.", "OK");
        }
    }

    private async void OnClearCacheClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync("Clear cached job feed?", "Removes the offline job list cache only. Tracked applications and your cloud session stay intact.", "Clear cache", "Cancel");
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