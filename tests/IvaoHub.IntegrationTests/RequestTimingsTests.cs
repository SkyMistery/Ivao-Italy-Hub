using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// <c>diagnostics/requests-YYYYMMDD.txt</c> on the real host (note 2026-10-18-i-tempi-delle-richieste): a request writes its
/// line, with the route the hub declares and the database time it waited, and nothing that names whoever asked.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class RequestTimingsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    /// <summary>A number shaped like a VID, sent in every place a request can carry one.</summary>
    private const string Vid = "704798";

    private const string Agent = "SecretAgent/9.9 (somebody's own browser)";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"ivaohub-request-timings-{Guid.NewGuid():N}");
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task ARequestWritesItsLineWithTheRouteItsDatabaseTimeAndNothingPersonal()
    {
        var token = TestContext.Current.CancellationToken;
        var host = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Diagnostics:Requests:Enabled", "true")
            .UseSetting("Diagnostics:Requests:Directory", _folder));

        using (var client = host.CreateClient())
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/content/public/Page/member-{Vid}?vid={Vid}&q=secret");
            request.Headers.UserAgent.ParseAdd(Agent);
            request.Headers.Add("X-Forwarded-For", "203.0.113.77");
            using var response = await client.SendAsync(request, token);

            using var unrouted = await client.GetAsync($"/{Vid}", token);
        }

        // Stopping the host writes what is still queued.
        await host.DisposeAsync();

        var file = Path.Combine(_folder, RequestTimingsLog.FileName(DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.True(File.Exists(file), $"{file} was not written");
        var text = await File.ReadAllTextAsync(file, token);
        var lines = text.Split('\n').Select(RequestTimingsLine.Parse).OfType<RequestTimingsLine>().ToList();

        var content = Assert.Single(lines, line => line.Route == "/api/content/public/{kind}/{slug}");
        Assert.Equal("GET", content.Method);
        Assert.True(content.Commands > 0, "the content was read from the database, and its commands were not counted");
        Assert.True(content.Total > TimeSpan.Zero);

        // Nothing of what was asked beyond the pattern, nothing of who asked.
        Assert.DoesNotContain(Vid, text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("member", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SecretAgent", text, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.77", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACheckOfHealthIsNoVisitorItHasNoLineAndTheFirstVisitorStaysTheColdRequest()
    {
        var token = TestContext.Current.CancellationToken;
        var host = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Diagnostics:Requests:Enabled", "true")
            .UseSetting("Diagnostics:Requests:Directory", _folder));

        using (var client = host.CreateClient())
        {
            // As the keep-warm Worker asks: a query string that changes every time, so that no cache answers for the hub.
            using var probe = await client.GetAsync($"/health?t={DateTime.UtcNow.Ticks}", token);
            Assert.True(probe.IsSuccessStatusCode, $"/health answered {probe.StatusCode}");

            using var visit = await client.GetAsync("/api/version", token);
        }

        await host.DisposeAsync();

        var file = Path.Combine(_folder, RequestTimingsLog.FileName(DateOnly.FromDateTime(DateTime.UtcNow)));
        var lines = (await File.ReadAllTextAsync(file, token)).Split('\n').Select(RequestTimingsLine.Parse).OfType<RequestTimingsLine>().ToList();

        Assert.DoesNotContain(lines, line => line.Route.Contains("health", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, Assert.Single(lines, line => line.Route == "/api/version").Number);
    }

    [Fact]
    public async Task ATestHostWritesNothingUnlessItAsks()
    {
        var token = TestContext.Current.CancellationToken;
        var host = _factory.WithWebHostBuilder(builder => builder.UseSetting("Diagnostics:Requests:Directory", _folder));

        using (var client = host.CreateClient())
        {
            using var response = await client.GetAsync("/api/version", token);
        }

        await host.DisposeAsync();

        Assert.False(Directory.Exists(_folder) && Directory.EnumerateFiles(_folder).Any());
    }
}
