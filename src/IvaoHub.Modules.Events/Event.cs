using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Events;

/// <summary>Who organises an event (design M4 §1.2): the division itself, the network with the division, or another division.</summary>
public enum EventOrganizer
{
    Division,

    /// <summary>An event of headquarters in collaboration with the division, written by hand with its own page (§17.1 n.2).</summary>
    Network,

    OtherDivision,
}

/// <summary>
/// An event of the division (design M4 §1.2), <c>evt_events</c>: an RFE, an RFO, an MSE, an online day, a free event, an event of
/// headquarters in collaboration with the division.
/// <para>Born whole in the skeleton (E2), with every column the later phases use, so that none of them migrates it again: the text
/// and the dates (E3a, E3b), the switches the kind presets and the slots, the roster and the event in person read (E5, E11, E16),
/// how far the jobs got (E11b, E13a), and the three limits of whoever books and does not fly, which an event may change for itself
/// (E13b; empty is the setting).</para>
/// <para>Its kind is a key of the calendar's vocabulary, never a list of the code: the behaviour is in the five switches, which
/// the kind only presets (note 2026-09-29-i-tipi-di-evento). Its state is not stored: it is read off the dates and
/// <see cref="Status"/>, as a tour's is (§2.1).</para>
/// <para>In the care of the base department of the module, always, and of whoever organises it with that department
/// (<see cref="OwnerDepartmentMask"/>); the departments that collaborate on a part of it hold their permissions on the base
/// department instead (§1.1). A permission can be granted on one event alone, <c>events:event:{id}</c>.</para>
/// <para>It projects itself (E3b, §8.1): a calendar entry and a line of the search while it is seen, and its files — for good while
/// it is a draft, until a week after its end once it is published.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.Area)]
public sealed class Event : IOwnedByDepartment, IVisible, IPublishable, IAuditable, IHasResourceScope, IProjectable
{
    /// <summary>The longest address of an event, <c>/events/{slug}</c>: the same as a tour's.</summary>
    public const int MaxSlugLength = 100;

    /// <summary>What a banner and the pictures of the description stay in the library for, after the end (§2.4, c2).</summary>
    public static readonly TimeSpan MediaKeptAfterEnd = TimeSpan.FromDays(7);

    /// <summary>
    /// The kind of an event's line in the search: the module's key — «event» is a word of the calendar, and the kinds of the
    /// calendar are the division's, never the code's (note 2026-09-29-i-tipi-di-evento).
    /// </summary>
    public const string SearchKind = EventsModule.ModuleKey;

    /// <summary>The body an event starts with: no sections.</summary>
    public const string EmptyBody = """{"schemaVersion":1,"sections":[]}""";

    public long Id { get; set; }

    /// <summary>The address, <c>/events/{slug}</c>; unique among events.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>A key of the kinds of the calendar (<c>cms_calendar_kinds</c>): the division's word for this event.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Public slots, prepared by the staff and taken by a pilot (§1.5).</summary>
    public bool PublicSlots { get; set; }

    /// <summary>Private slots, generated from the capacity of each airport (§3.2).</summary>
    public bool PrivateSlots { get; set; }

    /// <summary>Whether the event has an ATC roster (§4): not every event has one.</summary>
    public bool HasRoster { get; set; }

    /// <summary>The event is about every airport and position of the division, as an online day is, rather than about its own airports.</summary>
    public bool WholeDivision { get; set; }

    /// <summary>An event people come to in person (§4-bis): registration with questions, and activities.</summary>
    public bool InPerson { get; set; }

    public EventOrganizer Organizer { get; set; }

    /// <summary>The page of whoever organises it, for an event of the network or of another division.</summary>
    public string? ExternalUrl { get; set; }

    public Localized<string> Title { get; set; } = Localized<string>.Empty;

    public Localized<string> Summary { get; set; } = Localized<string>.Empty;

    /// <summary>The description: a <c>BlockDocument</c>, edited and rendered as every rich text of the hub is.</summary>
    public string BodyJson { get; set; } = EmptyBody;

    /// <summary>The banner of the event's page, from the media library.</summary>
    public long? BannerMediaId { get; set; }

    /// <summary>When the published event can be seen; empty, from its publication (§2.2).</summary>
    public DateTime? VisibleFromUtc { get; set; }

    /// <summary>When booking opens; it closes slot by slot, at each off block time (§3.3). Empty for an event without slots.</summary>
    public DateTime? BookingOpensAtUtc { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }

    /// <summary>The length of a shift of the roster; empty, the setting's.</summary>
    public int? ShiftMinutes { get; set; }

