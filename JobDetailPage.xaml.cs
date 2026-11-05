using System.Text.Json;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class JobDetailPage : ContentPage
{
    private readonly JobFeedItem _job;
    private readonly SupabaseDataService _supabaseService = new();
    private readonly ApplicationTrackerService _applicationTracker = new();

    public JobDetailPage(JobFeedItem job)
    {
        InitializeComponent();
        _job = job;
        Title = "Job detail";
        BindJob();
    }

    private void BindJob()
    {
        TitleLabel.Text = string.IsNullOrWhiteSpace(_job.Title) ? "Untitled role" : _job.Title;
        CompanyLabel.Text = string.IsNullOrWhiteSpace(_job.Company) ? "Company unknown" : _job.Company;
        SalaryLabel.Text = _job.SalaryLabel;
        PayTypeLabel.Text = string.IsNullOrWhiteSpace(_job.SalaryPayType)
            ? "Pay type not listed"
            : $"Pay type: {_job.SalaryPayType}";

        RemoteLabel.Text = $"Remote / location: {DisplayOrUnknown(_job.RemoteScope)}";
        PostingLabel.Text = $"Posting date: {DisplayOrUnknown(_job.PostingDate)}";
        VerifiedLabel.Text = $"Verified at: {DisplayOrUnknown(_job.VerifiedAt)}";
        MatchLabel.Text = _job.MatchScore is null
            ? "Match score: unknown"
            : $"Match score: {_job.MatchScore}%";
        JobIdLabel.Text = $"Job ID: {DisplayOrUnknown(_job.JobId)}";
        DescriptionLabel.Text = DisplayOrUnknown(_job.Description);
        PostingStatusLabel.Text = $"Posting status: {DisplayOrUnknown(_job.PostingStatus)}";
        RequisitionLabel.Text = $"Requisition: {DisplayOrUnknown(_job.RequisitionId)}";
        CompanySummaryLabel.Text = DisplayOrUnknown(_job.CompanySummary);
        FitRationaleLabel.Text = DisplayOrUnknown(_job.FitRationale);
        QualificationsLabel.Text = FormatList(_job.RelevantQualifications);
        EligibilityNotesLabel.Text = FormatList(_job.EligibilityNotes);
        ApplicationInstructionsLabel.Text = $"Application instructions: {DisplayOrUnknown(_job.ApplicationInstructions)}";
        SourceUrlsLabel.Text = _job.SourceUrls.Count == 0
            ? "Source URLs: unknown"
            : $"Source URLs:\\n{string.Join("\\n", _job.SourceUrls)}";

        MetadataPathLabel.Text = $"Metadata: {DisplayOrUnknown(_job.MetadataPath)}";
        ResumePathLabel.Text = $"Resume: {DisplayOrUnknown(_job.ResumeDropboxFilename ?? _job.ResumePath)}";
        CoverPathLabel.Text = $"Cover letter: {DisplayOrUnknown(_job.CoverLetterDropboxFilename ?? _job.CoverLetterPath)}";
        TrackingPathLabel.Text = $"Tracking: {DisplayOrUnknown(_job.ApplicationTrackingPath)}";

        ApplicationUrlLabel.Text = string.IsNullOrWhiteSpace(_job.ApplicationUrl)
            ? "Application URL: unknown"
            : $"Application URL: {_job.ApplicationUrl}";

        OpenAppButton.IsEnabled = IsSafeHttps(_job.ApplicationUrl);
    }

    private async void OnPrepareEmailClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ApplicationEmailPage(_job));
    }

    private async void OnManageEmployerClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new EmployersPage());
    }

    private static string FormatList(IReadOnlyCollection<string>? values) =>
        values is null || values.Count == 0
            ? "Not provided in the current feed."
            : string.Join("\\n", values.Select(value => $"• {value}"));

    private async void OnMetadataClicked(object sender, EventArgs e) =>
        await OpenDocumentAsync(DocumentKind.Metadata);

    private async void OnResumeClicked(object sender, EventArgs e) =>
        await OpenDocumentAsync(DocumentKind.Resume);

    private async void OnCoverClicked(object sender, EventArgs e) =>
        await OpenDocumentAsync(DocumentKind.CoverLetter);

    private async void OnTrackClicked(object sender, EventArgs e)
    {
        TrackButton.IsEnabled = false;
        try
        {
            var record = await _applicationTracker.TrackAsync(_job);
            var syncMessage = "local only";
            try
            {
                if (await _supabaseService.IsSignedInAsync())
                {
                    await _supabaseService.UpsertApplicationTrackingBatchAsync(await _applicationTracker.GetAllAsync());
                    syncMessage = "cloud synced";
                }
            }
            catch
            {
                // The Applications tab retries cloud sync without losing the local record.
            }
            StatusLabel.Text = $"Tracked · {syncMessage} · stage {record.Status}.";
            await DisplayAlertAsync("Application tracker", $"Saved locally. Current stage: {record.Status}.", "OK");
        }
        catch
        {
            StatusLabel.Text = "Could not save this job to the local tracker.";
            await DisplayAlertAsync("Couldn't track job", "JobFlow could not save this job to the local application tracker.", "OK");
        }
        finally
        {
            TrackButton.IsEnabled = true;
        }
    }

    private async void OnOpenApplicationClicked(object sender, EventArgs e)
    {
        if (IsSafeHttps(_job.ApplicationUrl)
            && Uri.TryCreate(_job.ApplicationUrl, UriKind.Absolute, out var uri))
        {
            await Launcher.Default.OpenAsync(uri);
            return;
        }

        await DisplayAlertAsync("Application link unavailable", "This job does not have a valid HTTPS application URL.", "OK");
    }

    private async Task OpenDocumentAsync(DocumentKind kind)
    {
        var documentKind = kind switch
        {
            DocumentKind.Resume => "resume",
            DocumentKind.CoverLetter => "cover",
            _ => "metadata"
        };
        var title = kind switch
        {
            DocumentKind.Metadata => "Metadata",
            DocumentKind.Resume => "Resume",
            _ => "Cover letter"
        };
        var subtitle = $"{_job.Title} · {_job.Company}";

        try
        {
            var isSignedIn = await _supabaseService.IsSignedInAsync();
            if (isSignedIn)
            {
                StatusLabel.Text = $"Loading {title.ToLowerInvariant()} from JobFlow Cloud…";
                var content = await _supabaseService.GetJobDocumentAsync(_job.JobId, documentKind);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    StatusLabel.Text = $"Opened {title.ToLowerInvariant()} from JobFlow Cloud.";
                    await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, "Supabase cloud record", content));
                    return;
                }
            }
            var dropboxUrl = kind switch
            {
                DocumentKind.Resume => _job.ResumeDropboxUrl,
                DocumentKind.CoverLetter => _job.CoverLetterDropboxUrl,
                _ => null
            };
            var dropboxFilename = kind switch
            {
                DocumentKind.Resume => _job.ResumeDropboxFilename,
                DocumentKind.CoverLetter => _job.CoverLetterDropboxFilename,
                _ => null
            };

            // Application records may reference an older offline feed snapshot. Fetch the
            // latest shared metadata so newly linked Dropbox documents appear without
            // requiring the user to refresh the Jobs feed first.
            if (isSignedIn && string.IsNullOrWhiteSpace(dropboxUrl))
            {
                var metadataJson = await _supabaseService.GetJobDocumentAsync(_job.JobId, "metadata");
                if (!string.IsNullOrWhiteSpace(metadataJson))
                {
                    try
                    {
                        using var metadataDocument = JsonDocument.Parse(metadataJson);
                        var metadataRoot = metadataDocument.RootElement;
                        var urlKey = kind == DocumentKind.Resume ? "resume_dropbox_url" : "cover_letter_dropbox_url";
                        var filenameKey = kind == DocumentKind.Resume ? "resume_dropbox_filename" : "cover_letter_dropbox_filename";
                        if (metadataRoot.TryGetProperty(urlKey, out var urlValue) && urlValue.ValueKind == JsonValueKind.String)
                            dropboxUrl = urlValue.GetString();
                        if (metadataRoot.TryGetProperty(filenameKey, out var filenameValue) && filenameValue.ValueKind == JsonValueKind.String)
                            dropboxFilename = filenameValue.GetString();
                    }
                    catch (JsonException)
                    {
                        // A malformed metadata field must not prevent the existing document fallback.
                    }
                }
            }

            if (IsSafeDropboxUrl(dropboxUrl) && Uri.TryCreate(dropboxUrl, UriKind.Absolute, out var dropboxUri))
            {
                if (!string.IsNullOrWhiteSpace(dropboxFilename))
                {
                    if (kind == DocumentKind.Resume) ResumePathLabel.Text = $"Resume: {dropboxFilename}";
                    if (kind == DocumentKind.CoverLetter) CoverPathLabel.Text = $"Cover letter: {dropboxFilename}";
                }
                StatusLabel.Text = $"Opening {title.ToLowerInvariant()} in Dropbox…";
                await Launcher.Default.OpenAsync(dropboxUri);
                StatusLabel.Text = $"Opened {title.ToLowerInvariant()} in Dropbox.";
                return;
            }
            if (!isSignedIn)
            {
                await DisplayAlertAsync("Document not linked", "No Dropbox document is linked yet. Sign in to JobFlow Cloud to check for a saved application kit.", "OK");
                return;
            }
            await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, "No saved document is linked for this job yet."));
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Could not load {title.ToLowerInvariant()}.";
            await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, $"Document request failed: {ex.Message}"));
        }
    }

    private static string DisplayOrUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static bool IsSafeHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsSafeDropboxUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.Equals("dropbox.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".dropbox.com", StringComparison.OrdinalIgnoreCase));

    private enum DocumentKind
    {
        Metadata,
        Resume,
        CoverLetter
    }
}
