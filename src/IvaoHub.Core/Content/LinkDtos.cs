using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>
/// A link as a list shows it. Translated fields travel whole — the browser knows which language it
/// is drawing, the server does not — so there is no "give me the Italian list" endpoint.
/// </summary>
public sealed record LinkListDto(
    long Id,
    Department OwnerDepartment,
    Visibility Visibility,
    Localized<string> Title,
    string Url,
    string? Category,
    int Sort,
    bool IsActive,
    DateTime UpdatedAt);

/// <summary>A link as the form shows it, with the audit trail and the version to write back.</summary>
public sealed record LinkDetailDto(
    long Id,
    Department OwnerDepartment,
    Visibility Visibility,
    Localized<string> Title,
    string Url,
    Localized<string>? Description,
    string? Category,
    int Sort,
    bool IsActive,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set. The audit columns are absent on purpose: the interceptor fills them, and
/// a payload that could carry them would be a payload that could lie about them.
/// <para><see cref="RowVersion"/> is the version the form was loaded with. Sending back a stale one
/// is how the server finds out that somebody else saved first, and answers 409.</para>
/// </summary>
public sealed record LinkWriteDto(
    Department OwnerDepartment,
    Visibility Visibility,
    Localized<string> Title,
    string Url,
    Localized<string>? Description,
    string? Category,
    int Sort,
    bool IsActive,
    DateTime RowVersion);

/// <summary>
/// Entity to payload and back. Generated: a mapping written by hand is a place where a new column
/// can be forgotten.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class LinkMapper
{
    public partial LinkListDto ToList(Link link);

    public partial LinkDetailDto ToDetail(Link link);

    public partial void Apply(LinkWriteDto payload, Link link);
}
