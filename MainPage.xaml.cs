using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class MainPage : ContentPage
{
    private readonly GitHubDataService _dataService = new();
    private readonly FeedCacheService _cacheService = new();
    private readonly ApplicationTrackerService _applicationTracker = new();
    private IReadOnlyList<JobFeedItem> _allJobs = [];

    public MainPage()
    {
        InitializeComponent();
        _ = RefreshConnectionSummaryAsync();
        _ = LoadCachedFeedAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshConnectionSummaryAsync();
    }

    private async Task RefreshConnectionSummaryAsync()
    {
        try
        {
            var token = await GitHubAccountStore.GetAccessTokenAsync();
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            ConnectionSummaryLabel.Text = string.IsNullOrWhiteSpace(token)
                ? "Sign in with GitHub on the Integrations tab, then choose your data repository."
                : $"Signed in · reading {owner}/{repo}. Change this on the Integrations tab.";
        }
        catch
        {
            ConnectionSummaryLabel.Text = "Sign in with GitHub on the Integrations tab.";
        }
    }

    private async Task LoadCachedFeedAsync()
    {
        try
        {
            var cached = await _cacheService.LoadLatestAsync();
            if (cached is null) return;

            ApplyJobs(cached.Jobs);
            FeedDateLabel.Text = $"Feed generated: {cached.GeneratedAt ?? "date not supplied"}";
            StatusLabel.Text = $"Showing {cached.Jobs.Count} cached jobs from {cached.SourcePath ?? "the last successful sync"}. Pull a fresh feed when online.";
        }
        catch
        {
            // A broken or unavailable cache must not prevent online use.
        }
    }

    private async void OnLoadJobsClicked(object sender, EventArgs e)
    {
        var token = await GitHubAccountStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync(
                "Sign in required",
                "Open the Integrations tab, sign in with GitHub, and choose your data repository. You do not paste a token.",
                "OK");
            return;
        }

        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Loading latest feed from GitHub…";
        try
        {
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            var feed = await _dataService.LoadLatestFeedAsync(token, owner, repo);
            ApplyJobs(feed.Jobs);
            FeedDateLabel.Text = $"Feed generated: {feed.GeneratedAt ?? "date not supplied"}";
            StatusLabel.Text = $"{feed.Jobs.Count} unique jobs from {owner}/{repo}. Feed data is read-only.";

            try { await _cacheService.SaveLatestAsync(feed); }
            catch { StatusLabel.Text += " Local cache could not be updated."; }
        }
        catch (Exception)
        {
            var cached = await TryLoadCachedFeedAsync();
            if (cached is not null)
            {
                ApplyJobs(cached.Jobs);
                FeedDateLabel.Text = $"Feed generated: {cached.GeneratedAt ?? "date not supplied"}";
                StatusLabel.Text = $"Offline: showing {cached.Jobs.Count} cached jobs. GitHub refresh failed.";
            }
            await DisplayAlertAsync(
                "JobFlow couldn't refresh",
                "GitHub access or the feed could not be verified. If a saved feed exists, it remains available offline. Check sign-in, repository choice, network, and feed structure.",
                "OK");
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private void ApplyJobs(IReadOnlyList<JobFeedItem> jobs)
    {
        _allJobs = jobs.ToList();
        ApplyFilter(SearchEntry.Text);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);

    private void ApplyFilter(string? query)
    {
        if (_allJobs.Count == 0)
        {
            JobsCollection.ItemsSource = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            JobsCollection.ItemsSource = _allJobs;
            return;
        }

        var term = query.Trim();
        var filtered = _allJobs
            .Where(job =>
                (job.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (job.Company?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (job.JobId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        JobsCollection.ItemsSource = filtered;
        StatusLabel.Text = $"Showing {filtered.Count} of {_allJobs.Count} jobs matching \"{term}\".";
    }

    private async Task<DailyFeed?> TryLoadCachedFeedAsync()
    {
        try { return await _cacheService.LoadLatestAsync(); }
        catch { return null; }
    }

    private async void OnJobDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        await Navigation.PushAsync(new JobDetailPage(job));
    }

    private async void OnViewResumeClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        await OpenDocumentAsync(job, DocumentKind.Resume);
    }

    private async void OnViewCoverLetterClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        await OpenDocumentAsync(job, DocumentKind.CoverLetter);
    }

    private async Task OpenDocumentAsync(JobFeedItem job, DocumentKind kind)
    {
        var token = await GitHubAccountStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            await DisplayAlertAsync("Sign in required", "Sign in with GitHub on the Integrations tab before opening documents.", "OK");
            return;
        }

        var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
        var title = kind == DocumentKind.Resume ? "Resume" : "Cover letter";
        var subtitle = $"{job.Title} · {job.Company}";
        var candidates = BuildDocumentPaths(job, kind);

        if (candidates.Count == 0)
        {
            await Navigation.PushAsync(new DocumentViewerPage(
                title,
                subtitle,
                "No document path is available for this job in the feed."));
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

                StatusLabel.Text = $"Opened {title.ToLowerInvariant()} from {path}.";
                await Navigation.PushAsync(new DocumentViewerPage(title, subtitle, $"{owner}/{repo}/{path}", text));
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        StatusLabel.Text = $"Could not open {title.ToLowerInvariant()}.";
        var detail = lastError?.Message ?? "File not found in the data repository.";
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

    private enum DocumentKind
    {
        Resume,
        CoverLetter
    }

    private async void OnTrackApplicationClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        try
        {
            var record = await _applicationTracker.TrackAsync(job);
            await DisplayAlertAsync("Application tracker", $"Saved locally. Current stage: {record.Status}.", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Couldn't track job", "JobFlow could not save this job to the local application tracker.", "OK");
        }
    }

    private async void OnOpenApplicationClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url }
            && Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps)
        {
            await Launcher.Default.OpenAsync(parsed);
        }
        else
        {
            await DisplayAlertAsync("Application link unavailable", "This job does not have a valid HTTPS application URL.", "OK");
        }
    }
}
