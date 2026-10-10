using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>How many private slots a generation wrote, how many free ones it took away first, and how many booked ones it kept.</summary>
public sealed record PrivateSlotsGeneratedDto(int Generated, int Removed, int Kept);

/// <summary>
/// «Generate the private slots» (design M4 §3.2, §7.2, E7): for an event with private slots, every airport of it with a capacity gets
/// its private slots for the event's hours (<see cref="PrivateSlotGenerator"/>), at regular intervals and away from the times already
/// held. <b>Generating again replaces the free private slots</b>: the ones a pilot booked stay, and count in the capacity of their hour
/// like a public slot. An event with private slots and no public ones — an MSE, as a division presets it — has its whole capacity
/// private, with nothing in the code that knows the kind.
/// <para><b>What counts in an hour</b>: every flight already there, at every airport of the event it touches — a public slot as the
/// staff wrote it, a private one as its pilot wrote it (<see cref="BookedFlight"/>) —, in its direction: a flight between two airports
/// of the event is a departure from one and an arrival at the other.</para>
/// <para>All of it in one save — the free private slots that go, the new ones in the event's care —, each with its row in the audit.
/// A pilot booking a free private slot in the same moment stops its delete (the key of the booking), and the staff are told to look
/// again; the same when another generation, or «delete the free ones», took some of them first.</para>
/// </summary>
public sealed class PrivateSlotGeneration(EventsDbContext database)
{
    /// <summary>Where a refusal of the whole generation lands: it is about the event's private slots, not a field of a form.</summary>
    public const string Field = "privateSlots";

    /// <summary>
    /// The most private slots one generation makes: the busiest event with a few airports and a few hours takes a few thousand; more is
    /// a capacity or a window typed wrong — 999 an hour for days —, refused before a page of tens of thousands of slots is written.
    /// </summary>
    public const int MaxSlots = 5000;

    public async Task<(PrivateSlotsGeneratedDto? Result, Refusals Problems)> GenerateAsync(Event row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var problems = new Refusals();

        // The switch says what the event has (§1.2): no private slots, nothing to generate.
        if (!row.PrivateSlots)
        {
            return (null, problems.Add(Field, "events:errors.noPrivateSlots"));
        }

        var airports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == row.Id)
            .OrderBy(airport => airport.Ordinal)
            .ThenBy(airport => airport.Id)
            .ToListAsync(cancellationToken);

        if (airports.Count == 0)
        {
            return (null, problems.Add(Field, EventPublishing.SlotsNeedAirportsKey));
        }

        if (airports.All(airport => SlotCapacity.Of(airport).IsEmpty))
        {
            return (null, problems.Add(Field, "events:errors.noCapacity"));
        }

        var slots = await database.Slots.Where(slot => slot.EventId == row.Id).ToListAsync(cancellationToken);
        var bookings = await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .Where(booking => booking.EventId == row.Id)
            .ToDictionaryAsync(booking => booking.SlotId, cancellationToken);

        // The free private slots the generator made go; the booked ones, and every public one, stay and count.
        var leaving = slots.Where(slot => slot is { Kind: SlotKind.Private, Generated: true } && !bookings.ContainsKey(slot.Id)).ToList();
        var staying = slots.Except(leaving).ToList();
        var held = Held(staying, bookings, airports.Select(airport => airport.Icao).ToHashSet(StringComparer.Ordinal));

        var generated = airports
            .SelectMany(airport => PrivateSlotGenerator
                .Generate(SlotCapacity.Of(airport), row.StartsAtUtc, row.EndsAtUtc, held.GetValueOrDefault(airport.Icao) ?? [])
                .Select(slot => new EventSlot
                {
                    EventId = row.Id,
                    Kind = SlotKind.Private,
                    EventAirportIcao = airport.Icao,
                    IsArrival = slot.IsArrival,
                    OffBlockUtc = slot.IsArrival ? null : slot.AtUtc,
                    OnBlockUtc = slot.IsArrival ? slot.AtUtc : null,
                    Generated = true,
                    OwnerDepartment = row.OwnerDepartment,
                    OwnerDepartmentMask = row.OwnerDepartmentMask,
                }))
            .ToList();

        if (generated.Count > MaxSlots)
        {
            return (null, problems.Add(Field, "events:errors.privateSlotsTooMany"));
        }

        database.Slots.RemoveRange(leaving);
        database.Slots.AddRange(generated);
        await database.SaveChangesAsync(cancellationToken);

        return (new PrivateSlotsGeneratedDto(generated.Count, leaving.Count, staying.Count(slot => slot.Kind == SlotKind.Private)), problems);
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
