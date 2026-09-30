using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T7a that need no database: the estimated time with the numbers of the design (M2 §1.5), the checks of
/// the shape of a tour for every kind that has legs (§1.2.1, §2), and the numbering that keeps no holes (§1.4.1). The
/// great circle is the core's, and <see cref="GreatCircleTests"/> asks it the questions this class used to ask.
/// </summary>
public sealed class LegTests
{
    private static readonly HashSet<string> Known = ["AAAA", "BBBB", "CCCC"];

    [Theory]
    // An A320 at 450 kt with k = 5 % and c = 20 minutes, the design's example (§1.5).
    [InlineData(200, 48)]
    [InlineData(800, 132)]
    [InlineData(2000, 300)]
    public void TheEstimatedTimeIsAShareOfTheDistanceAndAFixedPart(int distanceNm, int minutes) =>
        Assert.Equal(minutes, EstimatedTime.Minutes(distanceNm, 450, 0.05m, 20));

    [Fact]
    public void AKindWithLegsNeedsOneAndAKindWithoutRefusesThem()
    {
        Assert.Contains(new ShapeProblem("legs", "flightops:errors.noLegs"), TourShape.Problems(Tour(TourKind.Sequential), [], Known));

        // A retired leg is not part of the shape.
        Assert.Contains(
            new ShapeProblem("legs", "flightops:errors.noLegs"),
            TourShape.Problems(Tour(TourKind.Free), [Retired(Leg(1, "AAAA", "BBBB"))], Known));

        Assert.Empty(TourShape.Problems(Tour(TourKind.Sequential), [Leg(1, "AAAA", "BBBB")], Known));
        // An Open tour needs no leg; what it needs besides, a goal, is T7c's (OpenTourTests).
        Assert.DoesNotContain(TourShape.Problems(Tour(TourKind.Open), [], Known), problem => problem.Field == "legs");
        Assert.Contains(
            new ShapeProblem("legs", "flightops:errors.kindHasNoLegs"),
            TourShape.Problems(Tour(TourKind.Container), [Leg(1, "AAAA", "BBBB")], Known));
    }

    [Fact]
    public void EveryLegHasKnownAirportsAndAReleaseInsideThePeriod()
    {
        var tour = Tour(TourKind.Free);
        var early = Leg(2, "BBBB", "CCCC");
        early.ReleaseAt = tour.ReleaseAt!.Value.AddDays(-1);
        var late = Leg(3, "CCCC", "AAAA");
        late.ReleaseAt = tour.CloseAt!.Value.AddDays(1);
        var inside = Leg(4, "AAAA", "BBBB");
        inside.ReleaseAt = tour.ReleaseAt.Value.AddDays(3);

        var problems = TourShape.Problems(tour, [Leg(1, "AAAA", "ZZZZ"), early, late, inside], Known);

        Assert.Equal(
            [
                new ShapeProblem("legs.1", "flightops:errors.airportUnknown"),
                new ShapeProblem("legs.2", "flightops:errors.legReleaseOutsidePeriod"),
                new ShapeProblem("legs.3", "flightops:errors.legReleaseOutsidePeriod"),
            ],
            problems);
    }

    [Fact]
    public void ATourWithAChosenStartIsARing()
    {
        var tour = Tour(TourKind.SequentialChosenStart);
        Leg[] ring = [Leg(1, "AAAA", "BBBB"), Leg(2, "BBBB", "CCCC"), Leg(3, "CCCC", "AAAA")];

        Assert.Empty(TourShape.Problems(tour, ring, Known));
        Assert.Contains(
            new ShapeProblem("legs", "flightops:errors.notARing"),
            TourShape.Problems(tour, [ring[0], ring[1]], Known));

        // A retired leg in the middle breaks the ring it leaves behind.
        Assert.Contains(
            new ShapeProblem("legs", "flightops:errors.notARing"),
            TourShape.Problems(tour, [ring[0], Retired(ring[1]), ring[2]], Known));

        // A plain sequence has no such rule: a pilot may be "teleported" between legs.
        Assert.Empty(TourShape.Problems(Tour(TourKind.Sequential), [ring[0], ring[2]], Known));
    }

    [Fact]
    public void ADistanceTourCanBeCompletedWithItsLegs()
    {
        var tour = Tour(TourKind.Distance);
        Leg[] legs = [Leg(1, "AAAA", "BBBB", 300m), Leg(2, "BBBB", "CCCC", 250m)];

        Assert.Contains(new ShapeProblem("requiredNm", "errors.required"), TourShape.Problems(tour, legs, Known));

        tour.RequiredNm = 550;
        Assert.Empty(TourShape.Problems(tour, legs, Known));

        tour.RequiredNm = 551;
        Assert.Contains(new ShapeProblem("requiredNm", "flightops:errors.distanceUnreachable"), TourShape.Problems(tour, legs, Known));
    }

    [Fact]
    public void TheNumbersHaveNoHolesAndKeepTheOrder()
    {
        Leg[] legs = [Leg(2, "AAAA", "BBBB"), Leg(5, "BBBB", "CCCC"), Leg(9, "CCCC", "AAAA")];
        legs[0].Id = 30;
        legs[1].Id = 10;
        legs[2].Id = 20;

        LegBook.Renumber(legs);

        Assert.Equal([1, 2, 3], legs.Select(leg => leg.Number));
        Assert.Equal([30L, 10L, 20L], legs.Select(leg => leg.Id));
    }

    private static Tour Tour(TourKind kind) => new()
    {
        Kind = kind,
        ReleaseAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        CloseAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
    };

    private static Leg Leg(int number, string departure, string arrival, decimal distanceNm = 100m) => new()
    {
        Number = number,
        DepartureIcao = departure,
        ArrivalIcao = arrival,
        DistanceNm = distanceNm,
    };

    private static Leg Retired(Leg leg)
    {
        leg.RetiredAt = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        leg.RetiredReason = "gone";
        return leg;
    }
}
