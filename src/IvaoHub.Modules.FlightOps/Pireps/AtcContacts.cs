using System.Text.RegularExpressions;
using IvaoHub.Core.Atc;
using IvaoHub.Modules.FlightOps.Rules;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>Where a controller on a report comes from (design M2 §3.3). Stored by name.</summary>
public enum AtcContactOrigin
{
    /// <summary>Online along the flight, proposed by the hub, and kept by the pilot.</summary>
    Proposed,

    /// <summary>Written by the pilot: the archive did not propose it.</summary>
    Added,

    /// <summary>Proposed by the hub and taken away by the pilot, who says they did not contact it (Carmine, 23 September 2026).</summary>
    Removed,
}

/// <summary>What a controller allowed (design M2 §3.3); each kind declares which checks it softens (Toursystem ADR-014).</summary>
public enum ExemptionKind
{
    FreeSpeed,
    DirectRouting,
    LevelChange,

    /// <summary>Softens nothing: the note is for the validator, who judges it.</summary>
    Other,
}

/// <summary>Whether the position of an exemption was online during the flight, as far as the archive can tell.</summary>
public enum ExemptionStatus
{
    Online,

    /// <summary>The archive covers the whole flight and does not list it: shown to the validator, never a refusal.</summary>
    NotOnline,

    /// <summary>No archive, or one that does not reach back to the flight: the validator decides.</summary>
    Unverifiable,
}

/// <summary>A controller as the pilot declares it: a callsign, and the frequency when they want to write one.</summary>
public sealed record AtcContactWriteDto(string Callsign, string? Frequency);

/// <summary>An exemption as the pilot declares it: the position among the contacted ones, what it allowed, and a note.</summary>
public sealed record AtcExemptionWriteDto(string Callsign, ExemptionKind Kind, string? Note);

/// <summary>A controller on a report, where it came from included.</summary>
public sealed record AtcContactDto(string Callsign, string? Frequency, AtcContactOrigin Origin);

/// <summary>An exemption on a report: with its status at the send, and the checks it softens as they were then.</summary>
public sealed record AtcExemptionDto(
    string Callsign,
    ExemptionKind Kind,
    string? Note,
    ExemptionStatus Status,
    IReadOnlyList<string> Softens);

/// <summary>
/// What the form shows while the pilot fills it in: the controllers online along the chosen flights, or «not available» —
/// and the credit the outlines of the regions ask for, next to an answer derived from them.
/// </summary>
public sealed record AtcProposalDto(bool Available, IReadOnlyList<AtcContactDto> Proposed, string Attribution);

/// <summary>The perimeter of each kind of exemption (Carmine, 23 September 2026): no exemption softens everything.</summary>
public static class ExemptionCatalog
{
    /// <summary>
    /// <c>DirectRouting</c> softens no check of M2 — none of them reads the route — and will soften the adherence to the route
    /// when there is one; <c>Other</c> softens nothing at all.
    /// </summary>
    public static IReadOnlyList<string> Softens(ExemptionKind kind) => kind switch
    {
        ExemptionKind.FreeSpeed => [CheckCatalog.Speed250],
        ExemptionKind.LevelChange => [CheckCatalog.SemicircularLevels],
        _ => [],
    };
}

/// <summary>
/// The facts of one flight the proposal reads: where it started and where it actually landed — the diversion airport, for the
/// first flight of a diversion —, when, and the regions it was seen in.
/// </summary>
public sealed record FlightPassage(
    string DepartureIcao,
    string LandedAtIcao,
    DateTime StartedAt,
    DateTime EndedAt,
    DateTime TakeoffAt,
    DateTime? LandingAt,
    IReadOnlyList<RegionCrossing> Crossings);

/// <summary>A moment of the flight in the air, and the regions it was in then.</summary>
public sealed record RegionCrossing(DateTime At, IReadOnlyList<string> Regions);

/// <summary>
/// The controllers contacted, proposed and declared (design M2 §3.3): the light version, on the server — the positions
/// online at the departure, the arrival and the diversion, and in the regions the track was seen in, at the moment it was
/// there. The precise version along the route is the validator's agent's (§6.6).
/// <para>Pure: the archive and the regions are gathered by the caller.</para>
/// </summary>
public static partial class AtcProposal
{
    /// <summary>How long after take-off the departure's positions are still the ones a pilot talks to (APP, DEP).</summary>
    public static readonly TimeSpan AfterTakeoff = TimeSpan.FromMinutes(20);

    /// <summary>How long before landing the arrival's positions already are (APP, TWR).</summary>
    public static readonly TimeSpan BeforeLanding = TimeSpan.FromMinutes(30);

    public const int MaxContacts = 30;
    public const int MaxExemptions = 10;

    /// <summary>The positions to propose, each once, in the order the flight met them.</summary>
    public static IReadOnlyList<AtcPresence> Propose(IReadOnlyList<FlightPassage> flights, AtcActivity activity)
    {
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(activity);

        var proposed = new List<AtcPresence>();

        void Add(IEnumerable<AtcPresence> found)
        {
            foreach (var presence in found.OrderBy(presence => presence.StartedAt))
            {
                if (!proposed.Any(kept => string.Equals(kept.Callsign, presence.Callsign, StringComparison.OrdinalIgnoreCase)))
                {
                    proposed.Add(presence);
                }
            }
        }

        foreach (var flight in flights)
        {
            Add(activity.Online.Where(presence =>
                presence.Station == flight.DepartureIcao
                && presence.Overlaps(flight.StartedAt, flight.TakeoffAt + AfterTakeoff)));

            foreach (var crossing in flight.Crossings.OrderBy(crossing => crossing.At))
            {
                Add(activity.Online.Where(presence =>
                    crossing.Regions.Contains(presence.Station, StringComparer.Ordinal)
                    && presence.Overlaps(crossing.At, crossing.At)));
            }

            var landing = flight.LandingAt ?? flight.EndedAt;
            Add(activity.Online.Where(presence =>
                presence.Station == flight.LandedAtIcao
                && presence.Overlaps(landing - BeforeLanding, flight.EndedAt)));
        }

        return proposed;
    }

