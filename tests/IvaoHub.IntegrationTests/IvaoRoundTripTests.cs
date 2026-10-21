using System.Net;
using IvaoHub.Core.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The whole IVAO round trip, with IVAO played by the test: the login, the return with a code, the token endpoint that
/// answers with a signed id_token, the user info, and the application cookie at the end (note
/// 2026-09-28-il-nonce-e-il-consenso-di-ivao).
/// <para>On 28 September 2026 the first sign in of two members on the test installation came back refused on the nonce,
/// and the second, seconds later, went through. IVAO's consent screen — the page a member meets the first time they
/// sign in to a client — posts the request on without the nonce (its form has no field for it), so that return
/// carries a nonce that is none of ours. These tests give the hub that return and check what the member sees.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class IvaoRoundTripTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // Its own range, because the suite shares one database and a VID is a row.
    private const int MemberVid = 648001;

    /// <summary>What IVAO puts in the id_token after its consent screen: a nonce, but not the one the hub sent.</summary>
    private const string ForeignNonce = "639000000000000000.Tm90VGhlTm9uY2VPZlRoaXNSb3VuZA";

    private readonly FakeIvao _ivao = new(MemberVid);

    private HubWebApplicationFactory _factory = null!;

    private WebApplicationFactory<Program> _host = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _host = _ivao.Serve(_factory);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        await _factory.DisposeAsync();
        _ivao.Dispose();
    }

    [Fact]
    public async Task AReturnWithAForeignNonceStartsTheRoundAgainOnceAndTheMemberIsSignedIn()
    {
        var token = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();

        var first = await FakeIvao.LoginAsync(browser, "/me", token);

        // The consent screen: IVAO answers with a nonce that is not the one of this round.
        _ivao.NextNonce = ForeignNonce;
        using var refused = await FakeIvao.ReturnAsync(browser, first.State, token);

        // Not the error page: the browser is sent to IVAO again, on a round of its own.
        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        var second = IvaoRound.From(refused.Headers.Location);
        Assert.NotEqual(first.State, second.State);
        Assert.NotEqual(first.Nonce, second.Nonce);

        // The consent is given now, so IVAO answers at once, with the nonce the hub sent.
        _ivao.NextNonce = second.Nonce;
        using var accepted = await FakeIvao.ReturnAsync(browser, second.State, token);

        Assert.Equal(HttpStatusCode.Found, accepted.StatusCode);
        Assert.Equal("/me", accepted.Headers.Location?.OriginalString);
        Assert.Contains(
            accepted.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("hub.auth=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASecondRoundThatFailsOnTheNonceEndsOnTheErrorPage()
    {
        var token = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();

        var first = await FakeIvao.LoginAsync(browser, "/me", token);
        _ivao.NextNonce = ForeignNonce;
        using var again = await FakeIvao.ReturnAsync(browser, first.State, token);
        var second = IvaoRound.From(again.Headers.Location);

        // A fault that does not go away must not become a loop between the hub and IVAO.
        _ivao.NextNonce = ForeignNonce;
        using var refused = await FakeIvao.ReturnAsync(browser, second.State, token);

        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        Assert.Equal($"{IvaoAuthenticationExtensions.LoginErrorPath}?code=nonce", refused.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task TheNonceOfTheRoundSignsInAtTheFirstReturn()
    {
        // The nonce is still checked, and a return that carries the right one needs no second round.
        var token = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();

        var round = await FakeIvao.LoginAsync(browser, "/me", token);
        _ivao.NextNonce = round.Nonce;
        using var accepted = await FakeIvao.ReturnAsync(browser, round.State, token);

        Assert.Equal(HttpStatusCode.Found, accepted.StatusCode);
        Assert.Equal("/me", accepted.Headers.Location?.OriginalString);
        Assert.Equal(1, _ivao.TokenRequests);
    }

    [Fact]
    public async Task ACorrelationFailureIsNotStartedAgain()
    {
        // Only the nonce earns a second round. A return this browser did not start has no state the hub can read back,
        // and it goes to the error page as before.
        var token = TestContext.Current.CancellationToken;
        using var starter = CreateBrowser();
        var round = await FakeIvao.LoginAsync(starter, "/me", token);

        using var stranger = CreateBrowser();
        _ivao.NextNonce = round.Nonce;
        using var refused = await FakeIvao.ReturnAsync(stranger, round.State, token);

        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        Assert.Equal(
            $"{IvaoAuthenticationExtensions.LoginErrorPath}?code=correlation",
            refused.Headers.Location?.OriginalString);
    }

    private HttpClient CreateBrowser() => _host.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });
}
