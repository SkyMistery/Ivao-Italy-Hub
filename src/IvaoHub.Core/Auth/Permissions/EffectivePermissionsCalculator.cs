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
public readonly record struct EffectivePermission(string Name, Department? Department, string Source);

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
    /// </summary>
    public static bool Has(
        IEnumerable<EffectivePermission> permissions,
        bool isSuperadmin,
        string permission,
        Department department)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return isSuperadmin
            || permissions.Any(held =>
                string.Equals(held.Name, permission, StringComparison.Ordinal)
                && (held.Department is null || held.Department == department));
    }

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

    public static IReadOnlyList<EffectivePermission> Calculate(
        IEnumerable<StaffPosition> positions,
        IEnumerable<UserGrant> grants,
        bool isSuperadmin,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(grants);

        // The super administrator bypasses every policy. The full list is still produced, because
        // the SPA hides menus by permission and an empty list would hide everything.
        if (isSuperadmin)
        {
            return [.. CorePermissions.All.Select(p => new EffectivePermission(p.Name, null, SuperadminSource))];
        }

        var effective = new HashSet<EffectivePermission>();

        foreach (var position in positions)
        {
            AddDerived(effective, position);
        }

        var active = grants
            .Where(grant => grant.Kind == GrantKind.Permission)
            .Where(grant => grant.SuspendedAt is null)
            .Where(grant => grant.ExpiresAt is null || grant.ExpiresAt > nowUtc)
            .Where(grant => CorePermissions.IsKnown(grant.Value))
            // A grant may never confer a global permission, nor the right to hand out permissions:
            // the perimeter of the staff is always decided by IVAO (plan section 6.3).
            .Where(grant => !CorePermissions.IsGlobalPermission(grant.Value))
            .ToArray();

        foreach (var grant in active.Where(grant => grant.Effect == GrantEffect.Grant))
        {
            effective.Add(new EffectivePermission(
                grant.Value,
                grant.Department,
                $"{GrantSourcePrefix}{grant.Id}"));
        }

        // Edit implies View, in one place, before the denies so that an explicit deny still wins.
        foreach (var permission in effective.ToArray())
        {
            if (CorePermissions.ViewOf(permission.Name) is { } view && view != permission.Name)
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
            .DistinctBy(permission => (permission.Name, permission.Department))];
    }

    /// <summary>Which source is worth keeping when the same permission is reached more than once.</summary>
    private static int Rank(string source) => source switch
    {
        SuperadminSource => 0,
        _ when source.StartsWith(RoleSourcePrefix, StringComparison.Ordinal) => 1,
        _ => 2,
    };

    private static void AddDerived(HashSet<EffectivePermission> effective, StaffPosition position)
    {
        if (RolePermissionMatrix.ReachesEveryDepartment(position))
        {
            var source = $"{RoleSourcePrefix}{position.Role}";
            foreach (var name in CorePermissions.Departmental)
            {
                effective.Add(new EffectivePermission(name, null, source));
            }

            foreach (var name in CorePermissions.Global)
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
