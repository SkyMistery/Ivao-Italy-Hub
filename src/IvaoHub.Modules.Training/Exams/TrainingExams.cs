using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace IvaoHub.Modules.Training.Exams;

/// <summary>An exam as the form loads it: the ladder and the rating, the position, when, and the candidate and the examiner by VID.</summary>
public sealed record ExamDto(
    long Id,
    RatingKind Kind,
    int Rating,
    string? Position,
    DateTime StartsAtUtc,
    int CandidateVid,
    int ExaminerVid,
    DateTime RowVersion);

/// <summary>
/// An exam as the list of the staff shows it (design M3 §4.2): what, where and when, the candidate and the examiner by VID — and, for
/// whoever reads it, whether it is theirs, assigned to them, and whether they may change it and take it off the calendar, as the one
/// handler answers on the row. An advisor sees which exams are theirs, and is offered no step on anybody else's (note
/// 2026-09-26-le-righe-affidate-a-chi-scrive §3.6).
/// </summary>
/// <param name="Id">The exam.</param>
/// <param name="Kind">The ladder.</param>
/// <param name="Rating">The rating examined, by the number the hub keeps.</param>
/// <param name="RatingShortName">The rating examined, as the core's vocabulary names it.</param>
/// <param name="Position">The position of an exam on a ladder examined on positions.</param>
/// <param name="StartsAtUtc">When the exam starts.</param>
/// <param name="CandidateVid">Who is examined.</param>
/// <param name="ExaminerVid">Who examines, the member the exam is assigned to.</param>
/// <param name="Mine">Whether the reader is the examiner.</param>
/// <param name="MayEdit">Whether the reader may change it and take it off the calendar.</param>
/// <param name="RowVersion">The version.</param>
public sealed record ExamRowDto(
    long Id,
    RatingKind Kind,
    int Rating,
    string? RatingShortName,
    string? Position,
    DateTime StartsAtUtc,
    int CandidateVid,
    int ExaminerVid,
    bool Mine,
    bool MayEdit,
    DateTime RowVersion);

/// <summary>
/// What the staff writes of an exam (design M3 §1.5): the ladder and the rating, the position of an exam on one, when, the candidate
/// and the examiner by VID. Its department is the module's base department, which the payload does not carry.
/// </summary>
public sealed record ExamWriteDto(
    RatingKind Kind,
    int Rating,
    string? Position,
    DateTime? StartsAtUtc,
    int CandidateVid,
    int ExaminerVid,
    DateTime RowVersion);

/// <summary>
/// What the form of an exam chooses from: every rating of the two ladders, as the core's vocabulary has them (the maintainer's answer on
/// #178: the exams go up to the eighth rating, which nobody trains for); the examiners the reader may give an exam to — themselves, for
/// an advisor; every examiner the hub knows, for whoever edits the area — by VID and name; and the positions of the division the
/// ratings are trained on.
/// </summary>
public sealed record ExamChoicesDto(
    IReadOnlyList<TrainingRatingDto> Ratings,
    IReadOnlyList<TrainingMemberDto> Examiners,
    IReadOnlyList<TrainingPositionDto> Positions);

/// <summary>
/// The rules of an exam on its own (design M3 §1.5, §2.8): a rating of its ladder, as the core's vocabulary has it — every one, trained
/// or not, the eighth too (the maintainer's answer on #178) —; for a rating trained on a kind of position, one of the division's for
/// it, and for any other none; when; the candidate; and an examiner who is not the candidate and who puts exams in the calendar — who
/// holds <c>Training.ManageExams</c> on the base department, as a sign in computes it (note <c>2026-09-26-gli-esaminatori</c>). Messages
/// are i18n keys. Whether the reader may give the exam to that examiner is not a rule of the payload: the one handler answers it on the
/// row.
/// </summary>
public sealed class ExamWriteDtoValidator : AbstractValidator<ExamWriteDto>
{
    public ExamWriteDtoValidator(RatingVocabulary vocabulary, IAtcPositionDirectory directory, TrainingExams exams)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(exams);

        RuleFor(exam => exam.Kind).IsInEnum().WithMessage("errors.required");
        RuleFor(exam => exam.Rating)
            .Must((exam, _) => Examined(vocabulary, exam) is not null)
            .WithMessage(TrainingExams.RatingUnknown);

