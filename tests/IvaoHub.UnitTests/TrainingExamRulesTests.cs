using System.Reflection;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Public;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The exams of the training on their own (M3, A10c; design M3 §1.5, §5.1; notes <c>2026-09-26-le-righe-affidate-a-chi-scrive</c>
/// §3.6, <c>il-training-in-pubblico</c>): what the catalogue and the exam declare for the rule of the rows assigned to the writer —
/// the three points of the reviewer on #146 —, the one public entry of the calendar that names nobody, the exams still to come, and the
/// position as the directory spells it. The ratings are made up, as every test of the module's own (§10).
/// </summary>
public sealed class TrainingExamRulesTests
{
    private static readonly DateTime Day = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    private static readonly RatingVocabulary Ratings = new(
    [
        new Rating(RatingKind.Atc, 1, "R1", HasPracticalTraining: false, PositionType: null),
        new Rating(RatingKind.Atc, 2, "R2", HasPracticalTraining: true, PositionType: "TWR"),
        new Rating(RatingKind.Atc, 3, "R3", HasPracticalTraining: true, PositionType: "APP"),
        new Rating(RatingKind.Pilot, 1, "P1", HasPracticalTraining: false, PositionType: null),
        new Rating(RatingKind.Pilot, 2, "P2", HasPracticalTraining: true, PositionType: null),
    ]);

    private static readonly PermissionCatalog Catalogue = new([.. CorePermissions.All, .. TrainingPermissions.All]);

    /// <summary>
    /// <c>Training.ManageExams</c> reaches only the exams assigned to whoever holds it, and on any other is worth <c>Training.Edit</c>; it
    /// is denied to nobody, as design M3 §3.1 has it.
    /// </summary>
    [Fact]
    public void ManagingExamsReachesTheExamsAssignedToTheWriter()
    {
        Assert.True(Catalogue.IsOnlyForAssignee(TrainingPermissions.ManageExams));
        Assert.False(Catalogue.IsDeniedToStakeholder(TrainingPermissions.ManageExams));
        Assert.Equal(TrainingPermissions.Edit, Catalogue.EditOf(TrainingPermissions.ManageExams));
    }

    /// <summary>
    /// The exam declares what the rule asks of it (note §3.6, the reviewer's points 1 and 2 on #146): its area, so that the guard falls
    /// back on <c>Training.Edit</c> as the handler does and not on <c>Exams.Edit</c>; the alternative, at creation and at deletion; the
    /// examiner it is assigned to. The check the hub runs when it starts accepts it.
    /// <para>And the point 3: the exam and the catalogue say the same of the member a row is about. The guard keeps whoever a row is about
    /// out of every alternative, the handler only out of a permission denied to them: an exam about its candidate with
    /// <c>ManageExams</c> not denied to them would have the handler say yes and the guard no to the candidate who examines. The exam is
    /// about nobody, and the permission is denied to nobody.</para>
    /// </summary>
    [Fact]
    public void TheExamDeclaresItsAreaItsAlternativeAndItsExaminer()
    {
        Assert.Equal(TrainingPermissions.Area, typeof(Exam).GetCustomAttribute<PermissionAreaAttribute>()?.Area);

        var alternative = Assert.Single(typeof(Exam).GetCustomAttributes<AlsoWrittenWithAttribute>());
        Assert.Equal(
            (TrainingPermissions.ManageExams, true, true),
            (alternative.Permission, alternative.AlsoOnCreation, alternative.AlsoOnDeletion));

        var exam = new Exam { CandidateVid = 790068, ExaminerVid = 790090 };
        Assert.Equal(790090, ((IHasAssignee)exam).AssigneeVid);
        Assert.Equal(
            typeof(IHasStakeholder).IsAssignableFrom(typeof(Exam)),
            Catalogue.IsDeniedToStakeholder(TrainingPermissions.ManageExams));

        Catalogue.VerifyAlternatives([typeof(Exam), typeof(Training)]);
    }

    /// <summary>
    /// An exam is one public entry of the calendar, of its own kind, at its start, titled with its rating and its position and nobody's
    /// VID, pointing at the public page of the training (§5.1; note <c>il-training-in-pubblico</c>); the title follows the rating when it
    /// changes after the exam was read.
    /// </summary>
    [Fact]
    public void AnExamIsOnePublicEntryOfTheCalendarThatNamesNobody()
    {
        var context = new ProjectionContext(["it", "en"], "it", new BlockDocumentWalker(["it", "en"]), new StubClock(Day));
        var exam = new Exam
        {
            Id = 42,
            Kind = RatingKind.Atc,
            Rating = 2,
            Position = "XXAA_TWR",
            StartsAtUtc = Day.AddHours(18),
            CandidateVid = 790068,
            ExaminerVid = 790090,
            OwnerDepartment = Department.TD,
        };
        exam.Knows(Ratings);

        var entry = Assert.Single(exam.Project(context)!.Calendar);
        Assert.Equal(
            (Exam.CalendarKind, Day.AddHours(18), (DateTime?)null, Visibility.Public, Exam.PublicPath, Department.TD),
            (entry.Kind, entry.StartsAtUtc, entry.EndsAtUtc, entry.Visibility, entry.Url, entry.OwnerDepartment));
        Assert.Equal(["R2 · XXAA_TWR", "R2 · XXAA_TWR"], entry.Title.Values);
        Assert.DoesNotContain(entry.Title.Values, title => title.Contains("790", StringComparison.Ordinal));
        Assert.Null(exam.Project(context)!.Search);

        // Read, then moved to another rating: the entry says the rating it has now.
        exam.Rating = 3;
        Assert.Equal("R3 · XXAA_TWR", Assert.Single(exam.Project(context)!.Calendar).Title.Get("en"));

        // Without the vocabulary, the position alone; a pilot's exam, which has none, by its number.
        var unknown = new Exam { Id = 43, Kind = RatingKind.Pilot, Rating = 2, StartsAtUtc = Day };
        Assert.Equal("#43", Assert.Single(unknown.Project(context)!.Calendar).Title.Get("en"));
        unknown.Knows(Ratings);
        Assert.Equal("P2", Assert.Single(unknown.Project(context)!.Calendar).Title.Get("en"));
    }

    /// <summary>The exams still to come are the ones from the start of today in the division's time zone on, as the sessions are.</summary>
    [Fact]
    public void TheExamsStillToComeStartFromTheBeginningOfToday()
    {
        var heldBefore = Day.AddHours(-2);
        Exam[] exams =
        [
            new() { Id = 1, StartsAtUtc = heldBefore.AddMinutes(-1) },
            new() { Id = 2, StartsAtUtc = heldBefore },
            new() { Id = 3, StartsAtUtc = Day.AddDays(3) },
        ];

        Assert.Equal([2L, 3L], PublicSessions.UpcomingExams(exams.AsQueryable(), heldBefore).Select(exam => exam.Id));
    }

    /// <summary>The form's payload onto an exam: the position as the directory spells a callsign, and none when nothing is written.</summary>
    [Fact]
    public void AnExamTakesItsPositionAsTheDirectorySpellsIt()
    {
        var exam = new Exam();

        TrainingExams.Apply(new ExamWriteDto(RatingKind.Atc, 2, " xxaa_twr ", Day, 790068, 790090, default), exam);
        Assert.Equal(("XXAA_TWR", Day, 790068, 790090), (exam.Position, exam.StartsAtUtc, exam.CandidateVid, exam.ExaminerVid));

        TrainingExams.Apply(new ExamWriteDto(RatingKind.Pilot, 2, "  ", Day, 790068, 790090, default), exam);
        Assert.Null(exam.Position);
    }

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
