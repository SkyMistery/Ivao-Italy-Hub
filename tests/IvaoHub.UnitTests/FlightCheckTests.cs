using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Weather;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The checks on the flight plan (M2, T17) on the <b>corpus</b>: the tours' own PIREPs of August and September 2026, recorded from
/// the tracker (<c>tracker-reports-780002.json</c>) with what the controllers found (note 2026-09-24-i-controlli-dai-pirep-veri
/// §5). Each flight goes through what a send does — the tracker's reader, <see cref="TrackedFlight"/>, the row the report keeps
/// — and the engine reads it back as it would. Then the pieces, one by one.
/// </summary>
public sealed class FlightCheckTests
{
    /// <summary>The checks on the plan, with the parameters each tour of the corpus gives them.</summary>
    private static readonly string[] PlanChecks =
    [
        CheckCatalog.Callsign, CheckCatalog.Aircraft, CheckCatalog.FlightRules, CheckCatalog.PlanAtTakeoff,
        CheckCatalog.FlightPlanForm, CheckCatalog.Alternate, CheckCatalog.Equipment, CheckCatalog.RepeatedRoute,
    ];

    private static readonly IFlightCheck[] All =
    [
        new CallsignCheck(), new AircraftCheck(), new FlightRulesCheck(), new PlanAtTakeoffCheck(), new FlightPlanFormCheck(),
        new AlternateCheck(), new EquipmentCheck(), new RepeatedRouteCheck(),
    ];

    /// <summary>What an IFR tour asks of the equipment, as Carmine gave the Turboprop's: S, D, G, R, W, Y for I and Y, none for V.</summary>
    private const string IfrEquipment = """{"lettersI":["S","D","G","R","W","Y"],"lettersY":["S","D","G","R","W","Y"]}""";

    // ─── The corpus ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(879788, "IFR", "equipment")] // Turboprop 5, B350 without D and Y
    [InlineData(879691, "IFR", "equipment")] // Turboprop 4
    [InlineData(879558, "IFR", "equipment")] // Turboprop 3
    [InlineData(882171, "VFR", "flightPlanForm")] // VFR 4, DCT in a VFR route
    [InlineData(880760, "VFR", "")] // VFR 24: rejected for a runway, a manoeuvre of the leg
    [InlineData(881923, "VFR", "flightPlanForm")] // VFR 22: F085 as the level of a VFR plan (Carmine, 24 September 2026: it fails)
    [InlineData(877464, "London City", "aircraft")] // Dangerous Airports 2, a B737 at London City
    [InlineData(877596, "IFR", "")] // Dangerous Airports 4: the speed is T18's
    [InlineData(877187, "IFR", "")] // Bizjet 8: the speed is T18's
    [InlineData(877196, "IFR", "")] // Ryanair Summer 19: the sim rate is T18's
    [InlineData(880159, "IFR", "")] // Ryanair Summer 19
    [InlineData(879610, "IFR", "")] // Volotea
    [InlineData(881263, "IFR", "")] // Lufthansa Group, a STAR in the route into Vienna
    [InlineData(881169, "IFR", "")] // Ryanair Summer 16
    [InlineData(876413, "IFR", "")] // Itavia
    public void EveryReportOfTheCorpusGivesWhatTheControllersFoundOnThePlan(long report, string tour, string failing)
    {
        var context = Context(report, tour == "London City" ? new HashSet<string>(["A318", "E190", "DH8D"]) : null);
        var parameters = new Dictionary<string, JsonObject>
        {
            [CheckCatalog.Equipment] = Read(CheckCatalog.Equipment, tour == "VFR" ? "{}" : IfrEquipment),
            [CheckCatalog.FlightRules] = Read(CheckCatalog.FlightRules, tour == "VFR" ? """{"rules":["V"]}""" : """{"rules":["I","Y"]}"""),
        };

        var failed = All
            .Where(check => check.Evaluate(context, parameters.GetValueOrDefault(check.Key) ?? Read(check.Key, "{}")).Outcome == CheckOutcome.Failed)
            .Select(check => check.Key)
            .ToList();

        Assert.Equal(failing.Length == 0 ? [] : failing.Split(','), failed);
        Assert.Equal(PlanChecks.Length, All.Select(check => check.Key).Intersect(PlanChecks).Count());
    }

