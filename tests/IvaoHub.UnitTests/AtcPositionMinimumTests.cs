using System.Globalization;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The minimum of a position for the window of a shift (M4, E10c; the maintainer's answer on #204): the highest minimum among
/// the FRAs that hold at some moment of it, so that nobody is proposed where IVAO would refuse them for part of the shift. The
/// rules are the FRAs IVAO had for two positions of the bench on 30 September 2026 (<c>tests/fixtures/ivao/fras-IT.json</c>),
/// and a few written for an edge. 7 October 2026 is a Wednesday, 10 October a Saturday.
/// </summary>
public sealed class AtcPositionMinimumTests
{
    private static readonly int AllWeek = Enum.GetValues<DayOfWeek>().Sum(IvaoFraReader.DayBit);

    private static readonly int Weekdays = Days(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);

    private static readonly int Weekend = Days(DayOfWeek.Saturday, DayOfWeek.Sunday);

    /// <summary>Bari's tower: AS2 from 08 to 23, ADC from 23 to 08.</summary>
    private static readonly AtcPositionMinimum BariTower = new(
    [
        new(3, AllWeek, new TimeOnly(8, 0), new TimeOnly(23, 0), null),
        new(5, AllWeek, new TimeOnly(23, 0), new TimeOnly(8, 0), null),
    ]);

    /// <summary>An approach of Malpensa: ADC until 17 on weekdays and until 12 at the weekend, APC after, and APC at night.</summary>
    private static readonly AtcPositionMinimum MalpensaApproach = new(
    [
        new(6, AllWeek, new TimeOnly(23, 0), new TimeOnly(8, 0), null),
        new(5, Weekend, new TimeOnly(8, 0), new TimeOnly(12, 0), null),
        new(5, Weekdays, new TimeOnly(8, 0), new TimeOnly(17, 0), null),
        new(6, Weekdays, new TimeOnly(17, 0), new TimeOnly(23, 0), null),
        new(6, Weekend, new TimeOnly(12, 0), new TimeOnly(23, 0), null),
    ]);

    [Theory]
    [InlineData("2026-10-07 10:00", "2026-10-07 11:00", 3)] // by day
    [InlineData("2026-10-07 23:00", "2026-10-08 00:00", 5)] // by night
    [InlineData("2026-10-08 02:00", "2026-10-08 03:00", 5)] // the night that began the day before
    [InlineData("2026-10-07 22:30", "2026-10-07 23:30", 5)] // a shift across the change: the higher of the two
    [InlineData("2026-10-07 07:00", "2026-10-07 09:00", 5)]
    [InlineData("2026-10-07 22:00", "2026-10-07 23:00", 3)] // it ends when the night begins
    [InlineData("2026-10-07 08:00", "2026-10-07 09:00", 3)] // it begins when the night ends
    [InlineData("2026-10-07 10:00", "2026-10-07 10:00", 3)] // a moment
    [InlineData("2026-10-07 10:00", "2026-10-08 10:00", 5)] // a whole day and night
    public void ADayAndANight(string from, string to, int expected)
    {
        Assert.Equal(expected, BariTower.Over(At(from), At(to)));
    }

    [Theory]
    [InlineData("2026-10-07 16:00", "2026-10-07 17:00", 5)] // a Wednesday afternoon
    [InlineData("2026-10-07 16:30", "2026-10-07 17:30", 6)]
    [InlineData("2026-10-09 16:00", "2026-10-09 17:00", 5)] // a Friday
    [InlineData("2026-10-10 11:00", "2026-10-10 12:00", 5)] // a Saturday morning
    [InlineData("2026-10-10 12:00", "2026-10-10 13:00", 6)]
    [InlineData("2026-10-10 16:00", "2026-10-10 17:00", 6)] // the weekend's afternoon is the weekdays' evening
    [InlineData("2026-10-11 11:00", "2026-10-11 12:00", 5)] // a Sunday morning
    [InlineData("2026-10-09 22:00", "2026-10-10 01:00", 6)]
    public void TheWeekdaysAndTheWeekend(string from, string to, int expected)
    {
        Assert.Equal(expected, MalpensaApproach.Over(At(from), At(to)));
    }

    [Theory]
    [InlineData("2026-10-10 01:00", "2026-10-10 01:30", 7)] // Friday's night, on Saturday
    [InlineData("2026-10-11 01:00", "2026-10-11 01:30", null)] // Saturday's night: not a Friday
    [InlineData("2026-10-09 22:00", "2026-10-09 22:30", null)] // Friday, before it starts
    [InlineData("2026-10-08 23:30", "2026-10-09 00:30", null)] // Thursday's night
    public void AnFraOfOneWeekdayRunsPastMidnightIntoTheNext(string from, string to, int? expected)
    {
        var fridayNight = new AtcPositionMinimum([new(7, Days(DayOfWeek.Friday), new TimeOnly(23, 0), new TimeOnly(2, 0), null)]);

        Assert.Equal(expected, fridayNight.Over(At(from), At(to)));
    }

    [Theory]
    [InlineData("2026-09-07 19:00", "2026-09-07 20:00", 10)] // the evening of the date: the position closed but to a CAI
    [InlineData("2026-09-07 17:00", "2026-09-07 18:00", 5)]
    [InlineData("2026-09-08 19:00", "2026-09-08 20:00", 5)] // the day after
    [InlineData("2026-09-07 21:00", "2026-09-07 22:00", 10)]
    public void AnFraOfOneDateHoldsOnThatDateOnly(string from, string to, int expected)
    {
        var closedForAnEvening = new AtcPositionMinimum(
        [
            new(5, AllWeek, new TimeOnly(0, 0), new TimeOnly(0, 0), null),
            new(10, 0, new TimeOnly(18, 30), new TimeOnly(21, 30), new DateOnly(2026, 9, 7)),
        ]);

        Assert.Equal(expected, closedForAnEvening.Over(At(from), At(to)));
    }

    [Theory]
    [InlineData("2026-09-08 00:30", "2026-09-08 00:45", 10)]
    [InlineData("2026-09-08 01:00", "2026-09-08 02:00", null)]
    [InlineData("2026-09-06 23:30", "2026-09-07 00:30", null)] // the night before the date
    public void AnFraOfOneDateRunsPastItsMidnightToo(string from, string to, int? expected)
    {
        var lateEvening = new AtcPositionMinimum([new(10, 0, new TimeOnly(23, 0), new TimeOnly(1, 0), new DateOnly(2026, 9, 7))]);

        Assert.Equal(expected, lateEvening.Over(At(from), At(to)));
    }

    [Fact]
    public void WithoutAnFraThatHoldsThereIsNoMinimum()
    {
        var from = At("2026-10-07 10:00");
        var to = At("2026-10-07 11:00");

        Assert.Null(AtcPositionMinimum.None.Over(from, to));
        Assert.Null(new AtcPositionMinimum([]).Over(from, to));

        // No day and no date: an FRA that names no moment holds at none.
        Assert.Null(new AtcPositionMinimum([new(5, 0, new TimeOnly(0, 0), new TimeOnly(0, 0), null)]).Over(from, to));

        // 00:00 to 00:00 is the whole day, every day it names.
        Assert.Equal(6, new AtcPositionMinimum([new(6, AllWeek, new TimeOnly(0, 0), new TimeOnly(0, 0), null)]).Over(from, to));
    }

    [Theory]
    [InlineData("2026-10-07 10:00", "2026-10-07 11:00", true)] // by day an AS3 may open Bari's tower
    [InlineData("2026-10-07 23:00", "2026-10-08 00:00", false)] // not by night
    public void TheVocabularySaysWhoIsAtLeastTheMinimum(string from, string to, bool anAs3May)
    {
        var minimum = BariTower.Over(At(from), At(to));

        Assert.NotNull(minimum);
        Assert.Equal(anAs3May, IvaoRatings.Vocabulary.IsAtLeast(RatingKind.Atc, 4, minimum.Value));
    }

    [Fact]
    public void TheWarningBelowTheMinimumHasItsWordsInEveryLanguage()
    {
        // The maintainer's addition on #204: whoever builds the shifts may go below the FRA, and is told to lift it on IVAO
        // for that controller. The words name IVAO, so they are the core's, and a module shows them as they are.
        var catalog = new LocaleCatalog(
            HubPaths.Resolve(AppContext.BaseDirectory),
            Options.Create(new DivisionOptions
            {
                Code = "IT",
                CountryId = "IT",
                Domain = "it.ivao.aero",
                Timezone = "Europe/Rome",
                Locales = ["it", "en"],
                DefaultLocale = "it",
            }));

        foreach (var locale in new[] { "it", "en" })
        {
            Assert.Contains("FRA", catalog.Get(locale, AtcPositionMinimum.BelowMinimumKey), StringComparison.Ordinal);
        }
    }

    private static int Days(params DayOfWeek[] days) => days.Sum(IvaoFraReader.DayBit);

    private static DateTime At(string text) =>
        DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
