using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.Training;

/// <summary>Where a training is (design M3 §2.1). Stored by name, and no state is ever deleted: everything stays on record.</summary>
public enum TrainingState
{
    /// <summary>Asked for, and waiting for somebody to accept or refuse it; the trainee may still cancel it.</summary>
    Requested,

    /// <summary>Accepted, with no trainer yet: normal, for as long as it takes (R.1).</summary>
    Accepted,

    /// <summary>A trainer is assigned and the date is still to be fixed — again after a rescheduled session.</summary>
    Assigned,

    /// <summary>The session has a date. From the day after it the training shows as held, without that being written (§1.2).</summary>
    Scheduled,

    /// <summary>The trainer published the report.</summary>
    Completed,

    /// <summary>Refused: by the hub itself when the trainee says the theory is not passed, or by the staff with a reason.</summary>
    Rejected,

    /// <summary>Taken back by the trainee before anybody accepted it.</summary>
    Cancelled,

    /// <summary>Closed by the staff, or by the hub when the trainee chose no date in time (§2.5).</summary>
    Closed,

    /// <summary>The trainee did not come to the session.</summary>
    NoShow,
}

/// <summary>Who refused a training (design M3 §1.2). Stored by name.</summary>
public enum TrainingRejection
{
    /// <summary>The trainee said the theory exam of the rating is not passed (§2.2): recorded, shown on the screen, and no mail.</summary>
    TheoryNotPassed,

    /// <summary>Somebody of the staff, with a reason the trainee reads (A7).</summary>
    Staff,
}

/// <summary>
/// A training, <c>trn_trainings</c> (design M3 §1.2): one trainee, one ladder and one rating, from the request to the report. It
/// carries from the start every column the later phases write — the decision and the trainer (A7), the date and its reminder
/// (A8), the report (A9) — so that none of them migrates this table again.
/// <para>The trainee asks for it (<see cref="ISubmittedByMembers"/>) and it is about them (<see cref="IHasStakeholder"/>): they
/// cancel it, and will choose its date, through the one exception of the write guard for a member's own row, and nobody
/// approves, assigns, conducts or edits a training of their own, the super administrator included (§3). It is in the care of
/// the base department of the module (<see cref="IOwnedByDepartment"/>, the mask the interceptor keeps) and read by members only
/// (<see cref="IVisible"/>); which member is the endpoints' business — the trainee reads their own through theirs, with no field
/// of the staff's in it, the staff with <c>Training.View</c> (A7). It names the FIR of its position (<see cref="IHasFir"/>) for
/// the heads of a FIR (A11), and a trainer is enabled on this training alone through its scope (<see cref="IHasResourceScope"/>,
/// §3.3).</para>
/// <para>No participants, on purpose (§1.1): the core would give them <c>Training.View</c> on the row, and with it the notes of
/// the staff.</para>
/// <para>It sits at the root of the module because a class of this name in a namespace below the module's would be hidden
/// there by the module's own namespace.</para>
/// </summary>
[Audited]
[PermissionArea(TrainingPermissions.Area)]
public sealed class Training : IOwnedByDepartment, IAuditable, IVisible, ISubmittedByMembers, IHasStakeholder, IHasFir, IHasResourceScope
{
    /// <summary>As wide as a callsign in the core's reference of the positions a training copies it from.</summary>
    public const int MaxPositionLength = 32;

    /// <summary>As wide as an airport of that reference.</summary>
    public const int MaxAirportLength = 4;

    /// <summary>As wide as a FIR of that reference.</summary>
    public const int MaxFirLength = 8;

    /// <summary>The bound of a text of the request and of a reason of the staff. The report's comments are A9's to bound.</summary>
    public const int MaxTextLength = 2000;

    public long Id { get; set; }

    /// <summary>The ladder: the trainee's hours, rating and waiting are counted on it alone (§2.2).</summary>
    public RatingKind Kind { get; set; }

    /// <summary>
    /// The rating trained for, by the number the hub keeps: the one after the trainee's, with a practical training, as the core's
    /// vocabulary says when the request is made (§1.7, §2.2).
    /// </summary>
    public int Rating { get; set; }

    /// <summary>Decided by the hub at the request, from the trainee's history (§2.8), and never by the trainee.</summary>
    public bool IsMockExam { get; set; }

    /// <summary>The position chosen, on a ladder trained on positions; none for a pilot's training.</summary>
    public string? Position { get; set; }

    /// <summary>The airport of that position, copied from the core's reference with it; none for a sector.</summary>
    public string? AirportIcao { get; set; }

