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
/// availabilities of an event with a roster, the roster is proposed, once (<see cref="RosterProposal"/>). The publication at a date is its
/// second half, E12's.
/// <para><b>It decides from its data, never from the hour it runs</b> (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): every
/// event due — its close passed, its roster never proposed, <c>roster_proposed_at</c> empty — whenever it runs, so a run lost while the hub
/// slept is made up by the next, which the core's catch up runs after a start (E10j), and a run done twice finds the rosters proposed. Its
/// row in <c>hub_jobs_log</c> says when it finished on every way out, as the core's catch up reads it. It never throws.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class EventRosterJob(
    EventsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    RosterProposal proposal,
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
            foreach (var row in due)
            {
                if (await proposal.ProposeAsync(row.Id, settings, now, cancellationToken) is { } written)
                {
                    proposed++;
                    shifts += written;
                }
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{proposed} roster(s) proposed with {shifts} shift(s)");
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
}
