using System.Net;
using System.Text;
using IvaoHub.Core.Airspace;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Core.Weather;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// An outside service that cannot be reached is no answer, never an exception (issues #324 and #325, item 1). The IVAO
/// client promises null or an empty list on every call, and before #324 four of its calls threw instead: a PIREP already
/// saved answered 500 when IVAO did not, and the weather run failed when its IVAO link did. The weather and the FIR
/// outlines read the transport's failures as no answer, and let the resilience handler's own through: its total timeout
/// fires before NOAA's, and an open circuit answers without calling at all.
/// <para>Every way of failing is here, because each fails in another place: an address nobody answers, a call cut by the
/// client's timeout, a 200 that is not JSON, and the two exceptions of the standard resilience handler.</para>
/// </summary>
public sealed class OutboundFailureTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string> Failures => ["unreachable", "timeout", "not json", "total timeout", "open circuit"];

    /// <summary>The answer of a service that fails the way the test says.</summary>
    private static async Task<HttpResponseMessage> Fail(string failure, CancellationToken cancellationToken)
    {
        switch (failure)
        {
            case "unreachable":
                throw new HttpRequestException("No such host is known.");
            case "timeout":
                // Cut by the client's own timeout, which is a TaskCanceledException that is not the caller's.
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("never");
            case "not json":
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<html>maintenance</html>", Encoding.UTF8, "text/html"),
                };
            case "total timeout":
                throw new TimeoutRejectedException();
            case "open circuit":
                throw new BrokenCircuitException();
            default:
                throw new ArgumentOutOfRangeException(nameof(failure), failure, null);
        }
    }

    /// <summary>A service that answers by the function it is given, and counts what it was asked.</summary>
    private sealed class Service(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!.PathAndQuery);
            return answer(request, cancellationToken);
        }
    }

    /// <summary>An IVAO that gives its token and fails every other call; or fails the token too.</summary>
    private static Service Ivao(string failure, bool tokenToo = false) => new((request, cancellationToken) =>
        request.RequestUri!.AbsolutePath == "/v2/oauth/token" && !tokenToo
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ \"access_token\": \"token\", \"expires_in\": 3600 }", Encoding.UTF8, "application/json"),
            })
            : Fail(failure, cancellationToken));

    private static HttpClient Http(HttpMessageHandler handler, string address) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromMilliseconds(200) };

    private static IvaoApiClient Client(HttpMessageHandler handler, MemoryCache cache) => new(
        Http(handler, "https://api.example.org"),
        new IvaoApiTokenProvider(
            Http(handler, "https://api.example.org"),
            Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" }),
            cache,
            NullLogger<IvaoApiTokenProvider>.Instance),
        cache,
        IvaoNetworkPictureStore.None,
        NullLogger<IvaoApiClient>.Instance);

    private static IvaoAirspace Airspace() => new(
        new HashSet<string>(["LIRR"], StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(["LIRF"], StringComparer.OrdinalIgnoreCase));

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task EveryCallOfTheIvaoClientIsNoAnswerWhenIvaoFails(string failure)
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var ivao = Ivao(failure);
        var client = Client(ivao, cache);

        // The four that threw (#324): what a PIREP, the tracker calls of a send and the weather chain ask.
        Assert.Null(await client.GetRunwaysAsync("LIRF", token));
        Assert.Null(await client.GetFlightPlansAsync(42, token));
        Assert.Null(await client.GetTracksAsync(42, token));
        Assert.Null(await client.GetMetarAsync("LIRF", token));

        // The reference data of the night: an empty list is "keep what the snapshot has".
        Assert.Empty(await client.GetCentersAsync("IT", token));
        Assert.Empty(await client.GetAirportsAsync("IT", includeRunways: true, token));
        Assert.Empty(await client.GetAircraftTypesAsync(token));
        var (equipments, transponders) = await client.GetFlightPlanVocabulariesAsync(token);
        Assert.Empty(equipments);
        Assert.Empty(transponders);
        var (positions, sectors) = await client.GetAtcPositionsAsync(token);
        Assert.Empty(positions);
        Assert.Empty(sectors);
        Assert.Null(await client.GetFrasAsync("IT", token));

        // What a page waits on, and a member's own profile.
        Assert.Null(await client.SearchSessionsAsync(new IvaoSessionQuery(704798, Now.AddHours(-6), Now), token));
        Assert.Null(await client.GetDailyAtcBookingsAsync(DateOnly.FromDateTime(Now), cancellationToken: token));
        Assert.Null((await client.GetNetworkStatusAsync(Airspace(), token)).UpdatedAt);
        Assert.Null(await client.GetMeAsync("a member's token", token));

        // Each of them did ask: none was answered from a failure remembered by another.
        Assert.Contains(ivao.Asked, path => path.EndsWith("/runways", StringComparison.Ordinal));
        Assert.Contains(ivao.Asked, path => path.EndsWith("/flightPlans", StringComparison.Ordinal));
        Assert.Contains(ivao.Asked, path => path.EndsWith("/tracks", StringComparison.Ordinal));
        Assert.Contains(ivao.Asked, path => path.EndsWith("/metar", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task ATokenIvaoCannotGiveIsNoAnswerToo(string failure)
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var ivao = Ivao(failure, tokenToo: true);
        var client = Client(ivao, cache);

        Assert.Null(await client.GetRunwaysAsync("LIRF", token));
        Assert.Null(await client.GetFlightPlansAsync(42, token));
        Assert.Empty(await client.GetCentersAsync("IT", token));

        // Without a token the data calls are not made at all.
        Assert.All(ivao.Asked, path => Assert.Equal("/v2/oauth/token", path));
    }

    [Fact]
    public async Task TheCallersOwnCancellationIsStillTheirs()
    {
        // A reader that goes away is not IVAO failing: the cancellation reaches the caller, as it always did.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var ivao = Ivao("timeout");
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

        using var leaving = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        leaving.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetRunwaysAsync("LIRF", leaving.Token));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task TheWeatherChainGoesOnWhenNoaaAndIvaoFail(string failure)
    {
        // #324 item 4 and #325 item 1: NOAA cut by the resilience handler, the IVAO link failing, and VATSIM still asked.
        var token = TestContext.Current.CancellationToken;
        using var noaaService = new Service((_, cancellationToken) => Fail(failure, cancellationToken));
        var noaa = new NoaaWeatherClient(Http(noaaService, "https://aviationweather.gov"), new StubClock(Now), NullLogger<NoaaWeatherClient>.Instance);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var ivaoService = Ivao(failure);
        var ivao = Client(ivaoService, cache);

        using var vatsimService = new Service((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("LIRF 210750Z 27008KT CAVOK 25/18 Q1015", Encoding.UTF8, "text/plain"),
        }));
        var vatsim = new VatsimMetarClient(Http(vatsimService, "https://metar.vatsim.net"), NullLogger<VatsimMetarClient>.Instance);

        var source = new WeatherSource(noaa, ivao, vatsim, new StubClock(Now), NullLogger<WeatherSource>.Instance);
        var reports = await source.GetCurrentAsync(["LIRF"], token);

        Assert.Equal(VatsimMetarClient.SourceName, Assert.Single(reports).Source);
        // Null when nothing answered, empty when a body that is not JSON did: never an exception.
        Assert.Empty(await noaa.GetHistoryAsync("LIRF", Now.AddHours(-3), Now, token) ?? []);
    }

    [Theory]
    [InlineData("total timeout")]
    [InlineData("open circuit")]
    [InlineData("unreachable")]
    public async Task VatsimAndTheFirOutlinesAreNoAnswerWhenTheyCannotBeReached(string failure)
    {
        var token = TestContext.Current.CancellationToken;
        using var service = new Service((_, cancellationToken) => Fail(failure, cancellationToken));

        var vatsim = new VatsimMetarClient(Http(service, "https://metar.vatsim.net"), NullLogger<VatsimMetarClient>.Instance);
        Assert.Null(await vatsim.GetMetarAsync("LIRF", Now, token));

        var outlines = new VatSpyFirBoundarySource(new HttpClient(service, disposeHandler: false), NullLogger<VatSpyFirBoundarySource>.Instance);
        Assert.Empty(await outlines.GetAsync(token));
    }

    [Fact]
    public async Task NoaaCutByTheRealResilienceHandlerIsNoAnswer()
    {
        // Not a TimeoutRejectedException thrown by hand: the standard handler of AddWeather, with its timeouts shortened so
        // the test does not wait thirty seconds, in front of a NOAA that never answers.
        var token = TestContext.Current.CancellationToken;
        using var silent = new Service(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("never");
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new StubClock(Now));
        services.AddWeather();
        services.AddHttpClient<NoaaWeatherClient>().ConfigurePrimaryHttpMessageHandler(() => silent);
        services.Configure<HttpStandardResilienceOptions>($"{nameof(NoaaWeatherClient)}-standard", options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(300);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
        });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var noaa = scope.ServiceProvider.GetRequiredService<NoaaWeatherClient>();

        Assert.Empty(await noaa.GetCurrentMetarsAsync(["LIRF"], token));
        Assert.NotEmpty(silent.Asked);
    }

    [Fact]
    public void NoClientHasATimeoutOfItsOwnBehindTheHandlersTotal()
    {
        // The other half of #325 item 1. NOAA's client said sixty seconds behind the handler's thirty, VATSIM's fifteen and
        // IVAO's thirty: the standard resilience handler leaves a client's own timeout infinite, so only its total counts,
        // and none of those lines was a limit. Should a version of the handler stop doing so, this says it.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new IvaoOAuthOptions { Authority = "https://api.example.org" }));
        services.AddWeather();
        services.AddIvaoIntegration();
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var handlers = provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();

        foreach (var name in new[] { nameof(NoaaWeatherClient), nameof(VatsimMetarClient), nameof(IvaoApiClient), nameof(IvaoApiTokenProvider) })
        {
            using var http = factory.CreateClient(name);
            Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(30), handlers.Get($"{name}-standard").TotalRequestTimeout.Timeout);
        }
    }
}
