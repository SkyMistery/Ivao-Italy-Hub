using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.People;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Registers;

/// <summary>One pilot in the register of the staff (§3.7): the bookings not flown counted and taken away, whether they have the limits and until when.</summary>
public sealed record PilotRegisterRowDto(
    EventMemberDto Pilot,
    int Counted,
    int Excused,
    bool Limited,
    DateTime? LimitedUntil);

/// <summary>A booking not flown as its pilot reads it in their record: the event, the flight, and whether the staff took it away and why.</summary>
public sealed record MyUnflownDto(
    long Id,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    string Callsign,
    string DepartureIcao,
    DateTime OffBlockUtc,
    string ArrivalIcao,
    bool Excused,
    string? ExcusedNote);

/// <summary>
/// A pilot's own record (§3.7, §7.1): the bookings not flown that count, the threshold, whether they have the limits and until when, the limits
/// valid in the event asked about — its own, or the settings' —, and the bookings not flown themselves, the latest first.
/// </summary>
public sealed record PilotRecordDto(
    int Counted,
    int Excused,
    int Threshold,
    bool Limited,
    DateTime? LimitedUntil,
    int WindowHours,
    int MaxPerWindow,
    int MaxPerEvent,
    IReadOnlyList<MyUnflownDto> Unflown);

/// <summary>A member's two registers, as <c>/events/mine</c> reads them and a page that refuses a booking explains it (reading 8).</summary>
public sealed record MemberRecordDto(PilotRecordDto Pilot, ControllerRecordDto Controller);

/// <summary>
/// The record of the pilots who book and do not fly (design M4 §3.7, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 5, 7 and
/// 8): the bookings the check after the event looked at and found no flight for (<see cref="EventBooking.FlownCheckedAt"/> written,
/// <see cref="EventBooking.FlownAt"/> empty), of events ended in the last <c>pilotRetentionMonths</c> — the rows the module keeps —, less
/// those the staff took away by hand (<see cref="EventBooking.UnflownExcusedBy"/>). From <c>unflownThreshold</c> of them a pilot has the
/// limits (<see cref="BookingLimits"/>).
/// </summary>
public sealed class PilotRegister(EventsDbContext database, HubDbContext hub, ModuleSettingsStore settingsStore)
{
    /// <summary>A booking not flown of a pilot, with when it leaves the record: the end of its event and the months bookings are kept.</summary>
    private sealed record Entry(long Id, int Vid, DateTime Leaving, bool Excused);

