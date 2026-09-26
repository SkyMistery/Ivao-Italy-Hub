using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// A date the trainer proposes for the session of a training, <c>trn_slots</c> (design M3 §1.3, §2.5): when it would start and
/// end, and what the hub warned about when it was written — other trainings with their session on those days, entries of the
/// calendar of the kinds the division checks —, as it was then. Who proposed it and when are its stamps: the time the trainee has
/// had to choose is counted from them (§1.6, <c>responseReminderDays</c>, <c>maxResponseDays</c>).
/// <para>A child row of the training (§1.1): written with it, by whoever may conduct it, and never on its own — its authorization
/// is the training's. It lives only while the training waits for a date: the trainee chooses one, or the date is set by hand, or
/// the training closes, and every one of them goes (§1.3, §6), the training keeping which one was chosen.</para>
/// </summary>
public sealed class TrainingSlot : IAuditable
{
    public long Id { get; set; }

    public long TrainingId { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }

    /// <summary>The warnings when it was written (<see cref="DateWarnings"/>): nobody's name or VID in them.</summary>
    public string WarningsJson { get; set; } = "[]";

    /// <summary>When it was proposed.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Who proposed it: the trainer, or whoever else conducts the training.</summary>
    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }
}
