using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// After an FTP deploy these two endpoints are the only way to tell what is running and whether it
/// can reach the database, so they are anonymous and never cached (design M0 section 2.4).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class HealthAndVersionEndpointsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task HealthAnswersOnlyWhenTheDatabaseAnswers()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The health check middleware adds no-cache of its own; what matters is that nothing caches it.
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task VersionReportsTheStampOfThePackage()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/version", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("commit").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("dotnet").GetString()));
        Assert.True(body.GetProperty("builtAt").GetDateTime() > DateTime.UnixEpoch);
    }

    [Fact]
    public async Task StartUpMigratesTheDatabaseAndWritesTheDiagnosticsFile()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var report = await File.ReadAllTextAsync(
            Path.Combine(_factory.Paths.Diagnostics, StartupDiagnostics.FileName),
            TestContext.Current.CancellationToken);

        Assert.Contains("division      ", report, StringComparison.Ordinal);
        Assert.Contains("migrations    ", report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/anything")]
    [InlineData("/auth/login")]
    [InlineData("/services/vsop/x")]
    [InlineData("/_framework/blazor.js")]
    public async Task ExcludedPrefixesAreNotHandledBySpaFallback(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
