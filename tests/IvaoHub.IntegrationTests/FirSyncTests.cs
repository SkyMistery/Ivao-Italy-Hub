using IvaoHub.Core.Airspace;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The weekly job that writes the outlines into <c>ref_firs</c>, on the real database: the table's key
/// and its collation are what refused the first run on the test installation (16 October 2026).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class FirSyncTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static string Square(double lon, double lat) => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $$"""{"type":"Polygon","coordinates":[[[{{lon}},{{lat}}],[{{lon + 1}},{{lat}}],[{{lon + 1}},{{lat + 1}}],[{{lon}},{{lat + 1}}],[{{lon}},{{lat}}]]]}""");

    private static FirBoundary Boundary(string id, double lon, double lat) => new()
    {
        Id = id,
        GeometryJson = Square(lon, lat),
        MinLatitude = lat,
        MaxLatitude = lat + 1,
        MinLongitude = lon,
        MaxLongitude = lon + 1,
    };

    private async Task<int> RunAsync(IReadOnlyList<FirBoundary> boundaries, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var job = new FirSyncJob(
            new FixedSource(boundaries),
            services.GetRequiredService<HubDbContext>(),
            services.GetRequiredService<IFirLocator>(),
            services.GetRequiredService<IClock>(),
            NullLogger<FirSyncJob>.Instance);

        return await job.RunAsync(cancellationToken);
    }

    [Fact]
    public async Task AWholeWorldIsWrittenAndRewrittenWithoutDuplicates()
    {
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(2, await RunAsync([Boundary("XXAA", 0, 0), Boundary("XXBB", 10, 10)], token));

        // A second run over the rows the first one wrote, one region redrawn and one withdrawn.
        Assert.Equal(1, await RunAsync([Boundary("XXAA", 5, 5)], token));

        await using var scope = _factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<HubDbContext>().Firs.AsNoTracking().ToListAsync(token);

        var row = Assert.Single(rows);
        Assert.Equal("XXAA", row.Id);
        Assert.Equal(5, row.MinLatitude);
    }

    [Fact]
    public async Task ARegionNamedTwiceByTheSourceDoesNotFailTheRun()
    {
        // The source promises one boundary per region and the VATSpy reader merges the pieces; a
        // source that broke the promise used to track two rows with one key, and the run failed with
        // nothing written. The second spelling differs in case, which the collation calls the same.
        var token = TestContext.Current.CancellationToken;

        var count = await RunAsync([Boundary("XXAA", 0, 0), Boundary("XXCC", 20, 20), Boundary("xxaa", 40, 40)], token);
        Assert.Equal(2, count);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var rows = await database.Firs.AsNoTracking().OrderBy(fir => fir.Id).ToListAsync(token);
        Assert.Equal(["XXAA", "XXCC"], rows.Select(fir => fir.Id.ToUpperInvariant()));
        Assert.Equal(40, rows[0].MinLatitude);

        var log = await database.JobsLog.AsNoTracking()
            .Where(entry => entry.Job == FirSyncJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(token);

        Assert.Equal("succeeded", log.Status);
        Assert.Contains("named more than once", log.Message, StringComparison.Ordinal);

        // And the locator answers from what was written.
        var regions = await scope.ServiceProvider.GetRequiredService<IFirLocator>().LocateAsync(40.5, 40.5, token);
        Assert.Equal(["XXAA"], regions.Select(id => id.ToUpperInvariant()));
    }

    private sealed class FixedSource(IReadOnlyList<FirBoundary> boundaries) : IFirBoundarySource
    {
        public Task<IReadOnlyList<FirBoundary>> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(boundaries);
    }
}
