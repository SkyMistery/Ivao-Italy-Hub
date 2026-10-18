using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// What a module may ask the queue of the awards about its own rows (M4, E10x; note 2026-10-18-un-segnale-diventato-award): whether a
/// signal of theirs became an award. The one read of <c>cms_award_signals</c> a module writes, never a copy of its own.
/// </summary>
public static class AwardSignalQueries
{
    /// <summary>
    /// The source ids among <paramref name="sourceIds"/> of <paramref name="sourceModule"/> a signal of which became an award: its status is
    /// <see cref="AwardSignalStatus.Handled"/>, which only an assignment that answers it writes, and which never goes back. An assignment
    /// deleted from the register afterwards leaves the signal handled, and so the answer yes: the queue is a record of what was decided.
    /// <para>It works on any context that maps the table — the hub's, or a module's (<c>ModuleDbContext</c>) — and is given the caller's, so
    /// that inside a transaction (<c>Database.CurrentTransaction</c>) it reads the signal rows of those sources with <c>SELECT … FOR UPDATE</c>:
    /// the last committed version, held until the caller's transaction ends, so that an assignment in the same instant passes before or
    /// after it, never in between. Outside a transaction it is a plain read, and waits for nobody.</para>
    /// <para><b>Call it under READ COMMITTED.</b> What it answers is the last committed version under any isolation level, but what it locks
    /// is not: under REPEATABLE READ (MariaDB's default, and <c>BeginTransactionAsync()</c> without arguments) the <c>IN</c> on the index's
    /// prefix takes gap locks too, and two callers about sources with no signal yet can deadlock when both then write their signals.</para>
    /// <para>Only the context's own transaction counts: a transaction on another context, an ambient <c>TransactionScope</c>, or a call from
    /// a CRUD <c>BeforeSave</c> (which runs before the interceptor opens its transaction) gets the plain read, without a lock and without a
    /// word.</para>
    /// </summary>
    public static async Task<IReadOnlySet<string>> AssignedAsync(
        DbContext context,
        string sourceModule,
        IReadOnlyCollection<string> sourceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);
        ArgumentNullException.ThrowIfNull(sourceIds);

        string[] ids = [.. sourceIds.Distinct(StringComparer.Ordinal)];
        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (context.Database.CurrentTransaction is null)
        {
            return (await context.Set<AwardSignal>().AsNoTracking()
                    .Where(row => row.SourceModule == sourceModule && ids.Contains(row.SourceId) && row.Status == AwardSignalStatus.Handled)
                    .Select(row => row.SourceId)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);
        }

        // Every signal row of the sources is locked, whatever its status: the one an assignment is about to answer is a pending one. InnoDB
        // takes the locks while it scans the index (source_module, source_id, vid), in that index's order, so two callers meet in the same
        // order; an ORDER BY would only sort what comes back, after the locks.
        var placeholders = string.Join(", ", ids.Select((_, index) => $"{{{index + 1}}}"));
        var locked = await context.Database
            .SqlQuery<LockedSignal>(FormattableStringFactory.Create(
                $"SELECT source_id, status FROM cms_award_signals WHERE source_module = {{0}} AND source_id IN ({placeholders}) FOR UPDATE",
                [sourceModule, .. ids]))
            .ToListAsync(cancellationToken);

        return locked
            .Where(row => string.Equals(row.Status, nameof(AwardSignalStatus.Handled), StringComparison.Ordinal))
            .Select(row => row.SourceId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A signal row as the locking read returns it, its columns named by the hub's convention (<c>source_id</c>, <c>status</c>); the status as
    /// the column holds it, a word.
    /// </summary>
    private sealed class LockedSignal
    {
        public string SourceId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}
