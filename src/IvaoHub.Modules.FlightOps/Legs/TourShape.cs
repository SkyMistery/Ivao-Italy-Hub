using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>One refusal of the shape of a tour: the field it is filed under, and the i18n key that says why.</summary>
public sealed record ShapeProblem(string Field, string Key);

/// <summary>
/// What the legs of a tour must be before it can be marked ready (design M2 §1.2.1, §2), written once and read by
/// "ready", by the list of problems before it, and by every later write of a ready tour — its own and its legs'.
/// A pure function of the tour, its legs and the airports the hub knows, so every kind is tested without a database.
/// <para>A retired leg is not part of the shape: it is no longer flown (§1.4.1). A problem of one leg is filed under
/// <c>legs.{number}</c>, one of the whole under <c>legs</c>. The hubs, the rotations and the subtours are T7b's.</para>
/// </summary>
public static class TourShape
{
    /// <summary>The kinds that have no legs: an Open tour is flown anywhere, a container through its subtours (§2.6, §2.7).</summary>
    public static bool HasLegs(TourKind kind) => kind is not (TourKind.Open or TourKind.Container);

    public static IReadOnlyList<ShapeProblem> Problems(Tour tour, IReadOnlyList<Leg> legs, IReadOnlySet<string> knownAirports)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(knownAirports);

        var problems = new List<ShapeProblem>();
        var flown = legs.Where(leg => !leg.IsRetired).OrderBy(leg => leg.Number).ToList();

        if (!HasLegs(tour.Kind))
        {
            if (flown.Count > 0)
            {
                problems.Add(new("legs", "flightops:errors.kindHasNoLegs"));
            }

            return problems;
        }

        if (flown.Count == 0)
        {
            problems.Add(new("legs", "flightops:errors.noLegs"));
            return problems;
        }

        foreach (var leg in flown)
        {
            var field = $"legs.{leg.Number}";

            // Frozen at the write, but the world's snapshot can lose an airport afterwards.
            if (!knownAirports.Contains(leg.DepartureIcao) || !knownAirports.Contains(leg.ArrivalIcao))
            {
                problems.Add(new(field, "flightops:errors.airportUnknown"));
            }

            if (leg.ReleaseAt is { } release
                && ((tour.ReleaseAt is { } tourRelease && release < tourRelease) || (tour.CloseAt is { } close && release > close)))
            {
                problems.Add(new(field, "flightops:errors.legReleaseOutsidePeriod"));
            }
        }

        switch (tour.Kind)
        {
            // A start anywhere and a whole turn: every arrival is the next departure, the last one's the first's (answer 11).
            case TourKind.SequentialChosenStart
                when flown.Where((leg, index) => leg.ArrivalIcao != flown[(index + 1) % flown.Count].DepartureIcao).Any():
                problems.Add(new("legs", "flightops:errors.notARing"));
                break;

            // The legs together have to be able to complete it (answer 12): the great circles, never the miles flown.
            case TourKind.Distance when tour.RequiredNm is null:
                problems.Add(new("requiredNm", "errors.required"));
                break;

            case TourKind.Distance when flown.Sum(leg => leg.DistanceNm) < tour.RequiredNm:
                problems.Add(new("requiredNm", "flightops:errors.distanceUnreachable"));
                break;

            default:
                break;
        }

        return problems;
    }
}
