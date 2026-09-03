using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>The module a projection came from. The editorial core is simply "core".</summary>
public static class ProjectionSource
{
    public const string Core = "core";
}

/// <summary>
/// What a row wants to appear as in the search index, in the calendar and in the award queue. A
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
public sealed record ProjectionSnapshot(
    SearchProjection? Search,
    CalendarProjection? Calendar,
    IReadOnlyList<AwardSignalProjection> AwardSignals)
{
    public static ProjectionSnapshot ForSearch(SearchProjection search) => new(search, null, []);
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
public sealed record AwardSignalProjection(int Vid, string Reason);
