using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the ATC of an event that need no database (M4, E11a; design §4.2; note 2026-10-12-le-postazioni-e-la-disponibilita): when
/// the availabilities close and who may give one, what a window is, when some windows cover a stretch of hours, and what «Duplicate»
/// carries of the positions.
/// </summary>
public sealed class EventsAtcRulesTests
{
    private static readonly DateTime Starts = new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

    private static readonly Event Row = new() { HasRoster = true, StartsAtUtc = Starts, EndsAtUtc = Starts.AddHours(6) };

    [Fact]
    public void TheAvailabilitiesCloseTheDaysOfTheSettingBeforeTheStartOfAnEventWithARosterAndAPosition()
    {
        var settings = new EventsSettings { ApplicationsCloseDays = 3 };
        var closes = Starts.AddDays(-3);
        Assert.Equal(closes, AtcRules.ClosesAt(Row, settings));

        Assert.True(AtcRules.IsOpen(Row, settings, hasPositions: true, closes.AddTicks(-1)));
        Assert.False(AtcRules.IsOpen(Row, settings, hasPositions: true, closes));
        Assert.False(AtcRules.IsOpen(Row, settings, hasPositions: false, closes.AddDays(-1)));
        Assert.False(AtcRules.IsOpen(new Event { StartsAtUtc = Starts, EndsAtUtc = Starts.AddHours(6) }, settings, hasPositions: true, closes.AddDays(-1)));

        // A cancelled event takes none, and lets its controllers withdraw until the close.
        var cancelled = new Event { HasRoster = true, StartsAtUtc = Starts, EndsAtUtc = Starts.AddHours(6), CancelledAt = closes.AddDays(-2) };
        Assert.False(AtcRules.IsOpen(cancelled, settings, hasPositions: true, closes.AddDays(-1)));
        Assert.True(AtcRules.IsWithdrawable(cancelled, settings, closes.AddDays(-1)));
        Assert.False(AtcRules.IsWithdrawable(cancelled, settings, closes));
    }

    [Fact]
    public void TheLeastRatingIsAskedOfTheVocabularyAndNoneLetsEveryMemberIn()
    {
        var ladder = IvaoRatings.Vocabulary.Ladder(RatingKind.Atc);
        var least = new EventsSettings { MinimumAtcRating = ladder[2].Number };

        Assert.True(AtcRules.IsRatedEnough(IvaoRatings.Vocabulary, least, ladder[2].Number));
        Assert.True(AtcRules.IsRatedEnough(IvaoRatings.Vocabulary, least, ladder[^1].Number));
        Assert.False(AtcRules.IsRatedEnough(IvaoRatings.Vocabulary, least, ladder[1].Number));
        Assert.False(AtcRules.IsRatedEnough(IvaoRatings.Vocabulary, least, null));

        Assert.True(AtcRules.IsRatedEnough(IvaoRatings.Vocabulary, new EventsSettings(), null));
    }

    [Fact]
    public void AWindowEndsAfterItStartsInsideTheEventAndMeetsNoOther()
    {
        // Right: back in their order of time; two that touch do not meet.
        var problems = new Refusals();
        var sorted = AtcRules.Check(Row, [Window(3, 6), Window(0, 3)], problems);
        Assert.True(problems.IsEmpty);
        Assert.Equal([Window(0, 3), Window(3, 6)], sorted);

        problems = new Refusals();
        Assert.Empty(AtcRules.Check(Row, [Window(2, 1), Window(-1, 1), Window(1, 4), Window(3, 5), Window(5, 7)], problems));
        Assert.Equal(
            new Dictionary<string, string[]>
            {
                ["windows[0].toUtc"] = [AtcRules.EndsBeforeItStartsKey],
                ["windows[1].fromUtc"] = [AtcRules.OutsideEventKey],
                ["windows[2].fromUtc"] = ["events:errors.windowsOverlap"],
                ["windows[3].fromUtc"] = ["events:errors.windowsOverlap"],
                ["windows[4].fromUtc"] = [AtcRules.OutsideEventKey],
            },
            problems.Errors.ToDictionary(entry => entry.Key, entry => entry.Value));

        // None, or more than the most, refused on the list.
        problems = new Refusals();
        AtcRules.Check(Row, [], problems);
        Assert.Equal(["errors.required"], problems.Errors["windows"]);

        problems = new Refusals();
        AtcRules.Check(Row, [.. Enumerable.Range(0, EventAtcAvailability.MaxWindows + 1).Select(_ => Window(0, 1))], problems);
        Assert.Equal(["events:errors.tooManyWindows"], problems.Errors["windows"]);
    }

    [Fact]
    public void WindowsCoverAStretchWhenTheyReachItsEndWithNoGap()
    {
        Assert.True(AtcRules.Cover([Window(0, 2)], Hour(0), Hour(2)));
        Assert.True(AtcRules.Cover([Window(1, 3), Window(0, 1)], Hour(0), Hour(3)));
        Assert.True(AtcRules.Cover([Window(0, 2), Window(1, 4)], Hour(1), Hour(3)));
        Assert.False(AtcRules.Cover([Window(0, 1), Window(2, 3)], Hour(0), Hour(3)));
        Assert.False(AtcRules.Cover([Window(1, 3)], Hour(0), Hour(3)));
        Assert.False(AtcRules.Cover([], Hour(0), Hour(1)));

        Assert.True(AtcRules.Meet(Hour(0), Hour(2), Hour(1), Hour(3)));
        Assert.False(AtcRules.Meet(Hour(0), Hour(1), Hour(1), Hour(2)));
    }

    [Fact]
    public void DuplicateCopiesThePositionsMovedAndAsksTheirArea()
    {
        var source = new EventAtcPosition
        {
            EventId = 1,
            Callsign = "XXAA_TWR",
            Fir = "XXAA",
            FromUtc = Starts,
            ToUtc = Starts.AddHours(2),
            Note = "the first two hours",
        };
        var copy = new Event { Id = 9, OwnerDepartment = Department.ED, OwnerDepartmentMask = 5 };

        var copied = EventCopy.AtcPosition(source, copy, TimeSpan.FromDays(7));
        Assert.Equal(
            (9L, "XXAA_TWR", "XXAA", Starts.AddDays(7), Starts.AddDays(7).AddHours(2), "the first two hours", Department.ED, 5),
            (copied.EventId, copied.Callsign, copied.Fir, copied.FromUtc, copied.ToUtc, copied.Note, copied.OwnerDepartment, copied.OwnerDepartmentMask));

        Assert.Equal([EventsPermissions.AtcEdit], new EventDuplicatePlan(copy, TimeSpan.Zero, [], [], [], GeneratesPrivateSlots: false, [source]).AreasWritten);
        Assert.Empty(new EventDuplicatePlan(copy, TimeSpan.Zero, [], [], [], GeneratesPrivateSlots: false, []).AreasWritten);
    }

    private static DateTime Hour(int hours) => Starts.AddHours(hours);

    private static AtcWindowDto Window(int from, int to) => new(Hour(from), Hour(to));
}
