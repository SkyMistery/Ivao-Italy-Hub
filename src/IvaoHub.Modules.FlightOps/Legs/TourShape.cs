using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>One refusal of the shape of a tour: the field it is filed under, and the i18n key that says why.</summary>
public sealed record ShapeProblem(string Field, string Key);

/// <summary>
/// Everything the shape of a tour is made of besides the tour itself: its legs, its hubs and their rotations, its
/// subtours when it is a container, and its parent when it is a subtour. What a write is about to leave is handed in
/// as it will be, so a check never reads what is stored in place of what is being saved.
/// </summary>
public sealed record TourParts(
    IReadOnlyList<Leg> Legs,
    IReadOnlyList<TourHub> Hubs,
    IReadOnlyList<Rotation> Rotations,
    IReadOnlyList<Tour> Subtours,
    Tour? Parent)
{
    public static TourParts Of(IReadOnlyList<Leg> legs) => new(legs, [], [], [], null);
}

/// <summary>
/// What the shape of a tour must be before it can be marked ready (design M2 §1.2.1, §2), written once and read by
/// "ready", by the list of problems before it, and by every later write of a ready tour — its own and its rows'.
/// A pure function of the tour, its parts and the airports the hub knows, so every kind is tested without a database.
/// <para>A retired leg is not part of the shape: it is no longer flown (§1.4.1), and a rotation whose legs are all
/// retired no longer counts. A problem of one leg is filed under <c>legs.{number}</c>, one of the whole under
/// <c>legs</c>; one of a hub under <c>hubs.{ICAO}</c>, one of a rotation under <c>rotations.{hub ICAO}.{position}</c>,
/// its place among the rotations of its hub.</para>
/// </summary>
public static class TourShape
{
    /// <summary>The kinds that have no legs: an Open tour is flown anywhere, a container through its subtours (§2.6, §2.7).</summary>
    public static bool HasLegs(TourKind kind) => kind is not (TourKind.Open or TourKind.Container);

    public static IReadOnlyList<ShapeProblem> Problems(Tour tour, IReadOnlyList<Leg> legs, IReadOnlySet<string> knownAirports) =>
        Problems(tour, TourParts.Of(legs), knownAirports);

    public static IReadOnlyList<ShapeProblem> Problems(Tour tour, TourParts parts, IReadOnlySet<string> knownAirports)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(knownAirports);

        var problems = new List<ShapeProblem>();
        var flown = parts.Legs.Where(leg => !leg.IsRetired).OrderBy(leg => leg.Number).ToList();

        SubtourProblems(tour, parts, problems);

        if (tour.Kind == TourKind.Container)
        {
            ContainerProblems(tour, parts, problems);
        }

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

        if (tour.Kind == TourKind.Hub)
        {
            HubProblems(parts, flown, problems);
        }
        else
        {
            if (parts.Hubs.Count > 0)
            {
                problems.Add(new("hubs", "flightops:errors.kindHasNoHubs"));
            }

            problems.AddRange(flown
                .Where(leg => leg.RotationId is not null || leg.Kind == LegKind.HubConnection)
                .Select(leg => new ShapeProblem($"legs.{leg.Number}", "flightops:errors.legHubOnly")));
        }

