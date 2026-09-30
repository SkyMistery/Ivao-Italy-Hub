using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone once more (M4, E2b, note 2026-10-01-il-permesso-non-il-dipartimento): a grant to a position on a department that
/// is not the position's own gives the permission, not the department — decided by Carmine on #209 — on a real MariaDB, through
/// the host's own sign in and endpoints:
/// <list type="bullet">
/// <item>the advisors of the ATC operations, granted the test module's read permission on special operations, read the rows of
/// special operations in the generated list that reads with it, and one of them through the single handler; not a row of
/// another department;</item>
/// <item>and nothing else of special operations: no <c>dept</c> claim — so no group of that department in the back office —, no
/// row of it in another list, no row of it the global query filter keeps to the department, and none in the search;</item>
/// <item>a grant to a person on the same department still lets them in, as decided on 6 September (note
/// 2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento).</item>
/// </list>
/// The rows are the test module's (in the care of Events, its base department, and of the department each names) and the core's
/// links. Nobody seeded here has an address: no mail is sent, and the contacts tests keep their exact recipients.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PermissionFromOutsideTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the events module (CONTRIBUTING.md), 761090–761099 handed to E2b; 761090 is an identity of the unit tests.
    private const int CollaboratorVid = 761091;
    private const int GrantedByNameVid = 761092;

    private static readonly int[] SeededVids = [CollaboratorVid, GrantedByNameVid];

    /// <summary>
    /// What every row and grant written here says, so that one left behind by an interrupted run is found and taken away. A
    /// position's grant left behind would reach every advisor of the ATC operations of the next class.
    /// </summary>
    private const string Stem = "evt-test-e2b";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        await CleanAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(TestContext.Current.CancellationToken);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task APositionGrantedAReadPermissionOnAnotherDepartmentReadsItsRowsInThatListAndNothingElseOfIt()
    {
        var token = TestContext.Current.CancellationToken;
        var needle = $"varenna{Guid.NewGuid():N}"[..20];

        var special = await SeedItemAsync($"{Stem} special operations", Department.SOD, token);
        var flight = await SeedItemAsync($"{Stem} flight operations", Department.FOD, token);
        var theirs = await SeedLinkAsync(Department.AOD, $"{needle} atc", token);
        var notTheirs = await SeedLinkAsync(Department.SOD, $"{needle} special", token);

        // Whoever advises the ATC operations reads the test module on special operations: a position, on a department not its own.
        await SeedMemberAsync(CollaboratorVid, "IT-AOA7", token);
        await GrantToPositionAsync(Department.AOD, StaffLevel.Advisor, SampleModule.ViewPermission, Department.SOD, token);

        using var collaborator = await SignedInAsync(CollaboratorVid, token);

        // The permission is held there, and the department is not theirs: the back office draws the module's section and no
        // group of special operations (staffDestinations.ts reads user.departments).
        var me = await collaborator.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Equal([nameof(Department.AOD)], Strings(me.GetProperty("user").GetProperty("departments")));
        Assert.Contains(
            me.GetProperty("permissions").EnumerateArray(),
            held => held.GetProperty("name").GetString() == SampleModule.ViewPermission
                && held.GetProperty("department").GetString() == nameof(Department.SOD));
        Assert.Contains(
            me.GetProperty("navigation").GetProperty("staff").EnumerateArray(),
            entry => entry.GetProperty("path").GetString() == SampleModule.StaffNavigationPath);

        // The list that reads with that permission holds the rows of special operations, and not those of another department.
        var listed = await ItemsAsync(collaborator, token);
        Assert.Contains(special, listed);
        Assert.DoesNotContain(flight, listed);

        // So does the single handler, row by row, as it always did.
        using (var read = await collaborator.GetAsync(new Uri($"{SampleModule.ItemsPattern}/{special}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        using (var refused = await collaborator.GetAsync(new Uri($"{SampleModule.ItemsPattern}/{flight}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // Nothing else of special operations. Not a row of it in a list that reads with another permission, which they hold on
        // their own department: their link is there, the one of special operations is not.
        var links = await LinksAsync(collaborator, needle, token);
        Assert.Contains(theirs, links);
        Assert.DoesNotContain(notTheirs, links);

        // Not a row the global query filter keeps to special operations: neither the module's, nor the link in the search.
        Assert.DoesNotContain(special, await VisibleAsync(collaborator, token));
        var found = await SearchAsync(collaborator, needle, token);
        Assert.Contains($"{needle} atc", found);
        Assert.DoesNotContain($"{needle} special", found);
    }

    [Fact]
    public async Task AGrantToAPersonOnTheSameDepartmentStillLetsThemIn()
    {
        var token = TestContext.Current.CancellationToken;
        var needle = $"bellagio{Guid.NewGuid():N}"[..20];

        var special = await SeedItemAsync($"{Stem} special operations, by name", Department.SOD, token);
        var notTheirs = await SeedLinkAsync(Department.SOD, $"{needle} special", token);

        // The assistant of the ATC operations, granted the same permission on the same department by name.
        await SeedMemberAsync(GrantedByNameVid, "IT-AOAC", token);
        await GrantToMemberAsync(GrantedByNameVid, SampleModule.ViewPermission, Department.SOD, token);

        using var member = await SignedInAsync(GrantedByNameVid, token);

        // Inside special operations for what they see (note 2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento):
        // the department in the bootstrap, the rows of it in every list they read, the rows it keeps to itself.
        var me = await member.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Equal(
            [nameof(Department.AOD), nameof(Department.SOD)],
            Strings(me.GetProperty("user").GetProperty("departments")).Order(StringComparer.Ordinal));

        Assert.Contains(special, await ItemsAsync(member, token));
        Assert.Contains(notTheirs, await LinksAsync(member, needle, token));
        Assert.Contains(special, await VisibleAsync(member, token));
        Assert.Contains($"{needle} special", await SearchAsync(member, needle, token));
    }

    // ---- rows ---------------------------------------------------------------------------------------

    /// <summary>A row of the test module in the care of <paramref name="department"/>, and of Events, which the interceptor adds.</summary>
    private async Task<long> SeedItemAsync(string title, Department department, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var item = new SampleItem
        {
            Title = title,
            Visibility = Visibility.Department,
            OwnerDepartment = department,
            OwnerDepartmentMask = DepartmentMask.Of([department, Department.ED]),
        };

        database.Items.Add(item);
        await database.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    /// <summary>A link its department keeps to itself. The interceptor projects it into the search, in this transaction.</summary>
    private async Task<long> SeedLinkAsync(Department department, string title, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var link = new Link
        {
            OwnerDepartment = department,
            Visibility = Visibility.Department,
            Title = title.L(title),
            Url = $"https://example.org/{Stem}/{Guid.NewGuid():N}",
            IsActive = true,
        };

        database.Links.Add(link);
        await database.SaveChangesAsync(cancellationToken);
        return link.Id;
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
            LastName = "Outside",
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

    /// <summary>A grant to whoever holds a position of <paramref name="position"/> at <paramref name="level"/>, held on <paramref name="scope"/>.</summary>
    private Task GrantToPositionAsync(
        Department position,
        StaffLevel level,
        string permission,
        Department scope,
        CancellationToken cancellationToken) =>
        WriteGrantAsync(
            new UserGrant
            {
                PositionDepartment = position,
                PositionLevels = [level],
                Kind = GrantKind.Permission,
                Value = permission,
                Department = scope,
                Effect = GrantEffect.Grant,
                Reason = Stem,
            },
            cancellationToken);

    private Task GrantToMemberAsync(int vid, string permission, Department department, CancellationToken cancellationToken) =>
        WriteGrantAsync(
            new UserGrant
            {
                Vid = vid,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = department,
                Effect = GrantEffect.Grant,
                Reason = Stem,
            },
            cancellationToken);

    /// <summary>Written as the installation. A grant written changes its holders' stamp: each test signs in after it.</summary>
    private async Task WriteGrantAsync(UserGrant grant, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Add(grant);
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    // ---- what each of them reads --------------------------------------------------------------------

    /// <summary>The identifiers of the test module's generated list, newest first.</summary>
    private static async Task<long[]> ItemsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{SampleModule.ItemsPattern}?pageSize=100&dir=desc", cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];
    }

    /// <summary>The identifiers of the links' generated list, the ones that say the needle.</summary>
    private static async Task<long[]> LinksAsync(HttpClient client, string needle, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}?q={needle}&pageSize=100", cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(link => link.GetProperty("id").GetInt64())];
    }

    /// <summary>The test module's rows through the global query filter, as a page of the module reads them.</summary>
    private static async Task<long[]> VisibleAsync(HttpClient client, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<long[]>(SampleModule.VisiblePattern, cancellationToken) ?? [];

    /// <summary>The titles the search answers the needle with.</summary>
    private static async Task<string[]> SearchAsync(HttpClient client, string needle, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{SearchEndpoints.Pattern}?q={needle}&locale=it", cancellationToken);
        return [.. page.GetProperty("results").GetProperty("items").EnumerateArray().Select(item => item.GetProperty("title").GetString()!)];
    }

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(value => value.GetString()!)];

    // ---- the host ------------------------------------------------------------------------------------

    /// <summary>What this test left behind, and what an interrupted run of it left: taken away as the installation, before and after.</summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        await hub.UserGrants.Where(grant => grant.Reason == Stem).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => SeededVids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => SeededVids.Contains(user.Vid)).ExecuteDeleteAsync(cancellationToken);

        // Through the context, so that the interceptor takes their rows of the search away with them.
        var links = await hub.Links.IgnoreQueryFilters()
            .Where(link => link.Url.StartsWith($"https://example.org/{Stem}/"))
            .ToListAsync(cancellationToken);
        hub.Links.RemoveRange(links);
        await hub.SaveChangesAsync(cancellationToken);

        await sample.Items.IgnoreQueryFilters()
            .Where(item => item.Title.StartsWith(Stem))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
