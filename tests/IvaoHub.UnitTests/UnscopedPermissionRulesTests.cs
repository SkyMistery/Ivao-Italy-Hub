using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// A permission held without a resource scope (M4, E10u, note 2026-10-17-il-permesso-senza-ambito), for each kind of holder, as the
/// calculator gives it and as the cookie carries it — what a register of the whole division asks before a row is in hand, decided by
/// Carmine on #283 (finding 1, answer (b)):
/// <list type="bullet">
/// <item>held: a department's staff, a grant to a position on its own department, on another one or on every one (from outside), a
/// grant to a person on a department or on every one, a FIR team on its FIR or on the department, the director, a grantable global
/// permission and the View it brings, a super administrator;</item>
/// <item>not held: a grant on one row or one event, to a person, to a position or to a FIR team — where <c>HasAny</c> says yes;</item>
/// <item>the same answer from the cookie's reader, from the default member of <see cref="ICurrentUser"/> and from the set, and closed
/// for a reader that does not know the marks of the FIR and of "from outside".</item>
/// </list>
/// </summary>
public sealed class UnscopedPermissionRulesTests
{
    private const string View = "Probe.View";
    private const string Edit = "Probe.Edit";
    private const string OneEvent = "probe:event:5";
    private const string OneRow = "probe:row:1";

    // No database here: the unit identity of E2b's rules (761090), a number no integration test seeds.
    private const int Member = 761090;

    private static readonly PermissionCatalog Catalogue = new([
        .. CorePermissions.All,
        new PermissionDescriptor(View, IsGlobal: false),
        new PermissionDescriptor(Edit, IsGlobal: false),
    ]);

    [Fact]
    public void ADepartmentsStaffHoldItWithoutAScope()
    {
        // The core's permissions of a coordinator, on their own department.
        var role = Calculate([Events()], []);
        Assert.NotEmpty(role);
        Assert.All(role, held => Assert.True(Unscoped(role, held.Name)));

        // A module's permission reaches them through a grant to their position (division.json → positionGrants).
        var module = Calculate([Events()], [ToPosition(1, View, Department.ED, on: Department.ED)]);
        Assert.True(Unscoped(module, View));

        // The director holds every permission on every department.
        var director = Calculate([Director()], []);
        Assert.True(Unscoped(director, CorePermissions.LinksEdit));
        Assert.True(Unscoped(director, CorePermissions.PermissionsManage));

        // And a position of IVAO's headquarters, which reads the content of every department.
        Assert.True(Unscoped(Calculate([Headquarters()], []), CorePermissions.ContentView));
    }

    [Fact]
    public void AGrantToAPositionHoldsItWithoutAScopeOnItsOwnDepartmentOnAnotherAndOnEveryOne()
    {
        var onAnother = Calculate([Atc()], [ToPosition(1, Edit, Department.AOD, on: Department.ED)]);
        Assert.Contains(onAnother, held => held is { Name: Edit, FromOutside: true });
        Assert.True(Unscoped(onAnother, Edit));
        Assert.True(Unscoped(onAnother, View));

        var everywhere = Calculate([Atc()], [ToPosition(2, View, Department.AOD, on: null)]);
        Assert.True(Unscoped(everywhere, View));

        // A deny on one department next to it leaves every other one, each still without a scope.
        var denied = Calculate(
            [Atc()],
            [ToPosition(3, View, Department.AOD, on: null), ToPosition(4, View, Department.AOD, Department.FOD, GrantEffect.Deny)]);
        Assert.True(Unscoped(denied, View));
    }

    [Fact]
    public void AGrantToAPersonOnADepartmentOrOnEveryOneHoldsItWithoutAScope()
    {
        Assert.True(Unscoped(Calculate([Atc()], [ToPerson(1, View, Department.ED)]), View));
        Assert.True(Unscoped(Calculate([Atc()], [ToPerson(2, View, department: null)]), View));
    }

    [Fact]
    public void AFirTeamHoldsItWithoutAScopeOnItsFirAndOnTheDepartment()
    {
        UserGrant[] grants = [ToFirTeam(1, Edit, scope: null)];

        var own = Calculate([FirChief()], grants, FirStaffScope.Own);
        Assert.Contains(own, held => held is { Name: Edit, Fir: "XXAA" });
        Assert.True(Unscoped(own, Edit));

        var all = Calculate([FirChief()], grants, FirStaffScope.All);
        Assert.Contains(all, held => held is { Name: Edit, Fir: null, FromOutside: true });
        Assert.True(Unscoped(all, Edit));
    }

