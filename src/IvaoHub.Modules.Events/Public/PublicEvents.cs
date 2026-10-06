using System.Security.Claims;
using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Public;

/// <summary>An airport as the page of an event names it: its ICAO, and the name the core knows — none for one it no longer knows.</summary>
public sealed record PublicEventAirportDto(string Icao, string? Name);

/// <summary>A route of an event as its page shows it (design M4 §1.4): the two airports, the route to file, the remarks.</summary>
public sealed record PublicEventRouteDto(
    long Id,
    PublicEventAirportDto Departure,
    PublicEventAirportDto Arrival,
    string Route,
    Localized<string>? Remarks);

/// <summary>
/// An event as a card of <c>/events</c> and of the block <c>events.eventList</c> shows it (design M4 §7.1, §7.3): what fits on a
/// tile, the same for whoever is looking. <c>Airports</c> are its own, in their order; an event of the whole division has none.
/// </summary>
public sealed record PublicEventCardDto(
    long Id,
    string Slug,
    string Kind,
    Localized<string> Title,
    Localized<string> Summary,
    long? BannerMediaId,
    EventStateKind State,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool WholeDivision,
    IReadOnlyList<string> Airports);

/// <summary>
/// An event as its page shows it (design M4 §7.1, E4): the banner, the title, when — in UTC, as every moment the hub keeps —, the
/// kind, who organises it, the airports, the routes and the description; a cancelled one with its note.
/// <para><c>Seen</c> is false only to the staff of the events, who read the page of an event in every state — a draft, one not seen
/// yet, one that ended —, and the page tells them that nobody else does.</para>
/// </summary>
public sealed record PublicEventDto(
    long Id,
    string Slug,
    string Kind,
    EventOrganizer Organizer,
    string? ExternalUrl,
    Localized<string> Title,
    Localized<string> Summary,
    JsonNode Body,
    long? BannerMediaId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    EventStateKind State,
    bool Seen,
    bool WholeDivision,
    IReadOnlyList<PublicEventAirportDto> Airports,
    IReadOnlyList<PublicEventRouteDto> Routes,
    DateTime? CancelledAt,
    Localized<string>? CancellationNote);

