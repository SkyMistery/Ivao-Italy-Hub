using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A menu entry behind a permission held without a resource scope (M4, E10u, note 2026-10-17-il-permesso-senza-ambito), on a real
/// MariaDB, through the host's own sign in and <c>/api/me</c>: a register of the whole division is not opened by a grant on one row,
/// nor is its menu entry (Carmine on #283, finding 1, answer (b)).
/// <list type="bullet">
/// <item>a grant to a person on one row of the test module's permission: the entry marked <c>Unscoped</c> is not in their menu, the
/// twin entry without the mark — and the test module's own — are, as every entry has always been;</item>
/// <item>a grant to a person on the department, with no scope: both entries are in their menu.</item>
/// </list>
/// The entries are a probe module's of this class, added to this class's host only, behind the test module's <c>Sample.View</c>; the
/// other holders (a department's staff, a position, a FIR team, the headquarters, a super administrator) are the unit tests'
/// (<c>UnscopedPermissionRulesTests</c>). Nobody seeded here has an address.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class UnscopedNavigationTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // Past the ranges E13b (761200–229) and E14a (761230–259) were handed; grepped free in tests/.
    private const int OneRowVid = 761260;
    private const int DepartmentVid = 761261;

    private static readonly int[] SeededVids = [OneRowVid, DepartmentVid];

    /// <summary>What every grant written here says, so that one left behind by an interrupted run is found and taken away.</summary>
    private const string Stem = "evt-test-e10u";

    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureServices((context, services) =>
            services.AddHubModule(new ProbeModule(), context.Configuration, new DivisionOptions())));

        await CleanAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    [Fact]
    public async Task AnEntryAskingForNoScopeIsHiddenFromAGrantOnOneRowAndShownToWhoeverHoldsItOnTheDepartment()
    {
        var token = TestContext.Current.CancellationToken;

        // One row of the test module, the way a grant on one event is: the permission is held, on that row only.
        await SeedMemberAsync(OneRowVid, "IT-AOA7", token);
        await GrantToMemberAsync(OneRowVid, SampleModule.ViewPermission, Department.ED, "sample:item:1", token);

        // The same permission on the department, the way its staff holds it.
        await SeedMemberAsync(DepartmentVid, "IT-AOA8", token);
        await GrantToMemberAsync(DepartmentVid, SampleModule.ViewPermission, Department.ED, scope: null, token);

        using (var oneRow = await SignedInAsync(OneRowVid, token))
        {
            var me = await oneRow.GetFromJsonAsync<JsonElement>("/api/me", token);
            Assert.Contains(
                me.GetProperty("permissions").EnumerateArray(),
                held => held.GetProperty("name").GetString() == SampleModule.ViewPermission);

            var staff = StaffPaths(me);
            Assert.DoesNotContain(ProbeModule.UnscopedPath, staff);

            // The default is the entry every module has always had: held anywhere is enough.
            Assert.Contains(ProbeModule.PlainPath, staff);
            Assert.Contains(SampleModule.StaffNavigationPath, staff);
        }

        using var department = await SignedInAsync(DepartmentVid, token);
        var theirs = StaffPaths(await department.GetFromJsonAsync<JsonElement>("/api/me", token));
        Assert.Contains(ProbeModule.UnscopedPath, theirs);
        Assert.Contains(ProbeModule.PlainPath, theirs);
    }

    /// <summary>Two staff entries behind the test module's read permission, one of them only for whoever holds it without a scope.</summary>
    private sealed class ProbeModule : ModuleBase
    {
        public const string UnscopedPath = "/staff/unscoped-probe/register";

        public const string PlainPath = "/staff/unscoped-probe/list";

        public override string Key => "unscopedprobe";

        public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
        [
            new NavItemDescriptor("unscopedprobe.nav.register", UnscopedPath, SampleModule.ViewPermission, Unscoped: true),
            new NavItemDescriptor("unscopedprobe.nav.list", PlainPath, SampleModule.ViewPermission),
        ];
    }

    // ---- people and grants --------------------------------------------------------------------------

    /// <summary>A member of hub_users with one position of this division, as a sign in would have left them. No address.</summary>
    private async Task SeedMemberAsync(int vid, string position, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.Users.Add(new HubUser
        {
            Vid = vid,
            FirstName = "Test",
            LastName = "Unscoped",
            IsStaff = true,
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });

        var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>())!;
        database.UserStaffPositions.Add(new UserStaffPosition
        {
            Vid = vid,
            Position = position,
            Department = parsed.Department,
            Level = parsed.Level,
            Fir = parsed.Fir,
            SyncedAt = clock.UtcNow,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Written as the installation, before the member signs in.</summary>
    private async Task GrantToMemberAsync(
        int vid,
        string permission,
        Department department,
        string? scope,
        CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = department,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = Stem,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    private static string[] StaffPaths(JsonElement me) =>
        [.. me.GetProperty("navigation").GetProperty("staff").EnumerateArray().Select(entry => entry.GetProperty("path").GetString()!)];

    /// <summary>What this test left behind, and what an interrupted run of it left: taken away as the installation, before and after.</summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        await hub.UserGrants.Where(grant => grant.Reason == Stem).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => SeededVids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => SeededVids.Contains(user.Vid)).ExecuteDeleteAsync(cancellationToken);
    }
}
