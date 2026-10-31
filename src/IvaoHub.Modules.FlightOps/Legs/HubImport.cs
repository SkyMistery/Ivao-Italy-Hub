using System.Security.Cryptography;
using System.Text;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// One row of a hub tour's file, made a leg by the server like a leg of the editor, and where the file puts it: the
/// rotation <see cref="Rotation"/> of the hub <see cref="Hub"/>, or — both null — a connection between two hubs.
/// </summary>
public sealed record HubImportRow(int Row, Leg Leg, string? Hub, string? Rotation);

/// <summary>
/// The comparison of a hub tour's file with its hubs, rotations and legs: unit tested, and writing nothing but the places
/// (<c>Sort</c>) of the hubs and rotations it is handed (note 2026-10-28-hub-e-rotazioni-da-file, decided by Carmine on 29 October 2026). It is <see cref="LegImportPlan"/>
/// one level up: what is matched, kept, deleted or retired is a <b>rotation</b>, whole — never one of its legs alone
/// (§1.4.1) —, or a connection, which is one leg.
/// <para><b>The same rotation</b> is the same hub and the same legs in the same order, by departure and arrival; when two
/// rotations are the same they are paired in their order. <b>The same connection</b> is the same departure and arrival.</para>
/// <para><b>The order after</b> is the file's; a rotation or a connection the file does not contain and that stays — every
/// one on "merge", the ones with reports on "replace" — keeps its place after the one that came before it. The hubs come
/// in the order the legs first name them, the rotations of a hub in the order of their legs. A hub the file names and the
/// tour has not is added; on "replace", a hub the file does not name and that is left without rotations is deleted.</para>
/// </summary>
public sealed class HubImportPlan
{
    private HubImportPlan(
        IReadOnlyList<Step> steps,
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<TourHub> deletedHubs,
        IReadOnlyList<Unit> units,
        IReadOnlyList<Rotation> deletedRotations)
    {
        Steps = steps;
        Hubs = hubs;
        DeletedHubs = deletedHubs;
        Units = units;
        DeletedRotations = deletedRotations;
    }

    /// <summary>Every leg of the tour after the import, in its order, deleted ones last with no number.</summary>
    public IReadOnlyList<Step> Steps { get; }

    /// <summary>The hubs after the import, in their order: the tour's that stay and the new ones (<c>Id</c> 0).</summary>
    public IReadOnlyList<TourHub> Hubs { get; }

    public IReadOnlyList<TourHub> DeletedHubs { get; }

    /// <summary>The rotations and connections after the import, in their order.</summary>
    public IReadOnlyList<Unit> Units { get; }

    public IReadOnlyList<Rotation> DeletedRotations { get; }

    public bool ReasonRequired => Steps.Any(step =>
        step.Outcome is LegImportOutcome.Retired or LegImportOutcome.Restored
        || (step.Outcome == LegImportOutcome.Changed && step.HasReports));

    public bool Restores => Steps.Any(step => step.Outcome == LegImportOutcome.Restored);

