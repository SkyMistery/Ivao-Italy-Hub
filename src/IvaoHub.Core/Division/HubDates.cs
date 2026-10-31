using System.Globalization;

namespace IvaoHub.Core.Division;

/// <summary>The order a division writes the day, the month and the year of a date in (<see cref="DivisionOptions.DateFormat"/>).</summary>
public enum DateOrder
{
    /// <summary><c>dd/mm/yyyy</c>, the default.</summary>
    DayMonthYear,

    /// <summary><c>mm/dd/yyyy</c>.</summary>
    MonthDayYear,

    /// <summary><c>yyyy/mm/dd</c>.</summary>
    YearMonthDay,
}

/// <summary>
/// A date as the hub writes one into text — a mail, a digest —, in the order the division chose (note
/// 2026-10-31-il-formato-della-data): <c>31/10/2026</c>, and <c>31/10/2026 14:05</c> with its time, always in twenty four hours
/// (note 2026-10-13-le-ore-in-24). The browser's twin is <c>formatMoment</c> in <c>web/src/shared/i18n/useMoment.ts</c>, which reads
/// the same <c>dateFormat</c> from the bootstrap.
/// <para>⚠️ The order is always handed in, never defaulted: a writer that forgot it would write the default in a division that
/// chose another, and nobody would notice until a mail said 03/11 for the eleventh of March. A caller with the options reads
/// <see cref="DivisionOptions.ResolveDateOrder"/>.</para>
/// <para>A date a program reads — a sitemap, an address, a file name — is not this: it stays ISO, <c>yyyy-MM-dd</c>.</para>
/// </summary>
public static class HubDates
{
    /// <summary>The order of a division that says nothing (note 2026-10-22-il-piano-dopo-la-prova-dello-staff §4.E).</summary>
    public const string DefaultFormat = "dd/mm/yyyy";

    /// <summary>The formats a division may write in <c>dateFormat</c>, and the order each one means.</summary>
    public static readonly IReadOnlyDictionary<string, DateOrder> Formats = new Dictionary<string, DateOrder>(StringComparer.Ordinal)
    {
        [DefaultFormat] = DateOrder.DayMonthYear,
        ["mm/dd/yyyy"] = DateOrder.MonthDayYear,
        ["yyyy/mm/dd"] = DateOrder.YearMonthDay,
    };

    /// <summary>A day: <c>31/10/2026</c>, <c>10/31/2026</c> or <c>2026/10/31</c>.</summary>
    public static string Day(DateOnly day, DateOrder order) =>
        day.ToString(
            order switch
            {
                DateOrder.MonthDayYear => "MM'/'dd'/'yyyy",
                DateOrder.YearMonthDay => "yyyy'/'MM'/'dd",
                _ => "dd'/'MM'/'yyyy",
            },
            CultureInfo.InvariantCulture);

    /// <summary>The day of <paramref name="moment"/>, its time left out.</summary>
    public static string Day(DateTime moment, DateOrder order) => Day(DateOnly.FromDateTime(moment), order);

    /// <summary>A moment, its day and its time: <c>31/10/2026 14:05</c>. The zone is the caller's to say, next to it.</summary>
    public static string Moment(DateTime moment, DateOrder order) =>
        string.Create(CultureInfo.InvariantCulture, $"{Day(moment, order)} {moment:HH:mm}");
}
