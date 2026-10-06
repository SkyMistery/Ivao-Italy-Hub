using System.Net;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// Who is online at the airports a screen asks about (M4, E4b; note
/// <c>2026-10-06-chi-e-online-sugli-scali-nel-nucleo</c>): an airspace of airports and nothing else,
/// a key that names them, and one reading of the network a minute for every airspace that asks —
/// never one per airspace, because a screen may name any airports it likes.
/// </summary>
public sealed class LiveStatusAirportsTests
{
    /// <summary>
    /// A network of an evening, written to tell the airspaces apart: a centre and two airports of one
    /// FIR, an airport elsewhere, and flights between them.
    /// </summary>
    private const string Whazzup = """
        {
          "updatedAt": "2026-10-06T16:00:00.000Z",
          "connections": { "atc": 4, "pilot": 5 },
          "clients": {
            "atcs": [
              { "callsign": "LIRR_CTR", "atcSession": { "frequency": 129.075 } },
              { "callsign": "LIRF_TWR", "atcSession": { "frequency": 118.7 } },
              { "callsign": "LIRF_APP", "atcSession": { "frequency": 119.2 } },
              { "callsign": "EDDF_TWR", "atcSession": { "frequency": 119.9 } }
            ],
            "pilots": [
              { "callsign": "IVA1", "flightPlan": { "departureId": "LIRF", "arrivalId": "LIMC" } },
              { "callsign": "IVA2", "flightPlan": { "departureId": "EDDF", "arrivalId": "LIRF" } },
              { "callsign": "IVA3", "flightPlan": { "departureId": "EDDF", "arrivalId": "EGLL" } },
              { "callsign": "IVA4", "flightPlan": { "departureId": "limc", "arrivalId": null } },
              { "callsign": "IVA5", "flightPlan": null }
            ]
          }
        }
        """;

    /// <summary>An IVAO that answers the same evening every time, and counts how often it is asked.</summary>
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Whazzup, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static IvaoApiClient Create(Handler handler, MemoryCache cache)
    {
        var options = Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" });

        return new IvaoApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.org") },
            new IvaoApiTokenProvider(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.example.org") },
                options,
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            NullLogger<IvaoApiClient>.Instance);
    }

    /// <summary>A division as the snapshot builds it: one centre and its two airports.</summary>
    private static IvaoAirspace Division() => new(
        new HashSet<string>(["LIRR"], StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(["LIRF", "LIMC"], StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void TwoSetsOfAirportsNeverShareAKey()
    {
        // Before E4b the key named only the centres and the two counts, so any two sets of two airports
        // without a centre were "0/2/": the second event to ask would have read the first one's answer
        // for a minute.
        var rome = IvaoAirspace.OfAirports(["LIRF", "LIRA"]);
        var milan = IvaoAirspace.OfAirports(["LIMC", "LIML"]);

        Assert.NotEqual(rome.CacheKey, milan.CacheKey);
        Assert.Equal("0/2/LIRA,LIRF", rome.CacheKey);

        // The same airports are the same airspace however a screen writes them.
        Assert.Equal(rome.CacheKey, IvaoAirspace.OfAirports([" lira", "LIRF", "lirf "]).CacheKey);
    }

    [Fact]
    public void TheDivisionKeepsTheKeyItHad()
    {
        // The centres and the two counts, as before: the division's answer is cached under the same
        // key it always was, and nothing of it is recomputed because events can now ask too.
        var division = new IvaoAirspace(
            new HashSet<string>(["LIRR", "LIMM"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["LIRF", "LIMC", "LIML"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal("2/3/LIMM,LIRR", division.CacheKey);

        // And an installation with no snapshot yet is still the empty airspace it was.
        Assert.Equal("0/0/", IvaoAirspace.Empty.CacheKey);
    }

    [Fact]
    public void OnlyCodesAnAirportCanHaveAreKept()
    {
        // As the snapshot writes an airport: trimmed, upper case, once, and no wider than
        // `ref_ivao_airports.icao`. A longer code names no airport anybody flies to.
        var airspace = IvaoAirspace.OfAirports(["lirf", " LIRF ", "", "   ", "LIRF_TWR", "NOT-AN-AIRPORT", "K1G4"]);

        Assert.Equal(["K1G4", "LIRF"], airspace.Airports.Order(StringComparer.Ordinal));
        Assert.Empty(airspace.Centers);
    }

    [Fact]
    public void AnAirspaceOfAirportsCountsOnlyThoseAirports()
    {
        // The two rules of the division, on another space: a controller whose station is one of the
        // airports — the tower and the approach of Rome, not the centre above it —, a flight that starts
        // or ends at one of them.
        using var document = JsonDocument.Parse(Whazzup);
        var picture = IvaoWhazzup.Read(document.RootElement);

        var rome = picture.For(IvaoAirspace.OfAirports(["LIRF"]));

        Assert.Equal(["LIRF_APP", "LIRF_TWR"], rome.Positions.Select(position => position.Callsign));
        Assert.Equal(2, rome.AreaAtc);
        Assert.Equal(2, rome.AreaPilots);

        // The network's totals are the network's, whatever airspace asks.
        Assert.Equal(4, rome.NetworkAtc);
        Assert.Equal(5, rome.NetworkPilots);
        Assert.Equal(new DateTime(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc), rome.UpdatedAt);

        // Frankfurt is nobody's division here, and counts all the same when a screen asks for it.
        var frankfurt = picture.For(IvaoAirspace.OfAirports(["EDDF"]));
        Assert.Equal(["EDDF_TWR"], frankfurt.Positions.Select(position => position.Callsign));
        Assert.Equal(2, frankfurt.AreaPilots);

        // A list with no airport in it counts nobody.
        var nowhere = picture.For(IvaoAirspace.OfAirports([]));
        Assert.Equal(0, nowhere.AreaAtc);
        Assert.Equal(0, nowhere.AreaPilots);
        Assert.Equal(4, nowhere.NetworkAtc);
    }

    [Fact]
    public async Task OneReadingAMinuteAnswersEveryAirspace()
    {
        // The measure of E4b: a screen chooses the airports, and the block is anonymous, so the sets
        // are as many as anybody cares to invent. One reading of the network a minute answers all of
        // them — the division and every set of airports —, each counted by its own rule.
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var handler = new Handler();
        var client = Create(handler, cache);

        var division = await client.GetNetworkStatusAsync(Division(), token);
        var rome = await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports(["LIRF"]), token);
        var milan = await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports(["LIMC"]), token);

        for (var invented = 0; invented < 20; invented++)
        {
            await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports([$"X{invented:000}"]), token);
        }

        Assert.Equal(1, handler.Calls);

        // The division as it was: the centre and the two airports of its FIR, and the flights that
        // touch them.
        Assert.Equal(["LIRF_APP", "LIRF_TWR", "LIRR_CTR"], division.Positions.Select(position => position.Callsign));
        Assert.Equal(3, division.AreaPilots);

        Assert.Equal(2, rome.AreaAtc);
        Assert.Equal(2, rome.AreaPilots);

        // Milan has no controller online, and two flights touch it — one written in lower case.
        Assert.Equal(0, milan.AreaAtc);
        Assert.Equal(2, milan.AreaPilots);

        // And the same airspace asked again is the same answer, counted once.
        Assert.Same(rome, await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports(["lirf"]), token));
        Assert.Same(division, await client.GetNetworkStatusAsync(Division(), token));
    }
}
