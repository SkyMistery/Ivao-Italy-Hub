using System.ComponentModel.DataAnnotations;
using System.Globalization;

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
    /// The time zone as the runtime knows it, for a schedule that says "at night, here". The
    /// validator already refuses an unknown zone at start up; UTC is only so that a schedule can
    /// never be the thing that stops the site.
    /// </summary>
    public TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(Timezone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Where the mark of the division is served from, or null for a division that has none.
    /// <para>⚠️ It is configuration and not code, and that is the whole point: the hub draws the
    /// mark of whatever division it is running for, and knows nothing about which one that is. A
    /// fork puts its own file where this points and changes nothing else — and a division with no
    /// mark simply leaves this out, which is what the example and the test division do.</para>
    /// <para>An address rather than a media identifier, because the site's own frame is drawn before
    /// anybody signs in and on every page: a row of a table read through the visibility filter is
    /// the wrong shape for something the header needs on the first paint.</para>
    /// </summary>
    public string? LogoUrl { get; init; }

    /// <summary>
    /// Where the icon of a browser tab is served from, or null to leave the browser's own.
    /// <para>A second address and not the same one, because they are two drawings: the mark of the
    /// bar is white for the blue it sits on, and a tab icon has to read on a light tab bar and a
    /// dark one alike. Every brand kit ships both, and a division that has only one points both
    /// keys at it.</para>
    /// </summary>
    public string? FaviconUrl { get; init; }

    /// <summary>
    /// The ICAO prefixes of the division, upper case, 1 to 4 letters. A safety net rather than a
    /// source: the FIRs and the airports themselves come from the IVAO API. The synchronisation
    /// uses them to notice that a snapshot has nothing to do with this division, which is what a
    /// wrong <see cref="CountryId"/> looks like from the inside.
    /// </summary>
    public string[] IcaoPrefixes { get; init; } = [];

    /// <summary>
    /// What the division says about each module, by key: whether an optional one is on, and which
    /// department every row of it is always in the care of (M2, note
    /// 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3). A module the division does not name is
    /// on and has no base department.
    /// </summary>
    public Dictionary<string, ModuleSettings> Modules { get; init; } = [];

    /// <summary>
    /// The shared inbox of each department, by department code, for the notifications the hub
    /// sends. Optional and usually partial: a department with no entry is reached on its people
    /// alone, and a division with no shared inboxes at all leaves the key out (decision note of
    /// 6 September 2026).
    /// <para>It is configuration and not content: an address the code sends to is exactly what
    /// this file is for, and a fork writes its own without a screen or a table.</para>
    /// </summary>
    public Dictionary<string, string> DepartmentMailboxes { get; init; } = [];

    /// <summary>The time the mail to whoever assigns the awards goes out when the division says nothing.</summary>
    public const string DefaultAwardDigestTime = "07:00";

    /// <summary>
    /// When, every day, whoever assigns the awards is told about the signals that entered the queue since the last mail: a
    /// time of day in <see cref="Timezone"/>, written <c>HH:mm</c>, and <c>07:00</c> when left out — the morning of the people
    /// who read it. A day with no new signal sends nothing (M4, E10d, note 2026-09-30-la-mail-a-chi-assegna-gli-award:
    /// Carmine wanted it a setting of the division, not a schedule written in the code).
    /// </summary>
    public string AwardDigestTime { get; init; } = DefaultAwardDigestTime;

    /// <summary>
    /// <see cref="AwardDigestTime"/> as a time of day. The validator refuses one that is not <c>HH:mm</c> at start up; the
    /// default is only so that a schedule can never be the thing that stops the site, as with the time zone.
    /// </summary>
    public TimeOnly ResolveAwardDigestTime() =>
        TimeOnly.TryParseExact(AwardDigestTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : TimeOnly.ParseExact(DefaultAwardDigestTime, "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// The kinds of content a department marks ready and somebody with <c>Content.Approve</c>
    /// publishes, by name: <c>["Page"]</c> for a division whose pages go through its web team and
    /// its direction (note 2026-09-13-contenuti-centralizzati, 3.2). Empty — the default — is a
    /// division where whoever may publish, publishes.
    /// </summary>
    public string[] ContentApproval { get; init; } = [];

    /// <summary>
    /// Grants to positions the division starts with (M2, note
    /// 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.2): "the coordinator and the assistant of
    /// ATC manage the ATC positions of events". Read <b>once</b>, at the first start that finds them;
    /// after that <c>hub_user_grants</c> is the truth and they are changed from the permissions screen.
    /// </summary>
    public PositionGrantSeed[] PositionGrants { get; init; } = [];

    /// <summary>
    /// Bootstrap only: read once, when no super administrator exists yet. After that
    /// <c>hub_users.is_superadmin</c> is the truth and this list is ignored (plan section 4.1).
    /// </summary>
    public int[] SuperAdmins { get; init; } = [];

    /// <summary>
    /// How far the authority of a FIR team reaches: with <c>own</c>, a permission a grant gives to the team of a FIR is held on the
    /// rows of that FIR alone; with <c>all</c>, on its department, as any other grant. The staff of the departments is never held
    /// to a FIR (plan section 4.1; M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir).
    /// </summary>
    public FirStaffScope FirStaffScope { get; init; } = FirStaffScope.All;

    /// <summary>
    /// Where the ATC sessions of the network are read from, when the division has an archive of them (note
    /// 2026-09-14-dati-condivisi-con-vipi §3.4). Absent — the default, and a fork's — means none: the controllers a pilot
    /// contacted are «not available» and everything else works the same.
    /// </summary>
    public IvaoHub.Core.Atc.AtcDataOptions? AtcData { get; init; }

    // There is deliberately no ResolveName here. Falling back from one language to another is a
    // rule the hub already has, in Localized<T>.Resolve, and a second copy of it on this type had
    // no caller and would have been the copy that drifted.
}

/// <summary>One grant to a position, as <c>division.json</c> writes it.</summary>
public sealed class PositionGrantSeed
{
    /// <summary>The department of the position; absent for a grant to the team of a FIR.</summary>
    public Department? Department { get; init; }

    /// <summary>
    /// True for a grant to the team of a FIR instead of a department's position (M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir):
    /// whoever holds a FIR position at one of <see cref="Levels"/> — the chief is a coordinator, the assistant chief an assistant —
    /// holds it, on the FIR of that position when <see cref="DivisionOptions.FirStaffScope"/> is <c>own</c>. It names no FIR, and
    /// it is only taken for a permission of an area whose rows say their FIR.
    /// </summary>
    public bool FirTeam { get; init; }

    /// <summary>The levels of it that hold the grant.</summary>
    public StaffLevel[] Levels { get; init; } = [];

    /// <summary>The permission, for example <c>Events.ManageAtcPositions</c>.</summary>
    public string Permission { get; init; } = string.Empty;

    /// <summary>The department the permission is held on; absent means every department.</summary>
    public Department? Scope { get; init; }

    /// <summary>True takes the permission away from the position instead of giving it.</summary>
    public bool Deny { get; init; }
}

/// <summary>One module, as <c>division.json → modules.{key}</c> writes it.</summary>
public sealed class ModuleSettings
{
    /// <summary>
    /// Only an optional module can be switched off, and only by saying false: a module the division
    /// does not name, or names without this, is on.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The department every row of the module is always in the care of — events are always the
    /// Events department's, whoever else collaborates (Carmine, 13 September 2026). Configuration and
    /// not code: the module never names a department, and a division organised otherwise changes a
    /// line. Absent for a module whose rows have no base department.
    /// </summary>
    public Department? BaseDepartment { get; init; }
}
