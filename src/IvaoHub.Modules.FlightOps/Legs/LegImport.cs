using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// What an import does with the legs the file does not contain (design M2 §8.4, ADR-051 of Toursystem): "merge" leaves
/// them where they are, "replace" deletes those without reports and retires those with reports (§1.4.1).
/// </summary>
public enum LegImportMode
{
    Merge,
    Replace,
}

/// <summary>
/// One row of the file, as the browser read it (T8, Carmine 22 September 2026: the file is read in the browser). The
/// file names aircraft types only: the groups of a leg are the editor's, and a leg the file matches keeps its own.
/// <para>On a hub tour, <c>Hub</c> and <c>Rotation</c> say where the leg belongs: the rotation of that hub the label names,
/// or — both empty — the connection between two hubs (note 2026-10-28-hub-e-rotazioni-da-file). Any other tour leaves them
/// empty.</para>
/// </summary>
public sealed record LegImportRowDto(
    string DepartureIcao,
    string ArrivalIcao,
    IReadOnlyList<string>? Callsigns,
    IReadOnlyList<string>? FlightNumbers,
    IReadOnlyList<string>? AircraftTypes,
    DateTime? ReleaseAt,
    string? Hub = null,
    string? Rotation = null);

/// <summary>
/// The rows of the file, in their order — which is the order of the tour — and what to do with the legs it does not
/// contain. <c>Reason</c> is owed when the import retires, restores or changes a leg with reports; <c>Fingerprint</c>
/// is the one the preview answered, so that what is applied is what was looked at.
/// </summary>
public sealed record LegImportRequest(
    IReadOnlyList<LegImportRowDto> Rows,
    LegImportMode Mode,
    string? Reason,
    string? Fingerprint);

/// <summary>What the import does to one leg.</summary>
public enum LegImportOutcome
{
    /// <summary>A row of the file no leg matches: a new leg.</summary>
    Added,

    /// <summary>The same leg, with a callsign, a flight number, aircraft types or a release of the file's.</summary>
    Changed,

    /// <summary>The same leg, as it is.</summary>
    Unchanged,

    /// <summary>A retired leg the file names again: back in the tour (ADR-051, Carmine 22 September 2026).</summary>
    Restored,

    /// <summary>Not in the file, and left alone: "merge", or a leg already retired.</summary>
    Kept,

    /// <summary>Not in the file, without reports, on "replace": deleted.</summary>
    Deleted,

    /// <summary>Not in the file, with reports, on "replace": retired with the import's reason.</summary>
    Retired,
}

/// <summary>
/// One line of the preview: the outcome, the row of the file (from 0) if there is one, the leg if there is one, its
/// number now and after, and the fields that change. On a hub tour, the hub of the leg's rotation and the place of the
/// rotation in its hub after the import; neither for a connection.
/// </summary>
public sealed record LegImportLineDto(
    LegImportOutcome Outcome,
    int? Row,
    long? LegId,
    int? NumberBefore,
    int? NumberAfter,
    string DepartureIcao,
    string ArrivalIcao,
    IReadOnlyList<string> Changes,
    bool HasReports,
    string? Hub = null,
    int? Rotation = null);

/// <summary>
/// The differences, computed by the server and written nowhere; the fingerprint the apply must carry back. On a hub tour,
/// the hubs the import adds and the ones it deletes.
/// </summary>
public sealed record LegImportPreviewDto(
    IReadOnlyList<LegImportLineDto> Lines,
    bool ReasonRequired,
    string Fingerprint,
    IReadOnlyList<string>? HubsAdded = null,
    IReadOnlyList<string>? HubsDeleted = null);

/// <summary>
/// One row of the file, made a leg by the server like a leg of the editor, and where the file puts it: the rotation
/// <see cref="Rotation"/> of the hub <see cref="Hub"/>, or — both null — a leg by itself (a connection, on a hub tour).
/// </summary>
public sealed record ImportRow(int Row, Leg Leg, string? Hub = null, string? Rotation = null);