    /// <summary>Where an event in person takes place, in every language of the division.</summary>
    public Localized<string>? Venue { get; set; }

    /// <summary>Draft or published. Changed by the actions, never by the form.</summary>
    public PublishStatus Status { get; set; } = PublishStatus.Draft;

    /// <summary>When it was last published.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Who reads it once it is published and visible: everybody, or the members (§1.2).</summary>
    public Visibility Visibility { get; set; } = Visibility.Public;

    public DateTime? CancelledAt { get; set; }

    /// <summary>The VID of whoever cancelled it.</summary>
    public int? CancelledBy { get; set; }

    /// <summary>Why, in every language of the division: shown on the page of the event until its end (§2.3).</summary>
    public Localized<string>? CancellationNote { get; set; }

    /// <summary>When the roster was proposed, once (§4.3): the job reads it rather than the hour it runs at.</summary>
    public DateTime? RosterProposedAt { get; set; }

    /// <summary>When the checks after the event were done (§5.1): the same rule.</summary>
    public DateTime? AfterDoneAt { get; set; }

    /// <summary>For whoever books and does not fly: the hours of a window of the event (§3.7); empty, the setting's.</summary>
    public int? RestrictedWindowHours { get; set; }

    /// <summary>For whoever books and does not fly: the slots in each window; empty, the setting's.</summary>
    public int? RestrictedMaxPerWindow { get; set; }

    /// <summary>For whoever books and does not fly: the slots in the whole event; empty, the setting's.</summary>
    public int? RestrictedMaxPerEvent { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>What a permission granted on this event alone names.</summary>
    public string ResourceScope => ScopeOf(Id);

    /// <summary>The scope of an event: the row's own, and the one its rows of staff answer with.</summary>
    public static string ScopeOf(long eventId) => $"{EventsModule.ModuleKey}:event:{eventId}";

    public string SourceModule => EventsModule.ModuleKey;

    public string SourceId => $"event:{Id}";

    /// <summary>
    /// One calendar entry, a line of the search and the files it shows (design M4 §8.1, §2.4). The entry — of the event's kind, for
    /// whoever the event is for — and the line — only for an event everybody reads — exist while the event is seen
    /// (<see cref="EventState.IsSeen"/>): never for a draft, nor for one published and not seen yet, nor for one ended. A cancelled
    /// event has no entry, and keeps its line until its end: its page stays, with the note (§2.3).
    /// <para>The files of a published event are declared until a week after its end: the core's job deletes them then, if nothing
    /// else shows them. A draft keeps its files for as long as it is a draft, whatever its dates say (the interceptor keeps them for
    /// a row not published): one written with a window already past, by mistake or to be moved later, does not lose its banner
    /// before anybody publishes it (review of #221, point 5). The time decides the rest, and nobody writes the event when it is seen
    /// or when it ends: <see cref="EventReleaseJob"/> projects it again at both.</para>
    /// </summary>
    public ProjectionSnapshot? Project(ProjectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var media = MediaUses(context);

        if (!EventState.IsSeen(this, context.Clock.UtcNow))
        {
            return media.Count == 0 ? null : new ProjectionSnapshot(null, [], [], media);
        }

        var url = $"/events/{Slug}";
        var text = new Localized<string>(context.Locales.ToDictionary(
            locale => locale,
            locale => string.Join(
                ' ',
                new[] { Summary.Get(locale), context.Blocks.ExtractText(JsonNode.Parse(BodyJson), locale) }
                    .Where(part => !string.IsNullOrWhiteSpace(part)))));

        var search = Visibility == Visibility.Public
            ? new SearchProjection(SearchKind, url, OwnerDepartment, Visibility.Public, Title, text)
            : null;
        IReadOnlyList<CalendarProjection> calendar = CancelledAt is null
            ? [new CalendarProjection(Kind, StartsAtUtc, EndsAtUtc, AllDay: false, OwnerDepartment, Visibility, url, Title, Summary)]
            : [];

        return search is null && calendar.Count == 0 && media.Count == 0
            ? null
            : new ProjectionSnapshot(search, calendar, [], media);
    }

    /// <summary>The banner and the pictures of the description: until a week after the end once published, for good while a draft.</summary>
    private List<MediaUseProjection> MediaUses(ProjectionContext context)
    {
        DateTime? until = Status == PublishStatus.Published ? EndsAtUtc + MediaKeptAfterEnd : null;

        return
        [
            .. new[] { BannerMediaId }
                .OfType<long>()
                .Concat(context.Blocks.MediaReferences(JsonNode.Parse(BodyJson)).Select(reference => reference.Id))
                .Distinct()
                .Select(id => new MediaUseProjection(id, until)),
        ];
    }
}
