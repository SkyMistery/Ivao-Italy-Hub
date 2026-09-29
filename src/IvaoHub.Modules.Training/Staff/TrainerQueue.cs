using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Sessions;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// A training whose trainee has let the dates proposed wait longer than the division gives before the trainer is told (design M3
/// §2.5, R.3: <c>responseReminderDays</c>): the training as the staff's list shows it, and the whole days since the last date was
/// proposed.
/// </summary>
public sealed record WaitingTrainingDto(StaffTrainingRowDto Training, int Days);

/// <summary>
/// A trainer's trainings to move (design M3 §4.3, the block <c>training.trainerQueue</c>): the ones whose dates are theirs to propose,
/// the ones waiting for the trainee's choice for longer than the division gives (§2.5, R.3), and the ones whose report is theirs to
/// write. Each as the staff's list shows it, and each a link to its page, where the step is taken.
/// </summary>
public sealed record TrainerQueueDto(
    IReadOnlyList<StaffTrainingRowDto> ToPropose,
    IReadOnlyList<WaitingTrainingDto> Waiting,
    IReadOnlyList<StaffTrainingRowDto> ToReport);

/// <summary>
/// The three parts of a trainer's queue (design M3 §4.3), as a query and a pure function, so that the database and a test ask the same
/// thing. The rules are the ones the pages already follow, read here and never written again: a date is proposed while the training
/// waits for one (<see cref="TrainingDates"/>), the trainee who has not chosen is counted from the last date proposed
/// (<see cref="TrainingDates.Unanswered"/>, the one the closing of the night reads), and the session's outcome is recorded once it has
/// started (<see cref="TrainingSessions.IsRecordable"/>).
/// </summary>
public static class TrainerQueue
{
    /// <summary>A trainer's trainings still in their hands: assigned to them, waiting for a date or dated.</summary>
    public static IQueryable<Training> Theirs(IQueryable<Training> trainings, int trainerVid)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        return trainings.Where(training => training.TrainerVid == trainerVid
            && (training.State == TrainingState.Assigned || training.State == TrainingState.Scheduled));
    }

    /// <summary>
    /// The trainer's trainings split, given their proposals, the moment and <c>responseReminderDays</c>:
    /// <list type="bullet">
    /// <item><b>to propose</b> — waiting for a date with no date proposed still to come: none yet, or all gone by unchosen;</item>
    /// <item><b>waiting</b> — with dates still to come that the trainee has let wait more than <paramref name="reminderDays"/> days
    /// since the last was proposed, the longest first; before that nothing is the trainer's to do;</item>
    /// <item><b>to report</b> — dated, and its session started: rescheduled, not attended or reported, from the page.</item>
    /// </list>
    /// A training dated and still to come is in none of them: it waits for its day.
    /// </summary>
    public static TrainerQueueParts Split(
        IReadOnlyList<Training> trainings,
        IReadOnlyList<TrainingSlot> slots,
        DateTime now,
        int reminderDays)
    {
        ArgumentNullException.ThrowIfNull(trainings);
        ArgumentNullException.ThrowIfNull(slots);

        var toCome = slots.Where(slot => slot.StartsAtUtc > now).Select(slot => slot.TrainingId).ToHashSet();
        var late = TrainingDates.Unanswered(trainings.AsQueryable(), slots.AsQueryable(), now.AddDays(-reminderDays))
            .Select(training => training.Id)
            .ToHashSet();

        return new TrainerQueueParts(
            [
                .. trainings
                    .Where(training => training.State == TrainingState.Assigned && !toCome.Contains(training.Id))
                    .OrderBy(training => training.AssignedAt)
                    .ThenBy(training => training.Id),
            ],
            [
                .. trainings
                    .Where(training => late.Contains(training.Id) && toCome.Contains(training.Id))
                    .Select(training => (Training: training, Days: DaysSince(slots.Where(slot => slot.TrainingId == training.Id).Max(slot => slot.CreatedAt), now)))
                    .OrderByDescending(waiting => waiting.Days)
                    .ThenBy(waiting => waiting.Training.Id),
            ],
            [
                .. trainings
                    .Where(training => TrainingSessions.IsRecordable(training, now))
                    .OrderBy(training => training.ScheduledStartUtc)
                    .ThenBy(training => training.Id),
            ]);
    }

    /// <summary>The whole days from <paramref name="since"/> to <paramref name="now"/>: three days and an hour are three.</summary>
    public static int DaysSince(DateTime since, DateTime now) => (int)Math.Floor((now - since).TotalDays);
}

/// <summary>The three parts of a trainer's queue, as <see cref="TrainerQueue.Split"/> finds them.</summary>
public sealed record TrainerQueueParts(
    IReadOnlyList<Training> ToPropose,
    IReadOnlyList<(Training Training, int Days)> Waiting,
    IReadOnlyList<Training> ToReport);