/// <summary>
/// The comparison of the file with the shape of the tour, and nothing else: a pure function, unit tested (T8; note
/// 2026-10-28-hub-e-rotazioni-da-file). What is matched, kept, deleted or retired is a <b>unit</b>: a leg by itself — every
/// leg of a tour that is not a hub tour, a connection of one that is — or a rotation with its legs, whole, never one of
/// them alone (§1.4.1).
/// <para><b>The same unit</b> is the same hub and the same legs in the same order, by departure and arrival; a leg by
/// itself is the same departure and arrival. When the same unit is in the tour more than once, they are paired in their
/// order, the ones still flown first (Carmine, 22 September 2026: no column of numbers — the order of the rows is the order
/// of the tour). A unit of the file no unit matches is new, in its place.</para>
/// <para><b>The order after</b> is the file's; a unit the file does not contain and that stays — every one on "merge", the
/// ones with reports on "replace" — keeps its place after the unit that came before it. The hubs come in the order the
/// units first name them, the rotations of a hub in the order of their legs. A hub the file names and the tour has not is
/// added; on "replace", a hub the file names nowhere and that is left without rotations is deleted.</para>
/// <para>The plan writes nothing until it is applied: the places of the hubs and rotations are its own until
/// <see cref="Place"/>.</para>
/// </summary>
public sealed class LegImportPlan
{
    private readonly Dictionary<TourHub, int> hubPlaces;
    private readonly Dictionary<Unit, int> rotationPlaces;

    private LegImportPlan(
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

        // The places: hubs in their order, the rotations of a hub in the order of their legs.
        hubPlaces = hubs.Select((hub, index) => (hub, index)).ToDictionary(pair => pair.hub, pair => pair.index + 1);
        rotationPlaces = [];
        foreach (var group in units.Where(unit => unit.Rotation is not null).GroupBy(unit => unit.HubIcao, StringComparer.Ordinal))
        {
            var place = 1;
            foreach (var unit in group)
            {
                rotationPlaces[unit] = place++;
            }
        }
    }

    /// <summary>Every leg of the tour after the import, in its order, deleted ones last with no number.</summary>
    public IReadOnlyList<Step> Steps { get; }

    /// <summary>The hubs after the import, in their order: the tour's that stay and the new ones (<c>Id</c> 0).</summary>
    public IReadOnlyList<TourHub> Hubs { get; }

    public IReadOnlyList<TourHub> DeletedHubs { get; }

    /// <summary>The units after the import, in their order; a new rotation has <c>Id</c> 0 until it is saved.</summary>
    public IReadOnlyList<Unit> Units { get; }

    public IReadOnlyList<Rotation> DeletedRotations { get; }

    /// <summary>The hubs the import adds, by their ICAO.</summary>
    public IReadOnlyList<string> HubsAdded => [.. Hubs.Where(hub => hub.Id == 0).Select(hub => hub.Icao)];

    public bool ReasonRequired => Steps.Any(step =>
        step.Outcome is LegImportOutcome.Retired or LegImportOutcome.Restored
        || (step.Outcome == LegImportOutcome.Changed && step.HasReports));

    public bool Restores => Steps.Any(step => step.Outcome == LegImportOutcome.Restored);

