using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// A permission the user actually holds. <see cref="Department"/> null means every department.
/// <see cref="Source"/> says where it comes from, so that the permissions screen can show what is
/// a role and what was granted by hand.
/// <para>Two entries that differ only by <see cref="Source"/> are the same permission: the
/// calculator keeps one of them, because the cookie carries one claim per entry and a permission
/// held both by a role and by a grant would otherwise travel twice.</para>
/// </summary>
/// <param name="Name">The permission, always <c>Area.Action</c>.</param>
/// <param name="Department">Which department it is held on; null means every one of them.</param>
/// <param name="Source">Where it comes from: the role, a grant, or being a super administrator.</param>
/// <param name="ResourceScope">
/// When set, the permission is held on <b>that row only</b> — <c>flightops:tour:42</c> — and on
/// nothing else. It is how "this validator, on this tour" is said without a second mechanism
/// (decision note of 15 September 2026); null is the ordinary case, held across the department.
/// </param>
/// <param name="Fir">
/// When set, the permission is held on the rows of <b>that FIR</b> only (<see cref="IHasFir"/>): what a
/// grant to the team of a FIR gives when the division keeps FIR teams to their FIR (M3, A11a, note
/// 2026-09-27-i-capi-fir-sul-loro-fir). Null is the ordinary case, held on every row whatever its FIR.
/// </param>
public readonly record struct EffectivePermission(
    string Name,
    Department? Department,
    string Source,
    string? ResourceScope = null,
    string? Fir = null);

/// <summary>
/// Answering "does this person hold that permission?" against a set of effective permissions.
/// It lives here, once, because the cookie reader of the host and the test doubles of the suite
/// must not each carry their own copy of the rule: a copy is a place where the real answer and
/// the tested answer can quietly drift apart.
/// </summary>
public static class PermissionSet
{
    /// <summary>
    /// True when the set holds the permission on that department. A permission with no department
    /// is held everywhere, and a super administrator holds everything: that is the whole point of
    /// the role (design M0 section 3.3).
    /// <para><paramref name="resourceScope"/> and <paramref name="fir"/> are what the row being asked about says of itself
    /// (<c>IHasResourceScope</c>, <c>IHasFir</c>): together with the department, where the row is.</para>
    /// </summary>
    public static bool Has(
        IEnumerable<EffectivePermission> permissions,
        bool isSuperadmin,
        string permission,
        Department department,
        string? resourceScope = null,
        string? fir = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return isSuperadmin
            || permissions.Any(held =>
                string.Equals(held.Name, permission, StringComparison.Ordinal)
                && (held.Department is null || held.Department == department)
                && Reaches(held, resourceScope)
                && ReachesFir(held, fir));
    }

    /// <summary>
    /// Whether a held permission reaches the row being asked about. One held without a scope reaches
    /// everything, as it always has; one held with a scope reaches <b>only</b> the row that declares
    /// the same scope — so a validator enabled on one tour is not thereby enabled on the next.
    /// </summary>
    private static bool Reaches(EffectivePermission held, string? resourceScope) =>
        held.ResourceScope is null
        || string.Equals(held.ResourceScope, resourceScope, StringComparison.Ordinal);

    /// <summary>
    /// Whether a held permission reaches a row of that FIR (M3, A11a). One held without a FIR reaches every row, as it always
    /// has; one held on a FIR reaches <b>only</b> a row that says the same FIR — never a row of another FIR, a row with no
    /// FIR, or a row that does not say one. FIRs are compared as IVAO writes them, without regard to case.
    /// </summary>
    private static bool ReachesFir(EffectivePermission held, string? fir) =>
        held.Fir is null
        || (fir is not null && string.Equals(held.Fir, fir, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when the set holds the permission somewhere: on one department, on all of them, or as
    /// a global permission. It answers "may they do this at all", which is the only thing that can
    /// be asked before a row is in hand (design M0 section 3.7).
    /// </summary>
    public static bool HasAny(IEnumerable<EffectivePermission> permissions, bool isSuperadmin, string permission)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return isSuperadmin
            || permissions.Any(held => string.Equals(held.Name, permission, StringComparison.Ordinal));
    }
}

/// <summary>
/// Effective permissions = derived from the staff positions, union the grants, minus the denies
/// (plan section 6.3). Computed at login and whenever a grant changes; the result travels in the
/// authentication cookie, which is why a permission held everywhere is stored once with no
/// department instead of once per department.
/// </summary>
public static class EffectivePermissionsCalculator
{
    /// <summary>Source of a permission a super administrator holds by being one.</summary>
    public const string SuperadminSource = "superadmin";

