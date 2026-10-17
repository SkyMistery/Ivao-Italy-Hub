using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.People;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// One booking of a position of the event on the network (design M4 §4.4, §9.1, E15b; note 2026-09-29-il-roster-atc §2.9): the position's
/// callsign, its hours, who booked it — named by the core's reader when the hub knows them, by their number otherwise —, what for, and whether
/// they hold a shift on that position in those hours (<see cref="OnRoster"/>): the roster and the network agree.
/// </summary>
public sealed record NetworkBookingRowDto(
    string Callsign,
    DateTime FromUtc,
    DateTime ToUtc,
    EventMemberDto Booker,
    AtcBookingKind Kind,
    bool OnRoster);

/// <summary>
/// A page of the bookings of the network beside the roster, in the shape of every list of the hub, and whether the network answered:
/// <see cref="Available"/> false — the network could not be asked, or the event spans more days than the core asks — is never «nobody
/// booked» (note 2026-09-30-le-prenotazioni-atc-della-rete §3.2).
/// </summary>
public sealed record NetworkBookingsPage(IReadOnlyList<NetworkBookingRowDto> Items, int Page, int PageSize, int Total, bool Available);

/// <summary>
/// The bookings of the network beside the roster of an event (design M4 §4.4, §9.1, E15b; notes 2026-09-29-il-roster-atc §2.9,
/// 2026-09-30-le-prenotazioni-atc-della-rete and 2026-10-17-la-conservazione-di-m4b, reading 6): <b>read when the page asks</b>, through the
/// core's <see cref="IAtcBookingSource"/> — no job, no copy, no cache (Carmine's answer on #207), at most seven days of UTC —, for the
/// positions of the event the reader may read, as the roster asks the one handler on each with its FIR. One question of the event's window for
/// every position; a booking is kept on a callsign of the event, in the hours of one of its positions with that callsign.
/// <para>⚠️ <b>One hand written read</b>, the eighth of the ATC, beside the roster's: nothing of it is a row of the hub. The page asks it in a
/// query of its own, so a slow network makes the bookings wait and never the roster.</para>
/// </summary>
public static class NetworkBookings
{
    /// <summary>The bookings of the network beside the roster of an event.</summary>
    public const string Pattern = "/api/events/atc/events/{id:long}/network-bookings";

    public static IEndpointRouteBuilder MapNetworkBookingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, ReadAsync)
            .WithTags("EventAtc")
            .WithName("EventsAtcNetworkBookings")
            .Produces<NetworkBookingsPage>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.AtcView);

        return app;
    }

    /// <summary>
    /// The bookings that overlap the hours of the positions, each with whether its booker holds a shift on that callsign in those hours. Pure,
    /// so a test reads it without the network: a booking that meets two positions of the same callsign is one row.
    /// </summary>
    public static IReadOnlyList<(AtcBookingDto Booking, bool OnRoster)> Beside(
        IReadOnlyList<AtcBookingDto> bookings,
        IReadOnlyList<EventAtcPosition> positions,
        IReadOnlyList<EventAtcShift> shifts)
    {
        ArgumentNullException.ThrowIfNull(bookings);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(shifts);

        var callsigns = positions.ToLookup(position => position.Callsign.ToUpperInvariant());

        return
        [
            .. bookings
                .Where(booking => callsigns[booking.Callsign.ToUpperInvariant()]
                    .Any(position => AtcRules.Meet(position.FromUtc, position.ToUtc, booking.StartsAt, booking.EndsAt)))
                .Select(booking =>
                {
                    var ids = callsigns[booking.Callsign.ToUpperInvariant()].Select(position => position.Id).ToHashSet();
                    var onRoster = shifts.Any(shift => ids.Contains(shift.PositionId)
                        && shift.ControllerVid == booking.Vid
                        && AtcRules.Meet(shift.FromUtc, shift.ToUtc, booking.StartsAt, booking.EndsAt));
                    return (booking, onRoster);
                }),
        ];
    }

    private static async Task<IResult> ReadAsync(
        long id,
        int? page,
        int? pageSize,
        string? q,
        EventsDbContext database,
        HubDbContext hub,
        IAtcBookingSource source,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.HasRoster, http.RequestAborted);
        if (row is null)
        {
            return Results.NotFound();
        }

        var readable = new List<EventAtcPosition>();
        foreach (var position in await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
                     .Where(position => position.EventId == id)
                     .ToListAsync(http.RequestAborted))
        {
            if ((await authorization.AuthorizeAsync(http.User, position, EventsPermissions.AtcView)).Succeeded)
            {
                readable.Add(position);
            }
        }

        var size = Math.Clamp(pageSize ?? 100, 1, 200);
        var number = Math.Max(page ?? 1, 1);

        // No position to read: nothing to ask the network.
        if (readable.Count == 0)
        {
            return Results.Ok(new NetworkBookingsPage([], number, size, 0, Available: true));
        }

        // The positions sit inside the event's hours: one question of them all, the core asking each day it touches.
        if (await source.BookedAsync(row.StartsAtUtc, row.EndsAtUtc, callsign: null, http.RequestAborted) is not { } booked)
        {
            return Results.Ok(new NetworkBookingsPage([], number, size, 0, Available: false));
        }

        var ids = readable.Select(position => position.Id).ToList();
        var shifts = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking()
            .Where(shift => shift.EventId == id && ids.Contains(shift.PositionId))
            .ToListAsync(http.RequestAborted);
        var beside = Beside(booked, readable, shifts);
        var names = await MemberNames.ReadAsync(hub, beside.Select(entry => entry.Booking.Vid), http.RequestAborted);

        var term = q?.Trim();
        var all = beside
            .Select(entry => new NetworkBookingRowDto(
                entry.Booking.Callsign,
                entry.Booking.StartsAt,
                entry.Booking.EndsAt,
                EventsPeople.Member(entry.Booking.Vid, names),
                entry.Booking.Kind,
                entry.OnRoster))
            .Where(entry => string.IsNullOrEmpty(term)
                || entry.Callsign.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.Booker.Vid.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)
                || entry.Booker.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return Results.Ok(new NetworkBookingsPage([.. all.Skip((number - 1) * size).Take(size)], number, size, all.Count, Available: true));
    }
}
