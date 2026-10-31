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
/// How the client reads the answers the snapshot prunes against (issue #325, item 3), and which circuit each call goes
/// through (item 6).
/// </summary>
public sealed class IvaoReferenceAnswersTests
{
    private const string Rome = """{ "centerId": "LIRR", "name": "Roma", "countryId": "IT" }""";
    private const string Milan = """{ "centerId": "LIMM", "name": "Milano", "countryId": "IT" }""";

    [Fact]
    public async Task AWholeSetIsReadBareOrWrapped()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        Assert.Equal(2, (await Client(new Answer($"[{Rome},{Milan}]"), cache).GetCentersAsync("IT", TestContext.Current.CancellationToken)).Count);

        using var wrapped = new MemoryCache(new MemoryCacheOptions());
        var whole = $$"""{ "totalItems": 2, "page": 1, "pages": 1, "items": [{{Rome}},{{Milan}}] }""";
        Assert.Equal(2, (await Client(new Answer(whole), wrapped).GetCentersAsync("IT", TestContext.Current.CancellationToken)).Count);
    }

    [Theory]
    [InlineData("\"totalItems\": 40, \"page\": 1, \"pages\": 20")]
    [InlineData("\"totalItems\": 40")]
    [InlineData("\"pages\": 2")]
    public async Task OnePageOfSeveralIsNoAnswer(string paging)
    {
        // Page one alone would have the snapshot delete every centre of the pages after it.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var answer = $$"""{ {{paging}}, "items": [{{Rome}},{{Milan}}] }""";

        Assert.Empty(await Client(new Answer(answer), cache).GetCentersAsync("IT", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://api.ivao.aero/v2/users/me", "sign-in")]
    [InlineData("https://api.ivao.aero/v2/tracker/whazzup", "network")]
    [InlineData("https://api.ivao.aero/v2/tracker/sessions?callsign=IVA1&page=1", "tracker")]
    [InlineData("https://api.ivao.aero/v2/tracker/sessions/62747397/flightPlans", "tracker")]
    [InlineData("https://api.ivao.aero/v2/atc/bookings/daily/2026-10-31", "bookings")]
    [InlineData("https://api.ivao.aero/v2/airports/all?includeRunways=false", "reference")]
    [InlineData("https://api.ivao.aero/v2/centers?countryId=IT", "reference")]
    public void EveryFamilyOfCallsHasACircuitOfItsOwn(string uri, string circuit)
    {
        // One breaker for all let a burst of failing tracker searches stop the sign-in.
        Assert.Equal(circuit, IvaoServiceCollectionExtensions.CircuitOf(new Uri(uri)));
    }

    private static IvaoApiClient Client(HttpMessageHandler ivao, MemoryCache cache)
    {
        var options = Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" });

        return new IvaoApiClient(
            new HttpClient(ivao, disposeHandler: false) { BaseAddress = new Uri("https://api.example.org") },
            new IvaoApiTokenProvider(
                new HttpClient(ivao, disposeHandler: false) { BaseAddress = new Uri("https://api.example.org") },
                options,
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            IvaoNetworkPictureStore.None,
            NullLogger<IvaoApiClient>.Instance);
    }

    /// <summary>An IVAO that gives a token and then the same answer to every call.</summary>
    private sealed class Answer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    request.RequestUri!.AbsolutePath == "/v2/oauth/token"
                        ? "{ \"access_token\": \"token\", \"expires_in\": 3600 }"
                        : body,
                    Encoding.UTF8,
                    "application/json"),
            });
    }
}
