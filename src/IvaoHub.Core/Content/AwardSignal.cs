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
