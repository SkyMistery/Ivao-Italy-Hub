using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// How the hub writes a date is a setting of the division (note <c>decisions/2026-10-31-il-formato-della-data.md</c>, from the 22
/// October plan §4.E): <c>dateFormat</c> in <c>division.json</c>, <c>dd/mm/yyyy</c> when left out, <c>mm/dd/yyyy</c> or
/// <c>yyyy/mm/dd</c> by choice, and refused at start up when it is anything else. The time after it is twenty four hours.
/// </summary>
public sealed class DateFormatTests
{
    private static readonly DateTime Evening = new(2026, 3, 11, 19, 5, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("dd/mm/yyyy", "11/03/2026", "11/03/2026 19:05")]
    [InlineData("mm/dd/yyyy", "03/11/2026", "03/11/2026 19:05")]
    [InlineData("yyyy/mm/dd", "2026/03/11", "2026/03/11 19:05")]
    public void EachFormatWritesTheDayInItsOrderAndTheTimeInTwentyFourHours(string format, string day, string moment)
    {
        var division = Division() with { DateFormat = format };

        Assert.True(new DivisionOptionsValidator().Validate(null, division).Succeeded);
        Assert.Equal(day, HubDates.Day(DateOnly.FromDateTime(Evening), division.ResolveDateOrder()));
        Assert.Equal(day, HubDates.Day(Evening, division.ResolveDateOrder()));
        Assert.Equal(moment, HubDates.Moment(Evening, division.ResolveDateOrder()));
    }

    [Fact]
    public void ADivisionThatSaysNothingWritesTheDayFirst()
    {
        var division = Division();

        Assert.Equal(HubDates.DefaultFormat, division.DateFormat);
        Assert.Equal(DateOrder.DayMonthYear, division.ResolveDateOrder());
    }

    [Theory]
    [InlineData("dd-mm-yyyy")]
    [InlineData("DD/MM/YYYY")]
    [InlineData("yyyy-mm-dd")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherFormatStopsTheStart(string? format)
    {
        var result = new DivisionOptionsValidator().Validate(null, Division() with { DateFormat = format! });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("'dateFormat'", StringComparison.Ordinal));

        // And if a file got past the validator, a mail would still write a date: the default, never one that throws.
        Assert.Equal(DateOrder.DayMonthYear, (Division() with { DateFormat = format! }).ResolveDateOrder());
    }

    [Theory]
    // On the day it starts: its end is a time alone.
    [InlineData("2026-11-21T17:00:00", "2026-11-21T22:00:00", "21/11/2026 17:00 – 22:00")]
    // Past midnight: its end carries its day.
    [InlineData("2026-11-21T23:30:00", "2026-11-22T00:30:00", "21/11/2026 23:30 – 22/11/2026 00:30")]
    // At midnight exactly: the next day, with its date, as the browser's intervalEnd writes it.
    [InlineData("2026-11-21T23:00:00", "2026-11-22T00:00:00", "21/11/2026 23:00 – 22/11/2026 00:00")]
    // The night the clocks go back in Europe, and the one they go forward: UTC knows no change of hour, one day stays one day.
    [InlineData("2026-10-25T00:30:00", "2026-10-25T03:30:00", "25/10/2026 00:30 – 03:30")]
    [InlineData("2026-03-29T00:30:00", "2026-03-29T03:30:00", "29/03/2026 00:30 – 03:30")]
    public void AnIntervalWritesItsStartWholeAndItsEndAfterASpacedDash(string from, string to, string expected)
    {
        // The server's twin of the browser's intervalText (note 2026-11-22-l-intervallo-di-una-mail-nel-nucleo).
        Assert.Equal(expected, HubDates.Interval(Utc(from), Utc(to), DateOrder.DayMonthYear));
    }

    [Fact]
    public void AnIntervalWritesBothDaysInTheDivisionsOrder()
    {
        var from = Utc("2026-11-21T23:30:00");

        Assert.Equal("11/21/2026 23:30 – 11/22/2026 00:30", HubDates.Interval(from, from.AddHours(1), DateOrder.MonthDayYear));
        Assert.Equal("2026/11/21 23:30 – 2026/11/22 00:30", HubDates.Interval(from, from.AddHours(1), DateOrder.YearMonthDay));
        Assert.Equal("2026/11/21 23:30 – 23:45", HubDates.Interval(from, from.AddMinutes(15), DateOrder.YearMonthDay));
    }

    private static DateTime Utc(string moment) =>
        DateTime.SpecifyKind(DateTime.Parse(moment, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

    /// <summary>A division the validator accepts, with nothing said about dates.</summary>
    private static DivisionOptions Division() => new()
    {
        Code = "XX",
        CountryId = "XX",
        Name = new Dictionary<string, string> { ["en"] = "Example Division" },
        Domain = "example.org",
        Locales = ["en"],
        DefaultLocale = "en",
        Timezone = "UTC",
    };
}
