using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The private slots without a database (M4, E7; design §3.2, §3.4, §3.5): the generator — the event's hours, the capacity in arrivals
/// and departures or in movements, the times held that take their steps, regular intervals, a part of an hour —, and the flight of a
/// private booking — its times, when it closes. The times are invented, on one day, at round hours.
/// </summary>
public sealed class EventsPrivateSlotsTests
{
    private static readonly DateTime Day = new(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);

    // ---- the hours -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheHoursAreTheEventsFromItsStartTheLastOneCutAtItsEnd()
    {
        Assert.Equal(
            [(At(17), At(18)), (At(18), At(19)), (At(19), At(19, 30))],
            PrivateSlotGenerator.Hours(At(17), At(19, 30)));

        // Counted from the start, not from the clock: an event at half past has its hours at half past.
        Assert.Equal([(At(17, 30), At(18, 30)), (At(18, 30), At(19))], PrivateSlotGenerator.Hours(At(17, 30), At(19)));
        Assert.Empty(PrivateSlotGenerator.Hours(At(17), At(17)));
    }

    // ---- the capacity in arrivals and departures -------------------------------------------------------------------------------

    [Fact]
    public void EachDirectionHasItsStepsAtRegularIntervalsFromTheStartOfTheHour()
    {
        var slots = Generate(new SlotCapacity(null, Arrivals: 4, Departures: 2), At(17), At(18));

        Assert.Equal([At(17), At(17, 15), At(17, 30), At(17, 45)], Times(slots, arrivals: true));
        Assert.Equal([At(17), At(17, 30)], Times(slots, arrivals: false));
    }

    [Fact]
    public void AStepIsAtTheWholeMinuteWhenTheHourDoesNotDivide()
    {
        var slots = Generate(new SlotCapacity(null, Arrivals: 7, Departures: null), At(17), At(18));

        Assert.Equal([0, 9, 17, 26, 34, 43, 51], Times(slots, arrivals: true).Select(time => time.Minute));

        // A direction with no number takes nothing.
        Assert.Empty(Times(slots, arrivals: false));
    }

    [Fact]
    public void ThePublicSlotsTakeTheNearestStepOfTheirDirectionAndThePrivateOnesTheRest()
    {
        // An arrival at 17:07 is nearer :00 than :15; a departure at 17:20 is nearer :30 than :00.
        var slots = Generate(
            new SlotCapacity(null, Arrivals: 4, Departures: 2),
            At(17),
            At(18),
            new HeldSlot(At(17, 7), IsArrival: true),
            new HeldSlot(At(17, 20), IsArrival: false));

        Assert.Equal([At(17, 15), At(17, 30), At(17, 45)], Times(slots, arrivals: true));
        Assert.Equal([At(17)], Times(slots, arrivals: false));
    }

    [Fact]
    public void ATimeAsNearToTwoStepsTakesTheEarlierOne()
    {
        // Steps at :00 and :30: a time at :15 takes :00.
        Assert.Equal([At(17, 30)], PrivateSlotGenerator.FreeSteps(At(17), At(18), perHour: 2, [At(17, 15)]));
    }

    [Fact]
    public void AnHourFullOfPublicSlotsHasNoRoomLeft()
    {
        var slots = Generate(
            new SlotCapacity(null, Arrivals: 2, Departures: null),
            At(17),
            At(18),
            new HeldSlot(At(17, 5), IsArrival: true),
            new HeldSlot(At(17, 25), IsArrival: true),
            new HeldSlot(At(17, 40), IsArrival: true));

        Assert.Empty(slots);
    }

    // ---- the capacity in movements ---------------------------------------------------------------------------------------------

    [Fact]
    public void InMovementsTheDirectionsAlternateFromAnArrivalWhenNothingIsHeld()
    {
        var slots = Generate(new SlotCapacity(Movements: 4, null, null), At(17), At(18));

        Assert.Equal(
            [new GeneratedSlot(At(17), true), new GeneratedSlot(At(17, 15), false), new GeneratedSlot(At(17, 30), true), new GeneratedSlot(At(17, 45), false)],
            slots);
    }

    [Fact]
    public void InMovementsThePublicSlotsTakeTheirStepsAndThePrivateOnesEvenTheDirections()
    {
        // Six movements, a step every ten minutes; three public departures take :00, :10 and :20, and the hour's other three steps
        // are arrivals, so that the hour has as many of each.
        var slots = Generate(
            new SlotCapacity(Movements: 6, null, null),
            At(17),
            At(18),
            new HeldSlot(At(17), IsArrival: false),
            new HeldSlot(At(17, 10), IsArrival: false),
            new HeldSlot(At(17, 20), IsArrival: false));

        Assert.Equal([At(17, 30), At(17, 40), At(17, 50)], Times(slots, arrivals: true));
        Assert.Empty(Times(slots, arrivals: false));
    }

    // ---- the hours of the event ----------------------------------------------------------------------------------------------

    [Fact]
    public void APartOfAnHourTakesItsPartOfTheCapacity()
    {
        // The last half hour of an event takes half of thirty an hour: fifteen, every two minutes.
        var slots = Generate(new SlotCapacity(null, Arrivals: 30, Departures: null), At(18), At(18, 30));

        Assert.Equal(15, slots.Count);
        Assert.Equal(At(18), slots[0].AtUtc);
        Assert.Equal(At(18, 28), slots[^1].AtUtc);

        // Seven an hour for forty-five minutes: five, the part rounded down.
        Assert.Equal(5, Generate(new SlotCapacity(null, Arrivals: 7, Departures: null), At(18), At(18, 45)).Count);
    }

