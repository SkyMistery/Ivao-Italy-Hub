using IvaoHub.Core.Division;

namespace IvaoHub.Core.Awards;

/// <summary>
/// "This member received this award, for this reason." Always written by a person holding
/// <c>Awards.Assign</c>: the hub never assigns an award by itself (plan section 9.1). Who assigned it
/// and when are the audit columns, <c>created_by</c> and <c>created_at</c>, not a second pair saying
/// the same thing — the shape <c>UserGrant</c> set.
/// <para>A global resource, like a grant: it belongs to a member and not to a department, so the
/// endpoint's policy is the whole check. Revoking one deletes it, and the audit log keeps what it was.</para>
/// </summary>
[Audited]
public sealed class AwardAssignment : IAuditable
{
    public long Id { get; set; }

    /// <summary>The award; a real foreign key, since both tables are the core's.</summary>
    public long AwardId { get; set; }

    /// <summary>The member. Unconstrained: a member may never have signed in to the hub.</summary>
    public int Vid { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The signal of the queue this assignment answered, when it answered one. Set once, on creation,
    /// together with the signal becoming handled; one signal is answered by one assignment at most.
    /// </summary>
    public long? SignalId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
