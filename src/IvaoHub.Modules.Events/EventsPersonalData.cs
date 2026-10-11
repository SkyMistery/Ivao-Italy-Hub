using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The events' half of erasing a person's data (design M4 §11.1, E8b; notes 2026-09-29-i-dati-dei-membri-negli-eventi §2.5,
/// 2026-09-25-la-cancellazione-dei-dati-di-una-persona and 2026-10-11-duplica-e-la-cancellazione-negli-eventi §2), for the rows of M4a:
/// a pilot's bookings.
/// <list type="bullet">
/// <item>A booking of an event <b>still to end</b> goes (<see cref="EventState.IsOver"/>; a cancelled event too, until its end — Carmine's
/// answer 2 on issue #248, (i)): its slot is free again, on the page and in the export. Through the change
/// tracker, so that the audit keeps that a row went and the core empties its copies.</item>
/// <item>A booking of an event <b>over</b> stays, under the pseudonym the core writes into <c>booker_vid</c>: it is the event's history,
/// which its statistics count. Without what is the person's — the flight of a private slot as they wrote it (the callsign, the other
/// airport, the time there), the note the staff wrote about it when it was not flown, and the session of the network it was found in,
/// which ties the row to them on the network's tracker, as the session of a flight of the tours does.</item>
/// <item>A private arrival and its linked departure (E7) are one pilot's, in one event: they go together or stay together, and the
/// link that dissolves when one of a pair goes alone (<c>BookingPairs.LetGoAsync</c>) is not asked for.</item>
/// </list>
/// What they did as staff — an event cancelled, a booking not flown excused — stays, under the pseudonym the core writes into every
/// <c>…By</c> column, with their words, which are about others (answer 4 of the note of T20b). Nothing here writes a VID.
/// </summary>
public sealed class EventsPersonalData(EventsDbContext database, IClock clock) : IPersonalDataEraser
{
    public string ModuleKey => EventsModule.ModuleKey;

    public async Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default)
    {
        var ends = await Bookings(vid).AsNoTracking()
            .Join(CrudSource.BackOffice<Event>(database), booking => booking.EventId, row => row.Id, (booking, row) => row)
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;

        return Lines(toCome: ends.Count(row => !EventState.IsOver(row, now)), over: ends.Count(row => EventState.IsOver(row, now)));
    }

    public async Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bookings = await Bookings(request.Vid).AsTracking().ToListAsync(cancellationToken);
        var eventIds = bookings.Select(booking => booking.EventId).Distinct().ToList();
        var events = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .Where(row => eventIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        // A booking whose event is gone — unreachable today, since its slot's key holds the event — is left out, as the preview's join
        // leaves it out: the two say the same lines (the review of #249, point 1).
        var now = clock.UtcNow;
        var over = bookings.Where(booking => events.TryGetValue(booking.EventId, out var row) && EventState.IsOver(row, now)).ToList();
        var toCome = bookings.Where(booking => events.TryGetValue(booking.EventId, out var row) && !EventState.IsOver(row, now)).ToList();

        // The history of the event, without the person: the slot, the aircraft, whether and when it was flown and checked, the link
        // of a pair, when it was booked. The core writes the pseudonym into the pilot's column afterwards.
        foreach (var booking in over)
        {
            booking.Callsign = null;
            booking.OtherIcao = null;
            booking.OtherTimeUtc = null;
            booking.UnflownExcusedNote = null;
            booking.FlownSessionId = null;
        }

        database.Bookings.RemoveRange(toCome);
        await database.SaveChangesAsync(cancellationToken);

        return Lines(toCome: toCome.Count, over: over.Count);
    }

    /// <summary>The person's bookings, read past every filter: the superadmin erases bookings of events nobody sees any more.</summary>
    private IQueryable<EventBooking> Bookings(int vid) =>
        CrudSource.BackOffice<EventBooking>(database).Where(booking => booking.BookerVid == vid);

    private static IReadOnlyList<ErasureLine> Lines(int toCome, int over) =>
    [
        new("events:erasure.bookingsToCome", toCome, ErasureOutcome.Deleted),
        new("events:erasure.bookingsOver", over, ErasureOutcome.Anonymised),
    ];
}
