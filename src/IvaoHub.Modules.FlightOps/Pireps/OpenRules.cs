using System.Text.Json.Nodes;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// A flight reported on an <c>Open</c> tour, with what its filters ask about it: the countries of its airports, the longest
/// runway and the elevation of the arrival, the wake category of the aircraft. A fact the hub does not have is null, and a
/// filter that needs it lets the flight through — the validator judges what the hub cannot (the runways' rule, T1).
/// </summary>
public sealed record OpenFlight(
    string DepartureIcao,
    string ArrivalIcao,
    string? DepartureCountry,
    string? ArrivalCountry,
    double DepartureLongitude,
    double ArrivalLongitude,
    decimal DistanceNm,
    DateTime TakeoffAt,
    string? WakeCategory,
    string FlightRules,
    int? ArrivalLongestRunwayMetres,
    int? ArrivalElevationFeet);

/// <summary>What the goal of an <c>Open</c> tour needs of the airports touched: their country, and the regions they are in.</summary>
public sealed record OpenFacts(
    IReadOnlyDictionary<string, string> CountryOf,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RegionsOf)
{
    public static OpenFacts None { get; } = new(new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());
}

/// <summary>How far a pilot is towards the goal of an <c>Open</c> tour, and what is still missing at the completion.</summary>
public sealed record OpenProgress(int Done, int Target, IReadOnlyList<string> MissingMinFlightsAt)
{
    public bool Finished => Target > 0 && Done >= Target && MissingMinFlightsAt.Count == 0;
}

