using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// How the history of a training reads the rows of the core's audit log (A13b; note <c>2026-09-30-lo-storico-di-un-training</c>): each
/// step by what it changed, the hub's own steps by nobody, and a row it has no words for — emptied by an erasure, an action it does not
/// know, a value it cannot read — still a line with who and when. The rows here are written by hand in the shape the interceptor writes
/// them, the one the preview bench showed on 30 September 2026; that the interceptor still writes that shape is proved through the real
/// host by <c>TrainingStaffTests.History</c> (integration), which is the test that fails when it changes.
/// </summary>
public sealed class TrainingHistoryRulesTests
{
    private const int Trainee = 1001;
    private const int Advisor = 2002;
    private const int Trainer = 3003;
    private const int OtherTrainer = 3004;

    private static readonly DateTime Now = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ARequestTheHubRefusedForTheTheoryIsTwoLinesOfTheSameMoment()
    {
        var lines = TrainingHistory.Read(
        [
            Row(1, Trainee, "created", before: null, after: """{"Id":-9223372036854774805,"State":"Rejected","Rejection":"TheoryNotPassed","TraineeVid":1001,"RejectionReason":null}"""),
        ]);

        Assert.Equal(
            [
                new TrainingHistoryLine(Now.AddMinutes(1), Trainee, TrainingHistoryEvent.Requested),
                new TrainingHistoryLine(Now.AddMinutes(1), null, TrainingHistoryEvent.RejectedForTheory),
            ],
            lines);
    }

    [Fact]
    public void EveryEndOfATrainingIsReadWithWhoAndWhy()
    {
        var lines = TrainingHistory.Read(
        [
            Row(1, Advisor, "updated", """{"DecidedAt":null,"DecidedBy":null,"State":"Requested"}""", """{"DecidedAt":"2026-09-30T10:01:00Z","DecidedBy":2002,"Rejection":"Staff","RejectionReason":"More hours first.","State":"Rejected"}"""),
            Row(2, Trainee, "updated", """{"ClosedAt":null,"ClosedBy":null,"OpenKind":"Atc","State":"Requested"}""", """{"ClosedAt":"2026-09-30T10:02:00Z","ClosedBy":1001,"OpenKind":null,"State":"Cancelled"}"""),
            Row(3, Trainer, "updated", """{"ClosedAt":null,"ClosedBy":null,"OpenKind":"Pilot","ScheduledStartUtc":"2026-09-30T09:40:41Z","State":"Scheduled"}""", """{"ClosedAt":"2026-09-30T10:03:00Z","ClosedBy":3003,"OpenKind":null,"ScheduledStartUtc":null,"State":"NoShow"}"""),
            Row(4, Trainer, "updated", """{"CompletedAt":null,"GeneralComment":null,"OpenKind":"Atc","StaffComment":null,"State":"Scheduled"}""", """{"CompletedAt":"2026-09-30T10:04:00Z","GeneralComment":"A good session.","OpenKind":null,"StaffComment":"For the staff.","State":"Completed"}"""),
            Row(5, Advisor, "updated", """{"CloseReason":null,"ClosedAt":null,"ClosedBy":null,"OpenKind":"Atc","State":"Assigned"}""", """{"CloseReason":"No answer to the dates.","ClosedAt":"2026-09-30T10:05:00Z","ClosedBy":2002,"OpenKind":null,"State":"Closed"}"""),

            // The night's closing: nobody signed in wrote it, nobody closed it, and no reason was written.
            Row(6, 0, "updated", """{"ClosedAt":null,"OpenKind":"Atc","State":"Assigned"}""", """{"ClosedAt":"2026-09-30T10:06:00Z","OpenKind":null,"State":"Closed"}"""),
        ]);

        Assert.Equal(
            [
                new TrainingHistoryLine(Now.AddMinutes(1), Advisor, TrainingHistoryEvent.Rejected, Reason: "More hours first."),
                new TrainingHistoryLine(Now.AddMinutes(2), Trainee, TrainingHistoryEvent.Cancelled),
                new TrainingHistoryLine(Now.AddMinutes(3), Trainer, TrainingHistoryEvent.NoShow, Date: new DateTime(2026, 9, 30, 9, 40, 41, DateTimeKind.Utc)),
                new TrainingHistoryLine(Now.AddMinutes(4), Trainer, TrainingHistoryEvent.Completed),
                new TrainingHistoryLine(Now.AddMinutes(5), Advisor, TrainingHistoryEvent.Closed, Reason: "No answer to the dates."),
                new TrainingHistoryLine(Now.AddMinutes(6), null, TrainingHistoryEvent.Closed),
            ],
            lines);

        // What the report says stays in the report.
        Assert.DoesNotContain(lines, line => line.Reason is "A good session." or "For the staff.");
    }

