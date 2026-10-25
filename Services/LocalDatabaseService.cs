using JobFlow.Mobile.Models;
using SQLite;

namespace JobFlow.Mobile.Services;

/// <summary>
/// Owns the app's shared SQLite connection and applies ordered, idempotent schema migrations.
/// The database is device-local; this service performs no network or sync operations.
/// </summary>
public sealed class LocalDatabaseService
{
    private const int CurrentSchemaVersion = 3;
    private static readonly Lazy<LocalDatabaseService> SharedInstance =
        new(() => new LocalDatabaseService(GetDefaultDatabasePath()));

    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public static LocalDatabaseService Shared => SharedInstance.Value;

    /// <summary>Allows tests to use an isolated temporary database file.</summary>
    public LocalDatabaseService(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database path is required.", nameof(databasePath));

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connection = new SQLiteAsyncConnection(databasePath);
    }

    public SQLiteAsyncConnection Connection => _connection;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_initialized) return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            // Create the ledger first. Existing installations may already have data tables
            // created by the pre-migration version of the app.
            await _connection.CreateTableAsync<DatabaseMigrationRecord>();
            var applied = await _connection.Table<DatabaseMigrationRecord>()
                .ToListAsync();
            var version = applied.Count == 0 ? 0 : applied.Max(item => item.Version);

            if (version > CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"The local database schema ({version}) is newer than this app supports ({CurrentSchemaVersion}).");

            if (version < 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _connection.RunInTransactionAsync(database =>
                {
                    // CreateTable is safe for the legacy tables: it leaves existing rows intact.
                    database.CreateTable<CachedFeedRecord>();
                    database.CreateTable<ApplicationRecord>();
                    database.Insert(new DatabaseMigrationRecord
                    {
                        Version = 1,
                        AppliedAtUtc = DateTime.UtcNow,
                        Description = "Create feed cache and local application tracker tables."
                    });
                });
                version = 1;
            }

            if (version < 2)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _connection.RunInTransactionAsync(database =>
                {
                    database.CreateTable<CachedJobRecord>();
                    database.Insert(new DatabaseMigrationRecord
                    {
                        Version = 2,
                        AppliedAtUtc = DateTime.UtcNow,
                        Description = "Add normalized offline job cache."
                    });
                });
            }

            if (version < 3)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _connection.RunInTransactionAsync(database =>
                {
                    database.CreateTable<ApplicationFollowUpRecord>();
                    database.Insert(new DatabaseMigrationRecord
                    {
                        Version = 3,
                        AppliedAtUtc = DateTime.UtcNow,
                        Description = "Ensure local application follow-up dates are available in the shared database."
                    });
                });
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static string GetDefaultDatabasePath()
    {
#if ANDROID
        var root = FileSystem.AppDataDirectory;
#else
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif
        return Path.Combine(root, "jobflow.db3");
    }
}