    [Fact]
    public void TheTurbopropsB350IsMissingDAndYWhileWIsNotAskedBelowFl285()
    {
        var verdict = new EquipmentCheck().Evaluate(Context(879788), Read(CheckCatalog.Equipment, IfrEquipment));

        Assert.Equal(CheckOutcome.Failed, verdict.Outcome);
        Assert.Equal("D Y", Line(verdict, "equipmentMissing").Values!["letters"]);
        Assert.Equal("W", Line(verdict, "equipmentNotAbove").Values!["letters"]);
        Assert.Equal("250", Line(verdict, "equipmentNotAbove").Values!["level"]);
    }

    [Fact]
    public void AVfrPlanWithDirectsIsWrongAndTheLineSaysWhy()
    {
        var verdict = new FlightPlanFormCheck().Evaluate(Context(882171), []);

        Assert.Equal(CheckOutcome.Failed, verdict.Outcome);
        Assert.Contains(verdict.Evidence, line => line.Key == "flightops:evidence.vfrWithDct");
    }

    [Fact]
    public void TwoRevisionsFiledAfterTheTakeoffAreShownAndTheOneBeforeIsRead()
    {
        // SCI044 took off at 08:22 with revision 2; revisions 3 and 4 came at 08:39 and 08:44 (GR9: they do not count).
        var context = Context(880760);
        var verdict = new PlanAtTakeoffCheck().Evaluate(context, []);

        Assert.Equal(CheckOutcome.Passed, verdict.Outcome);
        Assert.Equal(2, context.Flights[0].PlanAtTakeoff!.Revision);
        Assert.Equal("2", Line(verdict, "planAtTakeoff").Values!["revision"]);
        Assert.Equal(["3", "4"], verdict.Evidence.Where(line => line.Key == "flightops:evidence.planRevisedAfterTakeoff").Select(line => line.Values!["revision"]));
    }

    [Fact]
    public void AStarInTheRouteIsRightIntoAnAirportWhoseCountryAsksForIt()
    {
        // ITY591 into Vienna ends its route with OBUTI2W: LO is in the setting. Without it, the same route is wrong.
        var context = Context(881263);
        Assert.Equal(CheckOutcome.Passed, new FlightPlanFormCheck().Evaluate(context, []).Outcome);

        var elsewhere = context with { Settings = new FlightOpsSettings { RouteProcedurePrefixes = ["ED"] } };
        var verdict = new FlightPlanFormCheck().Evaluate(elsewhere, []);
        Assert.Equal(CheckOutcome.Failed, verdict.Outcome);
        Assert.Equal("OBUTI2W", Line(verdict, "starInRoute").Values!["procedure"]);
    }

    [Fact]
    public void WAndJ1AreAskedAboveFl285FromTheRouteWhenTheTourListsThem()
    {
        // RYR73F files F180 but climbs to F380 in the route; its plan has W and not J1.
        var context = Context(880159);
        var parameters = Read(CheckCatalog.Equipment, """{"lettersI":["S","D","W","J1"]}""");

        var verdict = new EquipmentCheck().Evaluate(context, parameters);

        Assert.Equal(CheckOutcome.Failed, verdict.Outcome);
        Assert.Equal("J1", Line(verdict, "equipmentMissing").Values!["letters"]);
        Assert.Equal("380", Line(verdict, "equipmentAbove").Values!["level"]);
    }

