using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Public;

/// <summary>
/// A session as the site shows it (design M3 §4.1, §4.3; note <c>il-training-in-pubblico</c>): the position, the rating and when —
/// in UTC, as every moment the hub keeps —, whether it is over, and, to a signed in reader only, who: the trainee and the trainer, by
/// VID and by the name the hub has. To a visitor <c>Trainee</c> and <c>Trainer</c> are none, whatever the row holds.
/// </summary>
/// <param name="Id">The training, whose page is <c>/training/sessions/{id}</c>: the address of its entry of the calendar.</param>
/// <param name="Kind">The ladder.</param>
/// <param name="RatingShortName">The rating trained for, as the core's vocabulary names it.</param>
/// <param name="Position">The position, on a ladder trained on positions; none for a pilot's training.</param>
/// <param name="StartsAtUtc">When the session starts.</param>
/// <param name="Held">
/// Over: its day is over in the division's time zone (§1.2), or its report is published. The report itself is never public.
/// </param>
/// <param name="Trainee">Who the session is for, to a signed in reader only.</param>
/// <param name="Trainer">Who conducts it, to a signed in reader only.</param>
public sealed record PublicSessionDto(
    long Id,
    RatingKind Kind,
    string? RatingShortName,
    string? Position,
    DateTime StartsAtUtc,
    bool Held,
    TrainingMemberDto? Trainee,
    TrainingMemberDto? Trainer);

/// <summary>
/// An exam as the site shows it (design M3 §4.1, §4.3; note <c>il-training-in-pubblico</c>): the position, the rating and when, as its
/// entry of the calendar says them, and, to a signed in reader only, the candidate and the examiner — by VID and nothing else, because
/// of an exam the hub keeps nothing else of either (the training department's request, <c>HANDOFF-M3.md</c>). To a visitor
/// <c>CandidateVid</c> and <c>ExaminerVid</c> are none, whatever the row holds.
/// </summary>
/// <param name="Id">The exam.</param>
/// <param name="Kind">The ladder.</param>
/// <param name="RatingShortName">The rating examined, as the core's vocabulary names it.</param>
/// <param name="Position">The position, on a ladder examined on positions; none for a pilot's exam.</param>
/// <param name="StartsAtUtc">When the exam starts.</param>
/// <param name="CandidateVid">Who is examined, to a signed in reader only.</param>
/// <param name="ExaminerVid">Who examines, to a signed in reader only.</param>
public sealed record PublicExamDto(
    long Id,
    RatingKind Kind,
    string? RatingShortName,
    string? Position,
    DateTime StartsAtUtc,
    int? CandidateVid,
    int? ExaminerVid);

