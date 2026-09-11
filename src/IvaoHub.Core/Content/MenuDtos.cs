using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>One entry of the site menu, as the back office list shows it.</summary>
public sealed record MenuItemListDto(
    long Id,
    MenuScope Scope,
    long? ParentId,
    int Sort,
    Localized<string> Label,
    string Path,
    string? Icon,
    Visibility Visibility,
    bool IsActive,
    DateTime UpdatedAt);

/// <summary>The same, as the form loads it, with the version to write back.</summary>
public sealed record MenuItemDetailDto(
    long Id,
    MenuScope Scope,
    long? ParentId,
    int Sort,
    Localized<string> Label,
    string Path,
    string? Icon,
    Visibility Visibility,
    bool IsActive,
    Department OwnerDepartment,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set. ⚠️ There is no <c>OwnerDepartment</c> here, and its absence is the rule:
/// the menu belongs to the web team, so the department is a constant of the entity rather than a
/// field a payload can move. Nothing has to refuse the move because there is nothing to send.
/// </summary>
public sealed record MenuItemWriteDto(
    MenuScope Scope,
    long? ParentId,
    int Sort,
    Localized<string> Label,
    string Path,
    string? Icon,
    Visibility Visibility,
    bool IsActive,
    DateTime RowVersion);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class MenuItemMapper
{
    public partial MenuItemListDto ToList(MenuItem item);

    public partial MenuItemDetailDto ToDetail(MenuItem item);

    public partial void Apply(MenuItemWriteDto payload, MenuItem item);
}
