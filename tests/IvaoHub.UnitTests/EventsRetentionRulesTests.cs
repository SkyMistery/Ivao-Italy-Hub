using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of E15b that need no database (design M4 §4.4, §11; note 2026-10-17-la-conservazione-di-m4b): when the pilots' rows and the
/// availabilities leave an event — the pilots' on the very boundary the register counts by, also at the end of a month —, and which bookings of
/// the network stand beside the roster, with whether their booker holds the shift.
/// </summary>
public sealed class EventsRetentionRulesTests
{
    private static Event Ended(DateTime endsAtUtc) => new() { StartsAtUtc = endsAtUtc.AddHours(-4), EndsAtUtc = endsAtUtc };

    [Fact]
    public void ThePilotsRowsGoOnTheBoundaryTheRegisterCountsBy()
    {
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

        // Ended exactly 24 months ago: still counted, still kept; a minute earlier, neither.
        Assert.False(EventRetention.PilotsDue(Ended(now.AddMonths(-24)), 24, now));
        Assert.True(EventRetention.PilotsDue(Ended(now.AddMonths(-24).AddMinutes(-1)), 24, now));
        Assert.Equal(now.AddMonths(-24), EventRetention.PilotsKeptSince(now, 24));
    }

    [Fact]
    public void AtTheEndOfAMonthABookingLeavesTheDatabaseNoEarlierThanTheRegister()
    {
        // Ended on the 31st of August; six months later is the 28th of February, when the register still counts it (it counts the events ended
        // since the 28th of August). Counting forward from the end — the 31st of August plus six months is that same 28th — would delete it
        // that day, while the register still counts it.
        var ends = new DateTime(2025, 8, 31, 20, 0, 0, DateTimeKind.Utc);
        var february = new DateTime(2026, 2, 28, 20, 0, 0, DateTimeKind.Utc);

        Assert.True(ends >= EventRetention.PilotsKeptSince(february, 6));
        Assert.False(EventRetention.PilotsDue(Ended(ends), 6, february));
        Assert.True(ends.AddMonths(6) <= february);

        // From the first of March the register no longer counts it, and the retention takes it.
        var march = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(ends < EventRetention.PilotsKeptSince(march, 6));
        Assert.True(EventRetention.PilotsDue(Ended(ends), 6, march));
    }

    [Fact]
    public void TheAvailabilitiesGoWithTheEventOver()
    {
        var ends = new DateTime(2026, 10, 17, 22, 0, 0, DateTimeKind.Utc);

        Assert.False(EventRetention.AvailabilitiesDue(Ended(ends), ends.AddMinutes(-1)));
        Assert.True(EventRetention.AvailabilitiesDue(Ended(ends), ends));

        // Cancelled, and never published: over is over (question 1 of the note, (A)).
        var cancelled = Ended(ends);
        cancelled.CancelledAt = ends.AddDays(-5);
        Assert.True(EventRetention.AvailabilitiesDue(cancelled, ends.AddDays(1)));
    }

    [Fact]
    public void TheBookingsOfTheNetworkBesideTheRosterAreTheEventsCallsignsInTheirPositionsHours()
    {
        var evening = new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc);
        EventAtcPosition Position(long id, string callsign, int fromHour, int toHour) =>
            new() { Id = id, Callsign = callsign, FromUtc = evening.AddHours(fromHour), ToUtc = evening.AddHours(toHour) };
        AtcBookingDto Booking(string callsign, int fromHour, int toHour, int vid) =>
            new(callsign, evening.AddHours(fromHour), evening.AddHours(toHour), vid, AtcBookingKind.Controlling);

        // The tower twice in the evening, the approach early only.
        var positions = new[] { Position(1, "XXAA_TWR", 0, 2), Position(2, "XXAA_TWR", 3, 5), Position(3, "XXAA_APP", 0, 1) };
        var shifts = new[]
        {
            new EventAtcShift { PositionId = 2, ControllerVid = 761300, FromUtc = evening.AddHours(3), ToUtc = evening.AddHours(4) },
        };

        var beside = NetworkBookings.Beside(
            [
                // Across both windows of the tower: one row, its booker holding the shift of the second.
                Booking("xxaa_twr", 1, 4, 761300),
                // The tower between its two windows: no position is open then.
                Booking("XXAA_TWR", 2, 3, 761301),
                // The approach after its hours, and a callsign the event does not open.
                Booking("XXAA_APP", 2, 3, 761302),
                Booking("XXAA_CTR", 0, 5, 761303),
                // The approach in its hour, by somebody not on the roster.
                Booking("XXAA_APP", 0, 1, 761304),
            ],
            positions,
            shifts);

        Assert.Equal(
            [(761300, true), (761304, false)],
            beside.Select(entry => (entry.Booking.Vid, entry.OnRoster)));
    }
}
