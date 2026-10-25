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
    private readonly LocalDatabaseService _localDatabase;
    private readonly SQLiteAsyncConnection _database;

    public FeedCacheService()
    {
        _localDatabase = LocalDatabaseService.Shared;
        _database = _localDatabase.Connection;
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

    private Task EnsureInitializedAsync() => _localDatabase.InitializeAsync();
}