    /// <summary>The FIR of that position — an airport position's is its airport's —, copied with it; none for a pilot's training.</summary>
    public string? Fir { get; set; }

    /// <summary>Who the training is about, and who asked for it.</summary>
    public int TraineeVid { get; set; }

    /// <summary>The trainee's rating on the ladder when they asked, as the hub knew it; none when the network had said nothing.</summary>
    public int? TraineeRatingAtRequest { get; set; }

    /// <summary>The trainee's hours on the ladder when they asked, as the hub knew them; none when the network had said nothing.</summary>
    public decimal? TraineeHoursAtRequest { get; set; }

    /// <summary>When the trainee said the theory exam of the rating is passed (§2.2); none on a request refused for it.</summary>
    public DateTime? TheoryConfirmedAt { get; set; }

    /// <summary>What the trainee wrote about when they are free.</summary>
    public string? AvailabilityText { get; set; }

    /// <summary>What else the trainee wrote: notes and wishes.</summary>
    public string? NotesText { get; set; }

    public TrainingState State { get; set; }

    /// <summary>Who refused it, on a <see cref="TrainingState.Rejected"/> training.</summary>
    public TrainingRejection? Rejection { get; set; }

    /// <summary>Why the staff refused it: in the mail to the trainee, and on their page (A7).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Who accepted or refused it (A7); none when the hub refused it by itself.</summary>
    public int? DecidedBy { get; set; }

    /// <summary>When it was accepted or refused, by somebody or by the hub.</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>The trainer assigned (A7), who holds <c>Training.Conduct</c> on this training alone.</summary>
    public int? TrainerVid { get; set; }

    public int? AssignedBy { get; set; }

    public DateTime? AssignedAt { get; set; }

    /// <summary>When the session in hand starts (A8); a session already over is a row of the sessions (A9).</summary>
    public DateTime? ScheduledStartUtc { get; set; }

    /// <summary>The availability the trainee chose among the trainer's (A8); none when the date was set by hand.</summary>
    public long? ChosenSlotId { get; set; }

    /// <summary>
    /// When the reminder of the session in hand left (design M3 §5.3): once per session, and cleared with the date. Not in the
    /// table of §1.2, which lists the session in hand on this row; §5.3 puts the reminder on it, and A8 would otherwise migrate.
    /// </summary>
    public DateTime? RemindedAt { get; set; }

    /// <summary>The trainer's box of the report (A9): the next request on the same rating is a mock exam (§2.8).</summary>
    public bool ReadyForMockExam { get; set; }

    /// <summary>The trainer's box of the report (A9): the exam is booked on the network.</summary>
    public bool ReadyForExam { get; set; }

    /// <summary>The trainer's box of the report (A9): no waiting after this training.</summary>
    public bool CooldownWaived { get; set; }

    /// <summary>The report's comment for the trainee (A9).</summary>
    public string? GeneralComment { get; set; }

    /// <summary>The report's comment for the staff (A9), which the trainee never reads.</summary>
    public string? StaffComment { get; set; }

    /// <summary>When the report was published (A9).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Who closed it: the trainee who cancelled it, the staff; none when the hub closed it by itself (A8).</summary>
    public int? ClosedBy { get; set; }

    /// <summary>When it was cancelled, closed, or marked as a no-show.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Why the staff closed it by hand (A8); the state says which closure it was.</summary>
    public string? CloseReason { get; set; }

    /// <summary>
    /// The ladder while the training is open, none once it is not: the column of the unique key that makes «one open request
    /// per ladder» (§2.2 point 2) a rule of the database as well, so two requests sent at once cannot both pass.
    /// </summary>
    public RatingKind? OpenKind
    {
        get => IsOpen(State) ? Kind : null;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    /// <summary>When the trainee asked for it.</summary>
    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>Any member, as the global filter reads it; which member is the endpoints' business.</summary>
    public Visibility Visibility
    {
        get => Visibility.Members;

        // The column is written from the getter; what the database holds is never read back into the row.
        private set { }
    }

    public int? StakeholderVid => TraineeVid;

    public string ResourceScope => ScopeOf(Id);

    /// <summary>The scope of a grant on one training (§3.3): what the assignment writes for the trainer (A7).</summary>
    public static string ScopeOf(long id) => $"{TrainingModule.ModuleKey}:training:{id}";

    /// <summary>The states of a training still going: only one of them per trainee and ladder (§2.2 point 2).</summary>
    public static bool IsOpen(TrainingState state) =>
        state is TrainingState.Requested or TrainingState.Accepted or TrainingState.Assigned or TrainingState.Scheduled;
}
