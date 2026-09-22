using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The comparison of an import with the legs of a tour (T8, design M2 §8.4): the same leg recognised by departure,
/// arrival and its place among the legs with the same pair; the order of the file; what "merge" and "replace" do with
/// the legs the file does not contain (§1.4.1, ADR-051 of Toursystem).
/// </summary>
public sealed class LegImportTests
{
    private static readonly IReadOnlySet<long> NoReports = new HashSet<long>();

    [Fact]
    public void TheSameFileTwiceChangesNothing()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"));
        var plan = LegImportPlan.Make(legs, File(("AAAA", "BBBB"), ("BBBB", "CCCC")), LegImportMode.Replace, NoReports);

        Assert.All(plan.Steps, step => Assert.Equal(LegImportOutcome.Unchanged, step.Outcome));
        Assert.Equal([1, 2], plan.Steps.Select(step => step.NumberAfter!.Value));
        Assert.False(plan.ReasonRequired);
    }

    [Fact]
    public void AnInsertedRowIsANewLegAndTheOthersAreStillTheSame()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"));
        var plan = LegImportPlan.Make(
            legs,
            File(("AAAA", "BBBB"), ("BBBB", "DDDD"), ("BBBB", "CCCC")),
            LegImportMode.Merge,
            NoReports);

        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Added, LegImportOutcome.Unchanged],
            plan.Steps.Select(step => step.Outcome));
        Assert.Equal(1, plan.Steps[1].Row);

        plan.Apply(DateTime.UtcNow, null);
        Assert.Equal(3, legs[1].Number);
    }

    [Fact]
    public void APairTwiceIsMatchedInOrder()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "AAAA"), ("AAAA", "BBBB"));
        var file = File(("AAAA", "BBBB"), ("BBBB", "AAAA"), ("AAAA", "BBBB"));
        file[2].Callsigns = ["XYZ123", "XYZ124"];

        var plan = LegImportPlan.Make(legs, file, LegImportMode.Replace, NoReports);

        Assert.Same(legs[0], plan.Steps[0].Leg);
        Assert.Same(legs[2], plan.Steps[2].Leg);
        Assert.Equal(LegImportOutcome.Unchanged, plan.Steps[0].Outcome);
        Assert.Equal(LegImportOutcome.Changed, plan.Steps[2].Outcome);
        Assert.Equal(["callsigns"], plan.Steps[2].Changes);
    }

    [Fact]
    public void MergeLeavesTheAbsentLegsAfterTheLegBeforeThem()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"), ("CCCC", "DDDD"));
        var plan = LegImportPlan.Make(legs, File(("AAAA", "BBBB"), ("CCCC", "DDDD")), LegImportMode.Merge, NoReports);

        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Kept, LegImportOutcome.Unchanged],
            plan.Steps.Select(step => step.Outcome));
        Assert.Same(legs[1], plan.Steps[1].Leg);
        Assert.Equal(2, plan.Steps[1].NumberAfter);
    }

    [Fact]
    public void AnAbsentFirstLegStaysFirst()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"));
        var plan = LegImportPlan.Make(legs, File(("BBBB", "CCCC")), LegImportMode.Merge, NoReports);

        Assert.Same(legs[0], plan.Steps[0].Leg);
        Assert.Equal(LegImportOutcome.Kept, plan.Steps[0].Outcome);
    }

    [Fact]
    public void ReplaceDeletesTheAbsentLegsWithoutReportsAndRetiresTheOthers()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"), ("CCCC", "DDDD"));
        var reports = new HashSet<long> { legs[2].Id };
        var plan = LegImportPlan.Make(legs, File(("AAAA", "BBBB")), LegImportMode.Replace, reports);

        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Retired, LegImportOutcome.Deleted],
            plan.Steps.Select(step => step.Outcome));
        Assert.Null(plan.Steps[2].NumberAfter);
        Assert.True(plan.ReasonRequired);

        var now = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        var (added, deleted) = plan.Apply(now, "The file of the new season");

        Assert.Empty(added);
        Assert.Equal([legs[1]], deleted);
        Assert.Equal(now, legs[2].RetiredAt);
        Assert.Equal("The file of the new season", legs[2].RetiredReason);
        Assert.Equal(2, legs[2].Number);
    }

    [Fact]
    public void ARetiredLegTheFileNamesComesBackWithTheReason()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"));
        legs[1].RetiredAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        legs[1].RetiredReason = "Closed airport";

        var plan = LegImportPlan.Make(legs, File(("AAAA", "BBBB"), ("BBBB", "CCCC")), LegImportMode.Merge, NoReports);

        Assert.Equal(LegImportOutcome.Restored, plan.Steps[1].Outcome);
        Assert.True(plan.ReasonRequired);
        Assert.True(plan.Restores);

        plan.Apply(DateTime.UtcNow, "Reopened");
        Assert.Null(legs[1].RetiredAt);
        Assert.Null(legs[1].RetiredReason);
        Assert.Equal("Reopened", legs[1].ChangeReason);
    }

    [Fact]
    public void ARetiredLegNotInTheFileStaysRetiredOnReplace()
    {
        var legs = Tour(("AAAA", "BBBB"), ("BBBB", "CCCC"));
        legs[1].RetiredAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = LegImportPlan.Make(legs, File(("AAAA", "BBBB")), LegImportMode.Replace, new HashSet<long> { legs[1].Id });

        Assert.Equal(LegImportOutcome.Kept, plan.Steps[1].Outcome);
        Assert.False(plan.ReasonRequired);
    }

    [Fact]
    public void AChangeOfALegWithReportsOwesAReasonAndKeepsItsGroups()
    {
        var legs = Tour(("AAAA", "BBBB"));
        legs[0].Aircraft = new AllowedAircraft(["A320"], [7]);
        var file = File(("AAAA", "BBBB"));
        file[0].Aircraft = new AllowedAircraft(["A20N", "A320"], []);

        var plan = LegImportPlan.Make(legs, file, LegImportMode.Merge, new HashSet<long> { legs[0].Id });

        Assert.Equal(LegImportOutcome.Changed, plan.Steps[0].Outcome);
        Assert.Equal(["aircraft"], plan.Steps[0].Changes);
        Assert.True(plan.ReasonRequired);

        plan.Apply(DateTime.UtcNow, "New fleet");
        Assert.Equal(["A20N", "A320"], legs[0].Aircraft.Types);
        Assert.Equal([7L], legs[0].Aircraft.GroupIds);
        Assert.Equal("New fleet", legs[0].ChangeReason);
    }

    [Fact]
    public void TheFingerprintMovesWithTheLegs()
    {
        var legs = Tour(("AAAA", "BBBB"));
        var before = LegImportPlan.Fingerprint(legs);

        Assert.Equal(before, LegImportPlan.Fingerprint(Tour(("AAAA", "BBBB"))));

        legs[0].RowVersion = legs[0].RowVersion.AddSeconds(1);
        Assert.NotEqual(before, LegImportPlan.Fingerprint(legs));
    }

    private static List<Leg> Tour(params (string From, string To)[] pairs) =>
    [
        .. pairs.Select((pair, index) => new Leg
        {
            Id = 100 + index,
            Number = index + 1,
            DepartureIcao = pair.From,
            ArrivalIcao = pair.To,
            RowVersion = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        }),
    ];

    private static List<Leg> File(params (string From, string To)[] pairs) =>
        [.. pairs.Select(pair => new Leg { DepartureIcao = pair.From, ArrivalIcao = pair.To })];
}
