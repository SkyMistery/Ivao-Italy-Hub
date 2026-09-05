using System.Net;
using IvaoHub.Core.Services;
using IvaoHub.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The end to end bench signs a caller in as a member of staff without any proof at all, which is
/// exactly what a browser suite needs and exactly what must never exist anywhere else. It is fenced
/// twice — the environment must be <c>E2E</c> and <c>E2E:Enabled</c> must be set — and these tests
/// are what keeps both halves of that fence standing (design M1 section 11.1).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class E2EBenchTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheSignInDoesNotExistOutsideTheBench()
    {
        using var client = _factory.CreateApiClient();

        // A development host, which is what every other test in this project runs against.
        using var response = await client.PostAsync(
            new Uri("/e2e/signin", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void TheFlagOutsideItsEnvironmentStopsTheApplication()
    {
        // The failure this is watching for is a configuration file travelling to a server it was
        // not written for. Being quietly ignored there would be worse than not starting: nobody
        // would ever find out, and the file would stay.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["E2E:Enabled"] = "true" })
            .Build();

        var refusal = Assert.Throws<InvalidOperationException>(
            () => HubConfiguration.RequireE2EEnvironment(configuration, Environment(Environments.Production)));

        Assert.Contains("E2E:Enabled", refusal.Message, StringComparison.Ordinal);

        // Development is not the bench either: the fence is one environment, not "anything that is
        // not production".
        Assert.Throws<InvalidOperationException>(
            () => HubConfiguration.RequireE2EEnvironment(configuration, Environment(Environments.Development)));

        // And it says nothing at all when the flag is absent, which is every installation there is.
        HubConfiguration.RequireE2EEnvironment(new ConfigurationBuilder().Build(), Environment(Environments.Production));

        // In its own environment it is allowed, which is the only place it is.
        HubConfiguration.RequireE2EEnvironment(configuration, Environment(HubEnvironments.E2E));
    }

    private static IHostEnvironment Environment(string name) => new BenchEnvironment(name);

    /// <summary>An environment and nothing else: the guard reads one property.</summary>
    private sealed class BenchEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "IvaoHub.Web";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
