using IvaoHub.Core.Atc;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Staff;

namespace IvaoHub.Modules.Events.After;

/// <summary>A connection on a position as the check of a shift reads it: from when to when; no end while it is still open.</summary>
public sealed record PositionConnection(int? Vid, DateTime StartedAt, DateTime? EndedAt);

/// <summary>
/// The rules of the checks after an event that need nothing but their values (design M4 §4.5, §5.1, E13a; note
/// 2026-10-16-le-verifiche-dopo-l-evento, readings 1–4 and 7): when an event is checked, the window its flights are looked for in, which
/// session a booking flew, the minutes a controller was on a position within a shift, and whether that is enough. Pure, and tested as such;
/// <see cref="EventsAfterJob"/> reads the rows, the sources and the clock, and asks here.
/// </summary>
public static class AfterRules
{
    /// <summary>
    /// How long after its end the job keeps asking the sources of an event that still has rows to check (reading 7): then it closes the
    /// event with those rows left as they are, never marked, for the staff to settle by hand. The tracker keeps about ninety days.
    /// </summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// How far before a shift a controller's connections are looked for on the tracker (reading 4): its window holds the sessions that
    /// <i>started</i> in it (E10a), and a controller may have connected well before their shift.
    /// </summary>
    public static readonly TimeSpan ConnectedBefore = TimeSpan.FromHours(12);

    /// <summary>
    /// When an event is checked (reading 1): six hours after its end — the margin a slot falls in (<see cref="SlotWindow.Margin"/>), so
    /// the last departure allowed has left.
    /// </summary>
    public static DateTime DueAt(Event row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.EndsAtUtc + SlotWindow.Margin;
    }

    /// <summary>Whether an event is to be checked now: published, not cancelled, its checks not done, its moment passed.</summary>
    public static bool IsDue(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row is { Status: PublishStatus.Published, CancelledAt: null, AfterDoneAt: null } && DueAt(row) <= now;
    }

    /// <summary>Whether the job stops asking the sources for an event and closes it with what it found (reading 7).</summary>
    public static bool IsGivenUp(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.EndsAtUtc + GiveUpAfter <= now;
    }

    /// <summary>
    /// The window the flights of an event are looked for in (reading 2): the sessions that started between its start and its end widened
    /// by the margin of the slots — the same for the bookings and for the flights nobody booked, so the two count the same flights.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) FlightWindow(Event row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return (row.StartsAtUtc - SlotWindow.Margin, row.EndsAtUtc + SlotWindow.Margin);
    }

    /// <summary>
    /// The session a booking flew (reading 2): among the ones found for its pilot between its two airports, the one that started nearest
    /// to its departure time and that no other booking of the event took; none when there is none. The callsign does not count (c1).
    /// </summary>
    public static IvaoTrackerSessionDto? Flown(BookedFlight flight, IEnumerable<IvaoTrackerSessionDto> found, IReadOnlySet<long> taken)
    {
        ArgumentNullException.ThrowIfNull(flight);
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(taken);

        var departure = flight.OffBlockUtc ?? flight.OnBlockUtc;

        return found
            .Where(session => !taken.Contains(session.Id))
            .OrderBy(session => departure is { } at ? Math.Abs((session.StartedAt - at).Ticks) : 0)
            .ThenBy(session => session.StartedAt)
            .ThenBy(session => session.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// The minutes of these connections within <paramref name="fromUtc"/>–<paramref name="toUtc"/> (reading 4): the pieces cut to the shift,
    /// those that overlap counted once, one still open counted to the shift's end. Whole minutes, rounded down.
    /// </summary>
    public static int MinutesWithin(IEnumerable<PositionConnection> connections, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(connections);

        var pieces = connections
            .Select(connection => (From: Max(connection.StartedAt, fromUtc), To: Min(connection.EndedAt ?? toUtc, toUtc)))
            .Where(piece => piece.To > piece.From)
            .OrderBy(piece => piece.From)
            .ToList();

        var total = TimeSpan.Zero;
        DateTime? start = null;
        DateTime end = default;
        foreach (var (from, to) in pieces)
        {
            if (start is null || from > end)
            {
                if (start is { } open)
                {
                    total += end - open;
                }

                start = from;
                end = to;
            }
            else if (to > end)
            {
                end = to;
            }
        }

        if (start is { } last)
        {
            total += end - last;
        }

        return (int)total.TotalMinutes;
    }

    /// <summary>
    /// The minutes a shift asks to be attended (reading 4): <c>attendanceMinimumMinutes</c>, and never more than the shift lasts — a shift
    /// of twenty minutes with the setting at thirty is attended by being there all of it.
    /// </summary>
    public static int Threshold(int minimumMinutes, DateTime fromUtc, DateTime toUtc) =>
        Math.Max(1, Math.Min(minimumMinutes, (int)(toUtc - fromUtc).TotalMinutes));

    /// <summary>
    /// Who covered a position in a shift its controller missed (reading 5): among the archive's connections on it, somebody else's — a VID
    /// the archive gives, not the holder's — with the most minutes within the shift; none when nobody was there.
    /// </summary>
    public static int? CoveredBy(IEnumerable<AtcPresence> onThePosition, int holder, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(onThePosition);

        return onThePosition
            .Where(presence => presence.Vid is { } vid && vid != holder && vid > 0)
            .GroupBy(presence => presence.Vid!.Value)
            .Select(group => (Vid: group.Key, Minutes: MinutesWithin(group.Select(Connection), fromUtc, toUtc)))
            .Where(entry => entry.Minutes > 0)
            .OrderByDescending(entry => entry.Minutes)
            .ThenBy(entry => entry.Vid)
            .Select(entry => (int?)entry.Vid)
            .FirstOrDefault();
    }

    /// <summary>A connection of the archive, as the minutes are counted.</summary>
    public static PositionConnection Connection(AtcPresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);

        return new PositionConnection(presence.Vid, presence.StartedAt, presence.EndedAt);
    }

    /// <summary>A session of the tracker, as the minutes are counted: its declared length.</summary>
    public static PositionConnection Connection(IvaoTrackerSessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new PositionConnection(session.Vid, session.StartedAt, session.EndedAt);
    }

    private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;

    private static DateTime Min(DateTime first, DateTime second) => first < second ? first : second;
}
