using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The comparison of a hub tour's file with its hubs, rotations and legs (note 2026-10-28-hub-e-rotazioni-da-file): a
/// rotation recognised by its hub and its legs in order, matched, kept, deleted or retired whole; a connection by its
/// departure and arrival; the order of the file; the hubs added, kept and — on "replace" — deleted.
/// </summary>
public sealed class HubImportTests
{
    private static readonly IReadOnlySet<long> NoReports = new HashSet<long>();

    [Fact]
    public void AnEmptyHubTourGetsItsHubsAndRotationsInTheOrderOfTheFile()
    {
        var plan = HubImportPlan.Make(
            [],
            [],
            [],
            File(
                ("AAAA", "1", "AAAA", "BBBB"),
                ("AAAA", "1", "BBBB", "AAAA"),
                (null, null, "AAAA", "CCCC"),
                ("CCCC", "x", "CCCC", "DDDD"),
                ("CCCC", "x", "DDDD", "CCCC"),
                ("AAAA", "2", "AAAA", "EEEE"),
                ("AAAA", "2", "EEEE", "AAAA")),
            LegImportMode.Merge,
            NoReports);

        Assert.All(plan.Steps, step => Assert.Equal(LegImportOutcome.Added, step.Outcome));
        Assert.Equal(["AAAA", "CCCC"], plan.Hubs.Select(hub => hub.Icao));
        Assert.Equal([1, 2], plan.Hubs.Select(hub => hub.Sort));
        Assert.Equal(["AAAA", "CCCC"], plan.HubsAdded);

        // Three rotations — the hub's first, its second, the other hub's first — and one connection between them.
        var lines = plan.Lines();
        Assert.Equal([1, 1, null, 1, 1, 2, 2], lines.Select(line => line.Rotation));
        Assert.Equal(["AAAA", "AAAA", null, "CCCC", "CCCC", "AAAA", "AAAA"], lines.Select(line => line.Hub));
        Assert.Equal([2, 2, 2], plan.Units.Where(unit => unit.Rotation is not null).Select(unit => unit.Rotation!.Size));

        plan.Apply(DateTime.UtcNow, reason: null);
        Assert.Equal(LegKind.HubConnection, plan.Steps[2].Leg.Kind);
        Assert.All(plan.Steps.Where(step => step.Row != 2), step => Assert.Equal(LegKind.Normal, step.Leg.Kind));
    }

    [Fact]
    public void TheSameFileTwiceChangesNothing()
    {
        var (hubs, rotations, legs) = Shape();
        var plan = HubImportPlan.Make(hubs, rotations, legs, SameFile(), LegImportMode.Replace, NoReports);

        Assert.All(plan.Steps, step => Assert.Equal(LegImportOutcome.Unchanged, step.Outcome));
        Assert.Empty(plan.HubsAdded);
        Assert.Empty(plan.DeletedHubs);
        Assert.Empty(plan.DeletedRotations);
        Assert.False(plan.ReasonRequired);
    }

