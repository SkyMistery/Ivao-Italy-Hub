using System.Globalization;
using System.Text.Json.Nodes;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>
/// The checks on the flight plan (design M2 §6.4, T17): they read the revision valid at take-off of every flight of the
/// report — the one before the wheels left the ground (GR9) — and say, line by line, what they saw. A report with a
/// diversion has two flights, and every line says which one.
/// </summary>
internal static class PlanLines
{
    /// <summary>The lines of every flight with a plan; a flight without one says so, and a report without any is unavailable.</summary>
    public static CheckVerdict PerFlight(
        FlightCheckContext context,
        Func<CheckedFlight, IvaoFlightPlanDto, IEnumerable<(bool Failed, EvidenceLine Line)>> judge)
    {
        if (context.Flights.All(flight => flight.PlanAtTakeoff is null))
        {
            return CheckVerdict.Unavailable(EvidenceLine.Of("noPlan"));
        }

        var lines = new List<(bool, EvidenceLine)>();
        foreach (var flight in context.Flights)
        {
            lines.AddRange(flight.PlanAtTakeoff is { } plan
                ? judge(flight, plan).Select(line => (line.Failed, Numbered(line.Line, flight, context)))
                : [(false, Numbered(EvidenceLine.Of("noPlan"), flight, context))]);
        }

        return CheckVerdict.Of(lines);
    }

    /// <summary>The flight a line is about, when the report has more than one.</summary>
    public static EvidenceLine Numbered(EvidenceLine line, CheckedFlight flight, FlightCheckContext context)
    {
        if (context.Flights.Count < 2)
        {
            return line;
        }

        var values = new Dictionary<string, string>(line.Values ?? new Dictionary<string, string>(), StringComparer.Ordinal)
        {
            ["flight"] = flight.Seq.ToString(CultureInfo.InvariantCulture),
        };
        return line with { Values = values };
    }

    /// <summary><c>I</c>, <c>V</c>, <c>Y</c> or <c>Z</c>; <c>I</c> when the plan does not say, the strictest reading (as the send).</summary>
    public static string Rules(IvaoFlightPlanDto plan) =>
        plan.FlightRules is { Length: > 0 } rules && "IVYZ".Contains(rules[0], StringComparison.Ordinal) ? rules[..1] : "I";

    public static string Join(IEnumerable<string> letters) => string.Join(' ', letters);
}

/// <summary>The callsign flown is one the tour allows (§1.6); refused at the send too, so it fails only if the rules changed.</summary>
public sealed class CallsignCheck : IFlightCheck
{
    public string Key => CheckCatalog.Callsign;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CheckVerdict.Of([
            .. context.Flights.Select(flight =>
            {
                var verdict = CallsignRules.Judge(flight.Callsign, context.CallsignLevels);
                var key = verdict switch
                {
                    CallsignVerdict.Denied => "callsignDenied",
                    CallsignVerdict.NotAllowed => "callsignNotAllowed",
                    _ => "callsignAllowed",
                };
                return (verdict != CallsignVerdict.Allowed, PlanLines.Numbered(EvidenceLine.Of(key, ("callsign", flight.Callsign)), flight, context));
            }),
        ]);
    }
}

/// <summary>The aircraft is one the leg admits, groups expanded (§2.7); refused at the send too.</summary>
public sealed class AircraftCheck : IFlightCheck
{
    public string Key => CheckCatalog.Aircraft;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AllowedAircraft is not { } allowed)
        {
            return CheckVerdict.Passed(EvidenceLine.Of("aircraftAny"));
        }

        return CheckVerdict.Of([
            .. context.Flights.Select(flight =>
            {
                var type = flight.Aircraft ?? "?";
                var admitted = flight.Aircraft is { } aircraft && allowed.Contains(aircraft);
                var key = admitted ? "aircraftAllowed" : "aircraftNotAllowed";
                return (!admitted, PlanLines.Numbered(EvidenceLine.Of(key, ("aircraft", type)), flight, context));
            }),
        ]);
    }
}

/// <summary>
/// The plan names an alternate other than the destination (note 2026-09-24-i-controlli-dai-pirep-veri §4); the departure is
/// said and not failed. <c>ZZZZ</c> is an airfield without a code only with <c>ALTN/</c> in item 18 (design M2 §6.4).
/// </summary>
public sealed class AlternateCheck : IFlightCheck
{
    public const string Unnamed = "ZZZZ";

