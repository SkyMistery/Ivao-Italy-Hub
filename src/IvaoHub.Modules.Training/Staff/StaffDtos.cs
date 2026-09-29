using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>A person as the staff's pages name them: the VID that always is, and the name the hub has — none when it has none.</summary>
public sealed record TrainingMemberDto(int Vid, string? Name);

/// <summary>
/// A training as the staff's list shows it (design M3 §4.2): what it is, whose it is, where it stands, and who trains it. The
/// short name of the rating is the core's vocabulary's; the names are the hub's. <c>CreatedAt</c> is when it was asked for,
/// named as the column the list sorts on.
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
    DateTime? ScheduledStartUtc);

/// <summary>
/// What the reader may do on the training now, as the one handler answers on the row: a button is drawn when it said yes. Never
/// on a training of the reader's own, the super administrator included (§3).
/// </summary>
/// <param name="CanDecide">Accept or refuse the request (<c>Training.Approve</c>), while it waits.</param>
/// <param name="CanAssign">Assign the trainer or change them (<c>Training.Assign</c>), while the training is accepted and going on.</param>
public sealed record StaffTrainingActionsDto(bool CanDecide, bool CanAssign);

/// <summary>
/// A training as the staff reads it on its page (design M3 §2.3, §2.4, §4.2): the request with the trainee's rating and hours
/// when they asked, the site of the theory exam for the reminder of whoever approves, the decision, the trainer, and what the
/// reader may do. Read with <c>Training.View</c>, which the core never denies, so the trainee of the row reads it too: the fields
/// the trainee may not read — the notes of the staff, the report's comment for the staff — are not here, and arrive with the one
/// function of A9 that leaves them out for the row's trainee (note <c>le-note-riservate-e-il-trainee</c>). Never an address.
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
    DateTime? ScheduledStartUtc,
    DateTime? CompletedAt,
    TrainingMemberDto? ClosedBy,
    DateTime? ClosedAt,
    bool ReadyForMockExam,
    bool ReadyForExam,
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
