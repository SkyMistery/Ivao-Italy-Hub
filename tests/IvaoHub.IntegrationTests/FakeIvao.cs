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
/// IVAO played by the test, for the whole round trip of a sign in: the key that signs the id_token, the token endpoint and
/// the user info, all answered on the hub's backchannel for one member (note 2026-09-28-il-nonce-e-il-consenso-di-ivao).
/// The browser's two legs — the login and the return with a code — are <see cref="LoginAsync"/> and <see cref="ReturnAsync"/>.
/// </summary>
internal sealed class FakeIvao : HttpMessageHandler
{
    public const string UserInfoEndpoint = "https://api.ivao.aero/v2/users/me";

    private readonly RSA _rsa = RSA.Create(2048);

    private readonly int _memberVid;

    public FakeIvao(int memberVid)
    {
        _memberVid = memberVid;
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = "fake-ivao" };
    }

    public RsaSecurityKey SigningKey { get; }

    /// <summary>The nonce the next id_token carries.</summary>
    public string? NextNonce { get; set; }

    public int TokenRequests { get; private set; }

    /// <summary>A host whose IVAO is this one.</summary>
    public WebApplicationFactory<Program> Serve(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<OpenIdConnectOptions>(HubClaims.IvaoScheme, options =>
            {
                options.Configuration!.SigningKeys.Add(SigningKey);
                options.Configuration.UserInfoEndpoint = UserInfoEndpoint;
                options.Backchannel = new HttpClient(this);

                // ⚠️ The pinned configuration alone is not enough to validate a token: the framework has already built a
                // configuration manager for the real discovery document, and the token handler asks that one for the
                // keys, so the id_token would be checked against IVAO's real keys, over the network.
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
            })));

    /// <summary>A whole sign in that goes through at the first return, the way it does once the member has given consent.</summary>
    public async Task SignInAsync(HttpClient browser, CancellationToken cancellationToken)
    {
        var round = await LoginAsync(browser, "/me", cancellationToken);
        NextNonce = round.Nonce;
        using var accepted = await ReturnAsync(browser, round.State, cancellationToken);

        Assert.Equal(HttpStatusCode.Found, accepted.StatusCode);
        Assert.Equal("/me", accepted.Headers.Location?.OriginalString);
    }

    public static async Task<IvaoRound> LoginAsync(HttpClient browser, string returnUrl, CancellationToken cancellationToken)
    {
        using var response = await browser.GetAsync(
            new Uri($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return IvaoRound.From(response.Headers.Location);
    }

    /// <summary>IVAO sending the browser back to the callback, the way it does: a GET with the code and the state.</summary>
    public static Task<HttpResponseMessage> ReturnAsync(HttpClient browser, string state, CancellationToken cancellationToken) =>
        browser.GetAsync(
            new Uri($"/auth/callback?code=code-{Guid.NewGuid():N}&state={Uri.EscapeDataString(state)}", UriKind.Relative),
            cancellationToken);

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
            return Task.FromResult(Json(new { id = _memberVid, firstName = "Nonce", lastName = "Member" }));
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
        var claims = new Dictionary<string, object> { ["sub"] = _memberVid.ToString(System.Globalization.CultureInfo.InvariantCulture) };
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

/// <summary>The two values of a round that the hub put in the address of IVAO's authorize page.</summary>
internal sealed record IvaoRound(string State, string Nonce)
{
    public static IvaoRound From(Uri? location)
    {
        Assert.NotNull(location);
        Assert.StartsWith("https://sso.ivao.aero/authorize", location.ToString(), StringComparison.Ordinal);

        var query = HttpUtility.ParseQueryString(location.Query);
        var state = query["state"];
        var nonce = query["nonce"];
        Assert.False(string.IsNullOrEmpty(state));
        Assert.False(string.IsNullOrEmpty(nonce));
        return new IvaoRound(state, nonce);
    }
}
