using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone once more (M3, A3, note 2026-09-25-i-permessi-alternativi-e-la-creazione): the permissions a row may be
/// written with besides <c>Edit</c> — two on one entity, one of them also at creation — asked by the interceptor's guard
/// itself, with no endpoint in the way, as <see cref="DomainBackboneTests"/> does, on the test module's table
/// (<see cref="SampleRecord"/>) and a real MariaDB:
/// <list type="bullet">
/// <item>each alternative writes the row its scope names, and no other;</item>
/// <item>the member a row is about writes it with neither, and brings none about themselves into existence;</item>
/// <item>a new row is created with the alternative marked for it, not with the other one, not with one held on a single
/// row, and not on a department where it is not held;</item>
/// <item>no alternative moves a row between departments or deletes it;</item>
/// <item>and <c>Edit</c> is still enough.</item>
/// </list>
/// <para>Who writes is the identity a login puts in the cookie (<see cref="HubClaims.BuildIdentity"/>, as the test sign in
/// does), read by the host's own <see cref="HttpContextCurrentUser"/> from the request. Not <see cref="TestCurrentUser"/>:
/// it holds only the permissions of the core, and it does not pass the row's scope on, which is half of what is proved
/// here.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AlternativeWritePermissionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the training module (CONTRIBUTING.md); 790001–790004 are A1's. Nobody is seeded: the claims are the person.
    private const int DeciderVid = 790005;
    private const int RecorderVid = 790006;
    private const int MemberVid = 790007;
    private const int EditorVid = 790008;

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
    public async Task EachAlternativeWritesTheRowItsScopeNamesAndNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var first = await CreateAsync(who: null, "trn-test-a3 first", about: null, token, Department.ED);
        var second = await CreateAsync(who: null, "trn-test-a3 second", about: null, token, Department.ED);

        // Sample.Decide on the first row only, the way a trainer holds Training.Conduct on the training assigned to them.
        var decider = Holding(DeciderVid, Held(SampleModule.DecidePermission, Department.ED, ScopeOf(first)));
        await ChangeAsync(decider, first, row => row.Title = "trn-test-a3 decided", token);
        await RefusedAsync(() => ChangeAsync(decider, second, row => row.Title = "trn-test-a3 not theirs", token));

        // Sample.Record on the second row only: the entity's other alternative, and just as enough.
        var recorder = Holding(RecorderVid, Held(SampleModule.RecordPermission, Department.ED, ScopeOf(second)));
        await ChangeAsync(recorder, second, row => row.Title = "trn-test-a3 recorded", token);
        await RefusedAsync(() => ChangeAsync(recorder, first, row => row.Title = "trn-test-a3 not theirs", token));

        Assert.Equal("trn-test-a3 decided", (await FindAsync(first, token))!.Title);
        Assert.Equal("trn-test-a3 recorded", (await FindAsync(second, token))!.Title);
    }

    [Fact]
    public async Task TheMemberARowIsAboutWritesItWithNeitherAlternative()
    {
        var token = TestContext.Current.CancellationToken;
        var aboutThem = await CreateAsync(who: null, "trn-test-a3 about them", about: MemberVid, token, Department.ED);

        // Both alternatives, held across the department with no scope, and neither reaches the row about them.
        var member = Holding(
            MemberVid,
            Held(SampleModule.DecidePermission, Department.ED),
            Held(SampleModule.RecordPermission, Department.ED));

        await RefusedAsync(() => ChangeAsync(member, aboutThem, row => row.Title = "trn-test-a3 their own", token));

        // Nor does the one that creates bring a row about them into existence; one about somebody else it does.
        await RefusedAsync(() => CreateAsync(member, "trn-test-a3 their own", about: MemberVid, token, Department.ED));
        var aboutSomebodyElse = await CreateAsync(member, "trn-test-a3 somebody else", about: DeciderVid, token, Department.ED);
        await ChangeAsync(member, aboutSomebodyElse, row => row.Title = "trn-test-a3 somebody else, changed", token);

        Assert.Equal("trn-test-a3 about them", (await FindAsync(aboutThem, token))!.Title);
    }

    [Fact]
    public async Task ARowIsCreatedWithTheAlternativeMarkedForItAndOnADepartmentWhereItIsHeld()
    {
        var token = TestContext.Current.CancellationToken;

        // Sample.Decide changes rows and brings none into existence, held across the department as it may be.
        var decider = Holding(DeciderVid, Held(SampleModule.DecidePermission, Department.ED));
        await RefusedAsync(() => CreateAsync(decider, "trn-test-a3 by the decider", about: null, token, Department.ED));

        // Sample.Record does, the way whoever examines enters an exam.
        var recorder = Holding(RecorderVid, Held(SampleModule.RecordPermission, Department.ED));
        var created = await CreateAsync(recorder, "trn-test-a3 by the recorder", about: null, token, Department.ED);
        Assert.Equal(RecorderVid, (await FindAsync(created, token))!.CreatedBy);

        // Held on one row only, it creates nothing: a new row has no scope of its own yet.
        var onOneRow = Holding(RecorderVid, Held(SampleModule.RecordPermission, Department.ED, ScopeOf(created)));
        await RefusedAsync(() => CreateAsync(onOneRow, "trn-test-a3 by one row", about: null, token, Department.ED));

        // Held on another department, it creates nothing in the module's base department alone...
        var elsewhere = Holding(RecorderVid, Held(SampleModule.RecordPermission, Department.SOD));
        await RefusedAsync(() => CreateAsync(elsewhere, "trn-test-a3 elsewhere", about: null, token, Department.ED));

        // ...and creates a row that is in its department too, the base department added as ever: held on one of them, as Edit.
        var together = await CreateAsync(elsewhere, "trn-test-a3 together", about: null, token, Department.SOD);
        Assert.Equal([Department.SOD, Department.ED], ((IOwnedByDepartment)(await FindAsync(together, token))!).OwnerDepartments);
    }

    [Fact]
    public async Task NoAlternativeMovesARowOrDeletesIt()
    {
        var token = TestContext.Current.CancellationToken;
        var record = await CreateAsync(who: null, "trn-test-a3 stays", about: null, token, Department.ED);

        // Both alternatives on both departments, and still neither takes the row somewhere else: moving is Edit's.
        var both = Holding(
            RecorderVid,
            Held(SampleModule.DecidePermission, Department.ED),
            Held(SampleModule.DecidePermission, Department.SOD),
            Held(SampleModule.RecordPermission, Department.ED),
            Held(SampleModule.RecordPermission, Department.SOD));

        await RefusedAsync(() => ChangeAsync(
            both,
            record,
            row => row.OwnerDepartmentMask = DepartmentMask.Of([Department.SOD, Department.ED]),
            token));

        // Nor does either delete it, the one that creates included: deleting is Edit's as well.
        await RefusedAsync(() => DeleteAsync(both, record, token));

        Assert.Equal([Department.ED], ((IOwnedByDepartment)(await FindAsync(record, token))!).OwnerDepartments);
    }

    [Fact]
    public async Task EditIsStillEnough()
    {
        var token = TestContext.Current.CancellationToken;
        var editor = Holding(EditorVid, Held(SampleModule.EditPermission, Department.ED));

        var record = await CreateAsync(editor, "trn-test-a3 edited", about: null, token, Department.ED);
        await ChangeAsync(editor, record, row => row.Title = "trn-test-a3 edited again", token);
        Assert.Equal("trn-test-a3 edited again", (await FindAsync(record, token))!.Title);

        await DeleteAsync(editor, record, token);
        Assert.Null(await FindAsync(record, token));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>A signed in member holding exactly these permissions, as a login writes them into the cookie.</summary>
    private static ClaimsPrincipal Holding(int vid, params EffectivePermission[] permissions) =>
        new(HubClaims.BuildIdentity(
            vid,
            firstName: "Test",
            lastName: "Alternative",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: [],
            permissions: permissions));

    /// <summary>A permission held on a department, across it or — with a scope — on one row only, as a grant gives it.</summary>
    private static EffectivePermission Held(string permission, Department department, string? scope = null) =>
        new(permission, department, $"{EffectivePermissionsCalculator.GrantSourcePrefix}test", scope);

    private static string ScopeOf(long record) => $"{SampleModule.ModuleKey}:record:{record}";

    /// <summary>The guard's refusal: every alternative tried, and <c>Edit</c> — the permission that always suffices — missing.</summary>
    private static async Task RefusedAsync(Func<Task> write)
    {
        var refused = await Assert.ThrowsAsync<ForbiddenDomainException>(write);
        Assert.Equal(SampleModule.EditPermission, refused.Permission);
    }

    private async Task<long> CreateAsync(
        ClaimsPrincipal? who,
        string title,
        int? about,
        CancellationToken cancellationToken,
        params Department[] departments)
    {
        var id = await AsAsync(who, async database =>
        {
            var record = new SampleRecord
            {
                Title = title,
                StakeholderVid = about,
                OwnerDepartment = departments[0],
                OwnerDepartmentMask = DepartmentMask.Of(departments),
            };

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

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own, as <paramref name="who"/> — or as the installation itself when
    /// nobody is given, which the guard leaves alone. The identity goes where the cookie middleware puts it, the request,
    /// and the host's current user reads it from there.
    /// </summary>
    private async Task<T> AsAsync<T>(ClaimsPrincipal? who, Func<SampleDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = who is null ? null : new DefaultHttpContext { User = who, RequestServices = scope.ServiceProvider };

        try
        {
            return await work(scope.ServiceProvider.GetRequiredService<SampleDbContext>());
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }
}