    /// <summary>Source prefix of a permission derived from a staff position.</summary>
    public const string RoleSourcePrefix = "role:";

    /// <summary>Source prefix of a permission handed out to a single member by name.</summary>
    public const string GrantSourcePrefix = "grant:";

    /// <remarks>
    /// The catalogue is core plus modules: a grant naming a module permission is honoured on an
    /// installation that has that module and ignored on one that does not, which is the same rule
    /// that has always applied to a permission the code no longer declares.
    /// <para><paramref name="firStaffScope"/> is the division's: with <c>own</c>, what a grant to the team of a FIR gives is
    /// held on the FIR of the position it comes through (M3, A11a); with <c>all</c> — the default, and a division that does
    /// not say — on the grant's department, as any other grant.</para>
    /// </remarks>
    public static IReadOnlyList<EffectivePermission> Calculate(
        IEnumerable<StaffPosition> positions,
        IEnumerable<UserGrant> grants,
        bool isSuperadmin,
        DateTime nowUtc,
        PermissionCatalog catalogue,
        FirStaffScope firStaffScope = FirStaffScope.All)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(catalogue);

        // The super administrator bypasses every policy. The full list is still produced, because
        // the SPA hides menus by permission and an empty list would hide everything.
        if (isSuperadmin)
        {
            return [.. catalogue.All.Select(p => new EffectivePermission(p.Name, null, SuperadminSource))];
        }

        var effective = new HashSet<EffectivePermission>();

        foreach (var position in positions)
        {
            AddDerived(effective, position, catalogue);
        }


        var held = positions as IReadOnlyCollection<StaffPosition> ?? [.. positions];

        var active = grants
            .Where(grant => grant.Kind == GrantKind.Permission)
            // A grant to a position counts for somebody who holds it; a grant to a person has
            // already been chosen by its VID (M2, note 2026-09-13-moduli-non-subordinati-ai-dipartimenti).
            .Where(grant => grant.Vid is not null || grant.IsHeldThrough(held))
            .Where(grant => grant.SuspendedAt is null)
            .Where(grant => grant.ExpiresAt is null || grant.ExpiresAt > nowUtc)
            .Where(grant => catalogue.IsKnown(grant.Value))
            // A grant may never confer a global permission, nor the right to hand out permissions:
            // the perimeter of the staff is always decided by IVAO (plan section 6.3).
            .Where(grant => !catalogue.IsGlobal(grant.Value))
            .ToArray();

        foreach (var grant in active.Where(grant => grant.Effect == GrantEffect.Grant))
        {
            // A grant to the team of a FIR, when the division keeps FIR teams to their FIR (M3, A11a): one permission for each
            // FIR the member heads, held on that FIR's rows alone. Anything else is held across its department.
            IEnumerable<string?> firs = grant.PositionFirTeam && firStaffScope == FirStaffScope.Own
                ? grant.HeldThrough(held).Select(position => position.Fir).Distinct(StringComparer.OrdinalIgnoreCase)
                : [null];

            foreach (var fir in firs)
            {
                effective.Add(new EffectivePermission(
                    grant.Value,
                    grant.Department,
                    $"{GrantSourcePrefix}{grant.Id}",
                    grant.ResourceScope,
                    fir));
            }
        }

