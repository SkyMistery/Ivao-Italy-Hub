using IvaoHub.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// Makes up the runs the hub lost while no process was alive (note 2026-10-09-i-job-che-recuperano): once the page of whoever
/// woke the hub has been answered, and then every minute, it runs the jobs that are due, one after the other
/// (<see cref="ScheduledJobs.CatchUpAsync"/>). A night job whose hour passed while Passenger kept the hub stopped runs a few
/// seconds after the first visit of the morning, once, and a job whose run failed waits for its next hour, as it always did.
/// </summary>
/// <remarks>
/// <para>The first check waits for the visitor who woke the hub to have their page (issue #399, note
/// 2026-11-14-il-primo-giro-dei-job-dopo-la-pagina): no sooner than <see cref="FirstCheck"/> after the start, and then only
/// once no request has run for <see cref="Quiet"/> (<see cref="RequestsInFlight"/>), or at the latest <see cref="Latest"/>
/// after the start, so that a hub that is never quiet still makes up its runs. A fixed five seconds fell in the middle of the
/// first page on the test installation: its cold request reaches the hub about two seconds after the start, and the page
/// sends its next requests right after. Then the work goes on while the process lives. A process the host stops half way
/// through a run leaves that run unfinished, and the next start makes it up; work that has to be done inside a request is the
/// scheduled task's (<see cref="JobRunEndpoints"/>).</para>
/// <para>On by default only in <c>Production</c> (<see cref="JobOptions.CatchUp"/>): elsewhere every job keeps its hours
/// alone, so the integration tests and the end to end bench see no run they did not ask for.</para>
/// </remarks>
public sealed class JobCatchUp(
    ScheduledJobs jobs,
    RequestsInFlight requests,
    IOptions<JobOptions> options,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<JobCatchUp> logger) : BackgroundService
{
    /// <summary>After the start, the soonest the first check comes: the page of whoever woke the hub comes first.</summary>
    public static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the hub has had no request running before the first check: longer than the pause of a browser between the
    /// answer of a page and the calls its script sends, shorter than the silence after which the host stops an idle hub.
    /// </summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    /// <summary>After the start, the latest the first check comes, quiet or not: most processes of the host live less than a minute.</summary>
    public static readonly TimeSpan Latest = TimeSpan.FromSeconds(30);

    /// <summary>Between two checks: the shortest schedule of the hub, the queue of the mail, is one minute.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>Whether the hub makes up its lost runs: as the installation says, or else only in <c>Production</c>.</summary>
    public static bool IsOn(JobOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        return options.CatchUp ?? environment.IsProduction();
    }

    /// <summary>
    /// How long to wait before looking again whether the first check may come; zero when it may.
    /// <paramref name="sinceStart"/> is the time since the hub started, <paramref name="quiet"/> the time since a request
    /// last ran (<see cref="RequestsInFlight.Quiet"/>, zero while one runs).
    /// </summary>
    public static TimeSpan UntilFirstCheck(TimeSpan sinceStart, TimeSpan quiet)
    {
        if (sinceStart >= Latest)
        {
            return TimeSpan.Zero;
        }

        if (sinceStart < FirstCheck)
        {
            return FirstCheck - sinceStart;
        }

        // A request running now has quiet zero: the next look comes a whole quiet later, or at the latest if that is sooner.
        var wait = quiet >= Quiet ? TimeSpan.Zero : Quiet - quiet;
        return wait < Latest - sinceStart ? wait : Latest - sinceStart;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsOn(options.Value, environment))
        {
            return;
        }

        try
        {
            await StartedAsync(stoppingToken);
            await FirstCheckAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                await CheckAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The hub is stopping.
        }
    }

    /// <summary>Until the first check may come (<see cref="UntilFirstCheck"/>), looking again after every wait.</summary>
    private async Task FirstCheckAsync(CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        while (true)
        {
            var wait = UntilFirstCheck(TimeProvider.System.GetElapsedTime(started), requests.Quiet());
            if (wait <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(wait, cancellationToken);
        }
    }

    /// <summary>One check: the jobs that are due, one after the other, to the end of the pass; the pass says what it ran.</summary>
    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await jobs.CatchUpAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The check of the jobs that are due failed; the next one is in a minute.");
        }
    }

    private async Task StartedAsync(CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(cancellationToken);
    }
}
