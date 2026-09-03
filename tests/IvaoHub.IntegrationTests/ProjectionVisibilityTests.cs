using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The projections are rows like any other, and the rule that decides who may read a row is the
/// global query filter — not something each endpoint remembers.
/// <para><c>cms_search_index</c> and <c>cms_calendar_entries</c> carried an owner department and a
/// visibility as plain columns without declaring the interfaces, so the filter skipped them: the
/// two tables the search and the calendar are built on were the only ones with no safety net under
/// them. <c>cms_award_signals</c> has no owner to compare against and is deliberately left out.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ProjectionVisibilityTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int StaffVid = 700501;
    private const int MemberVid = 700502;
    private const int SuperadminVid = 700503;

    private readonly TestCurrentUser _user = new();
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, currentUser: _user);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ASearchRowIsOnlyReadableByWhoeverMayReadTheRowItMirrors()
    {
        var token = TestContext.Current.CancellationToken;
        var category = $"vis-{Guid.NewGuid():N}";

        // Seeded by the maintainer of the division, who is allowed everywhere.
        _user.Superadmin(SuperadminVid);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            database.Links.AddRange(
                NewLink(category, Department.ED, Visibility.Public),
                NewLink(category, Department.ED, Visibility.Members),
                NewLink(category, Department.ED, Visibility.Staff),
                NewLink(category, Department.ED, Visibility.Department),
                NewLink(category, Department.FOD, Visibility.Department));
            await database.SaveChangesAsync(token);
        }

        _user.Anonymous();
        Assert.Equal([Visibility.Public], await VisibleAsync(category, token));

        _user.Member(MemberVid);
        Assert.Equal([Visibility.Public, Visibility.Members], await VisibleAsync(category, token));

        // Staff of ED: everything of ED, and not the row FOD keeps to itself.
        _user.Coordinator(StaffVid, Department.ED);
        Assert.Equal(
            [Visibility.Public, Visibility.Members, Visibility.Staff, Visibility.Department],
            await VisibleAsync(category, token));

        _user.Superadmin(SuperadminVid);
        Assert.Equal(5, (await VisibleAsync(category, token)).Count);
    }

    [Fact]
    public async Task TheWriterStillFindsWhatItHasToRewriteWhoeverIsAsking()
    {
        // The other half of the same change: the projection tables are filtered, so the writer has
        // to read them past the filter. If it could not, it would fail to find the row of another
        // department and insert a second one, against a unique key.
        var token = TestContext.Current.CancellationToken;
        var category = $"rewrite-{Guid.NewGuid():N}";

        _user.Superadmin(SuperadminVid);

        long id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var link = NewLink(category, Department.FOD, Visibility.Department);
            database.Links.Add(link);
            await database.SaveChangesAsync(token);
            id = link.Id;
        }

        // Now somebody who cannot see a FOD department row updates it — a director, say.
        _user.Director(SuperadminVid);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var link = await database.Links.FirstAsync(row => row.Id == id, token);
            link.Title = "Aggiornato".L("Updated");
            await database.SaveChangesAsync(token);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var rows = await database.SearchIndex.IgnoreQueryFilters().AsNoTracking()
                .Where(row => row.SourceId == $"link:{id}")
                .ToListAsync(token);

            // One per language, rewritten — not two per language, added.
            Assert.Equal(2, rows.Count);
            Assert.Equal("Updated", rows.Single(row => row.Locale == "en").Title);
        }
    }

    private static Link NewLink(string category, Department department, Visibility visibility) => new()
    {
        OwnerDepartment = department,
        Visibility = visibility,
        Category = category,
        Title = "Roma".L("Rome"),
        Url = "https://www.ivao.aero",
        IsActive = true,
    };

    /// <summary>What the search index gives up, read as the current user through the filter.</summary>
    private async Task<List<Visibility>> VisibleAsync(string category, CancellationToken token)
    {
        // A new scope, because the context reads the current user when the query runs.
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var ids = await database.Links.IgnoreQueryFilters().AsNoTracking()
            .Where(link => link.Category == category)
            .Select(link => "link:" + link.Id)
            .ToListAsync(token);

        return await database.SearchIndex.AsNoTracking()
            .Where(row => ids.Contains(row.SourceId) && row.Locale == "en")
            .OrderBy(row => row.Id)
            .Select(row => row.Visibility)
            .ToListAsync(token);
    }
}
