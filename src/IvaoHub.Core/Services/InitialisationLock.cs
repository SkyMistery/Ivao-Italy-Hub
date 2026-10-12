using Microsoft.Extensions.Logging;

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
/// outlive its holder. The mechanics are <see cref="DatabaseLock"/>'s, shared with the scheduled jobs (note
/// 2026-10-09-i-job-che-recuperano); what this adds is its name, its wait and what a start says about it.</para>
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
    public const int LongestName = DatabaseLock.LongestName;

    /// <summary>
    /// How long a start waits for another one's initialisation: many times what an initialisation takes, and well inside
    /// the time the host gives a process to start.
    /// </summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(30);

    private readonly DatabaseLock _lock;
    private readonly ILogger _logger;

    private InitialisationLock(DatabaseLock held, ILogger logger)
    {
        _lock = held;
        _logger = logger;
        Report = new InitialisationLockReport(held.Held, held.Waited, held.Refusal);
    }

    /// <summary>Whether this start holds the lock, and how long it took to know.</summary>
    public InitialisationLockReport Report { get; }

    public bool Held => Report.Held;

    /// <summary>
    /// The lock's name for a database: the prefix and the database's name, or a hash of it when the two together are
    /// longer than a name may be.
    /// </summary>
    public static string NameFor(string database) => DatabaseLock.NameFor(NamePrefix, database);

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

        var held = await DatabaseLock.TakeAsync(connectionString, NameFor, wait, cancellationToken);
        if (held.Failure is not null)
        {
            logger.LogWarning(
                held.Failure,
                "The initialisation lock could not be asked for ({Reason}): this start initialises without it.",
                held.Failure.GetBaseException().Message);
        }
        else if (!held.Held)
        {
            logger.LogWarning(
                "The initialisation lock {Name} was not taken ({Refusal}): this start initialises without it.", held.Name, held.Refusal);
        }

        return new InitialisationLock(held, logger);
    }

    /// <summary>Releases the lock and closes its connection. Never throws: the closed connection releases it anyway.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!Held)
        {
            return;
        }

        var (stillHeld, failure) = await _lock.ReleaseAsync();
        if (!stillHeld)
        {
            // The connection fell during the initialisation, and the lock with it: another start may have run beside this one.
            _logger.LogWarning(
                failure,
                "The initialisation lock {Name} was no longer held when the initialisation ended: its connection was lost on the way, and another start may have initialised at the same time.",
                _lock.Name);
        }
    }
}

/// <summary>What a start got when it asked for the initialisation lock.</summary>
/// <param name="Held">True when the start initialised, or read the mark again, with the lock in its hands.</param>
/// <param name="Waited">From asking to the answer: the connection, and another start's initialisation when there was one.</param>
/// <param name="Refusal">Why it is not held, in the words of <c>starts.txt</c>; null when it is.</param>
public sealed record InitialisationLockReport(bool Held, TimeSpan Waited, string? Refusal)
{
    /// <summary>The database could not be reached, or did not answer: the reason is in the log, never in <c>starts.txt</c>.</summary>
    public const string CouldNotBeAsked = DatabaseLock.CouldNotBeAsked;
}
