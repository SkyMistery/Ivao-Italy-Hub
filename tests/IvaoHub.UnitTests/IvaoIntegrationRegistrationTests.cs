using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// Fixtures are a development convenience, and must never be one in production.
/// <para>The guard is checked where it actually stands: on the object that would serve the invented
/// airspace. It used to stand at registration as well, reading configuration before a test host or
/// a deployment had finished adding their sources — which is the very thing the hub decided not to
/// do (HANDOFF section 3). Resolving the client is therefore the honest test: it exercises the
/// choice at the moment it is really made.</para>
/// </summary>
public sealed class IvaoIntegrationRegistrationTests
{
    private static ServiceProvider Provider(bool useFixtures, string environment)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [IvaoServiceCollectionExtensions.UseFixturesKey] = useFixtures ? "true" : "false",
            })
            .Build());

        services.AddSingleton<IHostEnvironment>(new Environment(environment));
        services.AddSingleton(HubPaths.Resolve(AppContext.BaseDirectory));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddIvaoIntegration();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void RefusesFixturesOutsideDevelopment()
    {
        // A live site serving invented airspace would be worse than a live site with none.
        using var provider = Provider(useFixtures: true, environment: "Production");
        using var scope = provider.CreateScope();

        var refused = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IIvaoApiClient>());

        Assert.Contains("only allowed in development", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsFixturesInDevelopment()
    {
        using var provider = Provider(useFixtures: true, environment: "Development");
        using var scope = provider.CreateScope();

        Assert.IsType<FixtureIvaoApiClient>(scope.ServiceProvider.GetRequiredService<IIvaoApiClient>());
    }

    [Fact]
    public void TheChoiceIsMadeWhenTheClientIsBuiltAndNotWhenItIsRegistered()
    {
        // The configuration source arrives after AddIvaoIntegration has already run, exactly as it
        // does in a test host and in a deployment. The client must still come out as a fixture one.
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new Environment("Development"));
        services.AddSingleton(HubPaths.Resolve(AppContext.BaseDirectory));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddIvaoIntegration();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [IvaoServiceCollectionExtensions.UseFixturesKey] = "true",
            })
            .Build());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<FixtureIvaoApiClient>(scope.ServiceProvider.GetRequiredService<IIvaoApiClient>());
    }

    [Fact]
    public void RegistersTheDirectoryAndTheSyncJob()
    {
        var services = new ServiceCollection();
        services.AddIvaoIntegration();

        Assert.Contains(services, service => service.ServiceType == typeof(IFirDirectory));
        Assert.Contains(services, service => service.ServiceType == typeof(RefDataSyncJob));
        Assert.Contains(services, service => service.ServiceType == typeof(IIvaoApiClient));
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "IvaoHub.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
