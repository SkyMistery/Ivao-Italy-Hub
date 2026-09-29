using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone once more (M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir): a permission given to the team of a FIR, held on
/// the rows of that FIR alone when the division keeps FIR teams to their FIR — <c>firStaffScope: own</c>, which this host starts
/// with — asked of the single handler on the row as the CRUD engine asks it, of the interceptor's guard with no endpoint in the
/// way, and of a generated list, on the test module's <see cref="SampleRecord"/> and a real MariaDB:
/// <list type="bullet">
/// <item>the chief and the assistant chief of a FIR hold it on the rows of their FIR and on no other — not another FIR's, not a
/// row with no FIR —, and an advisor of the FIR, outside the grant's levels, holds nothing;</item>
/// <item>the guard lets them write the rows of their FIR — changed, created, taken away — and none of another, and moves no row
/// from one FIR to another; the handler says the same every time;</item>
/// <item>whoever leaves the position loses the permission at the next computation, the one a sign in makes;</item>
/// <item>with <c>firStaffScope: all</c> the team's grant is its department's, and the staff of a department is never held to a
/// FIR, as the rule that stood in the handler until A11a held it with <c>own</c>;</item>
/// <item>the generated list shows a chief the rows of their FIR, and only through the list's own read permission;</item>
/// <item>writing a grant to the team signs out whoever holds a FIR position at its levels.</item>
/// </list>
/// The FIRs are two of the test's own, known through a directory of FIRs that knows only them: nothing is written into the shared
/// reference data. Who writes is the identity a sign in puts in the cookie, read by the host's own
/// <see cref="HttpContextCurrentUser"/>, as in <see cref="AssignedRowPermissionTests"/>.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class FirTeamPermissionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the training module (CONTRIBUTING.md), 790080–790089 handed to A11a. The first four are identities only, the
    // others rows of hub_users seeded and taken away here.
    private const int ChiefVid = 790080;
    private const int AssistantVid = 790081;
    private const int AdvisorVid = 790082;
    private const int StaffVid = 790083;
    private const int LeavingVid = 790084;
    private const int ListChiefVid = 790085;
    private const int ListOtherVid = 790086;
    private const int SessionHolderVid = 790087;
    private const int SessionMemberVid = 790088;

    private static readonly int[] SeededVids = [LeavingVid, ListChiefVid, ListOtherVid, SessionHolderVid, SessionMemberVid];

    /// <summary>What every grant written here says, so that one left behind by an interrupted run is found and taken away.</summary>
    private const string Reason = "trn-test-a11a";

    // Two FIRs nobody has: the directory of this host knows them and nothing else.
    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";

    private readonly List<long> _records = [];
    private readonly List<long> _grants = [];
    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            // The division keeps FIR teams to their own FIR, on top of what the division file says (Program.cs: "a test host
            // adds its own after this line").
            services.Configure<DivisionOptions>(options => new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["firStaffScope"] = nameof(FirStaffScope.Own) })
                .Build()
                .Bind(options));

            services.AddScoped<IFirDirectory, TwoFirs>();
        }));

        await CleanAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(TestContext.Current.CancellationToken);
        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    [Fact]
    public async Task TheTeamOfAFirHoldsItsPermissionOnTheRowsOfItsFirAndOnNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var ours = await CreateAsync(who: null, "trn-test-a11a ours", Fir, assignedTo: null, token);
        var theirs = await CreateAsync(who: null, "trn-test-a11a theirs", OtherFir, assignedTo: null, token);
        var none = await CreateAsync(who: null, "trn-test-a11a of no FIR", fir: null, assignedTo: null, token);

        // The chief and the assistant chief, through a grant to the team at those two levels.
        foreach (var (vid, position) in new[] { (ChiefVid, Chief(Fir)), (AssistantVid, AssistantChief(Fir)) })
        {
            var member = ThroughTheTeam(vid, position, FirStaffScope.Own, SampleModule.DecidePermission);

            Assert.True(await AllowedAsync(member, await FindAsync(ours, token), SampleModule.DecidePermission));
            await ChangeAsync(member, ours, row => row.Title = $"trn-test-a11a decided by {vid}", token);
            Assert.Equal($"trn-test-a11a decided by {vid}", (await FindAsync(ours, token))!.Title);

            Assert.False(await AllowedAsync(member, await FindAsync(theirs, token), SampleModule.DecidePermission));
            await RefusedAsync(() => ChangeAsync(member, theirs, row => row.Title = "trn-test-a11a not theirs", token));

            Assert.False(await AllowedAsync(member, await FindAsync(none, token), SampleModule.DecidePermission));
            await RefusedAsync(() => ChangeAsync(member, none, row => row.Title = "trn-test-a11a not theirs", token));
        }

        // An advisor of the FIR is outside the grant's levels: the team's grant is not theirs at all.
        var advisor = ThroughTheTeam(AdvisorVid, Advisor(Fir), FirStaffScope.Own, SampleModule.DecidePermission);
        Assert.False(await AllowedAsync(advisor, await FindAsync(ours, token), SampleModule.DecidePermission));
        await RefusedAsync(() => ChangeAsync(advisor, ours, row => row.Title = "trn-test-a11a not an advisor's", token));

        Assert.Equal("trn-test-a11a theirs", (await FindAsync(theirs, token))!.Title);
        Assert.Equal("trn-test-a11a of no FIR", (await FindAsync(none, token))!.Title);
    }

    [Fact]
    public async Task TheGuardLetsTheTeamWriteTheRowsOfItsFirOnlyAndMovesNoRowBetweenFirs()
    {
        var token = TestContext.Current.CancellationToken;
        var chief = ThroughTheTeam(
            ChiefVid,
            Chief(Fir),
            FirStaffScope.Own,
            SampleModule.RecordPermission,
            SampleModule.ManagePermission,
            SampleModule.DecidePermission);

        // Created with Sample.Record, which also creates (A3): a row of their FIR, and not one of another FIR or of none.
        Assert.True(await AllowedAsync(chief, New("trn-test-a11a new", Fir, assignedTo: null), SampleModule.RecordPermission));
        var created = await CreateAsync(chief, "trn-test-a11a created by the chief", Fir, assignedTo: null, token);
        Assert.Equal(ChiefVid, (await FindAsync(created, token))!.CreatedBy);

        Assert.False(await AllowedAsync(chief, New("trn-test-a11a new", OtherFir, assignedTo: null), SampleModule.RecordPermission));
        await RefusedAsync(() => CreateAsync(chief, "trn-test-a11a of another FIR", OtherFir, assignedTo: null, token));
        Assert.False(await AllowedAsync(chief, New("trn-test-a11a new", fir: null, assignedTo: null), SampleModule.RecordPermission));
        await RefusedAsync(() => CreateAsync(chief, "trn-test-a11a of no FIR", fir: null, assignedTo: null, token));

        // Taken away with Sample.Manage, which deletes the rows assigned to the writer (A3b): theirs of their FIR, not of another.
        var oursToGo = await CreateAsync(who: null, "trn-test-a11a ours to go", Fir, assignedTo: ChiefVid, token);
        var theirsToStay = await CreateAsync(who: null, "trn-test-a11a theirs to stay", OtherFir, assignedTo: ChiefVid, token);

        Assert.False(await AllowedAsync(chief, await FindAsync(theirsToStay, token), SampleModule.ManagePermission));
        await RefusedAsync(() => DeleteAsync(chief, theirsToStay, token));
        Assert.True(await AllowedAsync(chief, await FindAsync(oursToGo, token), SampleModule.ManagePermission));
        await DeleteAsync(chief, oursToGo, token);
        Assert.Null(await FindAsync(oursToGo, token));
        Assert.NotNull(await FindAsync(theirsToStay, token));

        // Never moved: from their FIR to another — the handler, asked on the row as it would be, says no as well — nor from another
        // FIR to theirs.
        var moved = (await FindAsync(created, token))!;
        moved.Fir = OtherFir;
        Assert.False(await AllowedAsync(chief, moved, SampleModule.DecidePermission));
        await RefusedAsync(() => ChangeAsync(chief, created, row => row.Fir = OtherFir, token));

        var another = await CreateAsync(who: null, "trn-test-a11a another FIR's", OtherFir, assignedTo: null, token);
        await RefusedAsync(() => ChangeAsync(chief, another, row => row.Fir = Fir, token));

        // What moves a row between FIRs is Edit on both of them: as the staff of the department holds it, with no FIR.
        var staff = Holding(StaffVid, [], Held(SampleModule.EditPermission, Department.ED));
        await ChangeAsync(staff, created, row => row.Fir = OtherFir, token);
        Assert.Equal(OtherFir, (await FindAsync(created, token))!.Fir);
        Assert.Equal(OtherFir, (await FindAsync(another, token))!.Fir);
    }

    [Fact]
    public async Task WhoeverLeavesThePositionLosesThePermissionAtTheNextComputation()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await CreateAsync(who: null, "trn-test-a11a the chief's", Fir, assignedTo: null, token);
        await GrantToTheTeamAsync(SampleModule.DecidePermission, [StaffLevel.Coordinator], token);

        // The sign in, as IVAO says the chief of the FIR is: the computation holds the grant, on the FIR of the position.
        var chief = await SignInThroughIvaoAsync(LeavingVid, [$"{Fir}-CH"], token);
        Assert.Contains(chief.Permissions, held => held.Name == SampleModule.DecidePermission && held.Fir == Fir);
        Assert.True(await AllowedAsync(Principal(chief), await FindAsync(row, token), SampleModule.DecidePermission));

        // The next one, once IVAO lists the position no more: nothing is left of it.
        var gone = await SignInThroughIvaoAsync(LeavingVid, [], token);
        Assert.DoesNotContain(gone.Permissions, held => held.Name == SampleModule.DecidePermission);
        Assert.False(await AllowedAsync(Principal(gone), await FindAsync(row, token), SampleModule.DecidePermission));
    }

    [Fact]
    public async Task WithAllTheTeamHoldsItOnItsDepartmentAndTheStaffOfADepartmentIsNeverHeldToAFir()
    {
        var token = TestContext.Current.CancellationToken;
        var rows = new[]
        {
            await CreateAsync(who: null, "trn-test-a11a ours", Fir, assignedTo: null, token),
            await CreateAsync(who: null, "trn-test-a11a theirs", OtherFir, assignedTo: null, token),
            await CreateAsync(who: null, "trn-test-a11a of no FIR", fir: null, assignedTo: null, token),
        };

        // With firStaffScope all, the computation writes no FIR: the team's grant is held across its department, as any grant.
        var chiefWithAll = ThroughTheTeam(ChiefVid, Chief(Fir), FirStaffScope.All, SampleModule.DecidePermission);

        // And the staff of the department, who holds the permission with no FIR, reaches every row whatever its FIR: the rule that
        // stood in the handler until A11a refused them, on this host that says own, every row of a FIR they did not have.
        var staff = Holding(StaffVid, [], Held(SampleModule.DecidePermission, Department.ED));

        foreach (var who in new[] { chiefWithAll, staff })
        {
            foreach (var id in rows)
            {
                Assert.True(await AllowedAsync(who, await FindAsync(id, token), SampleModule.DecidePermission));
                await ChangeAsync(who, id, row => row.Title += " +", token);
            }
        }
    }

    [Fact]
    public async Task TheGeneratedListShowsAChiefTheRowsOfTheirFirThroughItsOwnReadPermissionOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var ours = await CreateAsync(who: null, "trn-test-a11a listed", Fir, assignedTo: null, token);
        var theirs = await CreateAsync(who: null, "trn-test-a11a not listed", OtherFir, assignedTo: null, token);
        var none = await CreateAsync(who: null, "trn-test-a11a not listed either", fir: null, assignedTo: null, token);

        // The chiefs read through the team. The assistant chiefs hold through it a permission that is not the list's reading, nor
        // implies it: one of another area — every permission of this area implies its View, on the same FIR.
        await GrantToTheTeamAsync(SampleModule.ViewPermission, [StaffLevel.Coordinator], token);
        await GrantToTheTeamAsync(CorePermissions.LinksEdit, [StaffLevel.Assistant], token);
        await SeedMemberAsync(ListChiefVid, $"{Fir}-CH", token);
        await SeedMemberAsync(ListOtherVid, $"{Fir}-ACH", token);
        await GrantToMemberAsync(ListOtherVid, SampleModule.ViewPermission, Department.SOD, token);

        using var chief = await SignedInClientAsync(ListChiefVid, token);
        var listed = await ListedAsync(chief, token);
        Assert.Contains(ours, listed);
        Assert.DoesNotContain(theirs, listed);
        Assert.DoesNotContain(none, listed);

        // A list of rows that say no FIR stays closed to them: they belong to no department.
        using var items = await chief.GetAsync(new Uri($"{SampleModule.ItemsPattern}?pageSize=100", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Forbidden, items.StatusCode);

        // The reviewer's point 2: the list's read permission from a grant on another department, and on their FIR a permission
        // that is not it. The rows of their FIR are not shown.
        using var other = await SignedInClientAsync(ListOtherVid, token);
        Assert.DoesNotContain(ours, await ListedAsync(other, token));
    }

    [Fact]
    public async Task WritingAGrantToTheTeamSignsOutWhoeverHoldsThePosition()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedMemberAsync(SessionHolderVid, $"{OtherFir}-ACH", token);
        await SeedMemberAsync(SessionMemberVid, position: null, token);

        var holder = await StampAsync(SessionHolderVid, token);
        var member = await StampAsync(SessionMemberVid, token);

        await GrantToTheTeamAsync(SampleModule.DecidePermission, [StaffLevel.Assistant], token);

        Assert.NotEqual(holder, await StampAsync(SessionHolderVid, token));
        Assert.Equal(member, await StampAsync(SessionMemberVid, token));
    }

    // ---- identities --------------------------------------------------------------------------------

    private static StaffPosition Chief(string fir) => new($"{fir}-CH", null, StaffLevel.Coordinator, fir, StaffRole.FirChief);

    private static StaffPosition AssistantChief(string fir) =>
        new($"{fir}-ACH", null, StaffLevel.Assistant, fir, StaffRole.FirAssistantChief);

    private static StaffPosition Advisor(string fir) => new($"{fir}-CHA1", null, StaffLevel.Advisor, fir, StaffRole.FirAdvisor);

    /// <summary>
    /// A member holding <paramref name="position"/>, and what a grant of each permission to the team of a FIR — the chiefs and
    /// the assistant chiefs, on the module's base department — gives them, as the real computation gives it.
    /// </summary>
    private ClaimsPrincipal ThroughTheTeam(int vid, StaffPosition position, FirStaffScope scope, params string[] permissions)
    {
        var grants = permissions.Select((permission, index) => new UserGrant
        {
            Id = index + 1,
            PositionFirTeam = true,
            PositionLevels = [StaffLevel.Coordinator, StaffLevel.Assistant],
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.ED,
            Effect = GrantEffect.Grant,
        });

        var effective = EffectivePermissionsCalculator.Calculate(
            [position],
            grants,
            isSuperadmin: false,
            DateTime.UtcNow,
            _factory.Services.GetRequiredService<PermissionCatalog>(),
            scope);

        return Holding(vid, [position], [.. effective]);
    }

    /// <summary>A signed in member holding exactly these positions and permissions, as a sign in writes them into the cookie.</summary>
    private static ClaimsPrincipal Holding(int vid, StaffPosition[] positions, params EffectivePermission[] permissions) =>
        new(HubClaims.BuildIdentity(
            vid,
            firstName: "Test",
            lastName: "FirTeam",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: positions,
            permissions: permissions));

    private static ClaimsPrincipal Principal(SignedInUser signedIn) =>
        new(HubClaims.BuildIdentity(
            signedIn.User.Vid,
            signedIn.User.FirstName,
            signedIn.User.LastName,
            locale: "en",
            signedIn.User.SecurityStamp,
            signedIn.User.IsSuperadmin,
            signedIn.User.IsStaff,
            signedIn.Positions,
            signedIn.Permissions));

    /// <summary>A permission held on a department, across it and with no FIR, as a department's position or a grant gives it.</summary>
    private static EffectivePermission Held(string permission, Department department) =>
        new(permission, department, $"{EffectivePermissionsCalculator.GrantSourcePrefix}test");

    // ---- rows ---------------------------------------------------------------------------------------

    /// <summary>A row of the module's base department as it would be stored, not saved: what the engine asks about on a create.</summary>
    private static SampleRecord New(string title, string? fir, int? assignedTo) => new()
    {
        Title = title,
        Fir = fir,
        AssigneeVid = assignedTo,
        OwnerDepartment = Department.ED,
        OwnerDepartmentMask = DepartmentMask.Of([Department.ED]),
    };

    /// <summary>The guard's refusal: every alternative tried, and <c>Edit</c> — the permission that always suffices — missing.</summary>
    private static async Task RefusedAsync(Func<Task> write)
    {
        var refused = await Assert.ThrowsAsync<ForbiddenDomainException>(write);
        Assert.Equal(SampleModule.EditPermission, refused.Permission);
    }

    /// <summary>What the single handler answers <paramref name="who"/> for this permission on this row, as the engine asks it.</summary>
    private Task<bool> AllowedAsync(ClaimsPrincipal who, SampleRecord? row, string permission) =>
        InScopeAsync(who, async services =>
            (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(who, row, permission)).Succeeded);

    private async Task<long> CreateAsync(
        ClaimsPrincipal? who,
        string title,
        string? fir,
        int? assignedTo,
        CancellationToken cancellationToken)
    {
        var id = await InScopeAsync(who, async services =>
        {
            var database = services.GetRequiredService<SampleDbContext>();
            var record = New(title, fir, assignedTo);
            database.Records.Add(record);
            await database.SaveChangesAsync(cancellationToken);
            return record.Id;
        });

        _records.Add(id);
        return id;
    }

    private Task ChangeAsync(ClaimsPrincipal who, long id, Action<SampleRecord> change, CancellationToken cancellationToken) =>
        InScopeAsync(who, async services =>
        {
            var database = services.GetRequiredService<SampleDbContext>();
            change(await database.Records.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task DeleteAsync(ClaimsPrincipal who, long id, CancellationToken cancellationToken) =>
        InScopeAsync(who, async services =>
        {
            var database = services.GetRequiredService<SampleDbContext>();
            database.Records.Remove(await database.Records.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task<SampleRecord?> FindAsync(long id, CancellationToken cancellationToken) =>
        InScopeAsync(who: null, services => services.GetRequiredService<SampleDbContext>()
            .Records.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    // ---- people, grants and the list ---------------------------------------------------------------

    /// <summary>A grant of the permission to the team of a FIR at those levels, on the module's base department, as the installation writes it.</summary>
    private Task GrantToTheTeamAsync(string permission, StaffLevel[] levels, CancellationToken cancellationToken) =>
        WriteGrantAsync(
            new UserGrant
            {
                PositionFirTeam = true,
                PositionLevels = levels,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = Department.ED,
                Effect = GrantEffect.Grant,
                Reason = Reason,
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
                Reason = Reason,
            },
            cancellationToken);

    private async Task WriteGrantAsync(UserGrant grant, CancellationToken cancellationToken)
    {
        var id = await InScopeAsync(who: null, async services =>
        {
            var database = services.GetRequiredService<HubDbContext>();
            database.UserGrants.Add(grant);
            await database.SaveChangesAsync(cancellationToken);
            return grant.Id;
        });

        _grants.Add(id);
    }

    /// <summary>A member of hub_users with one position, or none, as a sign in would have left them. No address: no mail is sent.</summary>
    private Task SeedMemberAsync(int vid, string? position, CancellationToken cancellationToken) =>
        InScopeAsync(who: null, async services =>
        {
            var database = services.GetRequiredService<HubDbContext>();
            var clock = services.GetRequiredService<IClock>();

            database.Users.Add(new HubUser
            {
                Vid = vid,
                FirstName = "Test",
                LastName = "FirTeam",
                IsStaff = position is not null,
                SecurityStamp = SuperadminService.NewStamp(),
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
            });

            if (position is not null)
            {
                var parsed = StaffRoleMap.Parse(position, "IT", TwoFirs.Ids)!;
                database.UserStaffPositions.Add(new UserStaffPosition
                {
                    Vid = vid,
                    Position = position,
                    Department = parsed.Department,
                    Level = parsed.Level,
                    Fir = parsed.Fir,
                    SyncedAt = clock.UtcNow,
                });
            }

            return await database.SaveChangesAsync(cancellationToken);
        });

    /// <summary>The sign in the host makes when IVAO answers with this profile: the real computation, with this host's options.</summary>
    private Task<SignedInUser> SignInThroughIvaoAsync(int vid, string[] positions, CancellationToken cancellationToken) =>
        InScopeAsync(who: null, services => services.GetRequiredService<UserSyncService>().UpsertAsync(
            new IvaoUserProfile(
                Vid: vid,
                FirstName: "Test",
                LastName: "FirTeam",
                PublicNickname: null,
                DivisionCode: "IT",
                CountryId: "IT",
                RatingAtc: null,
                RatingPilot: null,
                DiscordId: null,
                Email: null,
                LanguageId: "en",
                IvaoIsStaff: positions.Length > 0,
                IvaoIsSupervisor: false,
                StaffPositions: positions),
            cancellationToken));

    private Task<string> StampAsync(int vid, CancellationToken cancellationToken) =>
        InScopeAsync(who: null, services => services.GetRequiredService<HubDbContext>().Users
            .AsNoTracking()
            .Where(user => user.Vid == vid)
            .Select(user => user.SecurityStamp)
            .SingleAsync(cancellationToken));

    /// <summary>A client with the application cookie of a member, as the host signs them in.</summary>
    private async Task<HttpClient> SignedInClientAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>The identifiers the generated list of the records shows the client, the first hundred.</summary>
    private static async Task<long[]> ListedAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            new Uri($"{SampleModule.RecordsPattern}?pageSize=100", UriKind.Relative),
            cancellationToken);

        return [.. page.GetProperty("items").EnumerateArray().Select(record => record.GetProperty("id").GetInt64())];
    }

    // ---- the host ------------------------------------------------------------------------------------

    /// <summary>What this test left behind, and what an interrupted run of it left: taken away as the installation, before and after.</summary>
    private Task CleanAsync(CancellationToken cancellationToken) =>
        InScopeAsync(who: null, async services =>
        {
            var hub = services.GetRequiredService<HubDbContext>();
            var sample = services.GetRequiredService<SampleDbContext>();

            await hub.UserGrants
                .Where(grant => _grants.Contains(grant.Id) || grant.Reason == Reason)
                .ExecuteDeleteAsync(cancellationToken);
            await hub.UserStaffPositions.Where(position => SeededVids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
            await hub.Users.Where(user => SeededVids.Contains(user.Vid)).ExecuteDeleteAsync(cancellationToken);

            return await sample.Records
                .Where(row => _records.Contains(row.Id) || row.Title.StartsWith("trn-test-a11a"))
                .ExecuteDeleteAsync(cancellationToken);
        });

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own, as <paramref name="who"/> — or as the installation itself when
    /// nobody is given, which the guard leaves alone. The identity goes where the cookie middleware puts it, the request,
    /// and the host's current user reads it from there, for the handler as for the guard.
    /// </summary>
    private async Task<T> InScopeAsync<T>(ClaimsPrincipal? who, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = who is null ? null : new DefaultHttpContext { User = who, RequestServices = scope.ServiceProvider };

        try
        {
            return await work(scope.ServiceProvider);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    /// <summary>The airspace of this host: two FIRs of the test's own, and no airport.</summary>
    private sealed class TwoFirs : IFirDirectory
    {
        public static readonly IReadOnlySet<string> Ids = new HashSet<string>([Fir, OtherFir], StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Ids);

        public Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new IvaoAirspace(Ids, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        public void Invalidate()
        {
        }
    }
}
