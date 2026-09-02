using IvaoHub.Core.Auth;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Boots the real host against the container database. The OAuth client is supplied in memory,
/// the way a production deployment may supply it through <c>Ivao__*</c> environment variables, so
/// the test never needs the credentials file that is not in the repository.
/// </summary>
public sealed class HubWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Ivao:Authority"] = "https://api.ivao.aero",
                ["Ivao:ClientId"] = "test-client",
                ["Ivao:ClientSecret"] = "test-secret",
                ["Ivao:LoginUrl"] = "http://localhost/auth/login",
                ["Ivao:RedirectUri"] = "http://localhost/auth/callback",
                ["Ivao:PostLogoutRedirectUri"] = "http://localhost/",
                ["Ivao:Scopes:0"] = "openid",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestSignInStartupFilter>();

            // The endpoints of the identity provider are pinned instead of discovered: a test must
            // not depend on IVAO being reachable. What is still exercised is our own override of
            // the redirect URI, which is the part that can actually be got wrong.
            services.PostConfigure<OpenIdConnectOptions>(HubClaims.IvaoScheme, options =>
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://api.ivao.aero",
                    AuthorizationEndpoint = "https://sso.ivao.aero/authorize",
                    TokenEndpoint = "https://api.ivao.aero/v2/oauth/token",
                });
        });
    }

    /// <summary>A client that keeps cookies and does not follow redirects, so a test can see them.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    /// <summary>Signs the client in as an existing VID with a real application cookie.</summary>
    public async Task SignInAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>The repository root the host resolved, so a test can read the files it wrote.</summary>
    public HubPaths Paths => Services.GetRequiredService<HubPaths>();
}
