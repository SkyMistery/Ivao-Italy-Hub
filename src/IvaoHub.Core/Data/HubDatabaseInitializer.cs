using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Data;

/// <summary>
/// Applies the migration chain before the host accepts traffic. There is no shell on the
/// production server, so this is the only way migrations ever run (plan section 11.3): they are
/// additive only, and a failure stops the application instead of serving a half migrated database.
/// </summary>
public sealed class HubDatabaseInitializer(HubDbContext database, ILogger<HubDatabaseInitializer> logger)
{
    /// <summary>Migrations applied by this start, empty when the database was already up to date.</summary>
    public IReadOnlyList<string> AppliedMigrations { get; private set; } = [];

    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("Database is up to date, no migration to apply.");
        }
        else
        {
            logger.LogInformation("Applying {Count} migration(s): {Migrations}.", pending.Length, string.Join(", ", pending));
            await database.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied.");
        }

        AppliedMigrations = pending;
        return pending;
    }
}
