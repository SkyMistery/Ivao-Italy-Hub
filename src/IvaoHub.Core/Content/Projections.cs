using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>The module a projection came from. The editorial core is simply "core".</summary>
public static class ProjectionSource
{
    public const string Core = "core";
}

/// <summary>
/// What a row wants to appear as in the search index, in the calendar and in the award queue, and
/// which files of the library it keeps in use. A
/// module never writes into those tables: it describes itself and the interceptor writes, in the
/// same transaction as the row itself (design M0 section 3.6).
/// </summary>
public interface IProjectable
{
    /// <summary><c>core</c> for the editorial core, otherwise the key of the module.</summary>
    string SourceModule { get; }

    /// <summary>Stable identifier of the row, for example <c>link:42</c>.</summary>
    string SourceId { get; }

    /// <summary>What to project; <c>null</c> removes every projection of this row.</summary>
    ProjectionSnapshot? Project(ProjectionContext context);
}

/// <summary>
/// What an entity is allowed to know about the division while it projects itself: its languages,
/// and the walker that turns a body of blocks into text. An entity cannot be injected into, and
/// hardcoding the languages of a division is exactly what a forkable hub must not do.
/// </summary>
public sealed record ProjectionContext(
    IReadOnlyList<string> Locales,
    string DefaultLocale,
    BlockDocumentWalker Blocks);

/// <summary>Everything a row projects, at once. Missing pieces are simply null or empty.</summary>
/// <param name="Search">The line of the row in the search index, one per language once written.</param>
/// <param name="Calendar">
/// Every entry the row puts in the calendar, in a stable order: a tour has one per leg window, a
/// link has none. The position in the list is what tells one entry of a row from the next (M2, T4).
/// </param>
/// <param name="AwardSignals">The members the row points out for an award; a human decides.</param>
/// <param name="MediaUses">
/// The files of the library the row shows, and until when it needs each (M2, T4, note
/// 2026-09-15-file-con-scadenza). Unlike the other three, a row that is not published still declares
/// them: a tour being prepared needs its banner as much as a tour already open.
/// </param>
public sealed record ProjectionSnapshot(
    SearchProjection? Search,
    IReadOnlyList<CalendarProjection> Calendar,
    IReadOnlyList<AwardSignalProjection> AwardSignals,
    IReadOnlyList<MediaUseProjection> MediaUses)
{
    public static ProjectionSnapshot ForSearch(SearchProjection search) => new(search, [], [], []);

    /// <summary>
    /// What is left of a snapshot while its row is not published: only what the row needs to keep
    /// existing, and nothing a reader could find. <c>null</c> when that is nothing at all.
    /// </summary>
    public ProjectionSnapshot? Unpublished() => MediaUses.Count == 0 ? null : new(null, [], [], MediaUses);
}

/// <summary>One searchable row; it becomes one line per language of the division.</summary>
public sealed record SearchProjection(
    string Kind,
    string Url,
    Department OwnerDepartment,
    Visibility Visibility,
    Localized<string> Title,
    Localized<string> Text);

/// <summary>One entry of the single calendar of the division.</summary>
public sealed record CalendarProjection(
    string Kind,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool AllDay,
    Department OwnerDepartment,
    Visibility Visibility,
    string Url,
    Localized<string> Title,
    Localized<string>? Description);

/// <summary>"This member may deserve something." A human decides; the code only points.</summary>
/// <param name="Vid">The member.</param>
/// <param name="Reason">Why, in a sentence the queue shows.</param>
/// <param name="AwardId">
/// The award the row proposes, when it has one — a tour has exactly one (M2, T4b). Whoever assigns
/// may still choose another.
/// </param>
public sealed record AwardSignalProjection(int Vid, string Reason, long? AwardId = null);

/// <summary>
/// "This row shows this file until then." <c>null</c> means for as long as the row says so: a use
/// without an end, which keeps the file for good. A file whose uses have all ended is what the
/// media expiry job deletes, and only that (note 2026-09-15-file-con-scadenza §3).
/// </summary>
public sealed record MediaUseProjection(long MediaId, DateTime? UsedUntilUtc);