    [Fact]
    public void ARotationWithOtherLegsIsANewRotationAndTheOldOneGoesOnReplace()
    {
        var (hubs, rotations, legs) = Shape();
        var file = File(
            ("AAAA", "1", "AAAA", "BBBB"),
            ("AAAA", "1", "BBBB", "AAAA"),
            ("AAAA", "2", "AAAA", "FFFF"),
            ("AAAA", "2", "FFFF", "AAAA"));

        var merge = HubImportPlan.Make(hubs, rotations, legs, file, LegImportMode.Merge, NoReports);
        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Unchanged, LegImportOutcome.Kept, LegImportOutcome.Kept,
                LegImportOutcome.Added, LegImportOutcome.Added],
            merge.Steps.Select(step => step.Outcome));

        var replace = HubImportPlan.Make(hubs, rotations, legs, file, LegImportMode.Replace, NoReports);
        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Unchanged, LegImportOutcome.Added, LegImportOutcome.Added,
                LegImportOutcome.Deleted, LegImportOutcome.Deleted],
            replace.Steps.Select(step => step.Outcome));
        Assert.Equal([rotations[1]], replace.DeletedRotations);
        Assert.Equal([1, 2, 3, 4], replace.Steps.Take(4).Select(step => step.NumberAfter!.Value));
    }

    [Fact]
    public void ARotationWithReportsIsRetiredWholeOnReplace()
    {
        var (hubs, rotations, legs) = Shape();
        var file = File(("AAAA", "1", "AAAA", "BBBB"), ("AAAA", "1", "BBBB", "AAAA"));

        // A report points at the second leg of the second rotation: the whole rotation stays, retired.
        var plan = HubImportPlan.Make(hubs, rotations, legs, file, LegImportMode.Replace, new HashSet<long> { legs[3].Id });

        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Unchanged, LegImportOutcome.Retired, LegImportOutcome.Retired],
            plan.Steps.Select(step => step.Outcome));
        Assert.Empty(plan.DeletedRotations);
        Assert.True(plan.ReasonRequired);

        plan.Apply(DateTime.UtcNow, "fo-test new season");
        Assert.Equal("fo-test new season", legs[2].RetiredReason);
        Assert.Equal("fo-test new season", legs[3].RetiredReason);
    }

    [Fact]
    public void AHubTheFileNoLongerNamesGoesOnlyOnReplace()
    {
        var (hubs, rotations, legs) = Shape();
        hubs.Add(new TourHub { Id = 3, Icao = "ZZZZ", Sort = 2 });

        var merge = HubImportPlan.Make(hubs, rotations, legs, SameFile(), LegImportMode.Merge, NoReports);
        Assert.Equal(["AAAA", "ZZZZ"], merge.Hubs.Select(hub => hub.Icao));
        Assert.Empty(merge.DeletedHubs);

        var replace = HubImportPlan.Make(hubs, rotations, legs, SameFile(), LegImportMode.Replace, NoReports);
        Assert.Equal(["AAAA"], replace.Hubs.Select(hub => hub.Icao));
        Assert.Equal(["ZZZZ"], replace.DeletedHubs.Select(hub => hub.Icao));
    }

    [Fact]
    public void ARetiredRotationTheFileNamesComesBackWhole()
    {
        var (hubs, rotations, legs) = Shape();
        legs[2].RetiredAt = legs[3].RetiredAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var plan = HubImportPlan.Make(hubs, rotations, legs, SameFile(), LegImportMode.Merge, NoReports);

        Assert.Equal(
            [LegImportOutcome.Unchanged, LegImportOutcome.Unchanged, LegImportOutcome.Restored, LegImportOutcome.Restored],
            plan.Steps.Select(step => step.Outcome));
        Assert.True(plan.Restores);
    }

    [Fact]
    public void TheFingerprintMovesWithTheHubsAndTheRotations()
    {
        var (hubs, rotations, legs) = Shape();
        var before = HubImportPlan.Fingerprint(legs, hubs, rotations);

        rotations[0].RowVersion = rotations[0].RowVersion.AddSeconds(1);
        Assert.NotEqual(before, HubImportPlan.Fingerprint(legs, hubs, rotations));
    }

    /// <summary>A hub tour with one hub, AAAA, and its two rotations of two legs: AAAA–BBBB–AAAA and AAAA–CCCC–AAAA.</summary>
    private static (List<TourHub> Hubs, List<Rotation> Rotations, List<Leg> Legs) Shape()
    {
        var version = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var hubs = new List<TourHub> { new() { Id = 1, Icao = "AAAA", Sort = 1, RowVersion = version } };
        var rotations = new List<Rotation>
        {
            new() { Id = 10, HubId = 1, Sort = 1, Size = 2, RowVersion = version },
            new() { Id = 11, HubId = 1, Sort = 2, Size = 2, RowVersion = version },
        };
        var legs = new List<Leg>
        {
            Leg(100, 1, 10, "AAAA", "BBBB"),
            Leg(101, 2, 10, "BBBB", "AAAA"),
            Leg(102, 3, 11, "AAAA", "CCCC"),
            Leg(103, 4, 11, "CCCC", "AAAA"),
        };

        return (hubs, rotations, legs);

        Leg Leg(long id, int number, long rotation, string from, string to) => new()
        {
            Id = id,
            Number = number,
            RotationId = rotation,
            DepartureIcao = from,
            ArrivalIcao = to,
            RowVersion = version,
        };
    }

    private static List<HubImportRow> SameFile() => File(
        ("AAAA", "1", "AAAA", "BBBB"),
        ("AAAA", "1", "BBBB", "AAAA"),
        ("AAAA", "2", "AAAA", "CCCC"),
        ("AAAA", "2", "CCCC", "AAAA"));

    private static List<HubImportRow> File(params (string? Hub, string? Rotation, string From, string To)[] rows) =>
    [
        .. rows.Select((row, index) => new HubImportRow(
            index,
            new Leg { DepartureIcao = row.From, ArrivalIcao = row.To },
            row.Hub,
            row.Rotation)),
    ];
}
