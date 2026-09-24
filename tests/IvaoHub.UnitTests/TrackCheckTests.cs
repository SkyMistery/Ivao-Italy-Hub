using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The checks on the tracks (M2, T18) on the corpus of <see cref="FlightCheckTests"/>: the points the tracker gave, the airports
/// and runway ends IVAO publishes, and the METARs NOAA kept of the three VFR flights — with what the controllers found (note
/// 2026-09-24-i-controlli-dai-pirep-veri §5) as the expected outcome, every check with its starting values. Then the pieces.
/// </summary>
public sealed class TrackCheckTests
{
    private static readonly IFlightCheck[] All =
    [
        new DisconnectionsCheck(), new ParkingCheck(), new Speed250Check(), new SimRateCheck(), new MaxAltitudeCheck(),
        new LandingAtArrivalCheck(), new TakeoffFromThresholdCheck(), new VmcCheck(),
    ];

    // ─── The corpus ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(879788, "")] // Turboprop 5
    [InlineData(879691, "")] // Turboprop 4
    [InlineData(879558, "")] // Turboprop 3
    [InlineData(882171, "")] // VFR 4: a touch and go on the way, VMC at Portorož
    [InlineData(880760, "")] // VFR 24: no METAR at Pisa, VMC at Bergamo
    [InlineData(881923, "")] // VFR 22
    [InlineData(877464, "parking,speed250")] // Dangerous Airports 2: 0.8 minutes at the gate, 322 kt at 4800 ft
    [InlineData(877596, "speed250")] // Dangerous Airports 4
    [InlineData(877187, "speed250")] // Bizjet 8: the pilot declared an emergency
    [InlineData(877196, "simRate")] // Ryanair Summer 19: four times faster for a while
    [InlineData(880159, "")] // Ryanair Summer 19: the old system's false «disconnected 2921 minutes» and wrong airports
    [InlineData(879610, "")] // Volotea
    [InlineData(881263, "")] // Lufthansa Group
    [InlineData(881169, "")] // Ryanair Summer 16
    [InlineData(876413, "")] // Itavia: 259 kt at 9500 ft, in the band below FL100
    public void EveryReportOfTheCorpusGivesWhatTheControllersFoundOnTheTracks(long report, string failing)
    {
        var context = FlightCheckTests.Context(report);

        var outcomes = All.ToDictionary(check => check.Key, check => check.Evaluate(context, Read(check.Key, "{}")).Outcome);

        Assert.Equal(failing.Length == 0 ? [] : failing.Split(','), outcomes.Where(pair => pair.Value == CheckOutcome.Failed).Select(pair => pair.Key));
        Assert.DoesNotContain(CheckOutcome.Unavailable, outcomes.Values);
    }

    [Fact]
    public void TheTwoShortDisconnectionsOf880159AreShownAndPass()
    {
        var context = FlightCheckTests.Context(880159);

        var disconnections = new DisconnectionsCheck().Evaluate(context, Read(CheckCatalog.Disconnections, "{}"));
        var landing = new LandingAtArrivalCheck().Evaluate(context, Read(CheckCatalog.LandingAtArrival, "{}"));

        Assert.Equal(CheckOutcome.Passed, disconnections.Outcome);
        Assert.Equal(["1.7", "1.1"], disconnections.Evidence.Where(line => line.Key == Key("disconnectionInFlight")).Select(line => line.Values!["minutes"]));
        Assert.Equal("2.8", Line(disconnections, "disconnectionsWithin").Values!["total"]);
        Assert.Equal("LIMP", Line(landing, "landedAt").Values!["airport"]);

        // With a limit of one minute the longest is too long.
        var strict = new DisconnectionsCheck().Evaluate(context, Read(CheckCatalog.Disconnections, """{"maxSingleDisconnectMinutes":1}"""));
        Assert.Equal(CheckOutcome.Failed, strict.Outcome);
    }

