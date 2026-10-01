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

    /// <summary>
    /// Name of the scheme of personal tokens (M2, T19a): <c>Authorization: Bearer hubpat_…</c>, accepted only by the
    /// endpoints of the token's audience. Never a default scheme.
    /// </summary>
    public const string TokenScheme = "HubToken";

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

    /// <summary>The audience of the personal token a request came with. Only on the identity the token scheme builds.</summary>
    public const string Audience = "aud";

    /// <summary>The identifier of that token, for the audit of what the program wrote.</summary>
    public const string PersonalToken = "tok";

    /// <summary>Separates a permission from the department it is scoped to: <c>Links.Edit:EV</c>.</summary>
    private const char DepartmentSeparator = ':';

    /// <summary>
    /// Separates a permission from the single row it is held on: <c>Tours.Validate:FO@flightops:tour:42</c>.
    /// <para>After the department on purpose, so that a cookie written before scopes existed still
    /// reads exactly as it did: no separator, no scope.</para>
    /// </summary>
    private const char ScopeSeparator = '@';

    /// <summary>
    /// Separates, inside the scope's part, the FIR a permission is held on (M3, A11a): <c>Training.Assign:TD@#LIRR</c>.
    /// <para>Inside the scope's part on purpose (note 2026-09-27-i-capi-fir-sul-loro-fir §3.2, the reviewer's point 1): a
    /// reader that does not know it — a package from before a rollback, or <see cref="ParsePermission"/> — reads a scope no
    /// row declares, and the permission reaches no row. Closed, never "every department". A scope never holds it.</para>
    /// </summary>
    private const char FirSeparator = '#';

    /// <summary>Writes a permission as a single claim value; no department means every department.</summary>
    public static string FormatPermission(EffectivePermission permission)
    {
        var value = permission.Department is { } department
            ? $"{permission.Name}{DepartmentSeparator}{department}"
            : permission.Name;

        var where = permission.Fir is { Length: > 0 } fir
            ? $"{permission.ResourceScope}{FirSeparator}{fir}"
            : permission.ResourceScope;

        return where is { Length: > 0 }
            ? $"{value}{ScopeSeparator}{where}"
            : value;
    }

    /// <summary>
    /// Reads back a permission claim value the way every reader before M3 read it: the scope is whatever follows <c>@</c>, a
    /// FIR included, which no row declares — so a permission held on a FIR reaches no row through this reading.
    /// <para>A department it cannot read is refused (<see cref="FormatException"/>) rather than read as "every department",
    /// which is what a missing department means (M3, A11a). The cookie reader asks <see cref="ReadPermission"/>.</para>
    /// </summary>
    public static (string Name, Department? Department, string? ResourceScope) ParsePermission(string value)
    {
        var (name, department, scope, readable) = Split(value);

        return readable
            ? (name, department, scope)
            : throw new FormatException($"The permission claim '{value}' names a department this hub does not know.");
    }

    /// <summary>
    /// Reads a permission claim value into what the cookie holds: the scope and the FIR apart (M3, A11a). Null for a claim
    /// that names a department this hub does not know, or a FIR with no name: such a claim is worth nothing, where a missing
    /// department would have been worth every department (note 2026-09-27-i-capi-fir-sul-loro-fir §3.2).
    /// </summary>
    public static EffectivePermission? ReadPermission(string value, string source)
    {
        var (name, department, where, readable) = Split(value);
        if (!readable)
        {
            return null;
        }

        var scope = where;
        string? fir = null;
        if (where is not null && where.IndexOf(FirSeparator, StringComparison.Ordinal) is var hash and >= 0)
        {
            fir = where[(hash + 1)..];
            scope = where[..hash];
            if (fir.Length == 0)
            {
                return null;
            }
        }

        return new EffectivePermission(name, department, source, scope is { Length: > 0 } ? scope : null, fir);
    }

    /// <summary>The name, the department and what follows <c>@</c>; not readable when the department is not one of ours.</summary>
    private static (string Name, Department? Department, string? Where, bool Readable) Split(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string? where = null;
        var at = value.IndexOf(ScopeSeparator, StringComparison.Ordinal);
        if (at >= 0)
        {
            where = value[(at + 1)..];
            value = value[..at];
        }

        var separator = value.IndexOf(DepartmentSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            return (value, null, where, true);
        }

        var name = value[..separator];
        return Enum.TryParse<Department>(value[(separator + 1)..], out var department)
            ? (name, department, where, true)
            : (name, null, where, false);
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

        var materialisedPermissions = permissions.ToArray();

        // The departments this person belongs to, for the purpose of what they may *see*: the ones
        // their staff positions name, and the ones a grant reached them on.
        //
        // ⚠️ The second half is not decoration. A grant used to give the permission and nothing
        // else: the global query filter and the department filter of every list both read these
        // claims, so somebody granted Content.Edit on the AOD could open a row whose identifier
        // they already knew and got an empty list and no Department row of that department at all
        // (note 2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento). The test of F8 asked
        // for the detail and never for the list, which is why nobody had noticed.
        //
        // It is read off the source of the effective permission rather than off "any permission
        // with a department", because expanding a deny turns one permission held everywhere into
        // one explicit entry per surviving department — and those name departments nobody was ever
        // authorised on.
        //
        // ⚠️ What it widens, said out loud: a claim here is not a permission, it is "this person is
        // inside that department for the purpose of seeing". Whoever holds any grant on a
        // department therefore starts seeing every Visibility.Department row of it, including areas
        // they were given nothing on. That is the right reading of "authorised to reach that
        // department" and it is what makes a department dashboard visible to the people helping
        // out — but whoever hands a grant out has to know it.
        //
        // ⚠️ And a grant with no department is a grant on **every** department, so it reaches all of
        // them (note 2026-09-13-contenuti-centralizzati, section 3.1: "the permission to see and handle
        // them all"). It used to be dropped here, which gave the permission everywhere and a list
        // of nobody's rows -- the same bug as above, one level up.
        //
        // ⚠️ Except a permission held on one FIR (M3, A11a): the team of a FIR is not part of the department, and sees the rows
        // of its FIR through the permission itself — in the single handler, and in the lists that read with it
        // (note 2026-09-27-i-capi-fir-sul-loro-fir §3.5).
        //
        // ⚠️ And except a permission that says it came from outside (M4, E10f, note 2026-10-01-chi-assegna-gli-award-con-un-grant):
        // a grant of a global permission, which has no department to take anybody into, with the View it brings. It names no
        // department, and read as the grants above it would have taken whoever assigns the awards into all of them.
        var granted = materialisedPermissions
            .Where(permission => permission.Source.StartsWith(
                EffectivePermissionsCalculator.GrantSourcePrefix,
                StringComparison.Ordinal))
            .Where(permission => permission.Fir is null && !permission.FromOutside)
            .ToArray();

        var reached = materialised
            .Select(position => position.Department)
            .OfType<Division.Department>()
            .Concat(granted.Select(permission => permission.Department).OfType<Division.Department>())
            .Concat(granted.Any(permission => permission.Department is null)
                ? RolePermissionMatrix.AllDepartments
                : [])
            .Distinct();

        foreach (var department in reached)
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

        foreach (var permission in materialisedPermissions)
        {
            identity.AddClaim(new Claim(Permission, FormatPermission(permission)));
        }

        return identity;
    }
}