    /// <summary>
    /// What happens to every hub, rotation and leg. <paramref name="incoming"/> are the rows of the file already made legs
    /// by the server and already checked whole — the rows of a rotation consecutive, of its size, out of its hub and back
    /// —, in the file's order.
    /// </summary>
    public static HubImportPlan Make(
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<Rotation> rotations,
        IReadOnlyList<Leg> existing,
        IReadOnlyList<HubImportRow> incoming,
        LegImportMode mode,
        IReadOnlySet<long> withReports)
    {
        ArgumentNullException.ThrowIfNull(hubs);
        ArgumentNullException.ThrowIfNull(rotations);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(withReports);

        var tourUnits = TourUnits(hubs, rotations, existing);
        var fileUnits = FileUnits(incoming);

        // The units of the tour, each a queue by what it is: a file's unit takes the first one still free.
        var free = tourUnits
            .Where(unit => unit.Legs.Count > 0)
            .GroupBy(unit => unit.Signature)
            .ToDictionary(group => group.Key, group => new Queue<Unit>(group), StringComparer.Ordinal);

        var matched = new Unit?[fileUnits.Count];
        for (var index = 0; index < fileUnits.Count; index++)
        {
            if (free.TryGetValue(fileUnits[index].Signature, out var queue) && queue.Count > 0)
            {
                matched[index] = queue.Dequeue();
            }
        }

        var taken = matched.OfType<Unit>().ToHashSet();

        // A unit the file does not contain hangs after the last unit before it that the file does contain.
        var hanging = new Dictionary<Unit, List<Unit>>();
        var atStart = new List<Unit>();
        var gone = new List<Unit>();
        Unit? anchor = null;
        foreach (var unit in tourUnits)
        {
            if (taken.Contains(unit))
            {
                anchor = unit;
                continue;
            }

            if (mode == LegImportMode.Replace && !unit.Legs.Any(leg => withReports.Contains(leg.Id)))
            {
                gone.Add(unit);
                continue;
            }

            if (anchor is null)
            {
                atStart.Add(unit);
            }
            else
            {
                (hanging.TryGetValue(anchor, out var list) ? list : hanging[anchor] = []).Add(unit);
            }
        }

        var steps = new List<Step>();
        var after = new List<Unit>();

        void Absent(Unit unit)
        {
            after.Add(unit);
            var reports = unit.Legs.Any(leg => withReports.Contains(leg.Id));
            foreach (var leg in unit.Legs)
            {
                var outcome = mode == LegImportMode.Replace && reports && !leg.IsRetired
                    ? LegImportOutcome.Retired
                    : LegImportOutcome.Kept;
                steps.Add(new Step(outcome, null, leg, null, [], withReports.Contains(leg.Id), unit));
            }
        }

        atStart.ForEach(Absent);

        for (var index = 0; index < fileUnits.Count; index++)
        {
            var file = fileUnits[index];
            if (matched[index] is not { } unit)
            {
                var added = new Unit(file.Hub is null ? null : new Rotation { Size = file.Rows.Count }, file.Hub);
                foreach (var row in file.Rows)
                {
                    added.Legs.Add(row.Leg);
                    steps.Add(new Step(LegImportOutcome.Added, row.Row, row.Leg, row.Leg, [], false, added));
                }

                after.Add(added);
                continue;
            }

            after.Add(unit);
            for (var position = 0; position < unit.Legs.Count; position++)
            {
                var leg = unit.Legs[position];
                var row = file.Rows[position];
                var changes = LegImportPlan.Changes(leg, row.Leg);
                var outcome = leg.IsRetired
                    ? LegImportOutcome.Restored
                    : changes.Count > 0 ? LegImportOutcome.Changed : LegImportOutcome.Unchanged;
                steps.Add(new Step(outcome, row.Row, leg, row.Leg, changes, withReports.Contains(leg.Id), unit));
            }

            if (hanging.TryGetValue(unit, out var later))
            {
                later.ForEach(Absent);
            }
        }

        var number = 1;
        foreach (var step in steps)
        {
            step.NumberAfter = number++;
        }

        foreach (var unit in gone)
        {
            steps.AddRange(unit.Legs.Select(leg => new Step(LegImportOutcome.Deleted, null, leg, null, [], false, unit)));
        }

        var (hubsAfter, deletedHubs) = HubsAfter(hubs, after, incoming, mode);

        // The places: hubs in their order, the rotations of a hub in the order of their legs.
        for (var place = 0; place < hubsAfter.Count; place++)
        {
            hubsAfter[place].Sort = place + 1;
        }

        foreach (var group in after.Where(unit => unit.Rotation is not null).GroupBy(unit => unit.HubIcao, StringComparer.Ordinal))
        {
            var place = 1;
            foreach (var unit in group)
            {
                unit.Rotation!.Sort = place++;
            }
        }

        return new HubImportPlan(
            steps,
            hubsAfter,
            deletedHubs,
            after,
            [.. gone.Select(unit => unit.Rotation).OfType<Rotation>()]);
    }

    /// <summary>What the preview shows: every leg, with its hub and the place of its rotation in the hub after the import.</summary>
    public IReadOnlyList<LegImportLineDto> Lines() =>
    [
        .. Steps.Select(step => new LegImportLineDto(
            step.Outcome,
            step.Row,
            step.Outcome == LegImportOutcome.Added ? null : step.Leg.Id,
            step.Outcome == LegImportOutcome.Added ? null : step.Leg.Number,
            step.NumberAfter,
            step.Leg.DepartureIcao,
            step.Leg.ArrivalIcao,
            step.Changes,
            step.HasReports,
            step.Unit.HubIcao,
            step.Outcome == LegImportOutcome.Deleted ? null : step.Unit.Rotation?.Sort)),
    ];

