using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Who the backbone thinks is asking, decided by the test instead of by a cookie. F4 has no
/// endpoints yet, so the tests drive the context and the authorization service directly; the rule
/// that answers "may they?" is the real one, only the identity is supplied.
/// </summary>
public sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; private set; }

    public int Vid { get; private set; }

    public string FirstName => "Test";

    public string LastName => "User";

    public bool IsSuperadmin { get; private set; }

    public bool IsStaff { get; private set; }

    public string Locale { get; private set; } = "it";

    public IReadOnlySet<Department> Departments { get; private set; } = new HashSet<Department>();

    public bool HasAllDepartments { get; private set; }

    public IReadOnlySet<string> Firs { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Positions { get; private set; } = [];

    public IReadOnlyList<EffectivePermission> Permissions { get; private set; } = [];

    /// <summary>The same rules the real implementation applies, including the superadmin bypass.</summary>
    public bool Has(string permission, Department department) =>
        IsSuperadmin
        || Permissions.Any(held =>
            string.Equals(held.Name, permission, StringComparison.Ordinal)
            && (held.Department is null || held.Department == department));

    public bool HasAny(string permission) =>
        IsSuperadmin
        || Permissions.Any(held => string.Equals(held.Name, permission, StringComparison.Ordinal));

    public void Anonymous()
    {
        IsAuthenticated = false;
        Vid = 0;
        IsSuperadmin = false;
        IsStaff = false;
        HasAllDepartments = false;
        Departments = new HashSet<Department>();
        Permissions = [];
    }

    /// <summary>A member with no staff position: logged in, and nothing else.</summary>
    public void Member(int vid)
    {
        Anonymous();
        IsAuthenticated = true;
        Vid = vid;
    }

    /// <summary>
    /// The coordinator of a department: every departmental permission, on that department only.
    /// It is the identity the write guard is meant to hold in place.
    /// </summary>
    public void Coordinator(int vid, Department department)
    {
        Member(vid);
        IsStaff = true;
        Departments = new HashSet<Department> { department };
        Permissions = [.. CorePermissions.Departmental.Select(name =>
            new EffectivePermission(name, department, "role:test"))];
    }

    /// <summary>Staff of a department without the permission being tested.</summary>
    public void StaffWithout(int vid, Department department, string permission)
    {
        Coordinator(vid, department);
        Permissions = [.. Permissions.Where(held => held.Name != permission)];
    }

    public void Superadmin(int vid)
    {
        Member(vid);
        IsStaff = true;
        IsSuperadmin = true;
        HasAllDepartments = true;
    }
}