/// <summary>
/// The sessions of the trainings as the site shows them (design M3 §4.1, §4.3; note <c>il-training-in-pubblico</c>): the ones still
/// to be held, for <c>/training</c> and for the block <c>training.upcomingSessions</c>, and one by its address, for the page every
/// entry of the calendar points at; and the exams still to come beside them (A10c). Composed once here and read by the endpoints and
/// by the block, as the tours' public side is.
/// <para>A training is read by members only (<c>IVisible</c>), so a visitor passes the filter here, through the back office's source:
/// what leaves is its public session and nothing else — only a training <see cref="Training.SessionIsPublic"/> says has one, the rule
/// the calendar reads, and of it only what the calendar shows already, the rating, the position and when. The people are the hub's
/// answer to a signed in reader alone (§12 n.4): for a visitor their names are not even asked.</para>
/// </summary>
public sealed class PublicSessions(
    TrainingDbContext database,
    RatingVocabulary vocabulary,
    TrainingPeople people,
    ICurrentUser currentUser,
    IOptions<DivisionOptions> division,
    IClock clock)
{
    /// <summary>Never more than this, whatever a block asks for: a list of sessions is not an export.</summary>
    public const int MaxItems = 50;

    /// <summary>
    /// The sessions still to be held (§4.3): dated, and on a day that is not over in the division's time zone — today's too, begun or
    /// not, until it shows as held (§1.2) —, given the moment today began there (<see cref="StaffQueue.HeldBefore"/>).
    /// </summary>
    public static IQueryable<Training> Upcoming(IQueryable<Training> trainings, DateTime heldBefore)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        return trainings
            .Where(Training.SessionIsPublic)
            .Where(training => training.State == TrainingState.Scheduled && training.ScheduledStartUtc >= heldBefore);
    }

    /// <summary>The sessions still to be held, the soonest first: at most <paramref name="limit"/>, and never more than <see cref="MaxItems"/>.</summary>
    public async Task<IReadOnlyList<PublicSessionDto>> UpcomingAsync(int? limit, CancellationToken cancellationToken)
    {
        var heldBefore = HeldBefore();
        var trainings = await Upcoming(CrudSource.BackOffice<Training>(database).AsNoTracking(), heldBefore)
            .OrderBy(training => training.ScheduledStartUtc)
            .ThenBy(training => training.Id)
            .Take(limit is { } most && most > 0 ? Math.Min(most, MaxItems) : MaxItems)
            .ToListAsync(cancellationToken);

        return await DtosAsync(trainings, heldBefore, cancellationToken);
    }

    /// <summary>One session by its training; none when the training has no public session — not dated, or never held.</summary>
    public async Task<PublicSessionDto?> OneAsync(long id, CancellationToken cancellationToken)
    {
        var training = await CrudSource.BackOffice<Training>(database).AsNoTracking()
            .Where(Training.SessionIsPublic)
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

        return training is null ? null : (await DtosAsync([training], HeldBefore(), cancellationToken))[0];
    }

    /// <summary>
    /// The exams still to come (§4.3, A10c): on a day that is not over in the division's time zone, like the sessions. An exam has no
    /// state and every exam is in the calendar, so this is the whole of the rule — what a visitor reads of it is the same rating, position
    /// and time its entry of the calendar shows (<see cref="Exam.Project"/>).
    /// </summary>
    public static IQueryable<Exam> UpcomingExams(IQueryable<Exam> exams, DateTime heldBefore)
    {
        ArgumentNullException.ThrowIfNull(exams);

        return exams.Where(exam => exam.StartsAtUtc >= heldBefore);
    }

    /// <summary>
    /// The exams still to come, the soonest first: at most <paramref name="limit"/>, and never more than <see cref="MaxItems"/>. The
    /// candidate and the examiner by VID to a signed in reader, and to a visitor nothing of either.
    /// </summary>
    public async Task<IReadOnlyList<PublicExamDto>> UpcomingExamsAsync(int? limit, CancellationToken cancellationToken)
    {
        var exams = await UpcomingExams(database.Exams.AsNoTracking(), HeldBefore())
            .OrderBy(exam => exam.StartsAtUtc)
            .ThenBy(exam => exam.Id)
            .Take(limit is { } most && most > 0 ? Math.Min(most, MaxItems) : MaxItems)
            .ToListAsync(cancellationToken);

        var signedIn = currentUser.IsAuthenticated;
        return
        [
            .. exams.Select(exam => new PublicExamDto(
                exam.Id,
                exam.Kind,
                vocabulary.Find(exam.Kind, exam.Rating)?.ShortName,
                exam.Position,
                exam.StartsAtUtc,
                signedIn ? exam.CandidateVid : null,
                signedIn ? exam.ExaminerVid : null)),
        ];
    }

    private async Task<IReadOnlyList<PublicSessionDto>> DtosAsync(
        IReadOnlyList<Training> trainings,
        DateTime heldBefore,
        CancellationToken cancellationToken)
    {
        var names = currentUser.IsAuthenticated
            ? await people.NamesAsync([.. trainings.Select(training => (int?)training.TraineeVid), .. trainings.Select(training => training.TrainerVid)], cancellationToken)
            : null;

        return
        [
            .. trainings.Select(training => new PublicSessionDto(
                training.Id,
                training.Kind,
                vocabulary.Find(training.Kind, training.Rating)?.ShortName,
                training.Position,
                training.ScheduledStartUtc!.Value,
                training.State == TrainingState.Completed || StaffQueue.IsHeld(training, heldBefore),
                names is null ? null : TrainingPeople.Member(training.TraineeVid, names),
                names is null ? null : TrainingPeople.Member(training.TrainerVid, names))),
        ];
    }

    private DateTime HeldBefore() => StaffQueue.HeldBefore(clock.UtcNow, division.Value.ResolveTimeZone());
}

/// <summary>
/// The reads of the site (design M3 §4.1): the sessions still to be held and the exams still to come, for <c>/training</c>, and one
/// session, for <c>/training/sessions/{id}</c>. Anonymous, and answered for whoever asks: the people only to a signed in reader.
/// </summary>
public static class PublicSessionEndpoints
{
    public const string Pattern = "/api/training/sessions";

    /// <summary>The exams still to come (A10c): beside the sessions, and never taken for one — a session's address is a number.</summary>
    public const string ExamsPattern = Pattern + "/exams";

    public static IEndpointRouteBuilder MapPublicSessionEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var sessions = app.MapGroup(Pattern).WithTags("TrainingSessions");

        sessions.MapGet("/", async (PublicSessions publicSessions, HttpContext http) =>
                Results.Ok(await publicSessions.UpcomingAsync(limit: null, http.RequestAborted)))
            .WithName("TrainingUpcomingSessions")
            .Produces<IReadOnlyList<PublicSessionDto>>()
            .AllowAnonymous();

        sessions.MapGet("/{id:long}", async (long id, PublicSessions publicSessions, HttpContext http) =>
                await publicSessions.OneAsync(id, http.RequestAborted) is { } session ? Results.Ok(session) : Results.NotFound())
            .WithName("TrainingPublicSession")
            .Produces<PublicSessionDto>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        sessions.MapGet("/exams", async (PublicSessions publicSessions, HttpContext http) =>
                Results.Ok(await publicSessions.UpcomingExamsAsync(limit: null, http.RequestAborted)))
            .WithName("TrainingUpcomingExams")
            .Produces<IReadOnlyList<PublicExamDto>>()
            .AllowAnonymous();

        return app;
    }
}