    [Fact]
    public void ATrainerAndADateAreReadAsTheyWereAndAsTheyBecame()
    {
        var lines = TrainingHistory.Read(
        [
            // Given, then to another trainer — who is, since, a deleted person: the erasure wrote the pseudonym into the row.
            Row(1, Advisor, "updated", """{"AssignedAt":null,"AssignedBy":null,"State":"Accepted","TrainerVid":null}""", """{"AssignedAt":"2026-09-30T10:01:00Z","AssignedBy":2002,"State":"Assigned","TrainerVid":3003}"""),
            Row(2, Advisor, "updated", """{"AssignedAt":"2026-09-30T10:01:00Z","AssignedBy":2002,"TrainerVid":3003}""", """{"AssignedAt":"2026-09-30T10:02:00Z","AssignedBy":2002,"TrainerVid":-7}"""),

            // The dates proposed: the training touched, and nothing but its stamp changed — with who changed it too, should the log
            // ever say so.
            Row(3, Trainer, "updated", """{"UpdatedAt":"2026-09-30T10:02:00Z"}""", """{"UpdatedAt":"2026-09-30T10:03:00Z"}"""),
            Row(4, Trainer, "updated", """{"UpdatedAt":"2026-09-30T10:03:00Z","UpdatedBy":2002}""", """{"UpdatedAt":"2026-09-30T10:04:00Z","UpdatedBy":3003}"""),

            // Chosen by the trainee, moved by hand, the session rescheduled, and dated by hand again.
            Row(5, Trainee, "updated", """{"ChosenSlotId":null,"ScheduledStartUtc":null,"State":"Assigned"}""", """{"ChosenSlotId":27,"ScheduledStartUtc":"2026-10-08T13:00:00Z","State":"Scheduled"}"""),
            Row(6, Trainer, "updated", """{"ChosenSlotId":27,"ScheduledStartUtc":"2026-10-08T13:00:00Z"}""", """{"ChosenSlotId":null,"ScheduledStartUtc":"2026-10-09T18:30:00Z"}"""),
            Row(7, Trainer, "updated", """{"ScheduledStartUtc":"2026-10-09T18:30:00Z","State":"Scheduled"}""", """{"ScheduledStartUtc":null,"State":"Assigned"}"""),
            Row(8, OtherTrainer, "updated", """{"ScheduledStartUtc":null,"State":"Assigned"}""", """{"ScheduledStartUtc":"2026-10-12T17:00:00Z","State":"Scheduled"}"""),
        ]);

        Assert.Equal(
            [
                new TrainingHistoryLine(Now.AddMinutes(1), Advisor, TrainingHistoryEvent.Assigned, TrainerVid: Trainer),
                new TrainingHistoryLine(Now.AddMinutes(2), Advisor, TrainingHistoryEvent.TrainerChanged, TrainerVid: -7, PreviousTrainerVid: Trainer),
                new TrainingHistoryLine(Now.AddMinutes(3), Trainer, TrainingHistoryEvent.DatesChanged),
                new TrainingHistoryLine(Now.AddMinutes(4), Trainer, TrainingHistoryEvent.DatesChanged),
                new TrainingHistoryLine(Now.AddMinutes(5), Trainee, TrainingHistoryEvent.DateChosen, Date: new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc)),
                new TrainingHistoryLine(
                    Now.AddMinutes(6),
                    Trainer,
                    TrainingHistoryEvent.DateMoved,
                    Date: new DateTime(2026, 10, 9, 18, 30, 0, DateTimeKind.Utc),
                    PreviousDate: new DateTime(2026, 10, 8, 13, 0, 0, DateTimeKind.Utc)),
                new TrainingHistoryLine(Now.AddMinutes(7), Trainer, TrainingHistoryEvent.Rescheduled, Date: new DateTime(2026, 10, 9, 18, 30, 0, DateTimeKind.Utc)),
                new TrainingHistoryLine(Now.AddMinutes(8), OtherTrainer, TrainingHistoryEvent.DateSet, Date: new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc)),
            ],
            lines);

        Assert.All(lines.Where(line => line.Date is not null), line => Assert.Equal(DateTimeKind.Utc, line.Date!.Value.Kind));
    }

    [Fact]
    public void ATrainingAnErasureEmptiedSaysWhatHappenedAndWhenWithoutWhat()
    {
        // The rows of a finished training of an erased trainee (A12b): every earlier row emptied, the trainee's own by their pseudonym,
        // and the erasure's row with nothing in it.
        var lines = TrainingHistory.Read(
        [
            Row(1, -3, "created", before: null, after: null),
            Row(2, Advisor, "updated", before: null, after: null),
            Row(3, 999, "erased", before: null, after: null),
        ]);

        Assert.Equal(
            [
                new TrainingHistoryLine(Now.AddMinutes(1), -3, TrainingHistoryEvent.Requested),
                new TrainingHistoryLine(Now.AddMinutes(2), Advisor, TrainingHistoryEvent.Changed),
                new TrainingHistoryLine(Now.AddMinutes(3), 999, TrainingHistoryEvent.Erased),
            ],
            lines);
    }

    [Fact]
    public void ARowTheReadingHasNoWordsForIsStillALine()
    {
        var lines = TrainingHistory.Read(
        [
            // An action the reading does not know, a state by number rather than by name, a text that is no object, a change of
            // something the history does not show.
            Row(1, Advisor, "restored", before: null, after: """{"State":"Assigned"}"""),
            Row(2, Advisor, "updated", """{"State":0}""", """{"State":1}"""),
            Row(3, Advisor, "updated", "[]", "[]"),
            Row(4, Advisor, "updated", "not json", "{}"),
            Row(5, Trainer, "updated", """{"ReadyForExam":false}""", """{"ReadyForExam":true}"""),
            Row(6, Advisor, "updated", "{}", "{}"),
        ]);

        Assert.Equal(6, lines.Count);
        Assert.All(lines, line => Assert.Equal(TrainingHistoryEvent.Changed, line.Event));
        Assert.Equal([Advisor, Advisor, Advisor, Advisor, Trainer, Advisor], lines.Select(line => line.ByVid));
    }

    /// <summary>A row of the log about one training, written <paramref name="minutes"/> minutes after <see cref="Now"/>.</summary>
    private static AuditLogEntry Row(int minutes, int vid, string action, string? before, string? after) => new()
    {
        Id = minutes,
        Vid = vid,
        Action = action,
        Entity = "trn_trainings",
        EntityId = "42",
        BeforeJson = before,
        AfterJson = after,
        At = Now.AddMinutes(minutes),
    };
}
