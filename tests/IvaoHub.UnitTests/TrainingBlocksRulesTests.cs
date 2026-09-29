using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Public;
using IvaoHub.Modules.Training.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the blocks and of the public pages of the training (M3, A10b; design M3 §2.5, §4.1, §4.3, §5.1; note
/// <c>il-training-in-pubblico</c>) with no database: which sessions are public — the ones the calendar shows, by one rule —, which are
/// still to be held, and how a trainer's trainings split into the three parts of their queue: the dates to propose, the trainee's choice
/// late after <c>responseReminderDays</c>, the reports to write.
/// <para>The queries are the ones the database is asked, run here on lists.</para>
/// </summary>
public sealed class TrainingBlocksRulesTests
{
    /// <summary>Two hours ahead of UTC: a day there begins at 22:00 UTC of the day before.</summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("trn-test+2", TimeSpan.FromHours(2), "trn-test+2", "trn-test+2");

    private static readonly DateTime Day = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ASessionIsPublicExactlyWhenTheCalendarShowsIt()
    {
        var context = new ProjectionContext(["it", "en"], "it", new BlockDocumentWalker(["it", "en"]), new StubClock(Day));
        var isPublic = Training.SessionIsPublic.Compile();

        // Every state, dated and not: the page of a session and the entry of the calendar never disagree.
        foreach (var state in Enum.GetValues<TrainingState>())
        {
            foreach (var start in new DateTime?[] { Day.AddHours(16), null })
            {
                var training = Row(1, state, start);
                Assert.Equal(training.Project(context) is not null, isPublic(training));
            }
        }

        // Which, said out loud: dated, or completed with the session its report is about.
        Assert.True(isPublic(Row(2, TrainingState.Scheduled, Day.AddHours(16))));
        Assert.True(isPublic(Row(3, TrainingState.Completed, Day.AddHours(-20))));
        Assert.False(isPublic(Row(4, TrainingState.Assigned, null)));
        Assert.False(isPublic(Row(5, TrainingState.Closed, null)));
        Assert.False(isPublic(Row(6, TrainingState.NoShow, null)));
    }

    [Fact]
    public void TheSessionsStillToBeHeldAreTheDatedOnesOfTodayAndLaterInTheDivision()
    {
        // Noon of the 11th in UTC: the 11th there too, which began at 22:00 UTC of the 10th.
        var heldBefore = StaffQueue.HeldBefore(Day.AddHours(36), Zone);
        Training[] all =
        [
            Row(1, TrainingState.Scheduled, Day.AddHours(21)),     // 23:00 of the 10th there: held
            Row(2, TrainingState.Scheduled, Day.AddHours(22.5)),   // 00:30 of the 11th there, begun: today's
            Row(3, TrainingState.Scheduled, Day.AddHours(40)),     // later today
            Row(4, TrainingState.Scheduled, Day.AddHours(60)),     // tomorrow
            Row(5, TrainingState.Completed, Day.AddHours(23)),     // today's, and reported: over
            Row(6, TrainingState.Assigned, null),                  // no date yet
            Row(7, TrainingState.Closed, null),                    // no session any more
        ];

        var upcoming = PublicSessions.Upcoming(all.AsQueryable(), heldBefore).Select(training => training.Id).Order();
        Assert.Equal([2L, 3L, 4L], upcoming);
    }

    [Fact]
    public void ATrainersTrainingsSplitIntoTheDatesToProposeTheLateChoicesAndTheReportsToWrite()
    {
        var now = Day;
        Training[] all =
        [
            Row(1, TrainingState.Assigned, null, assignedAt: now.AddDays(-2)),   // nothing proposed yet
            Row(2, TrainingState.Assigned, null, assignedAt: now.AddDays(-3)),   // proposed yesterday: the trainee has time
            Row(3, TrainingState.Assigned, null, assignedAt: now.AddDays(-9)),   // proposed four days ago, unchosen
            Row(4, TrainingState.Assigned, null, assignedAt: now.AddDays(-8)),   // proposed, and every date gone by unchosen
            Row(5, TrainingState.Scheduled, now.AddHours(-2)),                   // its session has started
            Row(6, TrainingState.Scheduled, now.AddDays(1)),                     // its session is tomorrow
            Row(7, TrainingState.Assigned, null, assignedAt: now.AddDays(-20)),  // proposed ten days ago, unchosen
        ];
        TrainingSlot[] slots =
        [
            Slot(2, proposed: now.AddDays(-1), starts: now.AddDays(3)),
            Slot(3, proposed: now.AddDays(-5), starts: now.AddDays(2)),
            Slot(3, proposed: now.AddDays(-4), starts: now.AddDays(4)),
            Slot(4, proposed: now.AddDays(-6), starts: now.AddDays(-1)),
            Slot(7, proposed: now.AddDays(-10), starts: now.AddDays(5)),
        ];

        var parts = TrainerQueue.Split(all, slots, now, reminderDays: 3);

        // To propose: none yet, or none left to choose — the oldest assignment first.
        Assert.Equal([4L, 1L], parts.ToPropose.Select(training => training.Id));

        // Waiting longer than the division gives, counted from the last date proposed — the longest first.
        Assert.Equal([(7L, 10), (3L, 4)], parts.Waiting.Select(waiting => (waiting.Training.Id, waiting.Days)));

        // To report: the session has started. A session to come is nobody's to move yet.
        Assert.Equal([5L], parts.ToReport.Select(training => training.Id));

        // With more days given, the trainee who chose nothing for four days is still within them.
        Assert.Equal([7L], TrainerQueue.Split(all, slots, now, reminderDays: 5).Waiting.Select(waiting => waiting.Training.Id));
    }

    [Fact]
    public void TheTrainingsOfATrainerAreTheOnesStillInTheirHands()
    {
        Training[] all =
        [
            Row(1, TrainingState.Assigned, null, trainer: 5001),
            Row(2, TrainingState.Scheduled, Day, trainer: 5001),
            Row(3, TrainingState.Completed, Day, trainer: 5001),       // reported: over
            Row(4, TrainingState.Closed, null, trainer: 5001),         // closed: over
            Row(5, TrainingState.Assigned, null, trainer: 5002),       // another trainer's
            Row(6, TrainingState.Accepted, null, trainer: null),       // nobody's yet
        ];

        Assert.Equal([1L, 2L], TrainerQueue.Theirs(all.AsQueryable(), 5001).Select(training => training.Id).Order());
    }

    [Fact]
    public void TheDaysOfAWaitAreWholeDays()
    {
        Assert.Equal(3, TrainerQueue.DaysSince(Day.AddDays(-3).AddHours(-1), Day));
        Assert.Equal(2, TrainerQueue.DaysSince(Day.AddDays(-3).AddHours(1), Day));
        Assert.Equal(0, TrainerQueue.DaysSince(Day.AddHours(-23), Day));
    }

    private static Training Row(long id, TrainingState state, DateTime? start, int? trainer = 5001, DateTime? assignedAt = null) => new()
    {
        Id = id,
        Kind = RatingKind.Atc,
        Rating = 12,
        Position = "TRNTEST_BOX",
        TraineeVid = 1001,
        TrainerVid = trainer,
        AssignedAt = assignedAt,
        State = state,
        ScheduledStartUtc = start,
        OwnerDepartment = Department.TD,
        RatingShortName = "A2",
    };

    private static TrainingSlot Slot(long trainingId, DateTime proposed, DateTime starts) =>
        new() { TrainingId = trainingId, StartsAtUtc = starts, EndsAtUtc = starts.AddHours(2), CreatedAt = proposed };

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