    [Theory]
    [InlineData(879691, "02", false)]
    [InlineData(879558, "05", false)]
    [InlineData(877464, "27", false)]
    [InlineData(877596, "26", false)]
    [InlineData(880159, "33", false)]
    [InlineData(881263, "11", false)]
    [InlineData(876413, "30", false)]
    [InlineData(877187, "10", true)] // Melun, about 280 m in
    [InlineData(879610, "05", true)] // Olbia, about 470 m in: a rolling take-off, never caught standing
    public void TheRollStartsAtTheThresholdOrItSaysIntersectionAndNeverFails(long report, string runway, bool intersection)
    {
        var verdict = new TakeoffFromThresholdCheck().Evaluate(FlightCheckTests.Context(report), []);

        Assert.Equal(CheckOutcome.Passed, verdict.Outcome);
        var line = Assert.Single(verdict.Evidence);
        Assert.Equal(Key(intersection ? "takeoffFromIntersection" : "takeoffFromThreshold"), line.Key);
        Assert.Equal(runway, line.Values!["runway"]);
    }

    [Fact]
    public void TheRunwayOfBolzanoIsStartedWellPastItsThreshold()
    {
        // LIPB RW19 from about 340 m: an intersection, or a threshold IVAO places further up — the validator looks.
        var verdict = new TakeoffFromThresholdCheck().Evaluate(FlightCheckTests.Context(879788), []);
        var metres = int.Parse(Line(verdict, "takeoffFromIntersection").Values!["metres"], System.Globalization.CultureInfo.InvariantCulture);

        Assert.InRange(metres, 250, 400);
    }

    [Fact]
    public void AFreeSpeedExemptionSoftens250KtUnlessThePositionWasNotOnline()
    {
        var context = FlightCheckTests.Context(877596);
        var parameters = Read(CheckCatalog.Speed250, "{}");
        AtcExemptionDto Exemption(ExemptionStatus status) =>
            new("EGJJ_APP", ExemptionKind.FreeSpeed, null, status, ExemptionCatalog.Softens(ExemptionKind.FreeSpeed));

        var online = new Speed250Check().Evaluate(context with { Exemptions = [Exemption(ExemptionStatus.Online)] }, parameters);
        var unverifiable = new Speed250Check().Evaluate(context with { Exemptions = [Exemption(ExemptionStatus.Unverifiable)] }, parameters);
        var notOnline = new Speed250Check().Evaluate(context with { Exemptions = [Exemption(ExemptionStatus.NotOnline)] }, parameters);
        var otherKind = new Speed250Check().Evaluate(
            context with { Exemptions = [new("EGJJ_APP", ExemptionKind.DirectRouting, null, ExemptionStatus.Online, [])] },
            parameters);

        Assert.Equal(CheckOutcome.Passed, online.Outcome);
        Assert.Equal("EGJJ_APP", Line(online, "speed250Exempted").Values!["callsign"]);
        Assert.Equal(CheckOutcome.Passed, unverifiable.Outcome);
        Assert.Equal(CheckOutcome.Failed, notOnline.Outcome);
        Assert.Contains(notOnline.Evidence, line => line.Key == Key("speed250ExemptionNotOnline"));
        Assert.Equal(CheckOutcome.Failed, otherKind.Outcome);
    }

    [Fact]
    public void TheSpeedOfTheWorstPointIsEstimatedAndSaidSo()
    {
        var verdict = new Speed250Check().Evaluate(FlightCheckTests.Context(877464), Read(CheckCatalog.Speed250, "{}"));

        Assert.Equal("322", Line(verdict, "speed250Exceeded").Values!["ias"]);
        Assert.Equal("260", Line(verdict, "speed250Exceeded").Values!["limit"]);
        Assert.Contains(verdict.Evidence, line => line.Key == Key("speedEstimated"));
    }

