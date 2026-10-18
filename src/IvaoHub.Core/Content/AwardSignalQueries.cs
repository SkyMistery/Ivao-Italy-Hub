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
    /// the last committed version, under any isolation level, held until the caller's transaction ends, so that an assignment in the same
    /// instant passes before or after it, never in between. Outside a transaction it is a plain read, and waits for nobody.</para>
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

        // Every signal row of the sources is locked, whatever its status: the one an assignment is about to answer is a pending one. In the
        // order of their ids, so that two callers never take them crosswise.
        var placeholders = string.Join(", ", ids.Select((_, index) => $"{{{index + 1}}}"));
        var locked = await context.Database
            .SqlQuery<LockedSignal>(FormattableStringFactory.Create(
                $"SELECT source_id, status FROM cms_award_signals WHERE source_module = {{0}} AND source_id IN ({placeholders}) ORDER BY id FOR UPDATE",
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
