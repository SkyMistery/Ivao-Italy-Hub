using System.Globalization;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Training.Exams;

/// <summary>
/// An exam in the calendar, <c>trn_exams</c> (design M3 §1.5, §2.8): the exam itself is booked, held and graded on the network, and
/// the hub only puts it in the division's calendar (R.6, d1). Of the candidate and of the examiner it keeps the VID and nothing else —
/// no name, no address, nothing more of either (the training department's request, <c>HANDOFF-M3.md</c>) —; then the ladder, the
/// rating, the position of an exam on one, and when it starts. No outcome, no grade.
/// <para>Written by whoever examines (A3, A3b; notes <c>2026-09-26-gli-esaminatori</c>, <c>2026-09-26-le-righe-affidate-a-chi-scrive</c>
/// §3.6): <c>Training.ManageExams</c> — held by the coordinator, the assistant and the advisors of the training department — enters an
/// exam assigned to the writer, its examiner (<see cref="IHasAssignee"/>), and changes it and takes it off the calendar only while it is
/// theirs; whoever edits the area (<c>Training.Edit</c>: the coordinator, the assistant, the direction) does it on every exam, and
/// chooses the examiner of one they enter for somebody else. The single handler and the write guard ask the same, through the
/// catalogue (<c>OnlyForAssignee</c>), the alternative below and the area this class declares — without which the guard would fall
/// back on <c>Exams.Edit</c>, which nobody holds.</para>
/// <para>It does not say its candidate as the member it is about (no <see cref="IHasStakeholder"/>): design M3 §3.1 does not deny
/// <c>Training.ManageExams</c> to anybody, and an exam is an entry of the calendar, not a decision on the candidate. The candidate
/// who would be the examiner too is refused by the rules of the form, before any permission is asked.</para>
/// <para>It has no state: an exam is public for as long as it is on the calendar. It projects one public entry of the kind
/// <see cref="CalendarKind"/> (§5.1; note <c>il-training-in-pubblico</c>), titled with its rating and its position and nobody's name
/// or VID, pointing at the public page of the training, where the exams still to come are listed (A10c).</para>
/// </summary>
[Audited]
[PermissionArea(TrainingPermissions.Area)]
[AlsoWrittenWith(TrainingPermissions.ManageExams, AlsoOnCreation = true, AlsoOnDeletion = true)]
public sealed class Exam : IOwnedByDepartment, IAuditable, IHasAssignee, IProjectable
{
    /// <summary>What the source of every projection of an exam starts with, in the module's projections.</summary>
    public const string SourcePrefix = "exam:";

    /// <summary>The kind of the calendar an exam is an entry of (§5.1), in the seed of the kinds since A2.</summary>
    public const string CalendarKind = "exam";

    /// <summary>Where the entry of an exam points: the public page of the training, which lists the exams still to come.</summary>
    public const string PublicPath = "/training";

    private RatingVocabulary? _ratings;

    public long Id { get; set; }

    /// <summary>The ladder.</summary>
    public RatingKind Kind { get; set; }

    /// <summary>The rating examined, by the number the hub keeps: one the division trains for, as the core's vocabulary says (§1.7).</summary>
    public int Rating { get; set; }

    /// <summary>The position of an exam on a ladder examined on positions, as the core's directory spells it; none for a pilot's.</summary>
    public string? Position { get; set; }

    /// <summary>When the exam starts.</summary>
    public DateTime StartsAtUtc { get; set; }

    /// <summary>Who is examined: their VID, and nothing else of them.</summary>
    public int CandidateVid { get; set; }

    /// <summary>Who examines, the member the exam is assigned to: their VID, and nothing else of them.</summary>
    public int ExaminerVid { get; set; }

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Who put it in the calendar.</summary>
    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    int? IHasAssignee.AssigneeVid => ExaminerVid;

    /// <summary>
    /// The short name of its rating, as the core's vocabulary says it now — the rating may change after the exam was read —: what its
    /// entry of the calendar is titled with. None from a context that was given no vocabulary. Not a column.
    /// </summary>
    public string? RatingShortName => _ratings?.Find(Kind, Rating)?.ShortName;

    public string SourceModule => TrainingModule.ModuleKey;

    public string SourceId => SourceIdOf(Id);

    /// <summary>The source of the projection of one exam.</summary>
    public static string SourceIdOf(long id) => string.Create(CultureInfo.InvariantCulture, $"{SourcePrefix}{id}");

    /// <summary>
    /// Tells the exam the core's ratings, which its projection is titled with: a projection is worked out by the row itself, which asks
    /// no service, so the context that tracks an exam tells it (<c>TrainingDbContext</c>), as it tells a training.
    /// </summary>
    public void Knows(RatingVocabulary ratings) => _ratings = ratings;

    /// <summary>
    /// The exam in the calendar (§5.1; note <c>il-training-in-pubblico</c>): one public entry at its start, titled with its rating and its
    /// position — never a name nor a VID —, pointing at <see cref="PublicPath"/>. Taking the exam off the hub takes the entry away. Nothing
    /// in the search (§5.1).
    /// </summary>
    public ProjectionSnapshot? Project(ProjectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string?[] parts = [RatingShortName, Position];
        var title = string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
        if (title.Length == 0)
        {
            title = string.Create(CultureInfo.InvariantCulture, $"#{Id}");
        }

        return new ProjectionSnapshot(
            Search: null,
            [
                new CalendarProjection(
                    CalendarKind,
                    StartsAtUtc,
                    EndsAtUtc: null,
                    AllDay: false,
                    OwnerDepartment,
                    Visibility.Public,
                    PublicPath,
                    new Localized<string>(context.Locales.Select(locale => KeyValuePair.Create(locale, title))),
                    Description: null),
            ],
            [],
            []);
    }
}
