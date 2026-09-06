using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>
/// One calendar entry as a list shows it.
/// <para><see cref="IsProjection"/> travels because the screen has to draw it and must not work it
/// out for itself: the engine refuses a write on the same answer, and a list that decided
/// separately what a projection is would be a badge that disagrees with a 403.</para>
/// </summary>
public sealed record CalendarListDto(
    long Id,
    Department OwnerDepartment,
    Visibility Visibility,
    string Kind,
    Localized<string> Title,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool AllDay,
    string Url,
    bool IsProjection,
    DateTime UpdatedAt);

/// <summary>
/// The same entry as the form loads it, with the audit trail.
/// <para><see cref="SourceModule"/> travels so that a read only screen can <b>name</b> the module
/// the row belongs to instead of only refusing: "this entry comes from Events" is an explanation,
/// a disabled save button is a ticket.</para>
/// </summary>
public sealed record CalendarDetailDto(
    long Id,
    Department OwnerDepartment,
    Visibility Visibility,
    string Kind,
    Localized<string> Title,
    Localized<string>? Description,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool AllDay,
    string Url,
    bool IsProjection,
    string SourceModule,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy);

/// <summary>
/// What a client may set.
/// <para>Three things are absent on purpose. <c>sourceModule</c> and <c>sourceId</c> say who owns
/// the row, and a payload that could set them would be a payload that can claim a row belongs to a
/// module. The audit columns are the interceptor's.</para>
/// <para>⚠️ There is no <c>rowVersion</c> either, and that is a fact of the table rather than an
/// omission here: <c>cms_calendar_entries</c> has no concurrency token, so two members editing one
/// entry at the same time end with the second save winning. The model of M0 is not touched in this
/// phase; if the calendar ever needs the check, it is an additive column and a decision.</para>
/// </summary>
public sealed record CalendarWriteDto(
    Department OwnerDepartment,
    Visibility Visibility,
    string Kind,
    Localized<string> Title,
    Localized<string>? Description,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool AllDay,
    string? Url);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class CalendarMapper
{
    public partial CalendarListDto ToList(CalendarEntry entry);

    public partial CalendarDetailDto ToDetail(CalendarEntry entry);

    public partial void Apply(CalendarWriteDto payload, CalendarEntry entry);

    /// <summary>An address nobody typed is an empty column, because the column is not nullable.</summary>
    private static string ReadUrl(string? url) => url ?? string.Empty;
}
