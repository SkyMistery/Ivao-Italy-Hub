namespace IvaoHub.Modules.Training;

/// <summary>
/// The kinds of notification of the training (design M3 §5.2), declared to the core through <c>IModule.NotificationTypes</c>.
/// The words are in the module's language file: the mail under <c>mail.{type}</c>, the label of the profile under
/// <c>notifications.{name}</c> of the <c>training</c> namespace.
/// </summary>
public static class TrainingNotifications
{
    /// <summary>A request of theirs was received (§2.2): not a refusal of the hub, which the screen says instead. Its audience is the trainee.</summary>
    public const string RequestReceived = "training.requestReceived";

    /// <summary>The staff accepted a request of theirs (§2.3, A7). Its audience is the trainee.</summary>
    public const string RequestAccepted = "training.requestAccepted";

    /// <summary>The staff refused a request of theirs, with the reason (§2.3, A7). Its audience is the trainee.</summary>
    public const string RequestRejected = "training.requestRejected";

    /// <summary>A trainer was assigned to a training, or changed (§2.4, A7). Its audience is the trainee and the trainer, each in their words.</summary>
    public const string TrainerAssigned = "training.trainerAssigned";

    /// <summary>The trainer proposed dates for the session (§2.5, A8), which the trainee chooses from. Its audience is the trainee.</summary>
    public const string DatesProposed = "training.datesProposed";

    /// <summary>The date of the session is fixed (§2.5, A8): chosen by the trainee, or set by hand. Its audience is the trainee and the trainer.</summary>
    public const string DateConfirmed = "training.dateConfirmed";

    /// <summary>The session is near (§5.3, A8): <c>reminderLeadHours</c> before it, once. Its audience is the trainee and the trainer.</summary>
    public const string Reminder = "training.reminder";

    /// <summary>
    /// The training was closed (§2.5, A8): by the staff with a reason, or by the hub when no date was chosen in the time the division
    /// gives; or because the trainee did not come to the session, with the waiting it brings (§2.6, §5.2, A9). Its audience is the
    /// trainee.
    /// </summary>
    public const string TrainingClosed = "training.trainingClosed";

    /// <summary>The report of the session was published (§2.7, §5.2, A9): the trainee reads it on the page of the training. Its audience is the trainee.</summary>
    public const string ReportPublished = "training.reportPublished";

    /// <summary>
    /// The member was banned from asking for trainings (§2.9, §5.2, A10a), with the reason and until when. Its audience is the member
    /// banned. Lifting the ban sends nothing: the design names no mail for it.
    /// </summary>
    public const string Banned = "training.banned";

    public static readonly IReadOnlyList<string> All =
    [
        RequestReceived, RequestAccepted, RequestRejected, TrainerAssigned, DatesProposed, DateConfirmed, Reminder, TrainingClosed,
        ReportPublished, Banned,
    ];
}