    // ─── The pieces ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("F240", null, 240)]
    [InlineData("F180", "PAZZE Z854 BAGIX/N0459F380 DCT SOR", 380)]
    [InlineData("VFR", "DCT POREC", null)]
    [InlineData("A045", null, 45)]
    [InlineData("S1130", null, 371)]
    [InlineData("F090", "ORVAL V20 UPALO UN491 POGZI/N0466F370 DCT IBABA/N0462F390 T37 PAS/N0403F190", 390)]
    public void ThePlannedLevelIsTheHighestOfItem15AndOfTheRoute(string level, string? route, int? expected) =>
        Assert.Equal(expected, FlightPlanText.HighestFlightLevel(level, route));

    [Theory]
    [InlineData("RYR2599", CallsignShape.Airline)]
    [InlineData("LIT234S", CallsignShape.Airline)]
    [InlineData("ICELLO", CallsignShape.Registration)]
    [InlineData("N260MA", CallsignShape.Registration)]
    [InlineData("IKSZA", CallsignShape.Registration)]
    [InlineData("9HABC", CallsignShape.Unclear)]
    public void ACallsignSaysWhetherTheAircraftNeedsARegistration(string callsign, CallsignShape shape) =>
        Assert.Equal(shape, FlightPlanText.ShapeOf(callsign));

    [Fact]
    public void LettersWithADigitAreOneLetter() =>
        Assert.Equal(["S", "A", "D", "F", "G", "H", "I", "J1", "J7", "L", "P2", "R", "W", "X", "Y", "Z"], FlightPlanText.Letters("SADFGHIJ1J7LP2RWXYZ"));

    [Theory]
    [InlineData("S", "L", true)] // a Mode S with ADS-B and enhanced surveillance is a Mode S
    [InlineData("C", "H", true)]
    [InlineData("S", "C", false)]
    [InlineData("B1", "B1L", true)]
    public void ATransponderThatSaysMoreStandsForTheOneItContains(string required, string filed, bool passes)
    {
        var context = WithPlan(Plan(transponder: filed));
        var verdict = new EquipmentCheck().Evaluate(context, Read(CheckCatalog.Equipment, $$"""{"transponderI":["{{required}}"]}"""));

        Assert.Equal(passes ? CheckOutcome.Passed : CheckOutcome.Failed, verdict.Outcome);
    }

    [Theory]
    [InlineData(null, "", "alternateMissing", true)]
    [InlineData("LIMC", "", "alternateIsArrival", true)]
    [InlineData("LIRF", "", "alternateIsDeparture", false)]
    [InlineData("ZZZZ", "RMK/TCAS", "alternateUnnamedWithoutAltn", true)]
    [InlineData("ZZZZ", "ALTN/CAMPO SPORTIVO", "alternateUnnamed", false)]
    public void TheAlternateIsNamedAndIsNotTheDestination(string? alternate, string remarks, string key, bool fails)
    {
        var verdict = new AlternateCheck().Evaluate(WithPlan(Plan(alternate: alternate, remarks: remarks)), []);

        Assert.Equal(fails ? CheckOutcome.Failed : CheckOutcome.Passed, verdict.Outcome);
        Assert.Equal($"flightops:evidence.{key}", Assert.Single(verdict.Evidence).Key);
    }

    [Theory]
    [InlineData("SDGRWY", "PBN/A1B1 REG/EIABC", "planFormRight")]
    [InlineData("SDGRWY", "PBN/A1B1", "regMissing")]
    [InlineData("SDGRWY", "PBN/A1B1 REG/EIABC RMK TCAS", "rmkWithoutSlash")]
    [InlineData("SDGWY", "REG/EIABC", "planFormRight")]
    [InlineData("SDGWYZ", "REG/EIABC", "zWithoutDetails")]
    [InlineData("SDGRWY", "REG/EIABC", "rWithoutPbn")]
    public void TheFormOfThePlanIsJudgedLineByLine(string equipment, string remarks, string key)
    {
        var verdict = new FlightPlanFormCheck().Evaluate(WithPlan(Plan(equipment: equipment, remarks: remarks)), []);

        Assert.Equal(key == "planFormRight" ? CheckOutcome.Passed : CheckOutcome.Failed, verdict.Outcome);
        Assert.Contains(verdict.Evidence, line => line.Key == $"flightops:evidence.{key}");
    }

