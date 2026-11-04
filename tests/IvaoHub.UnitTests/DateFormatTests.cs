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