/// <summary>
/// The events as the site shows them (design M4 §7.1, §7.3, E4): the cards of the events to come and of those in progress, for
/// <c>/events</c> and the block <c>events.eventList</c>, and one event by its address, for its page — where its calendar entry and
/// its line in the search already point (E3b). Composed once here, as the tours' public side is.
/// <para>What the public sees is <see cref="EventState.IsSeen"/> and nothing else, read through the global query filter — published,
/// and for everybody or, for one of the members, for a signed in reader —: from its «seen from», or its publication, to its end,
/// cancelled or not. <b>No archive</b> (§2.4, c2): after its end an event is not on the list and its page answers 404 — to everybody
/// but the staff of the events, who hold <c>Events.View</c> on it and read its page in every state, through the one handler.</para>
/// </summary>
public sealed class PublicEvents(
    EventsDbContext database,
    IAirportDirectory airports,
    IAuthorizationService authorization,
    IClock clock)
{
    /// <summary>Never more cards than this, whatever is asked: the site has no archive, and a list is not an export.</summary>
    public const int MaxItems = DataBlockScope.MaxItems;

    /// <summary>
    /// The events seen now — to come and in progress, cancelled ones included until their end —, the soonest first: of the kinds
    /// named, or of every kind when none is, and at most <paramref name="limit"/> of them.
    /// </summary>
    public async Task<IReadOnlyList<PublicEventCardDto>> CardsAsync(
        IReadOnlyCollection<string> kinds,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        var now = clock.UtcNow;
        var query = database.Events.AsNoTracking().Where(EventState.Seen(now));

        if (kinds.Count > 0)
        {
            var wanted = kinds.ToArray();
            query = query.Where(row => wanted.Contains(row.Kind));
        }

        var rows = await query
            .OrderBy(row => row.StartsAtUtc)
            .ThenBy(row => row.Id)
            .Take(Math.Clamp(limit, 1, MaxItems))
            .ToListAsync(cancellationToken);

        // The airports of the page of cards in one query, not one per card.
        var ids = rows.Select(row => row.Id).ToList();
        var byEvent = (await database.Airports.AsNoTracking()
                .Where(airport => ids.Contains(airport.EventId))
                .OrderBy(airport => airport.Ordinal)
                .ThenBy(airport => airport.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(airport => airport.EventId, airport => airport.Icao);

        return
        [
            .. rows.Select(row => new PublicEventCardDto(
                row.Id,
                row.Slug,
                row.Kind,
                row.Title,
                row.Summary,
                row.BannerMediaId,
                EventState.Of(row, now),
                row.StartsAtUtc,
                row.EndsAtUtc,
                row.WholeDivision,
                [.. byEvent[row.Id]])),
        ];
    }

    /// <summary>
    /// One event by its address, with everything its page shows; null when <paramref name="reader"/> may not see it — not seen, or
    /// for the members and read by a visitor —, unless they hold <c>Events.View</c> on it.
    /// </summary>
    public async Task<PublicEventDto?> ReadAsync(string slug, ClaimsPrincipal reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var now = clock.UtcNow;
        var row = await database.Events.AsNoTracking()
            .Where(EventState.Seen(now))
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);
        var seen = row is not null;

        if (row is null)
        {
            // Not the reader's to see: the staff of the events read it all the same, a draft or an ended one included.
            var staff = await CrudSource.BackOffice<Event>(database).AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

            if (staff is null || !(await authorization.AuthorizeAsync(reader, staff, EventsPermissions.View)).Succeeded)
            {
                return null;
            }

            row = staff;
        }

        var own = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == row.Id)
            .OrderBy(airport => airport.Ordinal)
            .ThenBy(airport => airport.Id)
            .Select(airport => airport.Icao)
            .ToListAsync(cancellationToken);

        var routes = await database.Routes.AsNoTracking()
            .Where(route => route.EventId == row.Id)
            .OrderBy(route => route.DepartureIcao)
            .ThenBy(route => route.ArrivalIcao)
            .ThenBy(route => route.Id)
            .ToListAsync(cancellationToken);

        // Every airport the page names, in one question to the core.
        var known = await airports.FindAsync(
            [.. own.Concat(routes.SelectMany(route => new[] { route.DepartureIcao, route.ArrivalIcao })).Distinct(StringComparer.Ordinal)],
            cancellationToken);
        PublicEventAirportDto Named(string icao) => new(icao, known.GetValueOrDefault(icao)?.Name);

        return new PublicEventDto(
            row.Id,
            row.Slug,
            row.Kind,
            row.Organizer,
            row.ExternalUrl,
            row.Title,
            row.Summary,
            JsonNode.Parse(row.BodyJson) ?? JsonNode.Parse(Event.EmptyBody)!,
            row.BannerMediaId,
            row.StartsAtUtc,
            row.EndsAtUtc,
            EventState.Of(row, now),
            seen,
            row.WholeDivision,
            [.. own.Select(Named)],
            [
                .. routes.Select(route => new PublicEventRouteDto(
                    route.Id,
                    Named(route.DepartureIcao),
                    Named(route.ArrivalIcao),
                    route.Route,
                    route.Remarks)),
            ],
            row.CancelledAt,
            row.CancellationNote);
    }
}

/// <summary>
/// The read of the site (design M4 §7.1, E4): one event by its address, <c>/events/{slug}</c>, where its calendar entry and its line in
/// the search point. Anonymous, and answered for whoever asks. The list of <c>/events</c> is the block <c>events.eventList</c>, read
/// live like any block — as <c>/calendar</c> reads the calendar's —, so it has no address of its own.
/// </summary>
public static class PublicEventEndpoints
{
    public const string Pattern = "/api/events/public";

    public static IEndpointRouteBuilder MapPublicEventEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{Pattern}/{{slug}}", async (string slug, PublicEvents events, HttpContext http) =>
                await events.ReadAsync(slug, http.User, http.RequestAborted) is { } page ? Results.Ok(page) : Results.NotFound())
            .WithName("EventsPublicEvent")
            .WithTags("Events")
            .Produces<PublicEventDto>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return app;
    }
}
