using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The proposal of the roster on data of its own (M4, E11b; design §4.3, §4.5; note 2026-10-13-la-proposta-del-roster, readings 2–4 and 6):
/// who can take a shift, who comes first — the preferred rating and who falls back below it, the experience, the penalty, fewer shifts, the
/// availability given earlier —, the shifts spread, the breaks after the shifts in a row, the same roster from the same data, and the why
/// of every shift; and on the staff's roster the warnings of a shift and the uncovered pieces. On a vocabulary of ratings that is not the
/// network's (design M3 §10): the module names none.
/// </summary>
public sealed class EventsRosterTests
{
    private const string Tower = "XXAA_TWR";
    private const string Approach = "XXAA_APP";

    private static readonly DateTime Starts = new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

    /// <summary>Four ratings lowest first, and the third first on the kind «TWX»: nothing of the network's.</summary>
    private static readonly RatingVocabulary Vocabulary = new(
        [
            new Rating(RatingKind.Atc, 11, "Q1", false, null),
            new Rating(RatingKind.Atc, 12, "Q2", false, null),
            new Rating(RatingKind.Atc, 13, "Q3", true, "TWX"),
            new Rating(RatingKind.Atc, 14, "Q4", true, "APX"),
        ],
        new Dictionary<string, string> { ["TWX"] = "Q3", ["APX"] = "Q4" });

    private static readonly RosterRules Rules = new(ShiftMinutes: 60, MinimumAtcRating: 12, MaxConsecutiveShifts: 2, BreakShifts: 1);

    [Fact]
    public void APositionIsCutInPiecesOfTheShiftAndTheLastOneEndsWhereItCloses()
    {
        var pieces = RosterProposer.Pieces(Starts, Starts.AddMinutes(150), 60);

        Assert.Equal(
            [(Starts, Starts.AddHours(1)), (Starts.AddHours(1), Starts.AddHours(2)), (Starts.AddHours(2), Starts.AddMinutes(150))],
            pieces);
    }

    [Fact]
    public void WhoCanTakeAShiftIsAvailableForAllOfItAndRatedAtLeastTheSettingAndThePositionsMinimum()
    {
        // The position's FRA asks Q3 from 19:00: one rated Q2 takes the first two hours, not the third.
        var minimum = new AtcPositionMinimum([new AtcPositionRule(13, Days: 0, new TimeOnly(19, 0), new TimeOnly(23, 0), DateOnly.FromDateTime(Starts))]);
        var tower = Position(1, Tower, "TWX", hours: 3, minimum);
        var all = Windows((Starts, Starts.AddHours(3)));

        Assert.True(RosterProposer.CanTake(12, all, tower, Starts, Starts.AddHours(1), Rules, Vocabulary));
        Assert.False(RosterProposer.CanTake(12, all, tower, Starts.AddHours(2), Starts.AddHours(3), Rules, Vocabulary));
        Assert.True(RosterProposer.CanTake(13, all, tower, Starts.AddHours(2), Starts.AddHours(3), Rules, Vocabulary));

        // Below the setting's least, or with no rating at all, never; and available for a part of it only, never.
        Assert.False(RosterProposer.CanTake(11, all, tower, Starts, Starts.AddHours(1), Rules, Vocabulary));
        Assert.False(RosterProposer.CanTake(null, all, tower, Starts, Starts.AddHours(1), Rules, Vocabulary));
        Assert.False(RosterProposer.CanTake(13, Windows((Starts, Starts.AddMinutes(30))), tower, Starts, Starts.AddHours(1), Rules, Vocabulary));

        // Two windows that touch cover the hour between them.
        Assert.True(RosterProposer.CanTake(13, Windows((Starts, Starts.AddMinutes(30)), (Starts.AddMinutes(30), Starts.AddHours(1))), tower, Starts, Starts.AddHours(1), Rules, Vocabulary));
    }

