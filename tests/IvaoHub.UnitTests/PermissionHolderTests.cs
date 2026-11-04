using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// A holder answers the handler's question with the row's FIR too (issue #319, item 7): a FIR team's permission matched no row
/// at all, because the FIR was never asked.
/// </summary>
public sealed class PermissionHolderTests
{
    private const string Permission = "Training.Edit";

    [Fact]
    public void AFirTeamHoldsThePermissionOnTheRowsOfItsFirAndNoOther()
    {
        var team = new PermissionHolder(
            Vid: 931010,
            IsSuperadmin: false,
            Permissions: [new EffectivePermission(Permission, Department.TD, "grant:1", ResourceScope: null, Fir: "LIRR")]);

        Assert.True(team.Has(Permission, Department.TD, resourceScope: null, fir: "LIRR"));
        Assert.True(team.Has(Permission, Department.TD, resourceScope: null, fir: "lirr"));
        Assert.False(team.Has(Permission, Department.TD, resourceScope: null, fir: "LIMM"));
        Assert.False(team.Has(Permission, Department.TD));
    }

    [Fact]
    public void ADepartmentsStaffIsNeverLimitedByTheFir()
    {
        var staff = new PermissionHolder(931011, false, [new EffectivePermission(Permission, Department.TD, "role:test")]);

        Assert.True(staff.Has(Permission, Department.TD, resourceScope: null, fir: "LIMM"));
        Assert.True(staff.Has(Permission, Department.TD));
    }
}
