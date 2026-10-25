using JobFlow.Mobile.Models;
using SQLite;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Stores application progress locally on this device. No network or GitHub write operations occur here.
/// </summary>
public sealed class ApplicationTrackerService
{
    private readonly LocalDatabaseService _localDatabase;
    private readonly SQLiteAsyncConnection _database;

    public ApplicationTrackerService()
    {
        _localDatabase = LocalDatabaseService.Shared;
        _database = _localDatabase.Connection;
    }

    public async Task<ApplicationRecord> TrackAsync(JobFeedItem job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (string.IsNullOrWhiteSpace(job.JobId))
            throw new ArgumentException("A stable job ID is required to track an application.", nameof(job));

        await EnsureInitializedAsync();
        var existing = await _database.FindAsync<ApplicationRecord>(job.JobId);
        if (existing is not null) return existing;

        var record = new ApplicationRecord
        {
            JobId = job.JobId,
            Title = job.Title,
            Company = job.Company,
            ApplicationUrl = IsSafeHttps(job.ApplicationUrl) ? job.ApplicationUrl : null,
            Status = ApplicationStages.Interested,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await _database.InsertAsync(record);
        return record;
    }

    public async Task<IReadOnlyList<ApplicationRecord>> GetAllAsync()
    {
        await EnsureInitializedAsync();
        var records = await _database.Table<ApplicationRecord>().ToListAsync();
        return records.OrderByDescending(record => record.UpdatedAtUtc).ToList();
    }

    public async Task<ApplicationRecord?> AdvanceAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("A job ID is required.", nameof(jobId));
        await EnsureInitializedAsync();
        var record = await _database.FindAsync<ApplicationRecord>(jobId);
        if (record is null) return null;
        record.Status = ApplicationStages.Next(record.Status);
        record.UpdatedAtUtc = DateTime.UtcNow;
        await _database.UpdateAsync(record);
        return record;
    }

    public async Task<bool> RemoveAsync(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("A job ID is required.", nameof(jobId));
        await EnsureInitializedAsync();
        return await _database.DeleteAsync<ApplicationRecord>(jobId) > 0;
    }

    private Task EnsureInitializedAsync() => _localDatabase.InitializeAsync();
    private static bool IsSafeHttps(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
