using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>The matrix, row by row. It is the only place that decides who may do what.</summary>
public sealed class RolePermissionMatrixTests
{
    private static StaffPosition Position(StaffRole role, StaffLevel level, Department? department = null) =>
        new($"IT-{role}", department, level, null, role);

    [Fact]
    public void ACoordinatorRunsTheirOwnDepartmentEntirely()
    {
        string[] expected =
        [
            CorePermissions.ContentView,
            CorePermissions.ContentEdit,
            CorePermissions.ContentPublish,
            CorePermissions.ContentManageTemplates,
            CorePermissions.LinksView,
            CorePermissions.LinksEdit,
            CorePermissions.CalendarView,
            CorePermissions.CalendarEdit,
        ];

        Assert.Equal(expected.Order(), RolePermissionMatrix.OnOwnDepartment(StaffLevel.Coordinator).Order());
    }

    [Fact]
    public void AnAssistantHasTheSameReachAsTheirCoordinator()
    {
        Assert.Equal(
            RolePermissionMatrix.OnOwnDepartment(StaffLevel.Coordinator).Order(),
            RolePermissionMatrix.OnOwnDepartment(StaffLevel.Assistant).Order());
    }

    [Fact]
    public void AnAdvisorEditsButNeitherPublishesNorTouchesTemplates()
    {
        var permissions = RolePermissionMatrix.OnOwnDepartment(StaffLevel.Advisor);

        Assert.Contains(CorePermissions.ContentEdit, permissions);
        Assert.Contains(CorePermissions.LinksEdit, permissions);
        Assert.Contains(CorePermissions.CalendarEdit, permissions);
        Assert.DoesNotContain(CorePermissions.ContentPublish, permissions);
        Assert.DoesNotContain(CorePermissions.ContentManageTemplates, permissions);
    }

    [Fact]
    public void ATrainerHoldsNothingOfTheCore()
    {
        Assert.Empty(RolePermissionMatrix.OnOwnDepartment(StaffLevel.Member));
    }

    [Theory]
    [InlineData(StaffRole.Director, StaffLevel.Coordinator, true)]
    [InlineData(StaffRole.Director, StaffLevel.Assistant, true)]
    [InlineData(StaffRole.Web, StaffLevel.Coordinator, true)]
    [InlineData(StaffRole.Web, StaffLevel.Assistant, true)]
    [InlineData(StaffRole.Web, StaffLevel.Advisor, false)]
    [InlineData(StaffRole.Events, StaffLevel.Coordinator, false)]
    [InlineData(StaffRole.AtcOps, StaffLevel.Coordinator, false)]
    [InlineData(StaffRole.FirChief, StaffLevel.Coordinator, false)]
    public void OnlyTheDirectorAndTheWebTeamReachEveryDepartment(StaffRole role, StaffLevel level, bool expected)
    {
        Assert.Equal(expected, RolePermissionMatrix.ReachesEveryDepartment(Position(role, level)));
    }

    [Theory]
    [InlineData(StaffRole.HqStaff, true)]
    [InlineData(StaffRole.Director, false)]
    [InlineData(StaffRole.Events, false)]
    public void OnlyHeadquartersReadsEveryDepartmentWithoutWriting(StaffRole role, bool expected)
    {
        Assert.Equal(expected, RolePermissionMatrix.ReadsEveryDepartment(Position(role, StaffLevel.Member)));
    }

    [Fact]
    public void EveryDepartmentalAreaDeclaresBothViewAndEdit()
    {
        var areas = PermissionCatalog.Core.Departmental
            .Select(name => name[..name.IndexOf('.', StringComparison.Ordinal)])
            .Distinct(StringComparer.Ordinal);

        foreach (var area in areas)
        {
            Assert.Contains($"{area}.View", PermissionCatalog.Core.Departmental);
            Assert.Contains($"{area}.Edit", PermissionCatalog.Core.Departmental);
        }
    }

    [Fact]
    public void EveryPermissionTheMatrixHandsOutExistsInTheCatalogue()
    {
        foreach (var level in Enum.GetValues<StaffLevel>())
        {
            foreach (var name in RolePermissionMatrix.OnOwnDepartment(level))
            {
                Assert.True(PermissionCatalog.Core.IsKnown(name), name);
                Assert.False(PermissionCatalog.Core.IsGlobal(name), name);
            }
        }
    }
}
