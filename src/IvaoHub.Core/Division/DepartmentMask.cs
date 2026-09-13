using System.Reflection;

namespace IvaoHub.Core.Division;

/// <summary>
/// A set of departments as one integer: a bit per department (M2, note
/// 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3). It is how a row of a module is in the
/// care of several departments and still readable in one step by the global query filter — "at
/// least one in common" is <c>(row &amp; reader) != 0</c>, which SQL answers on a column and a
/// parameter, where a list or a second table would need a join the filter cannot write.
/// <para>⚠️ The bit of each department is <b>written down here</b> and never derived from the order
/// of <see cref="Department"/>: the value is stored, and a department added to the enum in the
/// middle must not move the others. New departments go at the end of <see cref="Bits"/>, and a
/// test says every department has one.</para>
/// </summary>
public static class DepartmentMask
{
    /// <summary>The name of the column a row owned by several departments declares.</summary>
    public const string PropertyName = nameof(IOwnedByDepartment.OwnerDepartmentMask);

    /// <summary>Department by bit, in the order the bits were given out. Append only.</summary>
    private static readonly Department[] Bits =
    [
        Department.HQ,
        Department.SOD,
        Department.FOD,
        Department.AOD,
        Department.TD,
        Department.MD,
        Department.ED,
        Department.PRD,
        Department.WD,
    ];

    /// <summary>The bit of one department.</summary>
    public static int Of(Department department)
    {
        var index = Array.IndexOf(Bits, department);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(department), department, "A department with no bit: add it at the end of DepartmentMask.Bits.");
        }

        return 1 << index;
    }

    /// <summary>The mask of several.</summary>
    public static int Of(IEnumerable<Department> departments)
    {
        ArgumentNullException.ThrowIfNull(departments);
        return departments.Aggregate(0, (mask, department) => mask | Of(department));
    }

    /// <summary>The departments of a mask, in the order of the enum.</summary>
    public static IReadOnlyList<Department> Departments(int mask) =>
        [.. Enum.GetValues<Department>().Where(department => (mask & Of(department)) != 0)];

    /// <summary>
    /// Whether rows of this type keep a set of departments in a column of their own
    /// (<see cref="IOwnedByDepartment.OwnerDepartmentMask"/> declared on the class), rather than the
    /// one <see cref="IOwnedByDepartment.OwnerDepartment"/> every editorial row has.
    /// </summary>
    public static bool IsStoredOn(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance) is { CanWrite: true };
    }
}
