using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Exams;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training;

/// <summary>
/// The training's half of erasing a person's data (design M3 §6.1, A12b; notes 2026-09-25-la-cancellazione-dei-dati-di-un-trainee,
/// 2026-09-25-la-cancellazione-dei-dati-di-una-persona and 2026-09-25-le-righe-che-restano-con-il-vid). The register of the trainings
/// stays, without the trainee; the rest about them goes.
/// <list type="bullet">
/// <item>A training of theirs that is over — completed, not attended, closed, refused, cancelled — is the register (§6): it stays with
/// its states, dates, ratings, grades and boxes, so the counts of the training department stay the same, and loses every text written
/// about them — the two of the request, the reason of a refusal and of a closing, the report's two comments, the notes of its sessions,
/// the comments and notes of its sheet. Nobody closed the training the hub closed (<see cref="Training.ClosedBy"/>): with its reason
/// gone, a closing of the staff still says who.</item>
/// <item>A training of theirs still open — asked for, accepted, assigned, dated — goes: it does not go on without them, like a report
/// of the tours not decided. Through the change tracker, so that its entry of the calendar goes in the same save; its dates, sessions
/// and sheet go with it.</item>
/// <item>An exam where they are the candidate goes, with its entry of the calendar: the exam itself is the network's.</item>
/// <item>A ban that holds stays as it is, VID and reason (Carmine: a ban without the VID protects nobody); an erasure run again after it
/// is over or lifted takes it. A ban over or lifted loses its reason.</item>
/// </list>
/// What they did as staff or as a trainer — trainings decided, assigned, conducted, reported or closed, sessions recorded, dates
/// proposed, exams entered or held, bans given or lifted — stays, and so do their words, which are about others (answer 4 of the note
/// of T20b). Nothing here writes a VID: the core writes the pseudonym into every <c>vid</c>/<c>*_vid</c>/<c>*_by</c> column
/// afterwards, the rows kept apart.
/// </summary>
public sealed class TrainingPersonalData(TrainingDbContext database, IClock clock) : IPersonalDataEraser
{
    public string ModuleKey => TrainingModule.ModuleKey;

    public async Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default)
    {
        var states = await Trainings(vid).AsNoTracking().Select(training => training.State).ToListAsync(cancellationToken);
        var bans = await Bans(vid).AsNoTracking().ToListAsync(cancellationToken);
        var now = clock.UtcNow;

        return Lines(
            closed: states.Count(state => !Training.IsOpen(state)),
            open: states.Count(Training.IsOpen),
            exams: await Exams(vid).CountAsync(cancellationToken),
            inForce: bans.Count(ban => ban.Holds(now)),
            over: bans.Count(ban => !ban.Holds(now)));
    }

    public async Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vid = request.Vid;
        var now = clock.UtcNow;

        var trainings = await Trainings(vid).AsTracking().ToListAsync(cancellationToken);
        var register = trainings.Where(training => !Training.IsOpen(training.State)).ToList();
        var open = trainings.Where(training => Training.IsOpen(training.State)).ToList();

        // The register: states, dates, ratings, grades and boxes stay; what anybody wrote about the trainee goes.
        foreach (var training in register)
        {
            training.AvailabilityText = null;
            training.NotesText = null;
            training.RejectionReason = null;
            training.CloseReason = null;
            training.GeneralComment = null;
            training.StaffComment = null;
        }

        var kept = register.Select(training => training.Id).ToList();
        foreach (var session in await database.Sessions.AsTracking().Where(session => kept.Contains(session.TrainingId)).ToListAsync(cancellationToken))
        {
            session.InternalNotes = null;
        }

        foreach (var evaluation in await database.Evaluations.AsTracking().Where(evaluation => kept.Contains(evaluation.TrainingId)).ToListAsync(cancellationToken))
        {
            evaluation.TraineeComment = null;
            evaluation.StaffNote = null;
        }

        // Never with a bulk delete: a dated training's entry of the calendar goes only through the interceptor (A8a).
        database.Trainings.RemoveRange(open);

        var exams = await Exams(vid).AsTracking().ToListAsync(cancellationToken);
        database.Exams.RemoveRange(exams);

        var bans = await Bans(vid).AsTracking().ToListAsync(cancellationToken);
        foreach (var ban in bans)
        {
            if (ban.Holds(now))
            {
                request.Keep(ban);
            }
            else
            {
                ban.Reason = string.Empty;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        return Lines(
            closed: register.Count,
            open: open.Count,
            exams: exams.Count,
            inForce: bans.Count(ban => ban.Holds(now)),
            over: bans.Count(ban => !ban.Holds(now)));
    }

    /// <summary>Every training the person asked for, whoever conducts it.</summary>
    private IQueryable<Training> Trainings(int vid) =>
        CrudSource.BackOffice<Training>(database).Where(training => training.TraineeVid == vid);

    /// <summary>The exams where the person is the candidate; the ones they hold stay, as their work.</summary>
    private IQueryable<Exam> Exams(int vid) => CrudSource.BackOffice<Exam>(database).Where(exam => exam.CandidateVid == vid);

    private IQueryable<TraineeBan> Bans(int vid) => CrudSource.BackOffice<TraineeBan>(database).Where(ban => ban.Vid == vid);

    private static IReadOnlyList<ErasureLine> Lines(int closed, int open, int exams, int inForce, int over) =>
    [
        new("training:erasure.closed", closed, ErasureOutcome.Anonymised),
        new("training:erasure.open", open, ErasureOutcome.Deleted),
        new("training:erasure.exams", exams, ErasureOutcome.Deleted),
        new("training:erasure.bansInForce", inForce, ErasureOutcome.Kept),
        new("training:erasure.bansOver", over, ErasureOutcome.Anonymised),
    ];
}
