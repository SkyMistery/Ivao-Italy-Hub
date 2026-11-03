using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
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
public sealed class ModuleGrants(HubDbContext database, PermissionCatalog catalogue, IClock clock)
{
    /// <summary>The lock a grant is looked for and written under, one per database.</summary>
    public const string LockPrefix = "hub-module-grants:";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

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

        // Every global permission, a grantable one too (M4, E10f): a grant written here is on a department and may be on one row,
        // which a global permission has not got. Only ever asked "at all?", Awards.Assign given on one tour would hold everywhere.
        if (catalogue.IsGlobal(permission))
        {
            return "errors.grant.globalPermission";
        }

        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Vid == vid && (user.IsStaff || user.IsSuperadmin), cancellationToken))
        {
            return "errors.grant.notStaff";
        }

        // Looked for and written under a lock of the database (issue #319, item 8): two «add a validator» at once each found
        // none and wrote one. Not a unique index: MariaDB counts two NULL scopes as different, so the grant on every row of the
        // department would stay unguarded, and the permissions screen may hold the same grant twice on purpose (another
        // expiry). Still held by another write after the wait, a refusal to ask again; one the database cannot be asked for
        // goes ahead without it, as the jobs do.
        await using var held = await DatabaseLock.TakeAsync(
            database.Database.GetConnectionString(),
            name => DatabaseLock.NameFor(LockPrefix, name),
            Wait,
            cancellationToken);
        if (held.Busy)
        {
            return "errors.grant.busy";
        }

        // Held already only by a copy of the module's own that still counts: an expired or suspended one would leave the
        // person without the permission and the screen saying they have it.
        var now = clock.UtcNow;
        if (await Matching(vid, permission, department, resourceScope, reason)
            .AnyAsync(grant => grant.SuspendedAt == null && (grant.ExpiresAt == null || grant.ExpiresAt > now), cancellationToken))
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

    /// <summary>
    /// Takes that very grant away: the same person, permission, department and scope, written by the module with that reason —
    /// every copy of it, so that one written twice before the lock (issue #319, item 8) does not outlive its removal. The same
    /// grant written by hand on the permissions screen is the staff's, and stays. False when there was none.
    /// </summary>
    public async Task<bool> TakeAsync(
        int vid,
        string permission,
        Department department,
        string? resourceScope,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var grants = await Matching(vid, permission, department, resourceScope, reason).ToListAsync(cancellationToken);
        if (grants.Count == 0)
        {
            return false;
        }

        database.UserGrants.RemoveRange(grants);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>The module's own copies of a grant: the reason it writes them with is what tells them from the staff's.</summary>
    private IQueryable<UserGrant> Matching(int vid, string permission, Department department, string? resourceScope, string? reason) =>
        database.UserGrants.Where(
            grant => grant.Vid == vid
                && grant.Value == permission
                && grant.Department == department
                && grant.ResourceScope == resourceScope
                && grant.Reason == reason
                && grant.Effect == GrantEffect.Grant);
}
