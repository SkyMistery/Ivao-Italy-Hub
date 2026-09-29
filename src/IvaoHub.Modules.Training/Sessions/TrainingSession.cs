using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Training.Sessions;

/// <summary>What a session of a training came to (design M3 §1.3, §2.6). Stored by name.</summary>
public enum SessionOutcome
{
    /// <summary>The session took place and its report was published: the training is completed (§2.7).</summary>
    Held,

    /// <summary>Too little traffic (R.5): the training went back to its dates for another session, with no report.</summary>
    Rescheduled,

    /// <summary>The trainee did not come: the training closed, and the waiting of a no-show runs (§2.6).</summary>
    NoShow,
}

/// <summary>
/// A session of a training that is over, <c>trn_sessions</c> (design M3 §1.3): when it was, what it came to, and — for one rescheduled
/// — the internal notes of whoever conducted it, which the staff and the trainers read and the trainee never does (R.5; note
/// <c>le-note-riservate-e-il-trainee</c>). The session in hand is the training's own date (<see cref="Training.ScheduledStartUtc"/>): when
/// whoever conducts the training records how it went, it becomes a row here. Who recorded it and when are its stamps.
/// <para>A child row of the training (§1.1): written with it, by whoever may conduct it, and never on its own — its authorization is the
/// training's. It is the history of the trainee's path, and it stays (§6).</para>
/// </summary>
public sealed class TrainingSession : IAuditable
{
    public long Id { get; set; }

    public long TrainingId { get; set; }

    /// <summary>When the session started: the date the training had when its outcome was recorded.</summary>
    public DateTime StartsAtUtc { get; set; }

    public SessionOutcome Outcome { get; set; }

    /// <summary>What whoever conducted a session rescheduled wrote of it for the staff (R.5): reserved, never the trainee's to read.</summary>
    public string? InternalNotes { get; set; }

    /// <summary>When its outcome was recorded.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Who recorded it: the trainer, or whoever else conducts the training.</summary>
    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}
