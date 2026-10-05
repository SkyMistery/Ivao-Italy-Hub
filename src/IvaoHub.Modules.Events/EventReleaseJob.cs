using System.Globalization;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Events;

/// <summary>
/// Projects again the events that became seen or ended since its last run (design M4 §2.2, §2.4, §8.4; note
/// 2026-09-29-la-vita-di-un-evento), as <c>TourReleaseJob</c> does for the tours. An event published with its release later is in
/// nobody's calendar until then, and one that ended is in nobody's after — and nothing writes the event at either moment: without
/// this, the calendar and the search would keep it as its last save left it.
/// <para>It decides from its data, never from the hour it runs (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): the
/// instants passed since the start of its last run that succeeded, so a run lost — the hub asleep, a run that failed — is made up
/// by the next one, and a run done twice projects the same rows the same way. It publishes nothing and changes no event, whose
/// state is read off its dates (§2.1): it brings the mirror up to date through the interceptor, with
/// <see cref="ProjectionRefresh"/> — no audit row, no new row version under an editor that is open.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class EventReleaseJob(
    EventsDbContext database,
    HubDbContext hub,
    ProjectionRefresh projections,
    IClock clock,
    ILogger<EventReleaseJob> logger) : IJob
{
    public const string JobName = "events-release";

    /// <summary>Every quarter of an hour: an event enters the calendar at most fifteen minutes after it is seen, and leaves it as late after its end.</summary>
    public const string Cron = "0 0/15 * * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many events were projected again on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Since the start of the last run that succeeded: an event seen or ended during a run that failed is caught by the next.
        var since = await hub.JobsLog.AsNoTracking()
            .Where(run => run.Job == JobName && run.Status == "succeeded")
            .MaxAsync(run => (DateTime?)run.StartedAt, cancellationToken) ?? DateTime.MinValue;

        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;

            // A draft projects only its files whenever it is projected, and its files do not follow the clock.
            var changed = await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .Where(row => row.Status == PublishStatus.Published
                    && ((row.VisibleFromUtc > since && row.VisibleFromUtc <= now)
                        || (row.EndsAtUtc > since && row.EndsAtUtc <= now)))
                .ToListAsync(cancellationToken);

            await projections.RefreshAsync(database, changed, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{changed.Count} event(s) seen or ended projected again");
            await hub.SaveChangesAsync(cancellationToken);

            return changed.Count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The events release job failed.");

            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return 0;
        }
    }
}