        // Edit implies View, in one place, before the denies so that an explicit deny still wins.
        foreach (var permission in effective.ToArray())
        {
            if (catalogue.ViewOf(permission.Name) is { } view && view != permission.Name)
            {
                effective.Add(permission with { Name = view });
            }
        }

        foreach (var deny in active.Where(grant => grant.Effect == GrantEffect.Deny))
        {
            Deny(effective, deny.Value, deny.Department);
        }

        // One entry per (name, department). The same permission can be reached twice, by a role and
        // by a grant, and the cookie would then carry the identical claim twice. The role wins,
        // because "they hold it anyway" is the more useful thing to show an administrator who is
        // about to delete the grant.
        return [.. effective
            .OrderBy(permission => permission.Name, StringComparer.Ordinal)
            .ThenBy(permission => permission.Department)
            .ThenBy(permission => Rank(permission.Source))
            .ThenBy(permission => permission.Source, StringComparer.Ordinal)
            .ThenBy(permission => permission.ResourceScope, StringComparer.Ordinal)
            .ThenBy(permission => permission.Fir, StringComparer.Ordinal)
            .DistinctBy(permission => (permission.Name, permission.Department, permission.ResourceScope, permission.Fir))];
    }

    /// <summary>Which source is worth keeping when the same permission is reached more than once.</summary>
    private static int Rank(string source) => source switch
    {
        SuperadminSource => 0,
        _ when source.StartsWith(RoleSourcePrefix, StringComparison.Ordinal) => 1,
        _ => 2,
    };

    private static void AddDerived(
        HashSet<EffectivePermission> effective,
        StaffPosition position,
        PermissionCatalog catalogue)
    {
        if (RolePermissionMatrix.ReachesEveryDepartment(position))
        {
            var source = $"{RoleSourcePrefix}{position.Role}";
            foreach (var name in catalogue.Departmental)
            {
                effective.Add(new EffectivePermission(name, null, source));
            }

            foreach (var name in catalogue.Global)
            {
                effective.Add(new EffectivePermission(name, null, source));
            }

            return;
        }

        if (RolePermissionMatrix.ReadsEveryDepartment(position))
        {
            effective.Add(new EffectivePermission(
                CorePermissions.ContentView,
                null,
                $"{RoleSourcePrefix}{position.Role}"));
            return;
        }

        // A FIR position owns no department, so it carries no permission of the core in M0; it
        // still makes the user staff and fills ICurrentUser.Firs.
        if (position.Department is not { } department)
        {
            return;
        }

        foreach (var name in RolePermissionMatrix.OnOwnDepartment(position.Level))
        {
            effective.Add(new EffectivePermission(
                name,
                department,
                $"{RoleSourcePrefix}{department}/{position.Level}"));
        }
    }

    /// <summary>
    /// Takes a permission away. A deny on one department has to bite even when the permission is
    /// held everywhere, so the "everywhere" entry is expanded into the departments that survive.
    /// <para>The expansion is a matter of that one permission and nothing else. Whether the user
    /// reaches every department is a fact of their role, carried by its own claim: it is not read
    /// back out of the shape of this list, which is what used to make a single deny quietly close
    /// seven departments to a director.</para>
    /// </summary>
    private static void Deny(HashSet<EffectivePermission> effective, string name, Department? department)
    {
        if (department is null)
        {
            effective.RemoveWhere(permission => permission.Name == name);
            return;
        }

        foreach (var permission in effective.Where(permission => permission.Name == name).ToArray())
        {
            if (permission.Department == department)
            {
                effective.Remove(permission);
                continue;
            }

            if (permission.Department is null)
            {
                effective.Remove(permission);
                foreach (var kept in RolePermissionMatrix.AllDepartments.Where(value => value != department))
                {
                    effective.Add(permission with { Department = kept });
                }
            }
        }
    }
}
