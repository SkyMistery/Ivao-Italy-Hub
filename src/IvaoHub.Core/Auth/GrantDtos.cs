using IvaoHub.Core.Division;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Auth;

/// <summary>
/// A grant as the list shows it. There is no department to narrow this list by: a grant is a
/// global resource, read and written behind <c>Permissions.Manage</c> and nothing else
/// (design M0 section 3.9).
/// </summary>
public sealed record GrantListDto(
    long Id,
    int Vid,
    string Value,
    Department? Department,
    GrantEffect Effect,
    DateTime? ExpiresAt,
    DateTime? SuspendedAt,
    string? Reason,
    DateTime UpdatedAt);

/// <summary>A grant as the form loads it, with the audit trail and the version to write back.</summary>
public sealed record GrantDetailDto(
    long Id,
    int Vid,
    GrantKind Kind,
    string Value,
    Department? Department,
    GrantEffect Effect,
    DateTime? ExpiresAt,
    DateTime? SuspendedAt,
    string? Reason,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What an administrator may set.
/// <para><c>SuspendedAt</c> is deliberately absent: it is written by the login when IVAO stops
/// listing the member as staff, and a grant is suspended rather than deleted so that it comes back
/// on its own if the position does. Letting a form set it would make the two meanings of "this
/// grant is asleep" indistinguishable.</para>
/// </summary>
public sealed record GrantWriteDto(
    int Vid,
    GrantKind Kind,
    string Value,
    Department? Department,
    GrantEffect Effect,
    DateTime? ExpiresAt,
    string? Reason,
    DateTime RowVersion);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class GrantMapper
{
    public partial GrantListDto ToList(UserGrant grant);

    public partial GrantDetailDto ToDetail(UserGrant grant);

    public partial void Apply(GrantWriteDto payload, UserGrant grant);
}
