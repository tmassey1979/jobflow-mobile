using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class JobDetailPage : ContentPage
{
    private readonly JobFeedItem _job;
    private readonly GitHubDataService _dataService = new();
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
        var token = await GitHubAccountStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("Sign in required", "Sign in with GitHub on the Integrations tab before opening documents.", "OK");
            return;
        }

        var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
        var title = kind == DocumentKind.Resume ? "Resume" : "Cover letter";
        var subtitle = $"{_job.Title} · {_job.Company}";
        var candidates = BuildDocumentPaths(_job, kind);

        if (candidates.Count == 0)
        {
            await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, "No document path is available for this job in the feed."));
            return;
        }

        StatusLabel.Text = $"Loading {title.ToLowerInvariant()}…";
        Exception? lastError = null;
        foreach (var path in candidates)
        {
            try
            {
                var text = await _dataService.ReadTextFileAsync(path, token, owner, repo);
                if (string.IsNullOrWhiteSpace(text))
                {
                    lastError = new InvalidOperationException("The file was empty.");
                    continue;
                }

                StatusLabel.Text = $"Opened {title.ToLowerInvariant()}.";
                await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, $"{owner}/{repo}/{path}", text));
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        StatusLabel.Text = $"Could not open {title.ToLowerInvariant()}.";
        var detail = lastError?.Message ?? "File not found.";
        await Navigation.PushAsync(new DocumentViewerPage(
            title,
            subtitle,
            $"Could not read this document from {owner}/{repo}. Tried: {string.Join(", ", candidates)}. {detail}"));
    }

    private static List<string> BuildDocumentPaths(JobFeedItem job, DocumentKind kind)
    {
        var paths = new List<string>();
        var preferred = kind == DocumentKind.Resume ? job.ResumePath : job.CoverLetterPath;
        if (!string.IsNullOrWhiteSpace(preferred))
            paths.Add(preferred.Trim().TrimStart('/'));

        if (!string.IsNullOrWhiteSpace(job.JobId))
        {
            var fileName = kind == DocumentKind.Resume ? "resume.md" : "cover-letter.md";
            var fallback = $"jobs/{job.JobId}/{fileName}";
            if (!paths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                paths.Add(fallback);
        }

        return paths;
    }

    private static string DisplayOrUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static bool IsSafeHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private enum DocumentKind
    {
        Resume,
        CoverLetter
    }
}
