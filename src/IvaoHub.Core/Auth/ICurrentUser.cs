using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Who is asking. Every check in the system goes through this, so that "may this person do that?"
/// is answered in one place and never re-implemented by a module (plan section 16.2).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>IVAO VID, 0 when anonymous.</summary>
    int Vid { get; }

    string FirstName { get; }

    string LastName { get; }

    bool IsSuperadmin { get; }

    bool IsStaff { get; }

    string Locale { get; }

    /// <summary>Departments of the recognised staff positions.</summary>
    IReadOnlySet<Department> Departments { get; }

    /// <summary>The director, the web team and a super administrator reach every department.</summary>
    bool HasAllDepartments { get; }

    /// <summary>FIRs of the recognised FIR positions.</summary>
    IReadOnlySet<string> Firs { get; }

    /// <summary>The raw positions as IVAO wrote them, recognised or not.</summary>
    IReadOnlyList<string> Positions { get; }

    IReadOnlyList<EffectivePermission> Permissions { get; }

    /// <summary>
    /// True when the user holds the permission on that department. A permission held everywhere
    /// (stored with no department) counts, and a super administrator always holds it: that is the
    /// whole point of the role.
    /// </summary>
    bool Has(string permission, Department department);

    /// <summary>
    /// True when the user holds the permission somewhere: on one department, on all of them, or
    /// as a global permission. It answers "may they do this at all", which is the only thing that
    /// can be asked before a row is in hand — opening a list, for instance (design M0 section 3.7).
    /// The department is then checked row by row.
    /// </summary>
    bool HasAny(string permission);
}

/// <summary>Reads the identity out of the application cookie. No database call per request.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor, IOptions<DivisionOptions> division)
    : ICurrentUser
{
    private ClaimsPrincipal? _readFor;
    private Snapshot? _snapshot;

    /// <summary>
    /// Read from the claims of the request, and read again when the principal changes: the cookie
    /// middleware sets it in the middle of the request, and anything resolved before that (the
    /// security stamp check builds a context, for one) must not freeze an anonymous answer.
    /// </summary>
    private Snapshot Current
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (_snapshot is null || !ReferenceEquals(principal, _readFor))
            {
                _snapshot = Read(principal, division.Value);
                _readFor = principal;
            }

            return _snapshot;
        }
    }

    public bool IsAuthenticated => Current.IsAuthenticated;

    public int Vid => Current.Vid;

    public string FirstName => Current.FirstName;

    public string LastName => Current.LastName;

    public bool IsSuperadmin => Current.IsSuperadmin;

    public bool IsStaff => Current.IsStaff;

    public string Locale => Current.Locale;

    public IReadOnlySet<Department> Departments => Current.Departments;

    public bool HasAllDepartments => Current.HasAllDepartments;

    public IReadOnlySet<string> Firs => Current.Firs;

    public IReadOnlyList<string> Positions => Current.Positions;

    public IReadOnlyList<EffectivePermission> Permissions => Current.Permissions;

    public bool Has(string permission, Department department)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        // A permission with no department is held everywhere: that is how a director and the web
        // team travel with a handful of claims instead of one per department.
        return IsSuperadmin
            || Permissions.Any(held =>
                string.Equals(held.Name, permission, StringComparison.Ordinal)
                && (held.Department is null || held.Department == department));
    }

    public bool HasAny(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return IsSuperadmin
            || Permissions.Any(held => string.Equals(held.Name, permission, StringComparison.Ordinal));
    }

    private static Snapshot Read(ClaimsPrincipal? principal, DivisionOptions division)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Snapshot.Anonymous(division.DefaultLocale);
        }

        var permissions = principal.FindAll(HubClaims.Permission)
            .Select(claim => HubClaims.ParsePermission(claim.Value))
            .Select(parsed => new EffectivePermission(parsed.Name, parsed.Department, "cookie"))
            .ToArray();

        var departments = principal.FindAll(HubClaims.Department)
            .Select(claim => Enum.TryParse<Department>(claim.Value, out var value) ? value : (Department?)null)
            .OfType<Department>()
            .ToHashSet();

        var isSuperadmin = principal.HasClaim(HubClaims.Superadmin, "1");

        // Held everywhere is stored as a permission with no department; that is also what tells us
        // the user reaches every department without listing them one by one.
        var hasAllDepartments = isSuperadmin
            || permissions.Any(permission => permission.Department is null && !CorePermissions.IsGlobalPermission(permission.Name));

        return new Snapshot(
            IsAuthenticated: true,
            Vid: int.TryParse(principal.FindFirstValue(HubClaims.Vid), CultureInfo.InvariantCulture, out var vid) ? vid : 0,
            FirstName: principal.FindFirstValue(HubClaims.FirstName) ?? string.Empty,
            LastName: principal.FindFirstValue(HubClaims.LastName) ?? string.Empty,
            IsSuperadmin: isSuperadmin,
            IsStaff: principal.HasClaim(HubClaims.Staff, "1") || isSuperadmin,
            Locale: principal.FindFirstValue(HubClaims.Locale) ?? division.DefaultLocale,
            Departments: departments,
            HasAllDepartments: hasAllDepartments,
            Firs: principal.FindAll(HubClaims.Fir).Select(claim => claim.Value).ToHashSet(StringComparer.OrdinalIgnoreCase),
            Positions: [.. principal.FindAll(HubClaims.Position).Select(claim => claim.Value)],
            Permissions: permissions);
    }

    private sealed record Snapshot(
        bool IsAuthenticated,
        int Vid,
        string FirstName,
        string LastName,
        bool IsSuperadmin,
        bool IsStaff,
        string Locale,
        IReadOnlySet<Department> Departments,
        bool HasAllDepartments,
        IReadOnlySet<string> Firs,
        IReadOnlyList<string> Positions,
        IReadOnlyList<EffectivePermission> Permissions)
    {
        public static Snapshot Anonymous(string locale) => new(
            IsAuthenticated: false,
            Vid: 0,
            FirstName: string.Empty,
            LastName: string.Empty,
            IsSuperadmin: false,
            IsStaff: false,
            Locale: locale,
            Departments: new HashSet<Department>(),
            HasAllDepartments: false,
            Firs: new HashSet<string>(),
            Positions: [],
            Permissions: []);
    }
}
