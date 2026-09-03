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

    // Not "the same rules as" the real implementation: literally the same code. A test double that
    // reimplements the rule it is meant to exercise can keep passing while production drifts away
    // from it, which is the one failure a test suite must not have.
    public bool Has(string permission, Department department) =>
        PermissionSet.Has(Permissions, IsSuperadmin, permission, department);

    public bool HasAny(string permission) =>
        PermissionSet.HasAny(Permissions, IsSuperadmin, permission);

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

    /// <summary>
    /// The director of the division: reaches every department, and holds every departmental
    /// permission with no department attached, exactly as the calculator produces them.
    /// </summary>
    public void Director(int vid)
    {
        Member(vid);
        IsStaff = true;
        HasAllDepartments = true;
        Departments = new HashSet<Department> { Department.HQ };
        Permissions =
        [
            .. CorePermissions.Departmental.Select(name =>
                new EffectivePermission(name, null, "role:Director")),
            .. CorePermissions.Global.Select(name =>
                new EffectivePermission(name, null, "role:Director")),
        ];
    }

    /// <summary>
    /// A position of IVAO headquarters: staff, reads the content of the division, owns no
    /// department and reaches none. It is the identity that used to come out reaching everything.
    /// </summary>
    public void HeadquartersStaff(int vid)
    {
        Member(vid);
        IsStaff = true;
        Permissions = [new EffectivePermission(CorePermissions.ContentView, null, "role:HqStaff")];
    }
}
