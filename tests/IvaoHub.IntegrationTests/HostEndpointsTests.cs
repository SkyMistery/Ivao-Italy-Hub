using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>Acceptance of F0: the host answers /health and does not swallow excluded prefixes.</summary>
public sealed class HostEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HostEndpointsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task HealthReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
