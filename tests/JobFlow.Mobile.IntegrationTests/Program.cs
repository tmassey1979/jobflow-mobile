using JobFlow.Mobile.Models;
using JobFlow.Mobile.Services;
using SQLite;

var tests = new (string Name, Action Run)[]
{
    ("connect clears prior error and records only permission names", ConnectRecordsPermissions),
    ("disconnect clears permissions sync time and errors", DisconnectClearsState),
    ("reauthentication is actionable and disconnectable", ReauthenticationState),
    ("not-configured providers cannot connect", NotConfiguredCannotConnect),
    ("sync timestamps require a connected provider", SyncRequiresConnected),
    ("permission names are deduplicated case-insensitively", PermissionsAreDeduplicated),
    ("error messages are flattened and bounded", ErrorMessagesAreSanitized),
    ("application stages progress in the declared order", ApplicationStagesProgressInOrder),
    ("unknown application stage restarts at interested", UnknownApplicationStageRestarts),
    ("database migration preserves legacy cache data and is idempotent", DatabaseMigrationPreservesLegacyData),
    ("database refuses to downgrade a newer schema", NewerDatabaseSchemaIsRejected),
    ("offline job imports deduplicate case-insensitively and survive feed omissions", OfflineJobImportsAreStable)
};
var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} integration state tests passed.");
return failures == 0 ? 0 : 1;

static void ConnectRecordsPermissions()
{
    var connection = new IntegrationConnection("github", "GitHub");
    connection.BeginConnect();
    Assert(connection.Status == IntegrationStatus.Connecting, "status should be connecting");
    connection.MarkFailed("Network unavailable");
    connection.BeginConnect();
    connection.MarkConnected(new[] { "Contents: read-only" }, DateTimeOffset.Parse("2026-10-24T12:00:00Z"));
    Assert(connection.Status == IntegrationStatus.Connected, "status should be connected");
    Assert(connection.ErrorMessage is null, "success should clear the prior error");
    Assert(connection.GrantedPermissions.Single() == "Contents: read-only", "permission should be visible");
    Assert(connection.LastSuccessfulSync.HasValue, "successful sync time should be retained");
}
static void DisconnectClearsState()
{
    var connection = new IntegrationConnection("github", "GitHub");
    connection.MarkConnected(new[] { "Contents: read-only" }, DateTimeOffset.UtcNow);
    connection.Disconnect();
    Assert(connection.Status == IntegrationStatus.Disconnected, "status should be disconnected");
    Assert(connection.GrantedPermissions.Count == 0, "permissions should be cleared");
    Assert(connection.LastSuccessfulSync is null, "sync time should be cleared");
    Assert(connection.ErrorMessage is null, "error should be cleared");
}
static void ReauthenticationState()
{
    var connection = new IntegrationConnection("google-calendar", "Google Calendar");
    connection.MarkConnected(new[] { "calendar.readonly" });
    connection.MarkNeedsReauthentication("Consent expired");
    Assert(connection.Status == IntegrationStatus.NeedsReauthentication, "status should request reauthentication");
    Assert(connection.CanDisconnect, "user should be able to disconnect");
    Assert(connection.ErrorMessage == "Consent expired", "recovery hint should be retained");
}
static void NotConfiguredCannotConnect() =>
    ExpectThrows<InvalidOperationException>(() => new IntegrationConnection("google-calendar", "Google Calendar", IntegrationStatus.NotConfigured).BeginConnect());
static void SyncRequiresConnected() =>
    ExpectThrows<InvalidOperationException>(() => new IntegrationConnection("github", "GitHub").MarkSyncSucceeded(DateTimeOffset.UtcNow));
static void PermissionsAreDeduplicated()
{
    var connection = new IntegrationConnection("github", "GitHub");
    connection.MarkConnected(new[] { "Contents: read-only", "contents: READ-ONLY", "", "Metadata: read-only" });
    Assert(connection.GrantedPermissions.Count == 2, "permission names should be deduplicated and blanks omitted");
}
static void ExpectThrows<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void ErrorMessagesAreSanitized()
{
    var connection = new IntegrationConnection("github", "GitHub");
    connection.MarkFailed("Network\r\nerror");
    Assert(connection.ErrorMessage == "Network  error", "line breaks should be flattened");
    connection.MarkFailed(new string('x', 300));
    Assert(connection.ErrorMessage?.Length == 240, "error text should be capped at 240 characters");
}
static void ApplicationStagesProgressInOrder()
{
    var expected = new[]
    {
        ApplicationStages.Interested, ApplicationStages.Preparing, ApplicationStages.ReadyToApply,
        ApplicationStages.Applied, ApplicationStages.Interviewing, ApplicationStages.Offer,
        ApplicationStages.Rejected, ApplicationStages.Interested
    };
    var current = expected[0];
    for (var index = 1; index < expected.Length; index++)
    {
        current = ApplicationStages.Next(current);
        Assert(current == expected[index], $"stage {index} should be {expected[index]} but was {current}");
    }
}
static void UnknownApplicationStageRestarts()
{
    Assert(ApplicationStages.Next("unexpected") == ApplicationStages.Interested, "unknown stage should safely restart at Interested");
    Assert(ApplicationStages.Next(null) == ApplicationStages.Interested, "missing stage should safely restart at Interested");
}

