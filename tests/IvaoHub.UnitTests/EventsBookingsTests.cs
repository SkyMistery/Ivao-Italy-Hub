using IvaoHub.Core.Division;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of a booking that need no database (design M4 §3.3, §3.5, §3.6, E6a): two bookings of one pilot go together when the
/// gap separates them, one way or the other and exactly at the edge; a slot is booked and withdrawn until its off block and not at
/// it; the bookings of an event open at their moment and never on a cancelled event. The times are invented, at a round hour.
/// </summary>
public sealed class EventsBookingsTests
{
    private static readonly DateTime Day = new(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc);

    private static readonly BookingInterval FiveToSix = Interval(17, 0, 18, 0);

    // ---- the compatibility (§3.5) --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(18, 10, 19, 0, true)]   // after it, exactly the gap from its end
    [InlineData(18, 9, 19, 0, false)]   // after it, a minute short of the gap
    [InlineData(15, 50, 16, 50, true)]  // before it, exactly the gap before its start
    [InlineData(15, 51, 16, 51, false)] // before it, a minute short
    [InlineData(17, 30, 18, 30, false)] // overlapping
    [InlineData(17, 0, 18, 0, false)]   // the same flight
    [InlineData(16, 0, 19, 0, false)]   // around it
    [InlineData(22, 0, 23, 0, true)]    // far after
    public void TwoBookingsGoTogetherWhenTheGapSeparatesThemOneWayOrTheOther(
        int fromHour,
        int fromMinute,
        int toHour,
        int toMinute,
        bool compatible)
    {
        var other = Interval(fromHour, fromMinute, toHour, toMinute);

        // The same answer whichever of the two was booked first.
        Assert.Equal(compatible, BookingRules.Compatible(FiveToSix, other, gapMinutes: 10));
        Assert.Equal(compatible, BookingRules.Compatible(other, FiveToSix, gapMinutes: 10));
    }

    [Fact]
    public void WithNoGapTwoFlightsMayFollowEachOtherAtTheSameMinuteAndNeverOverlap()
    {
        Assert.True(BookingRules.Compatible(FiveToSix, Interval(18, 0, 19, 0), gapMinutes: 0));
        Assert.True(BookingRules.Compatible(Interval(16, 0, 17, 0), FiveToSix, gapMinutes: 0));
        Assert.False(BookingRules.Compatible(FiveToSix, Interval(17, 59, 19, 0), gapMinutes: 0));
    }

    [Fact]
    public void ABookingHoldsItsPilotFromTheOffBlockToTheOnBlockOfAPublicSlot()
    {
        var slot = Public(offBlock: Day.AddHours(17), onBlock: Day.AddHours(18).AddMinutes(10));
        Assert.Equal(new BookingInterval(Day.AddHours(17), Day.AddHours(18).AddMinutes(10)), BookingInterval.Of(slot));

        // A private slot's flight is the pilot's to write (E7): nothing to hold yet.
        Assert.Null(BookingInterval.Of(new EventSlot { Kind = SlotKind.Private, OffBlockUtc = Day.AddHours(17) }));
    }

    // ---- until when (§3.3, §3.6) ----------------------------------------------------------------------------------------------

    [Fact]
    public void ASlotIsOpenUntilItsOffBlockAndClosedFromIt()
    {
        var offBlock = Day.AddHours(17);
        var slot = Public(offBlock, offBlock.AddHours(1));

        Assert.Equal(offBlock, BookingRules.ClosesAt(slot));
        Assert.True(BookingRules.IsOpen(slot, offBlock.AddTicks(-1)));
        Assert.False(BookingRules.IsOpen(slot, offBlock));
        Assert.False(BookingRules.IsOpen(slot, offBlock.AddMinutes(1)));

        // A private slot closes as E7 says: until then it is never open to these verbs.
        var generated = new EventSlot { Kind = SlotKind.Private, OffBlockUtc = offBlock };
        Assert.Null(BookingRules.ClosesAt(generated));
        Assert.False(BookingRules.IsOpen(generated, offBlock.AddHours(-1)));
    }

    [Fact]
    public void TheBookingsOfAnEventOpenAtTheirMomentAndNeverOnACancelledEvent()
    {
        var opens = Day.AddDays(-7);
        var row = new Event { Status = PublishStatus.Published, BookingOpensAtUtc = opens, StartsAtUtc = Day.AddHours(17), EndsAtUtc = Day.AddHours(22) };

        Assert.Equal(BookingRules.NotOpenKey, BookingRules.EventClosed(row, opens.AddTicks(-1)));
        Assert.Null(BookingRules.EventClosed(row, opens));
        Assert.Null(BookingRules.EventClosed(row, Day.AddHours(19)));

        // An event without the moment its bookings open takes none, and a cancelled one none whatever its dates.
        Assert.Equal(BookingRules.NotOpenKey, BookingRules.EventClosed(new Event { BookingOpensAtUtc = null }, Day));
        row.CancelledAt = opens.AddDays(1);
        Assert.Equal(BookingRules.CancelledKey, BookingRules.EventClosed(row, Day.AddHours(19)));
    }

    private static BookingInterval Interval(int fromHour, int fromMinute, int toHour, int toMinute) =>
        new(Day.AddHours(fromHour).AddMinutes(fromMinute), Day.AddHours(toHour).AddMinutes(toMinute));

    private static EventSlot Public(DateTime offBlock, DateTime onBlock) =>
        new() { Kind = SlotKind.Public, OffBlockUtc = offBlock, OnBlockUtc = onBlock };
}
