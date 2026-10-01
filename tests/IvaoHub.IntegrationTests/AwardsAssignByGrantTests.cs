using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// <c>Awards.Assign</c> given by a grant (M4, E10f, note <c>decisions/2026-10-01-chi-assegna-gli-award-con-un-grant.md</c>, decided by
/// Carmine on #205), through the real host: the division's own <c>positionGrants</c> give it to the coordinator and the assistant of
/// the membership department, who open the award queue, are among whoever the daily mail of E10d is for, and hold nothing else
/// global; the permissions screen gives it only whole; the seed skips it with a scope; a module's own grants never give it.
/// <para>Nobody here has an address: <c>ContactsAndNotificationsTests</c> counts the recipients of the membership department exactly
/// (CONTRIBUTING.md, "Tests"). So the mail itself is not sent here — <c>AwardQueueMailTests</c> proves it goes to whoever holds the
/// permission, and this class that the membership department holds it.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AwardsAssignByGrantTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // M4's range, E10f's share of it (761080–761089).
    private const int MembershipCoordinatorVid = 761080;
    private const int MembershipAssistantVid = 761081;
    private const int MembershipAdvisorVid = 761082;
    private const int DirectorVid = 761083;

    private static readonly int[] People = [MembershipCoordinatorVid, MembershipAssistantVid, MembershipAdvisorVid, DirectorVid];

    /// <summary>A department whose positions nobody else in the suite gives a grant to, nor counts the recipients of.</summary>
    private const Department Elsewhere = Department.SOD;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // With the fixtures: a host started on a database whose snapshot of IVAO is still empty would otherwise ask IVAO for it
        // (E10b's handoff), and no test calls IVAO.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(MembershipCoordinatorVid, "IT-MC", token);
        await SeedUserAsync(MembershipAssistantVid, "IT-MAC", token);
        await SeedUserAsync(MembershipAdvisorVid, "IT-MA1", token);
        await SeedUserAsync(DirectorVid, "IT-DIR", token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = CancellationToken.None;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // Their grants and positions go with them, so that no later class finds heads of the membership department it did not
            // seed among whoever holds Awards.Assign.
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await hub.UserGrants.Where(grant => grant.Vid != null && People.Contains(grant.Vid.Value)).ExecuteDeleteAsync(token);
            await hub.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync(token);
            await hub.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsStaff, false), token);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheDivisionGivesItToTheHeadsOfTheMembershipDepartmentAndNothingElseGlobal()
    {
        var token = TestContext.Current.CancellationToken;
        var catalogue = _factory.Services.GetRequiredService<PermissionCatalog>();

        // The grant of config/division.json, applied when a host first started on this database: not skipped as global.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var seeded = await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants.AsNoTracking()
                .Where(grant => grant.Reason == "division.json"
                    && grant.Value == CorePermissions.AwardsAssign
                    && grant.PositionDepartment == Department.MD)
                .ToListAsync(token);

            var grant = Assert.Single(seeded);
            Assert.Equal([StaffLevel.Coordinator, StaffLevel.Assistant], grant.PositionLevels);
            Assert.Null(grant.Department);
            Assert.Null(grant.ResourceScope);
            Assert.Equal(GrantEffect.Grant, grant.Effect);
        }

        foreach (var vid in new[] { MembershipCoordinatorVid, MembershipAssistantVid })
        {
            using var head = await SignedInAsync(vid, token);
            var me = await head.GetFromJsonAsync<JsonElement>("/api/me", token);
            var held = me.GetProperty("permissions").EnumerateArray().ToList();

            // Held with no department, the one global permission they hold, and nobody became a super administrator.
            var assign = Assert.Single(held, permission => permission.GetProperty("name").GetString() == CorePermissions.AwardsAssign);
            Assert.Equal(JsonValueKind.Null, assign.GetProperty("department").ValueKind);
            Assert.Equal(
                new[] { CorePermissions.AwardsAssign },
                held.Select(permission => permission.GetProperty("name").GetString()!).Where(catalogue.IsGlobal).Distinct());
            Assert.False(me.GetProperty("user").GetProperty("isSuperadmin").GetBoolean());

            // Inside their own department and no other: a grant of a global permission takes nobody into one (the widening the
            // session of E2b found in E10f's first draft, where they were inside all nine).
            AssertInsideOnly(me, Department.MD);

            // The queue and the register open; the permissions screen does not.
            Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(head, AwardEndpoints.SignalsPattern, token));
            Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(head, AwardEndpoints.AssignmentsPattern, token));
            Assert.Equal(HttpStatusCode.Forbidden, await StatusOfAsync(head, GrantEndpoints.Pattern, token));
        }

        // An advisor of the same department is not given it: the levels are the division's.
        using (var advisor = await SignedInAsync(MembershipAdvisorVid, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, await StatusOfAsync(advisor, AwardEndpoints.SignalsPattern, token));
        }

        // Among whoever the daily mail of E10d is for: the very question its job asks.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var holders = await scope.ServiceProvider.GetRequiredService<IPermissionHolders>()
                .HoldersOfAsync(CorePermissions.AwardsAssign, token);

            Assert.Contains(holders, holder => holder.Vid == MembershipCoordinatorVid && !holder.IsSuperadmin);
            Assert.Contains(holders, holder => holder.Vid == MembershipAssistantVid && !holder.IsSuperadmin);
            Assert.DoesNotContain(holders, holder => holder.Vid == MembershipAdvisorVid);
        }
    }

    [Fact]
    public async Task ThePermissionsScreenGivesItOnlyWhole()
    {
        var token = TestContext.Current.CancellationToken;
        using var director = await SignedInAsync(DirectorVid, token);

        // To a member of the staff by VID: accepted, and it bites at their next sign in.
        using var toTheAdvisor = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern, ToAPerson(MembershipAdvisorVid, CorePermissions.AwardsAssign, department: null), token);
        Assert.Equal(HttpStatusCode.Created, toTheAdvisor.StatusCode);
        var toTheAdvisorId = (await toTheAdvisor.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        try
        {
            using var advisor = await SignedInAsync(MembershipAdvisorVid, token);
            Assert.Equal(HttpStatusCode.OK, await StatusOfAsync(advisor, AwardEndpoints.SignalsPattern, token));

            // A grant to a person takes them into no department either.
            AssertInsideOnly(await advisor.GetFromJsonAsync<JsonElement>("/api/me", token), Department.MD);
        }
        finally
        {
            using var revoking = await director.DeleteAsync(new Uri($"{GrantEndpoints.Pattern}/{toTheAdvisorId}", UriKind.Relative), token);
            Assert.Equal(HttpStatusCode.NoContent, revoking.StatusCode);
        }

        // To a position: accepted as well.
        using var toAPosition = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern, ToAPosition(Elsewhere, CorePermissions.AwardsAssign, department: null), token);
        Assert.Equal(HttpStatusCode.Created, toAPosition.StatusCode);
        var toAPositionId = (await toAPosition.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();
        using (var revoking = await director.DeleteAsync(new Uri($"{GrantEndpoints.Pattern}/{toAPositionId}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, revoking.StatusCode);
        }

        // With a department: it would look like a limit that nothing applies.
        using var onADepartment = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern, ToAPerson(MembershipAdvisorVid, CorePermissions.AwardsAssign, nameof(Department.MD)), token);
        Assert.Equal(HttpStatusCode.BadRequest, onADepartment.StatusCode);
        Assert.Equal("errors.grant.globalDepartment", await FirstErrorAsync(onADepartment, "department", token));

        // To the team of a FIR: no row of the awards says its FIR.
        using var toTheTeamOfAFir = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern,
            new
            {
                vid = (int?)null,
                positionFirTeam = true,
                positionLevels = new[] { nameof(StaffLevel.Coordinator) },
                kind = nameof(GrantKind.Permission),
                value = CorePermissions.AwardsAssign,
                department = (string?)null,
                effect = nameof(GrantEffect.Grant),
                rowVersion = "0001-01-01T00:00:00",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, toTheTeamOfAFir.StatusCode);
        Assert.Equal("errors.grant.firTeamArea", await FirstErrorAsync(toTheTeamOfAFir, "value", token));

        // And the other global permissions stay closed, to a person as to a position.
        using var manage = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern, ToAPerson(MembershipAdvisorVid, CorePermissions.PermissionsManage, department: null), token);
        Assert.Equal(HttpStatusCode.BadRequest, manage.StatusCode);
        Assert.Equal("errors.grant.globalPermission", await FirstErrorAsync(manage, "value", token));

        using var admin = await director.PostAsJsonAsync(
            GrantEndpoints.Pattern, ToAPosition(Department.MD, CorePermissions.AdminAccess, department: null), token);
        Assert.Equal(HttpStatusCode.BadRequest, admin.StatusCode);
        Assert.Equal("errors.grant.globalPermission", await FirstErrorAsync(admin, "value", token));
    }

    [Fact]
    public async Task TheSeedGivesItWithNoScopeAndSkipsTheRest()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        // As in TheGrantsToPositionsOfDivisionJsonAreAppliedOnce: what the host remembered is put back at the end, or the next host
        // to start would apply the grants of the file a second time.
        var remembered = await database.DivisionSettings.AsNoTracking()
            .Where(row => row.Key == PositionGrantSeeder.AppliedSettingKey)
            .Select(row => row.ValueJson)
            .FirstOrDefaultAsync(token);
        await database.DivisionSettings.Where(row => row.Key == PositionGrantSeeder.AppliedSettingKey).ExecuteDeleteAsync(token);

        try
        {
            var seeder = new PositionGrantSeeder(
                database,
                Options.Create(new DivisionOptions
                {
                    PositionGrants =
                    [
                        new PositionGrantSeed { Department = Elsewhere, Levels = [StaffLevel.Coordinator], Permission = CorePermissions.AwardsAssign },

                        // Skipped: a global permission is held everywhere or not at all.
                        new PositionGrantSeed
                        {
                            Department = Elsewhere,
                            Levels = [StaffLevel.Assistant],
                            Permission = CorePermissions.AwardsAssign,
                            Scope = Elsewhere,
                        },

                        // Skipped: the team of a FIR, for an area whose rows say no FIR.
                        new PositionGrantSeed { FirTeam = true, Levels = [StaffLevel.Coordinator], Permission = CorePermissions.AwardsAssign },

                        // Skipped: never the right to hand out permissions.
                        new PositionGrantSeed { Department = Elsewhere, Levels = [StaffLevel.Advisor], Permission = CorePermissions.PermissionsManage },
                    ],
                }),
                scope.ServiceProvider.GetRequiredService<PermissionCatalog>(),
                scope.ServiceProvider.GetRequiredService<IClock>(),
                NullLogger<PositionGrantSeeder>.Instance);

            Assert.Equal(1, await seeder.SeedAsync(token));

            var seeded = await database.UserGrants.AsNoTracking()
                .Where(grant => grant.Reason == "division.json"
                    && (grant.PositionDepartment == Elsewhere || (grant.PositionFirTeam && grant.Value == CorePermissions.AwardsAssign)))
                .ToListAsync(token);

            var grant = Assert.Single(seeded);
            Assert.Equal(CorePermissions.AwardsAssign, grant.Value);
            Assert.Equal([StaffLevel.Coordinator], grant.PositionLevels);
            Assert.Null(grant.Department);

            // The ones skipped are not remembered, so a file put right applies at the next start; this one skips them again.
            Assert.Equal(0, await seeder.SeedAsync(token));
        }
        finally
        {
            await database.UserGrants
                .Where(grant => grant.Reason == "division.json" && grant.PositionDepartment == Elsewhere)
                .ExecuteDeleteAsync(token);

            var setting = await database.DivisionSettings.SingleOrDefaultAsync(row => row.Key == PositionGrantSeeder.AppliedSettingKey, token);
            if (remembered is null)
            {
                if (setting is not null)
                {
                    database.DivisionSettings.Remove(setting);
                }
            }
            else if (setting is not null)
            {
                setting.ValueJson = remembered;
            }

            await database.SaveChangesAsync(token);
        }
    }

    [Fact]
    public async Task AModulesOwnGrantsNeverGiveIt()
    {
        // A grant written from a module's screen is on a department and may be on one row, which a global permission has not got.
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var grants = scope.ServiceProvider.GetRequiredService<ModuleGrants>();

        Assert.Equal(
            "errors.grant.globalPermission",
            await grants.GiveAsync(MembershipAdvisorVid, CorePermissions.AwardsAssign, Department.MD, resourceScope: null, "test", token));
        Assert.Equal(
            "errors.grant.globalPermission",
            await grants.GiveAsync(MembershipAdvisorVid, CorePermissions.AwardsAssign, Department.FOD, "flightops:tour:42", "test", token));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static object ToAPerson(int vid, string value, string? department) => new
    {
        vid,
        kind = nameof(GrantKind.Permission),
        value,
        department,
        effect = nameof(GrantEffect.Grant),
        expiresAt = (string?)null,
        reason = "e10f-test",
        rowVersion = "0001-01-01T00:00:00",
    };

    private static object ToAPosition(Department position, string value, string? department) => new
    {
        vid = (int?)null,
        positionDepartment = position.ToString(),
        positionLevels = new[] { nameof(StaffLevel.Coordinator) },
        kind = nameof(GrantKind.Permission),
        value,
        department,
        effect = nameof(GrantEffect.Grant),
        expiresAt = (string?)null,
        reason = "e10f-test",
        rowVersion = "0001-01-01T00:00:00",
    };

    /// <summary>The departments <c>/api/me</c> says the member is inside, for the purpose of seeing: exactly that one.</summary>
    private static void AssertInsideOnly(JsonElement me, Department department)
    {
        var user = me.GetProperty("user");
        Assert.Equal(new[] { department.ToString() }, user.GetProperty("departments").EnumerateArray().Select(entry => entry.GetString()));
        Assert.False(user.GetProperty("hasAllDepartments").GetBoolean());
    }

    private static async Task<HttpStatusCode> StatusOfAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
        return response.StatusCode;
    }

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return problem.GetProperty("errors").GetProperty(field).EnumerateArray().First().GetString();
    }

    /// <summary>A client that can write, signed in with a fresh cookie: a grant written signs its holder out.</summary>
    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", HubPipeline.RequestedWithValue);
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member of the staff with one position and no address.</summary>
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
        user.LastName = "Assigner";
        user.IsSuperadmin = false;
        user.IsStaff = true;
        user.Email = null;
        user.Locale = "en";
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