    public string Key => CheckCatalog.Alternate;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters) =>
        PlanLines.PerFlight(context, (_, plan) => [Judge(plan)]);

    private static (bool, EvidenceLine) Judge(IvaoFlightPlanDto plan)
    {
        if (plan.AlternateIcao is not { } alternate)
        {
            return (true, EvidenceLine.Of("alternateMissing"));
        }

        if (alternate == Unnamed)
        {
            return FlightPlanText.HasIndicator(plan.Remarks, "ALTN")
                ? (false, EvidenceLine.Of("alternateUnnamed"))
                : (true, EvidenceLine.Of("alternateUnnamedWithoutAltn"));
        }

        if (alternate == plan.ArrivalIcao)
        {
            return (true, EvidenceLine.Of("alternateIsArrival", ("alternate", alternate)));
        }

        return alternate == plan.DepartureIcao
            ? (false, EvidenceLine.Of("alternateIsDeparture", ("alternate", alternate)))
            : (false, EvidenceLine.Of("alternateNamed", ("alternate", alternate)));
    }
}

/// <summary>
/// The letters of items 10a and 10b the rule requires with the plan's flight rules (Carmine, 24 September 2026). The letters
/// of <c>highLevelLetters</c> — W and J1 to start with — are required only when the plan asks for a level above
/// <c>highLevelFl</c>, in item 15 or in the route. A letter that says more stands for the one it contains: <c>S</c> in 10a
/// is VHF, VOR and ILS; a Mode S transponder with identity and altitude (<c>E</c>, <c>H</c>, <c>L</c>) is an <c>S</c>, and
/// with an <c>S</c> or <c>P</c> is a <c>C</c> (ICAO Doc 4444, appendix 2).
/// </summary>
public sealed class EquipmentCheck : IFlightCheck
{
    private static readonly Dictionary<string, string[]> Equipment = new(StringComparer.Ordinal)
    {
        ["V"] = ["S"],
        ["O"] = ["S"],
        ["L"] = ["S"],
    };

    private static readonly Dictionary<string, string[]> Transponder = new(StringComparer.Ordinal)
    {
        ["S"] = ["E", "H", "L"],
        ["C"] = ["S", "E", "H", "L", "P"],
    };

    public string Key => CheckCatalog.Equipment;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return PlanLines.PerFlight(context, (_, plan) => Judge(plan, parameters));
    }

    private static List<(bool, EvidenceLine)> Judge(IvaoFlightPlanDto plan, JsonObject parameters)
    {
        var rules = PlanLines.Rules(plan);
        var level = FlightPlanText.HighestFlightLevel(plan.Level, plan.Route);
        var above = OpenCatalog.Number(parameters, "highLevelFl") ?? 285;
        var high = level > above;
        var conditional = OpenCatalog.Codes(parameters, "highLevelLetters");

        var listed = OpenCatalog.Codes(parameters, CheckCatalog.LettersFor(rules));
        var required = listed.Where(letter => high || !conditional.Contains(letter)).ToList();
        var waived = listed.Except(required).ToList();
        var transponder = OpenCatalog.Codes(parameters, CheckCatalog.TransponderFor(rules));

        var missing = Missing(required, FlightPlanText.Letters(plan.Equipment), Equipment);
        var missingTransponder = Missing(transponder, FlightPlanText.Letters(plan.Transponder), Transponder);
        var filed = $"{plan.Equipment}/{plan.Transponder}";

        var lines = new List<(bool, EvidenceLine)>();
        if (missing.Count > 0)
        {
            lines.Add((true, EvidenceLine.Of("equipmentMissing", ("letters", PlanLines.Join(missing)), ("rules", rules), ("filed", filed))));
        }

        if (missingTransponder.Count > 0)
        {
            lines.Add((true, EvidenceLine.Of("transponderMissing", ("letters", PlanLines.Join(missingTransponder)), ("rules", rules), ("filed", filed))));
        }

        if (lines.Count == 0)
        {
            lines.Add(required.Count + transponder.Count == 0
                ? (false, EvidenceLine.Of("equipmentNoneRequired", ("rules", rules), ("filed", filed)))
                : (false, EvidenceLine.Of("equipmentFiled", ("rules", rules), ("filed", filed))));
        }

        var shownLevel = level?.ToString("000", CultureInfo.InvariantCulture) ?? "—";
        var threshold = above.ToString("000", CultureInfo.InvariantCulture);
        if (waived.Count > 0)
        {
            lines.Add((false, EvidenceLine.Of("equipmentNotAbove", ("letters", PlanLines.Join(waived)), ("level", shownLevel), ("above", threshold))));
        }
        else if (high && listed.Any(conditional.Contains))
        {
            lines.Add((false, EvidenceLine.Of("equipmentAbove", ("letters", PlanLines.Join(listed.Where(conditional.Contains))), ("level", shownLevel), ("above", threshold))));
        }

        return lines;
    }

    /// <summary>The letters required and not filed, a letter that stands for another counting as it.</summary>
    private static List<string> Missing(IEnumerable<string> required, IReadOnlyList<string> filed, IReadOnlyDictionary<string, string[]> standsFor) =>
        [.. required.Where(letter => !filed.Contains(letter) && !(standsFor.TryGetValue(letter, out var wider) && wider.Any(filed.Contains)))];
}

