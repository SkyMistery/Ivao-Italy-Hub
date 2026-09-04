using IvaoHub.Core.Auth;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
public sealed class HubWebApplicationFactory(
    string connectionString,
    bool useIvaoFixtures = false,
    TestCurrentUser? currentUser = null,
    IInterceptor? extraInterceptor = null)
    : WebApplicationFactory<Program>
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
                ["Ivao:UseFixtures"] = useIvaoFixtures ? "true" : "false",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestSignInStartupFilter>();

            // EF Core picks up any IInterceptor registered in the application container, so a test
            // can watch what the contexts of the host actually send to the database.
            if (extraInterceptor is not null)
            {
                // As IInterceptor, which is the service type EF Core looks for; registering the
                // concrete type would compile and be quietly ignored.
                services.AddSingleton<IInterceptor>(extraInterceptor);
            }

            // F4 has no endpoints, so a test says who is asking instead of signing a cookie in.
            // The tests of the identity itself leave this alone and use the real implementation.
            //
            // Nobody is signed in while the application is starting, and the backbone rests on it:
            // the write guard of the interceptor leaves an anonymous caller alone precisely because
            // an anonymous caller is the installation itself -- a migration, a job, the seed of the
            // system templates. The real implementation reads an HTTP context and so is anonymous
            // there by construction; a fake registered flatly would instead make the whole start up
            // sequence run as whichever coordinator the test had in mind, and the seeder would be
            // refused the department it seeds into. ApplicationStarted is the line between the two.
            if (currentUser is not null)
            {
                services.AddScoped<ICurrentUser>(provider =>
                    provider.GetRequiredService<IHostApplicationLifetime>()
                        .ApplicationStarted.IsCancellationRequested
                            ? currentUser
                            : StartUpUser);
            }

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

    /// <summary>Nobody, which is who the installation is while it is starting itself up.</summary>
    private static readonly TestCurrentUser StartUpUser = new();
}
