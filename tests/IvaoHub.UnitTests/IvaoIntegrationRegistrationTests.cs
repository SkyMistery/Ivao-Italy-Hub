using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>Fixtures are a development convenience, and must never be one in production.</summary>
public sealed class IvaoIntegrationRegistrationTests
{
    private static DivisionOptions Division() =>
        new() { Code = "XX", CountryId = "XX", Domain = "x", Timezone = "UTC", DefaultLocale = "en", Locales = ["en"] };

    private static IConfiguration Configuration(bool useFixtures) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [IvaoServiceCollectionExtensions.UseFixturesKey] = useFixtures ? "true" : "false",
            })
            .Build();

    [Fact]
    public void RefusesFixturesOutsideDevelopment()
    {
        // A live site serving invented airspace would be worse than a live site with none.
        var services = new ServiceCollection();

        var refused = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddIvaoIntegration(Configuration(useFixtures: true), new Environment("Production"), Division());
        });

        Assert.Contains("only allowed in development", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsFixturesInDevelopment()
    {
        var services = new ServiceCollection();

        services.AddIvaoIntegration(Configuration(useFixtures: true), new Environment("Development"), Division());

        Assert.Contains(services, service => service.ServiceType == typeof(IIvaoApiClient));
    }

    [Fact]
    public void TheFixtureClientItselfRefusesToExistOutsideDevelopment()
    {
        // The registration guard reads configuration that a deployment may add later, so the object
        // that would actually serve the invented airspace refuses on its own too.
        var refused = Assert.Throws<InvalidOperationException>(() => new FixtureIvaoApiClient(
            HubPaths.Resolve(AppContext.BaseDirectory),
            new Environment("Production"),
            NullLogger<FixtureIvaoApiClient>.Instance));

        Assert.Contains("only allowed in development", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UsesTheRealClientWhenFixturesAreOff()
    {
        var services = new ServiceCollection();

        services.AddIvaoIntegration(Configuration(useFixtures: false), new Environment("Production"), Division());

        Assert.Contains(services, service => service.ServiceType == typeof(IFirDirectory));
        Assert.Contains(services, service => service.ServiceType == typeof(RefDataSyncJob));
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "IvaoHub.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
