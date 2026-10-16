using System.Globalization;
using System.Text.Json;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// The short retention of an event in person (design M4 §4-bis.2, §11, E17; note 2026-10-15-le-attivita-e-la-conservazione-breve §4): its
/// registrations and its seats say who came in person and with what, so they go <c>inPersonRetentionMonths</c> after the event's end, and
/// what they summed stays in its statistics (<see cref="EventStats"/>). The one place that says when an event is past it, what the
/// statistics keep, and how the staff's lists read them back once the rows are gone — so that «the sums stay» is true on the page too.
/// </summary>
public sealed class InPersonRetention(EventsDbContext database, ModuleSettingsStore settingsStore, IClock clock)
{
    private static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Whether an event is past its retention: over (<see cref="EventState.IsOver"/>, the one clock of «finished»), and its end at least
    /// <paramref name="months"/> months ago.
    /// </summary>
    public static bool IsDue(Event row, int months, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return EventState.IsOver(row, now) && row.EndsAtUtc.AddMonths(months) <= now;
    }

    /// <summary>The sums of the answers as the statistics keep them: the numbers of each question.</summary>
    public static string SumsJson(IEnumerable<QuestionSum> sums) => JsonSerializer.Serialize(sums.ToList(), ColumnJson);

    /// <summary>The sums of the answers the statistics kept, by question; none for a column nobody wrote.</summary>
    public static IReadOnlyDictionary<long, QuestionSum> Sums(EventStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return (JsonSerializer.Deserialize<List<QuestionSum>>(stats.AnswerSumsJson, ColumnJson) ?? [])
            .ToDictionary(sum => sum.QuestionId);
    }

    /// <summary>The seats taken by activity as the statistics keep them: the activity's id and its seats.</summary>
    public static string SeatsJson(IReadOnlyDictionary<long, int> seats) =>
        JsonSerializer.Serialize(seats.ToDictionary(seat => seat.Key.ToString(CultureInfo.InvariantCulture), seat => seat.Value), ColumnJson);

    /// <summary>The seats taken by activity the statistics kept.</summary>
    public static IReadOnlyDictionary<long, int> Seats(EventStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return (JsonSerializer.Deserialize<Dictionary<string, int>>(stats.ActivitySeatsJson, ColumnJson) ?? [])
            .Where(seat => long.TryParse(seat.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .ToDictionary(seat => long.Parse(seat.Key, CultureInfo.InvariantCulture), seat => seat.Value);
    }

    /// <summary>
    /// The statistics of these events whose retention has run: the ones whose numbers the staff's lists read instead of the rows (reading
    /// 20) — past the retention date, with statistics, and <b>no registration and no seat left</b>. A date passed is not enough (the review
    /// of #271, point 1): §1.10 has the statistics worked out after the event too (E13a), and between the date and the run of the month the
    /// rows are still there and the statistics may not say their sums. An event still within it, or whose rows the run has not taken yet,
    /// is read from its rows.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, EventStats>> RetiredAsync(IReadOnlyCollection<long> eventIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        if (eventIds.Count == 0)
        {
            return new Dictionary<long, EventStats>();
        }

        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        var now = clock.UtcNow;
        var due = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .Where(row => eventIds.Contains(row.Id))
                .ToListAsync(cancellationToken))
            .Where(row => IsDue(row, settings.InPersonRetentionMonths, now))
            .Select(row => row.Id)
            .ToList();
        if (due.Count == 0)
        {
            return new Dictionary<long, EventStats>();
        }

        // The ones still holding a registration or a seat: the run has not taken them yet.
        var holding = (await CrudSource.BackOffice<EventRegistration>(database).AsNoTracking()
                .Where(registration => due.Contains(registration.EventId))
                .Select(registration => registration.EventId)
                .Union(CrudSource.BackOffice<EventActivitySeat>(database).AsNoTracking()
                    .Where(seat => due.Contains(seat.EventId))
                    .Select(seat => seat.EventId))
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var retired = due.Where(id => !holding.Contains(id)).ToList();

        return await CrudSource.BackOffice<EventStats>(database).AsNoTracking()
            .Where(stats => retired.Contains(stats.EventId))
            .ToDictionaryAsync(stats => stats.EventId, cancellationToken);
    }

    /// <summary>
    /// The seats taken in each activity of these events, by activity: counted from the seats, or read from the statistics of an event past
    /// its retention.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, int>> SeatsTakenAsync(IReadOnlyCollection<long> eventIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        var retired = await RetiredAsync(eventIds, cancellationToken);
        var live = eventIds.Where(id => !retired.ContainsKey(id)).ToList();

        var taken = (await CrudSource.BackOffice<EventActivitySeat>(database).AsNoTracking()
                .Where(seat => live.Contains(seat.EventId))
                .GroupBy(seat => seat.ActivityId)
                .Select(activity => new { activity.Key, Count = activity.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(activity => activity.Key, activity => activity.Count);

        foreach (var (activity, seats) in retired.Values.SelectMany(stats => Seats(stats)))
        {
            taken[activity] = seats;
        }

        return taken;
    }
}
