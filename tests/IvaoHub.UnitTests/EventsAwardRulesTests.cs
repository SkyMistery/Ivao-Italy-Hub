using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Reports;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of award (M4, E14b; design §1.9, §5.4; note 2026-10-17-le-regole-di-award): what each criterion asks of a report's verified items,
/// one by one, the parameters each takes and what they may be; and the shifts an <c>Auto</c> report makes of a controller's sessions.
/// </summary>
public sealed class EventsAwardRulesTests
{
    private static readonly DateTime Start = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<string, string> NoTypes = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ACriterionReadsOneKindOfReportAndTakesItsParameters()
    {
        Assert.Equal(ReportKind.Pilot, AwardRules.KindOf(AwardCriterion.AnyFlight));
        Assert.Equal(ReportKind.Pilot, AwardRules.KindOf(AwardCriterion.MinFlights));
        Assert.Equal(ReportKind.Pilot, AwardRules.KindOf(AwardCriterion.Route));
        Assert.Equal(ReportKind.Pilot, AwardRules.KindOf(AwardCriterion.RouteTimes));
        Assert.Equal(ReportKind.Pilot, AwardRules.KindOf(AwardCriterion.MinLegDistance));
        Assert.Equal(ReportKind.Atc, AwardRules.KindOf(AwardCriterion.AnyShift));
        Assert.Equal(ReportKind.Atc, AwardRules.KindOf(AwardCriterion.MinAtcMinutes));

        Assert.Equal(AwardParameters.None, AwardRules.ParametersOf(AwardCriterion.AnyFlight));
        Assert.Equal(AwardParameters.Route | AwardParameters.Count, AwardRules.ParametersOf(AwardCriterion.RouteTimes));
        Assert.Equal(AwardParameters.Minutes | AwardParameters.PositionType, AwardRules.ParametersOf(AwardCriterion.MinAtcMinutes));
    }

    [Fact]
    public void TheParametersAreCheckedOnTheirFields()
    {
        Assert.True(Check(AwardCriterion.AnyFlight).IsEmpty);

        // What a criterion does not take is not looked at.
        Assert.True(Check(AwardCriterion.AnyShift, departure: "XEA1", count: -4).IsEmpty);

        Assert.Equal(["errors.required"], Check(AwardCriterion.MinFlights).Errors["count"]);
        Assert.Equal(["errors.number.range"], Check(AwardCriterion.MinFlights, count: 0).Errors["count"]);
        Assert.Equal(["errors.number.range"], Check(AwardCriterion.MinFlights, count: AwardRules.MaxCount + 1).Errors["count"]);
        Assert.True(Check(AwardCriterion.MinFlights, count: AwardRules.MaxCount).IsEmpty);

        var route = Check(AwardCriterion.Route);
        Assert.Equal(["errors.required"], route.Errors["departureIcao"]);
        Assert.Equal(["errors.required"], route.Errors["arrivalIcao"]);
        Assert.Equal([ReportRules.SameAirportKey], Check(AwardCriterion.Route, departure: "xea1", arrival: "XEA1").Errors["arrivalIcao"]);
        Assert.True(Check(AwardCriterion.RouteTimes, departure: "XEA1", arrival: "XEA2", count: 2).IsEmpty);

        Assert.Equal(["errors.number.range"], Check(AwardCriterion.MinLegDistance, distance: AwardRules.MaxDistanceNm + 1).Errors["distanceNm"]);
        Assert.Equal(["errors.required"], Check(AwardCriterion.MinAtcMinutes).Errors["minutes"]);
        Assert.True(Check(AwardCriterion.MinAtcMinutes, minutes: 60).IsEmpty);

        Assert.Equal([AwardRules.CriterionKey], Check((AwardCriterion)99).Errors["criterion"]);
    }

