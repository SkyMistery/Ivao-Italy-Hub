using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone, extended with the case of a row in the care of two departments (M2, note
/// 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3), on the table of the test module:
/// <list type="bullet">
/// <item>a row always has the base department of its module, whoever created it;</item>
/// <item>a permission held on one of its departments is held on the row — to read it, to change it,
/// to find it in a list — and one held on none of them is not;</item>
/// <item>whoever creates a row puts in at least one department they hold the permission on;</item>
/// <item>and the global query filter of a module context reads the set as the hub's reads one.</item>
/// </list>
/// The permissions of the module are given to positions, as a division will give them (H1).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SeveralDepartmentsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own, and assistant positions nothing else in the suite gives a module permission to.
    private const int EventsAssistantVid = 760001;
    private const int SpecialOpsAssistantVid = 760002;
    private const int FlightOpsAssistantVid = 760003;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(EventsAssistantVid, "IT-EAC", token);
        await SeedUserAsync(SpecialOpsAssistantVid, "IT-SOAC", token);
        await SeedUserAsync(FlightOpsAssistantVid, "IT-FOAC", token);

        foreach (var department in new[] { Department.ED, Department.SOD, Department.FOD })
        {
            await GrantToAssistantsAsync(department, token);
        }
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ARowOfAModuleIsInTheCareOfItsBaseDepartmentAndOfWhoeverCollaborates()
    {
        var token = TestContext.Current.CancellationToken;

        using var specialOps = await SignedInAsync(SpecialOpsAssistantVid, token);
        using var events = await SignedInAsync(EventsAssistantVid, token);
        using var flightOps = await SignedInAsync(FlightOpsAssistantVid, token);

        // Special operations creates it, naming itself: the row is Events' too, without anybody saying so.
        using var created = await specialOps.PostAsJsonAsync(SampleModule.ItemsPattern, Item("Night ops", Department.SOD), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var row = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        var id = row.GetProperty("id").GetInt64();
        Assert.Equal(["SOD", "ED"], Departments(row));

        // Events manages it with the permission on its own department, and so does special operations.
        using (var edited = await events.PutAsJsonAsync($"{SampleModule.ItemsPattern}/{id}", Item("Night ops, edited", Department.SOD), token))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        using (var read = await specialOps.GetAsync(new Uri($"{SampleModule.ItemsPattern}/{id}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        // Flight operations is in none of its departments: not in the list, not writable.
        Assert.DoesNotContain(id, await IdsAsync(flightOps, token));
        Assert.Contains(id, await IdsAsync(specialOps, token));
        Assert.Contains(id, await IdsAsync(events, token));

        using (var refused = await flightOps.PutAsJsonAsync($"{SampleModule.ItemsPattern}/{id}", Item("Taken over", Department.FOD), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
    }

    [Fact]
    public async Task WhoeverCreatesARowPutsInADepartmentTheyHoldThePermissionOn()
    {
        var token = TestContext.Current.CancellationToken;
        using var flightOps = await SignedInAsync(FlightOpsAssistantVid, token);

        // Special operations only, written by flight operations: Events is added, and flight
        // operations holds the permission on neither.
        using var refused = await flightOps.PostAsJsonAsync(SampleModule.ItemsPattern, Item("Not ours", Department.SOD), token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var accepted = await flightOps.PostAsJsonAsync(SampleModule.ItemsPattern, Item("Ours too", Department.FOD, Department.SOD), token);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(["SOD", "FOD", "ED"], Departments(await accepted.Content.ReadFromJsonAsync<JsonElement>(token)));
    }

    [Fact]
    public async Task TheQueryFilterOfAModuleContextReadsTheSet()
    {
        var token = TestContext.Current.CancellationToken;

        using var specialOps = await SignedInAsync(SpecialOpsAssistantVid, token);
        using var flightOps = await SignedInAsync(FlightOpsAssistantVid, token);
        using var anonymous = _factory.CreateApiClient();

        using var created = await specialOps.PostAsJsonAsync(SampleModule.ItemsPattern, Item("Department only", Department.SOD), token);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        Assert.Contains(id, await VisibleAsync(specialOps, token));
        Assert.DoesNotContain(id, await VisibleAsync(flightOps, token));
        Assert.DoesNotContain(id, await VisibleAsync(anonymous, token));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static object Item(string title, params Department[] departments) => new
    {
        title,
        visibility = nameof(Visibility.Department),
        ownerDepartments = departments.Select(department => department.ToString()).ToArray(),
    };

    private static string[] Departments(JsonElement row) =>
        [.. row.GetProperty("ownerDepartments").EnumerateArray().Select(department => department.GetString()!)];

    private static async Task<long[]> IdsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{SampleModule.ItemsPattern}?pageSize=100", cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];
    }

    private static async Task<long[]> VisibleAsync(HttpClient client, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<long[]>(SampleModule.VisiblePattern, cancellationToken) ?? [];

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>The module's edit permission to the assistants of a department, on that department.</summary>
    private async Task GrantToAssistantsAsync(Department department, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var existing = await database.UserGrants.AnyAsync(
            grant => grant.PositionDepartment == department && grant.Value == SampleModule.EditPermission,
            cancellationToken);

        if (!existing)
        {
            database.UserGrants.Add(new UserGrant
            {
                PositionDepartment = department,
                PositionLevels = [StaffLevel.Assistant],
                Kind = GrantKind.Permission,
                Value = SampleModule.EditPermission,
                Department = department,
                Effect = GrantEffect.Grant,
                Reason = "test",
            });
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedUserAsync(int vid, string position, CancellationToken cancellationToken)
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
        user.LastName = "Assistant";
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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
