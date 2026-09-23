using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T11 that need no database: the three questions of every kind of tour (design M2 §2) — which legs may be
/// flown, which is next, when it is done — with the rejection and its grace (§2.5) and the dispute that unblocks (§3.8);
/// the flight of an <c>Open</c> tour against its filters, its sequence rules and its goal (§2.6.1); the daily limits by
/// the UTC day of the take-off (§3.6); and a real session of the tracker read as a flight.
/// </summary>
public sealed class PirepRulesTests
{
    private const int Grace = 12;

    private static readonly DateTime Release = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Release.AddDays(10);

    private long _nextReport = 1000;

    // ─── Sequential ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TourProgression.FlyAhead, "P", "Pending,Todo,Locked", 2L)]
    [InlineData(TourProgression.WaitForValidation, "P", "Pending,Locked,Locked", null)]
    [InlineData(TourProgression.WaitForValidation, "A", "Done,Todo,Locked", 2L)]
    [InlineData(TourProgression.FlyAhead, "A,P", "Done,Pending,Todo", 3L)]
    [InlineData(TourProgression.FlyAhead, "A,A,A", "Done,Done,Done", null)]
    [InlineData(TourProgression.FlyAhead, "W", "Todo,Locked,Locked", 1L)]
    public void ASequenceIsFlownInOrderAndFlyingAheadDependsOnTheProgression(
        TourProgression progression,
        string reports,
        string colours,
        long? next)
    {
        var tour = Tour(TourKind.Sequential, progression);
        var legs = Legs(3);
        var mine = Reports(legs, reports);

        var progress = TourRules.Of(tour, legs, [], [], mine, Now, Grace);

        Assert.Equal(colours, string.Join(',', legs.Select(leg => progress.Legs[leg.Id])));
        Assert.Equal(next, progress.Next);
        Assert.Equal(reports == "A,A,A", progress.Finished);
    }

    [Fact]
    public void ARejectedLegIsFlownAgainAndHoldsTheNextOnesOnlyAfterItsGrace()
    {
        var tour = Tour(TourKind.Sequential, TourProgression.FlyAhead);
        var legs = Legs(3);
        var decided = Now.AddHours(-2);
        var mine = new List<Pirep> { Report(legs[0], PirepStatus.Rejected, decidedAt: decided) };

        // Within the grace: the rejected leg again, and the one after it for a flight that took off in time.
        var inTime = TourRules.Of(tour, legs, [], [], mine, decided.AddHours(Grace), Grace);
        Assert.Equal([legs[0].Id, legs[1].Id], inTime.Flyable.Order());

        // After the grace: only the rejected leg.
        var late = TourRules.Of(tour, legs, [], [], mine, decided.AddHours(Grace).AddMinutes(1), Grace);
        Assert.Equal([legs[0].Id], late.Flyable);
        Assert.Equal(LegProgress.Todo, late.Legs[legs[0].Id]);
        Assert.Equal(LegProgress.Locked, late.Legs[legs[1].Id]);

        Assert.Equal(
            "flightops:errors.reportLegLocked",
            TourRules.Refusal(tour, legs, [], [], mine, legs[1].Id, decided.AddDays(1), Grace));
        Assert.Null(TourRules.Refusal(tour, legs, [], [], mine, legs[1].Id, decided.AddHours(1), Grace));

        // Disputed: it no longer holds anything (§3.8), and it may still be flown again.
        mine[0].IsDisputed = true;
        var disputed = TourRules.Of(tour, legs, [], [], mine, Now.AddDays(3), Grace);
        Assert.Equal([legs[0].Id, legs[1].Id], disputed.Flyable.Order());
    }

