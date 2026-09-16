using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>The seven shapes a tour can have (design M2 §2). Stored by name.</summary>
public enum TourKind
{
    Sequential,
    Free,
    Hub,
    SequentialChosenStart,
    Distance,
    Open,
    Container,
}

/// <summary>Whether a pilot may fly the next leg before the previous one is validated (design M2 §1.2).</summary>
public enum TourProgression
{
    FlyAhead,
    WaitForValidation,
}

/// <summary>How the rotations of a hub are flown (design M2 §2.3, answer 10).</summary>
public enum HubRotationOrder
{
    Fixed,
    Free,
}

/// <summary>What an <see cref="TourKind.Open"/> tour accumulates (design M2 §2.6.1). Written by T7.</summary>
public enum OpenGoal
{
    Distance,
    FlightCount,
    DistinctAirports,
    DistinctCountries,
    CollectList,
    CollectRegions,
}

/// <summary>
/// The aircraft a tour admits: types and groups of types, nothing else (Carmine, 16 September 2026 — IVAO's
/// «variants» are liveries and engines of one type, so a neo with its ceo is a group). Empty admits all.
/// Written by T7; T6 creates the column and copies it with a template.
/// </summary>
public sealed record AllowedAircraft(IReadOnlyList<string> Types, IReadOnlyList<long> GroupIds)
{
    public static AllowedAircraft All { get; } = new([], []);
}

