using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Reference;

namespace IvaoHub.Modules.Training.Requests;

/// <summary>
/// The trainee's own page of the training (design M3 §4.1), what <c>/training/request</c> and <c>/training/mine</c> read: who
/// they are — never their address, which is the core's and the mail queue's only (§0.5) —, where they stand on each ladder,
/// the question on the theory exam, and their trainings, newest first.
/// </summary>
/// <param name="Vid">The trainee.</param>
/// <param name="Name">Their name as the network gives it, read only: it is changed on the network.</param>
/// <param name="AsksTheory">Whether the request asks the trainee about the theory exam (<see cref="ITheoryExamSource"/>).</param>
/// <param name="TheoryExamUrl">Where the theory exam is taken, for the question; none until the division writes it.</param>
/// <param name="Paths">One per ladder, in the order of the core's ladders.</param>
/// <param name="Trainings">Every training of theirs, requests refused and cancelled included.</param>
public sealed record MyTrainingDto(
    int Vid,
    string Name,
    bool AsksTheory,
    string? TheoryExamUrl,
    IReadOnlyList<MyTrainingPathDto> Paths,
    IReadOnlyList<TraineeTrainingDto> Trainings);

/// <summary>
/// Where the trainee stands on one ladder (§2.2): their rating and hours, the one training the hub proposes and on which
/// positions, and the first rule that refuses a request now, with what the page needs to say why — a refusal is a bare key.
/// </summary>
/// <param name="Kind">The ladder.</param>
/// <param name="RatingShortName">The trainee's rating as the staff says it; none when the hub does not know it.</param>
/// <param name="Hours">Their hours on the ladder; none when the network has said nothing, which is not zero.</param>
/// <param name="Next">The rating proposed, the one a request sends back; none when there is nothing to ask for.</param>
/// <param name="IsMockExam">Whether that training would be a mock exam, as agreed with the trainer (§2.8).</param>
/// <param name="AsksPosition">Whether a request for it chooses a position.</param>
/// <param name="Positions">The positions offered for it, the hidden ones left out, by callsign.</param>
/// <param name="Refusal">The i18n key of the first rule that refuses, in the design's order; none when a request may be made.</param>
/// <param name="BannedUntil">With a ban, until when; none when it holds until somebody lifts it.</param>
/// <param name="OpenTrainingId">With a training still open on the ladder, which one.</param>
/// <param name="WaitUntil">While the waiting after the last training runs, until when.</param>
/// <param name="MinimumHours">The hours the rating proposed needs, when the division set a threshold.</param>
public sealed record MyTrainingPathDto(
    RatingKind Kind,
    string? RatingShortName,
    decimal? Hours,
    TrainingRatingDto? Next,
    bool IsMockExam,
    bool AsksPosition,
    IReadOnlyList<TrainingPositionDto> Positions,
    string? Refusal,
    DateTime? BannedUntil,
    long? OpenTrainingId,
    DateTime? WaitUntil,
    int? MinimumHours);

/// <summary>
/// A training as its trainee reads it (§1.1, §4.1). It has no field the trainee does not read — no comment of the staff, and
/// later no note of the sheet —, so their endpoints cannot hand one over whatever the row holds. <c>RequestedAt</c> is when
/// they asked for it; <c>RejectionReason</c> why the staff refused it, as the mail says it (A7).
/// </summary>
public sealed record TraineeTrainingDto(
    long Id,
    RatingKind Kind,
    int Rating,
    string? RatingShortName,
    bool IsMockExam,
    string? Position,
    TrainingState State,
    TrainingRejection? Rejection,
    string? RejectionReason,
    string? AvailabilityText,
    string? NotesText,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    DateTime? ScheduledStartUtc,
    DateTime? CompletedAt,
    DateTime? ClosedAt,
    bool ReadyForMockExam,
    bool ReadyForExam,
    DateTime RowVersion);

/// <summary>What a trainee sends to ask for a training (§2.2).</summary>
/// <param name="Kind">The ladder.</param>
/// <param name="Rating">The rating the page proposed: the server asks for it to be the one it proposes now.</param>
/// <param name="Position">The callsign chosen, on a ladder trained on positions; none otherwise.</param>
/// <param name="AvailabilityText">When the trainee is free, in their words.</param>
/// <param name="NotesText">Notes and wishes, in their words.</param>
/// <param name="TheoryPassed">The trainee's answer on the theory exam; none when they were not asked.</param>
public sealed record TrainingRequestWriteDto(
    RatingKind Kind,
    int Rating,
    string? Position,
    string? AvailabilityText,
    string? NotesText,
    bool? TheoryPassed);

/// <summary>The version of the training the trainee saw when they pressed «cancel».</summary>
public sealed record TrainingCancellation(DateTime RowVersion);
