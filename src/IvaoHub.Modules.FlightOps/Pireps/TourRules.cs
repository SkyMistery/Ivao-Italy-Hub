using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// How a leg looks to the pilot (design M2 §8.1): the four colours of the map — to fly, done, pending, not yet. A rejected
/// leg is to fly again when it can be flown, and not yet when an earlier one holds it.
/// </summary>
public enum LegProgress
{
    Todo,
    Done,
    Pending,
    Locked,
}

/// <summary>Where a pilot is in a tour: each leg's colour, the legs that may be reported now, the next one, whether it is done.</summary>
public sealed record TourProgress(
    IReadOnlyDictionary<long, LegProgress> Legs,
    IReadOnlySet<long> Flyable,
    long? Next,
    bool Finished);

/// <summary>
/// The three questions every kind of tour answers (design M2 §2) — <b>which legs may be flown now</b>, <b>which is the
/// next</b>, <b>when it is done</b> — written once and read by the page, the form and the save. A pure function of the tour,
/// its legs, its hubs and rotations, and the pilot's reports on it.
/// <para>The rules every kind shares: a retired leg is not flown and not counted; a leg not yet released is not flown, and in
/// a sequence stops the pilot at the one before; an accepted or pending leg is not flown again; a rejected one is. In every
/// kind but <c>Free</c>, <c>Distance</c> and <c>Open</c> a rejected leg blocks the ones after it (§2.5) — except for a flight
/// that took off within <c>rejectGraceHours</c> of the decision, and except once the rejection is disputed (§3.8).</para>
/// <para>The instant is a parameter: the page asks about now, the save asks about the take-off of the flight reported, which
/// is what the release and the grace are measured against.</para>
/// <para>What is chosen rather than written in the design (Carmine, 23 September 2026): a <c>Hub</c> tour's hub is chosen by
/// flying a leg of it, and a <c>SequentialChosenStart</c> starts at the leg of the first report that is not withdrawn.</para>
/// </summary>
public static class TourRules
{
    /// <summary>What a leg is for the pilot, from their reports on it.</summary>
    private enum LegState
    {
        Fresh,
        Done,
        Pending,
        Rejected,
    }

    /// <param name="tour">The tour: its kind, progression, rotation order and required distance.</param>
    /// <param name="legs">Every leg of the tour, retired ones included.</param>
    /// <param name="hubs">The hubs of a <c>Hub</c> tour; empty otherwise.</param>
    /// <param name="rotations">The rotations of a <c>Hub</c> tour; empty otherwise.</param>
    /// <param name="reports">The pilot's reports on the tour, every state: a withdrawn one counts for nothing.</param>
    /// <param name="at">The instant asked about: now for the page, the take-off for a report.</param>
    /// <param name="graceHours">The division's <c>rejectGraceHours</c>.</param>
    public static TourProgress Of(
        Tour tour,
        IReadOnlyList<Leg> legs,
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<Rotation> rotations,
        IReadOnlyList<Pirep> reports,
        DateTime at,
        int graceHours)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(hubs);
        ArgumentNullException.ThrowIfNull(rotations);
        ArgumentNullException.ThrowIfNull(reports);

        var context = new Context(tour, legs, reports, at, TimeSpan.FromHours(graceHours));

        var flyable = tour.Kind switch
        {
            TourKind.Sequential => context.Walk(context.Ordered),
            TourKind.SequentialChosenStart => context.FromTheStart(),
            TourKind.Free or TourKind.Distance => context.Loose(),
            TourKind.Hub => new HubWalk(context, hubs, rotations).Flyable(),

            // No leg of its own: an Open tour's reports are flights (OpenRules), a container's are its subtours'.
            _ => new HashSet<long>(),
        };

        var colours = context.Ordered.ToDictionary(
            leg => leg.Id,
            leg => context.State(leg) switch
            {
                LegState.Done => LegProgress.Done,
                LegState.Pending => LegProgress.Pending,
                _ when flyable.Contains(leg.Id) => LegProgress.Todo,
                _ => LegProgress.Locked,
            });

        var next = tour.Kind is TourKind.Sequential or TourKind.SequentialChosenStart or TourKind.Hub && flyable.Count == 1
            ? flyable.Single()
            : (long?)null;

        var counted = tour.Kind == TourKind.Hub ? context.Ordered.Where(leg => leg.Kind == LegKind.Normal).ToList() : context.Ordered;
        var finished = tour.Kind switch
        {
            TourKind.Distance => tour.RequiredNm is { } required
                && context.Ordered.Where(leg => context.State(leg) == LegState.Done).Sum(leg => leg.DistanceNm) >= required,
            TourKind.Open or TourKind.Container => false,
            _ => counted.Count > 0 && counted.All(leg => context.State(leg) == LegState.Done),
        };

