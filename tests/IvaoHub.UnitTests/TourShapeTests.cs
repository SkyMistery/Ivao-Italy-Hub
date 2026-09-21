using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T7b that need no database: the shape of a hub tour — hubs, rotations of their size out of the hub and
/// back, connections — of a container and of a subtour (design M2 §1.3, §2.3, §2.7), the place of a leg in its rotation,
/// and who decides a callsign (§1.6, note 2026-09-21-la-forma-dei-tour).
/// </summary>
public sealed class TourShapeTests
{
    private static readonly HashSet<string> Known = ["HUBA", "HUBB", "AAAA", "BBBB", "CCCC"];

    [Fact]
    public void AHubTourNeedsAHubAndEveryHubARotation()
    {
        var tour = Tour(TourKind.Hub);

        Assert.Contains(new ShapeProblem("hubs", "flightops:errors.noHubs"), Problems(tour, legs: [], hubs: [], rotations: []));

        var problems = Problems(tour, legs: [], hubs: [Hub(1, "HUBA")], rotations: []);
        Assert.Contains(new ShapeProblem("hubs.HUBA", "flightops:errors.hubWithoutRotations"), problems);
        Assert.Contains(new ShapeProblem("legs", "flightops:errors.noLegs"), problems);
    }

    [Fact]
    public void ARotationHasItsSizeAndLeavesAndReturnsToItsHub()
    {
        var tour = Tour(TourKind.Hub);
        TourHub[] hubs = [Hub(1, "HUBA")];
        Rotation[] rotations = [Rotation(10, hubId: 1, size: 2)];

        // Out and back: ready.
        Assert.Empty(Problems(tour, [InRotation(Leg(1, "HUBA", "AAAA"), 10), InRotation(Leg(2, "AAAA", "HUBA"), 10)], hubs, rotations));

        // One leg short of its size.
        Assert.Contains(
            new ShapeProblem("rotations.HUBA.1", "flightops:errors.rotationSize"),
            Problems(tour, [InRotation(Leg(1, "HUBA", "AAAA"), 10)], hubs, rotations));

        // Of its size, but it ends somewhere else.
        Assert.Contains(
            new ShapeProblem("rotations.HUBA.1", "flightops:errors.rotationNotAtHub"),
            Problems(tour, [InRotation(Leg(1, "HUBA", "AAAA"), 10), InRotation(Leg(2, "AAAA", "BBBB"), 10)], hubs, rotations));

        // The design asks only for the two ends: what happens in between is the tour's business.
        Rotation[] four = [Rotation(10, hubId: 1, size: 4)];
        Assert.Empty(Problems(
            tour,
            [
                InRotation(Leg(1, "HUBA", "AAAA"), 10),
                InRotation(Leg(2, "BBBB", "CCCC"), 10),
                InRotation(Leg(3, "CCCC", "AAAA"), 10),
                InRotation(Leg(4, "AAAA", "HUBA"), 10),
            ],
            hubs,
            four));
    }

    [Fact]
    public void ARotationRetiredWholeNoLongerCounts()
    {
        var tour = Tour(TourKind.Hub);
        TourHub[] hubs = [Hub(1, "HUBA")];
        Rotation[] rotations = [Rotation(10, hubId: 1, size: 2), Rotation(11, hubId: 1, size: 2, sort: 2)];
        Leg[] legs =
        [
            Retired(InRotation(Leg(1, "HUBA", "AAAA"), 10)),
            Retired(InRotation(Leg(2, "AAAA", "HUBA"), 10)),
            InRotation(Leg(3, "HUBA", "BBBB"), 11),
            InRotation(Leg(4, "BBBB", "HUBA"), 11),
        ];

        Assert.Empty(Problems(tour, legs, hubs, rotations));

        // With the second one gone too, the hub has nothing left to fly.
        Assert.Contains(
            new ShapeProblem("hubs.HUBA", "flightops:errors.hubWithoutRotations"),
            Problems(tour, [legs[0], legs[1]], hubs, [rotations[0]]));
    }

