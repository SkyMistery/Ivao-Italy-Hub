using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// An erasure writes a pseudonym into the person columns of a module's rows, and the save projects the row again (issue #326,
/// item 1): a signal of the award queue is a member's, never a pseudonym's, which nobody could assign and every assigner would
/// be told about the next morning.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AwardSignalPseudonymTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int PilotVid = 780047;
    private const int Pseudonym = -780047;

    private HubWebApplicationFactory _factory = null!;
    private long? _sample;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sample is { } id)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            module.Events.RemoveRange(await module.Events.Where(row => row.Id == id).ToListAsync());
            await module.SaveChangesAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task APseudonymGetsNoSignalAndThePendingOneOfThePersonIsWithdrawn()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var sample = new SampleEvent
        {
            Title = $"pseudonym-{Guid.NewGuid():N}",
            StartsAt = new DateTime(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            Status = PublishStatus.Published,
            AwardeeVid = PilotVid,
        };
        module.Events.Add(sample);
        await module.SaveChangesAsync(token);
        _sample = sample.Id;

        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.Equal(PilotVid, Assert.Single(await Signals(hub, sample.SourceId, token)).Vid);

        // What the erasure does to the row: the pseudonym where the member was, and the save that projects it again.
        sample.AwardeeVid = Pseudonym;
        sample.Title += "-erased";
        await module.SaveChangesAsync(token);

        Assert.Empty(await Signals(hub, sample.SourceId, token));
    }

    private static Task<List<AwardSignal>> Signals(HubDbContext hub, string sourceId, CancellationToken token) =>
        hub.AwardSignals.AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
            .ToListAsync(token);
}
