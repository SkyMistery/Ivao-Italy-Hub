using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A row of a module projects, and it projects inside its own transaction (M2, T4). Until T4 it did
/// not: the context of a module did not hold the projection tables, and the interceptor skipped the
/// projection without a word (note 2026-09-15-contatti-con-risposte §3.3). No module projected yet,
/// so nothing noticed; the tours would have been the first to find out, in production, with a tour
/// missing from the calendar.
/// <para>On the table of the test module, as the rest of the backbone of modules is.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ModuleProjectionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 780031;

    private readonly TestCurrentUser _user = new();
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, currentUser: _user);
        _user.Superadmin(SuperadminVid);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ARowOfAModuleIsFoundInSearchAndInTheCalendarAndLeavesBothWhenItAsks()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var sample = NewEvent(days: 2);
        module.Events.Add(sample);
        await module.SaveChangesAsync(token);

        var sourceId = sample.SourceId;
        Assert.Equal(["en", "it"], (await SearchOf(hub, sourceId, token)).Select(row => row.Locale));

        // Two entries for one row, told apart by their position.
        var calendar = await CalendarOf(hub, sourceId, token);
        Assert.Equal([0, 1], calendar.Select(entry => entry.Sequence));
        Assert.Equal(sample.StartsAt.AddDays(1), calendar[1].StartsAtUtc);

        // One entry fewer: the one at the end goes, the first is rewritten and not added again.
        sample.Days = 1;
        sample.StartsAt = sample.StartsAt.AddHours(3);
        await module.SaveChangesAsync(token);

        calendar = await CalendarOf(hub, sourceId, token);
        var only = Assert.Single(calendar);
        Assert.Equal(sample.StartsAt, only.StartsAtUtc);

        // And a null snapshot takes the row out of both.
        sample.IsWithdrawn = true;
        await module.SaveChangesAsync(token);

        Assert.Empty(await SearchOf(hub, sourceId, token));
        Assert.Empty(await CalendarOf(hub, sourceId, token));

        module.Events.Remove(sample);
        await module.SaveChangesAsync(token);
    }

    [Fact]
    public async Task RollingBackTheWriteOfAModuleLeavesNoProjectionBehind()
    {
        var token = TestContext.Current.CancellationToken;
        string sourceId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await using var transaction = await module.Database.BeginTransactionAsync(token);

            var sample = NewEvent(days: 1);
            sample.BannerMediaId = UnusedMediaId();
            module.Events.Add(sample);
            await module.SaveChangesAsync(token);
            sourceId = sample.SourceId;

            // Inside the transaction of the module, on its connection: visible to it before the commit.
            Assert.NotEmpty(await module.Set<SearchIndexEntry>().IgnoreQueryFilters().AsNoTracking()
                .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
                .ToListAsync(token));

            await transaction.RollbackAsync(token);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            Assert.Empty(await SearchOf(hub, sourceId, token));
            Assert.Empty(await CalendarOf(hub, sourceId, token));
            Assert.Empty(await UsesOf(hub, sourceId, token));
        }
    }

    [Fact]
    public async Task ADraftKeepsItsFilesInUseAndNothingAReaderCouldFind()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var until = new DateTime(2031, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var sample = NewEvent(days: 1);
        sample.Status = PublishStatus.Draft;
        sample.BannerMediaId = UnusedMediaId();
        sample.BannerNeededUntil = until;
        module.Events.Add(sample);
        await module.SaveChangesAsync(token);

        var sourceId = sample.SourceId;
        Assert.Empty(await SearchOf(hub, sourceId, token));
        Assert.Empty(await CalendarOf(hub, sourceId, token));
        var use = Assert.Single(await UsesOf(hub, sourceId, token));
        Assert.Equal(until, use.UsedUntil);

        // Extended: the date moves in the same save, with no second write anywhere.
        sample.BannerNeededUntil = until.AddMonths(2);
        await module.SaveChangesAsync(token);
        Assert.Equal(until.AddMonths(2), Assert.Single(await UsesOf(hub, sourceId, token)).UsedUntil);

        // Another banner: the old file is no longer in use.
        var replacement = UnusedMediaId();
        sample.BannerMediaId = replacement;
        await module.SaveChangesAsync(token);
        Assert.Equal(replacement, Assert.Single(await UsesOf(hub, sourceId, token)).MediaId);

        module.Events.Remove(sample);
        await module.SaveChangesAsync(token);
        Assert.Empty(await UsesOf(hub, sourceId, token));
    }

    private static SampleEvent NewEvent(int days) => new()
    {
        Title = $"fo-test-proj-{Guid.NewGuid():N}",
        StartsAt = new DateTime(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc),
        Days = days,
        Status = PublishStatus.Published,
    };

    /// <summary>A file identifier no file has: the uses are a projection and hold no key to the library.</summary>
    private static long UnusedMediaId() => 9_000_000_000L + Random.Shared.Next(1, int.MaxValue);

    private static Task<List<SearchIndexEntry>> SearchOf(HubDbContext database, string sourceId, CancellationToken token) =>
        database.SearchIndex.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
            .OrderBy(row => row.Locale)
            .ToListAsync(token);

    private static Task<List<CalendarEntry>> CalendarOf(HubDbContext database, string sourceId, CancellationToken token) =>
        database.CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
            .OrderBy(row => row.Sequence)
            .ToListAsync(token);

    private static Task<List<MediaUse>> UsesOf(HubDbContext database, string sourceId, CancellationToken token) =>
        database.MediaUses.AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
            .ToListAsync(token);
}
