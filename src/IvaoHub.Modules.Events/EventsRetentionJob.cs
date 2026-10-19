using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The retention of the events (design M4 §8.4, §11), <c>events-retention</c>, once a month — born with E17 for the events in person, and
/// widened by E15b to the rows of M4b. Notes 2026-10-15-le-attivita-e-la-conservazione-breve §4 and 2026-10-17-la-conservazione-di-m4b.
/// <list type="bullet">
/// <item><b>The events in person</b>: their registrations and the seats of their activities say who came in person and with what, so they go
/// <c>inPersonRetentionMonths</c> after the event's end (<see cref="InPersonRetention.IsDue"/>).</item>
/// <item><b>The pilots</b>: their bookings and their reports of support, with the items and the signals of award of those reports, go
/// <c>pilotRetentionMonths</c> after the end (<see cref="EventRetention.PilotsDue"/>) — the boundary the register of the pilots counts by,
/// so the register counts the same before and after a run.</item>
/// <item><b>The availabilities</b> of the controllers, with their windows, go with the event over (<see cref="EventRetention.AvailabilitiesDue"/>).</item>
/// <item><b>The shifts, how they went, the reports of the controllers and the requests to hand a shift over stay for good</b>: they are the
/// register of reliability (c3).</item>
/// </list>
/// <para>Before anything goes, in the same transaction, the event's statistics are worked out from its rows (<see cref="EventStatistics"/>,
/// under the lock of the event's row, as every write of them): how many booked and flew, the availabilities, the reports and the signals,
/// how many registered, the sums of the answers and the seats by activity. Afterwards the statistics keep those numbers, and the staff's lists
/// of an event in person read them back. One transaction per event, through the interceptor like every write (no bulk statement), as the
/// retention of the tours; and since the audit would keep the member's data of every row deleted — the answers of a registration, the flight
/// of a private booking, the note of an availability, the validator's note on a report, the callsign of an item — the core empties those
/// columns there too, in the same transaction (<see cref="AuditRedaction.EmptyDeletedAsync{TEntity, TKey}"/>, E10s).</para>
/// <para><b>It decides from its data, never from the hour it runs</b> (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): an event past
/// a retention with rows of it left. A run lost while the hub slept is made up by the next, or by the core's catch up (E10j); a run done twice
/// finds nothing. Past the filter of the members: the job is nobody. It never throws: a failure is a row in <c>hub_jobs_log</c>, as for every
/// job of the hub, written with its end on every way out; an event that fails is logged and left for the next run.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class EventsRetentionJob(
    EventsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    EventStatistics statistics,
    IClock clock,
    ILogger<EventsRetentionJob> logger) : IJob
{
    public const string JobName = "events-retention";

    /// <summary>Its trigger, by name: a test reads its zone and its cron.</summary>
    public const string TriggerName = $"{JobName}-monthly";

    /// <summary>The first day of every month at 04:40, in UTC (the trigger says so), after the retention of the tours.</summary>
    public const string Cron = "0 40 4 1 * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>
    /// What a run did: the events some of whose rows went, the registrations and seats of the events in person, the bookings and the reports
    /// of the pilots, and the availabilities of the controllers deleted.
    /// </summary>
    public sealed record Outcome(int Events, int Registrations, int Seats, int Bookings = 0, int Reports = 0, int Availabilities = 0)
    {
        public Outcome Add(Outcome other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return new(
                Events + other.Events,
                Registrations + other.Registrations,
                Seats + other.Seats,
                Bookings + other.Bookings,
                Reports + other.Reports,
                Availabilities + other.Availabilities);
        }
    }

    public async Task<Outcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

            // The events that ended at least that long ago and still hold rows of that kind; the exact rule is asked of each below, in one
            // place each.
            var inPersonEnded = now.AddMonths(-settings.InPersonRetentionMonths);
            var pilotsKeptSince = EventRetention.PilotsKeptSince(now, settings.PilotRetentionMonths);
            var inPerson = CrudSource.BackOffice<EventRegistration>(database).Select(registration => registration.EventId)
                .Union(CrudSource.BackOffice<EventActivitySeat>(database).Select(seat => seat.EventId));
            var pilots = CrudSource.BackOffice<EventBooking>(database).Select(booking => booking.EventId)
                .Union(CrudSource.BackOffice<EventReport>(database).Where(report => report.Kind == ReportKind.Pilot).Select(report => report.EventId));
            var available = CrudSource.BackOffice<EventAtcAvailability>(database).Select(availability => availability.EventId);

            var due = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                    .Where(row => (row.EndsAtUtc <= inPersonEnded && inPerson.Contains(row.Id))
                        || (row.EndsAtUtc < pilotsKeptSince && pilots.Contains(row.Id))
                        || (row.EndsAtUtc <= now && available.Contains(row.Id)))
                    .OrderBy(row => row.EndsAtUtc)
                    .ThenBy(row => row.Id)
                    .ToListAsync(cancellationToken))
                .Where(row => InPersonRetention.IsDue(row, settings.InPersonRetentionMonths, now)
                    || EventRetention.PilotsDue(row, settings.PilotRetentionMonths, now)
                    || EventRetention.AvailabilitiesDue(row, now))
                .ToList();

            // One event that fails is logged and left for the next run, never a stop for every event after it, month after month (the review
            // of #271, point 6; as events-roster does): its transaction took everything of it back.
            var outcome = new Outcome(0, 0, 0);
            var failed = new List<long>();
            foreach (var row in due)
            {
                try
                {
                    outcome = outcome.Add(await RetireAsync(row, settings, now, cancellationToken));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "The rows of the event {EventId} could not be retired.", row.Id);
                    database.ChangeTracker.Clear();
                    failed.Add(row.Id);
                }
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            var done = string.Create(
                CultureInfo.InvariantCulture,
                $"{outcome.Events} event(s) retired: {outcome.Registrations} registration(s), {outcome.Seats} seat(s), {outcome.Bookings} booking(s), {outcome.Reports} pilot report(s), {outcome.Availabilities} availability(ies) deleted");
            entry.Message = failed.Count == 0
                ? done
                : string.Create(CultureInfo.InvariantCulture, $"{done}; not done, left for the next run: {string.Join(", ", failed)}");
            if (entry.Message.Length > MaxMessageLength)
            {
                entry.Message = entry.Message[..MaxMessageLength];
            }

            await hub.SaveChangesAsync(cancellationToken);

            return outcome;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The events retention job failed.");

            database.ChangeTracker.Clear();
            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return new Outcome(0, 0, 0);
        }
    }

    /// <summary>
    /// One event, in one transaction: its statistics worked out from its rows, then whatever of it is past its retention deleted, and the
    /// member's data of every row deleted emptied in the audit. Returns how many of each went.
    /// </summary>
    private async Task<Outcome> RetireAsync(Event row, EventsSettings settings, DateTime now, CancellationToken cancellationToken)
    {
        // One transaction: the statistics, the rows gone, and their columns emptied in their copies in the audit (E10s, Carmine's answer 3
        // on issue #267) — a failure takes all of it back, and the next run finds the rows again.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Under the lock of the event's row, from the rows still there: what the statistics keep once they go.
        await statistics.WriteAsync(row, now, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        List<EventRegistration> registrations = [];
        List<EventActivitySeat> seats = [];
        if (InPersonRetention.IsDue(row, settings.InPersonRetentionMonths, now))
        {
            registrations = await CrudSource.BackOffice<EventRegistration>(database)
                .Where(registration => registration.EventId == row.Id)
                .ToListAsync(cancellationToken);
            seats = await CrudSource.BackOffice<EventActivitySeat>(database)
                .Where(seat => seat.EventId == row.Id)
                .ToListAsync(cancellationToken);
            database.Seats.RemoveRange(seats);
            database.Registrations.RemoveRange(registrations);
        }

        List<EventBooking> bookings = [];
        List<EventReport> reports = [];
        List<EventReportItem> items = [];
        if (EventRetention.PilotsDue(row, settings.PilotRetentionMonths, now))
        {
            // A private arrival and its departure are one pilot's in one event (E7): they go together, as every booking of the event.
            bookings = await CrudSource.BackOffice<EventBooking>(database)
                .Where(booking => booking.EventId == row.Id)
                .ToListAsync(cancellationToken);
            reports = await CrudSource.BackOffice<EventReport>(database)
                .Where(report => report.EventId == row.Id && report.Kind == ReportKind.Pilot)
                .ToListAsync(cancellationToken);
            var reportIds = reports.Select(report => report.Id).ToList();

            // The keys take the items and the signals along anyway; deleted here, each leaves its row in the audit, and a signal of the
            // core's queue still waiting for somebody goes with its row (E14b).
            items = await CrudSource.BackOffice<EventReportItem>(database)
                .Where(item => reportIds.Contains(item.ReportId))
                .ToListAsync(cancellationToken);
            database.ReportAwards.RemoveRange(await CrudSource.BackOffice<EventReportAward>(database)
                .Where(award => reportIds.Contains(award.ReportId))
                .ToListAsync(cancellationToken));
            database.ReportItems.RemoveRange(items);
            database.Reports.RemoveRange(reports);
            database.Bookings.RemoveRange(bookings);
        }

        List<EventAtcAvailability> availabilities = [];
        if (EventRetention.AvailabilitiesDue(row, now))
        {
            availabilities = await CrudSource.BackOffice<EventAtcAvailability>(database)
                .Where(availability => availability.EventId == row.Id)
                .ToListAsync(cancellationToken);
            database.AtcWindows.RemoveRange(await CrudSource.BackOffice<EventAtcWindow>(database)
                .Where(window => window.EventId == row.Id)
                .ToListAsync(cancellationToken));
            database.AtcAvailabilities.RemoveRange(availabilities);
        }

        await database.SaveChangesAsync(cancellationToken);

        // The seats, the windows and the signals have no column marked as the member's data (reading 7 of E17's note): there is nothing of
        // theirs to empty, and the core's call refuses such an entity.
        await EmptyAsync<EventRegistration>([.. registrations.Select(registration => registration.Id)], cancellationToken);
        await EmptyAsync<EventBooking>([.. bookings.Select(booking => booking.Id)], cancellationToken);
        await EmptyAsync<EventReport>([.. reports.Select(report => report.Id)], cancellationToken);
        await EmptyAsync<EventReportItem>([.. items.Select(item => item.Id)], cancellationToken);
        await EmptyAsync<EventAtcAvailability>([.. availabilities.Select(availability => availability.Id)], cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return new Outcome(1, registrations.Count, seats.Count, bookings.Count, reports.Count, availabilities.Count);
    }

    /// <summary>The core's emptying in the audit for the rows of one entity deleted, when there are any.</summary>
    private async Task EmptyAsync<TEntity>(IReadOnlyCollection<long> keys, CancellationToken cancellationToken)
        where TEntity : class
    {
        if (keys.Count > 0)
        {
            await AuditRedaction.EmptyDeletedAsync<TEntity, long>(database, keys, cancellationToken);
        }
    }
}
