using System.Text.Json;
using JobFlow.Mobile.Models;
using SQLite;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Persists validated feed jobs locally so previously imported jobs remain available offline.
/// Imported job facts are replaced by the latest validated feed; user-created application
/// notes and progress live in the separate applications table and are never overwritten here.
/// A failed or empty sync does not delete previously cached jobs.
/// </summary>
public sealed class OfflineJobStore
{
    private readonly LocalDatabaseService _localDatabase;
    private readonly SQLiteAsyncConnection _database;

    public OfflineJobStore() : this(LocalDatabaseService.Shared) { }

    public OfflineJobStore(LocalDatabaseService localDatabase)
    {
        _localDatabase = localDatabase ?? throw new ArgumentNullException(nameof(localDatabase));
        _database = _localDatabase.Connection;
    }

    public async Task<int> ImportFeedAsync(DailyFeed feed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feed);
        cancellationToken.ThrowIfCancellationRequested();
        await _localDatabase.InitializeAsync(cancellationToken);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var records = new List<CachedJobRecord>();
        foreach (var job in feed.Jobs ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (job is null || string.IsNullOrWhiteSpace(job.JobId) ||
                string.IsNullOrWhiteSpace(job.Title) || string.IsNullOrWhiteSpace(job.Company))
                throw new InvalidDataException("Every imported job must include job_id, title, and company.");

            if (!seen.Add(job.JobId.Trim()))
                continue;

            records.Add(new CachedJobRecord
            {
                JobId = job.JobId.Trim().ToLowerInvariant(),
                SourceJobId = job.JobId.Trim(),
                Title = job.Title,
                Company = job.Company,
                SalaryMinUsd = job.SalaryMinUsd,
                SalaryMaxUsd = job.SalaryMaxUsd,
                SalaryPayType = job.SalaryPayType,
                RemoteScope = job.RemoteScope,
                VerifiedAt = job.VerifiedAt,
                PostingDate = job.PostingDate,
                ApplicationUrl = IsSafeHttps(job.ApplicationUrl) ? job.ApplicationUrl : null,
                MatchScore = job.MatchScore,
                MetadataPath = job.MetadataPath,
                ResumePath = job.ResumePath,
                CoverLetterPath = job.CoverLetterPath,
                ApplicationTrackingPath = job.ApplicationTrackingPath,
                JobJson = JsonSerializer.Serialize(job),
                FeedSourcePath = feed.SourcePath,
                FeedGeneratedAt = feed.GeneratedAt,
                ImportedAtUtc = DateTime.UtcNow
            });
        }

        // Import only rows from a fully parsed feed. Upserts do not touch the application tracker.
        // Do not delete rows missing from a later feed: feed omissions are not authoritative deletes.
        await _database.RunInTransactionAsync(connection =>
        {
            foreach (var record in records)
                connection.InsertOrReplace(record);
        });
        return records.Count;
    }

    public async Task<IReadOnlyList<CachedJobRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _localDatabase.InitializeAsync(cancellationToken);
        var records = await _database.Table<CachedJobRecord>().ToListAsync();
        return records.OrderByDescending(record => record.FeedGeneratedAt, StringComparer.Ordinal)
            .ThenBy(record => record.Company, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CachedJobRecord?> FindAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("A job ID is required.", nameof(jobId));
        cancellationToken.ThrowIfCancellationRequested();
        await _localDatabase.InitializeAsync(cancellationToken);
        return await _database.FindAsync<CachedJobRecord>(jobId.Trim().ToLowerInvariant());
    }

    private static bool IsSafeHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
