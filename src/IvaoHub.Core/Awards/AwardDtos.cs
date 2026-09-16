using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Awards;

/// <summary>An award as the catalogue lists it, and as the select of an assignment offers it.</summary>
public sealed record AwardListDto(
    long Id,
    Department OwnerDepartment,
    Localized<string> Name,
    long? ImageMediaId,
    bool IsActive,
    DateTime UpdatedAt);

/// <summary>An award as the form loads it, with the version to write back.</summary>
public sealed record AwardDetailDto(
    long Id,
    Department OwnerDepartment,
    Localized<string> Name,
    Localized<string>? Description,
    Localized<string>? Criteria,
    long? ImageMediaId,
    bool IsActive,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>What a client may set on an award. The audit columns are the interceptor's.</summary>
public sealed record AwardWriteDto(
    Department OwnerDepartment,
    Localized<string> Name,
    Localized<string>? Description,
    Localized<string>? Criteria,
    long? ImageMediaId,
    bool IsActive,
    DateTime RowVersion);

/// <summary>
/// One line of the register. <see cref="AwardName"/> is not a column of the assignment: the list reads
/// the names of a whole page at once.
/// </summary>
public sealed record AwardAssignmentListDto(
    long Id,
    long AwardId,
    Localized<string>? AwardName,
    int Vid,
    string Reason,
    long? SignalId,
    DateTime CreatedAt,
    int CreatedBy);

/// <summary>An assignment as the form loads it.</summary>
public sealed record AwardAssignmentDetailDto(
    long Id,
    long AwardId,
    int Vid,
    string Reason,
    long? SignalId,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set on an assignment. <see cref="SignalId"/> is read on creation only — it is the
/// line of the queue being answered — and an update leaves the one the row has.
/// </summary>
public sealed record AwardAssignmentWriteDto(
    long AwardId,
    int Vid,
    string Reason,
    long? SignalId,
    DateTime RowVersion);

/// <summary>
/// One line of the queue: who, why, from which row of which module, and the award that row proposes
/// with its name, read for the whole page at once.
/// </summary>
public sealed record AwardSignalListDto(
    long Id,
    string SourceModule,
    string SourceId,
    int Vid,
    string Reason,
    long? AwardId,
    Localized<string>? AwardName,
    AwardSignalStatus Status,
    DateTime CreatedAt,
    DateTime? HandledAt,
    int? HandledBy);

/// <summary>A signal in full, as the assignment form reads it to fill itself.</summary>
public sealed record AwardSignalDetailDto(
    long Id,
    string SourceModule,
    string SourceId,
    int Vid,
    string Reason,
    long? AwardId,
    AwardSignalStatus Status,
    DateTime CreatedAt,
    DateTime? HandledAt,
    int? HandledBy);

/// <summary>
/// The only thing a person writes on a signal: dismissing it, or putting a dismissed one back in the
/// queue. <c>Handled</c> is never written by hand — an assignment writes it.
/// </summary>
public sealed record AwardSignalWriteDto(AwardSignalStatus Status);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class AwardMapper
{
    public partial AwardListDto ToList(Award award);

    public partial AwardDetailDto ToDetail(Award award);

    public partial void Apply(AwardWriteDto payload, Award award);

    // Not a column of the assignment: the list fills it for a whole page.
    [MapValue(nameof(AwardAssignmentListDto.AwardName), null)]
    public partial AwardAssignmentListDto ToList(AwardAssignment assignment);

    public partial AwardAssignmentDetailDto ToDetail(AwardAssignment assignment);

    public partial void Apply(AwardAssignmentWriteDto payload, AwardAssignment assignment);

    // Not a column of the signal either.
    [MapValue(nameof(AwardSignalListDto.AwardName), null)]
    public partial AwardSignalListDto ToList(AwardSignal signal);

    public partial AwardSignalDetailDto ToDetail(AwardSignal signal);
}
