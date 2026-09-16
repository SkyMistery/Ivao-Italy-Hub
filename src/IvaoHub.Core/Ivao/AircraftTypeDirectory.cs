using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The aircraft types the hub knows, as a question a module can ask without knowing where they come from
/// (M2, T5): "which of these codes is not a type?" to refuse a profile or a group, and "which types start
/// like this?" for the field that offers them while somebody types.
/// </summary>
public interface IAircraftTypeDirectory
{
    /// <summary>Up to <paramref name="limit"/> types whose code or model contains what is typed, codes first.</summary>
    Task<IReadOnlyList<AircraftTypeDto>> SearchAsync(string? text, int limit, CancellationToken cancellationToken = default);

    /// <summary>The codes of the list that are not a known type, in the order they were given.</summary>
    Task<IReadOnlyList<string>> UnknownAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);
}

/// <summary>One aircraft type, as a field offers it.</summary>
public sealed record AircraftTypeDto(string IcaoCode, string? Manufacturer, string Model);

internal sealed class AircraftTypeDirectory(HubDbContext database) : IAircraftTypeDirectory
{
    public async Task<IReadOnlyList<AircraftTypeDto>> SearchAsync(string? text, int limit, CancellationToken cancellationToken = default)
    {
        var query = database.IvaoAircraftTypes.AsNoTracking();
        var typed = text?.Trim() ?? string.Empty;

        if (typed.Length > 0)
        {
            query = query.Where(type => type.IcaoCode.StartsWith(typed) || type.Model.Contains(typed));
        }

        return await query
            .OrderBy(type => type.IcaoCode.StartsWith(typed) ? 0 : 1)
            .ThenBy(type => type.IcaoCode)
            .Take(limit)
            .Select(type => new AircraftTypeDto(type.IcaoCode, type.Manufacturer, type.Model))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> UnknownAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codes);

        if (codes.Count == 0)
        {
            return [];
        }

        var known = await database.IvaoAircraftTypes.AsNoTracking()
            .Where(type => codes.Contains(type.IcaoCode))
            .Select(type => type.IcaoCode)
            .ToListAsync(cancellationToken);

        return [.. codes.Where(code => !known.Contains(code, StringComparer.OrdinalIgnoreCase))];
    }
}

/// <summary><c>/api/reference/aircraft-types</c>: what the aircraft type field offers while somebody types.</summary>
public static class AircraftTypeEndpoints
{
    public const string Pattern = "/api/reference/aircraft-types";

    /// <summary>A page of suggestions, not a list to scroll: the field asks again as the text changes.</summary>
    private const int Limit = 20;

    public static IEndpointRouteBuilder MapAircraftTypeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async Task<Ok<IReadOnlyList<AircraftTypeDto>>> (
            string? q,
            IAircraftTypeDirectory types,
            HttpContext http) =>
            TypedResults.Ok(await types.SearchAsync(q, Limit, http.RequestAborted)))
        .WithName("AircraftTypes")
        .WithTags("Reference")
        .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }
}
