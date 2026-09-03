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
/// The application token is asked for once and reused until it is nearly spent. Asking on every
/// call would be slow for us and rude to IVAO; holding one past its expiry would fail the call it
/// was needed for.
/// </summary>
public sealed class IvaoApiTokenProviderTests
{
    private sealed class CountingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static IvaoApiTokenProvider Create(MemoryCache cache, CountingHandler handler, string[]? apiScopes = null)
    {
        var options = Options.Create(new IvaoOAuthOptions
        {
            Authority = "https://api.ivao.aero",
            ClientId = "client",
            ClientSecret = "secret",
            LoginUrl = "https://example.ivao.aero/auth/login",
            RedirectUri = "https://example.ivao.aero/auth/callback",
            PostLogoutRedirectUri = "https://example.ivao.aero/",
            Scopes = ["openid"],
            ApiScopes = apiScopes ?? [],
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.ivao.aero") };
        return new IvaoApiTokenProvider(http, options, cache, NullLogger<IvaoApiTokenProvider>.Instance);
    }

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    [Fact]
    public async Task AsksOnceAndThenReusesTheToken()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, """{"access_token":"abc","expires_in":3600}""");
        using var cache = NewCache();
        var provider = Create(cache, handler);

        var first = await provider.GetTokenAsync(TestContext.Current.CancellationToken);
        var second = await provider.GetTokenAsync(TestContext.Current.CancellationToken);
        var third = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("abc", first);
        Assert.Equal("abc", second);
        Assert.Equal("abc", third);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task AsksAgainOnceTheCachedTokenIsGone()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, """{"access_token":"abc","expires_in":3600}""");
        using var cache = NewCache();
        var provider = Create(cache, handler);

        await provider.GetTokenAsync(TestContext.Current.CancellationToken);
        cache.Clear();
        await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task DoesNotCacheATokenThatIsAlreadyTooCloseToExpiry()
    {
        // A token worth less than the safety margin must not be held: the next call would race it.
        var handler = new CountingHandler(HttpStatusCode.OK, """{"access_token":"abc","expires_in":5}""");
        using var cache = NewCache();
        var provider = Create(cache, handler);

        await provider.GetTokenAsync(TestContext.Current.CancellationToken);
        await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task AsksForTheApplicationScopesAndNotForTheMemberOnes()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, """{"access_token":"abc","expires_in":3600}""");
        using var cache = NewCache();
        var provider = Create(cache, handler, apiScopes: ["tracker"]);

        await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        var body = Assert.Single(handler.Bodies);
        Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
        Assert.Contains("scope=tracker", body, StringComparison.Ordinal);
        Assert.DoesNotContain("openid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsksForNoScopeAtAllWhenNoneIsConfigured()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, """{"access_token":"abc","expires_in":3600}""");
        using var cache = NewCache();
        var provider = Create(cache, handler);

        await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("scope=", Assert.Single(handler.Bodies), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnswersNullWhenIvaoRefuses()
    {
        // A refused token must not throw: the caller keeps yesterday's snapshot.
        var handler = new CountingHandler(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""");
        using var cache = NewCache();
        var provider = Create(cache, handler);

        Assert.Null(await provider.GetTokenAsync(TestContext.Current.CancellationToken));
    }
}
