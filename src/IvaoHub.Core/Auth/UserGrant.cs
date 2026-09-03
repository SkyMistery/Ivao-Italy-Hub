using IvaoHub.Core.Division;

namespace IvaoHub.Core.Auth;

/// <summary>What a grant does to a permission.</summary>
public enum GrantEffect
{
    Grant,
    Deny,
}

/// <summary>What a grant talks about. Only permissions for now.</summary>
public enum GrantKind
{
    Permission,
}

/// <summary>
/// A permission given to (or taken from) a single VID on top of what the staff positions derive.
/// Effective permissions are derived, union grants, minus denies (design M0 section 3.7).
/// Who granted it and when are the audit columns: <c>created_by</c> and <c>created_at</c>, not a
/// second pair of columns saying the same thing.
/// </summary>
[Audited]
public sealed class UserGrant : IAuditable
{
    public long Id { get; set; }

    public int Vid { get; set; }

    public GrantKind Kind { get; set; }

    /// <summary>The permission name, for example <c>Links.Edit</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Null means every department.</summary>
    public Department? Department { get; set; }

    public GrantEffect Effect { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Set when the roster sync no longer sees the VID as staff; the grant is kept, not deleted.</summary>
    public DateTime? SuspendedAt { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    public HubUser? User { get; set; }
}
