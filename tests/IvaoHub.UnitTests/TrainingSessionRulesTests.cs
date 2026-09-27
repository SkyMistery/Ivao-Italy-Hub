using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Sessions;
using IvaoHub.Modules.Training.Sheets;
using IvaoHub.Modules.Training.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules after the session (M3, A9; design M3 §1.4, §2.6, §2.7, §5.1) with no database: the sheet a report fills — the active items
/// of the training's ladder and rating in their order, each copied as it was, not applicable where nothing marks it —, what a report is
/// refused for and on which field, when what a session came to may be recorded, what the staff's page leaves out when its reader is the
/// training's trainee (note <c>le-note-riservate-e-il-trainee</c>), and the session held that stays in the calendar.
/// <para>The queries are the ones the database is asked, run here on lists. The ratings are numbers of this test's making.</para>
/// </summary>
public sealed class TrainingSessionRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private const int TraineeVid = 1001;

    [Fact]
    public void TheSheetOfAReportIsTheActiveItemsOfItsLadderAndRatingInTheirOrder()
    {
        SheetItem[] all =
        [
            Item(1, SheetSection.Practice, "taxi", sort: 20),
            Item(2, SheetSection.Theory, "airspace", sort: 10),
            Item(3, SheetSection.Practice, "switched off", sort: 5, active: false),
            Item(4, SheetSection.Practice, "same place, older", sort: 20),
            Item(5, SheetSection.Practice, "another rating", sort: 1, rating: 13),
            Item(6, SheetSection.Practice, "another ladder", sort: 1, kind: RatingKind.Pilot),
        ];

        var sheet = EvaluationSheet.ItemsOf(all.AsQueryable(), RatingKind.Atc, 12).Select(item => item.Id);

        // By place, and two items in the same place by when they were written; the switched off one and the others' left out.
        Assert.Equal([2L, 1L, 4L], sheet);
    }

    [Fact]
    public void AReportFillsACopyOfEachItemAndLeavesTheOnesNothingMarksNotApplicable()
    {
        SheetItem[] items =
        [
            Item(1, SheetSection.Practice, "taxi", sort: 1),
            Item(2, SheetSection.Theory, "airspace", sort: 2),
            Item(3, SheetSection.Practice, "departure", sort: 3),
            Item(4, SheetSection.Theory, "phraseology", sort: 4),
        ];

        // The first graded, the second marked, the third named with nothing marked, the fourth not named at all.
        var (sheet, problems) = EvaluationSheet.Fill(
            items,
            [
                new(2, Grade: null, TheoryMark.ToImprove, "  Revise the sectors.  ", StaffNote: null),
                new(1, Grade: 4, Mark: null, "Good taxi.", "  Slow on the readbacks.  "),
                new(3, Grade: null, Mark: null, TraineeComment: " ", StaffNote: null),
            ]);

        Assert.Null(problems);
        Assert.NotNull(sheet);

        // One per item, in the order of the sheet whatever the order of the entries; a copy of each item.
        Assert.Equal([1L, 2L, 3L, 4L], sheet.Select(evaluation => evaluation.SheetItemId));
        Assert.Equal([1, 2, 3, 4], sheet.Select(evaluation => evaluation.Sort));
        Assert.Equal(
            [SheetSection.Practice, SheetSection.Theory, SheetSection.Practice, SheetSection.Theory],
            sheet.Select(evaluation => evaluation.Section));
        Assert.Equal(["taxi", "airspace", "departure", "phraseology"], sheet.Select(evaluation => evaluation.Title.Get("en")));

        // How the session went, the texts trimmed; an empty one is none.
        Assert.Equal((4, (TheoryMark?)null, "Good taxi.", "Slow on the readbacks."), Marks(sheet[0]));
        Assert.Equal(((int?)null, TheoryMark.ToImprove, "Revise the sectors.", (string?)null), Marks(sheet[1]));

        // Not applicable: named with nothing marked, or not named at all.
        Assert.Equal(((int?)null, (TheoryMark?)null, (string?)null, (string?)null), Marks(sheet[2]));
        Assert.Equal(((int?)null, (TheoryMark?)null, (string?)null, (string?)null), Marks(sheet[3]));

        // A copy: the item changed afterwards leaves the report as it was written.
        items[0].Title = "it: rullaggio".L("en: taxiing");
        items[0].Section = SheetSection.Theory;
        items[0].Sort = 99;
        Assert.Equal(("taxi", SheetSection.Practice, 1), (sheet[0].Title.Get("en"), sheet[0].Section, sheet[0].Sort));

        static (int?, TheoryMark?, string?, string?) Marks(TrainingEvaluation evaluation) =>
            (evaluation.Grade, evaluation.Mark, evaluation.TraineeComment, evaluation.StaffNote);
    }

    [Fact]
    public void AReportIsRefusedOnTheFieldOfTheItemItGetsWrong()
    {
        SheetItem[] items = [Item(1, SheetSection.Practice, "taxi", sort: 1), Item(2, SheetSection.Theory, "airspace", sort: 2)];
        var tooLong = new string('x', Training.MaxTextLength + 1);

        // A grade out of its range, and a mark, on an item of practice; a grade on one of theory; texts too long. Each on its row.
        var (sheet, problems) = EvaluationSheet.Fill(
            items,
            [
                new(1, Grade: 0, TheoryMark.Done, tooLong, StaffNote: null),
                new(2, Grade: 3, Mark: null, TraineeComment: null, tooLong),
            ]);

        Assert.Null(sheet);
        Assert.NotNull(problems);
        Assert.Equal(["errors.number.range"], problems["sheet[0].grade"]);
        Assert.Equal([EvaluationSheet.MarkOnPractice], problems["sheet[0].mark"]);
        Assert.Equal(["errors.text.tooLong"], problems["sheet[0].traineeComment"]);
        Assert.Equal([EvaluationSheet.GradeOnTheory], problems["sheet[1].grade"]);
        Assert.Equal(["errors.text.tooLong"], problems["sheet[1].staffNote"]);

        // Five is the top and one the bottom; six is beyond.
        Assert.Null(EvaluationSheet.Fill(items, [new(1, 5, null, null, null)]).Problems);
        Assert.Null(EvaluationSheet.Fill(items, [new(1, 1, null, null, null)]).Problems);
        Assert.Equal(["errors.number.range"], EvaluationSheet.Fill(items, [new(1, 6, null, null, null)]).Problems!["sheet[0].grade"]);

        // An item that is not on the sheet — switched off meanwhile, or another rating's —, or one named twice: the page reads again.
        Assert.Equal([EvaluationSheet.Changed], EvaluationSheet.Fill(items, [new(3, 4, null, null, null)]).Problems!["sheet"]);
        Assert.Equal(
            [EvaluationSheet.Changed],
            EvaluationSheet.Fill(items, [new(1, 4, null, null, null), new(1, 2, null, null, null)]).Problems!["sheet"]);

        // No item at all: a report of comments and boxes alone.
        var (empty, none) = EvaluationSheet.Fill([], []);
        Assert.Null(none);
        Assert.Empty(empty!);
    }

    [Fact]
    public void WhatASessionCameToIsRecordedOnceTheSessionHasStarted()
    {
        // Before the session, no outcome: it may still be moved. From its start, the three roads — the same evening too.
        Assert.False(TrainingSessions.IsRecordable(Dated(Now.AddMinutes(1)), Now));
        Assert.True(TrainingSessions.IsRecordable(Dated(Now), Now));
        Assert.True(TrainingSessions.IsRecordable(Dated(Now.AddHours(-3)), Now));
        Assert.True(TrainingSessions.IsRecordable(Dated(Now.AddDays(-9)), Now));

        // Only a dated training: waiting for its date, or over, it has no session in hand.
        Assert.False(TrainingSessions.IsRecordable(Dated(null), Now));
        Assert.False(TrainingSessions.IsRecordable(Dated(Now.AddDays(-1), TrainingState.Completed), Now));
        Assert.False(TrainingSessions.IsRecordable(Dated(null, TrainingState.Assigned), Now));
        Assert.False(TrainingSessions.IsRecordable(Dated(null, TrainingState.NoShow), Now));
    }

    [Fact]
    public void TheStaffsPageLeavesOutWhatIsReservedWhenItsReaderIsTheTrainee()
    {
        var page = Page();

        // Anybody else reads it whole.
        var whole = ReservedFields.For(page, readerVid: 2002);
        Assert.Same(page, whole);
        Assert.False(whole.ReservedLeftOut);

        // The trainee reads it without the report's comment for the staff, the notes on the items and those on the sessions.
        var theirs = ReservedFields.For(page, readerVid: TraineeVid);
        Assert.True(theirs.ReservedLeftOut);
        Assert.Null(theirs.StaffComment);
        Assert.All(theirs.Sheet, item => Assert.Null(item.StaffNote));
        Assert.All(theirs.Sessions, session => Assert.Null(session.InternalNotes));

        // What they read anyway stays: the grades and marks, the comments for them, the general comment, the sessions themselves.
        Assert.Equal(page.GeneralComment, theirs.GeneralComment);
        Assert.Equal(page.Sheet.Select(item => (item.ItemId, item.Grade, item.Mark, item.TraineeComment)), theirs.Sheet.Select(item => (item.ItemId, item.Grade, item.Mark, item.TraineeComment)));
        Assert.Equal(page.Sessions.Select(session => (session.Id, session.StartsAtUtc, session.Outcome)), theirs.Sessions.Select(session => (session.Id, session.StartsAtUtc, session.Outcome)));

        // Nothing else of the page changes: the rest of it, compared as a whole with the reserved fields put back.
        Assert.Equal(page, theirs with { StaffComment = page.StaffComment, Sheet = page.Sheet, Sessions = page.Sessions, ReservedLeftOut = false });
    }

    [Fact]
    public void ASessionHeldStaysInTheCalendarAndOneRescheduledOrNotAttendedLeavesIt()
    {
        var context = new ProjectionContext(["it", "en"], "it", new BlockDocumentWalker(["it", "en"]), new StubClock(Now));

        // Reported: the training keeps the date of the session held, and its entry.
        var reported = Dated(Now.AddDays(-1), TrainingState.Completed);
        reported.RatingShortName = "A2";
        var entry = Assert.Single(reported.Project(context)!.Calendar);
        Assert.Equal((Now.AddDays(-1), "A2 · TRNTEST_BOX"), (entry.StartsAtUtc, entry.Title.Get("en")));

        // Rescheduled, it waits for a date again; not attended, it is closed: neither has a session in hand, nor an entry.
        Assert.Null(Dated(null, TrainingState.Assigned).Project(context));
        Assert.Null(Dated(null, TrainingState.NoShow).Project(context));

        // A completed training with no date — written by hand before A9 — has nothing to show.
        Assert.Null(Dated(null, TrainingState.Completed).Project(context));
    }

    private static SheetItem Item(long id, SheetSection section, string title, int sort, bool active = true, int rating = 12, RatingKind kind = RatingKind.Atc) => new()
    {
        Id = id,
        Kind = kind,
        Rating = rating,
        Section = section,
        Title = $"it: {title}".L(title),
        Sort = sort,
        IsActive = active,
    };

    private static Training Dated(DateTime? start, TrainingState state = TrainingState.Scheduled) => new()
    {
        Id = 42,
        Kind = RatingKind.Atc,
        Rating = 12,
        Position = "TRNTEST_BOX",
        TraineeVid = TraineeVid,
        State = state,
        ScheduledStartUtc = start,
        OwnerDepartment = Department.TD,
    };

    /// <summary>A page of a completed training, with something reserved in each place the rule looks.</summary>
    private static StaffTrainingDto Page()
    {
        var trainee = new TrainingMemberDto(TraineeVid, "Test Trainee");
        var trainer = new TrainingMemberDto(2002, "Test Trainer");

        return new StaffTrainingDto(
            42,
            RatingKind.Atc,
            12,
            "A2",
            "ratings.Atc.A2",
            IsMockExam: false,
            "TRNTEST_BOX",
            AirportIcao: null,
            Fir: null,
            trainee,
            TraineeRatingShortName: "A1",
            TraineeHoursAtRequest: 50m,
            RequestedAt: Now.AddDays(-20),
            TheoryConfirmedAt: Now.AddDays(-20),
            TheoryExamUrl: null,
            AvailabilityText: "Evenings.",
            NotesText: null,
            TrainingState.Completed,
            Rejection: null,
            RejectionReason: null,
            DecidedBy: trainer,
            DecidedAt: Now.AddDays(-19),
            Trainer: trainer,
            AssignedBy: trainer,
            AssignedAt: Now.AddDays(-18),
            Slots: [],
            ScheduledStartUtc: Now.AddDays(-1),
            Held: false,
            DateChosenByTrainee: true,
            CompletedAt: Now,
            ClosedBy: null,
            ClosedAt: null,
            CloseReason: null,
            ReadyForMockExam: true,
            ReadyForExam: false,
            CooldownWaived: false,
            GeneralComment: "A good first session.",
            StaffComment: "Needs a second look at the approaches.",
            Sheet:
            [
                new StaffEvaluationDto(1, SheetSection.Practice, "it: taxi".L("taxi"), 4, null, "Good taxi.", "Slow on the readbacks."),
                new StaffEvaluationDto(2, SheetSection.Theory, "it: airspace".L("airspace"), null, TheoryMark.Done, null, "Knew it all."),
            ],
            Sessions:
            [
                new StaffSessionDto(7, Now.AddDays(-8), SessionOutcome.Rescheduled, "Nobody around: only the taxi done.", trainer, Now.AddDays(-8)),
                new StaffSessionDto(8, Now.AddDays(-1), SessionOutcome.Held, null, trainer, Now),
            ],
            ReservedLeftOut: false,
            new StaffTrainingActionsDto(false, false, false, false, false),
            Now);
    }

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