        if (flown.Count == 0)
        {
            return problems;
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

    /// <summary>
    /// A hub tour (design M2 §1.3, §2.3): at least one hub, every hub with a rotation, every rotation of exactly its size
    /// out of the hub and back, every ordinary leg in a rotation, every connection between two hubs of the tour.
    /// </summary>
    private static void HubProblems(TourParts parts, IReadOnlyList<Leg> flown, List<ShapeProblem> problems)
    {
        if (parts.Hubs.Count == 0)
        {
            problems.Add(new("hubs", "flightops:errors.noHubs"));
        }

        foreach (var hub in parts.Hubs.OrderBy(hub => hub.Sort).ThenBy(hub => hub.Id))
        {
            var rotations = parts.Rotations.Where(rotation => rotation.HubId == hub.Id).OrderBy(rotation => rotation.Sort).ThenBy(rotation => rotation.Id).ToList();
            var active = 0;

            for (var position = 1; position <= rotations.Count; position++)
            {
                var rotation = rotations[position - 1];
                var all = parts.Legs.Where(leg => leg.RotationId == rotation.Id).ToList();
                var legs = all.Where(leg => !leg.IsRetired).OrderBy(leg => leg.Number).ToList();

                // Retired whole (§1.4.1): it stays for the reports that point at it, and no longer counts.
                if (all.Count > 0 && legs.Count == 0)
                {
                    continue;
                }

                active++;
                var field = $"rotations.{hub.Icao}.{position}";

                if (legs.Count != rotation.Size)
                {
                    problems.Add(new(field, "flightops:errors.rotationSize"));
                }
                else if (legs[0].DepartureIcao != hub.Icao || legs[^1].ArrivalIcao != hub.Icao)
                {
                    problems.Add(new(field, "flightops:errors.rotationNotAtHub"));
                }
            }

            if (active == 0)
            {
                problems.Add(new($"hubs.{hub.Icao}", "flightops:errors.hubWithoutRotations"));
            }
        }

        var hubs = parts.Hubs.Select(hub => hub.Icao).ToHashSet(StringComparer.Ordinal);
        var rotationIds = parts.Rotations.Select(rotation => rotation.Id).ToHashSet();

        foreach (var leg in flown)
        {
            var field = $"legs.{leg.Number}";

            if (leg.Kind == LegKind.HubConnection)
            {
                if (leg.RotationId is not null
                    || leg.DepartureIcao == leg.ArrivalIcao
                    || !hubs.Contains(leg.DepartureIcao)
                    || !hubs.Contains(leg.ArrivalIcao))
                {
                    problems.Add(new(field, "flightops:errors.connectionNotBetweenHubs"));
                }
            }
            else if (leg.RotationId is not { } rotationId || !rotationIds.Contains(rotationId))
            {
                problems.Add(new(field, "flightops:errors.legNotInRotation"));
            }
        }
    }

    /// <summary>A container (design M2 §2.7): at least two subtours, and no more of them required than there are.</summary>
    private static void ContainerProblems(Tour tour, TourParts parts, List<ShapeProblem> problems)
    {
        if (parts.Subtours.Count < 2)
        {
            problems.Add(new("subtours", "flightops:errors.containerNeedsSubtours"));
        }

        if (tour.RequiredSubtours is not { } required)
        {
            problems.Add(new("requiredSubtours", "errors.required"));
        }
        else if (required > parts.Subtours.Count)
        {
            problems.Add(new("requiredSubtours", "flightops:errors.requiredSubtoursTooMany"));
        }
    }

    /// <summary>
    /// A subtour (design M2 §2.7, note 2026-09-21-la-forma-dei-tour): its own dates inside its parent's period, and no
    /// award — the award is the parent's.
    /// </summary>
    private static void SubtourProblems(Tour tour, TourParts parts, List<ShapeProblem> problems)
    {
        if (!tour.IsSubtour)
        {
            return;
        }

        if (parts.Parent is not { } parent)
        {
            problems.Add(new("parentTourId", "flightops:errors.parentUnknown"));
            return;
        }

        if (tour.ReleaseAt is { } release && parent.ReleaseAt is { } parentRelease && release < parentRelease)
        {
            problems.Add(new("releaseAt", "flightops:errors.subtourOutsideParent"));
        }

        if (tour.CloseAt is { } close && parent.CloseAt is { } parentClose && close > parentClose)
        {
            problems.Add(new("closeAt", "flightops:errors.subtourOutsideParent"));
        }

        if (tour.AwardId is not null)
        {
            problems.Add(new("awardId", "flightops:errors.subtourHasNoAward"));
        }
    }
}