    /// <summary>
    /// The list the report keeps: every proposed position — kept when the pilot declares it, removed when not — and then the
    /// ones the pilot added. The proposal is the server's, worked out again at the send: a <c>proposed</c> the browser
    /// claims proves nothing.
    /// </summary>
    public static IReadOnlyList<AtcContactDto> Merge(IReadOnlyList<AtcPresence> proposed, IReadOnlyList<AtcContactWriteDto> declared)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(declared);

        var wanted = declared
            .Select(contact => (Callsign: Normalize(contact.Callsign), Frequency: Frequency(contact.Frequency)))
            .DistinctBy(contact => contact.Callsign)
            .ToList();

        var merged = proposed
            .Select(presence =>
            {
                var match = wanted.FirstOrDefault(contact => contact.Callsign == presence.Callsign);
                return new AtcContactDto(
                    presence.Callsign,
                    presence.Frequency ?? match.Frequency,
                    match.Callsign is null ? AtcContactOrigin.Removed : AtcContactOrigin.Proposed);
            })
            .ToList();

        merged.AddRange(wanted
            .Where(contact => !proposed.Any(presence => presence.Callsign == contact.Callsign))
            .Select(contact => new AtcContactDto(contact.Callsign, contact.Frequency, AtcContactOrigin.Added)));

        return merged;
    }

    /// <summary>
    /// Whether an exemption's position was online at some moment of the flights: «not online» only where the archive covers
    /// the whole interval, «unverifiable» where there is no archive or it does not reach back that far.
    /// </summary>
    public static ExemptionStatus StatusOf(string callsign, DateTime fromUtc, DateTime toUtc, AtcActivity? activity)
    {
        ArgumentNullException.ThrowIfNull(callsign);

        if (activity is null)
        {
            return ExemptionStatus.Unverifiable;
        }

        if (activity.Of(callsign).Any(presence => presence.Overlaps(fromUtc, toUtc)))
        {
            return ExemptionStatus.Online;
        }

        return activity.Covers(callsign, fromUtc) ? ExemptionStatus.NotOnline : ExemptionStatus.Unverifiable;
    }

    /// <summary>
    /// The refusals of what the pilot declared, by i18n key: a callsign that is not one, a frequency that is not one, too
    /// many of either, an exemption on a position that is not among the contacted ones, an <c>Other</c> with no note.
    /// </summary>
    public static IReadOnlyList<(string Field, string Key)> Problems(
        IReadOnlyList<AtcContactWriteDto> contacts,
        IReadOnlyList<AtcExemptionWriteDto> exemptions)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(exemptions);

        var problems = new List<(string, string)>();

        if (contacts.Count > MaxContacts)
        {
            problems.Add(("atcContacts", "flightops:errors.atcTooMany"));
        }

        if (contacts.Any(contact => !CallsignShape().IsMatch(Normalize(contact.Callsign))))
        {
            problems.Add(("atcContacts", "flightops:errors.atcCallsign"));
        }

        if (contacts.Any(contact => !string.IsNullOrWhiteSpace(contact.Frequency) && Frequency(contact.Frequency) is null))
        {
            problems.Add(("atcContacts", "flightops:errors.atcFrequency"));
        }

        if (exemptions.Count > MaxExemptions)
        {
            problems.Add(("exemptions", "flightops:errors.exemptionTooMany"));
        }

        var contacted = contacts.Select(contact => Normalize(contact.Callsign)).ToHashSet(StringComparer.Ordinal);
        if (exemptions.Any(exemption => !contacted.Contains(Normalize(exemption.Callsign))))
        {
            problems.Add(("exemptions", "flightops:errors.exemptionNotContacted"));
        }

        if (exemptions.Any(exemption => exemption.Kind == ExemptionKind.Other && string.IsNullOrWhiteSpace(exemption.Note)))
        {
            problems.Add(("exemptions", "flightops:errors.exemptionNoteRequired"));
        }

        if (exemptions.Any(exemption => exemption.Note?.Trim().Length > PirepValidation.MaxTextLength))
        {
            problems.Add(("exemptions", "errors.text.tooLong"));
        }

        return problems;
    }

    public static string Normalize(string? callsign) => (callsign ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>A frequency as the network writes it, <c>118.705</c>; null when what was written is not one.</summary>
    public static string? Frequency(string? text)
    {
        var trimmed = text?.Trim().Replace(',', '.');
        return trimmed is not null && FrequencyShape().IsMatch(trimmed) ? trimmed : null;
    }

    /// <summary><c>LIRF_TWR</c>, <c>LIRR_N_CTR</c>: a station, then one or more parts, letters and digits.</summary>
    [GeneratedRegex("^[A-Z0-9]{2,8}(_[A-Z0-9]{1,8}){1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CallsignShape();

    [GeneratedRegex(@"^1[0-3][0-9]\.[0-9]{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FrequencyShape();
}
