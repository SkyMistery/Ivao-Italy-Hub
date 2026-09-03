namespace IvaoHub.Core.Content;

/// <summary>What happened to a signal. Awards are never assigned automatically (plan section 9.1).</summary>
public enum AwardSignalStatus
{
    Pending,
    Handled,
    Dismissed,
}

/// <summary>
/// "This member may deserve something": a projection written by a module through
/// <c>IProjectable</c>. A human decides, the code only points.
/// <para>Deliberately not <c>IVisible</c> nor <c>IOwnedByDepartment</c>, unlike the other two
/// projections: a signal belongs to a member, not to a department, and there is nothing to compare
/// an owner against. It is a global resource in the sense of design section 3.9 — like
/// <c>UserGrant</c> and <c>AuditLogEntry</c> — read behind the global permission
/// <c>Awards.Assign</c> and never filtered by department.</para>
/// </summary>
public sealed class AwardSignal
{
    public long Id { get; set; }

    public string SourceModule { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public int Vid { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Set once when the signal appears; a signal already handled is never overwritten.</summary>
    public AwardSignalStatus Status { get; set; } = AwardSignalStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime? HandledAt { get; set; }

    public int? HandledBy { get; set; }
}
