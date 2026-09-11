using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The airspace of the division for the single page application: the airports and the centres of
/// the snapshot, with their names, so that an operational document chooses its ICAO and its FIR
/// from a list rather than typing them (implementation plan, G14). One of the five boxes of
/// va.ivao.aero's panel where a typing mistake does not show is now a choice.
/// <para>⚠️ An endpoint written by hand, and counted as such in the pull request: the CRUD engine is
/// for the rows of a department, and this is the picture of an API, read from the same cache the
/// login reads (<see cref="IFirDirectory"/>). Inside the IVAO perimeter (CLAUDE.md section 3), and
/// the field that consumes it on the other side is a generic list of suggestions.</para>
/// </summary>
public static class AirspaceEndpoints
{
    public const string Pattern = "/api/ref/airspace";

    public static IEndpointRouteBuilder MapAirspaceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async (IFirDirectory directory, HttpContext http) =>
                TypedResults.Ok(await directory.GetListingAsync(http.RequestAborted)))
            .WithName("AirspaceListing")
            .Produces<AirspaceListingDto>()
            // The staff writes documents; a visitor never needs the list. Any signed in member may
            // read it: it is the same public snapshot the live status is drawn from.
            .RequireAuthorization();

        return app;
    }
}

/// <summary>An airport or a centre, as a list offers it: the code, and the name beside it.</summary>
public sealed record AirspaceEntryDto(string Code, string Name);

/// <summary>The airports and the centres of the division, sorted by code.</summary>
public sealed record AirspaceListingDto(
    IReadOnlyList<AirspaceEntryDto> Airports,
    IReadOnlyList<AirspaceEntryDto> Centers);
