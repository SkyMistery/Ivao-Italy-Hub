using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>
/// The engine's job (design M2 §6.1): the checks run right after a send, and this runs them on every report waiting whose
/// checks are older than its place in the queue — a send whose run did not save, a report sent before the engine existed.
/// Every ten minutes, a batch at a time; a report changed meanwhile is tried again the next time. It writes its line in the
/// log of the jobs on every run, also when it found nothing to do (issue #399, note
/// <c>2026-11-14-il-primo-giro-dei-job-dopo-la-pagina</c> §4, decided by Carmine on #515): the guard of the jobs reads its last
/// run there, and without the line every new process of the host found it due and ran it once more. At most six lines an
/// hour, against the sixty of the queue of the mail.
/// <para>It runs as the application, outside any request, so the interceptor's guard leaves it alone as it leaves every job.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class FlightCheckJob(
    FlightOpsDbContext database,
    HubDbContext hub,
    FlightChecks engine,
    IClock clock,
    ILogger<FlightCheckJob> logger) : IJob
{
    public const string JobName = "flightops-flight-checks";

    public const string Cron = "0 0/10 * * * ?";

    /// <summary>At most this many reports a run: a backlog is worked off in turns.</summary>
    public const int Batch = 50;

    /// <summary>The message of a run that found no report waiting for its checks.</summary>
    public const string NothingWaiting = "No report waiting for its checks.";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>
    /// Returns how many reports were checked. A report that fails is written in the log and left for the next run; the others
    /// of the batch are checked all the same (issue #328, item 9): one report that always fails does not stop the job for the
    /// rest of the queue. Only the job's own cancellation stops it: a timeout of a call inside a check (a
    /// <see cref="TaskCanceledException"/> too) costs that report only.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = clock.UtcNow;
        var done = 0;
        var failed = new List<string>();

        List<long> ids;
        try
        {
            ids = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
                .Where(report => (report.Status == PirepStatus.Queued || report.Status == PirepStatus.InReview)
                    && (report.ChecksRanAt == null || report.ChecksRanAt < report.QueuedAt))
                .OrderBy(report => report.QueuedAt)
                .Select(report => report.Id)
                .Take(Batch)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The job of the checks failed.");
            await LogAsync(startedAt, "failed", Shortened(exception.Message), cancellationToken);
            return 0;
        }

        if (ids.Count == 0)
        {
            await LogAsync(startedAt, "succeeded", NothingWaiting, cancellationToken);
            return 0;
        }

        foreach (var id in ids)
        {
            try
            {
                var pirep = await CrudSource.BackOffice<Pirep>(database)
                    .Include(report => report.Flights)
                    .Include(report => report.Errors)
                    .AsSplitQuery()
                    .FirstAsync(report => report.Id == id, cancellationToken);

                if (await engine.RunAsync(pirep, cancellationToken))
                {
                    done++;
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(exception, "The checks of report {PirepId} failed.", id);
                failed.Add(string.Create(CultureInfo.InvariantCulture, $"report {id}: {exception.Message}"));
            }
            finally
            {
                database.ChangeTracker.Clear();
            }
        }

        var summary = string.Create(CultureInfo.InvariantCulture, $"{done} of {ids.Count} report(s) checked");
        if (failed.Count == 0)
        {
            await LogAsync(startedAt, "succeeded", summary, cancellationToken);
        }
        else
        {
            await LogAsync(startedAt, "failed", Shortened(string.Join("; ", [summary, .. failed])), cancellationToken);
        }

        return done;
    }

    private static string Shortened(string message) => message.Length <= MaxMessageLength ? message : message[..MaxMessageLength];

    private async Task LogAsync(DateTime startedAt, string status, string message, CancellationToken cancellationToken)
    {
        hub.ChangeTracker.Clear();
        hub.JobsLog.Add(new JobLogEntry { Job = JobName, StartedAt = startedAt, FinishedAt = clock.UtcNow, Status = status, Message = message });
        await hub.SaveChangesAsync(cancellationToken);
    }
}
