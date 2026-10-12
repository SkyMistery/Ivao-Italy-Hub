using System.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>How many private slots a generation wrote, how many free ones it took away first, and how many booked ones it kept.</summary>
public sealed record PrivateSlotsGeneratedDto(int Generated, int Removed, int Kept);

/// <summary>What «generate» came to: done, refused on the event's private slots, or about an event that is not there any more.</summary>
public enum GenerationResult
{
    Done,
    Refused,
    NotFound,
}

/// <summary>
/// «Generate the private slots» (design M4 §3.2, §7.2, E7): for an event with private slots, every airport of it with a capacity gets
/// its private slots for the event's hours (<see cref="PrivateSlotGenerator"/>), at regular intervals and away from the times already
/// held. <b>Generating again replaces the free private slots</b>: the ones a pilot booked stay, and count in the capacity of their hour
/// like a public slot. An event with private slots and no public ones — an MSE, as a division presets it — has its whole capacity
/// private, with nothing in the code that knows the kind.
/// <para><b>What counts in an hour</b>: every flight already there, at every airport of the event it touches — a public slot as the
/// staff wrote it, a private one as its pilot wrote it (<see cref="BookedFlight"/>) —, in its direction: a flight between two airports
/// of the event is a departure from one and an arrival at the other.</para>
/// <para><b>One generation of an event at a time</b> (the review of #246, point 1): the generation takes the row of the event
/// <c>FOR UPDATE</c>, in a transaction that reads what is committed, around everything it reads and writes — as the lock of a pilot's
/// first booking does (<see cref="PilotBookings"/>). A second generation of the same event waits, then reads the slots the first
/// wrote and replaces them: nothing unique stops two writes of a whole set of slots without a callsign, so without the lock the
/// capacity would be doubled. The event itself is read again under the lock, as it is then: its switch, its hours, its care.</para>
/// <para>All of it in one save — the free private slots that go, the new ones in the event's care —, each with its row in the audit.
/// A pilot booking a free private slot in the same moment either took the event's row first, and the generation reads the booking
/// and keeps the slot; or meets the generation on the slot itself: the key of the booking stops the delete, the staff are told to
/// read again, and a deadlock between the two is answered the same way; or finds the slot gone. The same when another write took
/// the free slots first.</para>
/// </summary>
public sealed class PrivateSlotGeneration(EventsDbContext database)
{
    /// <summary>Where a refusal of the whole generation lands: it is about the event's private slots, not a field of a form.</summary>
    public const string Field = "privateSlots";

    /// <summary>
    /// The most private slots one generation makes: the busiest event with a few airports and a few hours takes a few thousand; more is
    /// a capacity or a window typed wrong — 999 an hour for days —, refused before a page of tens of thousands of slots is written,
    /// and counted before one is built (<see cref="PrivateSlotGenerator.Count"/>).
    /// </summary>
    public const int MaxSlots = 5000;

    public async Task<(GenerationResult Result, PrivateSlotsGeneratedDto? Generated, Refusals Problems)> GenerateAsync(
        Event row,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var problems = new Refusals();

        // Inside a transaction already open — «Duplicate» generates the private slots of its copy in its own, one transaction for the
        // whole copy (E8b) — the lock and the writes are that one's, and its caller commits: the copy is a row nobody else sees yet.
        await using var transaction = database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        var locked = await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {row.Id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var current = locked.Count == 0
            ? null
            : await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == row.Id, cancellationToken);
        if (current is null)
        {
            return (GenerationResult.NotFound, null, problems);
        }

        // The switch says what the event has (§1.2): no private slots, nothing to generate.
        if (!current.PrivateSlots)
        {
            return Refused(problems.Add(Field, "events:errors.noPrivateSlots"));
        }

        var airports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == current.Id)
            .OrderBy(airport => airport.Ordinal)
            .ThenBy(airport => airport.Id)
            .ToListAsync(cancellationToken);

