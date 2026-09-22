using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>What an error weighs (design M2 §1.7, note 2026-09-14-requisiti-dei-tour §2). Stored by name.</summary>
public enum ErrorCategory
{
    /// <summary>A light error, with no maximum.</summary>
    Info,

    /// <summary>At most <see cref="TourError.YearlyMax"/> times in a calendar year; beyond, rejection is suggested.</summary>
    Warning,

    /// <summary>Rejection is suggested from the first time.</summary>
    Dangerous,
}

/// <summary>
/// A rule of the tours (design M2 §1.7), <c>fo_rules</c>: a <b>general</b> one, for every tour, when it has no tour, and a
/// tour's own otherwise — which may <b>amend</b> a general one, taking its place on that tour with the parameters it
/// changes (§5.2). A rule naming a check carries the check's parameters: the numbers of the regulation live here, where
/// the flight operations department writes them and the pilot reads them.
/// <para>A general rule is the base department's; a tour's is in the care of its tour, taken before the permission is
/// asked and followed when the tour's care changes, as every row of a tour (note 2026-09-18-le-leg-dei-tour).</para>
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class TourRule : IOwnedByDepartment, IAuditable
{
    public long Id { get; set; }

    /// <summary>The tour it belongs to; null for a general rule.</summary>
    public long? TourId { get; set; }

    /// <summary>How the regulation names it: «GR4», «IR3». Once among the general rules, once among a tour's.</summary>
    public string Code { get; set; } = string.Empty;

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    /// <summary>The rule itself, in markdown.</summary>
    public Localized<string> Text { get; set; } = Localized<string>.Empty;

    /// <summary>The general rule a tour's rule takes the place of; never on a general rule.</summary>
    public long? AmendsRuleId { get; set; }

    /// <summary>The check whose parameters it carries (<see cref="CheckCatalog"/>); an amendment has its rule's.</summary>
    public string? CheckKey { get; set; }

    /// <summary>
    /// The parameters of the check as a JSON object: all of them on a rule of its own, only those it changes on an
    /// amendment (Carmine, 22 September 2026).
    /// </summary>
    public string ParametersJson { get; set; } = "{}";

    public int Sort { get; set; }

    /// <summary>A retired rule is no rule: out of the effective rules, and out of every copy.</summary>
    public DateTime? RetiredAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The errors it is linked to, as the payload asked; the save writes the links (not a column).</summary>
    public IReadOnlyList<long>? RequestedErrorIds { get; set; }

    /// <summary>The links to its errors, <c>fo_rule_errors</c>: loaded by whoever needs them, written with the rule.</summary>
    public List<TourRuleError> ErrorLinks { get; set; } = [];

    /// <summary>Whether the tour is a template, read before the permission is asked (as the callsign constraints').</summary>
    public bool OnTemplate { get; set; }

    public bool IsGeneral => TourId is null;
}

/// <summary>
/// An error of the division's catalogue (design M2 §1.7), <c>fo_errors</c>: what a validator marks on a report, with what
/// it weighs, and whether the public may read it (answer 3) — the block <c>flightops.errorCatalog</c> shows those.
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class TourError : IOwnedByDepartment, IAuditable
{
    public long Id { get; set; }

    /// <summary>A short name a validator recognises.</summary>
    public Localized<string> Name { get; set; } = Localized<string>.Empty;

    /// <summary>What it is, for the validator and, when it is public, for the pilot; markdown.</summary>
    public Localized<string> Description { get; set; } = Localized<string>.Empty;

    /// <summary>Examples of it; markdown, optional.</summary>
    public Localized<string> Examples { get; set; } = Localized<string>.Empty;

    public ErrorCategory Category { get; set; }

    /// <summary>How many times in a calendar year before rejection is suggested; a <see cref="ErrorCategory.Warning"/> only.</summary>
    public int? YearlyMax { get; set; }

    /// <summary>The check whose failure suggests it (§6.1).</summary>
    public string? CheckKey { get; set; }

    public bool IsPublic { get; set; }

    public DateTime? RetiredAt { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}

/// <summary>
/// A rule and an error that go together, <c>fo_rule_errors</c>: many to many, and the system points out a rule with none
/// and an error with none (§1.7). Written only by the save of its rule, which the permission already covers.
/// </summary>
public sealed class TourRuleError
{
    public long RuleId { get; set; }

    public long ErrorId { get; set; }
}
