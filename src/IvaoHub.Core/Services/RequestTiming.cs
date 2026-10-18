namespace IvaoHub.Core.Services;

/// <summary>
/// What one request waited on, besides its own code: the database and the outbound HTTP calls (note
/// 2026-10-18-i-tempi-delle-richieste). The request's middleware puts one in <see cref="Current"/>; the database interceptor
/// and the outbound handler add to it from wherever the request's work runs, because the value flows with the async calls.
/// </summary>
/// <remarks>
/// Work that belongs to no request — the start, a job — adds to <see cref="Outside"/>, which the line of the start in
/// <c>starts.txt</c> reads when the hub becomes ready. Counters and not a list: nothing here grows with the request, and
/// nothing here names what was asked, only how long it took.
/// </remarks>
public sealed class RequestTiming
{
    private static readonly AsyncLocal<RequestTiming?> CurrentTiming = new();

    private long _commands;
    private long _commandTicks;
    private long _opens;
    private long _openTicks;
    private long _calls;
    private long _callTicks;

    /// <summary>The timing of the request whose work is running, or null outside a request.</summary>
    public static RequestTiming? Current
    {
        get => CurrentTiming.Value;
        set => CurrentTiming.Value = value;
    }

    /// <summary>Everything that ran outside a request since the process started.</summary>
    public static RequestTiming Outside { get; } = new();

    /// <summary>Where the time being measured now goes.</summary>
    public static RequestTiming Here => Current ?? Outside;

    public long Commands => Interlocked.Read(ref _commands);

    public TimeSpan CommandTime => TimeSpan.FromTicks(Interlocked.Read(ref _commandTicks));

    public long Opens => Interlocked.Read(ref _opens);

    public TimeSpan OpenTime => TimeSpan.FromTicks(Interlocked.Read(ref _openTicks));

    public long Calls => Interlocked.Read(ref _calls);

    public TimeSpan CallTime => TimeSpan.FromTicks(Interlocked.Read(ref _callTicks));

    /// <summary>One command sent to the database.</summary>
    public void Command() => Interlocked.Increment(ref _commands);

    /// <summary>Time spent on commands: the execution, and for a reader the reading of its rows.</summary>
    public void CommandTook(TimeSpan duration) => Interlocked.Add(ref _commandTicks, Math.Max(0, duration.Ticks));

    /// <summary>A connection opened, and how long the opening took: from the pool it is nothing, a new one is the network.</summary>
    public void Opened(TimeSpan duration)
    {
        Interlocked.Increment(ref _opens);
        Interlocked.Add(ref _openTicks, Math.Max(0, duration.Ticks));
    }

    /// <summary>An outbound HTTP call, retries and waits included.</summary>
    public void Called(TimeSpan duration)
    {
        Interlocked.Increment(ref _calls);
        Interlocked.Add(ref _callTicks, Math.Max(0, duration.Ticks));
    }
}