    [Fact]
    public void ThePreferredRatingComesFirstAndTheOthersOnlyWhenThereIsNobody()
    {
        var tower = Position(1, Tower, "TWX", hours: 2);

        // A Q2 who gave first, and a Q3 later: the Q3 holds the preferred rating of the tower, and takes both hours.
        var proposed = Propose([tower], [Candidate(1, 12, since: 0), Candidate(2, 13, since: 1)], Rules with { MaxConsecutiveShifts = 4 });
        Assert.All(proposed, shift => Assert.Equal(2, shift.Vid));
        Assert.All(proposed, shift => Assert.Equal(ShiftCriterion.Preferred, shift.Reason!.DecidedBy));
        Assert.All(proposed, shift => Assert.True(shift.Reason!.Preferred));

        // Nobody preferred: the Q2 has it, the only one who could.
        var fallback = Propose([tower], [Candidate(1, 12, since: 0)], Rules);
        Assert.All(fallback, shift => Assert.Equal(1, shift.Vid));
        Assert.All(fallback, shift => Assert.Equal(ShiftCriterion.Only, shift.Reason!.DecidedBy));
        Assert.All(fallback, shift => Assert.False(shift.Reason!.Preferred));

        // A higher rating is at least the preferred one too.
        Assert.True(RosterProposer.IsPreferred(14, tower, Vocabulary));
    }

    [Fact]
    public void TheExperienceOnThePositionThenOnItsKindComesAfterTheRatingAndOnlyWhenEverybodysIsKnown()
    {
        var tower = Position(1, Tower, "TWX", hours: 1);
        var onTheTower = new RosterExperience(Counts((Tower, 4)), Counts(("TWX", 4)));
        var onTowers = new RosterExperience(Counts(("XXBB_TWR", 9)), Counts(("TWX", 9)));

        // The one who opened this very tower beats the one who opened more towers elsewhere, who beats nobody.
        var proposed = Assert.Single(Propose(
            [tower],
            [Candidate(1, 13, since: 0, RosterExperience.None), Candidate(2, 13, since: 1, onTowers), Candidate(3, 13, since: 2, onTheTower)],
            Rules));
        Assert.Equal(3, proposed.Vid);
        Assert.Equal(ShiftCriterion.Experience, proposed.Reason!.DecidedBy);
        Assert.Equal(4, proposed.Reason.CallsignConnections);
        Assert.Equal(4, proposed.Reason.TypeConnections);

        var byKind = Assert.Single(Propose([tower], [Candidate(1, 13, since: 0, RosterExperience.None), Candidate(2, 13, since: 1, onTowers)], Rules));
        Assert.Equal(2, byKind.Vid);
        Assert.Equal(ShiftCriterion.Experience, byKind.Reason!.DecidedBy);

        // One whose history the archive did not give: the criterion is left out for everybody, and the earlier availability decides.
        var unknown = Assert.Single(Propose([tower], [Unknown(1, 13, since: 0), Candidate(3, 13, since: 2, onTheTower)], Rules));
        Assert.Equal(1, unknown.Vid);
        Assert.Equal(ShiftCriterion.EarlierAvailability, unknown.Reason!.DecidedBy);
        Assert.Null(unknown.Reason.CallsignConnections);
        Assert.Null(unknown.Reason.TypeConnections);
    }

    [Fact]
    public void TheLowerPenaltyThenFewerShiftsThenTheEarlierAvailabilityThenTheVidDecide()
    {
        // 0.08 and 0.25 (§4.5): one no-show in ten events, and in two.
        Assert.Equal(1.0 / 12, RosterProposer.Penalty(noShows: 1, attended: 9), precision: 10);
        Assert.Equal(0.25, RosterProposer.Penalty(noShows: 1, attended: 1));
        Assert.Equal(0, RosterProposer.Penalty(noShows: 0, attended: 0));

        var tower = Position(1, Tower, "TWX", hours: 1);
        var penalty = Assert.Single(Propose([tower], [Candidate(1, 13, since: 0, penalty: 0.25), Candidate(2, 13, since: 1, penalty: 0.08)], Rules));
        Assert.Equal(2, penalty.Vid);
        Assert.Equal(ShiftCriterion.Penalty, penalty.Reason!.DecidedBy);
        Assert.Equal(0.08, penalty.Reason.Penalty);

        var earlier = Assert.Single(Propose([tower], [Candidate(2, 13, since: 1), Candidate(1, 13, since: 0)], Rules));
        Assert.Equal(1, earlier.Vid);
        Assert.Equal(ShiftCriterion.EarlierAvailability, earlier.Reason!.DecidedBy);

        var vid = Assert.Single(Propose([tower], [Candidate(7, 13, since: 0), Candidate(5, 13, since: 0)], Rules));
        Assert.Equal(5, vid.Vid);
        Assert.Equal(ShiftCriterion.Vid, vid.Reason!.DecidedBy);
    }