    /// <summary>
    /// What happens to every leg of a tour that has no hubs: <paramref name="incoming"/> are the rows of the file already
    /// made legs by the server (airports frozen, values normalised), in the file's order, each a unit by itself.
    /// </summary>
    public static LegImportPlan Make(
        IReadOnlyList<Leg> existing,
        IReadOnlyList<Leg> incoming,
        LegImportMode mode,
        IReadOnlySet<long> withReports)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        return Make([], [], existing, [.. incoming.Select((leg, row) => new ImportRow(row, leg))], mode, withReports);
    }

    /// <summary>
    /// What happens to every hub, rotation and leg. <paramref name="rows"/> are the rows of the file already made legs by the
    /// server and, on a hub tour, already checked whole — the rows of a rotation consecutive, of its size, out of its hub and
    /// back —, in the file's order.
    /// </summary>
    public static LegImportPlan Make(
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<Rotation> rotations,
        IReadOnlyList<Leg> existing,
        IReadOnlyList<ImportRow> rows,
        LegImportMode mode,
        IReadOnlySet<long> withReports)
    {
        ArgumentNullException.ThrowIfNull(hubs);
        ArgumentNullException.ThrowIfNull(rotations);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(withReports);

        var tourUnits = TourUnits(hubs, rotations, existing);
        var fileUnits = FileUnits(rows);

        // The units of the tour, each a queue by what it is, the ones still flown first: a file's unit takes the first one free.
        var free = tourUnits
            .Where(unit => unit.Legs.Count > 0)
            .GroupBy(unit => unit.Signature, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<Unit>(group.OrderBy(unit => unit.IsRetired)),
                StringComparer.Ordinal);

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
                var changes = Changes(leg, row.Leg);
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

        var (hubsAfter, deletedHubs) = HubsAfter(hubs, after, rows, mode);

        return new LegImportPlan(
            steps,
            hubsAfter,
            deletedHubs,
            after,
            [.. gone.Select(unit => unit.Rotation).OfType<Rotation>()]);
    }

    /// <summary>What the preview shows: every leg, and on a hub tour its hub and the place of its rotation in the hub after the import.</summary>
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
            step.Outcome != LegImportOutcome.Deleted && rotationPlaces.TryGetValue(step.Unit, out var place) ? place : null)),
    ];

    /// <summary>The places of the plan on the hubs and rotations it keeps or adds: the apply's, never the preview's.</summary>
    public void Place()
    {
        foreach (var (hub, place) in hubPlaces)
        {
            if (hub.Sort != place)
            {
                hub.Sort = place;
            }
        }

        foreach (var (unit, place) in rotationPlaces)
        {
            if (unit.Rotation!.Sort != place)
            {
                unit.Rotation.Sort = place;
            }
        }
    }

    /// <summary>
    /// The plan on the legs: the file's values on the legs it matches (their groups kept), the retired units it names back,
    /// the absent ones deleted or retired, every number the plan's; a leg a row names is what the file makes it — in its
    /// rotation, or a leg by itself. The rotations and the hubs are the caller's to write first, so that a new leg can be put
    /// in its new rotation (<see cref="Unit.Rotation"/>, saved). Returns the new legs, to be added, and the deleted ones, to
    /// be removed; the caller owns the context.
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
                    Take(leg, step.File!);
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

            // A leg the file does not name stays what it was.
            if (step.File is { } file)
            {
                if (leg.Kind != file.Kind)
                {
                    leg.Kind = file.Kind;
                }

                if (leg.RotationId != step.Unit.Rotation?.Id)
                {
                    leg.RotationId = step.Unit.Rotation?.Id;
                }
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

    /// <summary>The file's units: the consecutive rows of one rotation of a hub, or one row by itself.</summary>
    private static List<FileUnit> FileUnits(IReadOnlyList<ImportRow> rows)
    {
        var units = new List<FileUnit>();
        foreach (var row in rows)
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
    /// then the tour's others. On "replace", one of those others is deleted when nothing names it any more: not the file,
    /// not even as the end of a connection, and not a connection that stays for its reports.
    /// </summary>
    private static (List<TourHub> Hubs, List<TourHub> Deleted) HubsAfter(
        IReadOnlyList<TourHub> hubs,
        IReadOnlyList<Unit> after,
        IReadOnlyList<ImportRow> rows,
        LegImportMode mode)
    {
        var result = new List<TourHub>();
        var deleted = new List<TourHub>();

        foreach (var icao in after.Select(unit => unit.HubIcao).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            result.Add(hubs.FirstOrDefault(hub => hub.Icao == icao) ?? new TourHub { Icao = icao });
        }

        // The file's hubs and the ends of its connections, and the ends of every connection that stays: one the file no
        // longer names, kept for its reports, still needs both its hubs.
        var named = rows
            .SelectMany(row => row.Hub is null ? new[] { row.Leg.DepartureIcao, row.Leg.ArrivalIcao } : [row.Hub])
            .Concat(after.Where(unit => unit.Rotation is null).SelectMany(unit => unit.Legs).SelectMany(leg => new[] { leg.DepartureIcao, leg.ArrivalIcao }))
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

        return (result, deleted);
    }

    /// <summary>The fields of the file that differ from the leg's, by the names of the payload.</summary>
    private static List<string> Changes(Leg leg, Leg file)
    {
        var changes = new List<string>();
        if (!leg.Callsigns.SequenceEqual(file.Callsigns, StringComparer.Ordinal))
        {
            changes.Add("callsigns");
        }

        if (!leg.FlightNumbers.SequenceEqual(file.FlightNumbers, StringComparer.Ordinal))
        {
            changes.Add("flightNumbers");
        }

        if (!leg.Aircraft.Types.SequenceEqual(file.Aircraft.Types, StringComparer.Ordinal))
        {
            changes.Add("aircraft");
        }

        if (leg.ReleaseAt != file.ReleaseAt)
        {
            changes.Add("releaseAt");
        }

        return changes;
    }

    private static void Take(Leg leg, Leg file)
    {
        leg.Callsigns = file.Callsigns;
        leg.FlightNumbers = file.FlightNumbers;
        leg.Aircraft = new AllowedAircraft(file.Aircraft.Types, leg.Aircraft.GroupIds);
        leg.ReleaseAt = file.ReleaseAt;
    }

    /// <summary>A rotation of the tour or of the file with its legs, or a leg by itself: what is matched, kept or removed whole.</summary>
    public sealed class Unit(Rotation? rotation, string? hubIcao)
    {
        /// <summary>The rotation; a new one has <c>Id</c> 0 until it is saved. Null for a leg by itself.</summary>
        public Rotation? Rotation { get; } = rotation;

        public string? HubIcao { get; } = hubIcao;

        public List<Leg> Legs { get; } = [];

        /// <summary>Every leg retired: a rotation retired whole, or a leg by itself retired.</summary>
        internal bool IsRetired => Legs.Count > 0 && Legs.All(leg => leg.IsRetired);

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

    private sealed record FileUnit(string? Hub, string? Label, List<ImportRow> Rows)
    {
        public string Signature => Unit.Key(Hub, Rows.Select(row => row.Leg));
    }
}

/// <summary>
/// The limits of one request: rows, the reason, and the length of a row's hub and rotation. What each row says is
/// checked row by row by the server.
/// </summary>
public sealed class LegImportRequestValidator : AbstractValidator<LegImportRequest>
{
    /// <summary>A tour of the division has tens of legs; a thousand rows is a file of something else.</summary>
    public const int MaxRows = 1000;

    /// <summary>A hub is an airport: its ICAO.</summary>
    public const int MaxHubLength = 4;

    /// <summary>A rotation's label only keeps its rows together; "1", "A", "Morning" — never a sentence.</summary>
    public const int MaxRotationLength = 32;

    public LegImportRequestValidator()
    {
        RuleFor(request => request.Rows).NotNull().WithMessage("errors.required");
        RuleFor(request => request.Rows.Count).LessThanOrEqualTo(MaxRows).When(request => request.Rows is not null)
            .OverridePropertyName("rows").WithMessage("flightops:errors.importTooManyRows");
        RuleForEach(request => request.Rows).ChildRules(row =>
        {
            row.RuleFor(line => line.Hub).Must(hub => (hub?.Trim().Length ?? 0) <= MaxHubLength)
                .OverridePropertyName("hub").WithMessage("errors.text.tooLong");
            row.RuleFor(line => line.Rotation).Must(label => (label?.Trim().Length ?? 0) <= MaxRotationLength)
                .OverridePropertyName("rotation").WithMessage("errors.text.tooLong");
        }).When(request => request.Rows is not null);
        RuleFor(request => request.Mode).IsInEnum().WithMessage("errors.required");
        RuleFor(request => request.Reason).MaximumLength(LegValidation.MaxReasonLength).WithMessage("errors.text.tooLong");
    }
}