static void DatabaseMigrationPreservesLegacyData()
{
    var databasePath = Path.Combine(
        Path.GetTempPath(),
        $"jobflow-migration-tests-{Guid.NewGuid():N}",
        "jobflow.db3");

    // Simulate a pre-migration installation: a cache table already exists but no migration ledger.
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    var legacyConnection = new SQLiteAsyncConnection(databasePath);
    legacyConnection.CreateTableAsync<CachedFeedRecord>().GetAwaiter().GetResult();
    legacyConnection.InsertAsync(new CachedFeedRecord
    {
        Id = "latest",
        GeneratedAt = "2026-10-24T12:00:00Z",
        SourcePath = "daily/2026-10-24.json",
        JsonPayload = "{\"schema_version\":1,\"jobs\":[]}",
        CachedAtUtc = DateTime.UtcNow
    }).GetAwaiter().GetResult();

    var database = new LocalDatabaseService(databasePath);
    database.InitializeAsync().GetAwaiter().GetResult();
    database.InitializeAsync().GetAwaiter().GetResult();

    var preserved = database.Connection.FindAsync<CachedFeedRecord>("latest").GetAwaiter().GetResult();
    Assert(preserved is not null, "migration must preserve an existing cached feed");
    Assert(preserved?.SourcePath == "daily/2026-10-24.json", "migration must preserve source metadata");

    var migrations = database.Connection.Table<DatabaseMigrationRecord>().ToListAsync().GetAwaiter().GetResult();
    Assert(migrations.Count(item => item.Version == 1) == 1, "repeated initialization must not duplicate migration records");
    Assert(migrations.Count(item => item.Version == 2) == 1, "job-cache migration should be applied once");
    Assert(database.Connection.Table<CachedJobRecord>().ToListAsync().GetAwaiter().GetResult() is not null,
        "migration should create the cached jobs table");
    Assert(database.Connection.Table<ApplicationRecord>().ToListAsync().GetAwaiter().GetResult() is not null,
        "migration should create the application table");
}

static void NewerDatabaseSchemaIsRejected()
{
    var databasePath = Path.Combine(
        Path.GetTempPath(),
        $"jobflow-newer-schema-tests-{Guid.NewGuid():N}",
        "jobflow.db3");
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

    var connection = new SQLiteAsyncConnection(databasePath);
    connection.CreateTableAsync<DatabaseMigrationRecord>().GetAwaiter().GetResult();
    connection.InsertAsync(new DatabaseMigrationRecord
    {
        Version = 3,
        AppliedAtUtc = DateTime.UtcNow,
        Description = "Simulated future schema"
    }).GetAwaiter().GetResult();

    var database = new LocalDatabaseService(databasePath);
    ExpectThrows<InvalidOperationException>(() => database.InitializeAsync().GetAwaiter().GetResult());
}

static void OfflineJobImportsAreStable()
{
    var databasePath = Path.Combine(Path.GetTempPath(), $"jobflow-offline-jobs-{Guid.NewGuid():N}", "jobflow.db3");
    var localDatabase = new LocalDatabaseService(databasePath);
    var store = new OfflineJobStore(localDatabase);
    var firstFeed = new DailyFeed
    {
        SchemaVersion = 1,
        GeneratedAt = "2026-10-25T10:00:00Z",
        SourcePath = "daily/2026-10-25.json",
        Jobs =
        [
            new JobFeedItem { JobId = "Job-ABC", Title = "Senior Engineer", Company = "Acme", ApplicationUrl = "https://jobs.example/apply" },
            new JobFeedItem { JobId = "job-abc", Title = "Duplicate", Company = "Duplicate Co" }
        ]
    };

    var imported = store.ImportFeedAsync(firstFeed).GetAwaiter().GetResult();
    Assert(imported == 1, "case-insensitive duplicate job IDs should import only once");
    var saved = store.FindAsync("JOB-ABC").GetAwaiter().GetResult();
    Assert(saved is not null && saved.Title == "Senior Engineer", "job should be retrievable offline by case-insensitive ID");
    Assert(saved?.ApplicationUrl == "https://jobs.example/apply", "safe HTTPS application URL should be retained");

    var tracker = new SQLiteAsyncConnection(databasePath);
    tracker.InsertAsync(new ApplicationRecord
    {
        JobId = "Job-ABC",
        Title = "Senior Engineer",
        Company = "Acme",
        Status = ApplicationStages.Preparing,
        Notes = "My private note",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    }).GetAwaiter().GetResult();

    var secondFeed = new DailyFeed
    {
        SchemaVersion = 1,
        GeneratedAt = "2026-10-26T10:00:00Z",
        SourcePath = "daily/2026-10-26.json",
        Jobs = [new JobFeedItem { JobId = "job-xyz", Title = "Staff Engineer", Company = "Beta" }]
    };
    store.ImportFeedAsync(secondFeed).GetAwaiter().GetResult();

    var all = store.GetAllAsync().GetAwaiter().GetResult();
    Assert(all.Count == 2, "a later feed omission must not delete previously cached jobs");
    var application = tracker.FindAsync<ApplicationRecord>("Job-ABC").GetAwaiter().GetResult();
    Assert(application?.Status == ApplicationStages.Preparing, "job imports must not overwrite application progress");
    Assert(application?.Notes == "My private note", "job imports must not overwrite user notes");
}
