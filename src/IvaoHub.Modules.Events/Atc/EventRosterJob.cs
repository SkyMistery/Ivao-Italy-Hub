using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// <c>events-roster</c> (design M4 §4.3, §8.4, E11b; note 2026-10-13-la-proposta-del-roster, reading 5), its first half: at the close of the
/// availabilities of an event with a roster, the roster is proposed, once (<see cref="RosterProposal"/>). Its second half (E12): at the start
/// less <c>rosterPublishDays</c> the roster is published, once, and each controller told of their shifts (<see cref="RosterPublication"/>).
/// <para><b>It decides from its data, never from the hour it runs</b> (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): every
/// event due — its close passed, its roster never proposed, <c>roster_proposed_at</c> empty — whenever it runs, so a run lost while the hub
/// slept is made up by the next, which the core's catch up runs after a start (E10j), and a run done twice finds the rosters proposed. Its
/// row in <c>hub_jobs_log</c> says when it finished on every way out, as the core's catch up reads it. It never throws; an event whose
/// proposal fails is logged, named in the row's message and left for the next run, and the others are proposed.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class EventRosterJob(
    EventsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    RosterProposal proposal,
    RosterPublication publication,
    IClock clock,
    ILogger<EventRosterJob> logger) : IJob
{
    public const string JobName = "events-roster";

    /// <summary>
    /// Every quarter of an hour, three minutes past, off the quarters the release (0) and the reminders (10) of the events run on: a roster
    /// is proposed at most a quarter of an hour after its close. In UTC, which the trigger says.
    /// </summary>
    public const string Cron = "0 3/15 * * * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many rosters were proposed on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
            var closedBy = now.AddDays(settings.ApplicationsCloseDays);

            // The database narrows to the events whose close passed; the rule itself is read in one place (RosterProposal.IsDue).
            var due = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                    .Where(row => row.HasRoster
                        && row.Status == PublishStatus.Published
                        && row.CancelledAt == null
                        && row.RosterProposedAt == null
                        && row.StartsAtUtc <= closedBy
                        && row.EndsAtUtc > now)
                    .OrderBy(row => row.StartsAtUtc)
                    .ThenBy(row => row.Id)
                    .ToListAsync(cancellationToken))
                .Where(row => RosterProposal.IsDue(row, settings, now))
                .ToList();

            var proposed = 0;
            var shifts = 0;
            var failed = new List<long>();
            foreach (var row in due)
            {
                // One event that fails is logged and left for the next run; the others go on (the review of #262, point 2). Its
                // transaction rolled back, so it is still due.
                try
                {
                    if (await proposal.ProposeAsync(row.Id, settings, now, cancellationToken) is { } written)
                    {
                        proposed++;
                        shifts += written;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "The roster of the event {EventId} could not be proposed.", row.Id);
                    database.ChangeTracker.Clear();
                    failed.Add(row.Id);
                }
            }

            var (published, told) = await PublishAsync(settings, now, failed, cancellationToken);

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            var done = string.Create(
                CultureInfo.InvariantCulture,
                $"{proposed} roster(s) proposed with {shifts} shift(s); {published} published; {told} shift(s) told");
            entry.Message = failed.Count == 0
                ? done
                : string.Create(CultureInfo.InvariantCulture, $"{done}; not done, left for the next run: {string.Join(", ", failed)}");
            await hub.SaveChangesAsync(cancellationToken);

            return proposed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The events roster job failed.");

            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return 0;
        }
    }

    /// <summary>
    /// The second half (E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, readings 1 and 2): every roster proposed whose date came
    /// is published, once — after the proposals, so a roster proposed late is published in the same run —, and every controller with a shift
    /// not told yet of a published roster is told, one mail per event. Decided from the data: a run lost is made up by the next.
    /// </summary>
    private async Task<(int Published, int Told)> PublishAsync(EventsSettings settings, DateTime now, List<long> failed, CancellationToken cancellationToken)
    {
        var publishedBy = now.AddDays(settings.RosterPublishDays);
        var due = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .Where(row => row.HasRoster
                    && row.Status == PublishStatus.Published
                    && row.CancelledAt == null
                    && row.RosterProposedAt != null
                    && row.RosterPublishedAt == null
                    && row.StartsAtUtc <= publishedBy
                    && row.EndsAtUtc > now)
                .OrderBy(row => row.StartsAtUtc)
                .ThenBy(row => row.Id)
                .ToListAsync(cancellationToken))
            .Where(row => RosterPublication.IsDue(row, settings, now))
            .ToList();

        var published = 0;
        foreach (var row in due)
        {
            // As a proposal that fails: logged and left for the next run, the others go on.
            try
            {
                published += await publication.PublishAsync(row.Id, settings, now, cancellationToken) ? 1 : 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "The roster of the event {EventId} could not be published.", row.Id);
                database.ChangeTracker.Clear();
                failed.Add(row.Id);
            }
        }

        var told = 0;
        foreach (var (eventId, vid) in await publication.UntoldAsync(now, cancellationToken))
        {
            try
            {
                told += await publication.TellAsync(eventId, vid, now, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "The shifts of the event {EventId} could not be told to {Vid}.", eventId, vid);
                database.ChangeTracker.Clear();
                failed.Add(eventId);
            }
        }

        return (published, told);
    }
}
