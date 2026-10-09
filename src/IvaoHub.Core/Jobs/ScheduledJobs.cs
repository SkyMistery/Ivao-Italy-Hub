using System.Collections.Concurrent;
using System.Diagnostics;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace IvaoHub.Core.Jobs;

/// <summary>What became of a job the hub found due.</summary>
public enum JobRunState
{
    /// <summary>It ran: its own row in <c>hub_jobs_log</c> says how it went.</summary>
    Ran,

    /// <summary>It did not run: another process was running the job, or a run since its occurrence had already done it.</summary>
    Skipped,

    /// <summary>It had started, and not finished when the answer had to go.</summary>
    Running,

    /// <summary>It had not started yet when the answer had to go: the jobs that are due start one after the other.</summary>
    Waiting,
}

/// <summary>One job the hub found due, and what became of it.</summary>
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
/// the cron's second, the ones a pass launches, in every module — as a trigger listener: it takes the job's named lock in
/// the database (<see cref="JobLocks"/>, <c>hub-job:&lt;database&gt;:&lt;job&gt;</c>) without waiting, and leaves the run
/// to whoever holds it; with the lock in hand it reads the job's last run again, and skips a run whose occurrence a run has
/// already covered (<see cref="JobSchedule"/>). The lock goes back when the run ends, or with the process. <b>The
/// pass</b> finds every job that is due and starts them <b>one after the other</b> through
/// <see cref="IScheduler.TriggerJob(JobKey, JobDataMap, CancellationToken)"/>, each when the one before has ended (Carmine,
/// on #239): after a night asleep a dozen jobs are due, and they never start together against the pool of a visitor's
/// page. One pass at a time in a process: <see cref="JobCatchUp"/>, a few seconds after a start and every minute, and the
/// scheduled task's address (<see cref="JobRunEndpoints"/>) join the pass under way rather than start another.</para>
/// <para>The last run of a job is the latest start of a row of its own in <c>hub_jobs_log</c> that has ended, whatever its
/// outcome (Carmine, on #239), or the latest start this process knows of — a run it let through, or what it last read in
/// the log —, whichever is later: a job that writes no row when it finds nothing to do (the tours' checks) is not launched
/// again every minute by the process that just ran it, and most minutes ask the database nothing.</para>
/// <para>A job paused in the scheduler is left alone: no pass launches it.</para>
/// <para>The guard never stops a job by failing: a lock that cannot be asked for, or a last run that cannot be read, lets
/// the run go ahead as it went before the guard existed, and says so in the log (Carmine, on #239).</para>
/// </remarks>
public sealed class ScheduledJobs(
    ISchedulerFactory schedulers,
    IServiceScopeFactory scopes,
    JobLocks locks,
    IClock clock,
    IHostApplicationLifetime lifetime,
    ILogger<ScheduledJobs> logger) : ITriggerListener
{
    /// <summary>
    /// How long a pass waits for one job before it goes on to the next: many times the longest run the hub has, so that only
    /// a run that hangs lets two jobs of a pass run together.
    /// </summary>
    public static readonly TimeSpan LongestRun = TimeSpan.FromMinutes(10);

    /// <summary>How often a wait looks again at the runs that were under way when it began.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    /// <summary>Where the guard leaves the lock of a run it let through, for the end of that run.</summary>
    private const string HeldKey = "hub.jobs.lock";

    /// <summary>Where a pass leaves the name of a launch it waits for, in the data of its trigger.</summary>
    private const string WaiterKey = "hub.jobs.waiter";

    /// <summary>The latest start of each job this process knows of: the runs it let through, and what it read in the log.</summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastStarts = new(StringComparer.Ordinal);

    /// <summary>The launches a pass waits for, by the name in their trigger's data.</summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JobRunState>> _waiting = new(StringComparer.Ordinal);

    private readonly Lock _passGate = new();
    private Pass? _pass;

    /// <inheritdoc />
    public string Name => "hub-scheduled-jobs";

    /// <summary>The check of <see cref="JobCatchUp"/>: the pass under way in this process, or a new one, to its end.</summary>
    public async Task CatchUpAsync(CancellationToken cancellationToken = default) =>
        await Join().Done.WaitAsync(cancellationToken);

    /// <summary>
    /// The scheduled task's call: the pass under way in this process, or a new one, waited for until it ends — with the
    /// runs that were already under way when the call came — or the <paramref name="wait"/> is over. The pass goes on after
    /// the answer while the process lives.
    /// </summary>
    /// <returns>The jobs of the pass, and what became of each by the end of the wait.</returns>
    public async Task<IReadOnlyList<JobRunOutcome>> RunDueAsync(TimeSpan wait, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulers.GetScheduler(cancellationToken);
        if (scheduler.IsShutdown)
        {
            return [];
        }

        var underWay = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
        var pass = Join();

        await WaitAsync(scheduler, pass.Done, underWay, wait, cancellationToken);
        return pass.Outcomes();
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

    /// <summary>After a run the guard let through: its lock goes back, and a pass waiting for it hears that it ran.</summary>
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

    // ---- the pass ----------------------------------------------------------------------------------------------------

    /// <summary>One pass: the jobs found due when it began, started one after the other, and what became of each.</summary>
    private sealed class Pass
    {
        private readonly List<string> _jobs = [];
        private readonly ConcurrentDictionary<string, JobRunState> _states = new(StringComparer.Ordinal);

        public Task Done { get; set; } = Task.CompletedTask;

        public void Plan(IEnumerable<string> jobs)
        {
            lock (_jobs)
            {
                foreach (var job in jobs)
                {
                    _jobs.Add(job);
                    _states[job] = JobRunState.Waiting;
                }
            }
        }

        public void Set(string job, JobRunState state) => _states[job] = state;

        public IReadOnlyList<JobRunOutcome> Outcomes()
        {
            lock (_jobs)
            {
                return [.. _jobs.Select(job => new JobRunOutcome(job, _states[job]))];
            }
        }
    }

    /// <summary>The pass under way in this process, or a new one, which runs on its own until its end or the host's stop.</summary>
    private Pass Join()
    {
        lock (_passGate)
        {
            if (_pass is { Done.IsCompleted: false } running)
            {
                return running;
            }

            var pass = new Pass();
            pass.Done = Task.Run(() => RunPassAsync(pass, lifetime.ApplicationStopping));
            _pass = pass;
            return pass;
        }
    }

    private async Task RunPassAsync(Pass pass, CancellationToken stopping)
    {
        try
        {
            var scheduler = await schedulers.GetScheduler(stopping);
            if (scheduler.IsShutdown)
            {
                return;
            }

            var due = await DueAsync(scheduler, stopping);
            pass.Plan(due.Select(key => key.Name));

            foreach (var key in due)
            {
                pass.Set(key.Name, JobRunState.Running);
                pass.Set(key.Name, await RunOneAsync(scheduler, key, stopping));
            }

            if (due.Count > 0)
            {
                logger.LogInformation(
                    "Ran {Count} job(s) that were due, one after the other: {Jobs}.",
                    due.Count,
                    string.Join(", ", pass.Outcomes().Select(outcome => $"{outcome.Job} {outcome.Outcome}")));
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The host is stopping.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "A pass over the jobs that were due stopped half way; the next check starts another.");
        }
    }

    /// <summary>
    /// The jobs whose schedule has an occurrence since their last run, in the order of their names, the paused ones left out.
    /// </summary>
    private async Task<List<JobKey>> DueAsync(IScheduler scheduler, CancellationToken cancellationToken)
    {
        List<JobKey> due = [];

        await using var scope = scopes.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var now = clock.UtcNow;

        var keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
        foreach (var key in keys.OrderBy(key => key.ToString(), StringComparer.Ordinal))
        {
            // Only the schedules that are running: a job whose every trigger is paused is not the hub's to run by itself.
            var schedules = new List<JobCron>();
            foreach (var cron in (await scheduler.GetTriggersOfJob(key, cancellationToken)).OfType<ICronTrigger>())
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

            // What this process last knew answers most minutes without asking the database.
            if (_lastStarts.TryGetValue(key.Name, out var known) && !JobSchedule.IsDue(schedules, known, now))
            {
                continue;
            }

            if (JobSchedule.IsDue(schedules, await LastStartAsync(hub, key.Name, cancellationToken), now))
            {
                due.Add(key);
            }
        }

        return due;
    }

    /// <summary>
    /// Launches one job and waits for what the guard says of it: ran, or skipped. A run that outlasts
    /// <see cref="LongestRun"/> is left running, and the pass goes on.
    /// </summary>
    private async Task<JobRunState> RunOneAsync(IScheduler scheduler, JobKey key, CancellationToken stopping)
    {
        var name = Guid.NewGuid().ToString("N");
        var waiter = new TaskCompletionSource<JobRunState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiting[name] = waiter;

        try
        {
            await scheduler.TriggerJob(key, new JobDataMap { [WaiterKey] = name }, stopping);
            return await waiter.Task.WaitAsync(LongestRun, stopping);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "The job {Job} has run for more than {Minutes} minutes: the pass goes on to the next job.",
                key.Name,
                LongestRun.TotalMinutes);
            return JobRunState.Running;
        }
        finally
        {
            // A launch nobody waits for any more is forgotten: the guard finds no name, and completes nothing.
            _waiting.TryRemove(name, out _);
        }
    }

    /// <summary>
    /// Until the pass has ended and every run that was under way when the wait began has ended, or the wait is over: a
    /// process that answers while a run goes on may be stopped by its host as soon as the answer is out.
    /// </summary>
    private static async Task WaitAsync(
        IScheduler scheduler,
        Task pass,
        IReadOnlyCollection<IJobExecutionContext> underWay,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var before = underWay.Select(run => run.FireInstanceId).ToHashSet(StringComparer.Ordinal);

        while (true)
        {
            if (before.Count > 0)
            {
                var now = await scheduler.GetCurrentlyExecutingJobs(cancellationToken);
                before.IntersectWith(now.Select(run => run.FireInstanceId));
            }

            var left = wait - watch.Elapsed;
            if ((pass.IsCompleted && before.Count == 0) || left <= TimeSpan.Zero)
            {
                return;
            }

            var pause = Task.Delay(left < Poll ? left : Poll, cancellationToken);
            await (pass.IsCompleted ? pause : Task.WhenAny(pass, pause));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    // ---- the pieces --------------------------------------------------------------------------------------------------

    /// <summary>
    /// The start of the last run of a job: the latest of its rows that ended, whatever the outcome, or the latest this
    /// process knows of; remembered, so that the next minutes need not ask. Null when it never ran.
    /// </summary>
    private async Task<DateTime?> LastStartAsync(HubDbContext hub, string job, CancellationToken cancellationToken)
    {
        // The index on (job, started_at) read from the end: the first row that has ended is the answer, however long the log.
        var logged = await hub.JobsLog.AsNoTracking()
            .Where(run => run.Job == job && run.FinishedAt != null)
            .OrderByDescending(run => run.StartedAt)
            .Select(run => (DateTime?)run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (logged is { } read)
        {
            Remember(job, read);
        }

        return _lastStarts.TryGetValue(job, out var known) ? Later(known, logged) : logged;
    }

    /// <summary>A run let through: its lock waits for its end, and its start counts in this process from now.</summary>
    private void Let(IJobExecutionContext context, string job, bool held, DateTime start)
    {
        if (held)
        {
            context.Put(HeldKey, job);
        }

        Remember(job, start);
    }

    /// <summary>A start of a job this process now knows of; only a later one replaces what it knew.</summary>
    private void Remember(string job, DateTime start) => _lastStarts.AddOrUpdate(job, start, (_, before) => Later(before, start));

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