    [Fact]
    public void AGrantableGlobalPermissionAndTheViewItBringsAreHeldWithoutAScope()
    {
        var assigner = Calculate([Atc()], [ToPerson(1, CorePermissions.AwardsAssign, department: null)]);
        Assert.True(Unscoped(assigner, CorePermissions.AwardsAssign));
        Assert.True(Unscoped(assigner, CorePermissions.AwardsView));
    }

    [Fact]
    public void ASuperAdministratorHoldsEverythingWithoutAScope()
    {
        Assert.True(PermissionSet.HasUnscoped([], isSuperadmin: true, View));
        Assert.True(PermissionSet.HasUnscoped([new EffectivePermission(View, Department.ED, "grant:1", OneRow)], isSuperadmin: true, View));
    }

    [Fact]
    public void AGrantOnOneRowOrOneEventIsHeldOnlyWithAScope()
    {
        // To a person, on one event: held somewhere, so HasAny says yes; not without a scope.
        var onOneEvent = Calculate([Atc()], [ToPerson(1, View, Department.ED, OneEvent)]);
        Assert.True(PermissionSet.HasAny(onOneEvent, false, View));
        Assert.False(Unscoped(onOneEvent, View));

        // Its Edit on one row, and the View it implies on the same row.
        var onOneRow = Calculate([Atc()], [ToPerson(2, Edit, Department.ED, OneRow)]);
        Assert.False(Unscoped(onOneRow, Edit));
        Assert.False(Unscoped(onOneRow, View));

        // To a position, on one event.
        Assert.False(Unscoped(Calculate([Atc()], [ToPosition(3, View, Department.AOD, Department.ED, scope: OneEvent)]), View));

        // To a FIR team, on one row: the scope wins over the FIR.
        Assert.False(Unscoped(Calculate([FirChief()], [ToFirTeam(4, Edit, OneRow)], FirStaffScope.Own), Edit));
    }

    [Fact]
    public void OneHoldingWithoutAScopeIsEnoughAndAnotherPermissionIsNone()
    {
        EffectivePermission[] both =
        [
            new(View, Department.ED, "grant:1", OneRow),
            new(View, Department.SOD, "grant:2"),
        ];
        Assert.True(PermissionSet.HasUnscoped(both, false, View));

        Assert.False(PermissionSet.HasUnscoped([new EffectivePermission(Edit, Department.ED, "grant:1")], false, View));
        Assert.False(PermissionSet.HasUnscoped([], false, View));
        Assert.Throws<ArgumentException>(() => PermissionSet.HasUnscoped([], false, " "));
    }

    [Fact]
    public void TheCookiesReaderAndTheDefaultMemberAnswerTheSame()
    {
        EffectivePermission[] permissions =
        [
            new(View, Department.ED, "grant:1", FromOutside: true),
            new(Edit, Department.TD, "grant:2", Fir: "XXAA"),
            new(CorePermissions.LinksView, Department.ED, "grant:3", OneRow),
        ];

        var cookie = Reading([.. BuildIdentity([Atc()], permissions).Claims]);
        Assert.True(cookie.HasUnscoped(View));
        Assert.True(cookie.HasUnscoped(Edit));
        Assert.False(cookie.HasUnscoped(CorePermissions.LinksView));
        Assert.True(cookie.HasAny(CorePermissions.LinksView));

        // A double that does not write the member answers through the interface, with the same code.
        ICurrentUser plain = new Holding(permissions);
        Assert.True(plain.HasUnscoped(View));
        Assert.True(plain.HasUnscoped(Edit));
        Assert.False(plain.HasUnscoped(CorePermissions.LinksView));
    }

