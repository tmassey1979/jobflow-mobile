using SQLite;

namespace JobFlow.Mobile.Models;

/// <summary>
/// Records an applied local SQLite schema migration. Contains no user content or credentials.
/// </summary>
[Table("database_migrations")]
public sealed class DatabaseMigrationRecord
{
    [PrimaryKey]
    public int Version { get; set; }

    public DateTime AppliedAtUtc { get; set; }

    public string Description { get; set; } = "";
}
