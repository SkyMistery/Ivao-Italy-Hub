using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Ivao;

/// <summary>What a start did about the reference data.</summary>
public enum FirstReferenceSyncOutcome
{
    /// <summary>The division's airspace was already there: nothing to do, the case of every start but the first.</summary>
    Present,

    /// <summary>This start synchronised, with the initialisation lock in its hands.</summary>
    Synced,

    /// <summary>This start synchronised without the lock: it could not be taken, and another start may have done it too.</summary>
    SyncedWithoutTheLock,

    /// <summary>This start waited for the lock, and the start that held it had synchronised meanwhile.</summary>
    AnotherStartSynced,
}

/// <summary>
/// The first synchronisation of the reference data, which a start runs when the database holds no centre of the division's
/// country (note 2026-09-28-l-avvio-a-freddo, §4.3: a hub that does not know its own FIRs cannot recognise a FIR staff
/// position). Under the initialisation lock (issue #325, item 7; note 2026-11-18-il-primo-allineamento-sotto-blocco):
/// two processes that start together on a fresh installation used to download the whole snapshot each.
/// </summary>
/// <remarks>
/// <para>The same lock as the initialisation's (<see cref="InitialisationLock"/>, note 2026-10-05-l-inizializzazione-sotto-blocco),
/// with the same rule: the check comes first and costs one query, so a start that finds the airspace never asks for the lock;
/// a start that has to synchronise takes it, and asks again with the lock in its hands. Of two starts on an empty table, one
/// synchronises; the other waits, finds the centres and goes on — the airspace is there for both exactly when it would have
/// been for the first.</para>
/// <para>A lock not taken is never a start without data: the wait ends and the start synchronises, as every start did before
/// the lock. A synchronisation that failed leaves the table empty, and the next start, or the one that was waiting, tries
/// again: the run's own row in <c>hub_jobs_log</c> says why.</para>
/// </remarks>
public sealed class FirstReferenceSync(
    HubDbContext database,
    RefDataSyncJob sync,
    IOptions<DivisionOptions> division,
    ILogger<FirstReferenceSync> logger)
{
    /// <summary>How long a start waits for another one's synchronisation before it goes on without the lock.</summary>
    public TimeSpan LockWait { get; set; } = InitialisationLock.DefaultWait;

    /// <summary>Synchronises when the division's airspace is missing, one process at a time.</summary>
    public Task<FirstReferenceSyncOutcome> RunAsync(CancellationToken cancellationToken = default) =>
        RunAsync(ConnectionString(), HasAirspaceAsync, sync.RunAsync, LockWait, logger, cancellationToken);

    /// <summary>
    /// The rule, apart from what it checks and what it runs: <paramref name="present"/> first, then, only when it says no,
    /// the lock of the database <paramref name="connectionString"/> names, <paramref name="present"/> again with the lock in
    /// hand, and <paramref name="synchronise"/> when it still says no or the lock could not be taken.
    /// </summary>
    public static async Task<FirstReferenceSyncOutcome> RunAsync(
        string? connectionString,
        Func<CancellationToken, Task<bool>> present,
        Func<CancellationToken, Task> synchronise,
        TimeSpan lockWait,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(synchronise);
        ArgumentNullException.ThrowIfNull(logger);

        if (await present(cancellationToken))
        {
            return FirstReferenceSyncOutcome.Present;
        }

        // Held until the synchronisation ends, so that whoever waits behind it finds the centres.
        await using var held = await InitialisationLock.TakeAsync(connectionString, lockWait, logger, cancellationToken);

        if (held.Held && await present(cancellationToken))
        {
            logger.LogInformation(
                "Another start synchronised the reference data while this one waited {Waited:0} ms for the initialisation lock.",
                held.Report.Waited.TotalMilliseconds);
            return FirstReferenceSyncOutcome.AnotherStartSynced;
        }

        await synchronise(cancellationToken);
        return held.Held ? FirstReferenceSyncOutcome.Synced : FirstReferenceSyncOutcome.SyncedWithoutTheLock;
    }

    /// <summary>
    /// Whether the table holds a centre of the division's country, counted as the FIR directory counts them: a table that
    /// holds only another country's centres — a countryId corrected since — is an airspace this hub does not know yet.
    /// </summary>
    private Task<bool> HasAirspaceAsync(CancellationToken cancellationToken)
    {
        var country = division.Value.CountryId;
        return database.IvaoCenters.AnyAsync(center => center.CountryId == country, cancellationToken);
    }

    /// <summary>The database the centres are in, for the lock's own connection; null when the context cannot say.</summary>
    private string? ConnectionString()
    {
        try
        {
            return database.Database.GetConnectionString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
