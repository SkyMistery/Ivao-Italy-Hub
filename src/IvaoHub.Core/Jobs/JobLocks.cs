using IvaoHub.Core.Services;
using MySqlConnector;

namespace IvaoHub.Core.Jobs;

/// <summary>What a process got when it asked for a job's lock.</summary>
public enum JobLockAnswer
{
    /// <summary>The lock is this process's until the run ends.</summary>
    Held,

    /// <summary>Another process — or another run in this one — holds it: the run is theirs.</summary>
    Busy,

    /// <summary>The database could not be asked, or did not answer: the run goes ahead without the lock.</summary>
    NotAsked,
}

/// <summary>
/// The named locks of the jobs one process runs (note 2026-10-09-i-job-che-recuperano), all held on <b>one</b> connection of
/// the process's own, outside the pool (<see cref="DatabaseLock"/>): opened with the first lock, closed when the last goes
/// back. However many jobs run at once — a whole night of them made up in the morning — the process holds one connection
/// more than its pool, and only while a job runs: the server caps the connections of a user for everybody together.
/// </summary>
/// <remarks>
/// <para>A lock is <c>hub-job:&lt;database&gt;:&lt;job&gt;</c>, asked without waiting. A process that dies takes the
/// connection, and every lock on it, with it.</para>
/// <para>The session would give the same lock twice (MariaDB counts them), so this process asks for each job once.</para>
/// <para>A connection that falls while jobs run loses all their locks at once: each run ends as it would, and says at its
/// end that its lock was lost on the way.</para>
/// </remarks>
public sealed class JobLocks : IAsyncDisposable
{
    /// <summary>What the name of every job's lock begins with; the database and the job follow.</summary>
    public const string Prefix = "hub-job:";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);
    private MySqlConnection? _connection;

    /// <summary>The name of a job's lock in a database: the server's names are shared by every database it holds.</summary>
    public static string NameFor(string database, string job) => DatabaseLock.NameFor(Prefix, $"{database}:{job}");

    /// <summary>Asks for a job's lock without waiting, on the process's connection, opening it when it is the first.</summary>
    public async Task<(JobLockAnswer Answer, Exception? Failure)> TakeAsync(
        string? connectionString,
        string job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_held.Contains(job))
            {
                return (JobLockAnswer.Busy, null);
            }

            _connection ??= await DatabaseLock.OpenAsync(connectionString, cancellationToken);

            switch (await DatabaseLock.AskAsync(_connection, NameFor(_connection.Database, job), TimeSpan.Zero, cancellationToken))
            {
                case true:
                    _held.Add(job);
                    return (JobLockAnswer.Held, null);

                case false:
                    await CloseIfIdleAsync();
                    return (JobLockAnswer.Busy, null);

                default:
                    await CloseIfIdleAsync();
                    return (JobLockAnswer.NotAsked, null);
            }
        }
        catch (Exception exception) when (DatabaseLock.IsTheDatabases(exception) && !cancellationToken.IsCancellationRequested)
        {
            await DropAsync();
            return (JobLockAnswer.NotAsked, exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Gives a job's lock back, and closes the connection when it held nothing else. Never throws: false, with the
    /// exception when there was one, when the lock was no longer this process's — its connection fell on the way.
    /// </summary>
    public async Task<(bool StillHeld, Exception? Failure)> ReleaseAsync(string job)
    {
        ArgumentNullException.ThrowIfNull(job);

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (!_held.Remove(job) || _connection is null)
            {
                return (false, null);
            }

            var stillHeld = await DatabaseLock.GiveBackAsync(_connection, NameFor(_connection.Database, job));
            await CloseIfIdleAsync();
            return (stillHeld, null);
        }
        catch (Exception exception) when (DatabaseLock.IsTheDatabases(exception))
        {
            // The connection is gone, and every lock it held with it.
            await DropAsync();
            return (false, exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The connection goes with the process's services. The gate stays usable: a run that ends while the host stops still
    /// gives its lock back, and finds nothing to give.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            await DropAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>No lock left on the connection: it closes, and the next lock opens another.</summary>
    private async Task CloseIfIdleAsync()
    {
        if (_held.Count == 0)
        {
            await DropAsync();
        }
    }

    /// <summary>The connection goes, and every lock it held: closing it releases them on the server.</summary>
    private async Task DropAsync()
    {
        _held.Clear();

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
