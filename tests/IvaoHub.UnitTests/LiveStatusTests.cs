using System.Net;
using System.Text;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// What the live strip rests on: the reading of who is connected **never throws**, whatever IVAO
/// answers or fails to answer (design M1 §6.2).
/// <para>It matters more here than for the other calls of the same client. The reference data is a
/// snapshot read by a job, so a failure is a log line nobody sees until tomorrow; this one is read
/// while somebody is looking at the page, on every public address of the site. A client that threw
/// would turn a bad afternoon at IVAO into a site that does not open.</para>
/// </summary>
public sealed class LiveStatusTests
{
    /// <summary>An IVAO that answers however the test says, or does not answer at all.</summary>
    private sealed class Handler(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(answer());
        }
    }

    private static IvaoApiClient Create(Handler handler, MemoryCache cache)
    {
        var options = Options.Create(new IvaoOAuthOptions
        {
            ClientId = "test",
            ClientSecret = "test",
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.org") };

        return new IvaoApiClient(
            http,
            new IvaoApiTokenProvider(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.example.org") },
                options,
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            NullLogger<IvaoApiClient>.Instance);
    }

    /// <summary>The airspace of a division that has a snapshot, so the reading has something to compare.</summary>
    private static IvaoAirspace Airspace() => new(
        new HashSet<string>(["LIRR"], StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(["LIRF"], StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.BadGateway, "<html>maintenance</html>")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    [InlineData(HttpStatusCode.OK, "{\"clients\":\"unexpected shape\"}")]
    public async Task LiveStatusDegradesWhenIvaoIsDown(HttpStatusCode status, string body)
    {
        // Four ways of being down, because they fail in different places: a status the client checks,
        // a body that is not JSON, and a body that is JSON of the wrong shape. None of them may
        // reach the caller as an exception.
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var handler = new Handler(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

        var client = Create(handler, cache);

        var answer = await client.GetNetworkStatusAsync(Airspace(), token);

        // "Unknown" and not "nobody is connected": `UpdatedAt` of null is what tells the strip and
        // the block to say nothing rather than to draw four zeroes, which would be the page
        // answering a question it could not ask.
        Assert.Null(answer.UpdatedAt);
        Assert.Equal(0, answer.NetworkAtc);
        Assert.Empty(answer.Positions);
    }

    [Fact]
    public async Task ANetworkThatIsDownIsAskedOnceAMinuteAndNotOncePerReader()
    {
        // The failure is cached for the same minute the answer would be. Without it every reader of
        // every public page turns into another call to a network that is already down — which is the
        // shape of an outage the hub makes worse rather than survives.
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var handler = new Handler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = Create(handler, cache);
        var airspace = Airspace();

        for (var reader = 0; reader < 5; reader++)
        {
            Assert.Null((await client.GetNetworkStatusAsync(airspace, token)).UpdatedAt);
        }

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task WhoIsConnectedIsReadWithoutAnApplicationToken()
    {
        // The picture of who is on the network is the public one, and the client reads it without a
        // token on purpose: an installation that has never been given client credentials — a fork on
        // its first day — still draws the strip. One call is one call: the token endpoint is not
        // among them, and a client asking for a token first would make two.
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var handler = new Handler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"updatedAt":"2026-09-07T09:00:00Z","clients":{"atcs":[],"pilots":[]}}""",
                Encoding.UTF8,
                "application/json"),
        });

        var client = Create(handler, cache);
        var answer = await client.GetNetworkStatusAsync(Airspace(), token);

        Assert.Equal(1, handler.Calls);
        Assert.NotNull(answer.UpdatedAt);
    }
}
