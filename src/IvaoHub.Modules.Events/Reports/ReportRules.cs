using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Registrations;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>One item as a member sends it (§5.2): a flight — callsign, departure, arrival, the day at midnight UTC — or a shift — the position, from, to.</summary>
public sealed record ReportItemRequest(
    string? Callsign,
    string? DepartureIcao,
    string? ArrivalIcao,
    DateTime? FlightDay,
    DateTime? FromUtc,
    DateTime? ToUtc);

/// <summary>A report as a member sends it, or sends again to correct it (§5.2): its items, all of them, every time.</summary>
public sealed record ReportRequest(IReadOnlyList<ReportItemRequest>? Items);

/// <summary>An item checked by the server's rules, ready for the check: a flight or a shift, its values upper case and in UTC.</summary>
public sealed record ReportItemDraft(
    string Callsign,
    string? DepartureIcao,
    string? ArrivalIcao,
    DateOnly? FlightDay,
    DateTime? FromUtc,
    DateTime? ToUtc);

/// <summary>What the check found of one item (reading 5): the session, the minutes, the distance, and whether it is verified — none: not asked.</summary>
public sealed record ItemFinding(long? SessionId, int? Minutes, decimal? DistanceNm, bool? Verified);

/// <summary>
/// The rules of the reports of support that need nothing but their values (design M4 §5.2, §5.3, E14a; note 2026-10-17-il-pirep-di-supporto,
/// readings 3–6): which event takes a report and until when, what an item may say, which session a flight flew, and what the items make of
/// the report. Pure, and tested as such; <see cref="MemberReports"/> and <see cref="ReportCheck"/> read the rows, the sources and the clock.
/// </summary>
public static class ReportRules
{
    /// <summary>The most items a report holds: an evening of flights, or of shifts.</summary>
    public const int MaxItems = 20;

    /// <summary>The longest shift an item may say: the longest shift of the settings.</summary>
    public static readonly TimeSpan LongestShift = TimeSpan.FromHours(12);

    public const string ClosedKey = "events:errors.reportClosed";

    public const string DecidedKey = "events:errors.reportDecided";

    public const string NoItemsKey = "events:errors.reportNoItems";

    public const string TooManyItemsKey = "events:errors.reportTooManyItems";

    public const string AirportUnknownKey = "events:errors.reportAirportUnknown";

    public const string SameAirportKey = "events:errors.reportSameAirport";

    public const string NotAtTheEventKey = "events:errors.reportNotAtTheEvent";

    public const string OutsideTheEventKey = "events:errors.reportOutsideTheEvent";

    public const string EndsBeforeItStartsKey = "events:errors.windowEndsBeforeItStarts";

    public const string TooLongKey = "events:errors.reportShiftTooLong";

    public const string OverlapKey = "events:errors.reportShiftsOverlap";

