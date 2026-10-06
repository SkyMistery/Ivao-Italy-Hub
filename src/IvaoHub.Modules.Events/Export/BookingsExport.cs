using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Export;

/// <summary>
/// One slot as the gate manager of the division reads it (design M4 §7.4): the fields it reads today from the booking system it
/// leaves, by their names there, with every time in UTC — and the stable identity of the slot it asks for, the flight number, the
/// rotation and the leg, and for a private slot the slot paired with it (E7), arrival and departure on the same gate.
/// <para>Whoever booked it and the aircraft they chose come with the bookings (E6a): empty on a free slot. The gate is the stand the
/// staff wrote, empty when there is none and on a private slot, until the stands are managed (§0.2).</para>
/// </summary>
public sealed record BookingExportDto(
    [property: JsonPropertyName("slot_id")] long SlotId,
    [property: JsonPropertyName("callsign")] string? Callsign,
    [property: JsonPropertyName("flight_number")] string? FlightNumber,
    [property: JsonPropertyName("booked_by")] int? BookedBy,
    [property: JsonPropertyName("aircraft_icao")] string? AircraftIcao,
    [property: JsonPropertyName("gate")] string? Gate,
    [property: JsonPropertyName("eobt")] DateTime? Eobt,
    [property: JsonPropertyName("eat")] DateTime? Eat,
    [property: JsonPropertyName("origin_icao")] string? OriginIcao,
    [property: JsonPropertyName("destination_icao")] string? DestinationIcao,
    [property: JsonPropertyName("rotation")] string? Rotation,
    [property: JsonPropertyName("leg")] int? Leg,
    [property: JsonPropertyName("paired_slot_id")] long? PairedSlotId);

/// <summary>
/// The export of the bookings of an event for the gate manager (design M4 §7.4, note 2026-09-29-i-tre-blocchi-e-che-cosa-resta-fuori-da-m4
/// §2.1, E5): <c>GET /api/events/{slug}/bookings/export</c>, read by a program with a member's personal token of the audience
/// <see cref="Audience"/> — never the cookie, never a key shared by everybody (CLAUDE.md §2) —, whose member holds
/// <c>EventBookings.View</c> on the event, asked of the one handler on its row; the permissions are rebuilt on every request.
/// <para>Every slot of the event, by its time at the airport of the event. <b>A draft is never exported</b> (§17.3 n.6): the gate
/// manager reads what was published. An error is a status of error: 401 without a token or with the cookie of the back office, 403
/// with a token of another audience or for an event whose slots the member does not read, 404 for an address no event has, 409 for
/// a draft.</para>
/// <para>404 comes before 403, as in the verbs of the back office, and that is meant: a token is made only by whoever holds
/// <c>EventBookings.View</c> somewhere, so whoever learns from a 403 that an address exists is staff of the bookings, and the address
/// of a published event is on the site anyway (the review of #228, point 7).</para>
/// <para>A contract with a program outside the hub, as the agent of the validators is (M2): its names are the gate manager's, and
/// they stay. It is counted with the endpoints written by hand (plan §16.6), among the verbs the design names (§7.2: «esportare»).</para>
/// </summary>
public static class BookingsExport
{
    /// <summary>What a member's token for the gate manager is for; its word is <c>events:tokenAudiences.bookings</c>.</summary>
    public const string Audience = "events.bookings";

    public const string Pattern = "/api/events/{slug}/bookings/export";

    /// <summary>What a program reads in the problem of a draft, besides its status.</summary>
    public const string DraftCode = "draft";

    public static IEndpointRouteBuilder MapBookingsExport(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, ExportAsync)
            .WithName("EventsBookingsExport")
            .WithTags("EventsExport")
            .Produces<IReadOnlyList<BookingExportDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(PersonalTokenPolicy.For(Audience));

        return app;
    }

    private static async Task<IResult> ExportAsync(
        string slug,
        EventsDbContext database,
        IAuthorizationService authorization,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, http.RequestAborted);

        if (row is null)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, code: null, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, row, EventsPermissions.BookingsView)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, code: null, catalog, currentUser);
        }

        if (row.Status != PublishStatus.Published)
        {
            return Problem(StatusCodes.Status409Conflict, "events:errors.exportDraft", DraftCode, catalog, currentUser);
        }

        var slots = await database.Slots.AsNoTracking()
            .Where(slot => slot.EventId == row.Id)
            .OrderBy(slot => slot.OffBlockUtc ?? slot.OnBlockUtc)
            .ThenBy(slot => slot.Id)
            .ToListAsync(http.RequestAborted);

        return Results.Ok(slots.Select(Flight).ToList());
    }

    /// <summary>
    /// A slot as a flight: a public one is its own flight; a private one is only its airport and its time there — the departure
    /// leaves from it, the arrival lands at it — until a pilot books it (E7).
    /// </summary>
    private static BookingExportDto Flight(EventSlot slot) => slot.Kind == SlotKind.Public
        ? new BookingExportDto(
            slot.Id,
            slot.Callsign,
            slot.FlightNumber,
            BookedBy: null,
            AircraftIcao: null,
            slot.Stand,
            slot.OffBlockUtc,
            slot.OnBlockUtc,
            slot.DepartureIcao,
            slot.ArrivalIcao,
            slot.RotationCode,
            slot.RotationLeg,
            PairedSlotId: null)
        : new BookingExportDto(
            slot.Id,
            Callsign: null,
            FlightNumber: null,
            BookedBy: null,
            AircraftIcao: null,
            Gate: null,
            slot.IsArrival ? null : slot.OffBlockUtc,
            slot.IsArrival ? slot.OnBlockUtc : null,
            slot.IsArrival ? null : slot.EventAirportIcao,
            slot.IsArrival ? slot.EventAirportIcao : null,
            Rotation: null,
            Leg: null,
            PairedSlotId: null);

    private static IResult Problem(int status, string titleKey, string? code, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(
            statusCode: status,
            title: catalog.Resolve(currentUser.Locale, titleKey),
            extensions: code is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });
}
