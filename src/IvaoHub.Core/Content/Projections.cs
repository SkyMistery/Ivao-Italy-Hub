using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;

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
/// <param name="Locales">The languages of the division.</param>
/// <param name="DefaultLocale">The language a row falls back to.</param>
/// <param name="Blocks">The walker that turns a body of blocks into text.</param>
/// <param name="Clock">
/// What time it is, for a row whose projection depends on it: a tour is the staff's until its release
/// and everybody's after (M2, T6). The clock of the host, so a test that moves time moves this too.
/// </param>
public sealed record ProjectionContext(
    IReadOnlyList<string> Locales,
    string DefaultLocale,
    BlockDocumentWalker Blocks,
    IClock Clock);

/// <summary>Everything a row projects, at once. Missing pieces are simply null or empty.</summary>
/// <param name="Search">The line of the row in the search index, one per language once written.</param>
/// <param name="Calendar">
/// Every entry the row puts in the calendar, in a stable order: a tour has two, its release and its
/// close, a link has none. The position in the list is what tells one entry of a row from the next (M2, T4).
/// </param>
/// <param name="AwardSignals">The members the row points out for an award; a human decides.</param>
/// <param name="MediaUses">
/// The files of the library the row shows, and until when it needs each (M2, T4, note
/// 2026-09-15-file-con-scadenza). Unlike the other three, a row that is not published still declares
/// them: a tour being prepared needs its banner as much as a tour already open.
/// </param>
/// <param name="ThreadOpenings">
/// The contact threads the row opens: a disputed report opens one with the department (M2, T14). Once only — a thread
/// has answers that are nobody's row, so it is never rewritten or removed by the row that opened it.
/// </param>
public sealed record ProjectionSnapshot(
    SearchProjection? Search,
    IReadOnlyList<CalendarProjection> Calendar,
    IReadOnlyList<AwardSignalProjection> AwardSignals,
    IReadOnlyList<MediaUseProjection> MediaUses,
    IReadOnlyList<ThreadOpeningProjection>? ThreadOpenings = null)
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

/// <summary>
/// "Open this conversation with the department, if it is not open yet" (note 2026-09-15-contatti-con-risposte §3.3). The
/// row and the thread live in two contexts; opened as a projection, they are written in one transaction.
/// <para>The key of "once only" is the row itself and the kind: the writer creates the thread when that row has not
/// opened one of that kind yet, and never touches it afterwards. What the thread decides — a dispute upheld or turned
/// down — stays on the row of the module; the thread is the conversation.</para>
/// </summary>
/// <param name="Kind">A kind of <see cref="ContactKinds"/>, or one of the module's.</param>
/// <param name="Department">Whose queue it lands in.</param>
/// <param name="Subject">One line, in the sender's words or the module's.</param>
/// <param name="Body">What the sender wrote.</param>
/// <param name="SenderVid">The member who opens it: the thread is theirs.</param>
/// <param name="ParticipantVids">Who else reads and answers: the validator of a disputed report.</param>
/// <param name="References">The objects it is about, with their labels taken now.</param>
public sealed record ThreadOpeningProjection(
    string Kind,
    Department Department,
    string Subject,
    string Body,
    int SenderVid,
    IReadOnlyList<int> ParticipantVids,
    IReadOnlyList<ThreadReferenceProjection> References);

/// <summary>One object a thread cites: <c>flightops</c> + <c>pirep:123</c>, and how it reads.</summary>
public sealed record ThreadReferenceProjection(string SourceModule, string SourceId, Localized<string> Label);
