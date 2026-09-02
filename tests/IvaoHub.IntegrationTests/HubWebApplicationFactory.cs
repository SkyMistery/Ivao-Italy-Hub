using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    }

    /// <summary>The repository root the host resolved, so a test can read the files it wrote.</summary>
    public HubPaths Paths => Services.GetRequiredService<HubPaths>();
}
