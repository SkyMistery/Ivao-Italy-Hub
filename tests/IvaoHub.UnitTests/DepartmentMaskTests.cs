using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The set of departments a row of a module is in the care of, as a stored integer (M2). The bit of a
/// department is a stored value: a department without one would be a row nobody finds, and one
/// that moved would hand the rows of a department to another.
/// </summary>
public sealed class DepartmentMaskTests
{
    [Fact]
    public void EveryDepartmentHasABitOfItsOwn()
    {
        var bits = Enum.GetValues<Department>().Select(DepartmentMask.Of).ToArray();

        Assert.Equal(bits.Length, bits.Distinct().Count());
        Assert.All(bits, bit => Assert.Equal(1, System.Numerics.BitOperations.PopCount((uint)bit)));
    }

    [Fact]
    public void TheBitsAreTheOnesAlreadyStored()
    {
        // Pinned: changing one of these is a migration of every stored row, not a refactoring.
        Assert.Equal(1, DepartmentMask.Of(Department.HQ));
        Assert.Equal(2, DepartmentMask.Of(Department.SOD));
        Assert.Equal(64, DepartmentMask.Of(Department.ED));
        Assert.Equal(256, DepartmentMask.Of(Department.WD));
    }

    [Fact]
    public void ASetComesBackAsTheDepartmentsItWasMadeOf()
    {
        var mask = DepartmentMask.Of([Department.ED, Department.SOD, Department.ED]);

        Assert.Equal([Department.SOD, Department.ED], DepartmentMask.Departments(mask));
    }

    [Fact]
    public void AnEditorialRowHasOneDepartmentAndAModuleRowDeclaresItsColumn()
    {
        Assert.False(DepartmentMask.IsStoredOn(typeof(Core.Content.ContentEntry)));
        Assert.True(DepartmentMask.IsStoredOn(typeof(SeveralDepartments)));

        IOwnedByDepartment editorial = new OneDepartment();
        Assert.Equal([Department.TD], editorial.OwnerDepartments);

        IOwnedByDepartment module = new SeveralDepartments { OwnerDepartmentMask = DepartmentMask.Of([Department.SOD, Department.ED]) };
        Assert.Equal([Department.SOD, Department.ED], module.OwnerDepartments);
    }

    private sealed class OneDepartment : IOwnedByDepartment
    {
        public Department OwnerDepartment => Department.TD;
    }

    private sealed class SeveralDepartments : IOwnedByDepartment
    {
        public Department OwnerDepartment => Department.ED;

        public int OwnerDepartmentMask { get; set; }
    }
}
