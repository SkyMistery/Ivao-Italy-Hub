using System.Globalization;
using System.Text.Json;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// One FRA of IVAO — a Facility Rating Assignment: the lowest ATC rating that may connect to a position, on some days of the
/// week between two times, or on one date (M4, E10c, note <c>2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione</c>).
/// A snapshot of the division's, as <c>/v2/fras</c> answers them for its country: 353 for Italy on 30 September 2026, on 202
/// positions, most of them twice — a minimum for the day and one for the night.
/// <para>Only the rows of a position. The ones IVAO keeps for a member — an exception for one person, 39 for Italy that day —
/// are never asked for, so no person is in this table; the staff grant them on IVAO, and the hub leaves them there.</para>
/// </summary>
public sealed class IvaoFra
{
    /// <summary>IVAO's identifier, the key: a position may have several FRAs.</summary>
    public long Id { get; set; }

    /// <summary>
    /// The callsign of the position, as <see cref="IvaoAtcPosition.Callsign"/> spells it — what an FRA of IVAO names by an
    /// identifier of its own, read from the position IVAO expands with it. A plain column, never a foreign key.
    /// </summary>
    public string Callsign { get; set; } = string.Empty;

    /// <summary>IVAO's number of the lowest ATC rating it lets connect (<c>minAtc</c>), as <see cref="Rating.Number"/> spells it.</summary>
    public int MinimumRating { get; set; }

    /// <summary>The days of the week it holds on, one bit each (<see cref="IvaoFraReader.DayBit"/>); none for an FRA of one date.</summary>
    public int Days { get; set; }

    /// <summary>When it starts holding, each of its days, in UTC like every time of IVAO.</summary>
    public TimeOnly StartsAt { get; set; }

    /// <summary>When it stops; at or before <see cref="StartsAt"/> it runs past midnight, and 00:00 to 00:00 is the whole day.</summary>
    public TimeOnly EndsAt { get; set; }

    /// <summary>The one date it holds on, instead of the days of the week: a position closed, or opened, for an evening.</summary>
    public DateOnly? OnDate { get; set; }

    /// <summary>Whether the division has it switched on: one switched off holds nowhere.</summary>
    public bool IsActive { get; set; }

    /// <summary>The row as IVAO sent it, with the position it names.</summary>
    public string RawJson { get; set; } = "{}";

    public DateTime SyncedAt { get; set; }
}

/// <summary>One FRA as the client reads it from IVAO: <see cref="IvaoFra"/> before it is a row.</summary>
public sealed record IvaoFraDto(
    long Id,
    string Callsign,
    int MinimumRating,
    int Days,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    DateOnly? OnDate,
    bool IsActive,
    string RawJson);

/// <summary>
/// Turns the pages of <c>/v2/fras</c> into <see cref="IvaoFraDto"/>s. It lives on its own, like the readers of the tracker and
/// of the positions, so that the real client and the fixture client read the payload the same way.
/// </summary>
public static class IvaoFraReader
{
    /// <summary>The most <c>/v2/fras</c> gives in a page, as its documentation says and as it answered on 30 September 2026.</summary>
    public const int PageSize = 100;

    /// <summary>
    /// How many pages a client reads before it gives up: five thousand FRAs, twelve times Italy's. A division with more would
    /// be read as no answer, never as half of one.
    /// </summary>
    public const int MaxPages = 50;

    private static readonly (string Field, DayOfWeek Day)[] Weekdays =
    [
        ("dayMon", DayOfWeek.Monday),
        ("dayTue", DayOfWeek.Tuesday),
        ("dayWed", DayOfWeek.Wednesday),
        ("dayThu", DayOfWeek.Thursday),
        ("dayFri", DayOfWeek.Friday),
        ("daySat", DayOfWeek.Saturday),
        ("daySun", DayOfWeek.Sunday),
    ];

    /// <summary>The bit of <see cref="IvaoFra.Days"/> that stands for <paramref name="day"/>.</summary>
    public static int DayBit(DayOfWeek day) => 1 << (int)day;

    /// <summary>The FRAs of one page, and how many pages IVAO says there are.</summary>
    public static (IReadOnlyList<IvaoFraDto> Rows, int Pages) ReadPage(JsonElement page)
    {
        if (page.ValueKind != JsonValueKind.Object
            || !page.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return ([], 0);
        }

        var pages = page.TryGetProperty("pages", out var count) && count.ValueKind == JsonValueKind.Number
            && count.TryGetInt32(out var number)
                ? number
                : 1;

        return (Read(items.EnumerateArray()), pages);
    }

    /// <summary>
    /// The FRAs of a position among <paramref name="rows"/>. A row a member's exception, one that names no position IVAO
    /// expanded, one without a minimum or with a time that does not read, is skipped: one odd row of IVAO must not make the
    /// night's snapshot fail, nor bring a person into it.
    /// </summary>
    public static IReadOnlyList<IvaoFraDto> Read(IEnumerable<JsonElement> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var fras = new List<IvaoFraDto>();
        var seen = new HashSet<long>();

        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object
                || !Integer(row, "id", out var id)
                || Names(row, "userId")
                || Names(row, "user_id")
                || Flag(row, "isBlacklist")
                || Callsign(row) is not { Length: <= IvaoAtcPosition.MaxCallsignLength } callsign
                || !Integer(row, "minAtc", out var minimum)
                || Time(row, "startTime") is not { } startsAt
                || Time(row, "endTime") is not { } endsAt
                || !seen.Add(id))
            {
                continue;
            }

            var days = Weekdays.Where(weekday => Flag(row, weekday.Field)).Sum(weekday => DayBit(weekday.Day));

            fras.Add(new IvaoFraDto(
                id,
                callsign,
                (int)minimum,
                days,
                startsAt,
                endsAt,
                Date(row, "date"),
                Flag(row, "active"),
                row.GetRawText()));
        }

        return fras;
    }

    /// <summary>The callsign of the position the FRA names: an airport's position, or a sector of a FIR.</summary>
    private static string? Callsign(JsonElement row)
    {
        foreach (var expansion in new[] { "atcPosition", "subcenter" })
        {
            if (row.TryGetProperty(expansion, out var position)
                && position.ValueKind == JsonValueKind.Object
                && position.TryGetProperty("composePosition", out var compose)
                && compose.ValueKind == JsonValueKind.String
                && compose.GetString()?.Trim() is { Length: > 0 } callsign)
            {
                return callsign.ToUpperInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// A time of day as IVAO writes it — <c>23:00:00</c>, as it answered on 30 September 2026, or <c>23:00</c>, as its
    /// documentation shows. A <c>24:00</c> is the end of the day, which is the midnight an FRA runs up to.
    /// </summary>
    private static TimeOnly? Time(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        if (text is "24:00" or "24:00:00")
        {
            return TimeOnly.MinValue;
        }

        return TimeOnly.TryParseExact(text, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
    }

    /// <summary>The date of an FRA of one date: <c>2026-09-12</c>, or the same with a time IVAO would add to it.</summary>
    private static DateOnly? Date(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: >= 10 } text
        && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static bool Integer(JsonElement row, string property, out long number)
    {
        number = 0;
        return row.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out number);
    }

    /// <summary>Whether the row names somebody in <paramref name="property"/>: a member's row.</summary>
    private static bool Names(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
        && !(value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var vid) && vid == 0);

    private static bool Flag(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
