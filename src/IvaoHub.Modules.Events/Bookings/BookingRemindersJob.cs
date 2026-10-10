using System.Data;
using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// The reminder of the day before (design M4 §3.8, §8.4; note 2026-09-29-gli-slot-e-le-prenotazioni §2.9): <c>reminderLeadHours</c>
/// before the off block of a booking — 24 by default —, a mail to its pilot, <see cref="EventsNotifications.BookingReminder"/>, with the
/// flight, the stand and the routes of the flight operations for its two airports. The core queues mails and does not schedule them, so
/// the module looks every quarter of an hour for the bookings about to fly that were not reminded yet; <see cref="EventBooking.RemindedAt"/>
/// makes it once.
/// <para><b>Near bookings in one mail.</b> When a booking of a pilot is due, the mail takes with it every other booking of theirs in the
/// same event, not reminded yet, whose off block falls within <c>reminderLeadHours</c> after the first of them
/// (<see cref="Window"/>): the legs of an evening are one mail, not one each as they enter the window a quarter of an hour apart.</para>
/// <para><b>It decides from its data, never from the hour it runs</b> (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): a run
/// lost while the hub slept is made up by the next one, which finds the same bookings not reminded — while their off block is still
/// to come: a flight gone is reminded no more —, and a run done twice finds them reminded. Two processes running it at once are
/// serialised by a lock on the pilot's bookings not reminded yet (<c>SELECT … FOR UPDATE</c>): the second reads them reminded, and sends
/// nothing. The mark goes before the mail, as the training's reminder: a mail that fails to be queued is a reminder lost, never one sent
/// twice.</para>
/// <para>A cancelled event is reminded no more. A private slot is reminded by the off block of the flight its pilot wrote (E7): a
/// departure's time at the airport of the event, the time an arrival leaves the other airport. Past the filter of the members, as the
/// staff read: the job is nobody. It never throws: a failure is a row in <c>hub_jobs_log</c>, as for every job of the hub.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class BookingRemindersJob(
    EventsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    EventsMail mail,
    IClock clock,
    ILogger<BookingRemindersJob> logger) : IJob
{
    public const string JobName = "events-reminders";

    /// <summary>
    /// Every quarter of an hour, ten minutes past, off the quarter the release of the events runs on and the five the training's
    /// reminder does: a reminder leaves at most a quarter of an hour after its moment. The same in every time zone.
    /// </summary>
    public const string Cron = "0 10/15 * * * ?";

    private const int MaxMessageLength = 2000;

    /// <summary>
    /// The bookings of one mail: due — their off block after <paramref name="now"/> and no later than <paramref name="lead"/> from it —,
    /// and with them the near ones of the same pilot and event, up to <paramref name="lead"/> after the first that is due. Empty when
    /// none is due. Pure, so that a test reads the rule without a database.
    /// </summary>
    public static IReadOnlyList<T> Window<T>(IEnumerable<T> notReminded, Func<T, DateTime> offBlock, DateTime now, TimeSpan lead)
    {
        ArgumentNullException.ThrowIfNull(notReminded);
        ArgumentNullException.ThrowIfNull(offBlock);

        var ahead = notReminded.Where(booking => offBlock(booking) > now).OrderBy(offBlock).ToList();
        if (ahead.Count == 0 || offBlock(ahead[0]) > now + lead)
        {
            return [];
        }

        var until = offBlock(ahead[0]) + lead;
        return [.. ahead.TakeWhile(booking => offBlock(booking) <= until)];
    }

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>Returns how many bookings were reminded on this run.</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
            var lead = TimeSpan.FromHours(settings.ReminderLeadHours);
            var until = now + lead;

            // Who has a booking due now, in which event: each pair is one mail at most. The off block of a flight is the slot's, or —
            // for a private arrival (E7) — the time its pilot leaves the other airport: the database narrows to either, and the
            // flight itself decides (BookedFlight), so that the rule is read in one place.
            var candidates = await (
                    from booking in CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
                    join slot in database.Slots.AsNoTracking() on booking.SlotId equals slot.Id
                    join row in CrudSource.BackOffice<Event>(database).AsNoTracking() on booking.EventId equals row.Id
                    where booking.RemindedAt == null
                        && row.CancelledAt == null
                        && ((slot.OffBlockUtc > now && slot.OffBlockUtc <= until)
                            || (booking.OtherTimeUtc > now && booking.OtherTimeUtc <= until))
                    select new { booking, slot })
                .ToListAsync(cancellationToken);

            var due = candidates
                .Where(entry => BookedFlight.Of(entry.slot, entry.booking).OffBlockUtc is { } offBlock && offBlock > now && offBlock <= until)
                .Select(entry => (entry.booking.EventId, entry.booking.BookerVid))
                .Distinct()
                .ToList();

            var reminded = 0;
            var mails = 0;
            foreach (var (eventId, vid) in due)
            {
                var count = await RemindAsync(eventId, vid, now, lead, cancellationToken);
                reminded += count;
                mails += count > 0 ? 1 : 0;
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(CultureInfo.InvariantCulture, $"{reminded} booking(s) reminded in {mails} mail(s)");
            await hub.SaveChangesAsync(cancellationToken);

            return reminded;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The events reminders job failed.");

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
    /// One mail to one pilot about one event, under the lock of their bookings not reminded yet: they are read again as committed, the
    /// ones of the mail are marked, and the mail is queued once the mark is saved. Returns how many bookings it carries — none when
    /// another run took them first, the event was cancelled meanwhile, or nothing is due any more.
    /// </summary>
    private async Task<int> RemindAsync(long eventId, int vid, DateTime now, TimeSpan lead, CancellationToken cancellationToken)
    {
        Event? row;
        List<(EventBooking Booking, EventSlot Slot)> flights;

        await using (var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            var locked = await database.Database
                .SqlQuery<long>($"SELECT id AS `Value` FROM evt_bookings WHERE event_id = {eventId} AND booker_vid = {vid} AND reminded_at IS NULL FOR UPDATE")
                .ToListAsync(cancellationToken);

            row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == eventId && candidate.CancelledAt == null, cancellationToken);
            if (row is null || locked.Count == 0)
            {
                return 0;
            }

            var bookings = await CrudSource.BackOffice<EventBooking>(database)
                .Where(booking => locked.Contains(booking.Id) && booking.RemindedAt == null)
                .ToListAsync(cancellationToken);
            var slotIds = bookings.Select(booking => booking.SlotId).ToList();
            var slots = await database.Slots.AsNoTracking()
                .Where(slot => slotIds.Contains(slot.Id))
                .ToDictionaryAsync(slot => slot.Id, cancellationToken);

            // Each by the off block of its flight: a public slot's, a private one's as its pilot wrote it (E7).
            flights = [.. Window(
                bookings
                    .Where(booking => slots.TryGetValue(booking.SlotId, out var slot) && BookedFlight.Of(slot, booking).OffBlockUtc is not null)
                    .Select(booking => (Booking: booking, Slot: slots[booking.SlotId])),
                flight => BookedFlight.Of(flight.Slot, flight.Booking).OffBlockUtc!.Value,
                now,
                lead)];
            if (flights.Count == 0)
            {
                return 0;
            }

            // The job's bookkeeping alone: not a change of the booking, so nothing is audited.
            foreach (var flight in flights)
            {
                flight.Booking.RemindedAt = now;
            }

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // The routes of the flight operations between the airports of these flights, in the order they were written (E4).
        var routes = await database.Routes.AsNoTracking()
            .Where(route => route.EventId == eventId)
            .OrderBy(route => route.Id)
            .ToListAsync(cancellationToken);

        await mail.BookingReminderAsync(row, vid, flights, routes, cancellationToken);
        return flights.Count;
    }
}
