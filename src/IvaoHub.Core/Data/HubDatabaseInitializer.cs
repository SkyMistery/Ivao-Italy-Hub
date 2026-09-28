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
        AppliedMigrations = await MigrateAsync(database, cancellationToken);
        return AppliedMigrations;
    }

    /// <summary>
    /// The same for the context of a module, with its own history table. Asked first and applied only when something is
    /// pending: <c>MigrateAsync</c> takes the migration lock, creates the history table if needed and opens a transaction
    /// even with nothing to do, and on one CPU that cost a start up to a second (note 2026-09-28-l-avvio-a-freddo, §3).
    /// </summary>
    public async Task<IReadOnlyList<string>> MigrateAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = context.GetType().Name;
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("{Context} is up to date, no migration to apply.", name);
        }
        else
        {
            logger.LogInformation(
                "{Context}: applying {Count} migration(s): {Migrations}.", name, pending.Length, string.Join(", ", pending));
            await context.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("{Context}: migrations applied.", name);
        }

        return pending;
    }
}