/// <summary>
/// A tour of the division (design M2 §1.2), and a template of one (§1.10): one table, <c>fo_tours</c>.
/// <para>Its state is not stored: <see cref="Status"/> says only whether somebody marked it ready, and
/// <see cref="TourState"/> reads the rest off the dates (§1.2.1). Ready is <see cref="PublishStatus.Published"/>,
/// so that the rule the interceptor applies to every publishable row — a draft keeps its files and nothing a
/// reader could find — is the tour's too without being written twice.</para>
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class Tour : IOwnedByDepartment, IAuditable, IVisible, IPublishable, IProjectable
{
    /// <summary>What a tour's banner and photo stay in the library for, after it closes (design M2 §1.14).</summary>
    public static readonly TimeSpan MediaKeptAfterClose = TimeSpan.FromDays(31);

    /// <summary>The body a tour starts with: no sections.</summary>
    public const string EmptyBriefing = """{"schemaVersion":1,"sections":[]}""";

    private static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web);

    public long Id { get; set; }

    /// <summary>The address, <c>/tours/{slug}</c>; unique among tours, none on a template.</summary>
    public string? Slug { get; set; }

    public bool IsTemplate { get; set; }

    public TourKind Kind { get; set; }

    /// <summary>The <see cref="TourKind.Container"/> this tour is a subtour of. Written by T7.</summary>
    public long? ParentTourId { get; set; }

    /// <summary>How many subtours complete a container. Written by T7.</summary>
    public int? RequiredSubtours { get; set; }

    /// <summary>The distance that completes a <see cref="TourKind.Distance"/> tour. Written by T7.</summary>
    public int? RequiredNm { get; set; }

    public OpenGoal? OpenGoal { get; set; }

    /// <summary>The parameters of the goal, as the goal's own schema says. Written by T7.</summary>
    public string? OpenGoalJson { get; set; }

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    public Localized<string> Summary { get; set; } = Localized<string>.Empty;

    /// <summary>A <c>BlockDocument</c>, edited and rendered as every rich text of the hub is.</summary>
    public string BriefingJson { get; set; } = EmptyBriefing;

    /// <summary>The photo behind the card of the tour, from the media library.</summary>
    public long? CoverMediaId { get; set; }

    /// <summary>The banner of the tour's page, from the media library.</summary>
    public long? BannerMediaId { get; set; }

    /// <summary>Draft, or ready (<see cref="PublishStatus.Published"/>). Changed by the actions, never by the form.</summary>
    public PublishStatus Status { get; set; } = PublishStatus.Draft;

    /// <summary>When it was last marked ready.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Hidden from everybody outside the staff, for good (design M2 §1.2.2).</summary>
    public bool IsHidden { get; set; }

    /// <summary>A ready tour the public sees before its release, as a preview (answer 1).</summary>
    public bool ShowPreview { get; set; }

    /// <summary>Null on a template only.</summary>
    public DateTime? ReleaseAt { get; set; }

    /// <summary>Null on a template only.</summary>
    public DateTime? CloseAt { get; set; }

    /// <summary>X: days to send a report after the flight, and the window searched in the tracker.</summary>
    public int ReportWindowDays { get; set; }

    public TourProgression Progression { get; set; }

    /// <summary>Only on a <see cref="TourKind.Hub"/> tour.</summary>
    public HubRotationOrder? HubRotationOrder { get; set; }

    /// <summary>SID, STAR and approach required in the report (answer 4).</summary>
    public bool RequiresProcedures { get; set; }

    /// <summary>Legs a pilot may report per UTC day on this tour; required when the division has no limit (§3.7).</summary>
    public int? DailyLegLimit { get; set; }

    /// <summary>The aircraft admitted, as a JSON object: the column.</summary>
    public string AllowedAircraftJson { get; set; } = JsonSerializer.Serialize(AllowedAircraft.All, ColumnJson);

    /// <summary><see cref="AllowedAircraftJson"/> as the record it is.</summary>
    public AllowedAircraft AllowedAircraft
    {
        get => JsonSerializer.Deserialize<AllowedAircraft>(AllowedAircraftJson, ColumnJson) ?? AllowedAircraft.All;
        set => AllowedAircraftJson = JsonSerializer.Serialize(value ?? AllowedAircraft.All, ColumnJson);
    }

    /// <summary>The lowest pilot rating that may report a flight; none when empty.</summary>
    public int? MinPilotRating { get; set; }

    /// <summary>The type the estimated times are computed for; none, no estimates (§1.5).</summary>
    public string? ReferenceAircraftIcao { get; set; }

    /// <summary>One award for a tour, never on a subtour (Carmine, 15 September 2026).</summary>
    public long? AwardId { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>
    /// Who may read the row, as the global query filter reads it: everybody once it is ready, not hidden and not a
    /// template; the staff otherwise. Coarse on purpose — a column cannot follow the clock — so a public read also
    /// asks <see cref="TourState.IsPublic"/>, which adds the release (T10). Computed, so no write can forget it.
    /// </summary>
    public Visibility Visibility
    {
        get => Status == PublishStatus.Published && !IsHidden && !IsTemplate ? Visibility.Public : Visibility.Staff;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    public string SourceModule => FlightOpsModule.ModuleKey;

    public string SourceId => $"tour:{Id}";

    /// <summary>
    /// Search, two calendar entries — release and close (answer 20) — and the files it shows (design M2 §9, §1.14).
    /// A hidden tour or a template projects only its files; a draft too, by the interceptor's rule. A ready tour the
    /// public cannot see yet projects for the staff, and <see cref="TourReleaseJob"/> projects it again at its release.
    /// </summary>
    public ProjectionSnapshot? Project(ProjectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var media = MediaUses(context);

        if (IsHidden || IsTemplate || ReleaseAt is not { } release || CloseAt is not { } close || Slug is null)
        {
            return media.Count == 0 ? null : new ProjectionSnapshot(null, [], [], media);
        }

        var visibility = TourState.IsPublic(this, context.Clock.UtcNow) ? Visibility.Public : Visibility.Staff;
        var url = $"/tours/{Slug}";
        var text = new Localized<string>(context.Locales.ToDictionary(
            locale => locale,
            locale => string.Join(
                ' ',
                new[] { Summary.Get(locale), context.Blocks.ExtractText(JsonNode.Parse(BriefingJson), locale) }
                    .Where(part => !string.IsNullOrWhiteSpace(part)))));

        return new ProjectionSnapshot(
            new SearchProjection(Kind: "tour", url, OwnerDepartment, visibility, Title, text),
            [
                new CalendarProjection("tour", release, EndsAtUtc: null, AllDay: false, OwnerDepartment, visibility, url, Title, Summary),
                new CalendarProjection("tour", close, EndsAtUtc: null, AllDay: false, OwnerDepartment, visibility, url, Title, Summary),
            ],
            [],
            media);
    }

    /// <summary>Cover, banner and the pictures of the briefing, until a month after the close; for good on a template.</summary>
    private List<MediaUseProjection> MediaUses(ProjectionContext context)
    {
        DateTime? until = CloseAt is { } close ? close + MediaKeptAfterClose : null;

        return
        [
            .. new[] { CoverMediaId, BannerMediaId }
                .OfType<long>()
                .Concat(context.Blocks.MediaReferences(JsonNode.Parse(BriefingJson)).Select(reference => reference.Id))
                .Distinct()
                .Select(id => new MediaUseProjection(id, until)),
        ];
    }
}