        if (airports.Count == 0)
        {
            return Refused(problems.Add(Field, EventPublishing.SlotsNeedAirportsKey));
        }

        if (airports.All(airport => SlotCapacity.Of(airport).IsEmpty))
        {
            return Refused(problems.Add(Field, "events:errors.noCapacity"));
        }

        var slots = await database.Slots.Where(slot => slot.EventId == current.Id).ToListAsync(cancellationToken);
        var bookings = await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .Where(booking => booking.EventId == current.Id)
            .ToDictionaryAsync(booking => booking.SlotId, cancellationToken);

        // The free private slots the generator made go; the booked ones, and every public one, stay and count.
        var leaving = slots.Where(slot => slot is { Kind: SlotKind.Private, Generated: true } && !bookings.ContainsKey(slot.Id)).ToList();
        var staying = slots.Except(leaving).ToList();
        var held = Held(staying, bookings, airports.Select(airport => airport.Icao).ToHashSet(StringComparer.Ordinal));
        IReadOnlyCollection<HeldSlot> HeldAt(EventAirport airport) => held.GetValueOrDefault(airport.Icao) ?? [];

        // Counted first, built only when it is not too many.
        var count = 0;
        foreach (var airport in airports)
        {
            count += PrivateSlotGenerator.Count(SlotCapacity.Of(airport), current.StartsAtUtc, current.EndsAtUtc, HeldAt(airport), MaxSlots - count);
            if (count > MaxSlots)
            {
                return Refused(problems.Add(Field, "events:errors.privateSlotsTooMany"));
            }
        }

        var generated = airports
            .SelectMany(airport => PrivateSlotGenerator
                .Generate(SlotCapacity.Of(airport), current.StartsAtUtc, current.EndsAtUtc, HeldAt(airport))
                .Select(slot => new EventSlot
                {
                    EventId = current.Id,
                    Kind = SlotKind.Private,
                    EventAirportIcao = airport.Icao,
                    IsArrival = slot.IsArrival,
                    OffBlockUtc = slot.IsArrival ? null : slot.AtUtc,
                    OnBlockUtc = slot.IsArrival ? slot.AtUtc : null,
                    Generated = true,
                    OwnerDepartment = current.OwnerDepartment,
                    OwnerDepartmentMask = current.OwnerDepartmentMask,
                }))
            .ToList();

        database.Slots.RemoveRange(leaving);
        database.Slots.AddRange(generated);
        await database.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return (
            GenerationResult.Done,
            new PrivateSlotsGeneratedDto(generated.Count, leaving.Count, staying.Count(slot => slot.Kind == SlotKind.Private)),
            problems);

        static (GenerationResult, PrivateSlotsGeneratedDto?, Refusals) Refused(Refusals refusals) => (GenerationResult.Refused, null, refusals);
    }

    /// <summary>
    /// The times every airport of the event already holds, by its code: each flight that stays, at the airports of the event it
    /// touches — where it leaves from at its off block, where it lands at its on block.
    /// </summary>
    private static Dictionary<string, List<HeldSlot>> Held(
        IEnumerable<EventSlot> staying,
        IReadOnlyDictionary<long, EventBooking> bookings,
        IReadOnlySet<string> eventAirports)
    {
        var held = new Dictionary<string, List<HeldSlot>>(StringComparer.Ordinal);

        void Hold(string? airport, DateTime? at, bool isArrival)
        {
            if (airport is not null && at is { } time && eventAirports.Contains(airport))
            {
                if (!held.TryGetValue(airport, out var times))
                {
                    held[airport] = times = [];
                }

                times.Add(new HeldSlot(time, isArrival));
            }
        }

        foreach (var slot in staying)
        {
            var flight = BookedFlight.Of(slot, bookings.GetValueOrDefault(slot.Id));
            Hold(flight.DepartureIcao, flight.OffBlockUtc, isArrival: false);
            Hold(flight.ArrivalIcao, flight.OnBlockUtc, isArrival: true);
        }

        return held;
    }
}