    /// <summary>The hubs the import adds, by their ICAO.</summary>
    public IReadOnlyList<string> HubsAdded => [.. Hubs.Where(hub => hub.Id == 0).Select(hub => hub.Icao)];

    /// <summary>
    /// The plan on the legs: the file's values on the legs it matches (their groups kept), the retired rotations it names
    /// back, the absent ones deleted or retired, every number the plan's. The rotations and the hubs are the caller's to
    /// write first, so that a new leg can be put in its new rotation (<see cref="Unit.Rotation"/>, saved). Returns the new
    /// legs, to be added, and the deleted ones, to be removed.
    /// </summary>
    public (IReadOnlyList<Leg> Added, IReadOnlyList<Leg> Deleted) Apply(DateTime now, string? reason)
    {
        var added = new List<Leg>();
        var deleted = new List<Leg>();

        foreach (var step in Steps)
        {
            var leg = step.Leg;
            switch (step.Outcome)
            {
                case LegImportOutcome.Added:
                    added.Add(leg);
                    break;
                case LegImportOutcome.Changed or LegImportOutcome.Restored:
                    LegImportPlan.Take(leg, step.File!);
                    if (step.Outcome == LegImportOutcome.Restored)
                    {
                        leg.RetiredAt = null;
                        leg.RetiredReason = null;
                    }

                    leg.ChangeReason = step.HasReports || step.Outcome == LegImportOutcome.Restored ? reason : null;
                    break;
                case LegImportOutcome.Retired:
                    leg.RetiredAt = now;
                    leg.RetiredReason = reason;
                    break;
                case LegImportOutcome.Deleted:
                    deleted.Add(leg);
                    break;
            }

            // What the file says the leg is: in its rotation, or a connection. A leg the file does not name stays what it was.
            if (step.Row is not null)
            {
                leg.Kind = step.Unit.Rotation is null ? LegKind.HubConnection : LegKind.Normal;
                leg.RotationId = step.Unit.Rotation?.Id;
            }

            if (step.NumberAfter is { } number && leg.Number != number)
            {
                leg.Number = number;
            }
        }

        return (added, deleted);
    }

