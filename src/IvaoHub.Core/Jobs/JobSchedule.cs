using Quartz;

namespace IvaoHub.Core.Jobs;

/// <summary>One cron schedule of a job, in the time zone its trigger runs in.</summary>
/// <param name="Cron">The Quartz cron expression, as the trigger holds it.</param>
/// <param name="Zone">The time zone the expression is read in: the trigger's own, never the server's guess.</param>
public sealed record JobCron(string Cron, TimeZoneInfo Zone)
{
    /// <summary>The schedule a cron trigger runs on.</summary>
    public static JobCron Of(ICronTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return new JobCron(trigger.CronExpressionString ?? string.Empty, trigger.TimeZone);
    }
}

/// <summary>
/// When a scheduled job is <b>due</b> (note 2026-10-09-i-job-che-recuperano, from 2026-09-28-i-job-quando-passenger-spegne-l-hub
/// §3, way B): when one of its schedules has an occurrence after the start of its last run and up to now. A process that
/// was not alive at the second of its cron has not lost the run: the next check that finds it due makes it up, once,
/// however many occurrences went by.
/// </summary>
/// <remarks>
/// The last run is the one that <b>ended</b>, whatever its outcome, read from <c>hub_jobs_log</c>: a run left
/// <c>running</c> by a process that died never ended, and is made up; a run that failed waits for its next occurrence, as
/// it always did. Which runs count is <see cref="ScheduledJobs"/>' business; this only reads a schedule.
/// </remarks>
public static class JobSchedule
{
    /// <summary>
    /// An occurrence this close after the start of a run is that run's own. A run launched a moment before an occurrence —
    /// yesterday's lost digest made up by the check at 06:59:59, say — does the very work the occurrence asks for, and
    /// without this the occurrence would run the job again a second later. Far below the minute every schedule of the hub
    /// is at least apart, so no occurrence of its own is ever taken from a job.
    /// </summary>
    public static readonly TimeSpan SameOccurrence = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether one of <paramref name="schedules"/> has an occurrence after <paramref name="lastStartUtc"/> (more than
    /// <see cref="SameOccurrence"/> after it) and no later than <paramref name="nowUtc"/>. A job that never ran is due; a
    /// job with no schedule never is.
    /// </summary>
    public static bool IsDue(IReadOnlyCollection<JobCron> schedules, DateTime? lastStartUtc, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(schedules);

        if (schedules.Count == 0)
        {
            return false;
        }

        if (lastStartUtc is not { } last)
        {
            return true;
        }

        var after = new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc)).Add(SameOccurrence);
        var now = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));

        return schedules.Any(schedule =>
            new CronExpression(schedule.Cron) { TimeZone = schedule.Zone }.GetNextValidTimeAfter(after) is { } next
            && next <= now);
    }
}