        RuleFor(exam => exam.Position)
            .Cascade(CascadeMode.Stop)
            .Must(position => !string.IsNullOrWhiteSpace(position)).WithMessage("errors.required")
            .MustAsync(async (exam, position, cancellationToken) =>
                (await directory.ForRatingAsync(Examined(vocabulary, exam)!, cancellationToken))
                    .Any(offered => string.Equals(offered.Callsign, position!.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage(TrainingExams.PositionUnknown)
            .When(exam => Examined(vocabulary, exam)?.PositionType is not null);
        RuleFor(exam => exam.Position)
            .Must(string.IsNullOrWhiteSpace).WithMessage(TrainingExams.PositionNotAsked)
            .When(exam => Examined(vocabulary, exam) is { PositionType: null });

        RuleFor(exam => exam.StartsAtUtc).NotNull().WithMessage("errors.required");
        RuleFor(exam => exam.CandidateVid).GreaterThan(0).WithMessage("errors.required");

        RuleFor(exam => exam.ExaminerVid)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("errors.required")
            .Must((exam, examiner) => examiner != exam.CandidateVid).WithMessage(TrainingExams.ExaminerIsCandidate)
            .MustAsync(async (examiner, cancellationToken) => (await exams.ExaminersAsync(cancellationToken)).Contains(examiner))
            .WithMessage(TrainingExams.ExaminerNotExaminer);
    }

    /// <summary>
    /// The rating of the exam, when the vocabulary knows it on the exam's ladder; none otherwise. Which ratings have an exam the vocabulary
    /// does not say, and the module writes no rating of its own: every one of the ladder is taken.
    /// </summary>
    private static Rating? Examined(RatingVocabulary vocabulary, ExamWriteDto exam) => vocabulary.Find(exam.Kind, exam.Rating);
}

/// <summary>
/// The exams of the staff (design M3 §1.5, §2.8, §4.2): who examines, what the form chooses from, and how the list reads each exam for
/// whoever looks at it. Who may write an exam is never decided here: the one handler answers on the row, and the write guard behind it,
/// through the catalogue — <c>Training.ManageExams</c> reaches only the exams assigned to the writer — and the exam's own declaration
/// (<see cref="Exam"/>).
/// </summary>
public sealed class TrainingExams(
    IPermissionHolders holders,
    ModuleRegistry modules,
    TrainingPeople people,
    TrainingReference reference,
    RatingVocabulary vocabulary,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser)
{
    /// <summary>A rating the vocabulary does not know on the exam's ladder.</summary>
    public const string RatingUnknown = "training:errors.examRatingUnknown";

    /// <summary>A position that is not one of the division's for the rating: the same words as a request's.</summary>
    public const string PositionUnknown = "training:errors.requestPositionUnknown";

    /// <summary>A position on an exam of a ladder that is not examined on positions.</summary>
    public const string PositionNotAsked = "training:errors.examPositionNotAsked";

    /// <summary>The examiner is the candidate: nobody examines themselves.</summary>
    public const string ExaminerIsCandidate = "training:errors.examinerIsCandidate";

    /// <summary>The examiner does not put exams in the calendar, or is unknown to the hub.</summary>
    public const string ExaminerNotExaminer = "training:errors.examinerNotExaminer";

    /// <summary>
    /// The examiners the hub knows: whoever holds <c>Training.ManageExams</c> on the base department of the module, as a sign in computes
    /// it — the coordinator, the assistant and the advisors of the training department, the direction, the super administrators
    /// (note <c>2026-09-26-gli-esaminatori</c>; plan §16.13: whoever signed in).
    /// </summary>
    public async Task<IReadOnlyList<int>> ExaminersAsync(CancellationToken cancellationToken)
    {
        var department = BaseDepartment();
        return
        [
            .. (await holders.HoldersOfAsync(TrainingPermissions.ManageExams, cancellationToken))
                .Where(holder => holder.Has(TrainingPermissions.ManageExams, department))
                .Select(holder => holder.Vid),
        ];
    }

    /// <summary>
    /// What the form chooses from (note 2026-09-26-le-righe-affidate-a-chi-scrive §3.6, «how the direction, the coordinator and the
    /// assistant choose the examiner of an exam they enter for somebody else»): every rating of the two ladders; of the examiners, the
    /// ones the one handler lets the reader give an exam to — an advisor only themselves, whoever edits the area every one —, by name; and
    /// the positions.
    /// </summary>
    public async Task<ExamChoicesDto> ChoicesAsync(CancellationToken cancellationToken)
    {
        var department = BaseDepartment();
        var offered = new List<int>();
        foreach (var vid in await ExaminersAsync(cancellationToken))
        {
            if (await MayAsync(new Exam { ExaminerVid = vid, OwnerDepartment = department }, TrainingPermissions.ManageExams))
            {
                offered.Add(vid);
            }
        }

        var names = await people.NamesAsync(offered.Select(vid => (int?)vid), cancellationToken);
        var examiners = offered
            .Select(vid => TrainingPeople.Member(vid, names)!)
            .OrderBy(member => member.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(member => member.Vid)
            .ToList();

        var ratings = Enum.GetValues<RatingKind>()
            .SelectMany(vocabulary.Ladder)
            .Select(rating => new TrainingRatingDto(rating.Kind, rating.Number, rating.ShortName, rating.NameKey))
            .ToList();

        return new ExamChoicesDto(ratings, examiners, await reference.PositionsAsync(cancellationToken));
    }

    /// <summary>The exams of a page of the list, each read for whoever looks: theirs or not, and whether the handler lets them change it.</summary>
    public async Task<IReadOnlyList<ExamRowDto>> RowsAsync(IReadOnlyList<Exam> exams)
    {
        ArgumentNullException.ThrowIfNull(exams);

        var rows = new List<ExamRowDto>(exams.Count);
        foreach (var exam in exams)
        {
            rows.Add(Row(exam, vocabulary, mine: exam.ExaminerVid == currentUser.Vid, await MayAsync(exam, TrainingPermissions.ManageExams)));
        }

        return rows;
    }

    /// <summary>An exam as a row of the list; <paramref name="mine"/> and <paramref name="mayEdit"/> are the reader's.</summary>
    public static ExamRowDto Row(Exam exam, RatingVocabulary vocabulary, bool mine, bool mayEdit)
    {
        ArgumentNullException.ThrowIfNull(exam);
        ArgumentNullException.ThrowIfNull(vocabulary);

        return new ExamRowDto(
            exam.Id,
            exam.Kind,
            exam.Rating,
            vocabulary.Find(exam.Kind, exam.Rating)?.ShortName,
            exam.Position,
            exam.StartsAtUtc,
            exam.CandidateVid,
            exam.ExaminerVid,
            mine,
            mayEdit,
            exam.RowVersion);
    }

    /// <summary>An exam as the form loads it.</summary>
    public static ExamDto Detail(Exam exam)
    {
        ArgumentNullException.ThrowIfNull(exam);

        return new ExamDto(exam.Id, exam.Kind, exam.Rating, exam.Position, exam.StartsAtUtc, exam.CandidateVid, exam.ExaminerVid, exam.RowVersion);
    }

    /// <summary>
    /// Everything the payload says, and the version the caller edited, so a stale form is answered 409. The position as the directory
    /// spells a callsign, and none when nothing is written.
    /// </summary>
    public static void Apply(ExamWriteDto payload, Exam exam)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(exam);

        exam.Kind = payload.Kind;
        exam.Rating = payload.Rating;
        exam.Position = string.IsNullOrWhiteSpace(payload.Position) ? null : payload.Position.Trim().ToUpperInvariant();
        exam.StartsAtUtc = payload.StartsAtUtc ?? default;
        exam.CandidateVid = payload.CandidateVid;
        exam.ExaminerVid = payload.ExaminerVid;
        exam.RowVersion = payload.RowVersion;
    }

    private Department BaseDepartment() => modules.BaseDepartmentOf(typeof(TrainingDbContext)) ?? default;

    /// <summary>Whether the reader holds <paramref name="permission"/> on this exam, as the one handler answers on the row.</summary>
    private async Task<bool> MayAsync(Exam exam, string permission) =>
        http.HttpContext is { } context
        && (await authorization.AuthorizeAsync(context.User, exam, permission)).Succeeded;
}
