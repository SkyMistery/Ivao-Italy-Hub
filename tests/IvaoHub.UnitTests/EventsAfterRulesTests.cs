using IvaoHub.Core.Atc;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the checks after an event (M4, E13a; note 2026-10-16-le-verifiche-dopo-l-evento, readings 1–5 and 7): when an event is
/// checked and given up, the window of its flights, which session a booking flew, the minutes on a position within a shift, the threshold, who
/// covered — and the setting's range.
/// </summary>
public sealed class EventsAfterRulesTests
{
    private static readonly DateTime Start = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    private static Event Evening(PublishStatus status = PublishStatus.Published, DateTime? cancelled = null, DateTime? done = null) => new()
    {
        StartsAtUtc = Start,
        EndsAtUtc = Start.AddHours(2),
        Status = status,
        CancelledAt = cancelled,
        AfterDoneAt = done,
    };

    [Fact]
    public void AnEventIsCheckedSixHoursAfterItsEndOnceAndNeverADraftOrACancelledOne()
    {
        var end = Start.AddHours(2);

        Assert.False(AfterRules.IsDue(Evening(), end.AddHours(6).AddTicks(-1)));
        Assert.True(AfterRules.IsDue(Evening(), end.AddHours(6)));
        Assert.False(AfterRules.IsDue(Evening(PublishStatus.Draft), end.AddDays(1)));
        Assert.False(AfterRules.IsDue(Evening(cancelled: Start), end.AddDays(1)));
        Assert.False(AfterRules.IsDue(Evening(done: end.AddHours(7)), end.AddDays(1)));

        Assert.False(AfterRules.IsGivenUp(Evening(), end.AddDays(30).AddTicks(-1)));
        Assert.True(AfterRules.IsGivenUp(Evening(), end.AddDays(30)));
    }

    [Fact]
    public void TheFlightsAreLookedForInTheWindowOfTheSlots()
    {
        var (from, to) = AfterRules.FlightWindow(Evening());

        Assert.Equal(Start.AddHours(-6), from);
        Assert.Equal(Start.AddHours(8), to);
    }

    [Fact]
    public void ABookingTakesTheSessionNearestToItsDepartureThatNoOtherBookingTook()
    {
        var flight = new BookedFlight("TST001", "LIRF", Start.AddHours(1), "LIRN", Start.AddHours(2));
        IvaoTrackerSessionDto[] found = [Session(1, Start.AddMinutes(20)), Session(2, Start.AddMinutes(70)), Session(3, Start.AddMinutes(150))];

        Assert.Equal(2, AfterRules.Flown(flight, found, new HashSet<long>())?.Id);
        Assert.Equal(1, AfterRules.Flown(flight, found, new HashSet<long> { 2 })?.Id);
        Assert.Null(AfterRules.Flown(flight, found, new HashSet<long> { 1, 2, 3 }));
        Assert.Null(AfterRules.Flown(flight, [], new HashSet<long>()));
    }

    [Fact]
    public void TheMinutesOnAPositionAreCountedInsideTheShiftOnceEach()
    {
        var from = Start;
        var to = Start.AddHours(1);

        // Before the shift and after it: nothing. Across its start: the part inside.
        Assert.Equal(0, AfterRules.MinutesWithin([Connection(-90, -30)], from, to));
        Assert.Equal(0, AfterRules.MinutesWithin([Connection(70, 90)], from, to));
        Assert.Equal(20, AfterRules.MinutesWithin([Connection(-30, 20)], from, to));

        // A reconnection: two pieces; two that overlap count once.
        Assert.Equal(40, AfterRules.MinutesWithin([Connection(0, 15), Connection(35, 60)], from, to));
        Assert.Equal(30, AfterRules.MinutesWithin([Connection(0, 20), Connection(10, 30)], from, to));

        // One still open counts to the end of the shift.
        Assert.Equal(45, AfterRules.MinutesWithin([new PositionConnection(1, Start.AddMinutes(15), EndedAt: null)], from, to));
    }

    [Fact]
    public void AShiftShorterThanTheSettingAsksItsWholeLength()
    {
        Assert.Equal(30, AfterRules.Threshold(30, Start, Start.AddHours(1)));
        Assert.Equal(20, AfterRules.Threshold(30, Start, Start.AddMinutes(20)));
    }

    [Fact]
    public void WhoCoveredIsSomebodyElseWithTheMostMinutesInTheShift()
    {
        var from = Start;
        var to = Start.AddHours(1);
        AtcPresence[] online =
        [
            new("LIRF_TWR", null, Start.AddMinutes(-10), Start.AddMinutes(10)) { Vid = 761181 },
            new("LIRF_TWR", null, Start.AddMinutes(10), Start.AddMinutes(25)) { Vid = 761024 },
            new("LIRF_TWR", null, Start.AddMinutes(40), Start.AddMinutes(60)) { Vid = 761024 },
            new("LIRF_TWR", null, Start.AddMinutes(25), Start.AddMinutes(40)) { Vid = 761183 },
            new("LIRF_TWR", null, Start.AddMinutes(0), Start.AddMinutes(60)),
        ];

        // The holder and the connection without a VID are nobody; 761024 has 35 minutes, 761183 fifteen.
        Assert.Equal(761024, AfterRules.CoveredBy(online, 761181, from, to));
        Assert.Null(AfterRules.CoveredBy(online.Take(1), 761181, from, to));
    }

    [Fact]
    public void TheMinutesOfAttendanceAreAMinuteToTwelveHours()
    {
        var validator = new EventsSettingsValidator();

        Assert.Equal(30, new EventsSettings().AttendanceMinimumMinutes);
        Assert.True(validator.Validate(new EventsSettings { AttendanceMinimumMinutes = 1 }).IsValid);
        Assert.True(validator.Validate(new EventsSettings { AttendanceMinimumMinutes = 12 * 60 }).IsValid);
        Assert.Contains(
            validator.Validate(new EventsSettings { AttendanceMinimumMinutes = 0 }).Errors,
            error => error.PropertyName == nameof(EventsSettings.AttendanceMinimumMinutes));
        Assert.Contains(
            validator.Validate(new EventsSettings { AttendanceMinimumMinutes = 12 * 60 + 1 }).Errors,
            error => error.PropertyName == nameof(EventsSettings.AttendanceMinimumMinutes));
    }

    private static PositionConnection Connection(int fromMinutes, int toMinutes) =>
        new(761024, Start.AddMinutes(fromMinutes), Start.AddMinutes(toMinutes));

    private static IvaoTrackerSessionDto Session(long id, DateTime started) =>
        new(id, 761020, "TST001", started, TimeSpan.FromHours(1), HasFlightPlan: true, "LIRF", "LIRN", "A320", "{}");
}
