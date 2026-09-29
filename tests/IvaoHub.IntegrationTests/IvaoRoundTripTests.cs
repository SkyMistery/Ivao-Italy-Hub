using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using IvaoHub.Core.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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

    private readonly FakeIvao _ivao = new();

    private HubWebApplicationFactory _factory = null!;

    private WebApplicationFactory<Program> _host = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);

        // IVAO's three endpoints, answered here: the key that signs the id_token, the token endpoint and the user info
        // are all the handler's backchannel.
        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<OpenIdConnectOptions>(HubClaims.IvaoScheme, options =>
            {
                options.Configuration!.SigningKeys.Add(_ivao.SigningKey);
                options.Configuration.UserInfoEndpoint = FakeIvao.UserInfoEndpoint;
                options.Backchannel = new HttpClient(_ivao);

                // ⚠️ The pinned configuration alone is not enough to validate a token: the framework has already built a
                // configuration manager for the real discovery document, and the token handler asks that one for the
                // keys, so the id_token would be checked against IVAO's real keys, over the network.
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
            })));

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

        var first = await LoginAsync(browser, "/me", token);

        // The consent screen: IVAO answers with a nonce that is not the one of this round.
        _ivao.NextNonce = ForeignNonce;
        using var refused = await ReturnFromIvaoAsync(browser, first.State, token);

        // Not the error page: the browser is sent to IVAO again, on a round of its own.
        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        var second = Round.From(refused.Headers.Location);
        Assert.NotEqual(first.State, second.State);
        Assert.NotEqual(first.Nonce, second.Nonce);

        // The consent is given now, so IVAO answers at once, with the nonce the hub sent.
        _ivao.NextNonce = second.Nonce;
        using var accepted = await ReturnFromIvaoAsync(browser, second.State, token);

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

        var first = await LoginAsync(browser, "/me", token);
        _ivao.NextNonce = ForeignNonce;
        using var again = await ReturnFromIvaoAsync(browser, first.State, token);
        var second = Round.From(again.Headers.Location);

        // A fault that does not go away must not become a loop between the hub and IVAO.
        _ivao.NextNonce = ForeignNonce;
        using var refused = await ReturnFromIvaoAsync(browser, second.State, token);

        Assert.Equal(HttpStatusCode.Found, refused.StatusCode);
        Assert.Equal($"{IvaoAuthenticationExtensions.LoginErrorPath}?code=nonce", refused.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task TheNonceOfTheRoundSignsInAtTheFirstReturn()
    {
        // The nonce is still checked, and a return that carries the right one needs no second round.
        var token = TestContext.Current.CancellationToken;
        using var browser = CreateBrowser();

        var round = await LoginAsync(browser, "/me", token);
        _ivao.NextNonce = round.Nonce;
        using var accepted = await ReturnFromIvaoAsync(browser, round.State, token);

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
        var round = await LoginAsync(starter, "/me", token);

        using var stranger = CreateBrowser();
        _ivao.NextNonce = round.Nonce;
        using var refused = await ReturnFromIvaoAsync(stranger, round.State, token);

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

    private static async Task<Round> LoginAsync(HttpClient browser, string returnUrl, CancellationToken cancellationToken)
    {
        using var response = await browser.GetAsync(
            new Uri($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return Round.From(response.Headers.Location);
    }

    /// <summary>IVAO sending the browser back to the callback, the way it does: a GET with the code and the state.</summary>
    private static Task<HttpResponseMessage> ReturnFromIvaoAsync(
        HttpClient browser,
        string state,
        CancellationToken cancellationToken) =>
        browser.GetAsync(
            new Uri($"/auth/callback?code=code-{Guid.NewGuid():N}&state={Uri.EscapeDataString(state)}", UriKind.Relative),
            cancellationToken);

    /// <summary>The two values of a round that the hub put in the address of IVAO's authorize page.</summary>
    private sealed record Round(string State, string Nonce)
    {
        public static Round From(Uri? location)
        {
            Assert.NotNull(location);
            Assert.StartsWith("https://sso.ivao.aero/authorize", location.ToString(), StringComparison.Ordinal);

            var query = HttpUtility.ParseQueryString(location.Query);
            var state = query["state"];
            var nonce = query["nonce"];
            Assert.False(string.IsNullOrEmpty(state));
            Assert.False(string.IsNullOrEmpty(nonce));
            return new Round(state, nonce);
        }
    }

    /// <summary>IVAO's token endpoint and user info, answering whatever the backchannel of the hub asks.</summary>
    private sealed class FakeIvao : HttpMessageHandler
    {
        public const string UserInfoEndpoint = "https://api.ivao.aero/v2/users/me";

        private readonly RSA _rsa = RSA.Create(2048);

        public FakeIvao() => SigningKey = new RsaSecurityKey(_rsa) { KeyId = "fake-ivao" };

        public RsaSecurityKey SigningKey { get; }

        /// <summary>The nonce the next id_token carries.</summary>
        public string? NextNonce { get; set; }

        public int TokenRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();

            if (uri == "https://api.ivao.aero/v2/oauth/token")
            {
                TokenRequests++;
                return Task.FromResult(Json(new
                {
                    access_token = "fake-access-token",
                    token_type = "Bearer",
                    expires_in = 3600,
                    id_token = IdToken(NextNonce),
                }));
            }

            if (uri == UserInfoEndpoint)
            {
                return Task.FromResult(Json(new { id = MemberVid, firstName = "Nonce", lastName = "Member" }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _rsa.Dispose();
            }

            base.Dispose(disposing);
        }

        private string IdToken(string? nonce)
        {
            var claims = new Dictionary<string, object> { ["sub"] = MemberVid.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (nonce is not null)
            {
                claims["nonce"] = nonce;
            }

            return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "https://api.ivao.aero",
                Audience = "test-client",
                Claims = claims,
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
            });
        }

        private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
    }
}
