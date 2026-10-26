using System.Windows.Input;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class MainPage : ContentPage
{
    private readonly GitHubDataService _dataService = new();
    private readonly FeedCacheService _cacheService = new();
    private readonly OfflineJobStore _offlineJobStore = new();
    private readonly ApplicationTrackerService _applicationTracker = new();
    private IReadOnlyList<JobFeedItem> _allJobs = [];
    private bool _isRefreshing;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await RefreshAllJobsAsync(fromPull: true));
        _ = RefreshConnectionSummaryAsync();
        _ = LoadFromOfflineStoreAsync();
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshConnectionSummaryAsync();
        _ = LoadFromOfflineStoreAsync();
    }

    private async Task RefreshConnectionSummaryAsync()
    {
        try
        {
            var token = await GitHubAccountStore.GetAccessTokenAsync();
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            ConnectionSummaryLabel.Text = string.IsNullOrWhiteSpace(token)
                ? "Sign in on Integrations, then choose your data repository."
                : $"Signed in · {owner}/{repo}";
        }
        catch
        {
            ConnectionSummaryLabel.Text = "Sign in on Integrations.";
        }
    }

    private async Task LoadFromOfflineStoreAsync()
    {
        try
        {
            var records = await _offlineJobStore.GetAllAsync();
            if (records.Count == 0)
            {
                // Fall back to the single latest cached feed envelope if the store is empty.
                var cached = await _cacheService.LoadLatestAsync();
                if (cached is not null)
                {
                    ApplyJobs(cached.Jobs);
                    FeedDateLabel.Text = $"Feed: {cached.GeneratedAt ?? "unknown"}";
                    StatusLabel.Text = $"{cached.Jobs.Count} jobs from last cache. Pull down to import every daily feed.";
                }
                return;
            }

            var jobs = records.Select(ToFeedItem).ToList();
            ApplyJobs(jobs);
            FeedDateLabel.Text = $"{records.Count} jobs in offline store";
            StatusLabel.Text = $"Showing {records.Count} imported jobs. Pull down or tap Sync all to refresh from GitHub.";
        }
        catch
        {
            // Offline store failure must not block the page.
        }
    }

    private static JobFeedItem ToFeedItem(CachedJobRecord r) => new()
    {
        JobId = r.SourceJobId ?? r.JobId,
        Title = r.Title,
        Company = r.Company,
        SalaryMinUsd = r.SalaryMinUsd,
        SalaryMaxUsd = r.SalaryMaxUsd,
        SalaryPayType = r.SalaryPayType,
        RemoteScope = r.RemoteScope,
        VerifiedAt = r.VerifiedAt,
        PostingDate = r.PostingDate,
        ApplicationUrl = r.ApplicationUrl,
        MatchScore = r.MatchScore,
        MetadataPath = r.MetadataPath,
        ResumePath = r.ResumePath,
        CoverLetterPath = r.CoverLetterPath,
        ApplicationTrackingPath = r.ApplicationTrackingPath
    };

    private async void OnLoadJobsClicked(object sender, EventArgs e) => await RefreshAllJobsAsync(fromPull: false);

    private async Task RefreshAllJobsAsync(bool fromPull)
    {
        if (IsRefreshing && fromPull) return;

        var token = await GitHubAccountStore.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            IsRefreshing = false;
            await DisplayAlertAsync(
                "Sign in required",
                "Open the Integrations tab, sign in with GitHub, and choose your data repository.",
                "OK");
            return;
        }

        IsRefreshing = true;
        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Importing every daily feed from GitHub…";

        try
        {
            var (owner, repo) = await GitHubAccountStore.GetSelectedRepositoryAsync();
            var progress = new Progress<string>(msg => MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg));

            var feed = await _dataService.LoadAllFeedsAsync(token, owner, repo, progress);

            // Persist the combined feed and upsert into the offline job store.
            try
            {
                await _cacheService.SaveLatestAsync(feed);
            }
            catch
            {
                // Still show the jobs even if cache write fails.
            }

            ApplyJobs(feed.Jobs);
            FeedDateLabel.Text = $"Imported {feed.Jobs.Count} unique jobs · {feed.SourcePath}";
            StatusLabel.Text = $"{feed.Jobs.Count} unique jobs from {owner}/{repo}. Pull down anytime to refresh all feeds.";
        }
        catch (Exception)
        {
            await LoadFromOfflineStoreAsync();
            await DisplayAlertAsync(
                "JobFlow couldn't refresh",
                "GitHub access or the feeds could not be verified. Previously imported jobs remain available offline. Check sign-in, repository choice, network, and that daily/YYYY-MM-DD.json files exist.",
                "OK");
        }
        finally
        {
            IsRefreshing = false;
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

    private async void OnJobDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        await Navigation.PushAsync(new JobDetailPage(job));
    }

    private async void OnTrackApplicationClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobFeedItem job }) return;
        try
        {
            var record = await _applicationTracker.TrackAsync(job);
            await DisplayAlertAsync(
                "Added to tracker",
                $"{record.Title} at {record.Company} is now in your Application Tracker (stage: {record.Status}). Open the Tracker tab to update stage, notes, or follow-up.",
                "OK");
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
