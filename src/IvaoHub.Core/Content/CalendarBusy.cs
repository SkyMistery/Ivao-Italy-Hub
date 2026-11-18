using IvaoHub.Core.Data;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>The row of a module that is being written, so that its own entry does not occupy its own date.</summary>
/// <param name="Module">The module that projected the entry: <c>core</c>, <c>events</c>, <c>training</c>….</param>
/// <param name="Id">The entry's <c>source_id</c>, as that module writes it.</param>
public sealed record CalendarSource(string Module, string Id);

/// <summary>
/// One stretch of time the calendar says is taken, as the date picker draws it: already widened by the rules of
/// <see cref="CalendarBusy.Occupies"/>, so the browser draws what the server refuses and nothing else.
/// </summary>
/// <param name="StartsAtUtc">The first moment taken.</param>
/// <param name="EndsAtUtc">The first moment free again.</param>
/// <param name="AllDay">An entry of the whole day.</param>
/// <param name="Kind">The kind of the entry, the key the legend and its colour come from.</param>
/// <param name="Blocks">Whether the kind takes these hours from the picker (<see cref="CalendarKind.Blocks"/>).</param>
/// <param name="Title">What the entry is called, in the languages of the division.</param>
public sealed record CalendarBusyDto(
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool AllDay,
    string Kind,
    bool Blocks,
    Localized<string> Title);

/// <summary>
/// The free and the busy of the one calendar, for whoever chooses a date (note 2026-11-18-il-calendario-libero-e-occupato): the
/// read the picker of the form draws, and the check a module's validator asks before it writes a date. The two are one query
/// and one rule, so the hours the browser switches off are the ones the server refuses.
/// <para>Both read through the global query filter, <b>as whoever is asking</b>: each person sees busy only what they may read in
/// the calendar, so a member never sees the staff's commitments, not even as a nameless "busy" (Carmine's answer 2); and a date
/// is refused only for an entry its writer could have seen. A draft occupies nothing, because it projects nothing
/// (<see cref="ProjectionSnapshot.Unpublished"/>), and a cancelled event has already left the calendar.</para>
/// </summary>
public sealed class CalendarBusy(HubDbContext database)
{
    /// <summary>The longest window the picker may ask for: a month and the weeks it shares with the next and the last.</summary>
    public const int MaxWindowDays = 62;

    /// <summary>A date refused because an entry of a kind that blocks takes that time.</summary>
    public const string BlockedKey = "errors.calendar.blocked";

    /// <summary>A window that ends before it starts, or longer than <see cref="MaxWindowDays"/>.</summary>
    public const string WindowKey = "errors.calendar.window";

    /// <summary>
    /// The time an entry takes (Carmine's answer 4): an entry of the whole day takes its days, in UTC (answer 3), up to the
    /// midnight that ends it or the end of the day it ends on; an entry with no end, or with an end that is not after its start,
    /// takes the hour it starts in; any other takes from its start to its end.
    /// </summary>
    public static (DateTime StartsAtUtc, DateTime EndsAtUtc) Occupies(DateTime startsAtUtc, DateTime? endsAtUtc, bool allDay)
    {
        var start = Utc(startsAtUtc);
        var end = endsAtUtc is { } value ? Utc(value) : (DateTime?)null;

        if (allDay)
        {
            var last = end is { } until && until > start ? until : start;
            var through = last.TimeOfDay == TimeSpan.Zero && last > start ? last : last.Date.AddDays(1);
            return (start.Date, through);
        }

        if (end is not { } finish || finish <= start)
        {
            var hour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0, DateTimeKind.Utc);
            return (hour, hour.AddHours(1));
        }

        return (start, finish);
    }

    /// <summary>Whether a window is one the picker may ask for: it ends after it starts, and it is no longer than the most.</summary>
    public static bool IsWindow(DateTime fromUtc, DateTime toUtc) =>
        toUtc > fromUtc && toUtc - fromUtc <= TimeSpan.FromDays(MaxWindowDays);

    /// <summary>Everything busy in the window that the reader may see, by its start, the row being written left out.</summary>
    public async Task<IReadOnlyList<CalendarBusyDto>> ReadAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CalendarSource? except,
        CancellationToken cancellationToken)
    {
        var from = Utc(fromUtc);
        var to = Utc(toUtc);

        var blocking = await database.CalendarKinds
            .AsNoTracking()
            .Where(kind => kind.Blocks)
            .Select(kind => kind.Key)
            .ToListAsync(cancellationToken);

        // A superset first, in the database: an entry with no end, or of a whole day, may take time up to a day after it starts,
        // so the lower bound is a day earlier; the rule itself is applied below, once, in Occupies.
        var reach = from.AddDays(-1);
        var query = database.CalendarEntries
            .AsNoTracking()
            .Where(entry => entry.StartsAtUtc < to
                && (entry.EndsAtUtc == null ? entry.StartsAtUtc >= reach : entry.EndsAtUtc >= reach));

        if (except is not null)
        {
            query = query.Where(entry => !(entry.SourceModule == except.Module && entry.SourceId == except.Id));
        }

        var entries = await query
            .OrderBy(entry => entry.StartsAtUtc)
            .ThenBy(entry => entry.Id)
            .Select(entry => new { entry.StartsAtUtc, entry.EndsAtUtc, entry.AllDay, entry.Kind, entry.Title })
            .ToListAsync(cancellationToken);

        return
        [
            .. entries
                .Select(entry =>
                {
                    var (starts, ends) = Occupies(entry.StartsAtUtc, entry.EndsAtUtc, entry.AllDay);
                    return new CalendarBusyDto(starts, ends, entry.AllDay, entry.Kind, blocking.Contains(entry.Kind), entry.Title);
                })
                .Where(busy => busy.StartsAtUtc < to && busy.EndsAtUtc > from)
                .OrderBy(busy => busy.StartsAtUtc),
        ];
    }

    /// <summary>
    /// The entries of a kind that blocks which take some of the time a date takes, as its writer may see them; empty when the
    /// date is free. The date takes its time by the same rule as an entry (<see cref="Occupies"/>): with no end, the hour it
    /// starts in — which is the hour the picker switches off.
    /// <para>A module's validator asks it on the field of the date and refuses with <see cref="BlockedKey"/> when it is not empty;
    /// the module says which of its rows is being written, so that a date moved within its own time is not refused by itself.</para>
    /// </summary>
    public async Task<IReadOnlyList<CalendarBusyDto>> BlockingAsync(
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        CalendarSource? except,
        CancellationToken cancellationToken)
    {
        var (from, to) = Occupies(startsAtUtc, endsAtUtc, allDay: false);
        var busy = await ReadAsync(from, to, except, cancellationToken);
        return [.. busy.Where(entry => entry.Blocks)];
    }

    /// <summary>A moment as UTC, whatever kind the binder gave it: a query string parsed in local time is converted, one with no kind is UTC.</summary>
    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
