namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// What an airport of an event takes in an hour (design M4 §1.3): movements, arrivals and departures together; or arrivals and
/// departures apart. A direction with no number takes no private slot, and an airport with none takes none at all.
/// </summary>
public sealed record SlotCapacity(int? Movements, int? Arrivals, int? Departures)
{
    public static SlotCapacity Of(EventAirport airport)
    {
        ArgumentNullException.ThrowIfNull(airport);
        return new SlotCapacity(airport.MaxMovementsPerHour, airport.MaxArrivalsPerHour, airport.MaxDeparturesPerHour);
    }

    /// <summary>No number at all: the private slots of the airport are not generated.</summary>
    public bool IsEmpty => Movements is null && Arrivals is null && Departures is null;
}

/// <summary>
/// A time an airport of the event already holds, in its direction: a public slot — booked or not —, or a private one a pilot booked.
/// Both take their part of the capacity of their hour.
/// </summary>
public readonly record struct HeldSlot(DateTime AtUtc, bool IsArrival);

/// <summary>A private slot the generator makes: its time at the airport of the event, and whether it arrives there.</summary>
public readonly record struct GeneratedSlot(DateTime AtUtc, bool IsArrival);

/// <summary>
/// The private slots of one airport of an event, generated from its capacity (design M4 §3.2, note 2026-09-29-gli-slot-e-le-prenotazioni
/// §2.4, E7). Pure, and tested as such; <see cref="PrivateSlotGeneration"/> reads the rows and writes what this answers.
/// <list type="bullet">
/// <item><b>The hours</b> are the event's: from its start, an hour each, the last one cut at its end — and it takes the part of the
/// capacity its minutes are of an hour, rounded down.</item>
/// <item><b>Regular intervals</b>: an hour that takes N slots is cut in N equal steps from its start, each at the whole minute —
/// 30 an hour is a slot every two minutes, at :00, :02, :04 and on.</item>
/// <item><b>Away from the public times</b>: each time the airport already holds — a public slot, a private one a pilot booked — takes
/// the free step nearest to it, the earlier one when two are as near; the private slots take the steps left. So the free room is the
/// capacity less the slots held, and a slot held never has a private one at its own step.</item>
/// <item><b>In arrivals and departures</b>, each direction has its own steps and its own held slots. <b>In movements</b>, the two
/// directions share the steps, and the private slots go to arrivals and departures so that the hour, with the slots it holds, keeps
/// the two as even as it can — an arrival first when they are even.</item>
/// </list>
/// A time held outside the event's hours takes nothing: the margin of six hours around a public slot's window (<see cref="SlotWindow"/>)
/// is for its own time, and no private slot is made there.
/// </summary>
public static class PrivateSlotGenerator
{
    /// <summary>The hours of an event: from its start, an hour each, the last one cut at its end. None for an event that does not last.</summary>
    public static IReadOnlyList<(DateTime FromUtc, DateTime ToUtc)> Hours(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        var hours = new List<(DateTime, DateTime)>();
        for (var from = startsAtUtc; from < endsAtUtc; from = from.AddHours(1))
        {
            var to = from.AddHours(1);
            hours.Add((from, to < endsAtUtc ? to : endsAtUtc));
        }

        return hours;
    }

    /// <summary>The private slots of one airport for the event's hours, by their time; none for an airport with no capacity.</summary>
    public static IReadOnlyList<GeneratedSlot> Generate(
        SlotCapacity capacity,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        IReadOnlyCollection<HeldSlot> held)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(held);

        var generated = new List<GeneratedSlot>();

        foreach (var (from, to) in Hours(startsAtUtc, endsAtUtc))
        {
            var inTheHour = held.Where(slot => slot.AtUtc >= from && slot.AtUtc < to).ToList();

            if (capacity.Movements is { } movements)
            {
                var arrivals = inTheHour.Count(slot => slot.IsArrival);
                var departures = inTheHour.Count - arrivals;

                foreach (var step in FreeSteps(from, to, movements, inTheHour.Select(slot => slot.AtUtc)))
                {
                    var isArrival = arrivals <= departures;
                    generated.Add(new GeneratedSlot(step, isArrival));

                    if (isArrival)
                    {
                        arrivals++;
                    }
                    else
                    {
                        departures++;
                    }
                }

                continue;
            }

            foreach (var (perHour, isArrival) in new[] { (capacity.Arrivals, true), (capacity.Departures, false) })
            {
                if (perHour is { } room)
                {
                    var times = inTheHour.Where(slot => slot.IsArrival == isArrival).Select(slot => slot.AtUtc);
                    generated.AddRange(FreeSteps(from, to, room, times).Select(step => new GeneratedSlot(step, isArrival)));
                }
            }
        }

        return [.. generated.OrderBy(slot => slot.AtUtc).ThenBy(slot => slot.IsArrival ? 0 : 1)];
    }

    /// <summary>
    /// The steps of an hour a capacity cuts it in, less the ones the times held take: each takes the free step nearest to it — the
    /// earlier when two are as near —, in the order of the times.
    /// </summary>
    public static IReadOnlyList<DateTime> FreeSteps(DateTime fromUtc, DateTime toUtc, int perHour, IEnumerable<DateTime> held)
    {
        ArgumentNullException.ThrowIfNull(held);

        var minutes = (toUtc - fromUtc).TotalMinutes;
        var count = (int)Math.Floor(perHour * minutes / 60);
        if (count <= 0)
        {
            return [];
        }

        var steps = Enumerable.Range(0, count)
            .Select(index => fromUtc.AddMinutes(Math.Round(index * minutes / count, MidpointRounding.AwayFromZero)))
            .ToList();
        var taken = new bool[count];

        foreach (var time in held.OrderBy(time => time))
        {
            var nearest = -1;
            for (var index = 0; index < count; index++)
            {
                if (!taken[index] && (nearest < 0 || Math.Abs((steps[index] - time).Ticks) < Math.Abs((steps[nearest] - time).Ticks)))
                {
                    nearest = index;
                }
            }

            if (nearest < 0)
            {
                break;
            }

            taken[nearest] = true;
        }

        return [.. steps.Where((_, index) => !taken[index])];
    }
}
