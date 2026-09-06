using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>A word of the vocabulary as the back office list shows it.</summary>
public sealed record CategoryListDto(
    long Id,
    ContentKind Kind,
    Department OwnerDepartment,
    string Key,
    Localized<string> Label,
    int Sort,
    bool IsActive,
    DateTime UpdatedAt);

/// <summary>The same, as the form loads it, with the version to write back.</summary>
public sealed record CategoryDetailDto(
    long Id,
    ContentKind Kind,
    Department OwnerDepartment,
    string Key,
    Localized<string> Label,
    int Sort,
    bool IsActive,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set. The key is here and is writable, which is worth saying out loud: a key
/// that could never be corrected would mean a typo lives for ever, and renaming one is a decision
/// somebody takes knowing the rows already filed under the old one keep it.
/// </summary>
public sealed record CategoryWriteDto(
    ContentKind Kind,
    Department OwnerDepartment,
    string Key,
    Localized<string> Label,
    int Sort,
    bool IsActive,
    DateTime RowVersion);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class CategoryMapper
{
    public partial CategoryListDto ToList(ContentCategory category);

    public partial CategoryDetailDto ToDetail(ContentCategory category);

    public partial void Apply(CategoryWriteDto payload, ContentCategory category);
}
