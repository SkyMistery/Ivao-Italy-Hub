using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Legs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Airports the tests of the tours own in the shared database (M2, T7a): a leg needs both of its airports in the core's
/// snapshot, and a tour needs a leg to be ready. X is no ICAO region, so nobody else's airport is touched; the
/// coordinates are Rome, Milan and London's, so the distances are real ones. A class that uses them runs its host
/// with the IVAO fixtures, because writing a leg fetches the runways of its airports.
/// </summary>
internal static class FoTestAirports
{
    public const string Rome = "XFA1";
    public const string Milan = "XFA2";
    public const string London = "XFA3";

    private static readonly (string Icao, string Iata, double Latitude, double Longitude)[] All =
    [
        (Rome, "XA1", 41.8002777778, 12.2388888889),
        (Milan, "XA2", 45.4451, 9.27674),
        (London, "XA3", 51.470748, -0.459909),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var (icao, iata, latitude, longitude) in All)
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport
                {
                    Icao = icao,
                    Iata = iata,
                    Name = $"fo-test {icao}",
                    CountryId = "XX",
                    Latitude = latitude,
                    Longitude = longitude,
                    SyncedAt = clock.UtcNow,
                });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Gone once no class needs them; a class that still does seeds them again.</summary>
    public static async Task RemoveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var codes = All.Select(airport => airport.Icao).ToArray();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().IvaoAirports
            .Where(airport => codes.Contains(airport.Icao))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public static string LegsUri(long tourId) =>
        LegEndpoints.Pattern.Replace("{tourId:long}", tourId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    public static LegWriteDto Leg(string departure, string arrival) =>
        new(departure, arrival, Callsigns: null, FlightNumbers: null, Aircraft: null, ReleaseAt: null, ChangeReason: null, RowVersion: default);

    /// <summary>One leg at the end of the tour, through the editor's own verb.</summary>
    public static async Task AddLegAsync(HttpClient client, long tourId, CancellationToken cancellationToken, string departure = Rome, string arrival = Milan)
    {
        using var response = await client.PostAsJsonAsync(LegsUri(tourId), Leg(departure, arrival), cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
    }
}
