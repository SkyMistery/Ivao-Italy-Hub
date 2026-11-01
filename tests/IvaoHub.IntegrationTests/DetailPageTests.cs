using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The generic engine's detail with the services of the request (#427, note 2026-11-01-le-azioni-di-un-evento-dal-server):
/// <c>CrudOptions.ToDetailPage</c> answers the read, the create and the update in place of <c>ToDetail</c>, so that a detail can
/// say what its reader may do on the row — here whether they decide it, asked of the single handler, which is what the page
/// of an event asks for its buttons. Proved through the real host, the real cookie and the test module's table:
/// <list type="bullet">
/// <item>a grant on one row says yes on that row and no on its neighbour, on the same list of the same department;</item>
/// <item>a row the reader is the stakeholder of says no, though the grant names it: the handler's rule, not one of the engine;</item>
/// <item>the create and the update answer with the same detail.</item>
/// </list>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class DetailPageTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // Given by dalberone for this phase (#427): nobody else's test person, never a super administrator.
    private const int ReaderVid = 761400;

    private readonly List<long> _items = [];
    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await ForgetGrantsAsync(token);
        await SeedReaderAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        // Nothing left behind: no grant another class of the same database could find on this VID, and no item.
        var token = TestContext.Current.CancellationToken;
        await ForgetGrantsAsync(token);

        if (_items.Count > 0)
        {
            await AsInstallationAsync(database => database.Items.IgnoreQueryFilters()
                .Where(row => _items.Contains(row.Id))
                .ExecuteDeleteAsync(token));
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheDetailSaysWhatTheReaderMayDoOnThatRowAndNotOnItsNeighbour()
    {
        var token = TestContext.Current.CancellationToken;
        var granted = await ItemAsync("core-test-427 granted", stakeholder: null, token);
        var neighbour = await ItemAsync("core-test-427 neighbour", stakeholder: null, token);
        var aboutThemselves = await ItemAsync("core-test-427 about the reader", stakeholder: ReaderVid, token);

        // Reading across the department; deciding the granted row and the one about themselves, and no other.
        await GrantAsync(SampleModule.ViewPermission, scope: null, token);
        await GrantAsync(SampleModule.DecidePermission, SampleItem.ScopeOf(granted), token);
        await GrantAsync(SampleModule.DecidePermission, SampleItem.ScopeOf(aboutThemselves), token);

        using var reader = await SignedInAsync(token);

        Assert.True(await DecidableAsync(reader, granted, token));
        Assert.False(await DecidableAsync(reader, neighbour, token));

        // Sample.Decide is denied to the stakeholder: the single handler says no, whatever the grant names.
        Assert.False(await DecidableAsync(reader, aboutThemselves, token));
    }

    [Fact]
    public async Task TheCreateAndTheUpdateAnswerWithTheSameDetail()
    {
        var token = TestContext.Current.CancellationToken;
        var granted = await ItemAsync("core-test-427 to change", stakeholder: null, token);

        await GrantAsync(SampleModule.ViewPermission, scope: null, token);
        await GrantAsync(SampleModule.EditPermission, scope: null, token);
        await GrantAsync(SampleModule.DecidePermission, SampleItem.ScopeOf(granted), token);

        using var reader = await SignedInAsync(token);

        using (var updated = await reader.PutAsJsonAsync(ItemUri(granted), Payload("core-test-427 changed"), token))
        {
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            var body = await updated.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal("core-test-427 changed", body.GetProperty("title").GetString());
            Assert.True(body.GetProperty("decidable").GetBoolean());
        }

        // A new row is in no grant's scope yet: the create says so with the same detail.
        using var created = await reader.PostAsJsonAsync(SampleModule.ActionsPattern, Payload("core-test-427 new"), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        _items.Add(detail.GetProperty("id").GetInt64());
        Assert.False(detail.GetProperty("decidable").GetBoolean());
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static Uri ItemUri(long id) =>
        new($"{SampleModule.ActionsPattern}/{id.ToString(CultureInfo.InvariantCulture)}", UriKind.Relative);

    private static object Payload(string title) => new
    {
        title,
        visibility = nameof(Visibility.Department),
        ownerDepartments = new[] { nameof(Department.ED) },
    };

    private static async Task<bool> DecidableAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var read = await client.GetAsync(ItemUri(id), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return (await read.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("decidable").GetBoolean();
    }

    /// <summary>An item of the module's base department, written by the installation, which the guard leaves alone.</summary>
    private async Task<long> ItemAsync(string title, int? stakeholder, CancellationToken cancellationToken)
    {
        var id = await AsInstallationAsync(async database =>
        {
            var row = new SampleItem
            {
                Title = title,
                StakeholderVid = stakeholder,
                Visibility = Visibility.Department,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            };

            database.Items.Add(row);
            await database.SaveChangesAsync(cancellationToken);
            return row.Id;
        });

        _items.Add(id);
        return id;
    }

    /// <summary>A grant to the reader on the department, across it or — with a scope — on one row, written straight to the table.</summary>
    private async Task GrantAsync(string permission, string? scope, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        database.UserGrants.Add(new UserGrant
        {
            Vid = ReaderVid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.ED,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = "test",
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task ForgetGrantsAsync(CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        await services.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
            .Where(row => row.Vid == ReaderVid)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>A member with no staff position: whatever they may do here, a grant of this test gave it.</summary>
    private async Task SeedReaderAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == ReaderVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = ReaderVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "Detail";
        user.IsStaff = true;
        user.IsSuperadmin = false;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in after the grants: the effective permissions are computed at login.</summary>
    private async Task<HttpClient> SignedInAsync(CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, ReaderVid, cancellationToken);
        return client;
    }

    /// <summary>Runs <paramref name="work"/> in a scope of its own with no request, as the installation itself.</summary>
    private async Task<T> AsInstallationAsync<T>(Func<SampleDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
        return await work(scope.ServiceProvider.GetRequiredService<SampleDbContext>());
    }
}
