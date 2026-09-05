using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>
/// A file as a list and a picker show it. The address is part of the row because it is built the
/// same way everywhere and nobody should assemble it twice.
/// </summary>
public sealed record MediaListDto(
    long Id,
    Department OwnerDepartment,
    Visibility Visibility,
    string FileName,
    string ContentType,
    long ByteSize,
    int? Width,
    int? Height,
    Localized<string> Alt,
    string? Category,
    string Url,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>A file as the metadata form shows it, with the version to write back.</summary>
public sealed record MediaDetailDto(
    long Id,
    Department OwnerDepartment,
    Visibility Visibility,
    string FileName,
    string ContentType,
    long ByteSize,
    int? Width,
    int? Height,
    Localized<string> Alt,
    Localized<string>? Title,
    string? Category,
    string Url,
    bool HasFile,
    DateTime? DeletedAt,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set. Everything about the file itself — its name on disk, its type, its size,
/// its pixels — is what the upload measured and is not a payload: a row that could be told it is a
/// PNG would be a row that can lie about what is on the disk.
/// </summary>
public sealed record MediaWriteDto(
    Department OwnerDepartment,
    Visibility Visibility,
    Localized<string> Alt,
    Localized<string>? Title,
    string? Category,
    DateTime RowVersion);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class MediaMapper
{
    public partial MediaListDto ToList(MediaAsset media);

    public partial MediaDetailDto ToDetail(MediaAsset media);

    public partial void Apply(MediaWriteDto payload, MediaAsset media);
}
