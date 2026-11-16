using IvaoHub.Core.Auth.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// <c>/api/atc/positions</c>: the ATC positions of the division, with their frequencies, for whoever writes content — what the
/// frequency table of a page offers while it is written, and what the editor compares its rows with to say which one IVAO
/// now spells otherwise (design of the editor's phase 3, §5.8; the maintainer's answer 6 of that design: the values are
/// copied into the block, never read live). The positions are IVAO's data, so the read lives in its perimeter (plan §4.2).
/// <para>⚠️ A hand written read, counted in the plan's §16.6: not a list of the CRUD engine, because the rows are a snapshot
/// of IVAO nobody writes, and they are the division's by the airspace (<see cref="IAtcPositionDirectory"/>), not by a
/// department.</para>
/// </summary>
public static class DivisionAtcPositionEndpoints
{
    public const string Pattern = "/api/atc/positions";

    public static IEndpointRouteBuilder MapDivisionAtcPositionEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Answered whole, and narrowed here: the division's list is a couple of hundred rows (232 in Italy on 25 September
        // 2026), and the editor reads all of it once — it needs the rows the block already names to compare them, and the
        // field filters what it offers as somebody types.
        app.MapGet(Pattern, async Task<Ok<IReadOnlyList<AtcPositionDto>>> (
            string? q,
            IAtcPositionDirectory positions,
            HttpContext http) =>
        {
            var division = await positions.OfDivisionAsync(http.RequestAborted);
            var text = q?.Trim();

            return TypedResults.Ok<IReadOnlyList<AtcPositionDto>>(string.IsNullOrEmpty(text)
                ? division
                : [.. division.Where(position =>
                    position.Callsign.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || position.Name.Contains(text, StringComparison.OrdinalIgnoreCase))]);
        })
        .WithName("DivisionAtcPositions")
        .WithTags("Reference")
        .RequireAuthorization(CorePermissions.ContentEdit);

        return app;
    }
}
