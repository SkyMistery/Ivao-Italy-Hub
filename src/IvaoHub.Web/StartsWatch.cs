using System.Reflection;
using System.Runtime.InteropServices;
using IvaoHub.Core.Services;

namespace IvaoHub.Web;

/// <summary>
/// Writes the lines of <c>diagnostics/starts.txt</c> for this process (<see cref="StartsLog"/>): the start when the host
/// is ready, the signal when the system asks it to stop, the stop when it stops in order; and counts the requests in
/// between, timing when the first answer left.
/// </summary>
internal sealed class StartsWatch
{
    private readonly HubPaths _paths;
    private readonly StartupTimings _timings;
    private readonly BuildInfo _build;
    // Kept for the life of the process: a registration that is collected stops listening.
    private readonly List<PosixSignalRegistration> _signals = [];
    private volatile bool _started;
    private long _requests;
    private long _firstAnswerTicks = -1;
    private int _stopped;

    private StartsWatch(HubPaths paths, StartupTimings timings, BuildInfo build)
    {
        _paths = paths;
        _timings = timings;
        _build = build;
    }

    /// <summary>
    /// Armed only when this process is the hub itself, like <see cref="StartupFailureWatch"/>: under the test host a
    /// process starts and stops dozens of hosts, and its lines would read like a row of crashes; under the tool that
    /// writes the OpenAPI document nothing starts at all.
    /// </summary>
    public static StartsWatch? Arm(WebApplication app, HubPaths paths, StartupTimings timings)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (Assembly.GetEntryAssembly() != typeof(StartsWatch).Assembly)
        {
            return null;
        }

        var watch = new StartsWatch(paths, timings, app.Services.GetRequiredService<BuildInfo>());
        app.Lifetime.ApplicationStarted.Register(watch.Started);
        app.Lifetime.ApplicationStopping.Register(watch.Stopping);
        watch.ListenForSignals();

        // First in the pipeline, so that every request is counted, the ones a later middleware answers included.
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            finally
            {
                if (Interlocked.Increment(ref watch._requests) == 1)
                {
                    // After the response: the route table is built inside the first request, before any middleware.
                    Interlocked.Exchange(ref watch._firstAnswerTicks, timings.Elapsed.Ticks);
                }
            }
        });

        return watch;
    }

    private void Started()
    {
        _timings.Step("listening");
        _started = true;

        var pid = Environment.ProcessId;
        var took = _timings.Elapsed;
        var memory = ProcessMemory.Now();
        StartsLog.AppendStart(_paths, lines =>
        {
            var now = DateTime.UtcNow;
            return StartsLog.StartLine(
                now,
                pid,
                _build,
                took,
                StartsLog.Previous(lines, now, pid, StartsLog.IsHubRunning),
                memory,
                _timings.Steps,
                _timings.Initialisation);
        });
    }

    /// <summary>Once, however many times it is called: a line for the stop, never two.</summary>
    private void Stopping()
    {
        // A process that never became ready wrote no start either, and startup-error.txt says why.
        if (!_started || Interlocked.Exchange(ref _stopped, 1) == 1)
        {
            return;
        }

        var first = Interlocked.Read(ref _firstAnswerTicks);
        StartsLog.Append(_paths, StartsLog.StopLine(
            DateTime.UtcNow,
            Environment.ProcessId,
            _timings.Elapsed,
            Interlocked.Read(ref _requests),
            first < 0 ? null : TimeSpan.FromTicks(first),
            ProcessMemory.Now()));
    }

    /// <summary>
    /// Looks at the signal and lets it through: the host's own handler still stops the application. The line is written
    /// when the signal arrives, whichever of the two handlers runs first (vIPI, 25 September 2026: a word in the stop
    /// line depended on that order and was wrong nearly every time).
    /// </summary>
    private void ListenForSignals()
    {
        Listen(PosixSignal.SIGTERM, "SIGTERM");
        Listen(PosixSignal.SIGINT, "SIGINT");
        if (!OperatingSystem.IsWindows())
        {
            Listen(PosixSignal.SIGQUIT, "SIGQUIT");
        }
    }

    private void Listen(PosixSignal signal, string name)
    {
        try
        {
            _signals.Add(PosixSignalRegistration.Create(
                signal,
                _ => StartsLog.Append(_paths, StartsLog.SignalLine(DateTime.UtcNow, Environment.ProcessId, name))));
        }
#pragma warning disable CA1031 // A platform that does not know the signal only loses its line.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }
}