    [Fact]
    public void ACheckThatBreaksIsUnavailableAndNeverFailed()
    {
        var engine = new FlightChecks(null!, [new Broken()], null!, null!, null!, null!, NullLogger<FlightChecks>.Instance);
        var found = engine.Evaluate(new Dictionary<string, JsonObject> { [CheckCatalog.Callsign] = [] }, WithPlan(Plan()), []);

        Assert.Equal(CheckOutcome.Unavailable, found[CheckCatalog.Callsign].Outcome);
    }

    [Fact]
    public void TheErrorsOfAFailedCheckAreSuggestedAndAConfirmedOneStays()
    {
        var equipment = new SnapshotErrorDto(1, Name("Equipment"), ErrorCategory.Warning, 3, CheckCatalog.Equipment);
        var alternate = new SnapshotErrorDto(2, Name("Alternate"), ErrorCategory.Warning, 3, CheckCatalog.Alternate);
        var manual = new SnapshotErrorDto(3, Name("Procedures"), ErrorCategory.Dangerous, null);
        var rules = new[] { new SnapshotRuleDto(1, null, "GR1", Name("GR1"), Name("GR1"), null, [], [equipment, alternate, manual]) };

        // A report sent again after «to modify»: the procedures confirmed then, an alternate the checks no longer suggest.
        var pirep = new Pirep
        {
            Errors =
            [
                new PirepError { ErrorId = 3, Confirmed = true },
                new PirepError { ErrorId = 2, SuggestedByCheck = true },
            ],
        };

        FlightChecks.Suggest(pirep, rules, [CheckCatalog.Equipment]);

        Assert.Equal([3L, 1L], pirep.Errors.Select(error => error.ErrorId));
        Assert.True(pirep.Errors[1].SuggestedByCheck);
        Assert.False(pirep.Errors[1].Confirmed);
        Assert.False(pirep.Errors[0].SuggestedByCheck);
    }

    // ─── Builders ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A report of the corpus as the engine reads it back after the send: with the position and the runway ends of its
    /// airports (<c>airports-corpus.json</c>) and the METARs NOAA kept of the VFR flights (<c>metars-corpus.json</c>), T18.
    /// </summary>
    internal static FlightCheckContext Context(long report, IReadOnlySet<string>? allowed = null)
    {
        var sessionId = Fixture("tracker-reports-780002.json").EnumerateArray()
            .Single(row => row.GetProperty("report").GetInt64() == report)
            .GetProperty("sessionIds")[0].GetInt64();
        var session = IvaoTrackerReader.ReadSessions(Fixture("tracker-sessions-780002.json")).Sessions.Single(row => row.Id == sessionId);
        var plans = IvaoTrackerReader.ReadFlightPlans(Fixture($"tracker-flightplans-{sessionId}.json"));
        var track = IvaoTrackerReader.ReadTracks(Fixture($"tracker-tracks-{sessionId}.json"));
        var flight = TrackedFlight.Read(session, plans, track);

        // The row the send writes (PirepSubmission): the plans as IVAO gave them, the revision valid at take-off.
        var row = new PirepFlight
        {
            Seq = 1,
            TrackerSessionId = sessionId,
            Callsign = CallsignRules.Normalize(session.Callsign),
            Aircraft = flight.Aircraft,
            DepartureIcao = flight.DepartureIcao,
            ArrivalIcao = flight.ArrivalIcao,
            TakeoffAt = flight.TakeoffAt!.Value,
            LandingAt = flight.LandingAt,
            FlightPlansJson = "[" + string.Join(',', flight.Plans.Select(plan => plan.RawJson)) + "]",
            PlanAtTakeoffRevision = flight.PlanAtTakeoff?.Revision,
        };

        return new FlightCheckContext(
            report,
            TourKind.Sequential,
            new SnapshotLegDto(1, 1, flight.DepartureIcao, flight.ArrivalIcao, 100, [], AllowedAircraft.All),
            IsDiversion: false,
            [FlightChecks.Checked(row, track)],
            [[], [], []],
            allowed,
            [],
            new FlightOpsSettings())
        {
            Airports = Corpus.Airports,
            Runways = Corpus.Runways,
            Metars = Corpus.Metars,
        };
    }

