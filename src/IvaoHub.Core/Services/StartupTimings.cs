using System.Diagnostics;

namespace IvaoHub.Core.Services;

/// <summary>
/// How long each step of a start took, counted from the moment the operating system created the process: the time a
/// visitor waits when Passenger starts the hub for their request (note 2026-09-28-l-avvio-a-freddo). The steps end up in
/// <c>diagnostics/starts.txt</c> (<see cref="StartsLog"/>), so that the server says where its seconds go.
/// </summary>
public sealed class StartupTimings
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly TimeSpan _beforeMain;
    private readonly List<StartupStep> _steps = [];
    private readonly Lock _gate = new();
    private TimeSpan _last;

    /// <param name="beforeMain">What passed between the creation of the process and this object: the runtime loading.</param>
    public StartupTimings(TimeSpan beforeMain)
    {
        _beforeMain = beforeMain < TimeSpan.Zero ? TimeSpan.Zero : beforeMain;
        _last = _beforeMain;
        _steps.Add(new StartupStep("runtime", _beforeMain));
    }

    /// <summary>Starts counting, with the time the runtime took before the program ran as the first step.</summary>
    public static StartupTimings Begin() => new(SinceProcessStart());

    /// <summary>Since the process was created.</summary>
    public TimeSpan Elapsed => _beforeMain + _watch.Elapsed;

    /// <summary>Closes a step: it lasted from the end of the previous one until now.</summary>
    public void Step(string name)
    {
        lock (_gate)
        {
            var now = Elapsed;
            _steps.Add(new StartupStep(name, now - _last));
            _last = now;
        }
    }

    public IReadOnlyList<StartupStep> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    private static TimeSpan SinceProcessStart()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return DateTime.Now - process.StartTime;
        }
#pragma warning disable CA1031 // A platform that does not say when the process started only loses the first step.
        catch (Exception)
#pragma warning restore CA1031
        {
            return TimeSpan.Zero;
        }
    }
}

/// <summary>One step of a start and how long it took.</summary>
public sealed record StartupStep(string Name, TimeSpan Duration);
