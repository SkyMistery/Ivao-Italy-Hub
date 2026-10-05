using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Sessions;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>A person as the staff's pages name them: the VID that always is, and the name the hub has — none when it has none.</summary>
public sealed record TrainingMemberDto(int Vid, string? Name);

/// <summary>
/// A training as the staff's list shows it (design M3 §4.2): what it is, whose it is, where it stands, and who trains it. The
/// short name of the rating is the core's vocabulary's; the names are the hub's. <c>CreatedAt</c> is when it was asked for,
/// named as the column the list sorts on. <c>Held</c> says a dated training shows as held (§1.2): its day is over in the
/// division's time zone, which nothing writes.
/// </summary>
public sealed record StaffTrainingRowDto(
    long Id,
    RatingKind Kind,
    int Rating,
    string? RatingShortName,
    bool IsMockExam,
    string? Position,
    TrainingState State,
    TrainingMemberDto Trainee,
    TrainingMemberDto? Trainer,
    DateTime CreatedAt,
    DateTime? ScheduledStartUtc,
    bool Held);

/// <summary>
/// What the reader may do on the training now, as the one handler answers on the row: a button is drawn when it said yes. Never
/// on a training of the reader's own, the super administrator included (§3).
/// </summary>
/// <param name="CanDecide">Accept or refuse the request (<c>Training.Approve</c>), while it waits.</param>
/// <param name="CanAssign">Assign the trainer or change them (<c>Training.Assign</c>), while the training is accepted and going on.</param>
/// <param name="CanConduct">
/// Propose dates, take one back, set the date by hand (<c>Training.Conduct</c>, A8), while the training has its trainer and goes
/// on: dates are proposed while it waits for one, and the date is set by hand then or once it has one.
/// </param>
/// <param name="CanClose">Close it with a reason (<c>Training.Approve</c>, A8), while it is accepted and going on.</param>
/// <param name="CanRecordOutcome">
/// Record how the session went (<c>Training.Conduct</c>, A9) — rescheduled, not attended, or reported —, once its session has started.
/// </param>
public sealed record StaffTrainingActionsDto(bool CanDecide, bool CanAssign, bool CanConduct, bool CanClose, bool CanRecordOutcome);

/// <summary>
/// A line of the history of a training (A13b; note <c>2026-09-30-lo-storico-di-un-training</c>): when, who — none for the hub itself —,
/// and what happened, with the trainers, the dates and the reason it names; the people named as the page names them, a person whose
/// data was erased by their pseudonym. Read from the core's audit log by <see cref="TrainingHistory"/>, never written.
/// </summary>
/// <param name="At">When it was written.</param>
/// <param name="By">Who wrote it; none when the hub did, as when it closes a training nobody dated in time.</param>
/// <param name="Event">What happened.</param>
/// <param name="Trainer">The trainer assigned, the first or the one after a change.</param>
/// <param name="PreviousTrainer">The trainer before a change.</param>
/// <param name="Date">
/// The date chosen, set or moved to; for a session rescheduled or not attended, the date of that session.
/// </param>
/// <param name="PreviousDate">The date before it was moved.</param>
/// <param name="Reason">Why the staff refused or closed it.</param>
public sealed record TrainingHistoryEntryDto(
    DateTime At,
    TrainingMemberDto? By,
    TrainingHistoryEvent Event,
    TrainingMemberDto? Trainer,
    TrainingMemberDto? PreviousTrainer,
    DateTime? Date,
    DateTime? PreviousDate,
    string? Reason);

/// <summary>
/// A training as the staff reads it on its page (design M3 §2.3, §2.4, §2.5, §2.6, §2.7, §4.2): the request with the trainee's
/// rating and hours when they asked, the site of the theory exam for the reminder of whoever approves, the decision, the trainer,
/// the dates proposed with their warnings, the session — held, from the day after it (§1.2), and whether its date was the trainee's
/// choice or set by hand —, the sessions that are over, the sheet and the report, the closing with its reason, the history of its
/// changes (A13b; empty on a trainee's path, which does not draw it, A13d), and what the reader may do. Never an address. <c>Sheet</c> is the copy a completed training's report keeps; while
/// the training is dated, the active items of its ladder and rating as a report would mark them now, with nothing marked; none
/// otherwise. Read with <c>Training.View</c>, which the core never denies, so the trainee of the row reads it too: the one rule of
/// <c>ReservedFields</c> leaves out what is reserved when they do — <c>StaffComment</c>, the <c>StaffNote</c> of every item of the
/// sheet, the <c>InternalNotes</c> of every session, the <c>History</c> — and says so in <c>ReservedLeftOut</c> (note
/// <c>le-note-riservate-e-il-trainee</c>; the history, Carmine's answer on #197: not by the trainee).
/// </summary>
public sealed record StaffTrainingDto(
    long Id,
    RatingKind Kind,
    int Rating,
    string? RatingShortName,
    string? RatingNameKey,
    bool IsMockExam,
    string? Position,
    string? AirportIcao,
    string? Fir,
    TrainingMemberDto Trainee,
    string? TraineeRatingShortName,
    decimal? TraineeHoursAtRequest,
    DateTime RequestedAt,
    DateTime? TheoryConfirmedAt,
    string? TheoryExamUrl,
    string? AvailabilityText,
    string? NotesText,
    TrainingState State,
    TrainingRejection? Rejection,
    string? RejectionReason,
    TrainingMemberDto? DecidedBy,
    DateTime? DecidedAt,
    TrainingMemberDto? Trainer,
    TrainingMemberDto? AssignedBy,
    DateTime? AssignedAt,
    IReadOnlyList<StaffSlotDto> Slots,
    DateTime? ScheduledStartUtc,
    bool Held,
    bool DateChosenByTrainee,
    DateTime? CompletedAt,
    TrainingMemberDto? ClosedBy,
    DateTime? ClosedAt,
    string? CloseReason,
    bool ReadyForMockExam,
    bool ReadyForExam,
    bool CooldownWaived,
    string? GeneralComment,
    string? StaffComment,
    IReadOnlyList<StaffEvaluationDto> Sheet,
    IReadOnlyList<StaffSessionDto> Sessions,
    IReadOnlyList<TrainingHistoryEntryDto> History,
    bool ReservedLeftOut,
    StaffTrainingActionsDto Actions,
    DateTime RowVersion);

/// <summary>
/// Somebody who may train a training (§2.4): of the staff of the training — the direction and the training department, trainers
/// included —, known to the hub because they signed in, with a rating on its ladder at least the one trained, and never its
/// trainee.
/// </summary>
/// <param name="Vid">Who they are.</param>
/// <param name="Name">Their name as the hub has it.</param>
/// <param name="RatingShortName">Their rating on the training's ladder, as the core's vocabulary names it.</param>
/// <param name="Positions">Their positions of the staff of the training, as the network spells them.</param>
/// <param name="IsCurrent">Whether they are the training's trainer already.</param>
public sealed record TrainerCandidateDto(int Vid, string Name, string? RatingShortName, IReadOnlyList<string> Positions, bool IsCurrent);

/// <summary>The version of the training the reader saw when they pressed «accept».</summary>
public sealed record TrainingDecisionDto(DateTime RowVersion);

/// <summary>A refusal of the staff (§2.3): the reason the trainee reads, and the version seen.</summary>
public sealed record TrainingRejectionDto(string? Reason, DateTime RowVersion);

/// <summary>An assignment (§2.4): the trainer chosen among the candidates, and the version seen.</summary>
public sealed record TrainingAssignmentDto(int TrainerVid, DateTime RowVersion);
