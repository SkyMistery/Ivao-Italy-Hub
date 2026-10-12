using System.Globalization;
using IvaoHub.Core.Jobs;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The locks of the jobs on MariaDB itself (note <c>decisions/2026-10-09-i-job-che-recuperano.md</c>): a process holds every
/// lock of its jobs on one connection, and only while a job runs; another process finds them busy; a lock given back is free
/// for the other at once. Two <see cref="JobLocks"/> are two processes.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class JobLocksTests(MariaDbFixture mariaDb)
{
    private const string First = "e10j-lock-first";
    private const string Second = "e10j-lock-second";

    [Fact]
    public async Task AProcessHoldsTheLocksOfItsJobsOnOneConnectionAndOnlyWhileTheyRun()
    {
        var token = TestContext.Current.CancellationToken;
        await using var one = new JobLocks();
        await using var other = new JobLocks();

        // Two jobs of one process: two locks, one connection.
        Assert.Equal(JobLockAnswer.Held, (await one.TakeAsync(mariaDb.ConnectionString, First, token)).Answer);
        Assert.Equal(JobLockAnswer.Held, (await one.TakeAsync(mariaDb.ConnectionString, Second, token)).Answer);

        var holderOfFirst = await HolderAsync(First, token);
        Assert.NotNull(holderOfFirst);
        Assert.Equal(holderOfFirst, await HolderAsync(Second, token));

        // The same job again in that process, or in another: busy.
        Assert.Equal(JobLockAnswer.Busy, (await one.TakeAsync(mariaDb.ConnectionString, First, token)).Answer);
        Assert.Equal(JobLockAnswer.Busy, (await other.TakeAsync(mariaDb.ConnectionString, First, token)).Answer);

        // Given back, it is the other's at once; the connection stays while it holds the second.
        Assert.True((await one.ReleaseAsync(First)).StillHeld);
        Assert.Equal(JobLockAnswer.Held, (await other.TakeAsync(mariaDb.ConnectionString, First, token)).Answer);
        Assert.Equal(holderOfFirst, await HolderAsync(Second, token));

        // The last lock given back closes the connection.
        Assert.True((await one.ReleaseAsync(Second)).StillHeld);
        Assert.False(await ConnectionAliveAsync(holderOfFirst.Value, token));

        Assert.True((await other.ReleaseAsync(First)).StillHeld);
        Assert.Null(await HolderAsync(First, token));

        // A lock given back twice, or never taken, is not held: nothing breaks.
        Assert.False((await other.ReleaseAsync(First)).StillHeld);
        Assert.False((await other.ReleaseAsync("e10j-lock-never")).StillHeld);
    }

    [Fact]
    public async Task ADatabaseThatCannotBeAskedIsNotALockAndNotAFailure()
    {
        await using var locks = new JobLocks();

        // Nobody answers at that address: the run is told to go ahead without the lock, with the reason for the log.
        var (answer, failure) = await locks.TakeAsync(
            "Server=127.0.0.1;Port=1;Database=nobody;User ID=nobody;Password=nobody;Connection Timeout=2",
            First,
            TestContext.Current.CancellationToken);

        Assert.Equal(JobLockAnswer.NotAsked, answer);
        Assert.NotNull(failure);
    }

    /// <summary>The connection that holds a job's lock, or null when nobody does.</summary>
    private async Task<long?> HolderAsync(string job, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("SELECT IS_USED_LOCK(@name)", connection);
        command.Parameters.AddWithValue("@name", JobLocks.NameFor(connection.Database, job));

        var holder = await command.ExecuteScalarAsync(cancellationToken);
        return holder is null or DBNull ? null : Convert.ToInt64(holder, CultureInfo.InvariantCulture);
    }

    private async Task<bool> ConnectionAliveAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE ID = @id", connection);
        command.Parameters.AddWithValue("@id", id);

        // The server notices a closed connection in a moment, not at once.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 0)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return true;
    }
}
