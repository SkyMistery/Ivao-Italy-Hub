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
/// The narrowed picture of the network on disk, for the next process (issue #397, note
/// 2026-10-26-la-foto-della-rete-oltre-il-processo): what is read back is what was kept, only within its minute, and
/// anything else is "no file" — never an exception and never a wrong figure.
/// </summary>
public sealed class NetworkPictureStoreTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"ivaohub-picture-{Guid.NewGuid():N}");
    private readonly Clock _clock = new() { Now = Noon };

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private IvaoNetworkPictureStore Store() =>
        new(_folder, _clock, NullLogger<IvaoNetworkPictureStore>.Instance);

    private static IvaoAirspace Airspace() => new(
        new HashSet<string>(["LIRR"], StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(["LIRF", "LIMC"], StringComparer.OrdinalIgnoreCase));

    private const string Evening = """
        {
          "updatedAt": "2026-10-26T11:59:30Z",
          "connections": { "atc": 210, "pilot": 1450 },
          "clients": {
            "atcs": [
              { "callsign": "LIRR_CTR", "atcSession": { "frequency": 125.5 } },
              { "callsign": "LIRF_TWR" },
              { "callsign": "EGLL_APP", "atcSession": { "frequency": 119.725 } }
            ],
            "pilots": [
              { "flightPlan": { "departureId": "LIRF", "arrivalId": "EGLL" } },
              { "flightPlan": { "departureId": "KJFK" } },
              { "flightPlan": { "arrivalId": "LIMC" } },
              { "flightPlan": {} }
            ]
          }
        }
        """;

    private static IvaoNetworkPicture Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return IvaoWhazzup.Read(document.RootElement);
    }

    private static void AssertSame(IvaoNetworkStatus expected, IvaoNetworkStatus actual)
    {
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
        Assert.Equal(expected.NetworkAtc, actual.NetworkAtc);
        Assert.Equal(expected.NetworkPilots, actual.NetworkPilots);
        Assert.Equal(expected.AreaAtc, actual.AreaAtc);
        Assert.Equal(expected.AreaPilots, actual.AreaPilots);
        Assert.Equal(expected.Positions, actual.Positions);
    }

    [Fact]
    public void WhatIsReadBackCountsAsWhatWasKept()
    {
        var picture = Read(Evening);
        Store().Write(picture);

        _clock.Now = Noon.AddSeconds(20);
        var kept = Store().Read(IvaoWhazzup.Freshness);

        Assert.NotNull(kept);
        Assert.Equal(TimeSpan.FromSeconds(40), kept.Value.Left);
        AssertSame(picture.For(Airspace()), kept.Value.Picture.For(Airspace()));
        AssertSame(picture.For(IvaoAirspace.OfAirports(["EGLL"], 10)), kept.Value.Picture.For(IvaoAirspace.OfAirports(["EGLL"], 10)));
    }

    [Fact]
    public void TheFileHoldsNoNameAndNoNumberOfAPerson()
    {
        // What the narrowed reading keeps and nothing more: the payload has names and VIDs, the file must not.
        const string withPeople = """
            {
              "updatedAt": "2026-10-26T11:59:30Z",
              "clients": {
                "atcs": [ { "callsign": "LIRR_CTR", "userId": 704798, "user": { "firstName": "Mario", "lastName": "Rossi" } } ],
                "pilots": [ { "userId": 123456, "callsign": "AZA123", "flightPlan": { "departureId": "LIRF", "remarks": "Mario" } } ]
              }
            }
            """;

        Store().Write(Read(withPeople));
        var text = File.ReadAllText(Path.Combine(_folder, IvaoNetworkPictureStore.FileName));

        Assert.DoesNotContain("704798", text, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mario", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AZA123", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(600)]
    [InlineData(-5)]
    public void APictureOutsideItsMinuteIsNoFile(int seconds)
    {
        // At the minute, past it, and "taken in the future" — a clock moved back: none is read.
        Store().Write(Read(Evening));

        _clock.Now = Noon.AddSeconds(seconds);

        Assert.Null(Store().Read(IvaoWhazzup.Freshness));
    }

    [Fact]
    public void AFailureIsKeptForItsMinuteToo()
    {
        // As in memory: a network that is down is asked once a minute, not once per process.
        Store().Write(IvaoNetworkPicture.Unknown);

        var kept = Store().Read(IvaoWhazzup.Freshness);

        Assert.NotNull(kept);
        Assert.Null(kept.Value.Picture.For(Airspace()).UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a picture at all")]
    [InlineData("ivao-network-picture 2\ntaken\t0\n")]
    [InlineData("ivao-network-picture 1\ntaken\tnoon\n")]
    [InlineData("ivao-network-picture 1\ntaken\t{now}\n")]
    [InlineData("ivao-network-picture 1\ntaken\t{now}\nupdated\t{now}\nnetwork\t1\n")]
    [InlineData("ivao-network-picture 1\ntaken\t{now}\nupdated\t{now}\nnetwork\t1\t2\nX\tsomething\n")]
    [InlineData("ivao-network-picture 1\ntaken\t{now}\nupdated\t{now}\nnetwork\t1\t2\nC\tLIRR_CTR\n")]
    public void HalfAFileOrAnotherShapeIsNoFile(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(
            Path.Combine(_folder, IvaoNetworkPictureStore.FileName),
            content.Replace("{now}", Noon.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));

        Assert.Null(Store().Read(IvaoWhazzup.Freshness));
    }

    [Fact]
    public void NoFolderIsNoFile()
    {
        Assert.Null(Store().Read(IvaoWhazzup.Freshness));
        Assert.Null(IvaoNetworkPictureStore.None.Read(IvaoWhazzup.Freshness));
    }

    [Fact]
    public void AFullEveningIsSmallAndReadsBack()
    {
        // A busy evening of the whole network, larger than any measured: the file stays far under the limit and is read.
        var atcs = string.Join(',', Enumerable.Range(0, 800).Select(n => $$$"""{"callsign":"S{{{n:D4}}}_APP","atcSession":{"frequency":118.1}}"""));
        var pilots = string.Join(',', Enumerable.Range(0, 8000).Select(n => $$$"""{"flightPlan":{"departureId":"A{{{n % 900:D3}}}","arrivalId":"B{{{n % 700:D3}}}"}}"""));
        var picture = Read($$$"""{"updatedAt":"2026-10-26T11:59:30Z","clients":{"atcs":[{{{atcs}}}],"pilots":[{{{pilots}}}]}}""");

        Store().Write(picture);

        var size = new FileInfo(Path.Combine(_folder, IvaoNetworkPictureStore.FileName)).Length;
        Assert.InRange(size, 1, IvaoNetworkPictureStore.MaxBytes / 10);

        var kept = Store().Read(IvaoWhazzup.Freshness);
        Assert.NotNull(kept);
        AssertSame(picture.For(IvaoAirspace.OfAirports(["A001", "S0005"], 10)), kept.Value.Picture.For(IvaoAirspace.OfAirports(["A001", "S0005"], 10)));
    }

    /// <summary>An IVAO that counts its calls and always answers the same evening.</summary>
    private sealed class Handler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Evening, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>A client of a process of its own: its own cache, the installation's store.</summary>
    private IvaoApiClient Process(Handler handler, IMemoryCache cache) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.example.org") },
        new IvaoApiTokenProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.org") },
            Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" }),
            cache,
            NullLogger<IvaoApiTokenProvider>.Instance),
        cache,
        Store(),
        NullLogger<IvaoApiClient>.Instance);

    [Fact]
    public async Task ANewProcessWithinTheMinuteReadsTheFileInsteadOfDownloading()
    {
        var token = TestContext.Current.CancellationToken;
        var handler = new Handler();

        using var firstCache = new MemoryCache(new MemoryCacheOptions());
        var first = await Process(handler, firstCache).GetNetworkStatusAsync(Airspace(), token);

        _clock.Now = Noon.AddSeconds(30);
        using var secondCache = new MemoryCache(new MemoryCacheOptions());
        var second = await Process(handler, secondCache).GetNetworkStatusAsync(Airspace(), token);

        Assert.Equal(1, handler.Calls);
        AssertSame(first, second);

        // Its minute over, the next process downloads again.
        _clock.Now = Noon.AddSeconds(61);
        using var thirdCache = new MemoryCache(new MemoryCacheOptions());
        await Process(handler, thirdCache).GetNetworkStatusAsync(Airspace(), token);

        Assert.Equal(2, handler.Calls);
    }
}
