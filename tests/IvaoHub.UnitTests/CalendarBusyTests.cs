using IvaoHub.Core.Content;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The time an entry of the calendar takes, as the date picker draws it and the server refuses it (note
/// <c>decisions/2026-11-18-il-calendario-libero-e-occupato.md</c>, Carmine's answers 3 and 4): in UTC; an entry with no end takes
/// the hour it starts in, one of the whole day its days, any other its start to its end.
/// </summary>
public sealed class CalendarBusyTests
{
    private static DateTime At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void AnEntryWithAnEndTakesItsStartToItsEnd()
    {
        Assert.Equal((At(20, 14, 30), At(20, 16)), CalendarBusy.Occupies(At(20, 14, 30), At(20, 16), allDay: false));
    }

    [Fact]
    public void AnEntryWithNoEndTakesTheHourItStartsIn()
    {
        Assert.Equal((At(20, 14), At(20, 15)), CalendarBusy.Occupies(At(20, 14, 30), endsAtUtc: null, allDay: false));
    }

    [Fact]
    public void AnEndThatIsNotAfterTheStartIsNoEnd()
    {
        Assert.Equal((At(20, 14), At(20, 15)), CalendarBusy.Occupies(At(20, 14, 30), At(20, 14, 30), allDay: false));
        Assert.Equal((At(20, 14), At(20, 15)), CalendarBusy.Occupies(At(20, 14, 30), At(20, 9), allDay: false));
    }

    [Fact]
    public void AnEntryOfTheWholeDayTakesItsDays()
    {
        // One day, however it is written: no end, an end on the same day, or the midnight that closes it.
        Assert.Equal((At(20, 0), At(21, 0)), CalendarBusy.Occupies(At(20, 0), endsAtUtc: null, allDay: true));
        Assert.Equal((At(20, 0), At(21, 0)), CalendarBusy.Occupies(At(20, 0), At(20, 0), allDay: true));
        Assert.Equal((At(20, 0), At(21, 0)), CalendarBusy.Occupies(At(20, 0), At(21, 0), allDay: true));

        // Three days, from the start of the first to the end of the last.
        Assert.Equal((At(20, 0), At(23, 0)), CalendarBusy.Occupies(At(20, 10), At(22, 18), allDay: true));
    }

    [Fact]
    public void AMomentWithNoKindIsUtc()
    {
        var unspecified = new DateTime(2026, 11, 20, 14, 30, 0, DateTimeKind.Unspecified);

        var (starts, ends) = CalendarBusy.Occupies(unspecified, endsAtUtc: null, allDay: false);

        Assert.Equal(DateTimeKind.Utc, starts.Kind);
        Assert.Equal((At(20, 14), At(20, 15)), (starts, ends));
    }

    [Fact]
    public void AWindowEndsAfterItStartsAndLastsAtMostSixtyTwoDays()
    {
        Assert.True(CalendarBusy.IsWindow(At(1, 0), At(1, 0).AddDays(CalendarBusy.MaxWindowDays)));
        Assert.False(CalendarBusy.IsWindow(At(1, 0), At(1, 0).AddDays(CalendarBusy.MaxWindowDays).AddSeconds(1)));
        Assert.False(CalendarBusy.IsWindow(At(2, 0), At(1, 0)));
        Assert.False(CalendarBusy.IsWindow(At(1, 0), At(1, 0)));
    }
}