    [Fact]
    public void EveryLegOfAHubTourIsInARotationOrConnectsTwoHubs()
    {
        var tour = Tour(TourKind.Hub);
        TourHub[] hubs = [Hub(1, "HUBA"), Hub(2, "HUBB", sort: 2)];
        Rotation[] rotations = [Rotation(10, hubId: 1, size: 2), Rotation(20, hubId: 2, size: 2)];
        List<Leg> legs =
        [
            InRotation(Leg(1, "HUBA", "AAAA"), 10),
            InRotation(Leg(2, "AAAA", "HUBA"), 10),
            Connection(Leg(3, "HUBA", "HUBB")),
            InRotation(Leg(4, "HUBB", "BBBB"), 20),
            InRotation(Leg(5, "BBBB", "HUBB"), 20),
        ];

        Assert.Empty(Problems(tour, legs, hubs, rotations));

        legs.Add(Leg(6, "AAAA", "BBBB"));
        legs.Add(Connection(Leg(7, "HUBA", "CCCC")));
        var problems = Problems(tour, legs, hubs, rotations);

        Assert.Contains(new ShapeProblem("legs.6", "flightops:errors.legNotInRotation"), problems);
        Assert.Contains(new ShapeProblem("legs.7", "flightops:errors.connectionNotBetweenHubs"), problems);
    }

    [Fact]
    public void HubsAndRotationsBelongToAHubTourOnly()
    {
        var problems = Problems(
            Tour(TourKind.Sequential),
            [InRotation(Leg(1, "HUBA", "AAAA"), 10), Connection(Leg(2, "AAAA", "HUBA"))],
            [Hub(1, "HUBA")],
            [Rotation(10, hubId: 1, size: 2)]);

        Assert.Contains(new ShapeProblem("hubs", "flightops:errors.kindHasNoHubs"), problems);
        Assert.Contains(new ShapeProblem("legs.1", "flightops:errors.legHubOnly"), problems);
        Assert.Contains(new ShapeProblem("legs.2", "flightops:errors.legHubOnly"), problems);
    }

    [Fact]
    public void AContainerHasTwoSubtoursAndAsksForNoMoreThanItHas()
    {
        var container = Tour(TourKind.Container);

        var none = TourShape.Problems(container, new TourParts([], [], [], [], null), Known);
        Assert.Contains(new ShapeProblem("subtours", "flightops:errors.containerNeedsSubtours"), none);
        Assert.Contains(new ShapeProblem("requiredSubtours", "errors.required"), none);

        Tour[] two = [Tour(TourKind.Sequential), Tour(TourKind.Free)];
        container.RequiredSubtours = 3;
        Assert.Equal(
            [new ShapeProblem("requiredSubtours", "flightops:errors.requiredSubtoursTooMany")],
            TourShape.Problems(container, new TourParts([], [], [], two, null), Known));

        container.RequiredSubtours = 2;
        Assert.Empty(TourShape.Problems(container, new TourParts([], [], [], two, null), Known));
    }

    [Fact]
    public void ASubtourStaysInsideItsParentAndHasNoAward()
    {
        var parent = Tour(TourKind.Container);
        var subtour = Tour(TourKind.Sequential);
        subtour.ParentTourId = 1;
        Leg[] legs = [Leg(1, "AAAA", "BBBB")];

        Assert.Contains(
            new ShapeProblem("parentTourId", "flightops:errors.parentUnknown"),
            TourShape.Problems(subtour, new TourParts(legs, [], [], [], null), Known));
        Assert.Empty(TourShape.Problems(subtour, new TourParts(legs, [], [], [], parent), Known));

        subtour.ReleaseAt = parent.ReleaseAt!.Value.AddDays(-1);
        subtour.CloseAt = parent.CloseAt!.Value.AddDays(1);
        subtour.AwardId = 5;
        var problems = TourShape.Problems(subtour, new TourParts(legs, [], [], [], parent), Known);

        Assert.Contains(new ShapeProblem("releaseAt", "flightops:errors.subtourOutsideParent"), problems);
        Assert.Contains(new ShapeProblem("closeAt", "flightops:errors.subtourOutsideParent"), problems);
        Assert.Contains(new ShapeProblem("awardId", "flightops:errors.subtourHasNoAward"), problems);
    }

