using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;

namespace IvaoHub.Core.Services;

/// <summary>
/// The database's own named lock, <c>GET_LOCK</c>, held on a connection outside the pool (note
/// 2026-10-05-l-inizializzazione-sotto-blocco §3). One piece for the two things that must happen in one process at a
/// time: the initialisation of a database (<see cref="InitialisationLock"/>, one lock on a connection of its own) and the
/// runs of the scheduled jobs (<c>Jobs.JobLocks</c>, the locks of a process on one connection: note
/// 2026-10-09-i-job-che-recuperano).
/// </summary>
/// <remarks>
/// <para>The lock belongs to a <b>connection</b>, so it is held on one opened outside the pool and kept open until it is
/// released. Closing that connection releases the lock whatever else happened, and a process that dies takes its
/// connection with it: the lock cannot outlive its holder, and there is no expiry to guess.</para>
/// <para>A lock's name is the server's, and the server does not isolate databases: every name carries the database, so
/// two installations on the same server never wait for each other.</para>
/// <para>An instance is one lock on a connection of its own, and it never throws but for a caller that is being stopped:
/// whatever goes wrong is a lock not held, with the reason in <see cref="Failure"/> for the caller's log. The static
/// primitives are for a holder that keeps several locks on one connection, and do throw.</para>
/// </remarks>
public sealed class DatabaseLock : IAsyncDisposable
{
    /// <summary>
    /// The longest name this gives a lock, in characters. MariaDB 11.4 takes 192 bytes and refuses one more with an
    /// error: 64 characters fit whatever the characters are.
    /// </summary>
    public const int LongestName = 64;

    /// <summary>Why a lock is not held when the database could not be reached, or did not answer.</summary>
    public const string CouldNotBeAsked = "could not be asked for";

    /// <summary>Past the wait itself, so that the command never gives up before the database has answered.</summary>
    private static readonly TimeSpan CommandMargin = TimeSpan.FromSeconds(15);

    private MySqlConnection? _connection;

    private DatabaseLock(MySqlConnection? connection, string name, TimeSpan waited, string? refusal, bool busy, Exception? failure)
    {
        _connection = connection;
        Name = name;
        Waited = waited;
        Refusal = refusal;
        Busy = busy;
        Failure = failure;
    }

    /// <summary>The name the lock was asked under; empty when the database could not even say its own name.</summary>
    public string Name { get; }

    /// <summary>Whether the lock was taken.</summary>
    public bool Held => Refusal is null;

    /// <summary>From asking to the answer: the connection, and the wait when somebody else held it.</summary>
    public TimeSpan Waited { get; }

    /// <summary>
    /// Why it is not held, in words that may be written in <c>starts.txt</c>: <c>not free after N ms</c>, or
    /// <see cref="CouldNotBeAsked"/>. Null when it is held.
    /// </summary>
    public string? Refusal { get; }

    /// <summary>True when another connection held the lock for the whole wait: somebody else is doing the work.</summary>
    public bool Busy { get; }

    /// <summary>The exception that kept the lock from being asked for, for the log only, never for a file.</summary>
    public Exception? Failure { get; }

    /// <summary>
    /// The lock's name for a <paramref name="scope"/> under a <paramref name="prefix"/>: the two together, or the prefix
    /// and a hash of the scope when the two are longer than a name may be.
    /// </summary>
    public static string NameFor(string prefix, string scope)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(scope);

