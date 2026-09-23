using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Grants to a person that a module writes from a screen of its own, which knows the rows a grant may be scoped to: «add a
/// validator» on a tour (M2, T15; decision note of 15 September 2026, §7.3 of the design of M2). The permissions screen never
/// writes a <c>resource_scope</c>; this is the other way in, with the same rules — a permission the catalogue knows, never a
/// global one, only to somebody this division counts as staff — and the same row, so audit, suspension when the person
/// leaves the staff and the refresh of their session all come with it.
/// <para>Who may call it is the module's endpoint's to decide (<c>Tours.ManageValidators</c>): a grant is not a row of a
/// department, and the interceptor has no owner to hold it against.</para>
/// </summary>
public sealed class ModuleGrants(HubDbContext database, PermissionCatalog catalogue)
{
    /// <summary>The grants to people of this permission in this department, with or without a scope, suspended ones included.</summary>
    public Task<List<UserGrant>> HeldAsync(string permission, Department department, CancellationToken cancellationToken = default) =>
        database.UserGrants.AsNoTracking()
            .Where(grant => grant.Vid != null
                && grant.Value == permission
                && grant.Department == department
                && grant.Effect == GrantEffect.Grant)
            .OrderBy(grant => grant.Vid)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Gives the permission to the person, on the scope or on every row of the department. Nothing happens when they have that
    /// very grant already. Returns the i18n key of the refusal, or null.
    /// </summary>
    public async Task<string?> GiveAsync(
        int vid,
        string permission,
        Department department,
        string? resourceScope,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!catalogue.IsKnown(permission))
        {
            return "errors.grant.unknownPermission";
        }

        if (catalogue.IsGlobal(permission))
        {
            return "errors.grant.globalPermission";
        }

        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Vid == vid && (user.IsStaff || user.IsSuperadmin), cancellationToken))
        {
            return "errors.grant.notStaff";
        }

        if (await FindAsync(vid, permission, department, resourceScope, cancellationToken) is not null)
        {
            return null;
        }

        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = department,
            ResourceScope = resourceScope,
            Effect = GrantEffect.Grant,
            Reason = reason,
        });
        await database.SaveChangesAsync(cancellationToken);
        return null;
    }

    /// <summary>Takes that very grant away: the same person, permission, department and scope. False when there was none.</summary>
    public async Task<bool> TakeAsync(
        int vid,
        string permission,
        Department department,
        string? resourceScope,
        CancellationToken cancellationToken = default)
    {
        var grant = await FindAsync(vid, permission, department, resourceScope, cancellationToken);
        if (grant is null)
        {
            return false;
        }

        database.UserGrants.Remove(grant);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<UserGrant?> FindAsync(int vid, string permission, Department department, string? resourceScope, CancellationToken cancellationToken) =>
        database.UserGrants.FirstOrDefaultAsync(
            grant => grant.Vid == vid
                && grant.Value == permission
                && grant.Department == department
                && grant.ResourceScope == resourceScope
                && grant.Effect == GrantEffect.Grant,
            cancellationToken);
}
