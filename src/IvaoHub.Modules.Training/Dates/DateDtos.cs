using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Staff;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// What the hub finds on the days of a date (design M3 §2.5), before anybody writes it: the division's policy, so the page knows
/// whether a warning asks for a confirmation or refuses the date, and the warnings — none when the policy is not to look.
/// </summary>
public sealed record DateConflictsDto(ConflictPolicy Policy, IReadOnlyList<DateWarning> Warnings);

/// <summary>
/// A date proposed for the session (§2.5): when it would start and end, in UTC. Either may arrive empty, as the page sends a box
/// nobody filled in (A8b), and is then refused as required on its own field.
/// </summary>
public sealed record TrainingSlotWriteDto(DateTime? StartsAtUtc, DateTime? EndsAtUtc);

/// <summary>
/// The trainer's dates (§2.5), proposed together, so the trainee is written to once: the dates; whether whoever proposes them has
/// seen their warnings and confirms them, which the policy <c>Warn</c> asks; and the version of the training they saw.
/// </summary>
public sealed record TrainingSlotsWriteDto(IReadOnlyList<TrainingSlotWriteDto>? Slots, bool Confirmed, DateTime RowVersion);

/// <summary>A date taken back before the trainee chose it, at the version of the training seen.</summary>
public sealed record TrainingSlotWithdrawalDto(DateTime RowVersion);

/// <summary>
/// The date set by hand (§2.5, d2): whenever the session starts — among the dates proposed or not, before or after today —, with
/// the same warnings and their confirmation, and the version of the training seen. The start may arrive empty, and is then
/// refused as required.
/// </summary>
public sealed record TrainingDateWriteDto(DateTime? StartsAtUtc, bool Confirmed, DateTime RowVersion);

/// <summary>The trainee's choice among the dates proposed (§2.5), at the version of their training they saw.</summary>
public sealed record TrainingSlotChoiceDto(long SlotId, DateTime RowVersion);

/// <summary>A training closed by the staff (§2.5): the reason the trainee reads, and the version seen.</summary>
public sealed record TrainingClosureDto(string? Reason, DateTime RowVersion);

/// <summary>A date proposed, as its trainee chooses it: nothing but when (§4.1). The warnings are the staff's.</summary>
public sealed record TraineeSlotDto(long Id, DateTime StartsAtUtc, DateTime EndsAtUtc);

/// <summary>A date proposed, as the staff reads it (§4.2): when, what the hub warned about then, and who proposed it when.</summary>
public sealed record StaffSlotDto(
    long Id,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    IReadOnlyList<DateWarning> Warnings,
    TrainingMemberDto ProposedBy,
    DateTime ProposedAt);