    /// <summary>Until when a report of an event may be sent and corrected (reading 3): <c>reportDays</c> after its end.</summary>
    public static DateTime ClosesAt(Event row, int reportDays)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.EndsAtUtc.AddDays(reportDays);
    }

    /// <summary>
    /// Whether an event takes a report now (reading 3): published, not cancelled, not in person, over, and not past <c>reportDays</c> from its
    /// end.
    /// </summary>
    public static bool IsOpen(Event row, int reportDays, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row is { Status: PublishStatus.Published, CancelledAt: null, InPerson: false }
            && EventState.IsOver(row, now)
            && now < ClosesAt(row, reportDays);
    }

    /// <summary>
    /// The items of a request checked one by one (reading 4), each refusal on its field — <c>items[2].arrivalIcao</c> —; null when something is
    /// refused. A flight has a callsign, two airports the hub knows and are not the same, a day in the event's window of flights, and touches
    /// one of the event's airports when it has its own; a shift a callsign and its hours inside the same window, ending after it starts, no
    /// longer than <see cref="LongestShift"/>, never over another of the report. <paramref name="problems"/> comes in empty.
    /// </summary>
    public static IReadOnlyList<ReportItemDraft>? Check(
        ReportKind kind,
        Event row,
        IReadOnlyCollection<string> eventAirports,
        IReadOnlySet<string> knownAirports,
        IReadOnlyList<ReportItemRequest>? items,
        Refusals problems)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(eventAirports);
        ArgumentNullException.ThrowIfNull(knownAirports);
        ArgumentNullException.ThrowIfNull(problems);

        if (items is null || items.Count == 0)
        {
            problems.Add("items", NoItemsKey);
            return null;
        }

        if (items.Count > MaxItems)
        {
            problems.Add("items", TooManyItemsKey);
            return null;
        }

        var (fromUtc, toUtc) = AfterRules.FlightWindow(row);
        var drafts = new List<ReportItemDraft>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index] ?? new ReportItemRequest(null, null, null, null, null, null);
            var field = $"items[{index}]";
            var callsign = Code(item.Callsign);
            if (callsign is null)
            {
                problems.Add($"{field}.callsign", "errors.required");
            }
            else if (callsign.Length > EventReportItem.MaxCallsignLength)
            {
                problems.Add($"{field}.callsign", "errors.text.tooLong");
            }

            if (kind == ReportKind.Pilot)
            {
                drafts.Add(Flight(item, field, callsign, row, eventAirports, knownAirports, fromUtc, toUtc, problems));
            }
            else
            {
                drafts.Add(Shift(item, field, callsign, fromUtc, toUtc, problems));
            }
        }

        if (kind == ReportKind.Atc)
        {
            for (var index = 0; index < drafts.Count; index++)
            {
                var shift = drafts[index];
                if (shift.FromUtc is { } from && shift.ToUtc is { } to
                    && drafts.Take(index).Any(other => other.FromUtc < to && from < other.ToUtc))
                {
                    problems.Add($"items[{index}].fromUtc", OverlapKey);
                }
            }
        }

        return problems.IsEmpty ? drafts : null;
    }

    /// <summary>
    /// Whether a session flew a route (E14b; note 2026-10-17-le-regole-di-award, reading 3-bis): the <b>last</b> revision of its flight plan says
    /// those two airports (E10v). The network finds a session for a route any revision of its plan said; one the pilot only filed and then
    /// changed is not a leg flown, and a rule of award never counts it. A session without the airports of its plan flew none.
    /// </summary>
    public static bool FlewRoute(NetworkSession session, string departure, string arrival)
    {
        ArgumentNullException.ThrowIfNull(session);

        return string.Equals(session.DepartureIcao, departure, StringComparison.OrdinalIgnoreCase)
            && string.Equals(session.ArrivalIcao, arrival, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a session lasted long enough to be a flight (E14b; Carmine's answer 1 on #293): at least <paramref name="minimumFlightMinutes"/>,
    /// the setting <c>minimumFlightMinutes</c>. A connection of a few minutes with a long plan filed flew nothing.
    /// </summary>
    public static bool LongEnough(NetworkSession session, int minimumFlightMinutes)
    {
        ArgumentNullException.ThrowIfNull(session);

        return (session.EndedAt - session.StartedAt).TotalMinutes >= minimumFlightMinutes;
    }

    /// <summary>
    /// The sessions the flights of a report flew (reading 5; the review of #289, point 4), each found among those between its two airports on
    /// its day: <b>first every item that finds one with its own callsign</b>, then the others, in their order, the first that started — a
    /// session never twice. Item by item, a flight declared «X» found no «X» and took the «Y» the next item declared, and the two read verified
    /// with each other's minutes. None where nothing is left, or where the source did not answer (no list found).
    /// </summary>
    public static NetworkSession?[] Flown(IReadOnlyList<(string Callsign, IReadOnlyList<NetworkSession>? Found)> flights)
    {
        ArgumentNullException.ThrowIfNull(flights);

        var taken = new HashSet<long>();
        var flown = new NetworkSession?[flights.Count];

        foreach (var exact in new[] { true, false })
        {
            for (var index = 0; index < flights.Count; index++)
            {
                if (flown[index] is not null || flights[index].Found is not { } found)
                {
                    continue;
                }

                var callsign = flights[index].Callsign;
                flown[index] = found
                    .Where(session => !taken.Contains(session.Id))
                    .Where(session => !exact || string.Equals(session.Callsign, callsign, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(session => session.StartedAt)
                    .ThenBy(session => session.Id)
                    .FirstOrDefault();
                if (flown[index] is { } session)
                {
                    taken.Add(session.Id);
                }
            }
        }

        return flown;
    }

    /// <summary>
    /// The window a flight's sessions are looked for in (reading 5): its day, cut to the event's window of flights — the sessions that started in it.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) DayWindow(Event row, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(row);

        var (fromUtc, toUtc) = AfterRules.FlightWindow(row);
        var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        return (start > fromUtc ? start : fromUtc, end < toUtc ? end : toUtc);
    }

    /// <summary>
    /// What the items make of the report (reading 5): any item not asked — a source that did not answer — makes it unavailable, never a guess;
    /// otherwise every item verified is valid, none invalid, some partial.
    /// </summary>
    public static ReportVerification Verification(IReadOnlyCollection<bool?> verified)
    {
        ArgumentNullException.ThrowIfNull(verified);

        if (verified.Count == 0 || verified.Any(item => item is null))
        {
            return ReportVerification.Unavailable;
        }

        var found = verified.Count(item => item == true);

        return found == verified.Count ? ReportVerification.Valid : found == 0 ? ReportVerification.Invalid : ReportVerification.Partial;
    }

    /// <summary>A code as the hub keeps it: trimmed, upper case; none when empty.</summary>
    public static string? Code(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();

    private static ReportItemDraft Flight(
        ReportItemRequest item,
        string field,
        string? callsign,
        Event row,
        IReadOnlyCollection<string> eventAirports,
        IReadOnlySet<string> knownAirports,
        DateTime fromUtc,
        DateTime toUtc,
        Refusals problems)
    {
        var departure = Airport(item.DepartureIcao, $"{field}.departureIcao", knownAirports, problems);
        var arrival = Airport(item.ArrivalIcao, $"{field}.arrivalIcao", knownAirports, problems);
        if (departure is not null && departure == arrival)
        {
            problems.Add($"{field}.arrivalIcao", SameAirportKey);
        }
        else if (departure is not null && arrival is not null && !row.WholeDivision && eventAirports.Count > 0
                 && !eventAirports.Contains(departure) && !eventAirports.Contains(arrival))
        {
            // A flight of an event with its own airports touches one of them (choice 3).
            problems.Add($"{field}.arrivalIcao", NotAtTheEventKey);
        }

        DateOnly? day = null;
        if (item.FlightDay is not { } at)
        {
            problems.Add($"{field}.flightDay", "errors.required");
        }
        else
        {
            day = DateOnly.FromDateTime(MemberSeats.Utc(at));
            var start = day.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            if (start.AddDays(1) <= fromUtc || start >= toUtc)
            {
                problems.Add($"{field}.flightDay", OutsideTheEventKey);
            }
        }

        return new ReportItemDraft(callsign ?? string.Empty, departure, arrival, day, null, null);
    }

    private static ReportItemDraft Shift(ReportItemRequest item, string field, string? callsign, DateTime fromUtc, DateTime toUtc, Refusals problems)
    {
        // An instant without its zone is read as UTC, never as the server's time (the review of #289, point 2).
        var from = item.FromUtc is { } sentFrom ? MemberSeats.Utc(sentFrom) : (DateTime?)null;
        var to = item.ToUtc is { } sentTo ? MemberSeats.Utc(sentTo) : (DateTime?)null;
        if (from is null)
        {
            problems.Add($"{field}.fromUtc", "errors.required");
        }

        if (to is null)
        {
            problems.Add($"{field}.toUtc", "errors.required");
        }

        if (from is { } start && to is { } end)
        {
            if (end <= start)
            {
                problems.Add($"{field}.toUtc", EndsBeforeItStartsKey);
            }
            else if (end - start > LongestShift)
            {
                problems.Add($"{field}.toUtc", TooLongKey);
            }
            else if (start < fromUtc || end > toUtc)
            {
                problems.Add($"{field}.fromUtc", OutsideTheEventKey);
            }
        }

        return new ReportItemDraft(callsign ?? string.Empty, null, null, null, from, to);
    }

    private static string? Airport(string? text, string field, IReadOnlySet<string> knownAirports, Refusals problems)
    {
        var code = Code(text);
        if (code is null)
        {
            problems.Add(field, "errors.required");
            return null;
        }

        if (!knownAirports.Contains(code))
        {
            problems.Add(field, AirportUnknownKey);
            return null;
        }

        return code;
    }
}
