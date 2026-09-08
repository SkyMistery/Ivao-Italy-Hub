using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Content;

/// <summary>One word of the division's calendar vocabulary, as a list row.</summary>
public sealed record CalendarKindListDto(
    long Id,
    string Key,
    Localized<string> Label,
    string Colour,
    int Sort,
    bool IsActive,
    DateTime UpdatedAt);

/// <summary>The same, as the form loads it, with the version to write back.</summary>
public sealed record CalendarKindDetailDto(
    long Id,
    string Key,
    Localized<string> Label,
    string Colour,
    int Sort,
    bool IsActive,
    DateTime CreatedAt,
    int CreatedBy,
    DateTime UpdatedAt,
    int UpdatedBy,
    DateTime RowVersion);

/// <summary>
/// What a client may set. The key is writable for the same reason a category's is: a key that could
/// never be corrected would mean a typo lives for ever, and renaming one is a decision somebody
/// takes knowing the entries already written with the old one keep it.
/// </summary>
public sealed record CalendarKindWriteDto(
    string Key,
    Localized<string> Label,
    string Colour,
    int Sort,
    bool IsActive,
    DateTime RowVersion);

/// <summary>Entity to payload and back. Generated, like every other mapping of the hub.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class CalendarKindMapper
{
    public partial CalendarKindListDto ToList(CalendarKind kind);

    public partial CalendarKindDetailDto ToDetail(CalendarKind kind);

    public partial void Apply(CalendarKindWriteDto payload, CalendarKind kind);
}