    [Fact]
    public void TheSimRateOf877196IsAboutFour()
    {
        var verdict = new SimRateCheck().Evaluate(FlightCheckTests.Context(877196), Read(CheckCatalog.SimRate, "{}"));

        var ratio = double.Parse(Line(verdict, "simRateFaster").Values!["ratio"], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(ratio, 3.5, 4.5);
    }

    [Fact]
    public void AVfrFlightIsJudgedOnTheMetarsItHasAndTheOtherEndIsSaid()
    {
        // SCI044 from Pisa, which publishes no METAR, to Bergamo at 09:05, where the 08:50 said 9000 m and BKN030.
        var context = FlightCheckTests.Context(880760);

        var met = new VmcCheck().Evaluate(context, Read(CheckCatalog.Vmc, "{}"));
        var strict = new VmcCheck().Evaluate(context, Read(CheckCatalog.Vmc, """{"minVisibilityMeters":9500}"""));
        var none = new VmcCheck().Evaluate(context with { Metars = [] }, Read(CheckCatalog.Vmc, "{}"));

        Assert.Equal(CheckOutcome.Passed, met.Outcome);
        Assert.Equal("LIPN", Line(met, "vmcNoMetar").Values!["airport"]);
        Assert.Equal("3000", Line(met, "vmcMet").Values!["ceiling"]);
        Assert.Equal(CheckOutcome.Failed, strict.Outcome);
        Assert.Equal(CheckOutcome.Unavailable, none.Outcome);
    }

    [Fact]
    public void AnIfrFlightHasNoVfrPartToJudge()
    {
        var verdict = new VmcCheck().Evaluate(FlightCheckTests.Context(879610), Read(CheckCatalog.Vmc, "{}"));

        Assert.Equal(CheckOutcome.Passed, verdict.Outcome);
        Assert.Equal("I", Line(verdict, "vmcNotVfr").Values!["rules"]);
    }

    [Fact]
    public void TheHighestAltitudeIsReadWithTheLimitOfThePlansRules()
    {
        // N260MA, VFR, climbed to 8608 ft.
        var context = FlightCheckTests.Context(881923);

        var kept = new MaxAltitudeCheck().Evaluate(context, Read(CheckCatalog.MaxAltitude, "{}"));
        var lower = new MaxAltitudeCheck().Evaluate(context, Read(CheckCatalog.MaxAltitude, """{"maxFeetV":8000}"""));

        Assert.Equal(CheckOutcome.Passed, kept.Outcome);
        Assert.Equal("19500", Line(kept, "maxAltitudeKept").Values!["limit"]);
        Assert.Equal(CheckOutcome.Failed, lower.Outcome);
        Assert.Equal("8608", Line(lower, "maxAltitudeExceeded").Values!["altitude"]);
    }

    [Fact]
    public void TheLandingIsTheLastOneAndATouchAndGoOnTheWayIsNot()
    {
        // ICELLO touched down 28 NM before Lošinj and went on: the landing is the one at the end.
        var verdict = new LandingAtArrivalCheck().Evaluate(FlightCheckTests.Context(882171), Read(CheckCatalog.LandingAtArrival, "{}"));

        Assert.Equal(CheckOutcome.Passed, verdict.Outcome);
        Assert.Equal("LDLO", Line(verdict, "landedAt").Values!["airport"]);
    }

    [Fact]
    public void TheFirstFlightOfADiversionIsMeantToLandAtTheDiversion()
    {
        // The same flight into Lošinj, reported as a diversion from a leg to Pula: it landed where it said.
        var context = FlightCheckTests.Context(882171) with { IsDiversion = true, DiversionIcao = "LDLO" };
        var diverted = context with { Leg = context.Leg with { ArrivalIcao = "LDPL" } };
        var notDiverted = diverted with { IsDiversion = false, DiversionIcao = null };

        Assert.Equal(CheckOutcome.Passed, new LandingAtArrivalCheck().Evaluate(diverted, Read(CheckCatalog.LandingAtArrival, "{}")).Outcome);
        Assert.Equal(CheckOutcome.Unavailable, new LandingAtArrivalCheck().Evaluate(notDiverted, Read(CheckCatalog.LandingAtArrival, "{}")).Outcome);
    }

    [Fact]
    public void ASessionThatEndsInTheAirDidNotLandAndIsADisconnection()
    {
        var context = FlightCheckTests.Context(879610);
        var flight = context.Flights[0];
        var cut = context with { Flights = [flight with { Track = [.. flight.Track!.Take(flight.Track!.Count / 2)], LandingAt = null }] };

        var landing = new LandingAtArrivalCheck().Evaluate(cut, Read(CheckCatalog.LandingAtArrival, "{}"));
        var disconnections = new DisconnectionsCheck().Evaluate(cut, Read(CheckCatalog.Disconnections, "{}"));

        Assert.Equal(CheckOutcome.Failed, landing.Outcome);
        Assert.Contains(landing.Evidence, line => line.Key == Key("noLanding"));
        Assert.Equal(CheckOutcome.Failed, disconnections.Outcome);
        Assert.Contains(disconnections.Evidence, line => line.Key == Key("sessionEndedInFlight"));
    }

    [Fact]
    public void AFlightWithoutItsTrackIsNotAvailable()
    {
        var context = FlightCheckTests.Context(879610);
        var bare = context with { Flights = [context.Flights[0] with { Track = null }] };

        foreach (var check in All.Where(check => check.Key != CheckCatalog.Vmc))
        {
            var verdict = check.Evaluate(bare, Read(check.Key, "{}"));
            Assert.Equal(CheckOutcome.Unavailable, verdict.Outcome);
            Assert.Equal(Key("noTrack"), Assert.Single(verdict.Evidence).Key);
        }
    }

    // ─── The pieces ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("METAR LIME 180850Z 01004KT 340V060 9000 BKN030 19/17 Q1016 RERA NOSIG", 9000, 3000)]
    [InlineData("METAR LJPZ 232200Z AUTO 12008KT 9999 NCD 12/10 Q1023", 10_000, null)]
    [InlineData("METAR LIRF 011020Z 27010KT CAVOK 25/12 Q1015", 10_000, null)]
    [InlineData("METAR LIME 180720Z VRB02KT 2000 RA BR FEW010 OVC030 18/17 Q1015 NOSIG", 2000, 3000)]
    [InlineData("METAR LIMC 011020Z 00000KT 0150 R35L/0400N FG VV001 08/08 Q1020 BECMG 3000", 150, 100)]
    [InlineData("METAR KJFK 011051Z 18012KT 1 1/2SM BR OVC008 18/17 A2992 RMK AO2", 2414, 800)]
    [InlineData("METAR KSFO 011056Z 28015KT P6SM FEW010 SCT200 18/12 A3001", 9656, null)]
    [InlineData("METAR EGLL 011020Z 22010KT 4000NE -RA SCT012 BKN020 OVC/// 12/10 Q1004 TEMPO 2000", 4000, 0)]
    public void AMetarGivesItsVisibilityAndItsCeiling(string raw, int visibility, int? ceiling)
    {
        var weather = MetarReading.Read(raw);

        Assert.NotNull(weather);
        Assert.Equal(visibility, weather.VisibilityMetres);
        Assert.Equal(ceiling, weather.CeilingFeet);
    }

    [Fact]
    public void AMetarWithoutAVisibilityIsNotRead() => Assert.Null(MetarReading.Read("METAR LIRF 011020Z NIL"));

    [Theory]
    [InlineData(250, 0, 250)]
    [InlineData(290, 9000, 253)]
    [InlineData(346, 5343, 320)]
    public void TheIndicatedSpeedIsTheGroundSpeedInTheStandardAtmosphere(int groundSpeed, int altitude, int indicated) =>
        Assert.InRange(Speed250Check.IndicatedKt(groundSpeed, altitude), indicated - 1.5, indicated + 1.5);

    [Fact]
    public void TheStartingValuesOfMaxAltitudeAreTodaysSystem()
    {
        var parameters = Read(CheckCatalog.MaxAltitude, "{}");

        Assert.Equal(19_500, OpenCatalog.Number(parameters, "maxFeetV"));
        Assert.Equal([66_000, 66_000, 66_000], new[] { "I", "Y", "Z" }.Select(rules => OpenCatalog.Number(parameters, CheckCatalog.MaxFeetFor(rules))));
    }

    [Fact]
    public void AReportWithTwoFlightsSaysWhichFlightEachLineIsAbout()
    {
        var context = FlightCheckTests.Context(879610);
        var second = context.Flights[0] with { Seq = 2 };
        var twice = context with { Flights = [context.Flights[0], second] };

        var verdict = new MaxAltitudeCheck().Evaluate(twice, Read(CheckCatalog.MaxAltitude, "{}"));

        Assert.Equal(["1", "2"], verdict.Evidence.Select(line => line.Values!["flight"]));
    }

    private static JsonObject Read(string key, string json) => CheckCatalog.Read(key, JsonNode.Parse(json), amending: false).Parameters;

    private static string Key(string key) => $"flightops:evidence.{key}";

    private static EvidenceLine Line(CheckVerdict verdict, string key) => verdict.Evidence.First(line => line.Key == Key(key));
}
