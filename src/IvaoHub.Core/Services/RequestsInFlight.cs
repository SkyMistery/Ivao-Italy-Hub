namespace IvaoHub.Core.Services;

/// <summary>
/// The requests running in the process, one counter for all who ask (issue #399): for each request the others it overlapped
/// (issue #402, note 2026-11-14-le-richieste-in-corso-nella-diagnostica) — the ones already running when it began, and the
/// ones that began while it ran, which are the numbers given after its own —, and how long the hub has had none running. The
/// column <c>others_n</c> of <c>requests-YYYYMMDD.txt</c>, the <c>in flight</c> of a <c>STOP</c> line in <c>starts.txt</c>
/// and the first check of the jobs that are due (<c>JobCatchUp</c>, note 2026-11-14-il-primo-giro-dei-job-dopo-la-pagina)
/// read it.
/// </summary>
/// <remarks>
/// <para>One instance in the process, fed by one middleware at the top of the pipeline. A check of <c>/health</c> is not a
/// request here: a monitor's, not a visitor's (note 2026-10-26-il-ping-che-tiene-sveglio-l-hub).</para>
/// <para>Three counters and no lock: a request that begins in the same instant as another may miss it, and an end in the same
/// instant as a look may be seen a moment late, which a count of diagnostics and a wait of seconds can afford.</para>
/// </remarks>
public sealed class RequestsInFlight
{
    private readonly TimeProvider _time;
    private long _running;
    private long _begun;
    private long _lastEnd;

    public RequestsInFlight()
        : this(TimeProvider.System)
    {
    }

    /// <summary>With the clock of a test; until a request ends, the hub has been quiet since this instance was made.</summary>
    public RequestsInFlight(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _time = time;
        _lastEnd = time.GetTimestamp();
    }

    /// <summary>The requests running now.</summary>
    public long Running => Interlocked.Read(ref _running);

    /// <summary>The requests begun in the process so far.</summary>
    public long Begun => Interlocked.Read(ref _begun);

    /// <summary>A request begins: its number in the process (1 is the first) and the others already running.</summary>
    public Ticket Begin()
    {
        var alreadyRunning = Interlocked.Increment(ref _running) - 1;
        return new Ticket(Interlocked.Increment(ref _begun), alreadyRunning);
    }

    /// <summary>The other requests that ran at some moment of the life of <paramref name="ticket"/>'s, up to now.</summary>
    public long Others(Ticket ticket) => ticket.AlreadyRunning + (Interlocked.Read(ref _begun) - ticket.Number);

    /// <summary>The request of <paramref name="ticket"/> ends: the other requests that ran at some moment of its life.</summary>
    public long End(Ticket ticket)
    {
        // The end is written before the count goes down: whoever sees none running sees this end too.
        Interlocked.Exchange(ref _lastEnd, _time.GetTimestamp());
        Interlocked.Decrement(ref _running);
        return Others(ticket);
    }

    /// <summary>
    /// How long the hub has had no request running: since the last one ended, or since this counter was made when none has
    /// ended yet; zero while one runs.
    /// </summary>
    public TimeSpan Quiet() =>
        Running > 0 ? TimeSpan.Zero : _time.GetElapsedTime(Interlocked.Read(ref _lastEnd));

    public readonly record struct Ticket(long Number, long AlreadyRunning);
}
