using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// <c>networkStats</c> with the airports a screen asks about (M4, E4b; note
/// <c>2026-10-06-chi-e-online-sugli-scali-nel-nucleo</c>), asked the way the browser asks it — anonymous,
/// the properties in the address — against the recorded evening of <c>tests/fixtures/ivao/whazzup.json</c>:
/// four controllers (<c>LIRR_CTR</c>, <c>LIMC_APP</c>, <c>LIRF_TWR</c>, <c>EDDF_TWR</c>), four flights
/// (Rome–Milan, Frankfurt–Milan, Frankfurt–London, one without a plan), 210 controllers on the network.
/// <para>With airports the area is theirs and the division is not asked; without them the answer is the
/// division's, as it always was.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class NetworkStatsAirportsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);

        await using var scope = _factory.Services.CreateAsyncScope();
        await SeedDivisionAsync(scope.ServiceProvider, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheAirportsAScreenAsksAboutAreTheArea()
    {
        // Frankfurt is no airport of the division: asked by a screen, it is counted all the same, with
        // its tower and the two flights that leave it.
        var token = TestContext.Current.CancellationToken;
        using var anonymous = _factory.CreateApiClient();

        var frankfurt = await AskAsync(anonymous, ["EDDF"], token);

        Assert.Equal(1, Figure(frankfurt, NetworkStatsProvider.DivisionAtc));
        Assert.Equal(2, Figure(frankfurt, NetworkStatsProvider.DivisionPilots));
        Assert.Equal(["EDDF_TWR"], Positions(frankfurt));

        // The network is the network, whatever area a screen asks about.
        Assert.Equal(210, Figure(frankfurt, NetworkStatsProvider.NetworkAtc));
    }

    [Fact]
    public async Task AnAirportCountsItsOwnStationsAndNotTheCentreAboveIt()
    {
        // Rome Fiumicino as a screen may write it: its tower, and not the centre of its FIR, which is
        // the division's and not the airport's; one flight, the one that leaves it.
        var token = TestContext.Current.CancellationToken;
        using var anonymous = _factory.CreateApiClient();

        var rome = await AskAsync(anonymous, [" lirf "], token);

        Assert.Equal(1, Figure(rome, NetworkStatsProvider.DivisionAtc));
        Assert.Equal(1, Figure(rome, NetworkStatsProvider.DivisionPilots));
        Assert.Equal(["LIRF_TWR"], Positions(rome));
    }

    [Fact]
    public async Task AListWithNoAirportInItCountsNobodyAndNeverTheDivision()
    {
        // A list is the airports asked even when nothing in it is one. Widening it to the division
        // would draw the division's figures under the title of an event's airports.
        var token = TestContext.Current.CancellationToken;
        using var anonymous = _factory.CreateApiClient();

        foreach (var airports in new[] { Array.Empty<string>(), ["NOT-AN-AIRPORT", "   "] })
        {
            var answer = await AskAsync(anonymous, airports, token);

            Assert.Equal(0, Figure(answer, NetworkStatsProvider.DivisionAtc));
            Assert.Equal(0, Figure(answer, NetworkStatsProvider.DivisionPilots));
            Assert.Empty(Positions(answer));

            // Asked and answered: an empty area, not a network that could not be reached.
            Assert.NotEqual(JsonValueKind.Null, answer.GetProperty("updatedAt").ValueKind);
        }
    }

    [Fact]
    public async Task WithoutAirportsTheAnswerIsTheDivisionsAsItWas()
    {
        // The strip on top of every public page asks without airports, and must read what it read
        // before E4b: the division's three controllers and two flights — asked after a screen asked
        // for airports, so that one answer cannot be taken for the other.
        var token = TestContext.Current.CancellationToken;
        using var anonymous = _factory.CreateApiClient();

        await AskAsync(anonymous, ["EDDF"], token);
        var division = await AskAsync(anonymous, airports: null, token);

        Assert.Equal(3, Figure(division, NetworkStatsProvider.DivisionAtc));
        Assert.Equal(2, Figure(division, NetworkStatsProvider.DivisionPilots));
        Assert.Equal(["LIMC_APP", "LIRF_TWR", "LIRR_CTR"], Positions(division));
    }

    // ---- helpers -----------------------------------------------------------------------------

    /// <summary>
    /// The block asked live, as the strip asks it: three figures and the positions, with the airports
    /// when there are any. The properties travel base64url encoded, as the browser sends them.
    /// </summary>
    private static async Task<JsonElement> AskAsync(
        HttpClient client,
        string[]? airports,
        CancellationToken cancellationToken)
    {
        var props = new JsonObject
        {
            ["figures"] = new JsonArray(
            [
                new JsonObject { ["figure"] = NetworkStatsProvider.DivisionAtc },
                new JsonObject { ["figure"] = NetworkStatsProvider.DivisionPilots },
                new JsonObject { ["figure"] = NetworkStatsProvider.NetworkAtc },
            ]),
            ["showPositions"] = true,
        };

        if (airports is not null)
        {
            props["airports"] = new JsonArray([.. airports.Select(airport => (JsonNode?)JsonValue.Create(airport))]);
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return await client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/blocks/data/{CoreBlocks.NetworkStats}?props={encoded}", UriKind.Relative),
            cancellationToken);
    }

    private static int Figure(JsonElement answer, string figure) =>
        answer.GetProperty("figures")
            .EnumerateArray()
            .First(entry => entry.GetProperty("figure").GetString() == figure)
            .GetProperty("value")
            .GetInt32();

    private static string[] Positions(JsonElement answer) =>
        [.. answer.GetProperty("positions").EnumerateArray().Select(position => position.GetProperty("callsign").GetString()!)];

    /// <summary>
    /// The snapshot the division's answer is counted against — two centres, two airports, the ones the
    /// recorded evening is written against —, written straight in when another test has not already.
    /// </summary>
    private static async Task SeedDivisionAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<HubDbContext>();
        var clock = services.GetRequiredService<IClock>();

        foreach (var (id, name) in new[] { ("LIRR", "Roma"), ("LIMM", "Milano") })
        {
            if (!await database.IvaoCenters.AnyAsync(center => center.Id == id, cancellationToken))
            {
                database.IvaoCenters.Add(new IvaoCenter { Id = id, Name = name, CountryId = "IT", SyncedAt = clock.UtcNow });
            }
        }

        foreach (var icao in new[] { "LIRF", "LIMC" })
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport { Icao = icao, Name = icao, CountryId = "IT", SyncedAt = clock.UtcNow });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        services.GetRequiredService<IFirDirectory>().Invalidate();
    }
}