    [Fact]
    public void ALegsPlaceInItsRotationFollowsTheOrderOfTheTour()
    {
        Leg[] legs =
        [
            InRotation(Leg(1, "HUBA", "AAAA"), 10),
            Leg(2, "HUBA", "HUBB"),
            InRotation(Leg(3, "AAAA", "BBBB"), 10),
            InRotation(Leg(4, "BBBB", "HUBA"), 10),
        ];
        legs[1].SeqInRotation = 7;

        LegBook.SequenceRotations(legs);

        Assert.Equal([1, null, 2, 3], legs.Select(leg => leg.SeqInRotation));
    }

    [Theory]
    // Nothing anywhere: every airline.
    [InlineData("", "", "", "AZA123", CallsignVerdict.Allowed)]
    // The tour's airline, whatever follows it: the real callsign of a leg is only a suggestion.
    [InlineData("", "+ITY", "", "ITY9", CallsignVerdict.Allowed)]
    [InlineData("", "+ITY", "", "AZA1165", CallsignVerdict.NotAllowed)]
    // The nearest level with allows decides: the leg's, then the tour's, then the parent's.
    [InlineData("+AZA", "+ITY", "", "AZA1", CallsignVerdict.Allowed)]
    [InlineData("+AZA", "+ITY", "", "ITY1", CallsignVerdict.NotAllowed)]
    [InlineData("", "", "+ITY", "ITY1", CallsignVerdict.Allowed)]
    [InlineData("", "+AZA", "+ITY", "ITY1", CallsignVerdict.NotAllowed)]
    // A deny of any level always wins, an airline or a whole callsign (the Itavia case of Toursystem).
    [InlineData("+ITY", "", "-ITY", "ITY1", CallsignVerdict.Denied)]
    [InlineData("", "+IHX,=IHX870", "", "IHX870", CallsignVerdict.Denied)]
    [InlineData("", "+IHX,=IHX870", "", "IHX8701", CallsignVerdict.Allowed)]
    [InlineData("", "-AZA", "", "ITY1", CallsignVerdict.Allowed)]
    [InlineData("", "+ity", "", " ity12 ", CallsignVerdict.Allowed)]
    public void TheNearestAllowsDecideAndEveryDenyWins(string leg, string tour, string parent, string callsign, CallsignVerdict verdict) =>
        Assert.Equal(verdict, CallsignRules.Judge(callsign, [Rules(leg), Rules(tour), Rules(parent)]));

    private static IReadOnlyList<ShapeProblem> Problems(Tour tour, IReadOnlyList<Leg> legs, IReadOnlyList<TourHub> hubs, IReadOnlyList<Rotation> rotations) =>
        TourShape.Problems(tour, new TourParts(legs, hubs, rotations, [], null), Known);

    /// <summary><c>+ITY</c> allows an airline, <c>-ITY</c> denies one, <c>=IHX870</c> denies a callsign; comma separated.</summary>
    private static List<CallsignRule> Rules(string text) =>
    [
        .. text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(rule => new CallsignRule
        {
            Mode = rule[0] == '+' ? CallsignMode.Allow : CallsignMode.Deny,
            Match = rule[0] == '=' ? CallsignMatch.Exact : CallsignMatch.Airline,
            Value = CallsignRules.Normalize(rule[1..]),
        }),
    ];

    private static Tour Tour(TourKind kind) => new()
    {
        Kind = kind,
        ReleaseAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        CloseAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
    };

    private static TourHub Hub(long id, string icao, int sort = 1) => new() { Id = id, Icao = icao, Sort = sort };

    private static Rotation Rotation(long id, long hubId, int size, int sort = 1) => new() { Id = id, HubId = hubId, Size = size, Sort = sort };

    private static Leg Leg(int number, string departure, string arrival) => new()
    {
        Number = number,
        DepartureIcao = departure,
        ArrivalIcao = arrival,
        DistanceNm = 100m,
    };

    private static Leg InRotation(Leg leg, long rotationId)
    {
        leg.RotationId = rotationId;
        return leg;
    }

    private static Leg Connection(Leg leg)
    {
        leg.Kind = LegKind.HubConnection;
        return leg;
    }

    private static Leg Retired(Leg leg)
    {
        leg.RetiredAt = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        leg.RetiredReason = "gone";
        return leg;
    }
}