    /// <summary>
    /// The state of the shape the preview looked at: every leg, hub and rotation and its version. The apply carries it
    /// back, and a tour whose shape changed in between is a 409, not an import of something nobody saw.
    /// </summary>
    public static string Fingerprint(IEnumerable<Leg> legs, IEnumerable<TourHub> hubs, IEnumerable<Rotation> rotations)
    {
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(hubs);
        ArgumentNullException.ThrowIfNull(rotations);

        var text = string.Join(
            ';',
            legs.OrderBy(leg => leg.Id).Select(leg => $"l{leg.Id}:{leg.RowVersion.Ticks}")
                .Concat(hubs.OrderBy(hub => hub.Id).Select(hub => $"h{hub.Id}:{hub.RowVersion.Ticks}"))
                .Concat(rotations.OrderBy(rotation => rotation.Id).Select(rotation => $"r{rotation.Id}:{rotation.RowVersion.Ticks}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>
    /// The tour's units, in the order of their first leg: a rotation with its legs, retired ones included, or one leg in no
    /// rotation. A rotation with no legs yet comes last.
    /// </summary>
    private static List<Unit> TourUnits(IReadOnlyList<TourHub> hubs, IReadOnlyList<Rotation> rotations, IReadOnlyList<Leg> existing)
    {
        var hubOf = hubs.ToDictionary(hub => hub.Id, hub => hub.Icao);
        var rotationOf = rotations.ToDictionary(rotation => rotation.Id);
        var units = new List<Unit>();
        var byRotation = new Dictionary<long, Unit>();

        foreach (var leg in existing.OrderBy(leg => leg.Number).ThenBy(leg => leg.Id))
        {
            if (leg.RotationId is { } id && rotationOf.TryGetValue(id, out var rotation))
            {
                if (!byRotation.TryGetValue(id, out var unit))
                {
                    unit = new Unit(rotation, hubOf.GetValueOrDefault(rotation.HubId));
                    byRotation[id] = unit;
                    units.Add(unit);
                }

                unit.Legs.Add(leg);
                continue;
            }

            var single = new Unit(null, null);
            single.Legs.Add(leg);
            units.Add(single);
        }

        units.AddRange(rotations
            .Where(rotation => !byRotation.ContainsKey(rotation.Id))
            .OrderBy(rotation => hubs.FirstOrDefault(hub => hub.Id == rotation.HubId)?.Sort)
            .ThenBy(rotation => rotation.Sort)
            .ThenBy(rotation => rotation.Id)
            .Select(rotation => new Unit(rotation, hubOf.GetValueOrDefault(rotation.HubId))));

        return units;
    }

    /// <summary>The file's units: the consecutive rows of one rotation of a hub, or one connection.</summary>
    private static List<FileUnit> FileUnits(IReadOnlyList<HubImportRow> incoming)
    {
        var units = new List<FileUnit>();
        foreach (var row in incoming)
        {
            if (row.Hub is not null
                && units.Count > 0
                && units[^1] is { Hub: { } hub, Label: { } label } last
                && hub == row.Hub
                && label == row.Rotation)
            {
                last.Rows.Add(row);
                continue;
            }

            units.Add(new FileUnit(row.Hub, row.Rotation, [row]));
        }

        return units;
    }

    /// <summary>
    /// The hubs after the import: those the units name, in the order they first name them — the tour's own, or new ones —,
    /// then the tour's others. On "replace", one of those others is deleted when the file names it nowhere, not even as the
    /// end of a connection.
    /// </summary>
    private static (List<TourHub> Hubs, List<TourHub> Deleted) HubsAfter(
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<Unit> after,
        IReadOnlyList<HubImportRow> incoming,
        LegImportMode mode)
    {
        var result = new List<TourHub>();
        var deleted = new List<TourHub>();

        foreach (var icao in after.Select(unit => unit.HubIcao).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            result.Add(hubs.FirstOrDefault(hub => hub.Icao == icao) ?? new TourHub { Icao = icao });
        }

        var named = incoming
            .SelectMany(row => row.Hub is null ? new[] { row.Leg.DepartureIcao, row.Leg.ArrivalIcao } : [row.Hub])
            .ToHashSet(StringComparer.Ordinal);

        foreach (var hub in hubs.OrderBy(hub => hub.Sort).ThenBy(hub => hub.Id).Where(hub => !result.Contains(hub)))
        {
            if (mode == LegImportMode.Replace && !named.Contains(hub.Icao))
            {
                deleted.Add(hub);
                continue;
            }

            // Named only as the end of a connection: it stays a hub, or the connection would end nowhere.
            result.Add(hub);
        }

        // A hub only a connection names, and the tour has not: added too.
        foreach (var icao in named.Where(icao => result.All(hub => hub.Icao != icao)))
        {
            result.Add(new TourHub { Icao = icao });
        }

        return (result, deleted);
    }

    /// <summary>A rotation of the tour or of the file with its legs, or a connection: what is matched, kept or removed whole.</summary>
    public sealed class Unit(Rotation? rotation, string? hubIcao)
    {
        /// <summary>The rotation; a new one has <c>Id</c> 0 until it is saved. Null for a connection.</summary>
        public Rotation? Rotation { get; } = rotation;

        public string? HubIcao { get; } = hubIcao;

        public List<Leg> Legs { get; } = [];

        internal string Signature => Key(Rotation is null ? null : HubIcao, Legs);

        internal static string Key(string? hub, IEnumerable<Leg> legs) =>
            $"{hub ?? "~"}|{string.Join(';', legs.Select(leg => $"{leg.DepartureIcao}>{leg.ArrivalIcao}"))}";
    }

    /// <summary>One leg in the plan: the tour's (or the file's, when added), the row of the file that names it, and its unit.</summary>
    public sealed class Step(
        LegImportOutcome outcome,
        int? row,
        Leg leg,
        Leg? file,
        IReadOnlyList<string> changes,
        bool hasReports,
        Unit unit)
    {
        public LegImportOutcome Outcome { get; } = outcome;

        public int? Row { get; } = row;

        public Leg Leg { get; } = leg;

        public Leg? File { get; } = file;

        public IReadOnlyList<string> Changes { get; } = changes;

        public bool HasReports { get; } = hasReports;

        public Unit Unit { get; } = unit;

        public int? NumberAfter { get; internal set; }
    }

    private sealed record FileUnit(string? Hub, string? Label, List<HubImportRow> Rows)
    {
        public string Signature => Unit.Key(Hub, Rows.Select(row => row.Leg));
    }
}
