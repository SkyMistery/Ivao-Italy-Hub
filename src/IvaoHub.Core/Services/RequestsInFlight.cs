namespace IvaoHub.Core.Services;

/// <summary>
/// The requests running in a process, and for each one the others it overlapped (issue #402, note
/// 2026-11-14-le-richieste-in-corso-nella-diagnostica): the ones already running when it began, and the ones that began while
/// it ran, which are the numbers given after its own. The column <c>others_n</c> of <c>requests-YYYYMMDD.txt</c> and the
/// <c>in flight</c> of a <c>STOP</c> line in <c>starts.txt</c> read it.
/// </summary>
/// <remarks>Two counters and no lock: a request that begins in the same instant as another may miss it, which a count of
/// diagnostics can afford.</remarks>
public sealed class RequestsInFlight
{
    private long _running;
    private long _begun;

    /// <summary>The requests running now.</summary>
    public long Running => Interlocked.Read(ref _running);

    /// <summary>A request begins: its number in the process (1 is the first) and the others already running.</summary>
    public Ticket Begin()
    {
        var alreadyRunning = Interlocked.Increment(ref _running) - 1;
        return new Ticket(Interlocked.Increment(ref _begun), alreadyRunning);
    }

    /// <summary>The request of <paramref name="ticket"/> ends: the other requests that ran at some moment of its life.</summary>
    public long End(Ticket ticket)
    {
        Interlocked.Decrement(ref _running);
        return ticket.AlreadyRunning + (Interlocked.Read(ref _begun) - ticket.Number);
    }

    public readonly record struct Ticket(long Number, long AlreadyRunning);
}