/// <summary>
/// The rules of a flight on an <c>Open</c> tour (design M2 §2.6.1): its filters and its sequence rules, which block the
/// report like the callsign and the aircraft do (§3.2 point 5), and the route already flown (<c>NoRepeatedRoute</c>, always
/// on; A→B and B→A are different routes, Carmine, 15 September 2026). Then the goal: how far the accepted flights got.
/// A pure function of the catalogue's parameters (<see cref="OpenCatalog"/>): one catalogue, read by the form and here.
/// <para><c>MinFlightsAt</c> is not a filter: it is asked at the completion.</para>
/// </summary>
public static class OpenRules
{
    /// <summary>
    /// What is wrong with the flight, as i18n keys; empty when nothing is. The reports are the pilot's on the tour without
    /// the one being corrected: the earlier flight a chain or a ladder is measured against is the last one before this
    /// take-off that is accepted or pending.
    /// </summary>
    public static IReadOnlyList<string> Problems(OpenFlight flight, IReadOnlyList<TourConstraint> constraints, IReadOnlyList<Pirep> reports)
    {
        ArgumentNullException.ThrowIfNull(flight);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(reports);

        var problems = new List<string>();
        var counted = reports.Where(report => Pirep.Counts(report.Status)).ToList();

        if (counted.Any(report => report.DepartureIcao == flight.DepartureIcao && report.ArrivalIcao == flight.ArrivalIcao))
        {
            problems.Add("flightops:errors.reportRouteRepeated");
        }

        var previous = counted
            .Where(report => report.TakeoffAt < flight.TakeoffAt)
            .OrderBy(report => report.TakeoffAt)
            .LastOrDefault();

        foreach (var constraint in constraints)
        {
            var parameters = OpenCatalog.Parse(constraint.ParametersJson);
            if (Problem(constraint.Kind, parameters, flight, previous) is { } key)
            {
                problems.Add(key);
            }
        }

        return [.. problems.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// How far the accepted flights got towards the goal (§2.6.1), and the airports of <c>MinFlightsAt</c> not yet visited
    /// often enough — the rule asked at the completion.
    /// </summary>
    public static OpenProgress Progress(
        OpenGoal goal,
        JsonObject parameters,
        IReadOnlyList<TourConstraint> constraints,
        IReadOnlyList<Pirep> reports,
        OpenFacts facts)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(facts);

        var accepted = reports.Where(report => report.Status == PirepStatus.Accepted).ToList();
        var touched = accepted
            .SelectMany(report => new[] { report.DepartureIcao, report.ArrivalIcao })
            .ToHashSet(StringComparer.Ordinal);
        var countries = touched
            .Select(icao => facts.CountryOf.GetValueOrDefault(icao))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var (done, target) = goal switch
        {
            OpenGoal.Distance => ((int)accepted.Sum(report => report.DistanceNm), OpenCatalog.Number(parameters, "nm") ?? 0),
            OpenGoal.FlightCount => (accepted.Count, OpenCatalog.Number(parameters, "count") ?? 0),
            OpenGoal.DistinctAirports => (touched.Count, OpenCatalog.Number(parameters, "count") ?? 0),
            OpenGoal.DistinctCountries => (countries.Count, OpenCatalog.Number(parameters, "count") ?? 0),
            OpenGoal.CollectList => Collected(OpenCatalog.Codes(parameters, "airports"), touched, parameters),
            OpenGoal.CollectRegions => Collected(Regions(parameters), Regions(parameters, countries, touched, facts), parameters),
            _ => (0, 0),
        };

        var missing = constraints
            .Where(constraint => constraint.Kind == TourConstraintKind.MinFlightsAt)
            .Select(constraint => OpenCatalog.Parse(constraint.ParametersJson))
            .Where(minimum =>
            {
                var airport = OpenCatalog.Codes(minimum, "airport").FirstOrDefault();
                var times = OpenCatalog.Number(minimum, "count") ?? 1;
                return airport is not null
                    && accepted.Count(report => report.DepartureIcao == airport || report.ArrivalIcao == airport) < times;
            })
            .Select(minimum => OpenCatalog.Codes(minimum, "airport")[0])
            .ToList();

        return new OpenProgress(done, target, missing);
    }

    private static string? Problem(TourConstraintKind kind, JsonObject parameters, OpenFlight flight, Pirep? previous)
    {
        var countries = OpenCatalog.Codes(parameters, "countries");

        return kind switch
        {
            TourConstraintKind.DepartureOrArrivalIn =>
                Unknown(flight.DepartureCountry) || Unknown(flight.ArrivalCountry)
                || countries.Contains(flight.DepartureCountry!) || countries.Contains(flight.ArrivalCountry!)
                    ? null
                    : "flightops:errors.reportCountries",
            TourConstraintKind.DepartureIn =>
                Unknown(flight.DepartureCountry) || countries.Contains(flight.DepartureCountry!) ? null : "flightops:errors.reportCountries",
            TourConstraintKind.ArrivalIn =>
                Unknown(flight.ArrivalCountry) || countries.Contains(flight.ArrivalCountry!) ? null : "flightops:errors.reportCountries",
            TourConstraintKind.TouchesAirport =>
                OpenCatalog.Codes(parameters, "airports") is var airports
                && (airports.Contains(flight.DepartureIcao) || airports.Contains(flight.ArrivalIcao))
                    ? null
                    : "flightops:errors.reportAirportNotTouched",
            TourConstraintKind.DistanceBetween =>
                (OpenCatalog.Number(parameters, "minNm") is not { } min || flight.DistanceNm >= min)
                && (OpenCatalog.Number(parameters, "maxNm") is not { } max || flight.DistanceNm <= max)
                    ? null
                    : "flightops:errors.reportDistance",
            TourConstraintKind.AircraftCategory =>
                Unknown(flight.WakeCategory) || OpenCatalog.Codes(parameters, "categories").Contains(flight.WakeCategory!)
                    ? null
                    : "flightops:errors.reportAircraftCategory",
            TourConstraintKind.ArrivalRunwayMax =>
                flight.ArrivalLongestRunwayMetres is not { } longest || OpenCatalog.Number(parameters, "meters") is not { } most || longest <= most
                    ? null
                    : "flightops:errors.reportRunwayTooLong",
            TourConstraintKind.ArrivalElevationMin =>
                flight.ArrivalElevationFeet is not { } elevation || OpenCatalog.Number(parameters, "feet") is not { } least || elevation >= least
                    ? null
                    : "flightops:errors.reportElevationTooLow",
            TourConstraintKind.FlightRules =>
                OpenCatalog.Codes(parameters, "rules").Contains(flight.FlightRules) ? null : "flightops:errors.reportFlightRules",
            TourConstraintKind.Chained =>
                previous is null || previous.ArrivalIcao == flight.DepartureIcao ? null : "flightops:errors.reportNotChained",
            TourConstraintKind.Eastbound =>
                Heading(flight) > 0 ? null : "flightops:errors.reportNotEastbound",
            TourConstraintKind.Westbound =>
                Heading(flight) < 0 ? null : "flightops:errors.reportNotWestbound",
            TourConstraintKind.IncreasingDistance =>
                previous is null || flight.DistanceNm > previous.DistanceNm ? null : "flightops:errors.reportDistanceNotIncreasing",
            _ => null,
        };
    }

    private static bool Unknown(string? fact) => string.IsNullOrEmpty(fact);

    /// <summary>
    /// East is positive: the difference of the longitudes brought into (−180, 180], so a flight across the antimeridian goes
    /// the short way round, as its great circle does.
    /// </summary>
    public static double Heading(OpenFlight flight)
    {
        ArgumentNullException.ThrowIfNull(flight);

        var delta = (flight.ArrivalLongitude - flight.DepartureLongitude) % 360;
        return delta switch
        {
            > 180 => delta - 360,
            <= -180 => delta + 360,
            _ => delta,
        };
    }

    private static (int Done, int Target) Collected(IReadOnlyList<string> wanted, IReadOnlySet<string> reached, JsonObject parameters)
    {
        var done = wanted.Count(reached.Contains);
        var target = OpenCatalog.Number(parameters, "count") is { } count ? Math.Min(count, wanted.Count) : wanted.Count;
        return (done, target);
    }

    /// <summary>The regions the goal lists: its countries, or its flight information regions — never both (note 2026-09-22).</summary>
    private static IReadOnlyList<string> Regions(JsonObject parameters) =>
        OpenCatalog.Codes(parameters, "countries") is { Count: > 0 } countries ? countries : OpenCatalog.Codes(parameters, "firs");

    private static HashSet<string> Regions(JsonObject parameters, IReadOnlySet<string> countries, IReadOnlySet<string> touched, OpenFacts facts) =>
        OpenCatalog.Codes(parameters, "countries").Count > 0
            ? [.. countries]
            : [.. touched.SelectMany(icao => facts.RegionsOf.GetValueOrDefault(icao) ?? [])];
}

/// <summary>
/// The daily limits (design M2 §3.6): they <b>block</b> the report. What counts is the pilot's reports neither rejected nor
/// withdrawn, by the <b>UTC day of the take-off</b> — the flights of a day, not the sends: with a limit of twelve, the
/// thirteenth flight of a day is refused whatever day it is reported on (Carmine, 15 September 2026). The tour's limit
/// counts the tour's reports, the division's every tour's.
/// </summary>
public static class DailyLimits
{
    /// <summary>Which limit the flight would go over, as an i18n key; null when it goes over none.</summary>
    /// <param name="takeoffAt">When the flight reported took off: its UTC day is the one counted.</param>
    /// <param name="tourId">The tour it is reported on.</param>
    /// <param name="tourLimit">The tour's own limit, if it has one.</param>
    /// <param name="divisionLimit">The division's, unless it is switched off (§3.7).</param>
    /// <param name="counted">The pilot's reports that count — on every tour — without the one being corrected.</param>
    public static string? Refusal(
        DateTime takeoffAt,
        long tourId,
        int? tourLimit,
        int? divisionLimit,
        IReadOnlyList<(long TourId, DateTime TakeoffAt)> counted)
    {
        ArgumentNullException.ThrowIfNull(counted);

        var day = takeoffAt.Date;
        var thatDay = counted.Where(report => report.TakeoffAt.Date == day).ToList();

        if (tourLimit is { } perTour && thatDay.Count(report => report.TourId == tourId) >= perTour)
        {
            return "flightops:errors.reportTourDailyLimit";
        }

        return divisionLimit is { } perDivision && thatDay.Count >= perDivision
            ? "flightops:errors.reportDivisionDailyLimit"
            : null;
    }
}
