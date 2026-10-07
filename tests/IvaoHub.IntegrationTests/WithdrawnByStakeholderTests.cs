using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The backbone once more (M4, E10h, note 2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga): a row its member takes back by
/// deleting it, the way a pilot withdraws their booking of an event, asked of the interceptor's guard itself, with no endpoint in
/// the way, as <see cref="AlternativeWritePermissionTests"/> does, on the test module's tables and a real MariaDB:
/// <list type="bullet">
/// <item>the member a marked row is about deletes the row they sent, and the audit says it was them;</item>
/// <item>nobody else does without <c>Edit</c> — another member, and not by writing their own VID into the row first: the row as
/// it was loaded decides;</item>
/// <item>but a stub never loaded is believed as its caller wrote it — the limit the maintainer accepted, as for T11 (answer 2 on
/// #232): the endpoint must load the row;</item>
/// <item>the staff still delete it with <c>Edit</c>;</item>
/// <item>a row whose entity is not marked (<see cref="SampleReport"/>, what a pilot's report is) stays the department's to delete,
/// though its member sends it and keeps changing it.</item>
/// </list>
/// <para>Who writes is the identity a login puts in the cookie (<see cref="HubClaims.BuildIdentity"/>), read by the host's own
/// <see cref="HttpContextCurrentUser"/> from the request, as in <see cref="AlternativeWritePermissionTests"/>. Nobody is seeded:
/// the claims are the person.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class WithdrawnByStakeholderTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the events module (CONTRIBUTING.md), the three E6a left to E10h. No ED or MD staff with an address: nobody is
    // seeded at all.
    private const int MemberVid = 761037;
    private const int OtherMemberVid = 761047;
    private const int StaffVid = 761048;

    private readonly List<long> _submissions = [];
    private readonly List<long> _reports = [];
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Nothing left behind: the rows go as the installation, which the guard leaves alone.
        var token = TestContext.Current.CancellationToken;
        if (_submissions.Count > 0)
        {
            await AsAsync(who: null, database => database.Submissions.Where(row => _submissions.Contains(row.Id)).ExecuteDeleteAsync(token));
        }

        if (_reports.Count > 0)
        {
            await AsAsync(who: null, database => database.Reports.Where(row => _reports.Contains(row.Id)).ExecuteDeleteAsync(token));
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheMemberARowIsAboutTakesItBackAndTheAuditSaysItWasThem()
    {
        var token = TestContext.Current.CancellationToken;
        var member = Holding(MemberVid);

        // Sent by a member who holds nothing, as a pilot books a slot, and taken back by them.
        var submission = await SendAsync(member, "evt-test-e10h taken back", token);
        await DeleteSubmissionAsync(member, submission, token);

        Assert.Null(await FindSubmissionAsync(submission, token));
        Assert.True(await AuditedAsync("smp_submissions", submission, "created", MemberVid, token));
        Assert.True(await AuditedAsync("smp_submissions", submission, "deleted", MemberVid, token));
    }

    [Fact]
    public async Task NobodyElseTakesItBackWithoutEdit()
    {
        var token = TestContext.Current.CancellationToken;
        var submission = await SendAsync(Holding(MemberVid), "evt-test-e10h not theirs", token);

        // Another member, holding nothing: the row is not about them.
        var other = Holding(OtherMemberVid);
        await RefusedAsync(() => DeleteSubmissionAsync(other, submission, token));

        // Nor with a permission that is not the area's Edit, across the department: the mark opens the row to its member alone.
        var viewer = Holding(OtherMemberVid, Held(SampleModule.ViewPermission, Department.ED));
        await RefusedAsync(() => DeleteSubmissionAsync(viewer, submission, token));

        Assert.NotNull(await FindSubmissionAsync(submission, token));
        Assert.False(await AuditedAsync("smp_submissions", submission, "deleted", OtherMemberVid, token));
    }

    [Fact]
    public async Task TheRowAsItWasLoadedSaysWhoseItIs()
    {
        var token = TestContext.Current.CancellationToken;
        var submission = await SendAsync(Holding(MemberVid), "evt-test-e10h loaded", token);

        // Another member who writes their own VID into the instance before removing it: the guard reads the row as it was
        // loaded, and it is still the member's.
        var other = Holding(OtherMemberVid);
        await RefusedAsync(() => AsAsync(other, async database =>
        {
            var row = await database.Submissions.SingleAsync(row => row.Id == submission, token);
            row.SenderVid = OtherMemberVid;
            database.Submissions.Remove(row);
            return await database.SaveChangesAsync(token);
        }));

        Assert.Equal(MemberVid, (await FindSubmissionAsync(submission, token))!.SenderVid);
    }

    /// <summary>
    /// The limit, pinned as it is (the reviewer's point 2 on #232, accepted by the maintainer, answer 2): the guard reads the
    /// tracker's original values, as T11's change does, and for a row attached without being read those are what the caller
    /// wrote. So a stub that names another member's row with the writer's own VID passes, and that row is gone. The endpoint must
    /// load the row — E6a's withdrawal reads the booking by its id and its pilot first. If the guard ever reads the database
    /// again, this test changes with it, and says so.
    /// </summary>
    [Fact]
    public async Task AStubNeverLoadedIsBelievedAsItsCallerWroteIt()
    {
        var token = TestContext.Current.CancellationToken;
        var submission = await SendAsync(Holding(MemberVid), "evt-test-e10h stub", token);

        // Another member removes a stub of the member's row — its key, and their own VID where the member's is — never read.
        await AsAsync(Holding(OtherMemberVid), async database =>
        {
            database.Submissions.Remove(new SampleSubmission
            {
                Id = submission,
                SenderVid = OtherMemberVid,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });

            return await database.SaveChangesAsync(token);
        });

        // The member's row is gone, and the audit at least says who took it.
        Assert.Null(await FindSubmissionAsync(submission, token));
        Assert.True(await AuditedAsync("smp_submissions", submission, "deleted", OtherMemberVid, token));
    }

    [Fact]
    public async Task TheStaffStillDeleteItWithEdit()
    {
        var token = TestContext.Current.CancellationToken;
        var submission = await SendAsync(Holding(MemberVid), "evt-test-e10h removed by the staff", token);

        // Edit on another department reaches nothing in the module's base department alone, as ever...
        var elsewhere = Holding(StaffVid, Held(SampleModule.EditPermission, Department.SOD));
        await RefusedAsync(() => DeleteSubmissionAsync(elsewhere, submission, token));

        // ...and on the row's own, it removes the row a member sent, as the staff take a booking away.
        var staff = Holding(StaffVid, Held(SampleModule.EditPermission, Department.ED));
        await DeleteSubmissionAsync(staff, submission, token);

        Assert.Null(await FindSubmissionAsync(submission, token));
        Assert.True(await AuditedAsync("smp_submissions", submission, "deleted", StaffVid, token));
    }

    [Fact]
    public async Task ARowWhoseEntityIsNotMarkedIsStillTheDepartmentsToDelete()
    {
        var token = TestContext.Current.CancellationToken;
        var member = Holding(MemberVid);

        // A report: the member sends it and keeps changing it, as a pilot corrects their own (M2, T11)...
        var report = await AsAsync(member, async database =>
        {
            var row = new SampleReport
            {
                Title = "evt-test-e10h report",
                SenderVid = MemberVid,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            };

            database.Reports.Add(row);
            await database.SaveChangesAsync(token);
            return row.Id;
        });
        _reports.Add(report);

        await AsAsync(member, async database =>
        {
            (await database.Reports.SingleAsync(row => row.Id == report, token)).Title = "evt-test-e10h report, corrected";
            return await database.SaveChangesAsync(token);
        });

        // ...but does not delete it: deleting a row a member sent is the department's, unless its entity says otherwise.
        await RefusedAsync(() => DeleteReportAsync(member, report, token));
        Assert.Equal("evt-test-e10h report, corrected", (await FindReportAsync(report, token))!.Title);

        // The staff do, with Edit, as ever.
        await DeleteReportAsync(Holding(StaffVid, Held(SampleModule.EditPermission, Department.ED)), report, token);
        Assert.Null(await FindReportAsync(report, token));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>A signed in member holding exactly these permissions — none, for a member — as a login writes them into the cookie.</summary>
    private static ClaimsPrincipal Holding(int vid, params EffectivePermission[] permissions) =>
        new(HubClaims.BuildIdentity(
            vid,
            firstName: "Test",
            lastName: "Withdrawal",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: permissions.Length > 0,
            positions: [],
            permissions: permissions));

    /// <summary>A permission held on a department, across it, as a grant gives it.</summary>
    private static EffectivePermission Held(string permission, Department department) =>
        new(permission, department, $"{EffectivePermissionsCalculator.GrantSourcePrefix}test", null);

    /// <summary>The guard's refusal: the member's way in closed, and <c>Edit</c> — the permission that always suffices — missing.</summary>
    private static async Task RefusedAsync(Func<Task> write)
    {
        var refused = await Assert.ThrowsAsync<ForbiddenDomainException>(write);
        Assert.Equal(SampleModule.EditPermission, refused.Permission);
    }

    /// <summary>A submission the member sends about themselves, in the module's base department.</summary>
    private async Task<long> SendAsync(ClaimsPrincipal who, string title, CancellationToken cancellationToken)
    {
        var vid = int.Parse(who.FindFirstValue(HubClaims.Vid)!, CultureInfo.InvariantCulture);
        var id = await AsAsync(who, async database =>
        {
            var row = new SampleSubmission
            {
                Title = title,
                SenderVid = vid,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            };

            database.Submissions.Add(row);
            await database.SaveChangesAsync(cancellationToken);
            return row.Id;
        });

        _submissions.Add(id);
        return id;
    }

    private Task DeleteSubmissionAsync(ClaimsPrincipal who, long id, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            database.Submissions.Remove(await database.Submissions.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task DeleteReportAsync(ClaimsPrincipal who, long id, CancellationToken cancellationToken) =>
        AsAsync(who, async database =>
        {
            database.Reports.Remove(await database.Reports.SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    private Task<SampleSubmission?> FindSubmissionAsync(long id, CancellationToken cancellationToken) =>
        AsAsync(who: null, database => database.Submissions.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    private Task<SampleReport?> FindReportAsync(long id, CancellationToken cancellationToken) =>
        AsAsync(who: null, database => database.Reports.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    /// <summary>Whether the audit of the core holds this action on this row, written by this VID.</summary>
    private async Task<bool> AuditedAsync(string table, long id, string action, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var key = id.ToString(CultureInfo.InvariantCulture);

        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(entry => entry.Entity == table && entry.EntityId == key && entry.Action == action && entry.Vid == vid, cancellationToken);
    }

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
