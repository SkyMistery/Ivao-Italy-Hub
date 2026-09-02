using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// Derived from the positions, union the grants, minus the denies. The safety rails matter as much
/// as the arithmetic: a grant may never hand out a global permission, an expired or suspended
/// grant is worth nothing, and a deny on one department bites even when the permission is held
/// everywhere (plan section 6.3).
/// </summary>
public sealed class EffectivePermissionsTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

    private static StaffPosition Events(StaffLevel level = StaffLevel.Coordinator) =>
        new("IT-EC", Department.EV, level, null, StaffRole.Events);

    private static StaffPosition Director() =>
        new("IT-DIR", Department.HQ, StaffLevel.Coordinator, null, StaffRole.Director);

    private static UserGrant Grant(
        string value,
        Department? department = null,
        GrantEffect effect = GrantEffect.Grant,
        DateTime? expiresAt = null,
        DateTime? suspendedAt = null,
        long id = 1) =>
        new()
        {
            Id = id,
            Vid = 704798,
            Kind = GrantKind.Permission,
            Value = value,
            Department = department,
            Effect = effect,
            ExpiresAt = expiresAt,
            SuspendedAt = suspendedAt,
        };

    private static IReadOnlyList<EffectivePermission> Calculate(
        IEnumerable<StaffPosition>? positions = null,
        IEnumerable<UserGrant>? grants = null,
        bool isSuperadmin = false) =>
        EffectivePermissionsCalculator.Calculate(positions ?? [], grants ?? [], isSuperadmin, Now);

    private static bool Holds(IReadOnlyList<EffectivePermission> permissions, string name, Department? department) =>
        permissions.Any(p => p.Name == name && (p.Department is null || p.Department == department));

    [Fact]
    public void AMemberWithNoPositionHoldsNothing()
    {
        Assert.Empty(Calculate());
    }

    [Fact]
    public void ACoordinatorHoldsTheirDepartmentAndNoOther()
    {
        var permissions = Calculate([Events()]);

        Assert.True(Holds(permissions, CorePermissions.ContentEdit, Department.EV));
        Assert.False(Holds(permissions, CorePermissions.ContentEdit, Department.FO));
        Assert.DoesNotContain(permissions, p => CorePermissions.IsGlobalPermission(p.Name));
    }

    [Fact]
    public void TheDirectorHoldsEveryDepartmentAndTheGlobalPermissions()
    {
        var permissions = Calculate([Director()]);

        foreach (var department in Enum.GetValues<Department>())
        {
            Assert.True(Holds(permissions, CorePermissions.ContentPublish, department));
        }

        Assert.True(Holds(permissions, CorePermissions.PermissionsManage, null));
        Assert.True(Holds(permissions, CorePermissions.AdminAccess, null));
    }

    [Fact]
    public void APermissionHeldEverywhereIsStoredOnceWithNoDepartment()
    {
        // The list travels in the authentication cookie: one entry, not one per department.
        var permissions = Calculate([Director()]);

        Assert.All(permissions, permission => Assert.Null(permission.Department));
    }

    [Fact]
    public void HeadquartersReadsEverythingAndWritesNothing()
    {
        var position = new StaffPosition("HQ-EC", null, StaffLevel.Member, null, StaffRole.HqStaff);

        var permissions = Calculate([position]);

        Assert.True(Holds(permissions, CorePermissions.ContentView, Department.FO));
        Assert.False(Holds(permissions, CorePermissions.ContentEdit, Department.FO));
    }

    [Fact]
    public void AFirPositionMakesNobodyAnEditorOfADepartment()
    {
        var position = new StaffPosition("LIRR-CH", null, StaffLevel.Coordinator, "LIRR", StaffRole.FirChief);

        Assert.Empty(Calculate([position]));
    }

    [Fact]
    public void AGrantAddsAPermissionOnAnotherDepartment()
    {
        var permissions = Calculate([Events()], [Grant(CorePermissions.LinksEdit, Department.FO)]);

        Assert.True(Holds(permissions, CorePermissions.LinksEdit, Department.FO));
        Assert.Contains(permissions, p => p.Name == CorePermissions.LinksEdit && p.Source == "grant:1");
    }

    [Fact]
    public void EditImpliesViewEvenWhenItComesFromAGrant()
    {
        var permissions = Calculate(grants: [Grant(CorePermissions.LinksEdit, Department.FO)]);

        Assert.True(Holds(permissions, CorePermissions.LinksView, Department.FO));
    }

    [Fact]
    public void AnExpiredGrantIsWorthNothing()
    {
        var expired = Grant(CorePermissions.LinksEdit, Department.FO, expiresAt: Now.AddDays(-1));

        Assert.False(Holds(Calculate(grants: [expired]), CorePermissions.LinksEdit, Department.FO));
    }

    [Fact]
    public void AGrantThatHasNotExpiredYetStillCounts()
    {
        var live = Grant(CorePermissions.LinksEdit, Department.FO, expiresAt: Now.AddDays(1));

        Assert.True(Holds(Calculate(grants: [live]), CorePermissions.LinksEdit, Department.FO));
    }

    [Fact]
    public void ASuspendedGrantIsWorthNothingButIsNotDeleted()
    {
        var suspended = Grant(CorePermissions.LinksEdit, Department.FO, suspendedAt: Now.AddDays(-1));

        Assert.False(Holds(Calculate(grants: [suspended]), CorePermissions.LinksEdit, Department.FO));
    }

    [Theory]
    [InlineData(CorePermissions.PermissionsManage)]
    [InlineData(CorePermissions.ModulesManage)]
    [InlineData(CorePermissions.AuditView)]
    [InlineData(CorePermissions.AwardsAssign)]
    [InlineData(CorePermissions.AdminAccess)]
    public void AGrantCanNeverConferAGlobalPermission(string name)
    {
        var permissions = Calculate([Events()], [Grant(name)]);

        Assert.DoesNotContain(permissions, permission => permission.Name == name);
    }

    [Fact]
    public void AGrantOfAnUnknownPermissionIsIgnored()
    {
        Assert.Empty(Calculate(grants: [Grant("Nonsense.Edit", Department.FO)]));
    }

    [Fact]
    public void ADenyRemovesADerivedPermissionOnThatDepartment()
    {
        var deny = Grant(CorePermissions.ContentPublish, Department.EV, GrantEffect.Deny);

        var permissions = Calculate([Events()], [deny]);

        Assert.False(Holds(permissions, CorePermissions.ContentPublish, Department.EV));
        Assert.True(Holds(permissions, CorePermissions.ContentEdit, Department.EV));
    }

    [Fact]
    public void ADenyWithNoDepartmentRemovesThePermissionEverywhere()
    {
        var deny = Grant(CorePermissions.ContentPublish, null, GrantEffect.Deny);

        var permissions = Calculate([Director()], [deny]);

        Assert.DoesNotContain(permissions, permission => permission.Name == CorePermissions.ContentPublish);
    }

    [Fact]
    public void ADenyOnOneDepartmentBitesEvenWhenThePermissionIsHeldEverywhere()
    {
        var deny = Grant(CorePermissions.ContentPublish, Department.FO, GrantEffect.Deny);

        var permissions = Calculate([Director()], [deny]);

        Assert.False(Holds(permissions, CorePermissions.ContentPublish, Department.FO));
        Assert.True(Holds(permissions, CorePermissions.ContentPublish, Department.EV));
    }

    [Fact]
    public void ADenyWinsOverAGrantOfTheSamePermission()
    {
        var grant = Grant(CorePermissions.LinksEdit, Department.FO, id: 1);
        var deny = Grant(CorePermissions.LinksEdit, Department.FO, GrantEffect.Deny, id: 2);

        Assert.False(Holds(Calculate(grants: [grant, deny]), CorePermissions.LinksEdit, Department.FO));
    }

    [Fact]
    public void TheSuperAdministratorHoldsTheWholeCatalogue()
    {
        var permissions = Calculate(isSuperadmin: true);

        Assert.Equal(CorePermissions.All.Count, permissions.Count);
        Assert.All(permissions, permission => Assert.Equal("superadmin", permission.Source));
    }

    [Fact]
    public void TwoPositionsAddUp()
    {
        var atc = new StaffPosition("IT-AOA1", Department.AO, StaffLevel.Advisor, null, StaffRole.AtcOps);

        var permissions = Calculate([Events(), atc]);

        Assert.True(Holds(permissions, CorePermissions.ContentPublish, Department.EV));
        Assert.True(Holds(permissions, CorePermissions.ContentEdit, Department.AO));
        Assert.False(Holds(permissions, CorePermissions.ContentPublish, Department.AO));
    }
}
