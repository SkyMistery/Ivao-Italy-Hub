using System.Net;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
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
    /// <summary>The most airports the block counts for a screen, as `networkStats` asks them.</summary>
    private const int Limit = DataBlockScope.MaxItems;

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
        var rome = IvaoAirspace.OfAirports(["LIRF", "LIRA"], Limit);
        var milan = IvaoAirspace.OfAirports(["LIMC", "LIML"], Limit);

        Assert.NotEqual(rome.CacheKey, milan.CacheKey);
        Assert.Equal("0/2/LIRA,LIRF", rome.CacheKey);

        // The same airports are the same airspace however a screen writes them.
        Assert.Equal(rome.CacheKey, IvaoAirspace.OfAirports([" lira", "LIRF", "lirf "], Limit).CacheKey);
    }

    [Fact]
    public void ACommaNeverMakesTwoSetsOneKey()
    {
        // The key joins the airports with a comma, so a code carrying one would make two sets read the
        // same: "A,B" with "C", and "A" with "B,C", are both "A,B,C" (review of E4b, #226, point 4). A
        // code is letters and digits only, so neither comma survives.
        var first = IvaoAirspace.OfAirports(["A,B", "C"], Limit);
        var second = IvaoAirspace.OfAirports(["A", "B,C"], Limit);

        Assert.NotEqual(first.CacheKey, second.CacheKey);
        Assert.Equal(["C"], first.Airports.Order(StringComparer.Ordinal));
        Assert.Equal(["A"], second.Airports.Order(StringComparer.Ordinal));
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
        // As the snapshot writes an airport: trimmed, upper case, once, no wider than
        // `ref_ivao_airports.icao`, and letters and digits only. Anything else names no airport anybody
        // flies to: a longer code, a callsign, a code with a sign or a letter from outside the alphabet.
        var airspace = IvaoAirspace.OfAirports(
            ["lirf", " LIRF ", "", "   ", "LIRF_TWR", "NOT-AN-AIRPORT", "LI-F", "LI F", "LIRÉ", "K1G4"],
            Limit);

        Assert.Equal(["K1G4", "LIRF"], airspace.Airports.Order(StringComparer.Ordinal));
        Assert.Empty(airspace.Centers);
    }

    [Fact]
    public void TheCeilingOfTheAirportsIsCountedAfterTheCleaning()
    {
        // Fifty entries that are no airport, and one airport asked over and over, never push a real one
        // out: the ceiling counts what is left after the cleaning, in the order asked (review of E4b,
        // #226, point 3).
        var junkFirst = IvaoAirspace.OfAirports([.. Enumerable.Repeat("NOT-AN-AIRPORT", Limit), "EDDF"], Limit);
        Assert.Equal(["EDDF"], junkFirst.Airports.Order(StringComparer.Ordinal));

        var askedTwice = IvaoAirspace.OfAirports([.. Enumerable.Repeat("lirf", Limit), "EDDF"], 2);
        Assert.Equal(["EDDF", "LIRF"], askedTwice.Airports.Order(StringComparer.Ordinal));

        // And past the ceiling, the first ones asked are the ones kept.
        var many = IvaoAirspace.OfAirports([.. Enumerable.Range(0, Limit + 10).Select(index => $"Q{index:000}")], Limit);
        Assert.Equal(Limit, many.Airports.Count);
        Assert.Contains("Q000", many.Airports);
        Assert.Contains($"Q{Limit - 1:000}", many.Airports);
        Assert.DoesNotContain($"Q{Limit:000}", many.Airports);
    }

    [Fact]
    public void AnAirspaceOfAirportsCountsOnlyThoseAirports()
    {
        // The two rules of the division, on another space: a controller whose station is one of the
        // airports — the tower and the approach of Rome, not the centre above it —, a flight that starts
        // or ends at one of them.
        using var document = JsonDocument.Parse(Whazzup);
        var picture = IvaoWhazzup.Read(document.RootElement);

        var rome = picture.For(IvaoAirspace.OfAirports(["LIRF"], Limit));

        Assert.Equal(["LIRF_APP", "LIRF_TWR"], rome.Positions.Select(position => position.Callsign));
        Assert.Equal(2, rome.AreaAtc);
        Assert.Equal(2, rome.AreaPilots);

        // The network's totals are the network's, whatever airspace asks.
        Assert.Equal(4, rome.NetworkAtc);
        Assert.Equal(5, rome.NetworkPilots);
        Assert.Equal(new DateTime(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc), rome.UpdatedAt);

        // Frankfurt is nobody's division here, and counts all the same when a screen asks for it.
        var frankfurt = picture.For(IvaoAirspace.OfAirports(["EDDF"], Limit));
        Assert.Equal(["EDDF_TWR"], frankfurt.Positions.Select(position => position.Callsign));
        Assert.Equal(2, frankfurt.AreaPilots);

        // A list with no airport in it counts nobody.
        var nowhere = picture.For(IvaoAirspace.OfAirports([], Limit));
        Assert.Equal(0, nowhere.AreaAtc);
        Assert.Equal(0, nowhere.AreaPilots);
        Assert.Equal(4, nowhere.NetworkAtc);
    }

    [Fact]
    public void AReadingKeepsTheAnswersOfAFewAirspacesAndCountsTheRestEveryTime()
    {
        // The block is anonymous and a screen names the airports, so a caller can invent a set of them
        // per request: a reading keeps the answers of the first few airspaces and counts every other one
        // each time it asks, so nothing anybody invents grows what it holds (review of E4b, #226, point 2).
        using var document = JsonDocument.Parse(Whazzup);
        var picture = IvaoWhazzup.Read(document.RootElement);

        var kept = picture.For(Division());

        // Ten times as many sets as a reading keeps, each a code of its own (Q000, Q001…).
        var inventedSets = IvaoNetworkPicture.MaxKeptAnswers * 10;
        for (var invented = 0; invented < inventedSets; invented++)
        {
            picture.For(IvaoAirspace.OfAirports([$"Q{invented:000}"], Limit));
        }

        // The first airspaces asked keep their answer: the same one, counted once.
        Assert.Same(kept, picture.For(Division()));
        var firstInvented = IvaoAirspace.OfAirports(["Q000"], Limit);
        Assert.Same(picture.For(firstInvented), picture.For(firstInvented));

        // Past the ceiling an airspace is counted every time it asks — right, and never kept.
        var rome = IvaoAirspace.OfAirports(["LIRF"], Limit);
        var once = picture.For(rome);
        var again = picture.For(rome);

        Assert.NotSame(once, again);
        Assert.Equal(["LIRF_APP", "LIRF_TWR"], again.Positions.Select(position => position.Callsign));
        Assert.Equal(2, again.AreaPilots);

        // Nor does the last one invented find an answer kept for it.
        var lastInvented = IvaoAirspace.OfAirports([$"Q{inventedSets - 1:000}"], Limit);
        Assert.NotSame(picture.For(lastInvented), picture.For(lastInvented));
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
        var rome = await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports(["LIRF"], Limit), token);
        var milan = await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports(["LIMC"], Limit), token);

        for (var invented = 0; invented < 20; invented++)
        {
            await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports([$"X{invented:000}"], Limit), token);
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

        // And the same airspace asked again is the same answer, counted once: the three were among the
        // first airspaces the reading kept an answer for.
        Assert.Same(rome, await client.GetNetworkStatusAsync(IvaoAirspace.OfAirports(["lirf"], Limit), token));
        Assert.Same(division, await client.GetNetworkStatusAsync(Division(), token));
    }
}
