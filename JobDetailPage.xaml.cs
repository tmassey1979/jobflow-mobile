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

        MetadataPathLabel.Text = $"Metadata: {DisplayOrUnknown(_job.MetadataPath)}";
        ResumePathLabel.Text = $"Resume: {DisplayOrUnknown(_job.ResumePath)}";
        CoverPathLabel.Text = $"Cover letter: {DisplayOrUnknown(_job.CoverLetterPath)}";
        TrackingPathLabel.Text = $"Tracking: {DisplayOrUnknown(_job.ApplicationTrackingPath)}";

        ApplicationUrlLabel.Text = string.IsNullOrWhiteSpace(_job.ApplicationUrl)
            ? "Application URL: unknown"
            : $"Application URL: {_job.ApplicationUrl}";

        OpenAppButton.IsEnabled = IsSafeHttps(_job.ApplicationUrl);
    }

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
            StatusLabel.Text = $"Tracked locally · stage {record.Status}.";
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
            if (!await _supabaseService.IsSignedInAsync())
            {
                await DisplayAlertAsync("Sign in required", "Sign in to your JobFlow Cloud account on the Integrations tab before opening application kits.", "OK");
                return;
            }

            StatusLabel.Text = $"Loading {title.ToLowerInvariant()} from cloud…";
            var content = await _supabaseService.GetJobDocumentAsync(_job.JobId, documentKind);
            if (string.IsNullOrWhiteSpace(content))
            {
                await Navigation.PushAsync(new DocumentViewerPage(
                    title, subtitle, "This cloud record does not contain that document yet."));
                return;
            }

            StatusLabel.Text = $"Opened {title.ToLowerInvariant()} from cloud.";
            await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, "Supabase cloud record", content));
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Could not load {title.ToLowerInvariant()}.";
            await Navigation.PushAsync(new DocumentViewerPage(
                title, subtitle, $"Supabase document request failed: {ex.Message}"));
        }
    }

    private static string DisplayOrUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static bool IsSafeHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private enum DocumentKind
    {
        Metadata,
        Resume,
        CoverLetter
    }
}
