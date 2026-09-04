using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// Who reaches every department, and how the answer is arrived at.
/// <para>It is a fact of the role — director, web team, super administrator (design M0 section
/// 3.3) — carried by its own claim. It used to be inferred from the shape of the permission list:
/// "they hold a departmental permission with no department attached, so they must reach
/// everything". That inference was wrong in both directions, and both directions are pinned here,
/// because the answer feeds the global query filter and, from F5, the department filter of every
/// list in <c>MapCrud</c>.</para>
/// </summary>
public sealed class ReachesEveryDepartmentTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DivisionOptions Division = new()
    {
        Code = "IT",
        Locales = ["it", "en"],
        DefaultLocale = "it",
    };

    private static StaffPosition Director() =>
        new("IT-DIR", Department.HQ, StaffLevel.Coordinator, null, StaffRole.Director);

    private static StaffPosition Headquarters() =>
        new("HQ-WMA1", null, StaffLevel.Member, null, StaffRole.HqStaff);

    private static StaffPosition Events() =>
        new("IT-EC", Department.ED, StaffLevel.Coordinator, null, StaffRole.Events);

    [Fact]
    public void ADirectorReachesEveryDepartment()
    {
        Assert.True(SignedIn([Director()], Calculate([Director()])).HasAllDepartments);
    }

    [Fact]
    public void ACoordinatorDoesNot()
    {
        Assert.False(SignedIn([Events()], Calculate([Events()])).HasAllDepartments);
    }

    [Fact]
    public void ASuperAdministratorDoesWithNoPositionAtAll()
    {
        Assert.True(SignedIn([], [], superadmin: true).HasAllDepartments);
    }

    [Fact]
    public void AHeadquartersPositionDoesNot()
    {
        // It holds Content.View with no department, which is what used to be read as "reaches
        // everything" — and with it went the whole visibility filter, so a headquarters position
        // could read rows a department keeps to itself. It reads the division; it is not the
        // division's director.
        var permissions = Calculate([Headquarters()]);
        var user = SignedIn([Headquarters()], permissions);

        Assert.Contains(permissions, permission =>
            permission.Name == CorePermissions.ContentView && permission.Department is null);

        Assert.False(user.HasAllDepartments);
        Assert.True(user.IsStaff);
    }

    [Fact]
    public void ADenyOnOneDepartmentDoesNotTakeTheOtherEightAwayFromADirector()
    {
        // The deny expands the director's department-less entries into explicit departments. While
        // "reaches everything" was read back out of that list, the expansion answered a question it
        // was never asked: one deny left the director seeing nothing but HQ, and in F5 it would
        // have been a 403 on every other department's list.
        var grants = new[]
        {
            new UserGrant
            {
                Id = 1,
                Vid = 704798,
                Kind = GrantKind.Permission,
                Value = CorePermissions.LinksEdit,
                Department = Department.ED,
                Effect = GrantEffect.Deny,
            },
        };

        var user = SignedIn([Director()], Calculate([Director()], grants));

        Assert.True(user.HasAllDepartments);

        // The deny still bites, which is the whole point of it.
        Assert.False(user.Has(CorePermissions.LinksEdit, Department.ED));
        Assert.True(user.Has(CorePermissions.LinksEdit, Department.FOD));
    }

    [Fact]
    public void ADenyOnEveryDepartmentalPermissionStillLeavesTheDirectorReachingEveryDepartment()
    {
        // The extreme of the case above: nothing department-less survives in the list at all.
        var grants = PermissionCatalog.Core.Departmental
            .Select((name, index) => new UserGrant
            {
                Id = index + 1,
                Vid = 704798,
                Kind = GrantKind.Permission,
                Value = name,
                Department = Department.ED,
                Effect = GrantEffect.Deny,
            })
            .ToArray();

        var permissions = Calculate([Director()], grants);
        var user = SignedIn([Director()], permissions);

        Assert.DoesNotContain(permissions, permission =>
            permission.Department is null && !PermissionCatalog.Core.IsGlobal(permission.Name));

        Assert.True(user.HasAllDepartments);
    }

    private static IReadOnlyList<EffectivePermission> Calculate(
        IEnumerable<StaffPosition> positions,
        IEnumerable<UserGrant>? grants = null) =>
        EffectivePermissionsCalculator.Calculate(positions, grants ?? [], isSuperadmin: false, Now, PermissionCatalog.Core);

    /// <summary>The real cookie identity: what a login would have written, read back.</summary>
    private static HttpContextCurrentUser SignedIn(
        IReadOnlyList<StaffPosition> positions,
        IReadOnlyList<EffectivePermission> permissions,
        bool superadmin = false)
    {
        var identity = HubClaims.BuildIdentity(
            vid: 704798,
            firstName: "Test",
            lastName: "User",
            locale: "it",
            securityStamp: "stamp",
            isSuperadmin: superadmin,
            isStaff: true,
            positions: positions,
            permissions: permissions);

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context }, Options.Create(Division));
    }
}
