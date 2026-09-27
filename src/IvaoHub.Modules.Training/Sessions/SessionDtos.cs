using IvaoHub.Core.Localization;
using IvaoHub.Modules.Training.Sheets;
using IvaoHub.Modules.Training.Staff;

namespace IvaoHub.Modules.Training.Sessions;

/// <summary>
/// A session rescheduled for too little traffic (design M3 §2.6, R.5): what whoever conducted it writes of it for the staff — the
/// internal notes, which may be left empty and which the trainee never reads —, and the version of the training seen.
/// </summary>
public sealed record TrainingRescheduleDto(string? Notes, DateTime RowVersion);

/// <summary>A session the trainee did not come to (§2.6), at the version of the training seen.</summary>
public sealed record TrainingNoShowDto(DateTime RowVersion);

/// <summary>
/// The report (design M3 §2.7): the sheet — how the session went on each item, an item left out being not applicable (d4) —, the
/// comment for the trainee and the one for the staff, which the trainee never reads, the trainer's three boxes, and the version of
/// the training seen. Published at once, by whoever conducts the training (d2): there is no pass or fail.
/// </summary>
/// <param name="Sheet">How the session went on the items of the sheet the page read, one entry per item marked.</param>
/// <param name="GeneralComment">What the trainee reads of the session as a whole.</param>
/// <param name="StaffComment">What the staff and the trainers read of it, and the trainee never does.</param>
/// <param name="ReadyForMockExam">The next training on this rating is a mock exam (§2.8); never on a mock exam.</param>
/// <param name="ReadyForExam">The trainee may book the exam on the network.</param>
/// <param name="CooldownWaived">No waiting after this training (R.5).</param>
/// <param name="RowVersion">The version of the training seen.</param>
public sealed record TrainingReportDto(
    IReadOnlyList<TrainingEvaluationWriteDto>? Sheet,
    string? GeneralComment,
    string? StaffComment,
    bool ReadyForMockExam,
    bool ReadyForExam,
    bool CooldownWaived,
    DateTime RowVersion);

/// <summary>
/// A session that is over, as the staff reads it (design M3 §4.2): when it was, what it came to, the internal notes of a session
/// rescheduled — reserved: left out when the reader is the trainee of the row (note <c>le-note-riservate-e-il-trainee</c>) —, and who
/// recorded it when.
/// </summary>
public sealed record StaffSessionDto(
    long Id,
    DateTime StartsAtUtc,
    SessionOutcome Outcome,
    string? InternalNotes,
    TrainingMemberDto RecordedBy,
    DateTime RecordedAt);

/// <summary>A session that is over, as its trainee reads it (design M3 §4.1): when it was, and what it came to. Never a note.</summary>
public sealed record TraineeSessionDto(DateTime StartsAtUtc, SessionOutcome Outcome);

/// <summary>
/// An item of the sheet as the staff reads it (design M3 §4.2): which item, its section and title, how the session went on it — a
/// grade, a mark, or neither: not applicable —, the comment the trainee reads, and the note of the staff — reserved: left out when the
/// reader is the trainee of the row (note <c>le-note-riservate-e-il-trainee</c>). On a completed training, the copy its report keeps;
/// on a dated one, the item as a report would mark it now, with nothing marked yet.
/// </summary>
public sealed record StaffEvaluationDto(
    long ItemId,
    SheetSection Section,
    Localized<string> Title,
    int? Grade,
    TheoryMark? Mark,
    string? TraineeComment,
    string? StaffNote);

/// <summary>
/// An item of the report as its trainee reads it (design M3 §4.1): its section and title as the report copied them, the grade or the
/// mark — neither: not applicable —, and the comment written for them. Never the note of the staff.
/// </summary>
public sealed record TraineeEvaluationDto(
    SheetSection Section,
    Localized<string> Title,
    int? Grade,
    TheoryMark? Mark,
    string? TraineeComment);
