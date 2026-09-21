using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The airports of the world the hub knows, as a question a module can ask without knowing where they come from
/// (M2, T7): "where are these airports?" to freeze the ends of a leg and measure it, and "which airports start like
/// this?" for the field that offers them while somebody types. The same shape as <see cref="IAircraftTypeDirectory"/>.
/// </summary>
public interface IAirportDirectory
{
    /// <summary>Up to <paramref name="limit"/> airports whose ICAO or IATA code starts with what is typed, or whose name contains it.</summary>
    Task<IReadOnlyList<AirportDto>> SearchAsync(string? text, int limit, CancellationToken cancellationToken = default);

    /// <summary>The airports of the list the hub knows, by upper case ICAO code; an unknown code is simply absent.</summary>
    Task<IReadOnlyDictionary<string, AirportDto>> FindAsync(IReadOnlyCollection<string> icaos, CancellationToken cancellationToken = default);

    /// <summary>
    /// The countries of the list that have at least one airport the hub knows, by upper case code — the <c>CountryId</c> of an
    /// airport. A parameter naming a country is checked here as one naming an airport is checked by <see cref="FindAsync"/>.
    /// </summary>
    Task<IReadOnlySet<string>> KnownCountriesAsync(IReadOnlyCollection<string> countryIds, CancellationToken cancellationToken = default);
}

/// <summary>One airport, as a field offers it and a leg freezes it. Every airport of the snapshot has coordinates (T1).</summary>
public sealed record AirportDto(
    string Icao,
    string? Iata,
    string Name,
    string CountryId,
    double? Latitude,
    double? Longitude,
    int? ElevationFeet);

internal sealed class AirportDirectory(HubDbContext database) : IAirportDirectory
{
    public async Task<IReadOnlyList<AirportDto>> SearchAsync(string? text, int limit, CancellationToken cancellationToken = default)
    {
        var query = database.IvaoAirports.AsNoTracking();
        var typed = text?.Trim().ToUpperInvariant() ?? string.Empty;

        if (typed.Length > 0)
        {
            query = query.Where(airport =>
                airport.Icao.StartsWith(typed) || airport.Iata == typed || airport.Name.Contains(typed));
        }

        return await query
            .OrderBy(airport => airport.Icao.StartsWith(typed) ? 0 : airport.Iata == typed ? 1 : 2)
            .ThenBy(airport => airport.Icao)
            .Take(limit)
            .Select(airport => new AirportDto(
                airport.Icao,
                airport.Iata,
                airport.Name,
                airport.CountryId,
                airport.Latitude,
                airport.Longitude,
                airport.ElevationFeet))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, AirportDto>> FindAsync(
        IReadOnlyCollection<string> icaos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(icaos);

        var wanted = icaos
            .Where(icao => !string.IsNullOrWhiteSpace(icao))
            .Select(icao => icao.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (wanted.Length == 0)
        {
            return new Dictionary<string, AirportDto>(StringComparer.Ordinal);
        }

        return await database.IvaoAirports.AsNoTracking()
            .Where(airport => wanted.Contains(airport.Icao))
            .Select(airport => new AirportDto(
                airport.Icao,
                airport.Iata,
                airport.Name,
                airport.CountryId,
                airport.Latitude,
                airport.Longitude,
                airport.ElevationFeet))
            .ToDictionaryAsync(airport => airport.Icao, StringComparer.Ordinal, cancellationToken);
    }

    public async Task<IReadOnlySet<string>> KnownCountriesAsync(
        IReadOnlyCollection<string> countryIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryIds);

        var wanted = countryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (wanted.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var known = await database.IvaoAirports.AsNoTracking()
            .Where(airport => wanted.Contains(airport.CountryId))
            .Select(airport => airport.CountryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return known.ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary><c>/api/reference/airports</c>: what an airport field offers while somebody types.</summary>
public static class AirportEndpoints
{
    public const string Pattern = "/api/reference/airports";

    /// <summary>A page of suggestions, not a list to scroll: the field asks again as the text changes.</summary>
    private const int Limit = 20;

    public static IEndpointRouteBuilder MapAirportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async Task<Ok<IReadOnlyList<AirportDto>>> (
            string? q,
            IAirportDirectory airports,
            HttpContext http) =>
            TypedResults.Ok(await airports.SearchAsync(q, Limit, http.RequestAborted)))
        .WithName("Airports")
        .WithTags("Reference")
        .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }
}
