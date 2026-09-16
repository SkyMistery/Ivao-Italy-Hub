using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The two relations a row can have with a person, asked of the single handler through the very
/// cookie a login writes (decision note of 15 September 2026, phase T3):
/// <list type="bullet">
/// <item>a permission granted on <b>one row</b> reaches that row and no other — a validator enabled
/// on one tour is not thereby enabled on the next;</item>
/// <item>whoever a row is <b>about</b> may not use the permissions the catalogue marks on it — and
/// that holds for a super administrator too, the one place the role does not bypass a policy.</item>
/// </list>
/// </summary>
public sealed class ResourceScopeAndStakeholderTests
{
    private const string Validate = "Probe.Validate";
    private const string View = "Probe.View";
    private const string Edit = "Probe.Edit";
    private const int Pilot = 780010;
    private const int Validator = 780011;

    private static readonly PermissionCatalog Catalogue = new([
        .. CorePermissions.All,
        new PermissionDescriptor(View, IsGlobal: false),
        new PermissionDescriptor(Edit, IsGlobal: false),
        new PermissionDescriptor(Validate, IsGlobal: false, DeniedToStakeholder: true),
    ]);

    private static readonly DivisionOptions Division = new()
    {
        Code = "IT",
        Locales = ["it", "en"],
        DefaultLocale = "it",
    };

    /// <summary>A report of a tour: in the care of FOD, scoped to its tour, about its pilot.</summary>
    private sealed class Report(long tour, int? pilot) : IOwnedByDepartment, IHasResourceScope, IHasStakeholder
    {
        public Department OwnerDepartment { get; set; } = Department.FOD;

        public string ResourceScope { get; } = $"probe:tour:{tour}";

        public int? StakeholderVid { get; } = pilot;
    }

    [Fact]
    public void APermissionGrantedOnOneRowReachesThatRowAndNoOther()
    {
        var user = SignedIn(Validator, superadmin: false,
            new EffectivePermission(Validate, Department.FOD, "grant:1", "probe:tour:42"));

        Assert.True(user.Has(Validate, Department.FOD, "probe:tour:42"));
        Assert.False(user.Has(Validate, Department.FOD, "probe:tour:43"));

        // Asked without a row, a scoped grant does not answer "held on the department".
        Assert.False(user.Has(Validate, Department.FOD));

        // But "held at all" it is: that is what opens the queue in read-only for them (design §4.1).
        Assert.True(user.HasAny(Validate));
    }

    [Fact]
    public void APermissionHeldWithoutAScopeReachesEveryRowAsItAlwaysDid()
    {
        var user = SignedIn(Validator, superadmin: false,
            new EffectivePermission(Validate, Department.FOD, "role:coordinator"));

        Assert.True(user.Has(Validate, Department.FOD, "probe:tour:42"));
        Assert.True(user.Has(Validate, Department.FOD, "probe:tour:43"));
        Assert.True(user.Has(Validate, Department.FOD));
    }

    [Fact]
    public void TheScopeTravelsInTheCookieAndAnOldCookieStillReads()
    {
        var scoped = new EffectivePermission(Validate, Department.FOD, "grant:1", "probe:tour:42");

        Assert.Equal("Probe.Validate:FOD@probe:tour:42", HubClaims.FormatPermission(scoped));
        Assert.Equal((Validate, Department.FOD, "probe:tour:42"), HubClaims.ParsePermission("Probe.Validate:FOD@probe:tour:42"));
        Assert.Equal((Validate, (Department?)null, "probe:tour:42"), HubClaims.ParsePermission("Probe.Validate@probe:tour:42"));

        // Written before scopes existed: no separator, no scope, read exactly as before.
        Assert.Equal((Edit, Department.FOD, (string?)null), HubClaims.ParsePermission("Probe.Edit:FOD"));
        Assert.Equal((Edit, (Department?)null, (string?)null), HubClaims.ParsePermission("Probe.Edit"));
    }

    [Fact]
    public void TwoGrantsOnTwoRowsAreTwoPermissionsNotOne()
    {
        var grants = new[]
        {
            Grant(1, Validator, Validate, Department.FOD, "probe:tour:42"),
            Grant(2, Validator, Validate, Department.FOD, "probe:tour:43"),
        };

        var effective = EffectivePermissionsCalculator.Calculate([], grants, isSuperadmin: false, DateTime.UtcNow, Catalogue);

        Assert.Equal(
            ["probe:tour:42", "probe:tour:43"],
            effective.Where(permission => permission.Name == Validate).Select(permission => permission.ResourceScope));
    }

    [Fact]
    public async Task TheHandlerLetsAScopedValidatorDecideOnlyTheirTour()
    {
        var user = SignedIn(Validator, superadmin: false,
            new EffectivePermission(Validate, Department.FOD, "grant:1", "probe:tour:42"));

        Assert.True(await AllowedAsync(user, new Report(tour: 42, pilot: Pilot), Validate));
        Assert.False(await AllowedAsync(user, new Report(tour: 43, pilot: Pilot), Validate));
    }

    [Fact]
    public async Task NobodyDecidesARowAboutThemselves()
    {
        var coordinator = SignedIn(Pilot, superadmin: false,
            new EffectivePermission(Validate, null, "role:coordinator"),
            new EffectivePermission(View, null, "role:coordinator"));

        Assert.False(await AllowedAsync(coordinator, new Report(tour: 42, pilot: Pilot), Validate));

        // Somebody else's report, same person: allowed.
        Assert.True(await AllowedAsync(coordinator, new Report(tour: 42, pilot: Validator), Validate));

        // And reading their own is never refused: only the permissions the catalogue marks are.
        Assert.True(await AllowedAsync(coordinator, new Report(tour: 42, pilot: Pilot), View));
    }

    [Fact]
    public async Task NotEvenASuperAdministratorDecidesARowAboutThemselves()
    {
        var superadmin = SignedIn(Pilot, superadmin: true);

        Assert.False(await AllowedAsync(superadmin, new Report(tour: 42, pilot: Pilot), Validate));
        Assert.True(await AllowedAsync(superadmin, new Report(tour: 42, pilot: Pilot), Edit));
        Assert.True(await AllowedAsync(superadmin, new Report(tour: 42, pilot: Validator), Validate));
    }

    [Fact]
    public async Task ARowAboutNobodyRefusesNobody()
    {
        var user = SignedIn(Pilot, superadmin: false, new EffectivePermission(Validate, null, "role:coordinator"));

        Assert.True(await AllowedAsync(user, new Report(tour: 42, pilot: null), Validate));
    }

    private static async Task<bool> AllowedAsync(ICurrentUser user, object resource, string permission)
    {
        var handler = new DepartmentAuthorizationHandler(user, Options.Create(Division), Catalogue);
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static UserGrant Grant(long id, int vid, string permission, Department department, string scope) => new()
    {
        Id = id,
        Vid = vid,
        Kind = GrantKind.Permission,
        Value = permission,
        Department = department,
        Effect = GrantEffect.Grant,
        ResourceScope = scope,
    };

    private static HttpContextCurrentUser SignedIn(int vid, bool superadmin, params EffectivePermission[] permissions)
    {
        var identity = HubClaims.BuildIdentity(
            vid: vid,
            firstName: "Test",
            lastName: "User",
            locale: "it",
            securityStamp: "stamp",
            isSuperadmin: superadmin,
            isStaff: true,
            positions: [],
            permissions: permissions);

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context }, Options.Create(Division));
    }
}
