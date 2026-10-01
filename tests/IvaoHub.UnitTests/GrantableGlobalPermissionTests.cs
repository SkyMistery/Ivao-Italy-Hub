using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The one global permission a grant may confer (M4, E10f, note 2026-10-01-chi-assegna-gli-award-con-un-grant, decided by Carmine
/// on #205): <c>Awards.Assign</c>, which a division gives to whom it chooses — "configuration, not code", as plan section 9.1 has
/// always said. It is declared on the permission and conferred only whole, and nothing else global comes with it:
/// <c>Permissions.Manage</c>, the other global permissions and the status of super administrator stay out of a grant's reach.
/// </summary>
public sealed class GrantableGlobalPermissionTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static StaffPosition Membership(StaffLevel level) => level switch
    {
        StaffLevel.Coordinator => new("IT-MC", Department.MD, level, null, StaffRole.Membership),
        StaffLevel.Assistant => new("IT-MAC", Department.MD, level, null, StaffRole.Membership),
        _ => new("IT-MA1", Department.MD, level, null, StaffRole.Membership),
    };

    private static StaffPosition Events() => new("IT-EC", Department.ED, StaffLevel.Coordinator, null, StaffRole.Events);

    private static StaffPosition WebMaster() => new("IT-WM", Department.WD, StaffLevel.Coordinator, null, StaffRole.Web);

    private static StaffPosition FirChief() => new("LIRR-CH", null, StaffLevel.Coordinator, "LIRR", StaffRole.FirChief);

    /// <summary>What the division's file gives the membership department: to its coordinator and its assistant, whole.</summary>
    private static UserGrant ToTheMembershipDepartment(string value, long id = 50, GrantEffect effect = GrantEffect.Grant) => new()
    {
        Id = id,
        PositionDepartment = Department.MD,
        PositionLevels = [StaffLevel.Coordinator, StaffLevel.Assistant],
        Kind = GrantKind.Permission,
        Value = value,
        Effect = effect,
    };

    private static IReadOnlyList<EffectivePermission> Calculate(IEnumerable<StaffPosition> positions, params UserGrant[] grants) =>
        EffectivePermissionsCalculator.Calculate(positions, grants, isSuperadmin: false, Now, PermissionCatalog.Core, FirStaffScope.Own);

    private static bool HoldsAwardsAssign(IEnumerable<EffectivePermission> permissions) =>
        permissions.Any(permission => permission.Name == CorePermissions.AwardsAssign);

    [Fact]
    public void AwardsAssignIsTheOnlyGlobalPermissionOfTheCoreAGrantMayConfer()
    {
        var catalogue = PermissionCatalog.Core;

        Assert.Equal(
            new[] { CorePermissions.AwardsAssign },
            catalogue.Global.Where(name => !catalogue.IsClosedToGrants(name)));

        Assert.True(catalogue.IsClosedToGrants(CorePermissions.PermissionsManage));
        Assert.True(catalogue.IsClosedToGrants(CorePermissions.ModulesManage));
        Assert.True(catalogue.IsClosedToGrants(CorePermissions.AuditView));
        Assert.True(catalogue.IsClosedToGrants(CorePermissions.AdminAccess));
        Assert.True(catalogue.IsClosedToGrants(CorePermissions.CalendarManageKinds));

        // A permission of a department is never closed to a grant; a name the catalogue does not know is refused earlier, by
        // IsKnown, and answers false here as it does for IsGlobal.
        Assert.All(catalogue.Departmental, name => Assert.False(catalogue.IsClosedToGrants(name), name));
        Assert.False(catalogue.IsClosedToGrants("Invented.Assign"));
    }

    [Theory]
    [InlineData(CorePermissions.PermissionsManage)]
    [InlineData(CorePermissions.ModulesManage)]
    [InlineData(CorePermissions.AuditView)]
    [InlineData(CorePermissions.AdminAccess)]
    [InlineData(CorePermissions.CalendarManageKinds)]
    [InlineData(CorePermissions.LinksEdit)]
    public void ACatalogueThatMakesAnotherPermissionGrantableDoesNotStart(string name)
    {
        // Awards.Assign and no other (Carmine on #205, answer 2): whoever held Permissions.Manage by a grant could hand it on by
        // another, and every other global permission comes with the staff positions. A change of the code that declared one so
        // stops the start instead of opening the hub (the reviewer's point on #213); a second one is a decision with its note.
        var descriptors = CorePermissions.All.Select(permission => permission.Name == name
            ? permission with { GrantableAlthoughGlobal = true }
            : permission);

        var refused = Assert.Throws<InvalidOperationException>(() => new PermissionCatalog(descriptors));
        Assert.Contains(name, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AModuleCannotMakeAGlobalPermissionOfItsOwnGrantable()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => new PermissionCatalog(
            [.. CorePermissions.All, new PermissionDescriptor("Sample.Read", IsGlobal: true, GrantableAlthoughGlobal: true)]));

        Assert.Contains("Sample.Read", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StaffLevel.Coordinator)]
    [InlineData(StaffLevel.Assistant)]
    public void AGrantToAPositionConfersAwardsAssignAndNothingElseGlobal(StaffLevel level)
    {
        var permissions = Calculate([Membership(level)], ToTheMembershipDepartment(CorePermissions.AwardsAssign));

        // Held with no department, as a role holds it, and it says it comes from the grant.
        var assign = Assert.Single(permissions, permission => permission.Name == CorePermissions.AwardsAssign);
        Assert.Null(assign.Department);
        Assert.Equal($"{EffectivePermissionsCalculator.GrantSourcePrefix}50", assign.Source);

        // The one global permission they hold, and nothing makes them a super administrator.
        Assert.Equal(
            new[] { CorePermissions.AwardsAssign },
            permissions.Where(permission => PermissionCatalog.Core.IsGlobal(permission.Name)).Select(permission => permission.Name));
        Assert.DoesNotContain(permissions, permission => permission.Source == EffectivePermissionsCalculator.SuperadminSource);

        // What the queue, the register, the menu and the recipients of the daily mail ask: "at all?".
        Assert.True(PermissionSet.HasAny(permissions, isSuperadmin: false, CorePermissions.AwardsAssign));
        Assert.False(PermissionSet.HasAny(permissions, isSuperadmin: false, CorePermissions.PermissionsManage));

        // A permission of an area brings the area's View, in the one place that says so: whoever assigns reads every award (T4b).
        // Both say they came from outside, which keeps their holder out of every department but their own (below).
        Assert.True(assign.FromOutside);
        Assert.Contains(
            permissions,
            permission => permission.Name == CorePermissions.AwardsView && permission.Department is null && permission.FromOutside);
    }

    [Fact]
    public void AGrantOfItTakesNobodyIntoAnyDepartment()
    {
        // A global permission has no department, so a grant of it reaches none — not every one, as a grant of a permission of a
        // department given with no department does (BuildIdentity's claims). The session of E2b found the widening in the first
        // draft of E10f: the heads of the membership department would have seen every department's rows.
        var membership = Membership(StaffLevel.Coordinator);
        Assert.Equal(
            new[] { nameof(Department.MD) },
            DepartmentsOf([membership], ToTheMembershipDepartment(CorePermissions.AwardsAssign)));

        // To a person, the same: only the department of their own position.
        var toAPerson = new UserGrant
        {
            Id = 61,
            Vid = 761080,
            Kind = GrantKind.Permission,
            Value = CorePermissions.AwardsAssign,
            Effect = GrantEffect.Grant,
        };
        Assert.Equal(new[] { nameof(Department.ED) }, DepartmentsOf([Events()], toAPerson));

        // While a grant of a permission of a department with no department still reaches every one (note
        // 2026-09-13-contenuti-centralizzati §3.1), even next to it: the one that reaches wins over the one that does not.
        var everywhere = new UserGrant
        {
            Id = 99,
            Vid = 761080,
            Kind = GrantKind.Permission,
            Value = CorePermissions.AwardsView,
            Effect = GrantEffect.Grant,
        };
        Assert.Equal(
            RolePermissionMatrix.AllDepartments.Select(department => department.ToString()).Order(StringComparer.Ordinal),
            DepartmentsOf([Events()], toAPerson, everywhere).Order(StringComparer.Ordinal));
    }

    /// <summary>The departments the cookie of a login says its holder is inside, for the purpose of seeing.</summary>
    private static IEnumerable<string> DepartmentsOf(StaffPosition[] positions, params UserGrant[] grants)
    {
        var identity = HubClaims.BuildIdentity(
            vid: 761080,
            firstName: "Test",
            lastName: "Assigner",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: positions,
            permissions: Calculate(positions, grants));

        Assert.DoesNotContain(identity.Claims, claim => claim.Type == HubClaims.AllDepartments);
        return identity.FindAll(HubClaims.Department).Select(claim => claim.Value);
    }

    [Fact]
    public void AnAdvisorOfTheSameDepartmentIsNotGivenIt()
    {
        // The levels are the division's choice, read as for any grant to a position.
        Assert.False(HoldsAwardsAssign(Calculate([Membership(StaffLevel.Advisor)], ToTheMembershipDepartment(CorePermissions.AwardsAssign))));
    }

    [Fact]
    public void AGrantToAPersonConfersItToo()
    {
        // The case EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission held for Awards.Assign until E10f.
        var grant = new UserGrant
        {
            Id = 60,
            Vid = 761080,
            Kind = GrantKind.Permission,
            Value = CorePermissions.AwardsAssign,
            Effect = GrantEffect.Grant,
        };

        var permissions = Calculate([Events()], grant);

        Assert.True(HoldsAwardsAssign(permissions));
        Assert.Equal(
            new[] { CorePermissions.AwardsAssign },
            permissions.Where(permission => PermissionCatalog.Core.IsGlobal(permission.Name)).Select(permission => permission.Name));
    }

    [Fact]
    public void NextToTheOtherGlobalPermissionsOnlyAwardsAssignIsConferred()
    {
        var permissions = Calculate(
            [Membership(StaffLevel.Coordinator)],
            ToTheMembershipDepartment(CorePermissions.AwardsAssign, id: 50),
            ToTheMembershipDepartment(CorePermissions.PermissionsManage, id: 51),
            ToTheMembershipDepartment(CorePermissions.ModulesManage, id: 52),
            ToTheMembershipDepartment(CorePermissions.AuditView, id: 53),
            ToTheMembershipDepartment(CorePermissions.AdminAccess, id: 54),
            ToTheMembershipDepartment(CorePermissions.CalendarManageKinds, id: 55));

        Assert.Equal(
            new[] { CorePermissions.AwardsAssign },
            permissions.Where(permission => PermissionCatalog.Core.IsGlobal(permission.Name)).Select(permission => permission.Name));
    }

    [Fact]
    public void OnlyAWholeGrantConfersIt()
    {
        // A global permission is only ever asked "at all?", which would read a department, a row or a FIR as everywhere. Neither
        // the screen nor the seed writes such a grant, and one written by hand is not honoured: the side that closes.
        var onADepartment = ToTheMembershipDepartment(CorePermissions.AwardsAssign);
        onADepartment.Department = Department.MD;

        var onARow = ToTheMembershipDepartment(CorePermissions.AwardsAssign);
        onARow.ResourceScope = "flightops:tour:42";

        var toTheTeamOfAFir = new UserGrant
        {
            Id = 56,
            PositionFirTeam = true,
            PositionLevels = [StaffLevel.Coordinator],
            Kind = GrantKind.Permission,
            Value = CorePermissions.AwardsAssign,
            Effect = GrantEffect.Grant,
        };

        Assert.False(HoldsAwardsAssign(Calculate([Membership(StaffLevel.Coordinator)], onADepartment)));
        Assert.False(HoldsAwardsAssign(Calculate([Membership(StaffLevel.Coordinator)], onARow)));
        Assert.False(HoldsAwardsAssign(Calculate([FirChief()], toTheTeamOfAFir)));
    }

    [Fact]
    public void AWholeDenyTakesItFromWhoeverHoldsItByTheirRole()
    {
        // A division may keep its web team out of the queue. Until E10f a deny of a global permission was worth nothing.
        var deny = new UserGrant
        {
            Id = 70,
            PositionDepartment = Department.WD,
            PositionLevels = [StaffLevel.Coordinator],
            Kind = GrantKind.Permission,
            Value = CorePermissions.AwardsAssign,
            Effect = GrantEffect.Deny,
        };

        var permissions = Calculate([WebMaster()], deny);

        Assert.False(HoldsAwardsAssign(permissions));
        Assert.Contains(permissions, permission => permission.Name == CorePermissions.PermissionsManage);

        // Whole only, as a grant: a deny on one department is not honoured.
        deny.Department = Department.FOD;
        Assert.True(HoldsAwardsAssign(Calculate([WebMaster()], deny)));
    }

    [Fact]
    public void ADenyOfAGlobalPermissionClosedToGrantsIsStillWorthNothing()
    {
        var deny = new UserGrant
        {
            Id = 71,
            PositionDepartment = Department.WD,
            PositionLevels = [StaffLevel.Coordinator],
            Kind = GrantKind.Permission,
            Value = CorePermissions.AuditView,
            Effect = GrantEffect.Deny,
        };

        Assert.Contains(Calculate([WebMaster()], deny), permission => permission.Name == CorePermissions.AuditView);
    }
}
