using System.Collections.Concurrent;
using System.Diagnostics;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace IvaoHub.Core.Jobs;

/// <summary>What became of a run the hub launched.</summary>
public enum JobRunState
{
    /// <summary>It ran: its own row in <c>hub_jobs_log</c> says how it went.</summary>
    Ran,

    /// <summary>It did not run: another process was running the job, or a run since its occurrence had already done it.</summary>
    Skipped,

    /// <summary>It had not finished when the answer had to go.</summary>
    Running,
}

/// <summary>One job the hub launched because it was due, and what became of it.</summary>
/// <param name="Job">The job's name, the one its rows in <c>hub_jobs_log</c> carry.</param>
/// <param name="Outcome">What became of the run.</param>
public sealed record JobRunOutcome(string Job, JobRunState Outcome);

/// <summary>
/// The scheduled jobs of the hub, run once for each occurrence of their schedule whichever process is alive when it comes
/// (note 2026-10-09-i-job-che-recuperano, which gives a shape to way B of 2026-09-28-i-job-quando-passenger-spegne-l-hub).
/// Passenger stops an idle hub and starts it again on the next request, sometimes two processes at once, and every process
/// has its own Quartz: a job used to run only if a process was alive at the second of its cron, and twice if two were.
/// </summary>
/// <remarks>
/// <para>Two halves, and no job changes for either. <b>The guard</b> sits in front of every run Quartz starts — its own at
/// the cron's second, the ones <see cref="RunDueAsync"/> launches, in every module — as a trigger listener: it takes the
/// job's named lock in the database (<see cref="JobLocks"/>, <c>hub-job:&lt;database&gt;:&lt;job&gt;</c>) without
/// waiting, and leaves the run to whoever holds it; with the lock in hand it reads the job's last run again, and skips a run
/// whose occurrence a run has already covered (<see cref="JobSchedule"/>). The lock goes back when the run ends, or with
/// the process. <b>The launcher</b>, <see cref="RunDueAsync"/>, finds every job that is due and starts it through
/// <see cref="IScheduler.TriggerJob(JobKey, JobDataMap, CancellationToken)"/>: <see cref="JobCatchUp"/> a few seconds
/// after a start and every minute, and the scheduled task's address (<see cref="JobRunEndpoints"/>), waiting for them.</para>
/// <para>The last run of a job is the latest start of a row of its own in <c>hub_jobs_log</c> that has ended, whatever its
/// outcome, or the latest start this process let through, whichever is later: a job that writes no row when it finds
/// nothing to do (the tours' checks) is not launched again every minute by the process that just ran it.</para>
/// <para>A job paused in the scheduler is left alone: nothing here launches it.</para>
/// <para>The guard never stops a job by failing: a lock that cannot be asked for, or a last run that cannot be read, lets
/// the run go ahead as it went before the guard existed, and says so in the log.</para>
/// </remarks>
public sealed class ScheduledJobs(
    ISchedulerFactory schedulers,
    IServiceScopeFactory scopes,
    JobLocks locks,
    IClock clock,
    ILogger<ScheduledJobs> logger) : ITriggerListener
{
    /// <summary>How often a wait looks again at the runs that were under way when it began.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    /// <summary>Where the guard leaves the lock of a run it let through, for the end of that run.</summary>
    private const string HeldKey = "hub.jobs.lock";

    /// <summary>Where a launch that waits leaves its name, in the data of its trigger.</summary>
    private const string WaiterKey = "hub.jobs.waiter";

    /// <summary>The start of the last run of each job this process let through, as the guard counted it.</summary>
    private readonly ConcurrentDictionary<string, DateTime> _started = new(StringComparer.Ordinal);

    /// <summary>The launches somebody waits for, by the name in their trigger's data.</summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JobRunState>> _waiting = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string Name => "hub-scheduled-jobs";

    /// <summary>
    /// Launches every job that is due now, and with a <paramref name="wait"/> waits for them, and for the runs that were
    /// already under way, until they end or the wait is over. Without a wait, a job already running or already launched in
    /// this process is left to that run.
    /// </summary>
    /// <returns>The jobs launched, and what became of each by the end of the wait.</returns>
    public async Task<IReadOnlyList<JobRunOutcome>> RunDueAsync(TimeSpan wait, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulers.GetScheduler(cancellationToken);
        if (scheduler.IsShutdown)
        {
            return [];
        }

        var waits = wait > TimeSpan.Zero;
        var underWay = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
        var launched = new List<Launch>();

        try
        {
            await using (var scope = scopes.CreateAsyncScope())
            {
                var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                var now = clock.UtcNow;

                var keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
                foreach (var key in keys.OrderBy(key => key.ToString(), StringComparer.Ordinal))
                {
                    var triggers = await scheduler.GetTriggersOfJob(key, cancellationToken);

                    // Only the schedules that are running: a job whose every trigger is paused is not the hub's to run by itself.
                    var schedules = new List<JobCron>();
                    foreach (var cron in triggers.OfType<ICronTrigger>())
                    {
                        if (await scheduler.GetTriggerState(cron.Key, cancellationToken) != TriggerState.Paused)
                        {
                            schedules.Add(JobCron.Of(cron));
                        }
                    }

                    if (schedules.Count == 0)
                    {
                        continue;
                    }

                    // A run under way, or launched and not started yet, is the run of this occurrence for whoever does not wait.
                    if (!waits && (underWay.Any(run => run.JobDetail.Key.Equals(key)) || triggers.Any(trigger => trigger is not ICronTrigger)))
                    {
                        continue;
                    }

                    // What this process last let through answers most minutes without asking the database.
                    if (_started.TryGetValue(key.Name, out var mine) && !JobSchedule.IsDue(schedules, mine, now))
                    {
                        continue;
                    }

                    if (JobSchedule.IsDue(schedules, await LastStartAsync(hub, key.Name, cancellationToken), now))
                    {
                        launched.Add(await LaunchAsync(scheduler, key, waits, cancellationToken));
                    }
                }
            }

            if (waits)
            {
                await WaitAsync(scheduler, launched, underWay, wait, cancellationToken);
            }

            return [.. launched.Select(launch => new JobRunOutcome(
                launch.Job,
                launch.Outcome is { IsCompletedSuccessfully: true } outcome ? outcome.Result : JobRunState.Running))];
        }
        finally
        {
            // A launch nobody waits for any more is forgotten: the guard finds no name, and completes nothing.
            foreach (var launch in launched.Where(launch => launch.Waiter is not null))
            {
                _waiting.TryRemove(launch.Waiter!, out _);
            }
        }
    }

    // ---- the guard ---------------------------------------------------------------------------------------------------

    /// <inheritdoc />
    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Before every run: the job's lock, without waiting, and the job's last run read again with the lock in hand. A run
    /// another process is doing, or one a run since its occurrence has already done, is skipped.
    /// </summary>
    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var job = context.JobDetail.Key.Name;
        var held = false;

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            var (answer, failure) = await locks.TakeAsync(ConnectionString(hub), job, cancellationToken);
            if (answer == JobLockAnswer.Busy)
            {
                logger.LogInformation("The job {Job} is running in another process: this run is left to it.", job);
                return Skip(context);
            }

            held = answer == JobLockAnswer.Held;
            if (!held)
            {
                logger.LogWarning(failure, "The lock of the job {Job} could not be asked for: this run goes ahead without it.", job);
            }

            var now = clock.UtcNow;
            var schedules = (await context.Scheduler.GetTriggersOfJob(context.JobDetail.Key, cancellationToken))
                .OfType<ICronTrigger>()
                .Select(JobCron.Of)
                .ToList();

            if (schedules.Count > 0 && !JobSchedule.IsDue(schedules, await LastStartAsync(hub, job, cancellationToken), now))
            {
                if (held)
                {
                    await locks.ReleaseAsync(job);
                }

                logger.LogInformation("The job {Job} has already run for its last occurrence: this run is skipped.", job);
                return Skip(context);
            }

            Let(context, job, held, now);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The scheduler is stopping: the run would be stopped as soon as it began.
            if (held)
            {
                await locks.ReleaseAsync(job);
            }

            return Skip(context);
        }
        catch (Exception exception)
        {
            // A guard that cannot decide never stops a job: the run goes ahead as it did before the guard, with the lock if
            // the guard got that far.
            logger.LogWarning(exception, "The guard of the job {Job} could not decide: this run goes ahead.", job);
            Let(context, job, held, clock.UtcNow);
            return false;
        }
    }

    /// <inheritdoc />
    public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>After a run the guard let through: its lock goes back, and whoever waits for it hears that it ran.</summary>
    public async Task TriggerComplete(
        ITrigger trigger,
        IJobExecutionContext context,
        SchedulerInstruction triggerInstructionCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Get(HeldKey) is string job)
        {
            var (stillHeld, failure) = await locks.ReleaseAsync(job);
            if (!stillHeld)
            {
                logger.LogWarning(
                    failure,
                    "The lock of the job {Job} was lost while it ran: its connection fell, and another process may have run it at the same time.",
                    job);
            }
        }

        Complete(context, JobRunState.Ran);
    }

    // ---- the pieces --------------------------------------------------------------------------------------------------

    /// <summary>A launch through the scheduler, with what its waiter will say, when somebody waits for it.</summary>
    private sealed record Launch(string Job, string? Waiter, Task<JobRunState>? Outcome);

    private async Task<Launch> LaunchAsync(IScheduler scheduler, JobKey key, bool waits, CancellationToken cancellationToken)
    {
        var data = new JobDataMap();
        string? name = null;
        TaskCompletionSource<JobRunState>? waiter = null;

        if (waits)
        {
            name = Guid.NewGuid().ToString("N");
            waiter = new TaskCompletionSource<JobRunState>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiting[name] = waiter;
            data[WaiterKey] = name;
        }

        try
        {
            await scheduler.TriggerJob(key, data, cancellationToken);
        }
        catch
        {
            if (name is not null)
            {
                _waiting.TryRemove(name, out _);
            }

            throw;
        }

        return new Launch(key.Name, name, waiter?.Task);
    }

    /// <summary>
    /// Until every launch has an answer and every run that was under way when the wait began has ended, or the wait is over:
    /// a process that answers while a run goes on may be stopped by its host as soon as the answer is out.
    /// </summary>
    private static async Task WaitAsync(
        IScheduler scheduler,
        IReadOnlyList<Launch> launched,
        IReadOnlyCollection<IJobExecutionContext> underWay,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var answered = Task.WhenAll(launched.Select(launch => launch.Outcome!));
        var before = underWay.Select(run => run.FireInstanceId).ToHashSet(StringComparer.Ordinal);

        while (true)
        {
            if (before.Count > 0)
            {
                var now = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
                before.IntersectWith(now.Select(run => run.FireInstanceId));
            }

            var left = wait - watch.Elapsed;
            if ((answered.IsCompleted && before.Count == 0) || left <= TimeSpan.Zero)
            {
                return;
            }

            var pause = Task.Delay(left < Poll ? left : Poll, cancellationToken);
            await (answered.IsCompleted ? pause : Task.WhenAny(answered, pause));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// The start of the last run of a job: the latest of its rows that ended, whatever the outcome, or the latest this
    /// process let through. Null when it never ran.
    /// </summary>
    private async Task<DateTime?> LastStartAsync(HubDbContext hub, string job, CancellationToken cancellationToken)
    {
        // The index on (job, started_at) read from the end: the first row that has ended is the answer, however long the log.
        var logged = await hub.JobsLog.AsNoTracking()
            .Where(run => run.Job == job && run.FinishedAt != null)
            .OrderByDescending(run => run.StartedAt)
            .Select(run => (DateTime?)run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return _started.TryGetValue(job, out var mine) ? Later(mine, logged) : logged;
    }

    /// <summary>A run let through: its lock waits for its end, and its start counts in this process from now.</summary>
    private void Let(IJobExecutionContext context, string job, bool held, DateTime start)
    {
        if (held)
        {
            context.Put(HeldKey, job);
        }

        _started.AddOrUpdate(job, start, (_, before) => Later(before, start));
    }

    private bool Skip(IJobExecutionContext context)
    {
        Complete(context, JobRunState.Skipped);
        return true;
    }

    private void Complete(IJobExecutionContext context, JobRunState state)
    {
        if (context.Trigger.JobDataMap.TryGetString(WaiterKey, out var waiter)
            && waiter is not null
            && _waiting.TryRemove(waiter, out var waiting))
        {
            waiting.TrySetResult(state);
        }
    }

    /// <summary>The database the log is in, for the lock's own connection; null when the context cannot say.</summary>
    private static string? ConnectionString(HubDbContext hub)
    {
        try
        {
            return hub.Database.GetConnectionString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static DateTime Later(DateTime one, DateTime? other) => other is { } value && value > one ? value : one;
}
