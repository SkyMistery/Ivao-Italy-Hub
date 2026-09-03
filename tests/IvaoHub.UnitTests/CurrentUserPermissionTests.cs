using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The two questions the hub asks about a permission, read from the very claims a login writes:
/// "on this row?" and "at all?". They are separate methods on purpose — a single one with an
/// optional department would leave the caller to remember what a missing department meant.
/// </summary>
public sealed class CurrentUserPermissionTests
{
    private static readonly DivisionOptions Division = new()
    {
        Code = "IT",
        Locales = ["it", "en"],
        DefaultLocale = "it",
    };

    [Fact]
    public void ACoordinatorHoldsThePermissionOnTheirOwnDepartmentOnly()
    {
        var user = SignedIn(new EffectivePermission(CorePermissions.LinksEdit, Department.ED, "role:test"));

        Assert.True(user.Has(CorePermissions.LinksEdit, Department.ED));
        Assert.False(user.Has(CorePermissions.LinksEdit, Department.FOD));

        // But they may still open the screen: what they see in it is filtered row by row.
        Assert.True(user.HasAny(CorePermissions.LinksEdit));
        Assert.False(user.HasAny(CorePermissions.ContentPublish));
    }

    [Fact]
    public void APermissionStoredWithNoDepartmentIsHeldEverywhere()
    {
        // How a director and the web team travel: one claim instead of one per department.
        var user = SignedIn(new EffectivePermission(CorePermissions.LinksEdit, null, "role:test"));

        Assert.True(user.Has(CorePermissions.LinksEdit, Department.ED));
        Assert.True(user.Has(CorePermissions.LinksEdit, Department.FOD));
        Assert.True(user.HasAny(CorePermissions.LinksEdit));
    }

    [Fact]
    public void AGlobalPermissionAnswersTheQuestionThatHasNoDepartment()
    {
        var user = SignedIn(new EffectivePermission(CorePermissions.PermissionsManage, null, "role:test"));

        Assert.True(user.HasAny(CorePermissions.PermissionsManage));
        Assert.False(user.HasAny(CorePermissions.AuditView));
    }

    [Fact]
    public void ASuperAdministratorHoldsEverything()
    {
        var user = SignedIn(superadmin: true);

        Assert.True(user.Has(CorePermissions.ContentPublish, Department.SOD));
        Assert.True(user.HasAny(CorePermissions.PermissionsManage));
    }

    [Fact]
    public void AnAnonymousVisitorHoldsNothing()
    {
        var user = new HttpContextCurrentUser(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(Division));

        Assert.False(user.IsAuthenticated);
        Assert.False(user.Has(CorePermissions.LinksView, Department.ED));
        Assert.False(user.HasAny(CorePermissions.LinksView));
        Assert.Equal("it", user.Locale);
    }

    /// <summary>Builds the real cookie identity: the test reads what a login would have written.</summary>
    private static HttpContextCurrentUser SignedIn(params EffectivePermission[] permissions) =>
        SignedIn(superadmin: false, permissions);

    private static HttpContextCurrentUser SignedIn(bool superadmin, params EffectivePermission[] permissions)
    {
        var identity = HubClaims.BuildIdentity(
            vid: 600000,
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
