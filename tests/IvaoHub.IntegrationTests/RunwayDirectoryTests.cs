using System.Net;
using System.Text;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The runways fetched on demand (issue #324): <see cref="RunwayDirectory.EnsureAsync"/> never throws, because a report
/// already saved calls it, and an exception there was a 500 over a report the pilot could no longer send again. Through
/// the real IVAO client in front of an IVAO answered here, on the real database: IVAO down, a designator IVAO lists twice,
/// and two reports to the same airport at once, the second finding the first one's rows when it saves.
/// <para>The airports are of region X, which no ICAO code uses, so nobody else's runways are touched.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class RunwayDirectoryTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string Down = "XRW1";
    private const string Twice = "XRW2";
    private const string Raced = "XRW3";

    private static readonly string[] Airports = [Down, Twice, Raced];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        await RemoveAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await RemoveAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AnAirportIvaoCannotAnswerForStaysWithoutRunwaysAndNothingThrows()
    {
        var token = TestContext.Current.CancellationToken;
        using var ivao = new Ivao((_, _) => throw new HttpRequestException("No such host is known."));

        Assert.Equal(0, await EnsureAsync(ivao, Down, token));
        Assert.Empty(await RunwaysAsync(Down, token));
        Assert.Contains($"/v2/airports/{Down}/runways", ivao.Asked);
    }

    [Fact]
    public async Task ADesignatorIvaoListsTwiceIsOneRunway()
    {
        var token = TestContext.Current.CancellationToken;
        using var ivao = new Ivao((_, _) => Task.FromResult(Json(Runways(Twice, "RW07", "RW07", "RW25"))));

        Assert.Equal(2, await EnsureAsync(ivao, Twice, token));
        Assert.Equal(["RW07", "RW25"], (await RunwaysAsync(Twice, token)).Select(runway => runway.Designator));
    }

    [Fact]
    public async Task TheSecondOfTwoReportsAtOnceLetsTheFirstOnesRunwaysStand()
    {
        // The other report found the airport without runways at the same moment, fetched them, and saved first: here that
        // happens while this one is still waiting on IVAO, so the moment is the same every run.
        var token = TestContext.Current.CancellationToken;
        using var ivao = new Ivao(async (_, cancellationToken) =>
        {
            await using var other = _factory.Services.CreateAsyncScope();
            var database = other.ServiceProvider.GetRequiredService<HubDbContext>();
            database.IvaoRunways.AddRange(
                new IvaoRunway { AirportIcao = Raced, Designator = "RW16", SyncedAt = DateTime.UtcNow },
                new IvaoRunway { AirportIcao = Raced, Designator = "RW34", SyncedAt = DateTime.UtcNow });
            await database.SaveChangesAsync(cancellationToken);

            return Json(Runways(Raced, "RW16", "RW34"));
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var directory = Directory(scope, ivao);

        Assert.Equal(0, await directory.EnsureAsync([Raced], token));
        Assert.Equal(["RW16", "RW34"], (await directory.GetAsync(Raced, token)).Select(runway => runway.Designator));

        // What the caller does next on the same context still saves: the rows it let go are no longer tracked.
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.Empty(database.ChangeTracker.Entries<IvaoRunway>());
        await database.SaveChangesAsync(token);
    }

    /// <summary>An IVAO that gives its token, and answers every other call with the function it is given.</summary>
    private sealed class Ivao(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/v2/oauth/token")
            {
                return Task.FromResult(Json("{ \"access_token\": \"token\", \"expires_in\": 3600 }"));
            }

            Asked.Add(request.RequestUri.AbsolutePath);
            return answer(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Runways(string icao, params string[] designators) =>
        $"[{string.Join(',', designators.Select(designator =>
            $"{{ \"airportIcao\": \"{icao}\", \"runway\": \"{designator}\", \"length\": 3000, \"bearing\": 70 }}"))}]";

    private async Task<int> EnsureAsync(Ivao ivao, string icao, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await Directory(scope, ivao).EnsureAsync([icao], cancellationToken);
    }

    /// <summary>The directory of the host, with the real IVAO client in front of the IVAO of the test.</summary>
    private static RunwayDirectory Directory(AsyncServiceScope scope, Ivao ivao)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new IvaoApiClient(
            new HttpClient(ivao, disposeHandler: false) { BaseAddress = new Uri("https://api.example.org") },
            new IvaoApiTokenProvider(
                new HttpClient(ivao, disposeHandler: false) { BaseAddress = new Uri("https://api.example.org") },
                Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" }),
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            IvaoNetworkPictureStore.None,
            NullLogger<IvaoApiClient>.Instance);

        return new RunwayDirectory(
            scope.ServiceProvider.GetRequiredService<HubDbContext>(),
            client,
            scope.ServiceProvider.GetRequiredService<IClock>(),
            NullLogger<RunwayDirectory>.Instance);
    }

    private async Task<List<IvaoRunway>> RunwaysAsync(string icao, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().IvaoRunways.AsNoTracking()
            .Where(runway => runway.AirportIcao == icao)
            .OrderBy(runway => runway.Designator)
            .ToListAsync(cancellationToken);
    }

    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().IvaoRunways
            .Where(runway => Airports.Contains(runway.AirportIcao))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
