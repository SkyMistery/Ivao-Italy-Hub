using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The first synchronisation of the reference data under the initialisation lock, on the real database (issue #325, item 7;
/// note 2026-11-18-il-primo-allineamento-sotto-blocco): of two starts on a fresh installation one downloads and the other
/// waits and finds the centres; a start that finds them never asks for the lock; a start that waited behind a failed
/// download tries itself; a lock that cannot be taken never leaves a start without its airspace. And the download is the
/// centres alone, which leaves the scheduled job due (the maintainer's answer on the issue, «B, solo i centri»).
/// </summary>
/// <remarks>What is checked and what is run are the test's own, an airspace of a flag and a download that counts: the
/// centres of the shared database belong to every class of the suite, and emptying them would change what the others read.
/// The lock is the real one.</remarks>
[Collection(MariaDbCollection.Name)]
public sealed class FirstReferenceSyncTests(MariaDbFixture mariaDb)
{
    [Fact]
    public async Task OfTwoStartsOnAFreshInstallationOneDownloadsAndTheOtherWaitsAndFindsTheCentres()
    {
        var token = TestContext.Current.CancellationToken;
        var airspace = new FakeAirspace();

        // The download holds the lock until the other start is waiting for it, as a download of 26 MB would.
        var outcomes = await Task.WhenAll(
            RunAsync(airspace, async cancellationToken =>
            {
                await WaitForALockWaiterAsync(cancellationToken);
                airspace.Download();
            }, token),
            RunAsync(airspace, async cancellationToken =>
            {
                await WaitForALockWaiterAsync(cancellationToken);
                airspace.Download();
            }, token));

        Assert.Equal(1, airspace.Downloads);
        Assert.Contains(FirstReferenceSyncOutcome.Synced, outcomes);
        Assert.Contains(FirstReferenceSyncOutcome.AnotherStartSynced, outcomes);
    }

    [Fact]
    public async Task AStartThatFindsTheCentresNeitherDownloadsNorAsksForTheLock()
    {
        var token = TestContext.Current.CancellationToken;
        var airspace = new FakeAirspace();
        airspace.Download();

        // Somebody holds the lock: a start that asked for it would wait, and this one must not.
        await using var holder = await HoldTheLockAsync(token);

        var outcome = await RunAsync(
            airspace, _ => throw new InvalidOperationException("not again"), token, lockWait: TimeSpan.FromSeconds(10));

        Assert.Equal(FirstReferenceSyncOutcome.Present, outcome);
        Assert.Equal(1, airspace.Downloads);
    }

    [Fact]
    public async Task AStartThatWaitedBehindAFailedDownloadDownloadsItself()
    {
        var token = TestContext.Current.CancellationToken;
        var airspace = new FakeAirspace();

        // IVAO did not answer the first: RefDataSyncJob never throws, it leaves the table empty and a failed row.
        var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failing = RunAsync(
            airspace,
            async cancellationToken =>
            {
                inside.SetResult();
                await WaitForALockWaiterAsync(cancellationToken);
                airspace.Fail();
            },
            token);
        await inside.Task;

        var second = await RunAsync(airspace, _ => { airspace.Download(); return Task.CompletedTask; }, token);

        Assert.Equal(FirstReferenceSyncOutcome.Synced, await failing);
        Assert.Equal(FirstReferenceSyncOutcome.Synced, second);
        Assert.Equal(1, airspace.Failures);
        Assert.Equal(1, airspace.Downloads);
    }

    [Fact]
    public async Task AStartThatCannotTakeTheLockDownloadsWithoutIt()
    {
        var token = TestContext.Current.CancellationToken;
        var airspace = new FakeAirspace();

        // A process that hangs with the lock: the wait ends, and the start downloads as every start did before the lock.
        await using var holder = await HoldTheLockAsync(token);

        var outcome = await RunAsync(
            airspace, _ => { airspace.Download(); return Task.CompletedTask; }, token, lockWait: TimeSpan.FromMilliseconds(300));

        Assert.Equal(FirstReferenceSyncOutcome.SyncedWithoutTheLock, outcome);
        Assert.Equal(1, airspace.Downloads);
    }

    [Fact]
    public async Task TheHubsOwnStartFindsTheAirspaceItSynchronised()
    {
        // The host's start synchronised from the fixtures, because the division's centres were missing or were already
        // there: either way the start after it finds them, and asks nobody.
        var token = TestContext.Current.CancellationToken;
        await using var factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<FirstReferenceSync>().RunAsync(token);

        Assert.Equal(FirstReferenceSyncOutcome.Present, outcome);
    }

    [Fact]
    public async Task TheStartDownloadsOnlyTheCentresAndLeavesTheScheduledJobDue()
    {
        // «B, solo i centri» (the maintainer, issue #325): the start's download is the centres alone, recorded under a name of
        // its own, so that the scheduled job has not run and the first round of the jobs takes the rest of the snapshot.
        var token = TestContext.Current.CancellationToken;
        await using var factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var syncRuns = await database.JobsLog.CountAsync(entry => entry.Job == RefDataSyncJob.JobName, token);
        var airports = await database.IvaoAirports.AsNoTracking().MaxAsync(airport => (DateTime?)airport.SyncedAt, token);
        var centres = await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunCentresAsync(token);

        Assert.True(centres > 0);
        Assert.Contains(
            await database.IvaoCenters.AsNoTracking().Select(center => center.Id).ToListAsync(token),
            id => id == "LIRR");

        var run = await database.JobsLog.AsNoTracking()
            .Where(entry => entry.Job == RefDataSyncJob.CentresJobName)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(token);
        Assert.Equal("succeeded", run.Status);
        Assert.NotNull(run.FinishedAt);
        Assert.StartsWith($"{centres} centre(s) for ", run.Message, StringComparison.Ordinal);

        // Nothing else of the snapshot was touched, and the scheduled job has no run more than before.
        Assert.Equal(syncRuns, await database.JobsLog.CountAsync(entry => entry.Job == RefDataSyncJob.JobName, token));
        Assert.Equal(airports, await database.IvaoAirports.AsNoTracking().MaxAsync(airport => (DateTime?)airport.SyncedAt, token));
    }

    private Task<FirstReferenceSyncOutcome> RunAsync(
        FakeAirspace airspace,
        Func<CancellationToken, Task> synchronise,
        CancellationToken cancellationToken,
        TimeSpan? lockWait = null) =>
        FirstReferenceSync.RunAsync(
            mariaDb.ConnectionString,
            _ => Task.FromResult(airspace.Present),
            synchronise,
            lockWait ?? TimeSpan.FromSeconds(30),
            NullLogger.Instance,
            cancellationToken);

    /// <summary>The initialisation lock, taken on a connection of the test's own and held until it is closed.</summary>
    private async Task<MySqlConnection> HoldTheLockAsync(CancellationToken cancellationToken)
    {
        var connection = new MySqlConnection(new MySqlConnectionStringBuilder(mariaDb.ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var take = new MySqlCommand("SELECT GET_LOCK(@name, 0)", connection);
        take.Parameters.AddWithValue("@name", InitialisationLock.NameFor(connection.Database));
        Assert.Equal(1L, Convert.ToInt64(await take.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture));
        return connection;
    }

    /// <summary>Until a connection waits for a named lock: a start behind the one that is downloading.</summary>
    private async Task WaitForALockWaiterAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE STATE = 'User lock' AND INFO LIKE '%GET_LOCK%' AND ID <> CONNECTION_ID()",
            connection);
        for (var tries = 0; tries < 100; tries++)
        {
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        Assert.Fail("No start ever waited for the initialisation lock.");
    }

    /// <summary>The division's centres as a flag, and the downloads that set it.</summary>
    private sealed class FakeAirspace
    {
        private int _downloads;
        private int _failures;
        private volatile bool _present;

        public bool Present => _present;

        public int Downloads => _downloads;

        public int Failures => _failures;

        public void Download()
        {
            Interlocked.Increment(ref _downloads);
            _present = true;
        }

        public void Fail() => Interlocked.Increment(ref _failures);
    }
}
