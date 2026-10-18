using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.People;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>
/// How a controller's shifts went, counted (design M4 §4.5, E13b): attended, confirmed no-shows, excused, and still to decide — a no-show the
/// check proposed, or a shift the check closed the event without (note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 3, 4 and 11).
/// </summary>
public sealed record ShiftCounts(int Attended, int NoShows, int Excused, int ToDecide)
{
    public static readonly ShiftCounts None = new(0, 0, 0, 0);
}

/// <summary>One controller in the register of the staff (§7.2): their counts and their penalty, named by the core's reader.</summary>
public sealed record ControllerRegisterRowDto(
    EventMemberDto Controller,
    int Attended,
    int NoShows,
    int Excused,
    int ToDecide,
    double Penalty);

/// <summary>A shift of an event over as its controller reads it in their register: the event, the position, the hours, how it went, and the staff's note.</summary>
public sealed record MyRegisterShiftDto(
    long Id,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    string Callsign,
    DateTime FromUtc,
    DateTime ToUtc,
    ShiftAttendance Attendance,
    string? Note);

/// <summary>A controller's own register (§4.5, §7.1): their counts, their penalty, and their shifts of the events over, the latest first.</summary>
public sealed record ControllerRecordDto(
    int Attended,
    int NoShows,
    int Excused,
    int ToDecide,
    double Penalty,
    IReadOnlyList<MyRegisterShiftDto> Shifts);

/// <summary>
/// The register of reliability of the controllers (design M4 §4.5, §7.2, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 3
/// and 4): every shift of every event and FIR counts — the register and the penalty are the controller's, one number wherever it is read,
/// the same one the proposal of a roster uses (<see cref="RosterData.PenaltiesAsync"/>). The shifts stay for good (c3), and so does the
/// register.
/// </summary>
public sealed class ControllerRegister(EventsDbContext database, HubDbContext hub, ModuleSettingsStore settingsStore)
{
    /// <summary>
    /// The counts of these controllers — of every one with a shift, when <paramref name="vids"/> is null —, leaving out one event: the
    /// proposal of its roster counts the others.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, ShiftCounts>> CountsAsync(
        EventsDbContext database,
        IReadOnlyCollection<int>? vids,
        long? exceptEventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var shifts = CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking();
        if (vids is not null)
        {
            shifts = shifts.Where(shift => vids.Contains(shift.ControllerVid));
        }

        if (exceptEventId is { } except)
        {
            shifts = shifts.Where(shift => shift.EventId != except);
        }

        // Still to decide: a no-show proposed, or a shift the check left unknown when it closed its event (reading 11) — before, the check
        // is looking at it.
        var grouped = await (
                from shift in shifts
                join row in CrudSource.BackOffice<Event>(database) on shift.EventId equals row.Id
                group new { shift.Attendance, Closed = row.AfterDoneAt != null } by shift.ControllerVid into counts
                select new
                {
                    Vid = counts.Key,
                    Attended = counts.Count(entry => entry.Attendance == ShiftAttendance.Attended),
                    NoShows = counts.Count(entry => entry.Attendance == ShiftAttendance.NoShow),
                    Excused = counts.Count(entry => entry.Attendance == ShiftAttendance.Excused),
                    ToDecide = counts.Count(entry => entry.Attendance == ShiftAttendance.NoShowProposed
                        || (entry.Attendance == ShiftAttendance.Unknown && entry.Closed)),
                })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(entry => entry.Vid, entry => new ShiftCounts(entry.Attended, entry.NoShows, entry.Excused, entry.ToDecide));
    }

    /// <summary>
    /// The register of every controller with something in it, by penalty, then by VID — the search box a VID or a piece of a name —, a page of
    /// it with the names in one query.
    /// </summary>
    public async Task<PagedResult<ControllerRegisterRowDto>> PageAsync(int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var weight = (await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken)).NoShowWeight;
        var counted = (await CountsAsync(database, vids: null, exceptEventId: null, cancellationToken))
            .Where(entry => entry.Value != ShiftCounts.None)
            .ToList();
        var names = await MemberNames.ReadAsync(hub, counted.Select(entry => entry.Key), cancellationToken);

        var term = search?.Trim();
        var rows = counted
            .Select(entry => new ControllerRegisterRowDto(
                EventsPeople.Member(entry.Key, names),
                entry.Value.Attended,
                entry.Value.NoShows,
                entry.Value.Excused,
                entry.Value.ToDecide,
                RosterProposer.Penalty(entry.Value.NoShows, entry.Value.Attended, weight)))
            .Where(row => string.IsNullOrEmpty(term) || RegisterPages.Matches(row.Controller, term))
            .OrderByDescending(row => row.Penalty)
            .ThenByDescending(row => row.ToDecide)
            .ThenBy(row => row.Controller.Vid)
            .ToList();

        return RegisterPages.Page(rows, page, pageSize);
    }

    /// <summary>A controller's own register: their counts and penalty, and their shifts of the events over, the latest first.</summary>
    public async Task<ControllerRecordDto> OfAsync(int vid, DateTime now, CancellationToken cancellationToken)
    {
        var weight = (await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken)).NoShowWeight;
        var counts = (await CountsAsync(database, [vid], exceptEventId: null, cancellationToken)).GetValueOrDefault(vid) ?? ShiftCounts.None;

        var shifts = await (
                from shift in CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
                join row in CrudSource.BackOffice<Event>(database) on shift.EventId equals row.Id
                join position in CrudSource.BackOffice<EventAtcPosition>(database) on shift.PositionId equals position.Id
                where shift.ControllerVid == vid && row.EndsAtUtc <= now && row.CancelledAt == null
                orderby shift.FromUtc descending, shift.Id descending
                select new { shift, row.Slug, row.Title, position.Callsign })
            .ToListAsync(cancellationToken);

        return new ControllerRecordDto(
            counts.Attended,
            counts.NoShows,
            counts.Excused,
            counts.ToDecide,
            RosterProposer.Penalty(counts.NoShows, counts.Attended, weight),
            [
                .. shifts.Select(entry => new MyRegisterShiftDto(
                    entry.shift.Id,
                    entry.shift.EventId,
                    entry.Slug,
                    entry.Title,
                    entry.Callsign,
                    entry.shift.FromUtc,
                    entry.shift.ToUtc,
                    entry.shift.Attendance,
                    entry.shift.AttendanceNote)),
            ]);
    }
}
