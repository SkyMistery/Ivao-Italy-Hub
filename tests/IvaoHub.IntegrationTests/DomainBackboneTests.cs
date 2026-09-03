using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone against a real MariaDB. These tests do not go through an endpoint on purpose:
/// what they prove is that the mechanisms hold even when the endpoint is the one thing that is
/// missing, which is exactly the guarantee the design asks for (design M0 section 8).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class DomainBackboneTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int StaffVid = 700001;
    private const int SuperadminVid = 700002;

    private readonly TestCurrentUser _user = new();
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, currentUser: _user);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task InterceptorFillsAuditAndTimestamps()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Coordinator(StaffVid, Department.ED);

        long id;
        DateTime created;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var link = NewLink(Department.ED);

            // Whatever the caller puts in the audit columns is irrelevant: they are filled here.
            link.CreatedBy = 999999;
            link.CreatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            database.Links.Add(link);
            await database.SaveChangesAsync(token);

            id = link.Id;
            created = link.CreatedAt;

            Assert.Equal(StaffVid, link.CreatedBy);
            Assert.Equal(StaffVid, link.UpdatedBy);
            Assert.NotEqual(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), link.CreatedAt);
            Assert.Equal(link.CreatedAt, link.UpdatedAt);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var link = await database.Links.SingleAsync(row => row.Id == id, token);

            link.Sort = 5;
            link.CreatedBy = 111111;
            await database.SaveChangesAsync(token);

            // Who created a row, and when, is written once and never rewritten.
            var stored = await database.Links.AsNoTracking().SingleAsync(row => row.Id == id, token);
            Assert.Equal(StaffVid, stored.CreatedBy);
            Assert.Equal(created, stored.CreatedAt, TimeSpan.FromSeconds(1));
            Assert.True(stored.UpdatedAt >= stored.CreatedAt);
        }
    }

    [Fact]
    public async Task InterceptorBlocksCrossDepartmentWrite()
    {
        var token = TestContext.Current.CancellationToken;

        var id = await SeedAsSuperadminAsync(NewLink(Department.FOD), token);

        _user.Coordinator(StaffVid, Department.ED);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        // Creating one for somebody else's department...
        database.Links.Add(NewLink(Department.FOD));
        var creating = await Assert.ThrowsAsync<ForbiddenDomainException>(() => database.SaveChangesAsync(token));
        Assert.Equal(CorePermissions.LinksEdit, creating.Permission);

        database.ChangeTracker.Clear();

        // ...and editing one, even by calling SaveChanges directly with no policy in the way.
        var theirs = await database.Links.SingleAsync(row => row.Id == id, token);
        theirs.Sort = 3;
        await Assert.ThrowsAsync<ForbiddenDomainException>(() => database.SaveChangesAsync(token));

        database.ChangeTracker.Clear();

        // Moving a row out of a department is a write on that department too.
        var moving = await database.Links.SingleAsync(row => row.Id == id, token);
        moving.OwnerDepartment = Department.ED;
        await Assert.ThrowsAsync<ForbiddenDomainException>(() => database.SaveChangesAsync(token));
    }

    [Fact]
    public async Task AuditLogWritten()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Coordinator(StaffVid, Department.ED);

        long id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var link = NewLink(Department.ED);
            database.Links.Add(link);
            await database.SaveChangesAsync(token);
            id = link.Id;

            link.Sort = 9;
            await database.SaveChangesAsync(token);

            database.Links.Remove(link);
            await database.SaveChangesAsync(token);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var key = id.ToString(CultureInfo.InvariantCulture);
            var rows = await database.AuditLog.AsNoTracking()
                .Where(entry => entry.Entity == "cms_links" && entry.EntityId == key)
                .OrderBy(entry => entry.Id)
                .ToListAsync(token);

            Assert.Equal(["created", "updated", "deleted"], rows.Select(row => row.Action));
            Assert.All(rows, row => Assert.Equal(StaffVid, row.Vid));
            Assert.All(rows, row => Assert.False(row.IsSuperadmin));

            // A creation says what appeared, an update says only what moved, a deletion says what
            // was there: an audit row is meant to be read, not to be diffed by hand.
            Assert.Contains("Roma", rows[0].AfterJson!, StringComparison.Ordinal);
            Assert.Contains("\"Sort\":9", rows[1].AfterJson!, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Url\"", rows[1].AfterJson!, StringComparison.Ordinal);
            Assert.Null(rows[2].AfterJson);
            Assert.Contains("Roma", rows[2].BeforeJson!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ProjectionUpsertedInSameTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Coordinator(StaffVid, Department.ED);

        long id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var link = NewLink(Department.ED);
            database.Links.Add(link);
            await database.SaveChangesAsync(token);
            id = link.Id;

            var rows = await ProjectionsOf(database, $"link:{id}", token);
            Assert.Equal(["en", "it"], rows.Select(row => row.Locale).Order());
            Assert.Equal("Roma", rows.Single(row => row.Locale == "it").Title);
            Assert.Equal("Rome", rows.Single(row => row.Locale == "en").Title);
            Assert.Equal(Department.ED, rows[0].OwnerDepartment);

            // An update rewrites the projection rather than adding a second one.
            link.Title = "Milano".L("Milan");
            await database.SaveChangesAsync(token);

            rows = await ProjectionsOf(database, $"link:{id}", token);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Milano", rows.Single(row => row.Locale == "it").Title);

            database.Links.Remove(link);
            await database.SaveChangesAsync(token);
            Assert.Empty(await ProjectionsOf(database, $"link:{id}", token));
        }

        // And when the caller owns the transaction, the projection is inside it: rolling back
        // leaves neither the row nor its index entry behind.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(token);

            var link = NewLink(Department.ED);
            database.Links.Add(link);
            await database.SaveChangesAsync(token);

            Assert.NotEmpty(await ProjectionsOf(database, $"link:{link.Id}", token));
            await transaction.RollbackAsync(token);

            database.ChangeTracker.Clear();
            Assert.Empty(await ProjectionsOf(database, $"link:{link.Id}", token));
            Assert.Null(await database.Links.AsNoTracking().FirstOrDefaultAsync(row => row.Id == link.Id, token));
        }
    }

    [Fact]
    public async Task DraftContentIsNotProjected()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Coordinator(StaffVid, Department.ED);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var content = new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = $"page-{Guid.NewGuid():N}",
            OwnerDepartment = Department.ED,
            Visibility = Visibility.Public,
            Status = PublishStatus.Draft,
            Title = "Bozza".L("Draft"),
            BodyJson = """
            {"schemaVersion":1,"sections":[{"id":"s","blocks":[{"id":"b","type":"text",
             "props":{"markdown":{"it":"Testo italiano","en":"English text"}}}]}]}
            """,
        };

        database.Contents.Add(content);
        await database.SaveChangesAsync(token);

        var sourceId = $"content:{content.Id}";
        Assert.Empty(await ProjectionsOf(database, sourceId, token));

        content.Status = PublishStatus.Published;
        content.PublishedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(token);

        var rows = await ProjectionsOf(database, sourceId, token);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Testo italiano", rows.Single(row => row.Locale == "it").Text);
        Assert.Equal("English text", rows.Single(row => row.Locale == "en").Text);
        Assert.Equal($"/{content.Slug}", rows[0].Url);

        // Taking a page back to draft takes it out of the index as well.
        content.Status = PublishStatus.Draft;
        await database.SaveChangesAsync(token);
        Assert.Empty(await ProjectionsOf(database, sourceId, token));
    }

    [Fact]
    public async Task VisibilityFilterPerRole()
    {
        var token = TestContext.Current.CancellationToken;
        var category = Guid.NewGuid().ToString("N");

        foreach (var visibility in new[] { Visibility.Public, Visibility.Members, Visibility.Staff, Visibility.Department })
        {
            var link = NewLink(Department.ED);
            link.Visibility = visibility;
            link.Category = category;
            await SeedAsSuperadminAsync(link, token);
        }

        _user.Anonymous();
        Assert.Equal([Visibility.Public], await VisibleAsync(category, token));

        _user.Member(700010);
        Assert.Equal([Visibility.Public, Visibility.Members], await VisibleAsync(category, token));

        // Staff of another department: everything but what belongs to the Events department.
        _user.Coordinator(700011, Department.FOD);
        Assert.Equal([Visibility.Public, Visibility.Members, Visibility.Staff], await VisibleAsync(category, token));

        _user.Coordinator(700012, Department.ED);
        Assert.Equal(
            [Visibility.Public, Visibility.Members, Visibility.Staff, Visibility.Department],
            await VisibleAsync(category, token));

        _user.Superadmin(SuperadminVid);
        Assert.Equal(4, (await VisibleAsync(category, token)).Count);
    }

    [Fact]
    public async Task AuthorizationHandlerIsTheOnlyOne()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IAuthorizationHandler>()
            .Where(handler => handler.GetType().Assembly.GetName().Name?.StartsWith("IvaoHub", StringComparison.Ordinal) == true)
            .ToArray();

        var handler = Assert.Single(handlers);
        Assert.IsType<DepartmentAuthorizationHandler>(handler);

        // And it is the one that decides, resource by resource.
        _user.Coordinator(StaffVid, Department.ED);
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));

        Assert.True((await authorization.AuthorizeAsync(principal, NewLink(Department.ED), CorePermissions.LinksEdit)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, NewLink(Department.FOD), CorePermissions.LinksEdit)).Succeeded);

        // Without a resource the question is only whether they hold the permission at all.
        Assert.True((await authorization.AuthorizeAsync(principal, resource: null, CorePermissions.LinksEdit)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, resource: null, CorePermissions.PermissionsManage)).Succeeded);
    }

    private static Link NewLink(Department department) => new()
    {
        OwnerDepartment = department,
        Visibility = Visibility.Public,
        Title = "Roma".L("Rome"),
        Description = "Il sito".L("The site"),
        Url = "https://www.ivao.aero",
        IsActive = true,
    };

    private static Task<List<SearchIndexEntry>> ProjectionsOf(HubDbContext database, string sourceId, CancellationToken token) =>
        database.SearchIndex.AsNoTracking()
            .Where(row => row.SourceModule == ProjectionSource.Core && row.SourceId == sourceId)
            .OrderBy(row => row.Locale)
            .ToListAsync(token);

    /// <summary>Seeds a row the way the maintainer of the division would: allowed everywhere.</summary>
    private async Task<long> SeedAsSuperadminAsync(Link link, CancellationToken token)
    {
        _user.Superadmin(SuperadminVid);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.Links.Add(link);
        await database.SaveChangesAsync(token);
        return link.Id;
    }

    private async Task<List<Visibility>> VisibleAsync(string category, CancellationToken token)
    {
        // A new scope, because the context reads the current user once, when it is built.
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        return await database.Links.AsNoTracking()
            .Where(link => link.Category == category)
            .OrderBy(link => link.Id)
            .Select(link => link.Visibility)
            .ToListAsync(token);
    }
}
