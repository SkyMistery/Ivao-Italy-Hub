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
    /// True when the user holds the permission, optionally on that department. A super
    /// administrator always holds it: that is the whole point of the role.
    /// </summary>
    bool Has(string permission, Department? department = null);
}

/// <summary>Reads the identity out of the application cookie. No database call per request.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor, IOptions<DivisionOptions> division)
    : ICurrentUser
{
    private readonly Lazy<Snapshot> _snapshot = new(() => Read(accessor, division.Value));

    public bool IsAuthenticated => _snapshot.Value.IsAuthenticated;

    public int Vid => _snapshot.Value.Vid;

    public string FirstName => _snapshot.Value.FirstName;

    public string LastName => _snapshot.Value.LastName;

    public bool IsSuperadmin => _snapshot.Value.IsSuperadmin;

    public bool IsStaff => _snapshot.Value.IsStaff;

    public string Locale => _snapshot.Value.Locale;

    public IReadOnlySet<Department> Departments => _snapshot.Value.Departments;

    public bool HasAllDepartments => _snapshot.Value.HasAllDepartments;

    public IReadOnlySet<string> Firs => _snapshot.Value.Firs;

    public IReadOnlyList<string> Positions => _snapshot.Value.Positions;

    public IReadOnlyList<EffectivePermission> Permissions => _snapshot.Value.Permissions;

    public bool Has(string permission, Department? department = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        if (IsSuperadmin)
        {
            return true;
        }

        foreach (var held in Permissions)
        {
            if (!string.Equals(held.Name, permission, StringComparison.Ordinal))
            {
                continue;
            }

            // A permission with no department is held everywhere.
            if (held.Department is null || held.Department == department)
            {
                return true;
            }
        }

        return false;
    }

    private static Snapshot Read(IHttpContextAccessor accessor, DivisionOptions division)
    {
        var principal = accessor.HttpContext?.User;
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
