using System.Globalization;
using IvaoHub.Core.Data.Crud;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>What the two registers of the staff share: the search box — a VID or a piece of a name — and a page of rows counted in memory.</summary>
internal static class RegisterPages
{
    /// <summary>The longest page a register gives.</summary>
    public const int MaxPageSize = 200;

    public static bool Matches(EventMemberDto person, string term) =>
        person.Vid.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)
        || person.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    public static PagedResult<T> Page<T>(IReadOnlyList<T> rows, int page, int pageSize)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var number = Math.Max(page, 1);

        return new PagedResult<T>([.. rows.Skip((number - 1) * size).Take(size)], number, size, rows.Count);
    }
}
