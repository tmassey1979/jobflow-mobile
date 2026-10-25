using System.Text.Json;
using JobFlow.Mobile.Models;
using SQLite;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Offline-first cache for the last successfully validated job feed.
/// The database stores feed data only, never GitHub credentials.
/// </summary>
public sealed class FeedCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public FeedCacheService()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "jobflow.db3");
        _database = new SQLiteAsyncConnection(databasePath);
    }

    public async Task SaveLatestAsync(DailyFeed feed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feed);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync();
        var record = new CachedFeedRecord
        {
            Id = "latest",
            GeneratedAt = feed.GeneratedAt,
            SourcePath = feed.SourcePath,
            JsonPayload = JsonSerializer.Serialize(feed, JsonOptions),
            CachedAtUtc = DateTime.UtcNow
        };
        await _database.InsertOrReplaceAsync(record);
    }

    public async Task<DailyFeed?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync();
        var record = await _database.FindAsync<CachedFeedRecord>("latest");
        if (record is null || string.IsNullOrWhiteSpace(record.JsonPayload))
            return null;
        var feed = DailyFeedParser.Parse(record.JsonPayload);
        feed.SourcePath = record.SourcePath;
        return feed;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _database.CreateTableAsync<CachedFeedRecord>();
            _initialized = true;
        }
        finally { _initializationLock.Release(); }
    }
}
