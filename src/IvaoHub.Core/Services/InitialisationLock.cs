using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace IvaoHub.Core.Services;

/// <summary>
/// The lock that lets one process at a time initialise a database (note 2026-10-05-l-inizializzazione-sotto-blocco,
/// issue #203): two processes of the hub that start in the same second without a valid mark used to initialise together,
/// and the second one stopped on the unique key of a row both had seeded.
/// </summary>
/// <remarks>
/// <para>It is the database's own named lock, <c>GET_LOCK</c>, which belongs to a <b>connection</b>: so it is taken on a
/// connection of its own, opened outside the pool and kept open until the initialisation ends. Closing that connection
/// releases the lock whatever else happened, and a process that dies takes its connection with it: the lock cannot
/// outlive its holder.</para>
/// <para>The server does not isolate databases and a lock's name is the server's: the name carries the database, so two
/// installations on the same server never wait for each other.</para>
/// <para>A lock not taken is never a failed start: <see cref="Held"/> is false, the start initialises without it, as
/// every start did before, and says so.</para>
/// </remarks>
public sealed class InitialisationLock : IAsyncDisposable
{
    /// <summary>What every name begins with; the database's name follows.</summary>
    public const string NamePrefix = "hub-init:";

    /// <summary>
    /// The longest name this gives a lock, in characters. MariaDB 11.4 takes 192 bytes and refuses one more with an
    /// error: 64 characters fit whatever the characters are, and a database's name is at most as long.
    /// </summary>
    public const int LongestName = 64;

    /// <summary>
    /// How long a start waits for another one's initialisation: many times what an initialisation takes, and well inside
    /// the time the host gives a process to start.
    /// </summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);

    /// <summary>Past the wait itself, so that the command never gives up before the database has answered.</summary>
    private static readonly TimeSpan CommandMargin = TimeSpan.FromSeconds(15);

    private readonly MySqlConnection? _connection;
    private readonly string _name;
    private readonly ILogger _logger;

    private InitialisationLock(MySqlConnection? connection, string name, InitialisationLockReport report, ILogger logger)
    {
        _connection = connection;
        _name = name;
        _logger = logger;
        Report = report;
    }

    /// <summary>Whether this start holds the lock, and how long it took to know.</summary>
    public InitialisationLockReport Report { get; }

    public bool Held => Report.Held;

    /// <summary>
    /// The lock's name for a database: the prefix and the database's name, or a hash of it when the two together are
    /// longer than a name may be.
    /// </summary>
    public static string NameFor(string database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var name = NamePrefix + database;
        return name.Length <= LongestName
            ? name
            : NamePrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(database)))[..(LongestName - NamePrefix.Length)];
    }

    /// <summary>
    /// Waits up to <paramref name="wait"/> for the lock of the database <paramref name="connectionString"/> names. It
    /// never throws but for a start that is being stopped: whatever goes wrong is a lock not held.
    /// </summary>
    public static async Task<InitialisationLock> TakeAsync(
        string? connectionString,
        TimeSpan wait,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var watch = Stopwatch.StartNew();
        MySqlConnection? connection = null;
        try
        {
            // Outside the pool: the lock lives as long as this connection, and the pool would keep it alive after us.
            connection = new MySqlConnection(new MySqlConnectionStringBuilder(connectionString ?? string.Empty) { Pooling = false }.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var name = NameFor(connection.Database);
            await using var command = new MySqlCommand("SELECT GET_LOCK(@name, @seconds)", connection)
            {
                CommandTimeout = (int)Math.Ceiling((wait + CommandMargin).TotalSeconds),
            };
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@seconds", wait.TotalSeconds);

            // 1 when it is ours, 0 when the wait ended first, NULL when the database could not say.
            var answer = await command.ExecuteScalarAsync(cancellationToken);
            if (answer is not null and not DBNull && Convert.ToInt64(answer, CultureInfo.InvariantCulture) == 1)
            {
                return new InitialisationLock(connection, name, new InitialisationLockReport(Held: true, watch.Elapsed, Refusal: null), logger);
            }

            await connection.DisposeAsync();
            var refusal = answer is null or DBNull
                ? InitialisationLockReport.CouldNotBeAsked
                : string.Create(CultureInfo.InvariantCulture, $"not free after {wait.TotalMilliseconds:0} ms");
            logger.LogWarning(
                "The initialisation lock {Name} was not taken ({Refusal}): this start initialises without it.", name, refusal);
            return new InitialisationLock(null, name, new InitialisationLockReport(Held: false, watch.Elapsed, refusal), logger);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or ArgumentException or TimeoutException
            && !cancellationToken.IsCancellationRequested)
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            logger.LogWarning(
                exception,
                "The initialisation lock could not be asked for ({Reason}): this start initialises without it.",
                exception.GetBaseException().Message);
            return new InitialisationLock(
                null,
                string.Empty,
                new InitialisationLockReport(Held: false, watch.Elapsed, InitialisationLockReport.CouldNotBeAsked),
                logger);
        }
    }

    /// <summary>Releases the lock and closes its connection. Never throws: the closed connection releases it anyway.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await using var command = new MySqlCommand("SELECT RELEASE_LOCK(@name)", _connection) { CommandTimeout = 5 };
            command.Parameters.AddWithValue("@name", _name);

            // Not through the start's token: a start being stopped still gives the lock back.
            var answer = await command.ExecuteScalarAsync(CancellationToken.None);
            if (answer is null or DBNull || Convert.ToInt64(answer, CultureInfo.InvariantCulture) != 1)
            {
                Lost(null);
            }
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException)
        {
            Lost(exception);
        }

        await _connection.DisposeAsync();
    }

    /// <summary>The connection fell during the initialisation, and the lock with it: another start may have run beside this one.</summary>
    private void Lost(Exception? exception) => _logger.LogWarning(
        exception,
        "The initialisation lock {Name} was no longer held when the initialisation ended: its connection was lost on the way, and another start may have initialised at the same time.",
        _name);
}

/// <summary>What a start got when it asked for the initialisation lock.</summary>
/// <param name="Held">True when the start initialised, or read the mark again, with the lock in its hands.</param>
/// <param name="Waited">From asking to the answer: the connection, and another start's initialisation when there was one.</param>
/// <param name="Refusal">Why it is not held, in the words of <c>starts.txt</c>; null when it is.</param>
public sealed record InitialisationLockReport(bool Held, TimeSpan Waited, string? Refusal)
{
    /// <summary>The database could not be reached, or did not answer: the reason is in the log, never in <c>starts.txt</c>.</summary>
    public const string CouldNotBeAsked = "could not be asked for";
}
