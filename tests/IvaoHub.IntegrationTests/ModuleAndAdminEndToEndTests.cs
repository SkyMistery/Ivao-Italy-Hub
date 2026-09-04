using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Atc;
using IvaoHub.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What F8 adds, over the wire and on a real database: the core composing a module's contributions,
/// maintenance closing a module for writing, the administration screens on the CRUD engine's global
/// mode, and a grant that bites on the very next request (design M0 sections 6.1 and 3.9).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ModuleAndAdminEndToEndTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int DirectorVid = 620001;
    private const int EventsCoordinatorVid = 620002;
    private const int MemberVid = 620003;
    private const int SuperadminVid = 620004;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    // --- the module registry ------------------------------------------------------------------

    [Fact]
    public async Task ModuleRegistryComposesNavAndExclusions()
    {
        var token = TestContext.Current.CancellationToken;

        using var client = WritingClient();
        var body = await client.GetFromJsonAsync<JsonElement>("/api/me", token);

        // The module is listed, with its department, enabled and open.
        var module = body.GetProperty("modules").EnumerateArray().Single();
        Assert.Equal(AtcModule.ModuleKey, module.GetProperty("key").GetString());
        Assert.Equal(nameof(Department.AOD), module.GetProperty("department").GetString());
        Assert.True(module.GetProperty("enabled").GetBoolean());
        Assert.False(module.GetProperty("maintenance").GetBoolean());

        // Its menu entry is composed with the core's, as a translation key and not as a phrase.
        var publicNavigation = body.GetProperty("navigation").GetProperty("public").EnumerateArray()
            .Select(entry => (entry.GetProperty("key").GetString(), entry.GetProperty("path").GetString()))
            .ToArray();

        Assert.Equal(("nav.home", "/"), publicNavigation[0]);
        Assert.Contains(("nav.atc", "/atc"), publicNavigation);

        // And its endpoints are mapped, under its own prefix and nowhere else.
        var ping = await client.GetFromJsonAsync<JsonElement>($"/api/{AtcModule.ModuleKey}/ping", token);
        Assert.Equal(AtcModule.ModuleKey, ping.GetProperty("module").GetString());
    }

    [Fact]
    public async Task TheSpaDoesNotAnswerForWhatAModuleExcluded()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = WritingClient();

        // The atc module declares /services/vsop: while vIPI still answers for it behind the same
        // host, the single page application must hand it back rather than draw its own 404 over
        // something that exists.
        using var excluded = await client.GetAsync(new Uri("/services/vsop/whatever", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.NotFound, excluded.StatusCode);

        // An address the SPA does own reaches the fallback, which in a test host has no index.html
        // to serve and answers 404 as well -- what is being fixed here is that the two paths take
        // different branches, so the assertion is on the exclusion list itself.
        var registry = _factory.Services.GetRequiredService<ModuleRegistry>();
        Assert.Contains("/services/vsop", registry.SpaFallbackExclusions);
        Assert.Contains("/vsop", registry.SpaFallbackExclusions);
    }

    // --- maintenance --------------------------------------------------------------------------

    [Fact]
    public async Task MaintenanceReturns503OnWrites()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);

        using var client = WritingClient();
        await _factory.SignInAsync(client, DirectorVid, token);

        // Open: a write to an address the module does not have is a 404 or a 405, never a 503.
        using var beforeWrite = await client.PostAsync(
            new Uri($"/api/{AtcModule.ModuleKey}/ping", UriKind.Relative),
            content: null,
            token);
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, beforeWrite.StatusCode);

        using var closing = await client.PutAsJsonAsync(
            $"{ModuleAdminEndpoints.Pattern}/{AtcModule.ModuleKey}/maintenance",
            new { maintenance = true },
            token);
        Assert.Equal(HttpStatusCode.NoContent, closing.StatusCode);

        // Reads still work: a department reorganising its data wants nobody to change anything,
        // not its pages to go blank.
        using var read = await client.GetAsync(new Uri($"/api/{AtcModule.ModuleKey}/ping", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // Writes do not, and the refusal names the module and carries an i18n key resolved into a
        // sentence in the language of the caller.
        using var write = await client.PostAsync(
            new Uri($"/api/{AtcModule.ModuleKey}/ping", UriKind.Relative),
            content: null,
            token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, write.StatusCode);

        var problem = await write.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(AtcModule.ModuleKey, problem.GetProperty("module").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));

        // The core is untouched: maintenance is per module, not a switch on the whole site.
        using var core = await client.GetAsync(new Uri(LinksEndpoints.Pattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, core.StatusCode);

        // And the flip left an audit row, written by the interceptor because DivisionSetting is
        // [Audited] -- no service of ours writes one by hand.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var key = ModuleRegistry.MaintenanceKey(AtcModule.ModuleKey);

            Assert.True(await database.AuditLog.AnyAsync(
                entry => entry.Entity == "hub_division_settings" && entry.EntityId == key && entry.Vid == DirectorVid,
                token));
        }

        // Reopened, so the rest of the suite is not left looking at a closed module.
        using var reopening = await client.PutAsJsonAsync(
            $"{ModuleAdminEndpoints.Pattern}/{AtcModule.ModuleKey}/maintenance",
            new { maintenance = false },
            token);
        Assert.Equal(HttpStatusCode.NoContent, reopening.StatusCode);
    }

    [Fact]
    public async Task MaintenanceIsRefusedToWhoeverDoesNotAdministerModules()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = WritingClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        using var response = await client.PutAsJsonAsync(
            $"{ModuleAdminEndpoints.Pattern}/{AtcModule.ModuleKey}/maintenance",
            new { maintenance = true },
            token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClosingAModuleThisBuildDoesNotHaveIsANotFound()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);

        using var client = WritingClient();
        await _factory.SignInAsync(client, DirectorVid, token);

        using var response = await client.PutAsJsonAsync(
            $"{ModuleAdminEndpoints.Pattern}/nothing-like-this/maintenance",
            new { maintenance = true },
            token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- grants -------------------------------------------------------------------------------

    [Fact]
    public async Task GrantsEndpointEnforcesStaffOnly()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedUserAsync(MemberVid, cancellationToken: token);

        using var client = WritingClient();
        await _factory.SignInAsync(client, DirectorVid, token);

        // A member who has logged in but holds no staff position of this division: the roster of
        // the hub is wider than its staff, and a grant only goes to the staff.
        using var toAMember = await client.PostAsJsonAsync(
            GrantEndpoints.Pattern,
            Grant(MemberVid, "Links.Edit", nameof(Department.FOD)),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, toAMember.StatusCode);
        Assert.Equal("errors.grant.notStaff", await FirstErrorAsync(toAMember, "vid", token));

        // A permission with no department cannot be handed out at all: who administers the hub is
        // decided by the positions IVAO publishes, and a grant is not a way around that.
        using var global = await client.PostAsJsonAsync(
            GrantEndpoints.Pattern,
            Grant(DirectorVid, "Permissions.Manage", department: null),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, global.StatusCode);
        Assert.Equal("errors.grant.globalPermission", await FirstErrorAsync(global, "value", token));

        // And a name the catalogue does not know is a refusal, not a row that quietly does nothing.
        using var unknown = await client.PostAsJsonAsync(
            GrantEndpoints.Pattern,
            Grant(DirectorVid, "Invented.Edit", nameof(Department.FOD)),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("errors.grant.unknownPermission", await FirstErrorAsync(unknown, "value", token));
    }

    [Fact]
    public async Task AGrantReachesTheNextRequestAndItsRemovalTheOneAfter()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        var flightOpsLink = await SeedLinkAsync(Department.FOD, "grant-target", token);

        using var coordinator = WritingClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);

        // The coordinator of events, on a link of flight operations: not theirs.
        using var before = await coordinator.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{flightOpsLink}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        using var director = WritingClient();
        await _factory.SignInAsync(director, DirectorVid, token);

        using var granting = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern,
            Grant(EventsCoordinatorVid, "Links.Edit", nameof(Department.FOD)),
            token);
        Assert.Equal(HttpStatusCode.Created, granting.StatusCode);

        // The very next request, with the cookie they were already carrying, is refused: UserGrant
        // is IAffectsUserSession, so the interceptor gave that VID a fresh security stamp inside the
        // same transaction as the grant, and OnValidatePrincipal rejects a cookie carrying the old
        // one (design M0 section 3.3). This is what "immediately" means -- the stale session stops
        // working at once rather than at the end of its twelve hours.
        using var stale = await coordinator.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{flightOpsLink}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);

        // Signed in again -- in a browser this is the round trip to IVAO, which is silent for
        // somebody who has already consented -- the grant is in the cookie and the row is theirs.
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);
        using var after = await coordinator.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{flightOpsLink}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        var created = await granting.Content.ReadFromJsonAsync<JsonElement>(token);
        var grantId = created.GetProperty("id").GetInt64();

        using var revoking = await director.DeleteAsync(
            new Uri($"{GrantEndpoints.Pattern}/{grantId}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.NoContent, revoking.StatusCode);

        using var revokedStale = await coordinator.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{flightOpsLink}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedStale.StatusCode);

        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);
        using var revoked = await coordinator.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{flightOpsLink}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Fact]
    public async Task OnlyWhoeverAdministersPermissionsReadsTheGrants()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var anonymous = WritingClient();
        using var challenged = await anonymous.GetAsync(new Uri(GrantEndpoints.Pattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Unauthorized, challenged.StatusCode);

        using var coordinator = WritingClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);
        using var refused = await coordinator.GetAsync(new Uri(GrantEndpoints.Pattern, UriKind.Relative), token);

        // A coordinator holds every departmental permission on their own department and none of the
        // global ones: reading who holds what is one of those.
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    // --- the audit log ------------------------------------------------------------------------

    [Fact]
    public async Task TheAuditLogIsReadableAndNotWritable()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedLinkAsync(Department.ED, "audited-link", token);

        using var client = WritingClient();
        await _factory.SignInAsync(client, DirectorVid, token);

        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{AuditEndpoints.Pattern}?sort=At&dir=desc&filter[entity]=cms_links",
            token);

        Assert.True(page.GetProperty("total").GetInt32() > 0);
        Assert.All(
            page.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("cms_links", item.GetProperty("entity").GetString()));

        // ReadOnly maps the two reads and nothing else, so there is no route to write with: the
        // record of what happened cannot be edited by the people it is about. Whether the router
        // answers "no such address" or "not that verb" is its own business; either way there is
        // nothing there, and neither is a 201.
        using var written = await client.PostAsJsonAsync(AuditEndpoints.Pattern, new { }, token);
        Assert.True(
            written.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"a write on the audit log answered {written.StatusCode}");
    }

    // --- super administrators -----------------------------------------------------------------

    [Fact]
    public async Task OnlyASuperAdministratorSeesOrChangesTheSuperAdministrators()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        using var director = WritingClient();
        await _factory.SignInAsync(director, DirectorVid, token);

        // The director holds every global permission of the catalogue, and it still is not enough:
        // there is nothing above Permissions.Manage on purpose.
        using var refused = await director.GetAsync(
            new Uri(GrantEndpoints.SuperadminPattern, UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var superadmin = WritingClient();
        await _factory.SignInAsync(superadmin, SuperadminVid, token);

        var listed = await superadmin.GetFromJsonAsync<int[]>(GrantEndpoints.SuperadminPattern, token);
        Assert.Contains(SuperadminVid, listed!);

        // A VID nobody has ever seen: the roster of the hub is exactly the people who have logged
        // in at least once, so this is a typo rather than somebody to promote.
        using var never = await superadmin.PostAsync(
            new Uri($"{GrantEndpoints.SuperadminPattern}/999999", UriKind.Relative),
            content: null,
            token);

        Assert.Equal(HttpStatusCode.BadRequest, never.StatusCode);
        Assert.Equal("errors.superadmin.neverLoggedIn", await FirstErrorAsync(never, "vid", token));
    }

    // --- helpers ------------------------------------------------------------------------------

    /// <summary>
    /// A client that can write. Every state changing call under <c>/api</c> has to carry the header
    /// our own client always sends: a cross site form can post with the cookie attached, but it
    /// cannot set a header (design M0 section 6.4). A test that forgot it would be testing the
    /// guard rather than the endpoint, which is what six 403s looked like the first time.
    /// </summary>
    private HttpClient WritingClient()
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", HubPipeline.RequestedWithValue);
        return client;
    }

    private static object Grant(int vid, string value, string? department) => new
    {
        vid,
        kind = nameof(GrantKind.Permission),
        value,
        department,
        effect = nameof(GrantEffect.Grant),
        expiresAt = (string?)null,
        reason = "test",
        rowVersion = "0001-01-01T00:00:00",
    };

    private static async Task<string?> FirstErrorAsync(
        HttpResponseMessage response,
        string field,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return problem.GetProperty("errors").GetProperty(field).EnumerateArray().First().GetString();
    }

    private async Task<long> SeedLinkAsync(Department department, string slug, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var link = new Link
        {
            OwnerDepartment = department,
            Visibility = Visibility.Public,
            Title = slug.L(slug),
            Url = $"https://example.org/{slug}",
            IsActive = true,
        };

        database.Links.Add(link);
        await database.SaveChangesAsync(cancellationToken);
        return link.Id;
    }

    private async Task SeedUserAsync(
        int vid,
        bool isSuperadmin = false,
        string? position = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsSuperadmin = isSuperadmin;
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
                row => row.Vid == vid && row.Position == position,
                cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
