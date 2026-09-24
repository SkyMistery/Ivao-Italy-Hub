using System.Text.Json.Nodes;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Weather;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>What a check found (design M2 §6.2). Stored by name.</summary>
public enum CheckOutcome
{
    Passed,

    /// <summary>Its errors become suggestions (§6.1); the validator decides (§6.3).</summary>
    Failed,

    /// <summary>The data it reads is missing, or it could not run: never a failure.</summary>
    Unavailable,
}

/// <summary>Who ran a check: the server, or the agent on the validator's computer (§6.6, T19). Stored by name.</summary>
public enum CheckRanBy
{
    Server,
    Agent,
}

/// <summary>
/// One line of what a check saw. The server writes an i18n key with its values, which the page words in the reader's
/// language; the agent writes text (note 2026-09-15-token-personali-e-agente-del-validatore §3).
/// </summary>
public sealed record EvidenceLine(string? Key, IReadOnlyDictionary<string, string>? Values = null, string? Text = null)
{
    public static EvidenceLine Of(string key, params (string Name, string Value)[] values) =>
        new($"flightops:evidence.{key}", values.Length == 0 ? null : values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal));
}

/// <summary>A check's outcome and the lines that say why.</summary>
public sealed record CheckVerdict(CheckOutcome Outcome, IReadOnlyList<EvidenceLine> Evidence)
{
    public static CheckVerdict Passed(params EvidenceLine[] evidence) => new(CheckOutcome.Passed, evidence);

    public static CheckVerdict Failed(params EvidenceLine[] evidence) => new(CheckOutcome.Failed, evidence);

    public static CheckVerdict Unavailable(params EvidenceLine[] evidence) => new(CheckOutcome.Unavailable, evidence);

    /// <summary>Failed when any line is a failure, passed otherwise: a check that looks at several things says all of them.</summary>
    public static CheckVerdict Of(IReadOnlyList<(bool Failed, EvidenceLine Line)> lines) =>
        new(lines.Any(line => line.Failed) ? CheckOutcome.Failed : CheckOutcome.Passed, [.. lines.Select(line => line.Line)]);
}

/// <summary>
/// One automatic check the server runs (design M2 §6.2), one per key of <see cref="Rules.CheckCatalog"/>, which holds its
/// parameters and their starting values — one catalogue, not a second one next to the code. A pure function of what the
/// report and its surroundings were: <see cref="FlightChecks"/> gathers them, so a check reads no database and calls no
/// network, and the corpus of real flights proves it without a server (T17 chose that over the design's
/// <c>EvaluateAsync</c>).
/// </summary>
public interface IFlightCheck
{
    string Key { get; }

    /// <param name="context">What the report was, gathered once by the engine.</param>
    /// <param name="parameters">The parameters of the rule that names it, as the report froze them.</param>
    CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters);
}

/// <summary>One flight of the report as the checks read it: the plan at take-off, every revision, the track when kept.</summary>
public sealed record CheckedFlight(
    int Seq,
    string Callsign,
    string? Aircraft,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime TakeoffAt,
    DateTime? LandingAt,
    IReadOnlyList<IvaoFlightPlanDto> Plans,
    IvaoFlightPlanDto? PlanAtTakeoff,
    IReadOnlyList<IvaoTrackPointDto>? Track)
{
    /// <summary>Whether the plan the checks read was filed before the wheels left the ground (GR9).</summary>
    public bool HasPlanBeforeTakeoff => PlanAtTakeoff is { } plan && plan.FiledAt <= TakeoffAt;
}

/// <summary>
/// Everything a check may read (design M2 §6.2): the report and the leg it froze, its flights, and what the tour said when it
/// was judged — the callsign rules by level, the aircraft allowed, the routes the pilot already flew on a tour that counts
/// them — and the module's settings. The checks on the tracks (T18) add where the airports are and their runway ends, the
/// METARs kept for the flight and the exemptions the pilot declared; each starts empty, which the checks read as «not
/// available».
/// </summary>
public sealed record FlightCheckContext(
    long PirepId,
    TourKind TourKind,
    SnapshotLegDto Leg,
    bool IsDiversion,
    IReadOnlyList<CheckedFlight> Flights,
    IReadOnlyList<IReadOnlyList<CallsignRule>> CallsignLevels,
    IReadOnlySet<string>? AllowedAircraft,
    IReadOnlyList<(string Departure, string Arrival)> RoutesFlown,
    FlightOpsSettings Settings)
{
    /// <summary>Where the first flight landed instead, on a report with a diversion.</summary>
    public string? DiversionIcao { get; init; }

    /// <summary>The airports of the report by ICAO, with their position when the reference data has one.</summary>
    public IReadOnlyDictionary<string, AirportDto> Airports { get; init; } = new Dictionary<string, AirportDto>();

    /// <summary>The runway ends of the report's airports by ICAO: the thresholds <c>takeoffFromThreshold</c> measures from.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IvaoRunway>> Runways { get; init; } = new Dictionary<string, IReadOnlyList<IvaoRunway>>();

    /// <summary>The METARs kept for the report's airports around its flights (T16): what <c>vmc</c> reads.</summary>
    public IReadOnlyList<WeatherReport> Metars { get; init; } = [];

    /// <summary>The exemptions the pilot declared, with their status and the checks they soften as the send froze them.</summary>
    public IReadOnlyList<AtcExemptionDto> Exemptions { get; init; } = [];

    /// <summary>Where a flight was meant to land: the diversion airport for the first flight of a diversion, the leg's arrival otherwise.</summary>
    public string ExpectedArrival(CheckedFlight flight)
    {
        ArgumentNullException.ThrowIfNull(flight);
        return IsDiversion && flight.Seq == 1 && DiversionIcao is { Length: > 0 } diversion ? diversion : Leg.ArrivalIcao;
    }
}

/// <summary>
/// What a check found on a report (design M2 §6.1), <c>fo_check_results</c>: one row per check and per runner — the server's
/// replaced when it runs again, the agent's when it sends the same check again (T19).
/// <para>Not audited (Carmine, 24 September 2026, note 2026-09-24-il-contratto-dell-agente): a result is a suggestion, and the
/// decision that follows is what counts. An agent's row says itself who sent it — the VID, the token, the program's version —,
/// which is what revoking the right token needs.</para>
/// </summary>
public sealed class CheckResult
{
    public long Id { get; set; }

    public long PirepId { get; set; }

    public string CheckKey { get; set; } = string.Empty;

    public CheckOutcome Outcome { get; set; }

    /// <summary>A list of <see cref="EvidenceLine"/>.</summary>
    public string EvidenceJson { get; set; } = "[]";

    public CheckRanBy RanBy { get; set; }

    public DateTime RanAt { get; set; }

    /// <summary>The validator whose agent sent it; none for the server's.</summary>
    public int? ByVid { get; set; }

    /// <summary>The personal token it came with; none for the server's.</summary>
    public long? TokenId { get; set; }

    /// <summary>The version the agent said it was (<c>agentVersion</c>); none for the server's.</summary>
    public string? AgentVersion { get; set; }
}