/// <summary>The flight rules of the plan are among those the rule admits (note 2026-09-24-i-controlli-dai-pirep-veri §4).</summary>
public sealed class FlightRulesCheck : IFlightCheck
{
    public string Key => CheckCatalog.FlightRules;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var admitted = OpenCatalog.Codes(parameters, "rules");
        return PlanLines.PerFlight(context, (_, plan) =>
        {
            var rules = PlanLines.Rules(plan);
            var ok = admitted.Count == 0 || admitted.Contains(rules);
            return [(!ok, EvidenceLine.Of(ok ? "flightRulesAdmitted" : "flightRulesNotAdmitted", ("rules", rules), ("admitted", PlanLines.Join(admitted))))];
        });
    }
}

/// <summary>
/// A plan filed before the take-off (GR9, note 2026-09-24-i-controlli-dai-pirep-veri §4): the checks read that one, and the
/// revisions filed afterwards do not count — they are shown, not failed.
/// </summary>
public sealed class PlanAtTakeoffCheck : IFlightCheck
{
    public string Key => CheckCatalog.PlanAtTakeoff;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters) =>
        PlanLines.PerFlight(context, (flight, plan) =>
        {
            var lines = new List<(bool, EvidenceLine)>
            {
                flight.HasPlanBeforeTakeoff
                    ? (false, EvidenceLine.Of("planAtTakeoff", ("revision", Text(plan.Revision)), ("at", Time(plan.FiledAt)), ("takeoff", Time(flight.TakeoffAt))))
                    : (true, EvidenceLine.Of("planAfterTakeoff", ("at", Time(plan.FiledAt)), ("takeoff", Time(flight.TakeoffAt)))),
            };

            lines.AddRange(flight.Plans
                .Where(revision => revision.FiledAt > flight.TakeoffAt && revision.Revision != plan.Revision)
                .Select(revision => (false, EvidenceLine.Of("planRevisedAfterTakeoff", ("revision", Text(revision.Revision)), ("at", Time(revision.FiledAt))))));
            return lines;
        });

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Time(DateTime moment) => moment.ToString("HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>
/// The form of the plan, line by line (note 2026-09-24-i-controlli-dai-pirep-veri §4, answers of 24 September 2026):
/// <c>REG/</c> when the callsign is an airline's, <c>RMK</c> with its slash, <c>Z</c> with <c>COM/</c>, <c>NAV/</c> or
/// <c>DAT/</c>, <c>R</c> with <c>PBN/</c>, a VFR plan without <c>DCT</c> and with <c>VFR</c> as its level, and a SID or a
/// STAR in the route only where the airport's country asks for it (<see cref="Settings.FlightOpsSettings.RouteProcedurePrefixes"/>).
/// </summary>
public sealed class FlightPlanFormCheck : IFlightCheck
{
    public string Key => CheckCatalog.FlightPlanForm;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        return PlanLines.PerFlight(context, (flight, plan) => Judge(flight, plan, context.Settings.RouteProcedurePrefixes));
    }

    private static List<(bool, EvidenceLine)> Judge(CheckedFlight flight, IvaoFlightPlanDto plan, IReadOnlyList<string> prefixes)
    {
        var lines = new List<(bool, EvidenceLine)>();
        var remarks = plan.Remarks;
        var rules = PlanLines.Rules(plan);

        var hasRegistration = FlightPlanText.HasIndicator(remarks, "REG");
        switch (FlightPlanText.ShapeOf(flight.Callsign))
        {
            case CallsignShape.Airline when !hasRegistration:
                lines.Add((true, EvidenceLine.Of("regMissing", ("callsign", flight.Callsign))));
                break;
            case CallsignShape.Unclear when !hasRegistration:
                lines.Add((false, EvidenceLine.Of("regUnclear", ("callsign", flight.Callsign))));
                break;
            default:
                break;
        }

        if (FlightPlanText.HasBareRemark(remarks))
        {
            lines.Add((true, EvidenceLine.Of("rmkWithoutSlash")));
        }

        var letters = FlightPlanText.Letters(plan.Equipment);
        if (letters.Contains("Z") && !new[] { "COM", "NAV", "DAT" }.Any(indicator => FlightPlanText.HasIndicator(remarks, indicator)))
        {
            lines.Add((true, EvidenceLine.Of("zWithoutDetails")));
        }

        if (letters.Contains("R") && !FlightPlanText.HasIndicator(remarks, "PBN"))
        {
            lines.Add((true, EvidenceLine.Of("rWithoutPbn")));
        }

        var route = FlightPlanText.RouteTokens(plan.Route);
        if (rules == "V")
        {
            if (route.Contains("DCT"))
            {
                lines.Add((true, EvidenceLine.Of("vfrWithDct")));
            }

            if (!string.Equals(plan.Level?.Trim(), "VFR", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add((true, EvidenceLine.Of("vfrLevel", ("level", plan.Level ?? "—"))));
            }
        }
        else
        {
            var points = route.Where(token => token != "DCT" && !SpeedAndLevel(token)).ToList();
            if (points.Count > 0 && FlightPlanText.IsProcedure(points[0]) && !Asks(plan.DepartureIcao, prefixes))
            {
                lines.Add((true, EvidenceLine.Of("sidInRoute", ("procedure", points[0]), ("airport", plan.DepartureIcao))));
            }

            if (points.Count > 1 && FlightPlanText.IsProcedure(points[^1]) && !Asks(plan.ArrivalIcao, prefixes))
            {
                lines.Add((true, EvidenceLine.Of("starInRoute", ("procedure", points[^1]), ("airport", plan.ArrivalIcao))));
            }
        }

        if (!lines.Any(line => line.Item1))
        {
            lines.Insert(0, (false, EvidenceLine.Of("planFormRight")));
        }

        return lines;
    }

    private static bool Asks(string airport, IReadOnlyList<string> prefixes) =>
        prefixes.Any(prefix => airport.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>A speed and a level of their own at the start of a route, <c>N0235F190</c>.</summary>
    private static bool SpeedAndLevel(string token) =>
        token.Length >= 5 && token[0] is 'N' or 'K' or 'M' && char.IsAsciiDigit(token[1]) && char.IsAsciiDigit(token[2]);
}

/// <summary>
/// The route was not flown already on a <c>Distance</c> or an <c>Open</c> tour, A→B not being B→A (§2.6); refused at the send
/// too. The tours made of legs count no routes, and pass.
/// </summary>
public sealed class RepeatedRouteCheck : IFlightCheck
{
    public string Key => CheckCatalog.RepeatedRoute;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TourKind is not (TourKind.Distance or TourKind.Open))
        {
            return CheckVerdict.Passed(EvidenceLine.Of("routeNotCounted"));
        }

        var route = (context.Leg.DepartureIcao, context.Leg.ArrivalIcao);
        var values = new[] { ("departure", route.DepartureIcao), ("arrival", route.ArrivalIcao) };
        return context.RoutesFlown.Contains(route)
            ? CheckVerdict.Failed(EvidenceLine.Of("routeFlownBefore", values))
            : CheckVerdict.Passed(EvidenceLine.Of("routeNew", values));
    }
}