    [Fact]
    public void AReaderThatDoesNotKnowTheMarksReadsThemClosed()
    {
        // A package from before a rollback reads the FIR and the mark of "from outside" as pieces of the scope: held with one, so
        // not without one. Closed, never "the whole division".
        foreach (var claim in new[] { "Probe.View:ED@!", "Probe.View:TD@#XXAA" })
        {
            var (name, department, scope) = HubClaims.ParsePermission(claim);
            Assert.False(PermissionSet.HasUnscoped([new EffectivePermission(name, department, "cookie", scope)], false, View));
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static bool Unscoped(IEnumerable<EffectivePermission> permissions, string permission) =>
        PermissionSet.HasUnscoped(permissions, isSuperadmin: false, permission);

    private static StaffPosition Atc() => new("IT-AOC", Department.AOD, StaffLevel.Coordinator, null, StaffRole.AtcOps);

    private static StaffPosition Events() => new("IT-EC", Department.ED, StaffLevel.Coordinator, null, StaffRole.Events);

    private static StaffPosition Director() => new("IT-DIR", Department.HQ, StaffLevel.Coordinator, null, StaffRole.Director);

    private static StaffPosition Headquarters() => new("HQ-EC", null, StaffLevel.Coordinator, null, StaffRole.HqStaff);

    private static StaffPosition FirChief() => new("XXAA-CH", null, StaffLevel.Coordinator, "XXAA", StaffRole.FirChief);

    private static IReadOnlyList<EffectivePermission> Calculate(
        StaffPosition[] positions,
        UserGrant[] grants,
        FirStaffScope firStaffScope = FirStaffScope.Own) =>
        EffectivePermissionsCalculator.Calculate(positions, grants, false, DateTime.UtcNow, Catalogue, firStaffScope);

    /// <summary>A grant to the coordinators of <paramref name="position"/>, held on <paramref name="on"/> (null is every one).</summary>
    private static UserGrant ToPosition(
        long id,
        string permission,
        Department position,
        Department? on,
        GrantEffect effect = GrantEffect.Grant,
        string? scope = null) => new()
        {
            Id = id,
            PositionDepartment = position,
            PositionLevels = [StaffLevel.Coordinator],
            Kind = GrantKind.Permission,
            Value = permission,
            Department = on,
            ResourceScope = scope,
            Effect = effect,
        };

    private static UserGrant ToPerson(long id, string permission, Department? department, string? scope = null) => new()
    {
        Id = id,
        Vid = Member,
        Kind = GrantKind.Permission,
        Value = permission,
        Department = department,
        ResourceScope = scope,
        Effect = GrantEffect.Grant,
    };

    private static UserGrant ToFirTeam(long id, string permission, string? scope) => new()
    {
        Id = id,
        PositionFirTeam = true,
        PositionLevels = [StaffLevel.Coordinator],
        Kind = GrantKind.Permission,
        Value = permission,
        Department = Department.TD,
        ResourceScope = scope,
        Effect = GrantEffect.Grant,
    };

    private static ClaimsIdentity BuildIdentity(StaffPosition[] positions, EffectivePermission[] permissions) =>
        HubClaims.BuildIdentity(
            Member,
            firstName: "Test",
            lastName: "Unscoped",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: positions,
            permissions: permissions);

    /// <summary>The host's own reader of the cookie, on an identity made of these claims.</summary>
    private static HttpContextCurrentUser Reading(Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, HubClaims.CookieScheme)) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context }, Options.Create(Division()));
    }

    private static DivisionOptions Division() => new()
    {
        Code = "XX",
        CountryId = "XX",
        Domain = "hub.example.org",
        Name = new Dictionary<string, string> { ["en"] = "Example" },
        Locales = ["en"],
        DefaultLocale = "en",
        Timezone = "UTC",
    };

    /// <summary>An identity that writes only what the interface asks it to, as the integration suite's double does.</summary>
    private sealed class Holding(IReadOnlyList<EffectivePermission> permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public int Vid => Member;

        public string FirstName => "Test";

        public string LastName => "Unscoped";

        public bool IsSuperadmin => false;

        public bool IsStaff => true;

        public string Locale => "en";

        public IReadOnlySet<Department> Departments { get; } = new HashSet<Department>();

        public bool HasAllDepartments => false;

        public IReadOnlySet<string> Firs { get; } = new HashSet<string>();

        public IReadOnlyList<string> Positions => [];

        public IReadOnlyList<EffectivePermission> Permissions => permissions;

        public bool Has(string permission, Department department, string? resourceScope = null) =>
            PermissionSet.Has(Permissions, IsSuperadmin, permission, department, resourceScope);

        public bool HasAny(string permission) => PermissionSet.HasAny(Permissions, IsSuperadmin, permission);
    }
}
