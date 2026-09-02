using System.Text.RegularExpressions;
using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>
/// What a staff position means once it is recognised. The vocabulary is the same for every
/// division: IVAO names positions the same way everywhere, so this map lives in the code and needs
/// no configuration (plan section 4.1).
/// </summary>
public enum StaffRole
{
    Director,
    SpecialOps,
    FlightOps,
    AtcOps,
    Training,
    Trainer,
    Membership,
    Events,
    PublicRelations,
    Web,
    FirChief,
    FirAssistantChief,
    FirAdvisor,

    /// <summary>A position of IVAO headquarters: read only, and never a department of this division.</summary>
    HqStaff,
}

/// <summary>
/// A recognised staff position: what IVAO wrote, and what the hub makes of it.
/// <see cref="Department"/> is null for FIR and headquarters positions, which own no department.
/// </summary>
public sealed record StaffPosition(string Raw, Department? Department, StaffLevel Level, string? Fir, StaffRole Role);

/// <summary>
/// Turns <c>IT-EC</c> or <c>LIRR-CH</c> into a department, a level and an optional FIR.
/// Universal on purpose: a division that forks changes <c>division.json</c>, never this file.
/// </summary>
public static partial class StaffRoleMap
{
    /// <summary>Prefix of the positions of IVAO headquarters, which belong to no division.</summary>
    private const string HeadquartersPrefix = "HQ";

    /// <summary>
    /// Divisional suffixes, in the order they must be tried: from the most specific to the most
    /// general, because <c>T01</c>, <c>TA1</c> and <c>TAC</c> would otherwise shadow each other
    /// (plan section 4.1).
    /// </summary>
    private static readonly (Regex Pattern, Department Department, StaffLevel Level, StaffRole Role)[] DivisionalRules =
    [
        // Training comes first: T01-T99 before TA1-TA9, and both before TC and TAC.
        (Trainer(), Department.TD, StaffLevel.Member, StaffRole.Trainer),
        (TrainingAdvisor(), Department.TD, StaffLevel.Advisor, StaffRole.Training),
        (Exact("TAC"), Department.TD, StaffLevel.Assistant, StaffRole.Training),
        (Exact("TC"), Department.TD, StaffLevel.Coordinator, StaffRole.Training),

        (Exact("DIR"), Department.HQ, StaffLevel.Coordinator, StaffRole.Director),
        (Exact("ADIR"), Department.HQ, StaffLevel.Assistant, StaffRole.Director),

        (Advisor("SOA"), Department.SOD, StaffLevel.Advisor, StaffRole.SpecialOps),
        (Exact("SOAC"), Department.SOD, StaffLevel.Assistant, StaffRole.SpecialOps),
        (Exact("SOC"), Department.SOD, StaffLevel.Coordinator, StaffRole.SpecialOps),

        (Advisor("FOA"), Department.FOD, StaffLevel.Advisor, StaffRole.FlightOps),
        (Exact("FOAC"), Department.FOD, StaffLevel.Assistant, StaffRole.FlightOps),
        (Exact("FOC"), Department.FOD, StaffLevel.Coordinator, StaffRole.FlightOps),

        (Advisor("AOA"), Department.AOD, StaffLevel.Advisor, StaffRole.AtcOps),
        (Exact("AOAC"), Department.AOD, StaffLevel.Assistant, StaffRole.AtcOps),
        (Exact("AOC"), Department.AOD, StaffLevel.Coordinator, StaffRole.AtcOps),

        (Advisor("MA"), Department.MD, StaffLevel.Advisor, StaffRole.Membership),
        (Exact("MAC"), Department.MD, StaffLevel.Assistant, StaffRole.Membership),
        (Exact("MC"), Department.MD, StaffLevel.Coordinator, StaffRole.Membership),

        (Advisor("EA"), Department.ED, StaffLevel.Advisor, StaffRole.Events),
        (Exact("EAC"), Department.ED, StaffLevel.Assistant, StaffRole.Events),
        (Exact("EC"), Department.ED, StaffLevel.Coordinator, StaffRole.Events),

        (Advisor("PRA"), Department.PRD, StaffLevel.Advisor, StaffRole.PublicRelations),
        (Exact("PRAC"), Department.PRD, StaffLevel.Assistant, StaffRole.PublicRelations),
        (Exact("PRC"), Department.PRD, StaffLevel.Coordinator, StaffRole.PublicRelations),

        (Advisor("WMA"), Department.WD, StaffLevel.Advisor, StaffRole.Web),
        (Exact("AWM"), Department.WD, StaffLevel.Assistant, StaffRole.Web),
        (Exact("WM"), Department.WD, StaffLevel.Coordinator, StaffRole.Web),
    ];

    private static readonly (Regex Pattern, StaffLevel Level, StaffRole Role)[] FirRules =
    [
        (Advisor("CHA"), StaffLevel.Advisor, StaffRole.FirAdvisor),
        (Exact("ACH"), StaffLevel.Assistant, StaffRole.FirAssistantChief),
        (Exact("CH"), StaffLevel.Coordinator, StaffRole.FirChief),
    ];

    /// <summary>
    /// Reads one raw position. Returns null when it belongs to another division, or when the suffix
    /// is not one this map knows: an unrecognised position is never lost, it stays in
    /// <c>hub_user_staff_positions.raw</c> and is simply worth nothing.
    /// </summary>
    /// <param name="position">The raw position as IVAO wrote it.</param>
    /// <param name="divisionCode">The code of this division, from <c>division.json</c>.</param>
    /// <param name="firIds">
    /// The FIRs of the division, from <c>ref_ivao_centers</c>. Empty until the reference data has
    /// been synchronised, which only means FIR positions are not recognised yet.
    /// </param>
    public static StaffPosition? Parse(string? position, string divisionCode, IReadOnlySet<string> firIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(divisionCode);
        ArgumentNullException.ThrowIfNull(firIds);

        if (string.IsNullOrWhiteSpace(position))
        {
            return null;
        }

        var raw = position.Trim().ToUpperInvariant();
        var separator = raw.IndexOf('-', StringComparison.Ordinal);
        if (separator <= 0 || separator == raw.Length - 1)
        {
            return null;
        }

        var prefix = raw[..separator];
        var suffix = raw[(separator + 1)..];

        if (prefix.Equals(divisionCode, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (pattern, department, level, role) in DivisionalRules)
            {
                if (pattern.IsMatch(suffix))
                {
                    return new StaffPosition(raw, department, level, null, role);
                }
            }

            return null;
        }

        if (firIds.Contains(prefix))
        {
            foreach (var (pattern, level, role) in FirRules)
            {
                if (pattern.IsMatch(suffix))
                {
                    return new StaffPosition(raw, null, level, prefix, role);
                }
            }

            return null;
        }

        // Headquarters: no department of this division, read only (plan section 4.1).
        if (prefix.Equals(HeadquartersPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new StaffPosition(raw, null, StaffLevel.Member, null, StaffRole.HqStaff);
        }

        // Another division, or a prefix nobody knows.
        return null;
    }

    private static Regex Exact(string suffix) => new($"^{suffix}$", RegexOptions.CultureInvariant);

    private static Regex Advisor(string prefix) => new($"^{prefix}[1-9]$", RegexOptions.CultureInvariant);

    [GeneratedRegex("^T(0[1-9]|[1-9][0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex Trainer();

    [GeneratedRegex("^TA[1-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex TrainingAdvisor();
}
