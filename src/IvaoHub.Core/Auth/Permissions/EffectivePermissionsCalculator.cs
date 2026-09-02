using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// A permission the user actually holds. <see cref="Department"/> null means every department.
/// <see cref="Source"/> says where it comes from, so that the permissions screen can show what is
/// a role and what was granted by hand.
/// </summary>
public readonly record struct EffectivePermission(string Name, Department? Department, string Source);

/// <summary>
/// Effective permissions = derived from the staff positions, union the grants, minus the denies
/// (plan section 6.3). Computed at login and whenever a grant changes; the result travels in the
/// authentication cookie, which is why a permission held everywhere is stored once with no
/// department instead of once per department.
/// </summary>
public static class EffectivePermissionsCalculator
{
    private const string SuperadminSource = "superadmin";

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
            effective.Add(new EffectivePermission(grant.Value, grant.Department, $"grant:{grant.Id}"));
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

        return [.. effective
            .OrderBy(permission => permission.Name, StringComparer.Ordinal)
            .ThenBy(permission => permission.Department)];
    }

    private static void AddDerived(HashSet<EffectivePermission> effective, StaffPosition position)
    {
        if (RolePermissionMatrix.ReachesEveryDepartment(position))
        {
            var source = $"role:{position.Role}";
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
            effective.Add(new EffectivePermission(CorePermissions.ContentView, null, $"role:{position.Role}"));
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
            effective.Add(new EffectivePermission(name, department, $"role:{department}/{position.Level}"));
        }
    }

    /// <summary>
    /// Takes a permission away. A deny on one department has to bite even when the permission is
    /// held everywhere, so the "everywhere" entry is expanded into the departments that survive.
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