    /// <summary>The airports and the weather of the corpus, read once.</summary>
    private static class Corpus
    {
        public static readonly IReadOnlyDictionary<string, AirportDto> Airports = Fixture("airports-corpus.json").EnumerateArray()
            .ToDictionary(
                airport => airport.GetProperty("icao").GetString()!,
                airport => new AirportDto(
                    airport.GetProperty("icao").GetString()!,
                    null,
                    airport.GetProperty("icao").GetString()!,
                    "XX",
                    airport.GetProperty("latitude").GetDouble(),
                    airport.GetProperty("longitude").GetDouble(),
                    airport.GetProperty("elevation").GetInt32()));

        public static readonly IReadOnlyDictionary<string, IReadOnlyList<IvaoRunway>> Runways = Fixture("airports-corpus.json").EnumerateArray()
            .ToDictionary(
                airport => airport.GetProperty("icao").GetString()!,
                airport => (IReadOnlyList<IvaoRunway>)[
                    .. airport.GetProperty("runways").EnumerateArray().Select(runway => new IvaoRunway
                    {
                        AirportIcao = airport.GetProperty("icao").GetString()!,
                        Designator = runway.GetProperty("runway").GetString()!,
                        LengthMetres = runway.GetProperty("length").GetInt32(),
                        Bearing = runway.GetProperty("bearing").GetInt32(),
                        Latitude = runway.GetProperty("latitude").GetDouble(),
                        Longitude = runway.GetProperty("longitude").GetDouble(),
                    }),
                ]);

        public static readonly IReadOnlyList<WeatherReport> Metars =
        [
            .. Fixture("metars-corpus.json").EnumerateArray().Select(metar => new WeatherReport(
                metar.GetProperty("icao").GetString()!,
                WeatherReportKind.Metar,
                metar.GetProperty("issuedAt").GetDateTime().ToUniversalTime(),
                metar.GetProperty("raw").GetString()!,
                "noaa")),
        ];
    }

    private static FlightCheckContext WithPlan(IvaoFlightPlanDto plan) =>
        new(
            1,
            TourKind.Sequential,
            new SnapshotLegDto(1, 1, plan.DepartureIcao, plan.ArrivalIcao, 100, [], AllowedAircraft.All),
            IsDiversion: false,
            [new CheckedFlight(1, "EIN123", "A320", plan.DepartureIcao, plan.ArrivalIcao, Takeoff, Takeoff.AddHours(1), [plan], plan, null)],
            [[], [], []],
            null,
            [],
            new FlightOpsSettings());

    private static readonly DateTime Takeoff = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

    private static IvaoFlightPlanDto Plan(
        string equipment = "SDGRWY",
        string transponder = "S",
        string? alternate = "LIPE",
        string remarks = "PBN/A1B1 REG/EIABC") =>
        new(1, 1, Takeoff.AddMinutes(-30), "LIRF", "LIMC", alternate, null, "A320", "M", equipment, transponder, "I", "S",
            "F340", "N0450", "TIBER DCT ELKAP", remarks, null, null, "{}");

    private static JsonObject Read(string key, string json) => CheckCatalog.Read(key, JsonNode.Parse(json), amending: false).Parameters;

    private static EvidenceLine Line(CheckVerdict verdict, string key) =>
        verdict.Evidence.First(line => line.Key == $"flightops:evidence.{key}");

    private static Localized<string> Name(string text) => new(new Dictionary<string, string> { ["en"] = text });

    internal static JsonElement Fixture(string name)
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

    private sealed class Broken : IFlightCheck
    {
        public string Key => CheckCatalog.Callsign;

        public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters) => throw new InvalidOperationException("broken");
    }
}
