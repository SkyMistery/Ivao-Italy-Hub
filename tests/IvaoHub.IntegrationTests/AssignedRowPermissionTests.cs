using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone once more (M3, A3b, note 2026-09-26-le-righe-affidate-a-chi-scrive): a permission that reaches only the rows
/// assigned to the writer — <c>Sample.Manage</c> on the test module's <see cref="SampleRecord"/>, the way an examiner holds
/// <c>Training.ManageExams</c> on their exams — asked of the single handler on the row, as the CRUD engine asks it, and of the
/// interceptor's guard with no endpoint in the way, on a real MariaDB:
/// <list type="bullet">
/// <item>a row assigned to X is changed and taken away by X, and by nobody else who holds the permission the same way;</item>
/// <item>a new row is created assigned to its writer, and to nobody else;</item>
/// <item>the assignee neither hands a row over nor takes somebody else's;</item>
/// <item><c>Edit</c> still reaches every row, and the handler lets whoever holds it use the marked permission on any row;</item>
/// <item>the member a row is about is still left out;</item>
/// <item>the alternatives that are not marked do not look at the assignee, and still delete nothing.</item>
/// </list>
/// In every case the handler and the guard give the same answer. Who writes is the identity a login puts in the cookie, read by
/// the host's own <see cref="HttpContextCurrentUser"/>, as in <see cref="AlternativeWritePermissionTests"/>.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AssignedRowPermissionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the training module (CONTRIBUTING.md): 790001–790013 are A1's, A3's and A4's. Nobody is seeded: the claims
    // are the person.
    private const int AssigneeVid = 790040;
    private const int OtherVid = 790041;
    private const int CoordinatorVid = 790042;
    private const int MemberVid = 790043;
    private const int DeciderVid = 790044;

    private readonly List<long> _records = [];
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Nothing left behind: the rows go as the installation, which the guard leaves alone.
        if (_records.Count > 0)
        {
            await AsAsync(who: null, database => database.Records
                .Where(row => _records.Contains(row.Id))
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ARowAssignedToAMemberIsChangedAndTakenAwayByThemAndByNobodyElseHoldingTheSame()
    {
        var token = TestContext.Current.CancellationToken;
        var yours = await CreateAsync(who: null, "trn-test-a3b yours", about: null, assignedTo: AssigneeVid, token);

        // Sample.Manage across the department, as a TA holds Training.ManageExams by position, and the row is not theirs.
        var other = Holding(OtherVid, Held(SampleModule.ManagePermission, Department.ED));
        Assert.False(await AllowedAsync(other, await FindAsync(yours, token), SampleModule.ManagePermission));
        await RefusedAsync(() => ChangeAsync(other, yours, row => row.Title = "trn-test-a3b not theirs", token));
        await RefusedAsync(() => DeleteAsync(other, yours, token));

        // The same permission held the same way by the member the row is assigned to.
        var assignee = Holding(AssigneeVid, Held(SampleModule.ManagePermission, Department.ED));
        Assert.True(await AllowedAsync(assignee, await FindAsync(yours, token), SampleModule.ManagePermission));
        await ChangeAsync(assignee, yours, row => row.Title = "trn-test-a3b changed by its assignee", token);
        Assert.Equal("trn-test-a3b changed by its assignee", (await FindAsync(yours, token))!.Title);

        await DeleteAsync(assignee, yours, token);
        Assert.Null(await FindAsync(yours, token));
    }

    [Fact]
    public async Task ANewRowIsCreatedAssignedToWhoeverCreatesItAndToNobodyElse()
    {
        var token = TestContext.Current.CancellationToken;
        var assignee = Holding(AssigneeVid, Held(SampleModule.ManagePermission, Department.ED));

        // Asked on the row as it will be stored, the way the engine asks it before the save.
        Assert.True(await AllowedAsync(assignee, New("trn-test-a3b their own", AssigneeVid), SampleModule.ManagePermission));
        var own = await CreateAsync(assignee, "trn-test-a3b their own", about: null, assignedTo: AssigneeVid, token);
        Assert.Equal(AssigneeVid, (await FindAsync(own, token))!.CreatedBy);

        Assert.False(await AllowedAsync(assignee, New("trn-test-a3b for another", OtherVid), SampleModule.ManagePermission));
        await RefusedAsync(() => CreateAsync(assignee, "trn-test-a3b for another", about: null, assignedTo: OtherVid, token));

        Assert.False(await AllowedAsync(assignee, New("trn-test-a3b for nobody", assignedTo: null), SampleModule.ManagePermission));
        await RefusedAsync(() => CreateAsync(assignee, "trn-test-a3b for nobody", about: null, assignedTo: null, token));
    }

    [Fact]
    public async Task TheAssigneeNeitherHandsARowOverNorTakesSomebodyElses()
    {
        var token = TestContext.Current.CancellationToken;
        var mine = await CreateAsync(who: null, "trn-test-a3b mine", about: null, assignedTo: AssigneeVid, token);
        var theirs = await CreateAsync(who: null, "trn-test-a3b theirs", about: null, assignedTo: OtherVid, token);
        var assignee = Holding(AssigneeVid, Held(SampleModule.ManagePermission, Department.ED));

        // Handing it over: theirs before the write, somebody else's after it. The engine asks the handler on both.
        var handedOver = (await FindAsync(mine, token))!;
        handedOver.AssigneeVid = OtherVid;
        Assert.False(await AllowedAsync(assignee, handedOver, SampleModule.ManagePermission));
        await RefusedAsync(() => ChangeAsync(assignee, mine, row => row.AssigneeVid = OtherVid, token));

        // Taking one: somebody else's before the write, theirs after it.
        Assert.False(await AllowedAsync(assignee, await FindAsync(theirs, token), SampleModule.ManagePermission));
        await RefusedAsync(() => ChangeAsync(assignee, theirs, row => row.AssigneeVid = AssigneeVid, token));

        Assert.Equal(AssigneeVid, (await FindAsync(mine, token))!.AssigneeVid);
        Assert.Equal(OtherVid, (await FindAsync(theirs, token))!.AssigneeVid);
    }

    [Fact]
    public async Task EditStillReachesEveryRowAndLetsTheMarkedPermissionReachItToo()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await CreateAsync(who: null, "trn-test-a3b a coordinator's", about: null, assignedTo: AssigneeVid, token);

        // The marked permission and Edit, as a coordinator holds both: the handler falls back on Edit for a row not theirs.
        var coordinator = Holding(
            CoordinatorVid,
            Held(SampleModule.ManagePermission, Department.ED),
            Held(SampleModule.EditPermission, Department.ED));

        Assert.True(await AllowedAsync(coordinator, await FindAsync(row, token), SampleModule.ManagePermission));
        Assert.True(await AllowedAsync(coordinator, New("trn-test-a3b for another", OtherVid), SampleModule.ManagePermission));

        // And so does the guard: they hand the row over, create one for somebody else, and take both away.
        await ChangeAsync(coordinator, row, record => record.AssigneeVid = OtherVid, token);
        var forAnother = await CreateAsync(coordinator, "trn-test-a3b for another", about: null, assignedTo: OtherVid, token);
        await DeleteAsync(coordinator, row, token);
        await DeleteAsync(coordinator, forAnother, token);
        Assert.Null(await FindAsync(row, token));

        // Held on another department, Edit does not reach the row, and neither does the marked permission through it.
        var elsewhere = Holding(
            CoordinatorVid,
            Held(SampleModule.ManagePermission, Department.ED),
            Held(SampleModule.EditPermission, Department.SOD));
        var stays = await CreateAsync(who: null, "trn-test-a3b stays", about: null, assignedTo: AssigneeVid, token);

        Assert.False(await AllowedAsync(elsewhere, await FindAsync(stays, token), SampleModule.ManagePermission));
        await RefusedAsync(() => ChangeAsync(elsewhere, stays, record => record.Title = "trn-test-a3b not theirs", token));
    }

    [Fact]
    public async Task TheMemberARowIsAboutUsesTheMarkedPermissionOnItNotEvenWhenItIsAssignedToThem()
    {
        var token = TestContext.Current.CancellationToken;
        var aboutThem = await CreateAsync(who: null, "trn-test-a3b about them", about: MemberVid, assignedTo: MemberVid, token);
        var member = Holding(MemberVid, Held(SampleModule.ManagePermission, Department.ED));

        Assert.False(await AllowedAsync(member, await FindAsync(aboutThem, token), SampleModule.ManagePermission));
        await RefusedAsync(() => ChangeAsync(member, aboutThem, row => row.Title = "trn-test-a3b their own", token));
        await RefusedAsync(() => DeleteAsync(member, aboutThem, token));

        var newAboutThem = New("trn-test-a3b a new one about them", MemberVid);
        newAboutThem.StakeholderVid = MemberVid;
        Assert.False(await AllowedAsync(member, newAboutThem, SampleModule.ManagePermission));
        await RefusedAsync(() => CreateAsync(member, "trn-test-a3b a new one about them", about: MemberVid, assignedTo: MemberVid, token));

        Assert.Equal("trn-test-a3b about them", (await FindAsync(aboutThem, token))!.Title);
    }

    [Fact]
    public async Task TheAlternativesThatAreNotMarkedIgnoreTheAssigneeAndStillDeleteNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var row = await CreateAsync(who: null, "trn-test-a3b somebody's", about: null, assignedTo: AssigneeVid, token);

        // Sample.Decide is not marked: it changes a row assigned to somebody else, exactly as in A3.
        var decider = Holding(DeciderVid, Held(SampleModule.DecidePermission, Department.ED));
        Assert.True(await AllowedAsync(decider, await FindAsync(row, token), SampleModule.DecidePermission));
        await ChangeAsync(decider, row, record => record.Title = "trn-test-a3b decided", token);

        // Nor does it, or Sample.Record, take the row away: AlsoOnDeletion is the marked permission's alone.
        var both = Holding(
            DeciderVid,
            Held(SampleModule.DecidePermission, Department.ED),
            Held(SampleModule.RecordPermission, Department.ED));
        await RefusedAsync(() => DeleteAsync(both, row, token));

        Assert.Equal("trn-test-a3b decided", (await FindAsync(row, token))!.Title);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>A signed in member holding exactly these permissions, as a login writes them into the cookie.</summary>
    private static ClaimsPrincipal Holding(int vid, params EffectivePermission[] permissions) =>
        new(HubClaims.BuildIdentity(
            vid,
            firstName: "Test",
            lastName: "Assigned",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: [],
            permissions: permissions));

    /// <summary>A permission held on a department, across it, as a position or a grant gives it.</summary>
    private static EffectivePermission Held(string permission, Department department) =>
        new(permission, department, $"{EffectivePermissionsCalculator.GrantSourcePrefix}test");

    /// <summary>A row of the module's base department as it would be stored, not saved: what the engine asks about on a create.</summary>
    private static SampleRecord New(string title, int? assignedTo) => new()
    {
        Title = title,
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
        int? about,
        int? assignedTo,
        CancellationToken cancellationToken)
    {
        var id = await AsAsync(who, async database =>
        {
            var record = New(title, assignedTo);
            record.StakeholderVid = about;

            database.Records.Add(record);
            await database.SaveChangesAsync(cancellationToken);
            return record.Id;
        });

        _records.Add(id);
        return id;
    }

    private Task ChangeAsync(ClaimsPrincipal who, long id, Action<SampleRecord> change, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            change(await database.Records.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task DeleteAsync(ClaimsPrincipal who, long id, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            database.Records.Remove(await database.Records.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task<SampleRecord?> FindAsync(long id, CancellationToken cancellationToken) =>
        AsAsync(who: null, database => database.Records.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    private Task<T> AsAsync<T>(ClaimsPrincipal? who, Func<SampleDbContext, Task<T>> work) =>
        InScopeAsync(who, services => work(services.GetRequiredService<SampleDbContext>()));

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
}