    [Fact]
    public void ARetiredLegIsSkippedAndALegNotYetReleasedStopsThePilotBeforeIt()
    {
        var tour = Tour(TourKind.Sequential, TourProgression.FlyAhead);
        var legs = Legs(4);
        legs[1].RetiredAt = Now.AddDays(-1);
        legs[3].ReleaseAt = Now.AddDays(5);
        var mine = Reports(legs, "A");

        var progress = TourRules.Of(tour, legs, [], [], mine, Now, Grace);

        Assert.False(progress.Legs.ContainsKey(legs[1].Id));
        Assert.Equal(legs[2].Id, progress.Next);

        mine.Add(Report(legs[2], PirepStatus.Accepted));
        var stopped = TourRules.Of(tour, legs, [], [], mine, Now, Grace);
        Assert.Empty(stopped.Flyable);
        Assert.Equal(LegProgress.Locked, stopped.Legs[legs[3].Id]);
        Assert.False(stopped.Finished);

        // A retired leg is not counted for the completion (§1.4.1).
        mine.Add(Report(legs[3], PirepStatus.Accepted));
        Assert.True(TourRules.Of(tour, legs, [], [], mine, Now.AddDays(6), Grace).Finished);

        Assert.Equal("flightops:errors.reportLegGone", TourRules.Refusal(tour, legs, [], [], [], legs[1].Id, Now, Grace));
        Assert.Equal("flightops:errors.reportBeforeRelease", TourRules.Refusal(tour, legs, [], [], [], legs[3].Id, Now, Grace));
    }

    // ─── Free and Distance ───────────────────────────────────────────────────

    [Fact]
    public void AFreeTourFliesAnyLegNotAcceptedNorPendingAndARejectedOneWhenThePilotWants()
    {
        var tour = Tour(TourKind.Free, TourProgression.WaitForValidation);
        var legs = Legs(3);
        var mine = new List<Pirep>
        {
            Report(legs[0], PirepStatus.Rejected, decidedAt: Now.AddDays(-5)),
            Report(legs[1], PirepStatus.Queued),
        };

        var progress = TourRules.Of(tour, legs, [], [], mine, Now, Grace);

        Assert.Equal([legs[0].Id, legs[2].Id], progress.Flyable.Order());
        Assert.Null(progress.Next);
        Assert.Equal("flightops:errors.reportLegPending", TourRules.Refusal(tour, legs, [], [], mine, legs[1].Id, Now, Grace));
    }

    [Fact]
    public void ADistanceTourIsDoneWhenTheAcceptedLegsReachTheDistance()
    {
        var tour = Tour(TourKind.Distance, TourProgression.FlyAhead);
        tour.RequiredNm = 500;
        var legs = Legs(3); // 300 NM each
        var mine = Reports(legs, "A");

        Assert.False(TourRules.Of(tour, legs, [], [], mine, Now, Grace).Finished);

        mine.Add(Report(legs[2], PirepStatus.Accepted));
        var done = TourRules.Of(tour, legs, [], [], mine, Now, Grace);
        Assert.True(done.Finished);
        Assert.Equal([legs[1].Id], done.Flyable);
    }

    // ─── Sequential with a chosen start ───────────────────────────────────────

    [Fact]
    public void AChosenStartIsTheFirstReportNotWithdrawnAndTheRingGoesRound()
    {
        var tour = Tour(TourKind.SequentialChosenStart, TourProgression.FlyAhead);
        var legs = Legs(4);

        // Before any report, any leg starts it.
        Assert.Equal(legs.Select(leg => leg.Id).Order(), TourRules.Of(tour, legs, [], [], [], Now, Grace).Flyable.Order());

        // Started on the third: then the fourth, then round to the first.
        var mine = new List<Pirep> { Report(legs[2], PirepStatus.Accepted) };
        Assert.Equal(legs[3].Id, TourRules.Of(tour, legs, [], [], mine, Now, Grace).Next);

        mine.Add(Report(legs[3], PirepStatus.Accepted));
        Assert.Equal(legs[0].Id, TourRules.Of(tour, legs, [], [], mine, Now, Grace).Next);

        // The only report withdrawn: the start is free again (Carmine, 23 September 2026).
        var withdrawn = new List<Pirep> { Report(legs[2], PirepStatus.Withdrawn) };
        Assert.Equal(4, TourRules.Of(tour, legs, [], [], withdrawn, Now, Grace).Flyable.Count);

        // Rejected, it stays the start.
        var rejected = new List<Pirep> { Report(legs[2], PirepStatus.Rejected, decidedAt: Now.AddDays(-3)) };
        Assert.Equal([legs[2].Id], TourRules.Of(tour, legs, [], [], rejected, Now, Grace).Flyable);
    }

    // ─── Hub ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AHubIsChosenByFlyingItsFirstLegAndItsRotationsGoInTheTourOrder()
    {
        var shape = HubTour(HubRotationOrder.Fixed, connected: false);

        // At the start: the first leg of the first rotation of every hub.
        var start = TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, [], Now, Grace);
        Assert.Equal([shape.Leg("A1", 1), shape.Leg("B1", 1)], start.Flyable.Order());