    [Fact]
    public void AnyFlightAsksOneVerifiedFlight()
    {
        var rule = Rule(AwardCriterion.AnyFlight);

        Assert.True(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2")));
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2", verified: false)));

        // Not asked is not found.
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2", verified: null)));
        Assert.False(Met(rule, ReportKind.Pilot));
    }

    [Fact]
    public void MinFlightsCountsTheVerifiedOnes()
    {
        var rule = Rule(AwardCriterion.MinFlights, count: 2);

        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2"), Flight("XEA2", "XEA1", verified: false)));
        Assert.True(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2"), Flight("XEA2", "XEA1")));
        Assert.True(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2"), Flight("XEA2", "XEA1"), Flight("XEA1", "XEA3")));
    }

    [Fact]
    public void RouteAsksAVerifiedFlightFromOneAirportToTheOther()
    {
        var rule = Rule(AwardCriterion.Route, departure: "XEA1", arrival: "XEA2");

        Assert.True(Met(rule, ReportKind.Pilot, Flight("XEA3", "XEA1"), Flight("xea1", "xea2")));

        // The other way round is another route, and a flight not found is no flight.
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA2", "XEA1")));
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2", verified: false)));
    }

    [Fact]
    public void RouteTimesCountsTheVerifiedFlightsOfTheRoute()
    {
        var rule = Rule(AwardCriterion.RouteTimes, departure: "XEA1", arrival: "XEA2", count: 2);

        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2"), Flight("XEA2", "XEA1"), Flight("XEA1", "XEA3")));
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2"), Flight("XEA1", "XEA2", verified: false)));
        Assert.True(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2"), Flight("XEA1", "XEA2")));
    }

    [Fact]
    public void MinLegDistanceAsksAVerifiedFlightAtLeastThatLong()
    {
        var rule = Rule(AwardCriterion.MinLegDistance, distance: 500);

        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2", distance: 499.9m)));
        Assert.True(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA2", distance: 499.9m), Flight("XEA1", "XEA3", distance: 500m)));
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA3", distance: 900m, verified: false)));

        // A leg between two airports whose distance is not known is not long.
        Assert.False(Met(rule, ReportKind.Pilot, Flight("XEA1", "XEA4", distance: null)));
    }

    [Fact]
    public void AnyShiftAsksOneVerifiedShift()
    {
        var rule = Rule(AwardCriterion.AnyShift);

        Assert.True(Met(rule, ReportKind.Atc, Shift("XEA1_TWR", 45)));
        Assert.False(Met(rule, ReportKind.Atc, Shift("XEA1_TWR", 10, verified: false)));
    }

    [Fact]
    public void MinAtcMinutesAddsTheVerifiedMinutesOfThePositionsOfItsKind()
    {
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["XEA1_TWR"] = "TWR", ["XEA1_APP"] = "APP" };

        var any = Rule(AwardCriterion.MinAtcMinutes, minutes: 90);
        Assert.True(Met(any, ReportKind.Atc, types, Shift("XEA1_TWR", 45), Shift("XEA1_APP", 45)));
        Assert.False(Met(any, ReportKind.Atc, types, Shift("XEA1_TWR", 45), Shift("XEA1_APP", 60, verified: false)));

        var tower = Rule(AwardCriterion.MinAtcMinutes, minutes: 90, positionType: "TWR");
        Assert.False(Met(tower, ReportKind.Atc, types, Shift("XEA1_TWR", 45), Shift("XEA1_APP", 45)));
        Assert.True(Met(tower, ReportKind.Atc, types, Shift("XEA1_TWR", 45), Shift("xea1_twr", 45)));

        // A callsign the core does not know is of no kind.
        Assert.False(Met(tower, ReportKind.Atc, types, Shift("XEA9_TWR", 120)));
    }

    [Fact]
    public void ARuleReadsOnlyAReportOfItsKind()
    {
        Assert.False(Met(Rule(AwardCriterion.AnyFlight), ReportKind.Atc, Shift("XEA1_TWR", 60)));
        Assert.False(Met(Rule(AwardCriterion.AnyShift), ReportKind.Pilot, Flight("XEA1", "XEA2")));
    }

    [Fact]
    public void AnAutoReportOfAControllerHasOneShiftPerPositionInTheWindow()
    {
        var from = Start.AddHours(-6);
        var to = Start.AddHours(8);
        var positions = new HashSet<string>(["XEA1_TWR", "XEA1_APP"], StringComparer.Ordinal);
        NetworkSession[] sessions =
        [
            new(1, 761262, "XEA1_TWR", Start, Start.AddMinutes(40)),
            new(2, 761262, "xea1_twr", Start.AddMinutes(50), Start.AddMinutes(80)),
            new(3, 761262, "XEA1_APP", Start.AddHours(2), Start.AddHours(2).AddMinutes(10)),

            // Not a position of the event, and a connection over before the window.
            new(4, 761262, "XEA9_CTR", Start, Start.AddHours(1)),
            new(5, 761262, "XEA1_TWR", from.AddHours(-3), from.AddHours(-2)),
        ];

        var shifts = AutoReportRules.Shifts(sessions, positions, from, to, attendanceMinimumMinutes: 30);

        Assert.Equal(2, shifts.Count);
        Assert.Equal(("XEA1_TWR", Start, Start.AddMinutes(80)), (shifts[0].Draft.Callsign, shifts[0].Draft.FromUtc, shifts[0].Draft.ToUtc));
        Assert.Equal((70, true), (shifts[0].Finding.Minutes, shifts[0].Finding.Verified));

        // Ten minutes on the approach: in the report, not verified.
        Assert.Equal("XEA1_APP", shifts[1].Draft.Callsign);
        Assert.Equal((10, false), (shifts[1].Finding.Minutes, shifts[1].Finding.Verified));
    }

    [Fact]
    public void AnAutoShiftIsCutToTheWindowAndToTheLongestShift()
    {
        var from = Start.AddHours(-6);
        var to = Start.AddHours(20);
        var positions = new HashSet<string>(["XEA1_TWR"], StringComparer.Ordinal);

        var shifts = AutoReportRules.Shifts(
            [new NetworkSession(1, 761262, "XEA1_TWR", from.AddHours(-1), from.AddHours(15))],
            positions,
            from,
            to,
            attendanceMinimumMinutes: 30);

        var shift = Assert.Single(shifts);
        Assert.Equal(from, shift.Draft.FromUtc);
        Assert.Equal(from + ReportRules.LongestShift, shift.Draft.ToUtc);
        Assert.Equal((int)ReportRules.LongestShift.TotalMinutes, shift.Finding.Minutes);
    }

    [Fact]
    public void AnAutoReportOfAPilotHasOneFlightPerSessionThatTouchesTheEvent()
    {
        var from = Start.AddHours(-6);
        var to = Start.AddHours(8);
        var airports = new HashSet<string>(["XEA1"], StringComparer.Ordinal);
        NetworkSession[] sessions =
        [
            // The last revision is the flight: a plan filed from one leg to the next says the second.
            new(1, 761262, "tst1", Start.AddMinutes(30), Start.AddMinutes(100), "XEA1", "XEA2")
            {
                Routes = [new NetworkRoute("XEA3", "XEA1"), new NetworkRoute("XEA1", "XEA2")],
            },
            new(2, 761262, "TST1", Start, Start.AddMinutes(25), "XEA3", "XEA1"),

            // Touching none of the event's airports, without a plan, from an airport to itself, and started before the window.
            new(3, 761262, "TST1", Start.AddHours(2), Start.AddHours(3), "XEA3", "XEA4"),
            new(4, 761262, "TST1", Start.AddHours(3), Start.AddHours(4)),
            new(5, 761262, "TST1", Start.AddHours(4), Start.AddHours(5), "XEA1", "XEA1"),
            new(6, 761262, "TST1", from.AddMinutes(-1), Start, "XEA1", "XEA2"),
        ];

        var flights = AutoReportRules.Flights(sessions, airports, from, to, (departure, arrival) => departure == "XEA1" ? 300m : null);

        Assert.Equal(
            [(2L, "TST1", "XEA3", "XEA1"), (1L, "TST1", "XEA1", "XEA2")],
            flights.Select(flight => (flight.Finding.SessionId!.Value, flight.Draft.Callsign, flight.Draft.DepartureIcao!, flight.Draft.ArrivalIcao!)));
        Assert.Equal((DateOnly.FromDateTime(Start), 70, 300m, true), (
            flights[1].Draft.FlightDay!.Value,
            flights[1].Finding.Minutes!.Value,
            flights[1].Finding.DistanceNm!.Value,
            flights[1].Finding.Verified!.Value));
        Assert.Null(flights[0].Finding.DistanceNm);

        // An event without airports of its own takes any flight with a plan.
        Assert.Equal(3, AutoReportRules.Flights(sessions, null, from, to, (_, _) => null).Count);
    }

    [Fact]
    public void ASessionFliesOnlyTheLastRouteOfItsPlan()
    {
        // Filed to one airport, then changed and flown to another: the network finds it for both, and only the second was flown.
        var changed = new NetworkSession(1, 761262, "TST1", Start, Start.AddHours(1), "XEA1", "XEA3")
        {
            Routes = [new NetworkRoute("XEA1", "XEA2"), new NetworkRoute("XEA1", "XEA3")],
        };

        Assert.True(ReportRules.FlewRoute(changed, "xea1", "XEA3"));
        Assert.False(ReportRules.FlewRoute(changed, "XEA1", "XEA2"));
        Assert.False(ReportRules.FlewRoute(new NetworkSession(2, 761262, "TST1", Start, Start.AddHours(1)), "XEA1", "XEA2"));

        // The check gives out only the sessions that flew the item's route.
        var flown = ReportRules.Flown([("TST1", [.. new[] { changed }.Where(session => ReportRules.FlewRoute(session, "XEA1", "XEA2"))])]);
        Assert.Null(flown[0]);
    }

    private static Refusals Check(
        AwardCriterion criterion,
        string? departure = null,
        string? arrival = null,
        int? count = null,
        int? distance = null,
        int? minutes = null) =>
        AwardRules.Check(criterion, departure, arrival, count, distance, minutes, new Refusals());

    private static EventAwardRule Rule(
        AwardCriterion criterion,
        string? departure = null,
        string? arrival = null,
        int? count = null,
        int? distance = null,
        int? minutes = null,
        string? positionType = null) => new()
        {
            Criterion = criterion,
            Kind = AwardRules.KindOf(criterion),
            AwardId = 1,
            DepartureIcao = departure,
            ArrivalIcao = arrival,
            Count = count,
            DistanceNm = distance,
            Minutes = minutes,
            PositionType = positionType,
        };

    private static bool Met(EventAwardRule rule, ReportKind kind, params EventReportItem[] items) => Met(rule, kind, NoTypes, items);

    private static bool Met(EventAwardRule rule, ReportKind kind, Dictionary<string, string> types, params EventReportItem[] items) =>
        AwardRules.IsMet(rule, kind, items, types);

    private static EventReportItem Flight(string departure, string arrival, decimal? distance = 300m, bool? verified = true) => new()
    {
        Callsign = "TST1",
        DepartureIcao = departure,
        ArrivalIcao = arrival,
        FlightDay = DateOnly.FromDateTime(Start),
        Minutes = 60,
        DistanceNm = distance,
        Verified = verified,
    };

    private static EventReportItem Shift(string callsign, int minutes, bool? verified = true) => new()
    {
        Callsign = callsign,
        FromUtc = Start,
        ToUtc = Start.AddMinutes(minutes),
        Minutes = minutes,
        Verified = verified,
    };
}