    [Fact]
    public void TheShiftsSpreadAndTheOnesFewerCanTakeAreFilledFirst()
    {
        // Two positions at the same hour, two controllers: the approach's FRA asks Q4, so one of them alone can take it, and it is filled
        // first — its Q4 is on it —; the tower, which both could take, goes to the other.
        var tower = Position(1, Tower, "TWX", hours: 1);
        var approach = Position(2, Approach, "APX", hours: 1, minimum: new AtcPositionMinimum([new AtcPositionRule(14, Days: 0, new TimeOnly(0, 0), new TimeOnly(0, 0), DateOnly.FromDateTime(Starts))]));
        var proposed = Propose([tower, approach], [Candidate(1, 14, since: 0), Candidate(2, 13, since: 1)], Rules);

        Assert.Equal(1, proposed.Single(shift => shift.PositionId == 2).Vid);
        Assert.Equal(2, proposed.Single(shift => shift.PositionId == 1).Vid);

        // Three hours of a tower and two equal controllers: one shift each before anybody has two — fewer shifts decides.
        var evening = Propose([Position(1, Tower, "TWX", hours: 3)], [Candidate(1, 13, since: 0), Candidate(2, 13, since: 1)], Rules with { MaxConsecutiveShifts = 4 });
        Assert.Equal([1, 2, 1], evening.Select(shift => shift.Vid));
        Assert.Equal(ShiftCriterion.FewerShifts, evening[1].Reason!.DecidedBy);
    }

    [Fact]
    public void AfterTheShiftsInARowComesTheBreakAndAShiftAtTheSameHourIsNeverProposed()
    {
        // One controller, four hours of a tower: two in a row, one of break, one more.
        var proposed = Propose([Position(1, Tower, "TWX", hours: 4)], [Candidate(1, 13, since: 0)], Rules);
        Assert.Equal([1, 1, null, 1], proposed.Select(shift => shift.Vid));
        Assert.Equal(1, proposed[2].Candidates);

        // The run: a pause shorter than the break is no break; no break at all is no limit.
        (DateTime, DateTime) Hour(int at) => (Starts.AddHours(at), Starts.AddHours(at + 1));
        Assert.Equal(3, RosterProposer.LongestRun([Hour(0), Hour(1), (Starts.AddMinutes(150), Starts.AddMinutes(210))], Rules, Hour(1)));
        Assert.Equal(1, RosterProposer.LongestRun([Hour(0), Hour(2)], Rules, Hour(2)));
        Assert.Equal(1, RosterProposer.LongestRun([Hour(0), Hour(1), Hour(2)], Rules with { BreakShifts = 0 }, Hour(1)));

        // Two positions at the same hour and one controller: one of them, the other uncovered.
        var both = Propose([Position(1, Tower, "TWX", hours: 1), Position(2, Approach, "APX", hours: 1)], [Candidate(1, 14, since: 0)], Rules);
        Assert.Single(both, shift => shift.Vid == 1);
        Assert.Single(both, shift => shift.Vid is null);
    }

    [Fact]
    public void TheSameDataGiveTheSameRosterWhateverTheirOrderAndAShiftHeldIsLeftAlone()
    {
        var positions = new[] { Position(1, Tower, "TWX", hours: 3), Position(2, Approach, "APX", hours: 3) };
        var candidates = new[] { Candidate(1, 14, since: 0), Candidate(2, 13, since: 1), Candidate(3, 13, since: 2), Candidate(4, 12, since: 3) };

        var once = Propose(positions, candidates, Rules);
        var again = Propose([.. positions.Reverse()], [.. candidates.Reverse()], Rules);
        Assert.Equal(once, again, (left, right) =>
            left.PositionId == right.PositionId && left.FromUtc == right.FromUtc && left.Vid == right.Vid && left.Reason == right.Reason);

        // A shift somebody holds: its piece is not proposed again, and it counts for its controller.
        var held = RosterProposer.Propose(positions, candidates, [new RosterHeld(1, 1, Starts, Starts.AddHours(1))], Rules, Vocabulary);
        Assert.DoesNotContain(held, shift => shift.PositionId == 1 && shift.FromUtc == Starts);
        Assert.DoesNotContain(held, shift => shift.Vid == 1 && shift.FromUtc == Starts);
    }

