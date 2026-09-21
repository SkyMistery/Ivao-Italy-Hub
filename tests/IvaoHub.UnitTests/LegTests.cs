using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T7a that need no database: the great circle (taken from Toursystem with its tests), the estimated
/// time with the numbers of the design (M2 §1.5), the checks of the shape of a tour for every kind that has legs
/// (§1.2.1, §2), and the numbering that keeps no holes (§1.4.1).
/// </summary>
public sealed class LegTests
{
    // Real coordinates. The expected values were computed with an independent implementation, not with this one.
    private static readonly GeoPoint Lirf = new(41.8002777778, 12.2388888889);
    private static readonly GeoPoint Liml = new(45.4451, 9.27674);
    private static readonly GeoPoint Egll = new(51.470748, -0.459909);
    private static readonly GeoPoint Lime = new(45.6739, 9.7042);
    private static readonly GeoPoint Lflj = new(45.396999, 6.63472);
    private static readonly GeoPoint Kjfk = new(40.639801, -73.7789);

    private static readonly HashSet<string> Known = ["AAAA", "BBBB", "CCCC"];

    [Fact]
    public void TheGreatCircleIsRightOnThreeOrdersOfMagnitude()
    {
        Assert.Equal(253.9, GreatCircle.DistanceNm(Lirf, Liml), 1);
        Assert.Equal(130.1, GreatCircle.DistanceNm(Lime, Lflj), 1);
        Assert.Equal(779.6, GreatCircle.DistanceNm(Lirf, Egll), 1);
        Assert.Equal(3707.2, GreatCircle.DistanceNm(Lirf, Kjfk), 1);
        Assert.Equal(10807.3, GreatCircle.DistanceNm(new GeoPoint(90, 0), new GeoPoint(-90, 0)), 1);

        // The historical definition of the nautical mile: sixty per degree on the equator.
        Assert.Equal(60.0, GreatCircle.DistanceNm(new GeoPoint(0, 0), new GeoPoint(0, 1)), 0);
    }

    [Fact]
    public void TheGreatCircleHasNoDirectionNoNaNAndATenthOfAMile()
    {
        Assert.Equal(GreatCircle.DistanceNm(Lirf, Kjfk), GreatCircle.DistanceNm(Kjfk, Lirf), 6);

        // With the naive formula on the cosine this is NaN: the argument of the arccosine goes past 1.
        Assert.Equal(0, GreatCircle.DistanceNm(Lirf, Lirf));
        Assert.Equal(0m, GreatCircle.DistanceNmRounded(Egll, Egll));

        // Fiumicino and Urbe are some fifteen miles apart: where a formula that loses digits shows it.
        Assert.InRange(GreatCircle.DistanceNm(Lirf, new GeoPoint(41.9519, 12.4989)), 12, 20);

        var rounded = GreatCircle.DistanceNmRounded(Lirf, Liml);
        Assert.Equal(253.9m, rounded);
        Assert.Equal(1, rounded.Scale);
    }

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
