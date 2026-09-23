using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// A member who holds a permission, with everything they hold, so the caller can ask where: on which department and on
/// which rows (M2, T13, note 2026-09-23-la-validazione §3.3).
/// </summary>
public sealed record PermissionHolder(int Vid, bool IsSuperadmin, IReadOnlyList<EffectivePermission> Permissions)
{
    /// <summary>The handler's own question, asked of this member instead of the one making the request.</summary>
    public bool Has(string permission, Department department, string? resourceScope = null) =>
        PermissionSet.Has(Permissions, IsSuperadmin, permission, department, resourceScope);
}

/// <summary>
/// Who holds a permission. A notification meant for "whoever may do this" — the daily digest of the validators — asks
/// here rather than adding up positions and grants itself: the answer is the one a login computes, for every member who
/// could hold anything.
/// </summary>
public interface IPermissionHolders
{
    Task<IReadOnlyList<PermissionHolder>> HoldersOfAsync(string permission, CancellationToken cancellationToken = default);
}