        // Hub A chosen: its first rotation, in order, then its second.
        var mine = new List<Pirep> { Report(shape.Find("A1", 1), PirepStatus.Accepted) };
        Assert.Equal([shape.Leg("A1", 2)], TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace).Flyable);

        mine.Add(Report(shape.Find("A1", 2), PirepStatus.Accepted));
        Assert.Equal([shape.Leg("A2", 1)], TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace).Flyable);

        // Hub A done; B has no connecting leg, so it is reached freely.
        mine.Add(Report(shape.Find("A2", 1), PirepStatus.Accepted));
        mine.Add(Report(shape.Find("A2", 2), PirepStatus.Accepted));
        var moved = TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace);
        Assert.Equal([shape.Leg("B1", 1)], moved.Flyable);
        Assert.False(moved.Finished);
    }

    [Fact]
    public void WithRotationsInAnyOrderOneIsFinishedBeforeTheNextStarts()
    {
        var shape = HubTour(HubRotationOrder.Free, connected: false);
        var mine = new List<Pirep> { Report(shape.Find("A2", 1), PirepStatus.Accepted) };

        // Inside hub A, and rotation A2 begun: only its next leg.
        Assert.Equal([shape.Leg("A2", 2)], TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace).Flyable);

        mine.Add(Report(shape.Find("A2", 2), PirepStatus.Accepted));
        Assert.Equal([shape.Leg("A1", 1)], TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace).Flyable);
    }

    [Fact]
    public void AConnectedHubIsReachedOnlyThroughItsConnectingLeg()
    {
        var shape = HubTour(HubRotationOrder.Fixed, connected: true);
        var mine = shape.Legs
            .Where(leg => leg.Kind == LegKind.Normal && shape.HubOf(leg) == "XHUA")
            .Select(leg => Report(leg, PirepStatus.Accepted))
            .ToList();

        var done = TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace);
        var connection = shape.Legs.Single(leg => leg.Kind == LegKind.HubConnection);
        Assert.Equal([connection.Id], done.Flyable);

        mine.Add(Report(connection, PirepStatus.Accepted));
        var arrived = TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace);
        Assert.Equal([shape.Leg("B1", 1)], arrived.Flyable);

        foreach (var leg in shape.Legs.Where(leg => leg.Kind == LegKind.Normal && shape.HubOf(leg) == "XHUB"))
        {
            mine.Add(Report(leg, PirepStatus.Accepted));
        }

        Assert.True(TourRules.Of(shape.Tour, shape.Legs, shape.Hubs, shape.Rotations, mine, Now, Grace).Finished);
    }

    // ─── Open ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AnOpenFlightIsRefusedOnARouteAlreadyFlownButTheReverseIsAnotherRoute()
    {
        var mine = new List<Pirep> { OpenReport("XAAA", "XBBB", 300, PirepStatus.Queued, Now.AddDays(-1)) };

        Assert.Contains("flightops:errors.reportRouteRepeated", OpenRules.Problems(Flight("XAAA", "XBBB"), [], mine));
        Assert.Empty(OpenRules.Problems(Flight("XBBB", "XAAA"), [], mine));

        // A rejected route is flown again.
        mine[0].Status = PirepStatus.Rejected;
        Assert.Empty(OpenRules.Problems(Flight("XAAA", "XBBB"), [], mine));
    }

    [Fact]
    public void TheFiltersOfAnOpenTourBlockAndAMissingFactLetsTheFlightThrough()
    {
        var constraints = new List<TourConstraint>
        {
            Constraint(TourConstraintKind.DepartureOrArrivalIn, """{"countries":["XA"]}"""),
            Constraint(TourConstraintKind.DistanceBetween, """{"minNm":200,"maxNm":1500}"""),
            Constraint(TourConstraintKind.AircraftCategory, """{"categories":["L"]}"""),
            Constraint(TourConstraintKind.ArrivalRunwayMax, """{"meters":1500}"""),
            Constraint(TourConstraintKind.FlightRules, """{"rules":"V"}"""),
        };

        var good = Flight("XAAA", "XBBB") with { WakeCategory = "L", FlightRules = "V", ArrivalLongestRunwayMetres = 1200 };
        Assert.Empty(OpenRules.Problems(good, constraints, []));

        var wrong = good with
        {
            DepartureCountry = "XC",
            ArrivalCountry = "XD",
            DistanceNm = 100,
            WakeCategory = "M",
            ArrivalLongestRunwayMetres = 3000,
            FlightRules = "I",
        };
        Assert.Equal(
            [
                "flightops:errors.reportCountries",
                "flightops:errors.reportDistance",
                "flightops:errors.reportAircraftCategory",
                "flightops:errors.reportRunwayTooLong",
                "flightops:errors.reportFlightRules",
            ],
            OpenRules.Problems(wrong, constraints, []));

        // The hub does not know the runways or the category: the validator judges (the rule of the runways, T1).
        Assert.DoesNotContain(
            "flightops:errors.reportRunwayTooLong",
            OpenRules.Problems(wrong with { ArrivalLongestRunwayMetres = null, WakeCategory = null }, constraints, []));
    }

    [Fact]
    public void TheSequenceRulesReadThePilotsPreviousFlight()
    {
        var constraints = new List<TourConstraint>
        {
            Constraint(TourConstraintKind.Chained, "{}"),
            Constraint(TourConstraintKind.IncreasingDistance, "{}"),
            Constraint(TourConstraintKind.Eastbound, "{}"),
        };
        var mine = new List<Pirep> { OpenReport("XAAA", "XBBB", 400, PirepStatus.Accepted, Now.AddDays(-1)) };

        Assert.Empty(OpenRules.Problems(Flight("XBBB", "XCCC") with { DistanceNm = 500 }, constraints, mine));
        Assert.Equal(
            ["flightops:errors.reportNotChained", "flightops:errors.reportDistanceNotIncreasing", "flightops:errors.reportNotEastbound"],
            OpenRules.Problems(
                Flight("XDDD", "XCCC") with { DistanceNm = 300, DepartureLongitude = 20, ArrivalLongitude = 10 },
                constraints,
                mine));

        // Eastbound across the antimeridian: from 170° E to 170° W is twenty degrees east.
        Assert.Equal(20, OpenRules.Heading(Flight("XAAA", "XBBB") with { DepartureLongitude = 170, ArrivalLongitude = -170 }), 6);
    }

    [Fact]
    public void TheGoalOfAnOpenTourCountsTheAcceptedFlightsAndMinFlightsAtWaitsForTheCompletion()
    {
        var mine = new List<Pirep>
        {
            OpenReport("XAAA", "XBBB", 400, PirepStatus.Accepted, Now.AddDays(-3)),
            OpenReport("XBBB", "XCCC", 500, PirepStatus.Accepted, Now.AddDays(-2)),
            OpenReport("XCCC", "XDDD", 900, PirepStatus.Queued, Now.AddDays(-1)),
        };
        var facts = new OpenFacts(
            new Dictionary<string, string> { ["XAAA"] = "XA", ["XBBB"] = "XA", ["XCCC"] = "XB" },
            new Dictionary<string, IReadOnlyList<string>>());
        var atLeast = new List<TourConstraint> { Constraint(TourConstraintKind.MinFlightsAt, """{"airport":"XAAA","count":2}""") };

        Assert.Equal(900, OpenRules.Progress(OpenGoal.Distance, Parameters("""{"nm":900}"""), [], mine, facts).Done);
        Assert.True(OpenRules.Progress(OpenGoal.Distance, Parameters("""{"nm":900}"""), [], mine, facts).Finished);
        Assert.Equal((2, 2), Pair(OpenRules.Progress(OpenGoal.DistinctCountries, Parameters("""{"count":2}"""), [], mine, facts)));
        Assert.Equal((2, 3), Pair(OpenRules.Progress(OpenGoal.CollectList, Parameters("""{"airports":["XAAA","XCCC","XZZZ"]}"""), [], mine, facts)));

        var waiting = OpenRules.Progress(OpenGoal.FlightCount, Parameters("""{"count":2}"""), atLeast, mine, facts);
        Assert.Equal(["XAAA"], waiting.MissingMinFlightsAt);
        Assert.False(waiting.Finished);
    }

    // ─── Daily limits ─────────────────────────────────────────────────────────

    /// <summary>
    /// The example of the design (§3.6): with a limit of twelve, twelve flights that took off on the same UTC day are
    /// reported over three days, and the thirteenth of that day is refused whatever day it is reported on — while a flight
    /// of the next day is not.
    /// </summary>
    [Fact]
    public void TheDailyLimitCountsTheFlightsOfAUtcDayNotTheSends()
    {
        var day = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var flown = Enumerable.Range(0, 12).Select(hour => (TourId: 1L, TakeoffAt: day.AddHours(hour + 0.5))).ToList();

        Assert.Null(DailyLimits.Refusal(day.AddHours(23), 1, tourLimit: null, divisionLimit: 13, flown));
        Assert.Equal(
            "flightops:errors.reportDivisionDailyLimit",
            DailyLimits.Refusal(day.AddHours(23), 1, tourLimit: null, divisionLimit: 12, flown));
        Assert.Null(DailyLimits.Refusal(day.AddDays(1), 1, tourLimit: null, divisionLimit: 12, flown));

        // The tour's limit counts the tour's reports only; the division's counts every tour's.
        var elsewhere = flown.Select(report => report with { TourId = 2 }).Take(3).Concat(flown.Take(2)).ToList();
        Assert.Equal("flightops:errors.reportTourDailyLimit", DailyLimits.Refusal(day.AddHours(20), 1, tourLimit: 2, divisionLimit: 10, elsewhere));
        Assert.Null(DailyLimits.Refusal(day.AddHours(20), 3, tourLimit: 2, divisionLimit: 10, elsewhere));
    }

    // ─── The tracker ──────────────────────────────────────────────────────────

    /// <summary>A real session (LPMA to LPBJ): the wheels leave the ground after the taxi, and the plan valid then is the last filed before.</summary>
    [Fact]
    public void ARealSessionIsReadAsAFlightWithItsTakeoffAndThePlanValidThen()
    {
        const long id = 62748566;
        var session = IvaoTrackerReader.ReadSessions(Fixture("tracker-sessions-780001.json")).Sessions.Single(row => row.Id == id);
        var plans = IvaoTrackerReader.ReadFlightPlans(Fixture($"tracker-flightplans-{id}.json"));
        var track = IvaoTrackerReader.ReadTracks(Fixture($"tracker-tracks-{id}.json"));

        var flight = TrackedFlight.Read(session, plans, track);

        Assert.NotNull(flight.TakeoffAt);
        Assert.NotNull(flight.LandingAt);
        Assert.True(flight.TakeoffAt > session.StartedAt && flight.LandingAt > flight.TakeoffAt && flight.LandingAt <= session.EndedAt);
        Assert.All(track.Where(point => point.At < flight.TakeoffAt), point => Assert.True(point.OnGround));
        Assert.Equal(plans.Last(plan => plan.FiledAt <= flight.TakeoffAt).Revision, flight.PlanAtTakeoff!.Revision);
        Assert.Equal("LPMA", flight.DepartureIcao);
        Assert.Equal("LPBJ", flight.ArrivalIcao);
        Assert.Equal("E55P", flight.Aircraft);
    }

    // ─── Builders ─────────────────────────────────────────────────────────────

    private static Tour Tour(TourKind kind, TourProgression progression) => new()
    {
        Id = 1,
        Kind = kind,
        Progression = progression,
        ReleaseAt = Release,
        CloseAt = Release.AddMonths(3),
        ReportWindowDays = 7,
    };

    private static List<Leg> Legs(int count) =>
    [
        .. Enumerable.Range(1, count).Select(number => new Leg
        {
            Id = number,
            TourId = 1,
            Number = number,
            DepartureIcao = $"XL{number:00}",
            ArrivalIcao = $"XL{number + 1:00}",
            DistanceNm = 300,
        }),
    ];

    /// <summary>One report per letter, on the legs in order: A accepted, P queued, R rejected, W withdrawn.</summary>
    private List<Pirep> Reports(IReadOnlyList<Leg> legs, string letters) =>
    [
        .. letters.Split(',').Select((letter, index) => Report(legs[index], letter switch
        {
            "A" => PirepStatus.Accepted,
            "P" => PirepStatus.Queued,
            "R" => PirepStatus.Rejected,
            _ => PirepStatus.Withdrawn,
        })),
    ];

    private Pirep Report(Leg leg, PirepStatus status, DateTime? decidedAt = null)
    {
        var id = _nextReport++;
        return new Pirep
        {
            Id = id,
            TourId = leg.TourId,
            LegId = leg.Id,
            Status = status,
            SubmittedAt = Release.AddHours(id - 999),
            TakeoffAt = Release.AddHours(id - 1000),
            DecidedAt = decidedAt,
            DepartureIcao = leg.DepartureIcao,
            ArrivalIcao = leg.ArrivalIcao,
            DistanceNm = leg.DistanceNm,
        };
    }

    private Pirep OpenReport(string departure, string arrival, decimal distance, PirepStatus status, DateTime takeoff) => new()
    {
        Id = _nextReport++,
        TourId = 1,
        Status = status,
        SubmittedAt = takeoff.AddHours(3),
        TakeoffAt = takeoff,
        DepartureIcao = departure,
        ArrivalIcao = arrival,
        DistanceNm = distance,
    };

    private static OpenFlight Flight(string departure, string arrival) => new(
        departure,
        arrival,
        DepartureCountry: "XA",
        ArrivalCountry: "XB",
        DepartureLongitude: 10,
        ArrivalLongitude: 20,
        DistanceNm: 800,
        TakeoffAt: Now,
        WakeCategory: "M",
        FlightRules: "I",
        ArrivalLongestRunwayMetres: 2500,
        ArrivalElevationFeet: 300);

    private static TourConstraint Constraint(TourConstraintKind kind, string parameters) =>
        new() { TourId = 1, Kind = kind, ParametersJson = parameters };

    private static JsonObject Parameters(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static (int Done, int Target) Pair(OpenProgress progress) => (progress.Done, progress.Target);

    /// <summary>
    /// Two hubs, XHUA and XHUB, with two rotations of two legs each (A1, A2, B1, B2); with <paramref name="connected"/> a
    /// connecting leg from A to B.
    /// </summary>
    private static HubShape HubTour(HubRotationOrder order, bool connected)
    {
        var tour = Tour(TourKind.Hub, TourProgression.FlyAhead);
        tour.HubRotationOrder = order;

        var hubs = new List<TourHub> { new() { Id = 1, TourId = 1, Icao = "XHUA", Sort = 0 }, new() { Id = 2, TourId = 1, Icao = "XHUB", Sort = 1 } };
        var rotations = new List<Rotation>
        {
            new() { Id = 11, TourId = 1, HubId = 1, Sort = 0, Size = 2 },
            new() { Id = 12, TourId = 1, HubId = 1, Sort = 1, Size = 2 },
            new() { Id = 21, TourId = 1, HubId = 2, Sort = 0, Size = 2 },
            new() { Id = 22, TourId = 1, HubId = 2, Sort = 1, Size = 2 },
        };

        var legs = new List<Leg>();
        var number = 1;
        foreach (var rotation in rotations)
        {
            var hub = hubs.Single(row => row.Id == rotation.HubId).Icao;
            var outstation = $"XO{rotation.Id}";
            legs.Add(RotationLeg(number++, rotation.Id, 1, hub, outstation));
            legs.Add(RotationLeg(number++, rotation.Id, 2, outstation, hub));

            if (connected && rotation.Id == 12)
            {
                legs.Add(new Leg { Id = 99, TourId = 1, Number = number++, Kind = LegKind.HubConnection, DepartureIcao = "XHUA", ArrivalIcao = "XHUB", DistanceNm = 200 });
            }
        }

        return new HubShape(tour, legs, hubs, rotations);
    }

    private static Leg RotationLeg(int number, long rotationId, int seq, string departure, string arrival) => new()
    {
        Id = rotationId * 10 + seq,
        TourId = 1,
        Number = number,
        Kind = LegKind.Normal,
        RotationId = rotationId,
        SeqInRotation = seq,
        DepartureIcao = departure,
        ArrivalIcao = arrival,
        DistanceNm = 250,
    };

    private static JsonElement Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory!.FullName, FixtureIvaoApiClient.Directory, name);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private sealed record HubShape(Tour Tour, List<Leg> Legs, List<TourHub> Hubs, List<Rotation> Rotations)
    {
        /// <summary>A leg of a rotation by name — «A1» is hub A's first rotation — and its place in it.</summary>
        public Leg Find(string rotation, int seq) => Legs.Single(leg => leg.Id == RotationId(rotation) * 10 + seq);

        public long Leg(string rotation, int seq) => Find(rotation, seq).Id;

        public string HubOf(Leg leg) => Hubs.Single(hub => hub.Id == Rotations.Single(row => row.Id == leg.RotationId).HubId).Icao;

        private static long RotationId(string name) => (name[0] == 'A' ? 10 : 20) + (name[1] - '0');
    }
}
