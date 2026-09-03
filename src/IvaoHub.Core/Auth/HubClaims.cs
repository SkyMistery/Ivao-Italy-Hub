using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The claims of the application cookie. Short names on purpose: the cookie travels with every
/// request, and the IVAO profile is far too large to carry around (measured on the real payload:
/// about 1.5 kB of staff positions alone for two assignments).
/// </summary>
public static class HubClaims
{
    /// <summary>Name of the application cookie scheme. Not the IVAO challenge scheme.</summary>
    public const string CookieScheme = "Hub";

    /// <summary>Name of the OpenID Connect challenge scheme towards IVAO.</summary>
    public const string IvaoScheme = "IVAO";

    public const string Vid = "vid";
    public const string Superadmin = "sa";
    public const string Staff = "staff";

    /// <summary>
    /// The director, the web team and a super administrator reach every department. It is a fact of
    /// the role, so it is stated here rather than guessed from the shape of the permission list: a
    /// deny that expands one permission into explicit departments must not be able to change it
    /// (design M0 section 3.3).
    /// </summary>
    public const string AllDepartments = "alldept";

    public const string Department = "dept";
    public const string Fir = "fir";
    public const string Permission = "perm";
    public const string Position = "pos";
    public const string Locale = "locale";
    public const string SecurityStamp = "stamp";
    public const string FirstName = "given_name";
    public const string LastName = "family_name";

    /// <summary>Separates a permission from the department it is scoped to: <c>Links.Edit:EV</c>.</summary>
    private const char DepartmentSeparator = ':';

    /// <summary>Writes a permission as a single claim value; no department means every department.</summary>
    public static string FormatPermission(EffectivePermission permission) =>
        permission.Department is { } department
            ? $"{permission.Name}{DepartmentSeparator}{department}"
            : permission.Name;

    /// <summary>Reads back a permission claim value.</summary>
    public static (string Name, Department? Department) ParsePermission(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var separator = value.IndexOf(DepartmentSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            return (value, null);
        }

        var name = value[..separator];
        return Enum.TryParse<Department>(value[(separator + 1)..], out var department)
            ? (name, department)
            : (name, null);
    }

    /// <summary>
    /// The same identity with a different language. It is here, next to <see cref="BuildIdentity"/>,
    /// because the shape of a hub cookie is decided in one file and nowhere else: a member who
    /// switches language must not have to sign in again before the server answers them in it.
    /// </summary>
    public static ClaimsIdentity WithLocale(ClaimsIdentity identity, string locale)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        var replaced = new ClaimsIdentity(
            identity.Claims.Where(claim => claim.Type != Locale),
            CookieScheme,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);

        replaced.AddClaim(new Claim(Locale, locale));
        return replaced;
    }

    /// <summary>
    /// Builds the application identity. This is the only place a hub cookie is composed, so a test
    /// authentication handler produces exactly the same principal as a real IVAO login.
    /// </summary>
    public static ClaimsIdentity BuildIdentity(
        int vid,
        string firstName,
        string lastName,
        string locale,
        string securityStamp,
        bool isSuperadmin,
        bool isStaff,
        IEnumerable<StaffPosition> positions,
        IEnumerable<EffectivePermission> permissions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(permissions);

        var identity = new ClaimsIdentity(CookieScheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, vid.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(Vid, vid.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(FirstName, firstName));
        identity.AddClaim(new Claim(LastName, lastName));
        identity.AddClaim(new Claim(Locale, locale));
        identity.AddClaim(new Claim(SecurityStamp, securityStamp));

        if (isSuperadmin)
        {
            identity.AddClaim(new Claim(Superadmin, "1"));
        }

        if (isStaff)
        {
            identity.AddClaim(new Claim(Staff, "1"));
        }

        var materialised = positions.ToArray();

        // Computed here, from the positions, so that the real login and the fake login of the tests
        // cannot disagree about it: this method is the only place a hub cookie is composed.
        if (isSuperadmin || materialised.Any(RolePermissionMatrix.ReachesEveryDepartment))
        {
            identity.AddClaim(new Claim(AllDepartments, "1"));
        }

        foreach (var department in materialised
            .Select(position => position.Department)
            .OfType<Division.Department>()
            .Distinct())
        {
            identity.AddClaim(new Claim(Department, department.ToString()));
        }

        foreach (var fir in materialised.Select(position => position.Fir).OfType<string>().Distinct())
        {
            identity.AddClaim(new Claim(Fir, fir));
        }

        foreach (var raw in materialised.Select(position => position.Raw).Distinct())
        {
            identity.AddClaim(new Claim(Position, raw));
        }

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(Permission, FormatPermission(permission)));
        }

        return identity;
    }
}
