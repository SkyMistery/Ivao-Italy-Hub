using System.Text.RegularExpressions;
using IvaoHub.Core.Division;

namespace IvaoHub.Modules.FlightOps.Shape;

public enum CallsignMode
{
    Allow,
    Deny,
}

/// <summary>
/// What a constraint looks at (Carmine, 21 September 2026, note <c>2026-09-21-la-forma-dei-tour</c>): the airline — the
/// three letters a callsign starts with, the rest being the pilot's choice — or a whole callsign, which only a
/// <see cref="CallsignMode.Deny"/> names (the Vintage Jet of Toursystem forbids four callsigns of fatal accidents).
/// </summary>
public enum CallsignMatch
{
    Airline,
    Exact,
}

/// <summary>
/// A callsign constraint (design M2 §1.6), <c>fo_callsign_rules</c>: on a tour, or on one of its legs. The callsign of the
/// real flight on a leg is only a suggestion; these rows are what blocks a report (answer 5, §3.2).
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class CallsignRule : ITourChild, IAuditable
{
    public long Id { get; set; }

    public long TourId { get; set; }

    /// <summary>The leg it is about; none, the whole tour.</summary>
    public long? LegId { get; set; }

    public CallsignMode Mode { get; set; }

    public CallsignMatch Match { get; set; }

    /// <summary>Three letters for an airline, a callsign otherwise; upper case.</summary>
    public string Value { get; set; } = string.Empty;

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>
    /// Whether the tour it belongs to is a template, read with the tour before the permission is asked: changing a
    /// template needs <c>Tours.ManageTemplates</c> (design M2 §1.10). Not a column.
    /// </summary>
    public bool OnTemplate { get; set; }
}

/// <summary>Why a callsign may or may not be flown.</summary>
public enum CallsignVerdict
{
    Allowed,

    /// <summary>A <see cref="CallsignMode.Deny"/> of some level names it.</summary>
    Denied,

    /// <summary>The nearest level with <see cref="CallsignMode.Allow"/> rules names another airline.</summary>
    NotAllowed,
}

/// <summary>
/// Who decides a callsign (Carmine, 21 September 2026): the <b>allows of the nearest level</b> that has any — the leg,
/// then the tour, then the parent of a subtour — and <b>every deny of every level</b>, which always wins. No allow
/// anywhere admits every airline. A pure function: the report's form uses it in T11.
/// </summary>
public static partial class CallsignRules
{
    [GeneratedRegex("^[A-Z]{3}$")]
    public static partial Regex AirlinePattern();

    [GeneratedRegex("^[A-Z0-9]{2,16}$")]
    public static partial Regex CallsignPattern();

    public static string Normalize(string? text) => (text ?? string.Empty).Trim().ToUpperInvariant();

    /// <param name="callsign">What the pilot flew with.</param>
    /// <param name="levels">The rules of each level, the most specific first: leg, tour, parent.</param>
    public static CallsignVerdict Judge(string callsign, IReadOnlyList<IReadOnlyList<CallsignRule>> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        var flown = Normalize(callsign);

        if (levels.SelectMany(level => level).Any(rule => rule.Mode == CallsignMode.Deny && Matches(rule, flown)))
        {
            return CallsignVerdict.Denied;
        }

        var allows = levels
            .Select(level => level.Where(rule => rule.Mode == CallsignMode.Allow).ToList())
            .FirstOrDefault(level => level.Count > 0);

        return allows is null || allows.Any(rule => Matches(rule, flown)) ? CallsignVerdict.Allowed : CallsignVerdict.NotAllowed;
    }

    private static bool Matches(CallsignRule rule, string callsign) => rule.Match switch
    {
        CallsignMatch.Airline => callsign.StartsWith(rule.Value, StringComparison.Ordinal),
        _ => callsign == rule.Value,
    };
}