    [Fact]
    public void TheWarningsOfAShiftAreTheRulesItDoesNotKeep()
    {
        var minimum = new AtcPositionMinimum([new AtcPositionRule(14, Days: 0, new TimeOnly(0, 0), new TimeOnly(0, 0), DateOnly.FromDateTime(Starts))]);
        var tower = Position(1, Tower, "TWX", hours: 4, minimum);
        var first = Shift(1, vid: 5, at: 0);

        // Never available, rated below the setting, the position's FRA and the preferred one.
        Assert.Equal(
            [ShiftWarning.NotAvailable, ShiftWarning.BelowMinimum, ShiftWarning.BelowPositionMinimum, ShiftWarning.BelowPreferred],
            StaffRoster.Warnings(first, 11, [], tower, [first], Rules, Vocabulary));

        // Available and rated: nothing to say.
        Assert.Empty(StaffRoster.Warnings(first, 14, Windows((Starts, Starts.AddHours(4))), tower, [first], Rules, Vocabulary));

        // Three in a row, and another at the same hour.
        var roster = new[] { first, Shift(2, vid: 5, at: 1), Shift(3, vid: 5, at: 2) };
        Assert.Equal([ShiftWarning.TooManyInARow], StaffRoster.Warnings(roster[2], 14, Windows((Starts, Starts.AddHours(4))), tower, roster, Rules, Vocabulary));
        var twice = Shift(4, vid: 5, at: 0);
        Assert.Equal([ShiftWarning.Overlapping], StaffRoster.Warnings(twice, 14, Windows((Starts, Starts.AddHours(4))), tower, [first, twice], Rules, Vocabulary));
    }

    [Fact]
    public void AnUncoveredPieceIsWhatNoShiftOfItsPositionMeets()
    {
        var piece = (Starts, Starts.AddHours(1));
        Assert.Equal([piece], StaffRoster.Gaps(Starts, Starts.AddHours(1), []));
        Assert.Empty(StaffRoster.Gaps(Starts, Starts.AddHours(1), [Shift(1, vid: 5, at: 0)]));

        // A shift of the staff on half of it: the other half.
        var half = new EventAtcShift { Id = 2, ControllerVid = 5, FromUtc = Starts, ToUtc = Starts.AddMinutes(30) };
        Assert.Equal([(Starts.AddMinutes(30), Starts.AddHours(1))], StaffRoster.Gaps(Starts, Starts.AddHours(1), [half]));
    }

    private static IReadOnlyList<ProposedShift> Propose(IReadOnlyList<RosterPosition> positions, IReadOnlyList<RosterCandidate> candidates, RosterRules rules) =>
        RosterProposer.Propose(positions, candidates, [], rules, Vocabulary);

    private static RosterPosition Position(long id, string callsign, string type, int hours, AtcPositionMinimum? minimum = null) =>
        new(id, callsign, type, Starts, Starts.AddHours(hours), minimum ?? AtcPositionMinimum.None, Vocabulary.PreferredFor(type));

    /// <summary>A controller available the whole evening, who gave it <paramref name="since"/> minutes after the first.</summary>
    private static RosterCandidate Candidate(int vid, int? rating, int since, RosterExperience? experience = null, double penalty = 0) =>
        new(vid, rating, Windows((Starts, Starts.AddHours(6))), Starts.AddDays(-10).AddMinutes(since), experience ?? RosterExperience.None, penalty);

    /// <summary>The same, whose history the archive did not give.</summary>
    private static RosterCandidate Unknown(int vid, int? rating, int since) =>
        new(vid, rating, Windows((Starts, Starts.AddHours(6))), Starts.AddDays(-10).AddMinutes(since), Experience: null, Penalty: 0);

    private static EventAtcShift Shift(long id, int vid, int at) =>
        new() { Id = id, PositionId = 1, ControllerVid = vid, FromUtc = Starts.AddHours(at), ToUtc = Starts.AddHours(at + 1) };

    private static AtcWindowDto[] Windows(params (DateTime From, DateTime To)[] windows) =>
        [.. windows.Select(window => new AtcWindowDto(window.From, window.To))];

    private static Dictionary<string, int> Counts(params (string Key, int Count)[] counts) =>
        counts.ToDictionary(count => count.Key, count => count.Count, StringComparer.OrdinalIgnoreCase);
}
