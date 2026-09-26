using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A template or a page a later release seeds, when the back office has already written a row at that address. The seed
/// used to add its own row anyway, the unique index on <c>(kind, path, is_template)</c> refused it, and the start failed
/// with it; now the written row is left as it is and the key is remembered — the rule the calendar kinds follow since
/// M3, A2 (<see cref="CalendarKindSeedTests"/>).
/// <para>What the seed sees is a row at the address and no key saying it seeded it. Each test makes exactly that out of
/// a seeded row, by forgetting its key, and the next seed puts the key back.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContentSeedTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ATemplateAlreadyWrittenIsLeftAsItIsAndTheStartGoesOn() =>
        await AssertLeftAsItIsAsync(
            ContentSeeder.SettingPrefix + "policy",
            row => row.IsTemplate && row.Kind == ContentKind.Document && row.Slug == "policy");

    [Fact]
    public async Task APageAlreadyWrittenIsLeftAsItIsWithNoSecondMenuEntry()
    {
        var token = TestContext.Current.CancellationToken;
        var menuBefore = await MenuEntriesToAsync("/about", token);

        await AssertLeftAsItIsAsync(
            ContentSeeder.PageSettingPrefix + "about",
            row => !row.IsTemplate && row.Kind == ContentKind.Page && row.ParentPath == null && row.Slug == "about");

        Assert.Equal(menuBefore, await MenuEntriesToAsync("/about", token));
    }

    [Fact]
    public async Task ADashboardAlreadyWrittenIsLeftAsItIs()
    {
        var department = Enum.GetValues<Department>()[0];
        var slug = department.ToString().ToLowerInvariant();

        await AssertLeftAsItIsAsync(
            ContentSeeder.DashboardSettingPrefix + department,
            row => !row.IsTemplate && row.Kind == ContentKind.Dashboard && row.Slug == slug);
    }

    private async Task AssertLeftAsItIsAsync(string setting, System.Linq.Expressions.Expression<Func<ContentEntry, bool>> address)
    {
        var token = TestContext.Current.CancellationToken;
        var before = await RowAsync(address, token);

        await ForgetAsync(setting, token);

        // What used to throw on the unique index, and take the start with it.
        await SeedAsync(token);

        var after = await RowAsync(address, token);
        Assert.Equal(before, after);
        Assert.True(await IsRememberedAsync(setting, token));

        // Remembered: the next start does not even ask.
        await SeedAsync(token);
        Assert.Equal(before, await RowAsync(address, token));
    }

    private async Task<(long Id, DateTime UpdatedAt, string BodyJson)> RowAsync(
        System.Linq.Expressions.Expression<Func<ContentEntry, bool>> address,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var row = Assert.Single(await CrudSource.BackOffice<ContentEntry>(database).AsNoTracking()
            .Where(address)
            .ToListAsync(cancellationToken));

        return (row.Id, row.UpdatedAt, row.BodyJson);
    }

    private async Task<int> MenuEntriesToAsync(string path, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        return await CrudSource.BackOffice<MenuItem>(database).CountAsync(item => item.Path == path, cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(cancellationToken);
    }

    private async Task<bool> IsRememberedAsync(string setting, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        return await database.DivisionSettings.AnyAsync(row => row.Key == setting, cancellationToken);
    }

    /// <summary>The installation as a release before the seed of that row left it: the row there, and no key.</summary>
    private async Task ForgetAsync(string setting, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.DivisionSettings.RemoveRange(
            await database.DivisionSettings.Where(row => row.Key == setting).ToListAsync(cancellationToken));

        await database.SaveChangesAsync(cancellationToken);
    }
}
