using System.IO.Compression;
using System.Net;
using System.Text;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Core.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The weather chain, from the outside. The payloads are the shapes measured against the real
/// services on 16 September 2026: the world file of observations is a gzipped CSV whose first field
/// is the quoted bulletin, the queries answer JSON, and a forecast refuses the <c>hours</c>
/// parameter a report accepts.
/// </summary>
public sealed class WeatherTests
{
    /// <summary>Answers by path, and remembers what it was asked.</summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage> _answer;

        public ScriptedHandler(Func<Uri, HttpResponseMessage> answer) => _answer = answer;

        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(_answer(request.RequestUri!));
        }
    }

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    /// <summary>Answers a METAR for the airports it was given, and nothing for the others.</summary>
    private sealed class StubIvaoClient(params string[] icaos) : IIvaoApiClient
    {
        public List<string> Asked { get; } = [];

        public Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default)
        {
            Asked.Add(icao);
            return Task.FromResult(icaos.Contains(icao, StringComparer.OrdinalIgnoreCase)
                ? new IvaoMetarDto(icao, $"{icao} 160720Z 05005KT CAVOK 23/14 Q1016", new DateTime(2026, 9, 16, 7, 20, 0, DateTimeKind.Utc))
                : null);
        }

        public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoCenterDto>>([]);

        public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(string countryId, bool includeRunways = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoAirportDto>>([]);

        public Task<System.Text.Json.JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<System.Text.Json.JsonElement?>(null);

        public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(IvaoSessionQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoTrackerSessionDto>?>(null);

        public Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(long sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoFlightPlanDto>?>(null);

        public Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(long sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoTrackPointDto>?>(null);

        public Task<IvaoNetworkStatus> GetNetworkStatusAsync(IvaoAirspace airspace, CancellationToken cancellationToken = default) =>
            Task.FromResult(IvaoNetworkStatus.Unknown);
    }

    private static readonly DateTime Now = new(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Gzip(string body)
    {
        using var memory = new MemoryStream();
        using (var gzip = new GZipStream(memory, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(body));
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(memory.ToArray()) };
    }

    /// <summary>Two rows of the real file, header and all, with the quoted bulletin in front.</summary>
    private const string WorldFile =
        "raw_text,station_id,observation_time,latitude,longitude\n"
        + "\"METAR LIRF 160720Z 05005KT CAVOK 23/14 Q1016 NOSIG\",LIRF,2026-09-16T07:20:00.000Z,41.8000,12.2390\n"
        + "\"SPECI CYBW 160735Z AUTO 29003KT 3/8SM FG VV003 05/05 A3020\",CYBW,2026-09-16T07:35:00.000Z,51.1080,-114.3820\n";

    private static (NoaaWeatherClient Client, ScriptedHandler Handler) Noaa(Func<Uri, HttpResponseMessage> answer)
    {
        var handler = new ScriptedHandler(answer);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://aviationweather.gov") };
        return (new NoaaWeatherClient(http, NullLogger<NoaaWeatherClient>.Instance), handler);
    }

    [Fact]
    public async Task ManyAirportsAreServedByTheOneFileTheyAskUsToUse()
    {
        var (client, handler) = Noaa(_ => Gzip(WorldFile));
        var icaos = Enumerable.Range(0, 80).Select(index => $"ZZ{index:00}").Append("LIRF").ToArray();

        var reports = await client.GetCurrentMetarsAsync(icaos, TestContext.Current.CancellationToken);

        var report = Assert.Single(reports);
        Assert.Equal("LIRF", report.Icao);
        Assert.Equal(WeatherReportKind.Metar, report.Kind);
        Assert.StartsWith("METAR LIRF", report.Raw, StringComparison.Ordinal);
        Assert.Equal(new DateTime(2026, 9, 16, 7, 20, 0, DateTimeKind.Utc), report.IssuedAt);
        Assert.Equal("noaa", report.Source);

        // One call, not eighty one.
        Assert.Equal(["/data/cache/metars.cache.csv.gz"], handler.Asked);
    }

    [Fact]
    public async Task AFewAirportsAreAskedForByName()
    {
        var (client, handler) = Noaa(_ => Json(
            """[{"icaoId":"LIRF","reportTime":"2026-09-16T07:20:00.000Z","rawOb":"METAR LIRF 160720Z CAVOK"}]"""));

        var reports = await client.GetCurrentMetarsAsync(["LIRF", "LIMC"], TestContext.Current.CancellationToken);

        Assert.Single(reports);
        Assert.Contains("ids=LIRF,LIMC", Assert.Single(handler.Asked), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AForecastIsAskedForWithADateAndWithoutHours()
    {
        var (client, handler) = Noaa(uri => uri.PathAndQuery.Contains("/taf", StringComparison.Ordinal)
            ? Json("""[{"icaoId":"LIRF","issueTime":"2026-09-09T05:00:00.000Z","rawTAF":"TAF LIRF 090500Z"}]""")
            : Json("""[{"icaoId":"LIRF","reportTime":"2026-09-09T06:20:00.000Z","rawOb":"METAR LIRF 090620Z"}]"""));

        var reports = await client.GetHistoryAsync(
            "LIRF",
            new DateTime(2026, 9, 9, 6, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        Assert.NotNull(reports);
        Assert.Equal(2, reports.Count);
        Assert.Contains(reports, report => report.Kind == WeatherReportKind.Taf);

        var metarCall = handler.Asked.Single(call => call.Contains("/metar", StringComparison.Ordinal));
        var tafCall = handler.Asked.Single(call => call.Contains("/taf", StringComparison.Ordinal));
        Assert.Contains("date=20260909_0900", metarCall, StringComparison.Ordinal);
        Assert.Contains("hours=", metarCall, StringComparison.Ordinal);
        Assert.Contains("date=20260909_0900", tafCall, StringComparison.Ordinal);
        Assert.DoesNotContain("hours=", tafCall, StringComparison.Ordinal); // a TAF answers 400 to it
    }

    [Fact]
    public async Task AWindowOlderThanTheyKeepIsUnavailableWithoutAsking()
    {
        var (client, handler) = Noaa(_ => throw new InvalidOperationException("should not be called"));

        var reports = await client.GetHistoryAsync(
            "LIRF",
            DateTime.UtcNow.AddDays(-40),
            DateTime.UtcNow.AddDays(-40).AddHours(2),
            TestContext.Current.CancellationToken);

        Assert.Null(reports); // null is "we could not look", never "there was no weather"
        Assert.Empty(handler.Asked);
    }

    [Fact]
    public async Task AHistoryNobodyAnswersIsUnavailableAndNotEmpty()
    {
        var (client, _) = Noaa(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var reports = await client.GetHistoryAsync(
            "LIRF",
            DateTime.UtcNow.AddHours(-3),
            DateTime.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Null(reports);
    }

    [Fact]
    public async Task WhenTheFirstSourceIsDownTheChainFallsBackToIvaoAndThenToVatsim()
    {
        var (noaa, _) = Noaa(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var ivao = new StubIvaoClient("LIMC");

        var vatsimHandler = new ScriptedHandler(uri => uri.Query.Contains("LIRN", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("LIRN 160750Z 27008KT CAVOK 25/18 Q1015", Encoding.UTF8, "text/plain"),
            }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });

        var vatsim = new VatsimMetarClient(
            new HttpClient(vatsimHandler) { BaseAddress = new Uri("https://metar.vatsim.net") },
            NullLogger<VatsimMetarClient>.Instance);

        var source = new WeatherSource(noaa, ivao, vatsim, new StubClock(Now), NullLogger<WeatherSource>.Instance);

        var reports = await source.GetCurrentAsync(["LIRF", "LIMC", "LIRN"], TestContext.Current.CancellationToken);

        // LIMC came from IVAO, LIRN from VATSIM, and LIRF from nobody: a missing airport is missing,
        // not an exception and not an invented bulletin.
        Assert.Equal(2, reports.Count);
        Assert.Equal("ivao", Assert.Single(reports, report => report.Icao == "LIMC").Source);
        Assert.Equal("vatsim", Assert.Single(reports, report => report.Icao == "LIRN").Source);
        Assert.DoesNotContain(reports, report => report.Icao == "LIRF");

        // The fallbacks were asked only for what the first source did not answer for.
        Assert.Equal(["LIRF", "LIMC", "LIRN"], ivao.Asked);
        Assert.Equal(2, vatsimHandler.Asked.Count);
    }

    [Fact]
    public async Task TheFallbacksAreNotAskedWhenTheFirstSourceAnswers()
    {
        var (noaa, _) = Noaa(uri => uri.PathAndQuery.Contains("/taf", StringComparison.Ordinal)
            ? Json("[]")
            : Json("""[{"icaoId":"LIRF","reportTime":"2026-09-16T07:20:00.000Z","rawOb":"METAR LIRF 160720Z CAVOK"}]"""));

        var ivao = new StubIvaoClient("LIRF");
        var vatsim = new VatsimMetarClient(
            new HttpClient(new ScriptedHandler(_ => throw new InvalidOperationException("should not be called")))
            {
                BaseAddress = new Uri("https://metar.vatsim.net"),
            },
            NullLogger<VatsimMetarClient>.Instance);

        var source = new WeatherSource(noaa, ivao, vatsim, new StubClock(Now), NullLogger<WeatherSource>.Instance);

        var reports = await source.GetCurrentAsync(["LIRF"], TestContext.Current.CancellationToken);

        Assert.Equal("noaa", Assert.Single(reports).Source);
        Assert.Empty(ivao.Asked);
    }

    [Fact]
    public async Task AnswerThatIsNotAMetarIsNotTakenForOne()
    {
        var vatsim = new VatsimMetarClient(
            new HttpClient(new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("No METAR available", Encoding.UTF8, "text/plain"),
            }))
            {
                BaseAddress = new Uri("https://metar.vatsim.net"),
            },
            NullLogger<VatsimMetarClient>.Instance);

        Assert.Null(await vatsim.GetMetarAsync("LIRF", Now, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheChainIsWiredAndResolves()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IClock>(new StubClock(Now));
        services.AddSingleton<IIvaoApiClient>(new StubIvaoClient());
        services.AddWeather();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<WeatherSource>(scope.ServiceProvider.GetRequiredService<IWeatherSource>());

        // The two outside providers are reached with a user agent of our own, as their policies ask.
        var noaa = scope.ServiceProvider.GetRequiredService<NoaaWeatherClient>();
        Assert.NotNull(noaa);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<VatsimMetarClient>());
    }
}