        var name = prefix + scope;
        return name.Length <= LongestName
            ? name
            : prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)))[..(LongestName - prefix.Length)];
    }

    /// <summary>
    /// Waits up to <paramref name="wait"/> for the lock that <paramref name="nameFor"/> names for the database of
    /// <paramref name="connectionString"/>, on a connection of its own. <see cref="TimeSpan.Zero"/> asks without waiting.
    /// </summary>
    public static async Task<DatabaseLock> TakeAsync(
        string? connectionString,
        Func<string, string> nameFor,
        TimeSpan wait,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nameFor);

        var watch = Stopwatch.StartNew();
        MySqlConnection? connection = null;
        var name = string.Empty;
        try
        {
            connection = await OpenAsync(connectionString, cancellationToken);
            name = nameFor(connection.Database);

            switch (await AskAsync(connection, name, wait, cancellationToken))
            {
                case true:
                    return new DatabaseLock(connection, name, watch.Elapsed, refusal: null, busy: false, failure: null);

                case false:
                    await connection.DisposeAsync();
                    return new DatabaseLock(
                        null,
                        name,
                        watch.Elapsed,
                        string.Create(CultureInfo.InvariantCulture, $"not free after {wait.TotalMilliseconds:0} ms"),
                        busy: true,
                        failure: null);

                default:
                    await connection.DisposeAsync();
                    return new DatabaseLock(null, name, watch.Elapsed, CouldNotBeAsked, busy: false, failure: null);
            }
        }
        catch (Exception exception) when (IsTheDatabases(exception) && !cancellationToken.IsCancellationRequested)
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            return new DatabaseLock(null, name, watch.Elapsed, CouldNotBeAsked, busy: false, exception);
        }
    }

    /// <summary>
    /// Releases the lock and closes its connection. Never throws: the closed connection releases the lock anyway.
    /// <c>StillHeld</c> is false when the lock was no longer this connection's at the end — its connection was lost on
    /// the way, and somebody else may have taken the lock meanwhile —, with the exception when there was one. Asked of a
    /// lock not held, or a second time, it does nothing and says false.
    /// </summary>
    public async ValueTask<(bool StillHeld, Exception? Failure)> ReleaseAsync()
    {
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is null)
        {
            return (false, null);
        }

        var stillHeld = false;
        Exception? failure = null;
        try
        {
            stillHeld = await GiveBackAsync(connection, Name);
        }
        catch (Exception exception) when (IsTheDatabases(exception))
        {
            failure = exception;
        }

        await connection.DisposeAsync();
        return (stillHeld, failure);
    }

    /// <summary>Releases the lock, saying nothing about how it went; <see cref="ReleaseAsync"/> says it.</summary>
    public async ValueTask DisposeAsync() => await ReleaseAsync();

    // ---- the primitives ----------------------------------------------------------------------------------------------

    /// <summary>A connection to the database of <paramref name="connectionString"/>, outside the pool: the pool would keep a lock alive after its holder.</summary>
    public static async Task<MySqlConnection> OpenAsync(string? connectionString, CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(
            new MySqlConnectionStringBuilder(connectionString ?? string.Empty) { Pooling = false }.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Asks <paramref name="connection"/>'s session for the lock: true when it is the session's, false when another session
    /// held it for the whole <paramref name="wait"/>, null when the database could not say.
    /// </summary>
    public static async Task<bool?> AskAsync(MySqlConnection connection, string name, TimeSpan wait, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = new MySqlCommand("SELECT GET_LOCK(@name, @seconds)", connection)
        {
            CommandTimeout = (int)Math.Ceiling((wait + CommandMargin).TotalSeconds),
        };
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@seconds", wait.TotalSeconds);

        // 1 when it is ours, 0 when the wait ended first, NULL when the database could not say.
        var answer = await command.ExecuteScalarAsync(cancellationToken);
        return answer is null or DBNull ? null : Convert.ToInt64(answer, CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>
    /// Gives the lock back on <paramref name="connection"/>'s session: true when it was still the session's. Not through
    /// anybody's token: a holder being stopped still gives its lock back.
    /// </summary>
    public static async Task<bool> GiveBackAsync(MySqlConnection connection, string name)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = new MySqlCommand("SELECT RELEASE_LOCK(@name)", connection) { CommandTimeout = 5 };
        command.Parameters.AddWithValue("@name", name);

        var answer = await command.ExecuteScalarAsync(CancellationToken.None);
        return answer is not null and not DBNull && Convert.ToInt64(answer, CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>What the database, or the way to it, throws: a lock not held, never a failure of whoever asked.</summary>
    public static bool IsTheDatabases(Exception exception) =>
        exception is DbException or InvalidOperationException or ArgumentException or TimeoutException;
}