    [Fact]
    public void EveryHourOfTheEventIsGeneratedAndATimeOutsideItTakesNothing()
    {
        // A public arrival five hours after the end — inside the margin of its window — takes no room of the event's hours.
        var slots = Generate(
            new SlotCapacity(null, Arrivals: 2, Departures: null),
            At(17),
            At(19),
            new HeldSlot(At(23, 59), IsArrival: true),
            new HeldSlot(At(16, 59), IsArrival: true));

        Assert.Equal([At(17), At(17, 30), At(18), At(18, 30)], Times(slots, arrivals: true));
    }

    [Fact]
    public void AGenerationAgainKeepsTheRoomOfABookedPrivateSlot()
    {
        // The first generation, then a pilot books the arrival at 17:15: generated again, the hour keeps it, and no new slot is at its step.
        var capacity = new SlotCapacity(null, Arrivals: 4, Departures: null);
        var first = Generate(capacity, At(17), At(18));
        Assert.Contains(new GeneratedSlot(At(17, 15), true), first);

        var again = Generate(capacity, At(17), At(18), new HeldSlot(At(17, 15), IsArrival: true));

        Assert.Equal([At(17), At(17, 30), At(17, 45)], Times(again, arrivals: true));
    }

    [Fact]
    public void AnAirportWithNoCapacityGetsNothing()
    {
        var none = new SlotCapacity(null, null, null);

        Assert.True(none.IsEmpty);
        Assert.Empty(Generate(none, At(17), At(22)));
    }

    // ---- the flight of a private booking (§3.4, §3.5) ----------------------------------------------------------------------------

    [Fact]
    public void APrivateFlightIsItsTimeAtTheAirportAndTheOneItsPilotWrote()
    {
        var arrival = Private(isArrival: true, At(18));
        var departure = Private(isArrival: false, At(19));

        // An arrival leaves the other airport when its pilot says and lands at the slot's time; a departure the other way round.
        Assert.Equal(
            new BookedFlight("XYZ1", "XXBB", At(16, 40), "XXAA", At(18)),
            BookedFlight.Of(arrival, Booking("XYZ1", "XXBB", At(16, 40))));
        Assert.Equal(
            new BookedFlight("XYZ2", "XXAA", At(19), "XXCC", At(20, 10)),
            BookedFlight.Of(departure, Booking("XYZ2", "XXCC", At(20, 10))));

        // Nobody booked it: its airport and its time, nothing else — as the export shows a free private slot.
        Assert.Equal(new BookedFlight(null, null, null, "XXAA", At(18)), BookedFlight.Of(arrival, booking: null));
        Assert.Null(BookingInterval.Of(arrival, booking: null));
    }

    [Fact]
    public void APrivateBookingHoldsItsPilotForItsWholeFlightAndClosesAtItsOffBlock()
    {
        var arrival = Private(isArrival: true, At(18));
        var booked = Booking("XYZ1", "XXBB", At(16, 40));

        Assert.Equal(new BookingInterval(At(16, 40), At(18)), BookingInterval.Of(arrival, booked));

        // An arrival closes when its pilot leaves the other airport, not when it lands: booked and withdrawn until then.
        Assert.Equal(At(16, 40), BookingRules.ClosesAt(arrival, booked));
        Assert.True(BookingRules.IsOpen(arrival, booked, At(16, 39)));
        Assert.False(BookingRules.IsOpen(arrival, booked, At(16, 40)));

        // A departure closes at its time at the airport of the event.
        var departure = Private(isArrival: false, At(19));
        Assert.Equal(At(19), BookingRules.ClosesAt(departure, Booking("XYZ2", "XXCC", At(20, 10))));

        // A private slot alone has no flight, so nothing to close: it is booked with one.
        Assert.Null(BookingRules.ClosesAt(arrival));
    }

    [Fact]
    public void APrivateFlightIsCheckedAgainstAPublicOneOfThePilotLikeAnyOther()
    {
        // A public flight from 17:00 to 18:10, and a private arrival leaving the other airport at 18:15: five minutes, short of ten.
        var publicFlight = BookingInterval.Of(new EventSlot { Kind = SlotKind.Public, OffBlockUtc = At(17), OnBlockUtc = At(18, 10) })!.Value;
        var privateFlight = BookingInterval.Of(Private(isArrival: true, At(19)), Booking("XYZ1", "XXBB", At(18, 15)))!.Value;

        Assert.False(BookingRules.Compatible(publicFlight, privateFlight, gapMinutes: 10));
        Assert.True(BookingRules.Compatible(publicFlight, privateFlight, gapMinutes: 5));
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------------------

    private static List<GeneratedSlot> Generate(SlotCapacity capacity, DateTime starts, DateTime ends, params HeldSlot[] held) =>
        [.. PrivateSlotGenerator.Generate(capacity, starts, ends, held)];

    private static List<DateTime> Times(IEnumerable<GeneratedSlot> slots, bool arrivals) =>
        [.. slots.Where(slot => slot.IsArrival == arrivals).Select(slot => slot.AtUtc)];

    private static EventSlot Private(bool isArrival, DateTime at) => new()
    {
        Kind = SlotKind.Private,
        EventAirportIcao = "XXAA",
        IsArrival = isArrival,
        OffBlockUtc = isArrival ? null : at,
        OnBlockUtc = isArrival ? at : null,
        Generated = true,
    };

    private static EventBooking Booking(string callsign, string other, DateTime otherTime) =>
        new() { Callsign = callsign, OtherIcao = other, OtherTimeUtc = otherTime, AircraftIcao = "XA20" };
}
