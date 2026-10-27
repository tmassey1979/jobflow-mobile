using System.Windows.Input;
using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;

namespace JobFlow.Mobile;

public partial class MainPage : ContentPage
{
    private readonly SupabaseDataService _supabaseService = new();
    private readonly FeedCacheService _cacheService = new();
    private readonly OfflineJobStore _offlineJobStore = new();
    private readonly ApplicationTrackerService _applicationTracker = new();
    private IReadOnlyList<JobFeedItem> _allJobs = [];
    private HashSet<string> _trackedJobIds = new(StringComparer.OrdinalIgnoreCase);
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
            if (await _supabaseService.IsSignedInAsync())
            {
                ConnectionSummaryLabel.Text = $"Connected · Supabase cloud feed";
                return;
            }

            ConnectionSummaryLabel.Text = "Sign in on Integrations to sync cloud jobs.";
        }
        catch
        {
            ConnectionSummaryLabel.Text = "Connect Supabase or sign in on Integrations.";
        }
    }

    private async Task LoadTrackedIdsAsync()
    {
        try
        {
            var tracked = await _applicationTracker.GetAllAsync();
            _trackedJobIds = tracked
                .Select(r => r.JobId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _trackedJobIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task LoadFromOfflineStoreAsync()
    {
        try
        {
            await LoadTrackedIdsAsync();
            var records = await _offlineJobStore.GetAllAsync();
            if (records.Count == 0)
            {
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
            StatusLabel.Text = $"Showing {records.Count} imported jobs. Pull down or tap Sync all to refresh from the cloud.";
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

        bool signedIn;
        try { signedIn = await _supabaseService.IsSignedInAsync(); }
        catch { signedIn = false; }

        if (!signedIn)
        {
            IsRefreshing = false;
            await DisplayAlertAsync("Sign in required", "Open Integrations and sign in to your JobFlow Cloud account before syncing jobs.", "OK");
            return;
        }

        IsRefreshing = true;
        LoadButton.IsEnabled = false;
        StatusLabel.Text = "Loading cloud jobs…";
        try
        {
            var feed = await _supabaseService.LoadAllJobsAsync();
            try { await _cacheService.SaveLatestAsync(feed); }
            catch { /* Keep showing fetched jobs even if offline-cache persistence fails. */ }

            await LoadTrackedIdsAsync();
            ApplyJobs(feed.Jobs);
            FeedDateLabel.Text = $"Imported {feed.Jobs.Count} unique jobs · Supabase";
            StatusLabel.Text = $"{feed.Jobs.Count} cloud jobs loaded. The last successful feed remains available offline.";
        }
        catch (Exception ex)
        {
            await LoadFromOfflineStoreAsync();
            await DisplayAlertAsync(
                "JobFlow couldn't refresh",
                string.IsNullOrWhiteSpace(ex.Message)
                    ? "The cloud feed could not be verified. Previously imported jobs remain available offline."
                    : $"{ex.Message}\n\nPreviously imported jobs remain available offline.",
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

        IEnumerable<JobFeedItem> source = _allJobs;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            source = _allJobs.Where(job =>
                (job.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (job.Company?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (job.JobId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var items = source
            .Select(job => new JobListItem
            {
                Job = job,
                IsTracked = _trackedJobIds.Contains(job.JobId)
            })
            .ToList();

        JobsCollection.ItemsSource = items;

        if (!string.IsNullOrWhiteSpace(query))
            StatusLabel.Text = $"Showing {items.Count} of {_allJobs.Count} jobs matching \"{query.Trim()}\".";
    }

    private async void OnJobDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobListItem item }) return;
        await Navigation.PushAsync(new JobDetailPage(item.Job));
    }

    private async void OnTrackApplicationClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: JobListItem item }) return;
        try
        {
            var record = await _applicationTracker.TrackAsync(item.Job);
            try
            {
                if (await _supabaseService.IsSignedInAsync())
                    await _supabaseService.UpsertApplicationTrackingAsync(record);
            }
            catch
            {
                // Keep local tracking available; the Applications tab retries cloud sync.
            }
            _trackedJobIds.Add(record.JobId);
            ApplyFilter(SearchEntry.Text);
            await DisplayAlertAsync(
                "Moved to Applications",
                $"{record.Title} at {record.Company} is in your Application Tracker (stage: {record.Status}). Open the Applications tab to set stage, notes, or follow-up.",
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