        return new TourProgress(colours, flyable, next, finished);
    }

    /// <summary>
    /// Why this leg may not be reported for a flight that took off then, as an i18n key; null when it may. The reports are
    /// the pilot's on the tour <b>without</b> the one being corrected, if any: correcting a report re-flies its own leg.
    /// </summary>
    public static string? Refusal(
        Tour tour,
        IReadOnlyList<Leg> legs,
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<Rotation> rotations,
        IReadOnlyList<Pirep> reports,
        long legId,
        DateTime takeoffAt,
        int graceHours)
    {
        ArgumentNullException.ThrowIfNull(legs);

        var leg = legs.FirstOrDefault(row => row.Id == legId);
        if (leg is null || leg.IsRetired)
        {
            return "flightops:errors.reportLegGone";
        }

        if ((leg.ReleaseAt ?? tour.ReleaseAt) > takeoffAt)
        {
            return "flightops:errors.reportBeforeRelease";
        }

        var progress = Of(tour, legs, hubs, rotations, reports, takeoffAt, graceHours);
        if (progress.Flyable.Contains(legId))
        {
            return null;
        }

        return progress.Legs.GetValueOrDefault(legId) switch
        {
            LegProgress.Done => "flightops:errors.reportLegDone",
            LegProgress.Pending => "flightops:errors.reportLegPending",
            _ => "flightops:errors.reportLegLocked",
        };
    }

    /// <summary>The legs, reports and instant of one question, and what every kind asks of them.</summary>
    private sealed class Context
    {
        private readonly Tour _tour;
        private readonly DateTime _at;
        private readonly TimeSpan _grace;
        private readonly Dictionary<long, (LegState State, bool Passed)> _states = [];

        public Context(Tour tour, IReadOnlyList<Leg> legs, IReadOnlyList<Pirep> reports, DateTime at, TimeSpan grace)
        {
            _tour = tour;
            _at = at;
            _grace = grace;
            All = [.. legs.OrderBy(leg => leg.Number).ThenBy(leg => leg.Id)];
            Ordered = [.. All.Where(leg => !leg.IsRetired)];
            Reports = [.. reports.Where(report => report.Status != PirepStatus.Withdrawn).OrderBy(report => report.SubmittedAt).ThenBy(report => report.Id)];

            foreach (var leg in All)
            {
                _states[leg.Id] = Judge(Reports.Where(report => report.LegId == leg.Id).ToList());
            }
        }

        public Tour Tour => _tour;

        /// <summary>Every leg in the order of the tour.</summary>
        public IReadOnlyList<Leg> All { get; }

        /// <summary>The legs still in the tour, in its order.</summary>
        public IReadOnlyList<Leg> Ordered { get; }

        /// <summary>The reports that count, oldest first.</summary>
        public IReadOnlyList<Pirep> Reports { get; }

        public LegState State(Leg leg) => _states[leg.Id].State;

        /// <summary>Whether the pilot is past this leg for the ones after it: done, pending when flying ahead, or a rejection that no longer holds.</summary>
        public bool Passed(Leg leg) => _states[leg.Id].Passed;

        public bool Released(Leg leg) => (leg.ReleaseAt ?? _tour.ReleaseAt) is not { } release || release <= _at;

        /// <summary>A leg that may be reported, whatever the order says: released, and fresh or rejected.</summary>
        public bool Open(Leg leg) => Released(leg) && State(leg) is LegState.Fresh or LegState.Rejected;

        /// <summary>
        /// A sequence, walked from its first leg: every leg the pilot is past lets the walk go on, the first that is not stops
        /// it. What may be flown is the leg the walk stops at, and any rejected leg it went past.
        /// </summary>
        public HashSet<long> Walk(IEnumerable<Leg> sequence) => WalkThrough(sequence).Flyable;

        /// <summary>The walk, and whether it reached the end — a rotation passed, in a <c>Hub</c> tour.</summary>
        public (HashSet<long> Flyable, bool Through) WalkThrough(IEnumerable<Leg> sequence)
        {
            var flyable = new HashSet<long>();

            foreach (var leg in sequence)
            {
                if (!Released(leg))
                {
                    return (flyable, false);
                }

                if (Open(leg))
                {
                    flyable.Add(leg.Id);
                }

                if (!Passed(leg))
                {
                    return (flyable, false);
                }
            }

            return (flyable, true);
        }

        /// <summary><c>Free</c> and <c>Distance</c>: every released leg not accepted nor pending.</summary>
        public HashSet<long> Loose() => [.. Ordered.Where(Open).Select(leg => leg.Id)];

        /// <summary>
        /// <c>SequentialChosenStart</c> (§2.4): in order from the leg of the first report that counts to the last, then from the
        /// first to the one before the start. Before any report, any released leg starts it.
        /// </summary>
        public HashSet<long> FromTheStart()
        {
            var first = Reports.FirstOrDefault(report => report.LegId is not null);
            var at = first is null ? -1 : All.ToList().FindIndex(leg => leg.Id == first.LegId);

            if (at < 0)
            {
                return Loose();
            }

            var ring = All.Skip(at).Concat(All.Take(at)).Where(leg => !leg.IsRetired);
            return Walk(ring);
        }

        private (LegState State, bool Passed) Judge(IReadOnlyList<Pirep> reports)
        {
            if (reports.Any(report => report.Status == PirepStatus.Accepted))
            {
                return (LegState.Done, true);
            }

            if (reports.Any(report => Pirep.IsPending(report.Status)))
            {
                return (LegState.Pending, _tour.Progression == TourProgression.FlyAhead);
            }

            var rejected = reports
                .Where(report => report.Status == PirepStatus.Rejected)
                .OrderBy(report => report.DecidedAt ?? report.SubmittedAt)
                .LastOrDefault();

            if (rejected is null)
            {
                return (LegState.Fresh, false);
            }

            // Disputed, it no longer holds the next legs (§3.8); decided, it holds them for flights after the grace (§2.5).
            var decided = rejected.DecidedAt ?? rejected.SubmittedAt;
            return (LegState.Rejected, rejected.IsDisputed || _at <= decided + _grace);
        }
    }

    /// <summary>
    /// A <c>Hub</c> tour (design M2 §2.3): inside a hub the rotations in the tour's order or in any, each in its own order and
    /// one at a time; a hub finished, the next one — through a connecting leg from where the pilot is when the hub has any,
    /// freely when it has none. The hub a pilot starts from is the one whose leg they fly first (Carmine, 23 September 2026).
    /// </summary>
    private sealed class HubWalk(Context context, IReadOnlyList<TourHub> hubs, IReadOnlyList<Rotation> rotations)
    {
        public HashSet<long> Flyable()
        {
            var legsOf = context.Ordered
                .Where(leg => leg.Kind == LegKind.Normal && leg.RotationId is not null)
                .GroupBy(leg => leg.RotationId!.Value)
                .ToDictionary(group => group.Key, group => group.OrderBy(leg => leg.SeqInRotation ?? leg.Number).ToList());

            // A rotation whose legs are all retired no longer counts (§1.3).
            var rotationsOf = rotations
                .Where(rotation => legsOf.ContainsKey(rotation.Id))
                .GroupBy(rotation => rotation.HubId)
                .ToDictionary(group => group.Key, group => group.OrderBy(rotation => rotation.Sort).ThenBy(rotation => rotation.Id).ToList());

            var connections = context.Ordered.Where(leg => leg.Kind == LegKind.HubConnection).ToList();
            var hubOfRotation = rotations.ToDictionary(rotation => rotation.Id, rotation => rotation.HubId);

            bool Started(Rotation rotation) => legsOf[rotation.Id].Any(leg => context.State(leg) != LegState.Fresh);
            bool Through(Rotation rotation) => context.WalkThrough(legsOf[rotation.Id]).Through;
            List<Rotation> Of(TourHub hub) => rotationsOf.GetValueOrDefault(hub.Id) ?? [];
            bool Done(TourHub hub) => Of(hub).All(Through);

            // The legs that fly the pilot into a hub, or on inside it.
            HashSet<long> Inside(TourHub hub)
            {
                var open = Of(hub).Where(rotation => !Through(rotation)).ToList();
                var begun = open.Where(Started).ToList();

                IEnumerable<Rotation> next = context.Tour.HubRotationOrder == HubRotationOrder.Free
                    ? begun.Count > 0 ? begun : open
                    : open.Take(1);

                return [.. next.SelectMany(rotation => context.Walk(legsOf[rotation.Id]))];
            }

            // Where the pilot is: the hub of the last leg they are past — a connection lands them at its arrival.
            var position = context.Reports
                .Where(report => report.LegId is { } id && context.All.FirstOrDefault(leg => leg.Id == id) is { } leg && context.Passed(leg))
                .OrderBy(report => report.TakeoffAt)
                .Select(report => context.All.First(leg => leg.Id == report.LegId))
                .Select(leg => leg.Kind == LegKind.HubConnection
                    ? hubs.FirstOrDefault(hub => hub.Icao == leg.ArrivalIcao)
                    : leg.RotationId is { } rotationId && hubOfRotation.TryGetValue(rotationId, out var hubId)
                        ? hubs.FirstOrDefault(hub => hub.Id == hubId)
                        : null)
                .LastOrDefault(hub => hub is not null);

            var flyable = new HashSet<long>();

            // Inside a hub not finished — where the pilot is, and any other they began and left half done.
            var current = hubs
                .Where(hub => !Done(hub) && (hub.Id == position?.Id || Of(hub).Any(Started)))
                .ToList();

            if (current.Count > 0)
            {
                foreach (var hub in current)
                {
                    flyable.UnionWith(Inside(hub));
                }

                return flyable;
            }

            var ahead = hubs.Where(hub => !Done(hub)).ToList();
            var connected = connections
                .SelectMany(leg => new[] { leg.DepartureIcao, leg.ArrivalIcao })
                .ToHashSet(StringComparer.Ordinal);

            foreach (var hub in ahead)
            {
                if (position is null || !connected.Contains(hub.Icao))
                {
                    // The start, anywhere; or a hub no leg connects, reached freely.
                    flyable.UnionWith(Inside(hub));
                    continue;
                }

                // A connected hub, only through the leg from where the pilot is.
                flyable.UnionWith(connections
                    .Where(leg => leg.DepartureIcao == position.Icao && leg.ArrivalIcao == hub.Icao && context.Open(leg))
                    .Select(leg => leg.Id));
            }

            return flyable;
        }
    }
}
