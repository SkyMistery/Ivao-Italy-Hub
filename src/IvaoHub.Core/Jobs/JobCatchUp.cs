using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// Makes up the runs the hub lost while no process was alive (note 2026-10-09-i-job-che-recuperano): a few seconds after
/// the hub has started, and then every minute, it runs the jobs that are due, one after the other
/// (<see cref="ScheduledJobs.CatchUpAsync"/>). A night job whose hour passed while Passenger kept the hub stopped runs a few
/// seconds after the first visit of the morning, once, and a job whose run failed waits for its next hour, as it always did.
/// </summary>
/// <remarks>
/// <para>The first check waits for the visitor who woke the hub to have their page: then the work goes on while the process
/// lives. A process the host stops half way through a run leaves that run unfinished, and the next start makes it up; work
/// that has to be done inside a request is the scheduled task's (<see cref="JobRunEndpoints"/>).</para>
/// <para>On by default only in <c>Production</c> (<see cref="JobOptions.CatchUp"/>): elsewhere every job keeps its hours
/// alone, so the integration tests and the end to end bench see no run they did not ask for.</para>
/// </remarks>
public sealed class JobCatchUp(
    ScheduledJobs jobs,
    IOptions<JobOptions> options,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<JobCatchUp> logger) : BackgroundService
{
    /// <summary>After the start, before the first check: the page of whoever woke the hub comes first.</summary>
    public static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(5);

    /// <summary>Between two checks: the shortest schedule of the hub, the queue of the mail, is one minute.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>Whether the hub makes up its lost runs: as the installation says, or else only in <c>Production</c>.</summary>
    public static bool IsOn(JobOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        return options.CatchUp ?? environment.IsProduction();
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
            await Task.Delay(FirstCheck, stoppingToken);

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
