using IvaoHub.Modules.Training.Dates;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// The views of the staff's list of trainings (design M3 §4.2), as <c>filter[queue]</c> names them: to approve, to assign, in
/// progress, to close — held and still without a report —, and the history. Left out, the list holds every training.
/// <para>A session shows as held from the day after its own, in the division's time zone (§1.2): nothing writes it, so «to close»
/// and «in progress» are told apart by the moment today began there, which the list asks for at every read.</para>
/// </summary>
public static class StaffQueue
{
    /// <summary>The name of the filter, in <c>filter[queue]</c>.</summary>
    public const string Filter = "queue";

    /// <summary>Requests waiting to be accepted or refused.</summary>
    public const string ToApprove = "toApprove";

    /// <summary>Accepted, with no trainer yet.</summary>
    public const string ToAssign = "toAssign";

    /// <summary>With a trainer, the session still to come: to be dated, or dated for today or later.</summary>
    public const string InProgress = "inProgress";

    /// <summary>Held — the session's day is over — and still without a report or an outcome.</summary>
    public const string ToClose = "toClose";

    /// <summary>Over: reported, refused, cancelled, closed or not attended.</summary>
    public const string History = "history";

    /// <summary>Every view, in the order the list offers them.</summary>
    public static readonly IReadOnlyList<string> All = [ToApprove, ToAssign, InProgress, ToClose, History];

    /// <summary>
    /// The trainings of one view, given the moment a session starts to show as held — <see cref="HeldBefore"/> —; null for a view
    /// that does not exist, which the list answers with 400.
    /// </summary>
    public static IQueryable<Training>? Narrow(IQueryable<Training> trainings, string view, DateTime heldBefore)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        return view switch
        {
            ToApprove => trainings.Where(training => training.State == TrainingState.Requested),
            ToAssign => trainings.Where(training => training.State == TrainingState.Accepted),
            InProgress => trainings.Where(training => training.State == TrainingState.Assigned
                || (training.State == TrainingState.Scheduled
                    && (training.ScheduledStartUtc == null || training.ScheduledStartUtc >= heldBefore))),
            ToClose => trainings.Where(training => training.State == TrainingState.Scheduled && training.ScheduledStartUtc < heldBefore),
            History => trainings.Where(training => training.State == TrainingState.Completed
                || training.State == TrainingState.Rejected
                || training.State == TrainingState.Cancelled
                || training.State == TrainingState.Closed
                || training.State == TrainingState.NoShow),
            _ => null,
        };
    }

    /// <summary>
    /// The moment today began in the division's time zone, in UTC: a session that started before it was on a day that is over, and
    /// shows as held (§1.2). A day that begins in a gap of the clock begins at its first moment that exists.
    /// </summary>
    public static DateTime HeldBefore(DateTime utcNow, TimeZoneInfo zone) => DivisionDays.Begins(DivisionDays.Of(utcNow, zone), zone);

    /// <summary>Whether a session that starts at <paramref name="scheduledStartUtc"/> shows as held now (§1.2).</summary>
    public static bool IsHeld(DateTime scheduledStartUtc, DateTime utcNow, TimeZoneInfo zone) =>
        scheduledStartUtc < HeldBefore(utcNow, zone);

    /// <summary>
    /// Whether a training shows as held (§1.2, R.4): dated, and its session on a day that is over in the division's time zone. Never
    /// written: the pages read it off the date, as the view «to close» does.
    /// </summary>
    public static bool IsHeld(Training training, DateTime utcNow, TimeZoneInfo zone) => IsHeld(training, HeldBefore(utcNow, zone));

    /// <summary>The same, given the moment a session starts to show as held — <see cref="HeldBefore"/> —, for a whole page at once.</summary>
    public static bool IsHeld(Training training, DateTime heldBefore)
    {
        ArgumentNullException.ThrowIfNull(training);

        return training.State == TrainingState.Scheduled && training.ScheduledStartUtc is { } start && start < heldBefore;
    }
}
