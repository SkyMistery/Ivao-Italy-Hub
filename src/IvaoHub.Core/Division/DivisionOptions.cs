using System.ComponentModel.DataAnnotations;

namespace IvaoHub.Core.Division;

/// <summary>
/// Everything the code needs in order to behave like a given division, and nothing else
/// (plan section 4.1). Content, links and translations are not configuration.
/// Bound from <c>config/division.json</c> and validated at startup.
/// </summary>
public sealed record DivisionOptions
{
    /// <summary>Division code as IVAO writes it. Staff positions are matched as <c>^{code}-</c>.</summary>
    [Required]
    public string Code { get; init; } = string.Empty;

    /// <summary>ISO country used to ask the IVAO API for the FIRs and the airports of the division.</summary>
    [Required]
    public string CountryId { get; init; } = string.Empty;

    /// <summary>Division name, one entry per language in <see cref="Locales"/>.</summary>
    public Dictionary<string, string> Name { get; init; } = [];

    /// <summary>Public host, used to build absolute links.</summary>
    [Required]
    public string Domain { get; init; } = string.Empty;

    /// <summary>Languages of the division. Each one needs a <c>locales/{lang}/</c> directory.</summary>
    public string[] Locales { get; init; } = [];

    /// <summary>Fallback language; it must be one of <see cref="Locales"/>.</summary>
    [Required]
    public string DefaultLocale { get; init; } = string.Empty;

    /// <summary>IANA time zone, used to show local times next to UTC.</summary>
    [Required]
    public string Timezone { get; init; } = string.Empty;

    /// <summary>
    /// The ICAO prefixes of the division, upper case, 1 to 4 letters. A safety net rather than a
    /// source: the FIRs and the airports themselves come from the IVAO API. The synchronisation
    /// uses them to notice that a snapshot has nothing to do with this division, which is what a
    /// wrong <see cref="CountryId"/> looks like from the inside.
    /// </summary>
    public string[] IcaoPrefixes { get; init; } = [];

    /// <summary>Optional modules only. Department modules and the editorial core are always on.</summary>
    public Dictionary<string, bool> Modules { get; init; } = [];

    /// <summary>
    /// Bootstrap only: read once, when no super administrator exists yet. After that
    /// <c>hub_users.is_superadmin</c> is the truth and this list is ignored (plan section 4.1).
    /// </summary>
    public int[] SuperAdmins { get; init; } = [];

    /// <summary>How far the authority of a FIR team reaches.</summary>
    public FirStaffScope FirStaffScope { get; init; } = FirStaffScope.All;

    // There is deliberately no ResolveName here. Falling back from one language to another is a
    // rule the hub already has, in Localized<T>.Resolve, and a second copy of it on this type had
    // no caller and would have been the copy that drifted.
}
