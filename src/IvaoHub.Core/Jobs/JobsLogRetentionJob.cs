using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// Once a day, the rows of <c>hub_jobs_log</c> older than <see cref="Kept"/> go (issue #325, item 5). The notifications
/// write a row every minute, «skipped» included, and the modules' jobs one every quarter of an hour: on shared hosting the
/// table grew by one or two thousand rows a day, and nothing ever read the old ones.
/// <para>The last <b>finished</b> run of every job stays, however old: it is what says when the job is due next (note
/// 2026-10-09-i-job-che-recuperano), and a job that runs once a month or once a year would otherwise look as if it had
/// never run, and run again at the next start.</para>
/// <para>Deleted through <c>SaveChanges</c>, a batch at a time, like every write of the hub. It never throws: a failure
/// is a row in this same table.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class JobsLogRetentionJob(
    HubDbContext database,
    IClock clock,
    ILogger<JobsLogRetentionJob> logger) : IJob
{
    /// <summary>Name under which the runs are recorded.</summary>
    public const string JobName = "jobs-log-retention";

    /// <summary>A month: enough to read back what happened the week before a report, little enough to stay small.</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromDays(30);

    /// <summary>Rows deleted per save, so that the first run on an old installation is not one transaction of months.</summary>
    private const int BatchSize = 1000;

    /// <summary>Longest failure message kept on the log row; the log file has the rest.</summary>
    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many rows were deleted on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        database.JobsLog.Add(entry);
        await database.SaveChangesAsync(cancellationToken);

        try
        {
            var deleted = await DeleteAsync(entry.StartedAt - Kept, entry.Id, cancellationToken);

            database.ChangeTracker.Clear();
            database.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{deleted} row(s) older than {Kept.TotalDays:0} days deleted");
            await database.SaveChangesAsync(cancellationToken);

            return deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The retention of the jobs log failed.");

            database.ChangeTracker.Clear();
            database.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength
                ? exception.Message
                : exception.Message[..MaxMessageLength];
            await database.SaveChangesAsync(CancellationToken.None);

            return 0;
        }
    }

    private async Task<int> DeleteAsync(DateTime before, long running, CancellationToken cancellationToken)
    {
        // The row the guard reads for each job: its latest that ended, whatever the outcome.
        var lastRuns = await database.JobsLog.AsNoTracking()
            .Where(run => run.FinishedAt != null)
            .GroupBy(run => run.Job)
            .Select(runs => runs.Max(run => run.Id))
            .ToListAsync(cancellationToken);
        lastRuns.Add(running);

        var deleted = 0;
        while (true)
        {
            var batch = await database.JobsLog.AsNoTracking()
                .Where(run => run.StartedAt < before && !lastRuns.Contains(run.Id))
                .OrderBy(run => run.Id)
                .Select(run => run.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return deleted;
            }

            // The key is all a delete needs: the messages are not read only to be thrown away.
            database.JobsLog.RemoveRange(batch.Select(id => new JobLogEntry { Id = id }));
            await database.SaveChangesAsync(cancellationToken);
            database.ChangeTracker.Clear();
            deleted += batch.Count;
        }
    }
}