    /// <summary>The bookings not flown of these pilots — of every pilot, when <paramref name="vid"/> is null — still in the record.</summary>
    private async Task<List<Entry>> EntriesAsync(int? vid, DateTime now, int months, CancellationToken cancellationToken)
    {
        // The boundary events-retention deletes by (E15b): a booking leaves the record when it leaves the database.
        var since = EventRetention.PilotsKeptSince(now, months);
        var bookings = CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .Where(booking => booking.FlownCheckedAt != null && booking.FlownAt == null);
        if (vid is { } one)
        {
            bookings = bookings.Where(booking => booking.BookerVid == one);
        }

        var rows = await (
                from booking in bookings
                join row in CrudSource.BackOffice<Event>(database) on booking.EventId equals row.Id
                where row.EndsAtUtc >= since
                select new { booking.Id, booking.BookerVid, row.EndsAtUtc, Excused = booking.UnflownExcusedBy != null })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new Entry(row.Id, row.BookerVid, row.EndsAtUtc.AddMonths(months), row.Excused))];
    }

    /// <summary>
    /// The limits a pilot has in an event, or none (§3.7): read under the pilot's lock by the verbs of the bookings, so that the record is
    /// the one committed when they book.
    /// </summary>
    public async Task<PilotLimits?> LimitsAsync(int vid, Event row, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        var counted = (await EntriesAsync(vid, now, settings.PilotRetentionMonths, cancellationToken)).Count(entry => !entry.Excused);

        return counted >= settings.UnflownThreshold ? BookingLimits.Of(row, settings) : null;
    }

    /// <summary>The register of every pilot with a booking not flown in it, the ones with the limits first, a page of it with the names in one query.</summary>
    public async Task<PagedResult<PilotRegisterRowDto>> PageAsync(int page, int pageSize, string? search, DateTime now, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        var entries = await EntriesAsync(vid: null, now, settings.PilotRetentionMonths, cancellationToken);
        var names = await MemberNames.ReadAsync(hub, entries.Select(entry => entry.Vid), cancellationToken);

        var term = search?.Trim();
        var rows = entries
            .GroupBy(entry => entry.Vid)
            .Select(group =>
            {
                var leaving = group.Where(entry => !entry.Excused).Select(entry => entry.Leaving).ToList();
                var until = BookingLimits.Until(leaving, settings.UnflownThreshold);
                return new PilotRegisterRowDto(EventsPeople.Member(group.Key, names), leaving.Count, group.Count(entry => entry.Excused), until is not null, until);
            })
            .Where(row => string.IsNullOrEmpty(term) || RegisterPages.Matches(row.Pilot, term))
            .OrderByDescending(row => row.Limited)
            .ThenByDescending(row => row.Counted)
            .ThenBy(row => row.Pilot.Vid)
            .ToList();

        return RegisterPages.Page(rows, page, pageSize);
    }

    /// <summary>
    /// A pilot's own record, with the limits valid in <paramref name="eventId"/> when they ask about an event they see — its own, or the
    /// settings' —, and their bookings not flown still in the record, with their flights (<see cref="BookedFlight"/>).
    /// </summary>
    public async Task<PilotRecordDto> OfAsync(int vid, long? eventId, DateTime now, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        var entries = await EntriesAsync(vid, now, settings.PilotRetentionMonths, cancellationToken);
        var leaving = entries.Where(entry => !entry.Excused).Select(entry => entry.Leaving).ToList();
        var until = BookingLimits.Until(leaving, settings.UnflownThreshold);

        var seen = eventId is { } id
            ? await database.Events.AsNoTracking().Where(EventState.Seen(now)).FirstOrDefaultAsync(row => row.Id == id, cancellationToken)
            : null;
        var limits = seen is null
            ? new PilotLimits(settings.RestrictedWindowHours, settings.RestrictedMaxPerWindow, settings.RestrictedMaxPerEvent)
            : BookingLimits.Of(seen, settings);

        var ids = entries.Select(entry => entry.Id).ToList();
        var rows = await (
                from booking in CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
                join slot in CrudSource.BackOffice<EventSlot>(database) on booking.SlotId equals slot.Id
                join row in CrudSource.BackOffice<Event>(database) on booking.EventId equals row.Id
                where ids.Contains(booking.Id)
                select new { booking, slot, row.Slug, row.Title, row.EndsAtUtc })
            .ToListAsync(cancellationToken);

        return new PilotRecordDto(
            leaving.Count,
            entries.Count(entry => entry.Excused),
            settings.UnflownThreshold,
            until is not null,
            until,
            limits.WindowHours,
            limits.MaxPerWindow,
            limits.MaxPerEvent,
            [
                .. rows
                    .OrderByDescending(entry => entry.EndsAtUtc)
                    .ThenBy(entry => BookedFlight.Of(entry.slot, entry.booking).OffBlockUtc)
                    .Select(entry =>
                    {
                        var flight = BookedFlight.Of(entry.slot, entry.booking);
                        return new MyUnflownDto(
                            entry.booking.Id,
                            entry.booking.EventId,
                            entry.Slug,
                            entry.Title,
                            flight.Callsign ?? string.Empty,
                            flight.DepartureIcao ?? string.Empty,
                            flight.OffBlockUtc.GetValueOrDefault(),
                            flight.ArrivalIcao ?? string.Empty,
                            entry.booking.UnflownExcusedBy is not null,
                            entry.booking.UnflownExcusedNote);
                    }),
            ]);
    }
}
