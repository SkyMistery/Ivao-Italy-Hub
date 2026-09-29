using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Sessions;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Sheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>What a step of the staff came to: done, refused field by field, or not the reader's to take.</summary>
public enum StaffResult
{
    Done,
    Refused,
    Forbidden,
}

/// <summary>
/// The staff's side of a training (design M3 §2.3, §2.4, §4.2): reading it with the request and the reminder of the theory,
/// accepting or refusing the request with a reason, and assigning the trainer among the staff of the training who may train it.
/// Every write is a write of the training, which the interceptor lets whoever approves, assigns or conducts make
/// (<c>AlsoWrittenWith</c>, A3); who may do what on a training is the one handler's answer, asked on the row — nobody approves or
/// assigns a training of their own, the super administrator included (§3).
/// <para>Assigning writes the grant that lets the trainer conduct this training alone (§3.3): <c>Training.Conduct</c> with the
/// training's scope, through the core's <see cref="ModuleGrants"/>, and takes the previous trainer's away. The grant and the row
/// are two saves of two contexts: the grant goes first, so a trainer never holds the training without it, and one left behind by
/// a write that failed is taken back here or by the job of the night (<see cref="TrainingExpiryJob"/>).</para>
/// <para>The mails go after the save, through the one notification service (<see cref="TrainingMail"/>).</para>
/// <para>The dates of a training (A8) are <see cref="TrainingDates"/>'s: the page shows them — the dates proposed with their
/// warnings, the session and whether it shows as held — and says whether the reader may conduct it or close it. What its session
/// came to (A9) is <see cref="TrainingSessions"/>'s: the page shows the sessions that are over, the sheet and the report, and says
/// whether the reader may record it — without what is reserved when the reader is the trainee (<see cref="ReservedFields"/>).</para>
/// </summary>
public sealed class StaffTrainings(
    TrainingDbContext database,
    HubDbContext hub,
    RatingVocabulary vocabulary,
    ModuleGrants grants,
    ILogger<StaffTrainings> logger,
    ModuleSettingsStore settingsStore,
    TrainingMail mail,
    TrainingPeople people,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IOptions<DivisionOptions> division,
    IClock clock)
{
    /// <summary>What marks the grants the assignment writes, among the grants of the back office.</summary>
    public const string GrantReason = "training: trainer";

    /// <summary>A training as the staff reaches it, past the filter of the members: the handler says who may read it.</summary>
    public Task<Training?> FindAsync(long id, bool tracked, CancellationToken cancellationToken)
    {
        var trainings = CrudSource.BackOffice<Training>(database);
        return (tracked ? trainings : trainings.AsNoTracking()).FirstOrDefaultAsync(training => training.Id == id, cancellationToken);
    }

    /// <summary>Whether the reader holds <paramref name="permission"/> on this training, as the one handler answers on the row.</summary>
    public async Task<bool> MayAsync(Training training, string permission) =>
        http.HttpContext is { } context
        && (await authorization.AuthorizeAsync(context.User, training, permission)).Succeeded;

    /// <summary>The states a trainer is assigned in, or changed: accepted, and still going on (§2.1).</summary>
    public static bool IsAssignable(TrainingState state) =>
        state is TrainingState.Accepted or TrainingState.Assigned or TrainingState.Scheduled;

    /// <summary>
    /// The page of one training: the request, the decision, the trainer, the dates proposed and the session, the sessions that are
    /// over, the sheet and the report, the closing, and what the reader may do on it now. The one place the staff's answer on a
    /// training is built: every endpoint of the staff answers with it, and it leaves out what is reserved when the reader is the
    /// training's trainee (<see cref="ReservedFields"/>; note <c>le-note-riservate-e-il-trainee</c>).
    /// </summary>
    public async Task<StaffTrainingDto> PageAsync(Training training, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        var slots = await database.Slots.AsNoTracking()
            .Where(slot => slot.TrainingId == training.Id)
            .OrderBy(slot => slot.StartsAtUtc)
            .ThenBy(slot => slot.Id)
            .ToListAsync(cancellationToken);
        var sessions = await database.Sessions.AsNoTracking()
            .Where(session => session.TrainingId == training.Id)
            .OrderBy(session => session.StartsAtUtc)
            .ThenBy(session => session.Id)
            .ToListAsync(cancellationToken);
        var names = await people.NamesAsync(
            [
                training.TraineeVid, training.DecidedBy, training.TrainerVid, training.AssignedBy, training.ClosedBy,
                .. slots.Select(slot => (int?)slot.CreatedBy),
                .. sessions.Select(session => (int?)session.CreatedBy),
            ],
            cancellationToken);
        var settings = await settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);
        var rating = vocabulary.Find(training.Kind, training.Rating);

        var canDecide = training.State == TrainingState.Requested && await MayAsync(training, TrainingPermissions.Approve);
        var canAssign = IsAssignable(training.State) && await MayAsync(training, TrainingPermissions.Assign);
        // A session that has started is recorded: its date is no longer set by hand, nor the training closed (#149).
        var started = TrainingSessions.IsRecordable(training, clock.UtcNow);
        var canConduct = TrainingDates.IsDatable(training.State) && !started && await MayAsync(training, TrainingPermissions.Conduct);
        var canClose = TrainingDates.IsClosable(training.State) && !started && await MayAsync(training, TrainingPermissions.Approve);
        var canRecordOutcome = started && await MayAsync(training, TrainingPermissions.Conduct);

        var page = new StaffTrainingDto(
            training.Id,
            training.Kind,
            training.Rating,
            rating?.ShortName,
            rating?.NameKey,
            training.IsMockExam,
            training.Position,
            training.AirportIcao,
            training.Fir,
            TrainingPeople.Member(training.TraineeVid, names)!,
            vocabulary.Find(training.Kind, training.TraineeRatingAtRequest)?.ShortName,
            training.TraineeHoursAtRequest,
            training.CreatedAt,
            training.TheoryConfirmedAt,
            settings.TheoryExamUrl,
            training.AvailabilityText,
            training.NotesText,
            training.State,
            training.Rejection,
            training.RejectionReason,
            TrainingPeople.Member(training.DecidedBy, names),
            training.DecidedAt,
            TrainingPeople.Member(training.TrainerVid, names),
            TrainingPeople.Member(training.AssignedBy, names),
            training.AssignedAt,
            [
                .. slots.Select(slot => new StaffSlotDto(
                    slot.Id,
                    slot.StartsAtUtc,
                    slot.EndsAtUtc,
                    DateWarnings.Read(slot.WarningsJson),
                    TrainingPeople.Member(slot.CreatedBy, names)!,
                    slot.CreatedAt)),
            ],
            training.ScheduledStartUtc,
            StaffQueue.IsHeld(training, clock.UtcNow, division.Value.ResolveTimeZone()),
            training.ChosenSlotId is not null,
            training.CompletedAt,
            TrainingPeople.Member(training.ClosedBy, names),
            training.ClosedAt,
            training.CloseReason,
            training.ReadyForMockExam,
            training.ReadyForExam,
            training.CooldownWaived,
            training.GeneralComment,
            training.StaffComment,
            await SheetAsync(training, cancellationToken),
            [
                .. sessions.Select(session => new StaffSessionDto(
                    session.Id,
                    session.StartsAtUtc,
                    session.Outcome,
                    session.InternalNotes,
                    TrainingPeople.Member(session.CreatedBy, names)!,
                    session.CreatedAt)),
            ],
            ReservedLeftOut: false,
            new StaffTrainingActionsDto(canDecide, canAssign, canConduct, canClose, canRecordOutcome),
            training.RowVersion);

        return ReservedFields.For(page, currentUser.Vid);
    }

    /// <summary>
    /// The sheet of a training as the staff reads it (§1.4, §2.7): the copy its report keeps, once completed; while it is dated, the
    /// items a report would mark now — the active ones of its ladder and rating, in the order of the sheet —, with nothing marked; none
    /// otherwise.
    /// </summary>
    private async Task<IReadOnlyList<StaffEvaluationDto>> SheetAsync(Training training, CancellationToken cancellationToken)
    {
        if (training.State == TrainingState.Completed)
        {
            var filled = await database.Evaluations.AsNoTracking()
                .Where(evaluation => evaluation.TrainingId == training.Id)
                .OrderBy(evaluation => evaluation.Sort)
                .ThenBy(evaluation => evaluation.Id)
                .ToListAsync(cancellationToken);

            return
            [
                .. filled.Select(evaluation => new StaffEvaluationDto(
                    evaluation.SheetItemId,
                    evaluation.Section,
                    evaluation.Title,
                    evaluation.Grade,
                    evaluation.Mark,
                    evaluation.TraineeComment,
                    evaluation.StaffNote)),
            ];
        }

        if (training.State != TrainingState.Scheduled)
        {
            return [];
        }

        var items = await EvaluationSheet.ItemsOf(CrudSource.BackOffice<SheetItem>(database).AsNoTracking(), training.Kind, training.Rating)
            .ToListAsync(cancellationToken);

        return [.. items.Select(item => new StaffEvaluationDto(item.Id, item.Section, item.Title, null, null, null, null))];
    }

    /// <summary>The rows of one page of the list, with the names of the trainees and of the trainers: one query for the page.</summary>
    public async Task<IReadOnlyList<StaffTrainingRowDto>> RowsAsync(IReadOnlyList<Training> trainings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        var names = await people.NamesAsync(
            [.. trainings.Select(training => (int?)training.TraineeVid), .. trainings.Select(training => training.TrainerVid)],
            cancellationToken);
        var heldBefore = StaffQueue.HeldBefore(clock.UtcNow, division.Value.ResolveTimeZone());

        return [.. trainings.Select(training => Row(training, names, vocabulary, heldBefore))];
    }

    /// <summary>
    /// A row of the list; without the names when the caller has none to give. A dated training whose session started before
    /// <paramref name="heldBefore"/> — today's first moment in the division's time zone — shows as held.
    /// </summary>
    public static StaffTrainingRowDto Row(Training training, IReadOnlyDictionary<int, string> names, RatingVocabulary vocabulary, DateTime heldBefore)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(vocabulary);

        return new StaffTrainingRowDto(
            training.Id,
            training.Kind,
            training.Rating,
            vocabulary.Find(training.Kind, training.Rating)?.ShortName,
            training.IsMockExam,
            training.Position,
            training.State,
            TrainingPeople.Member(training.TraineeVid, names)!,
            TrainingPeople.Member(training.TrainerVid, names),
            training.CreatedAt,
            training.ScheduledStartUtc,
            StaffQueue.IsHeld(training, heldBefore));
    }

    /// <summary>
    /// Whoever may train this training (§2.4), by name: the staff of the training that the hub knows, with the rating, never the
    /// trainee — the rule of <see cref="TrainerChoice"/>, asked of each.
    /// </summary>
    public async Task<IReadOnlyList<TrainerCandidateDto>> CandidatesAsync(Training training, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        var staff = await StaffAsync(training.OwnerDepartment, only: null, cancellationToken);

        return
        [
            .. staff
                .Where(member => TrainerChoice.Refusal(training, member.Facts, vocabulary) is null)
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(member => member.Facts.Vid)
                .Select(member => new TrainerCandidateDto(
                    member.Facts.Vid,
                    member.Name,
                    vocabulary.Find(training.Kind, member.Facts.RatingOf(training.Kind))?.ShortName,
                    member.Positions,
                    member.Facts.Vid == training.TrainerVid)),
        ];
    }

    /// <summary>Accepts a request (§2.3): <c>Accepted</c>, who and when, and the mail to the trainee. Only a request waiting.</summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> AcceptAsync(
        Training training,
        DateTime rowVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        if (!await MayAsync(training, TrainingPermissions.Approve))
        {
            return (StaffResult.Forbidden, null);
        }

        if (training.State != TrainingState.Requested)
        {
            return Refuse("state", "training:errors.trainingNotRequested");
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = rowVersion;
        training.State = TrainingState.Accepted;
        training.DecidedBy = currentUser.Vid;
        training.DecidedAt = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        await mail.SendAsync(TrainingNotifications.RequestAccepted, training.TraineeVid, training, TrainingMail.MinePath, fill: null, cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// Refuses a request (§2.3) with the reason the trainee reads in the mail and on their page: <c>Rejected</c> by the staff, who
    /// and when. Only a request waiting; a refusal keeps nobody waiting (§2.2 point 3).
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> RejectAsync(
        Training training,
        TrainingRejectionDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await MayAsync(training, TrainingPermissions.Approve))
        {
            return (StaffResult.Forbidden, null);
        }

        if (training.State != TrainingState.Requested)
        {
            return Refuse("state", "training:errors.trainingNotRequested");
        }

        var reason = string.IsNullOrWhiteSpace(payload.Reason) ? null : payload.Reason.Trim();
        if (reason is null)
        {
            return Refuse("reason", "errors.required");
        }

        if (reason.Length > Training.MaxTextLength)
        {
            return Refuse("reason", "errors.text.tooLong");
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        training.State = TrainingState.Rejected;
        training.Rejection = TrainingRejection.Staff;
        training.RejectionReason = reason;
        training.DecidedBy = currentUser.Vid;
        training.DecidedAt = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        await mail.SendAsync(
            TrainingNotifications.RequestRejected,
            training.TraineeVid,
            training,
            TrainingMail.MinePath,
            (data, _) => data["reason"] = reason,
            cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// Assigns the trainer, or changes them (§2.4): somebody the rule of <see cref="TrainerChoice"/> lets train it, asked again
    /// here whatever the page offered. Writes their grant on this training and takes the previous trainer's away (§3.3); an
    /// accepted training becomes <c>Assigned</c>, a dated one keeps its date. Every date proposed that the trainee has not chosen
    /// goes (A8), whoever proposed it — the previous trainer, or the coordinator or the assistant, who conduct every training —:
    /// the new trainer proposes their own. Mails the trainee and the trainer.
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> AssignAsync(
        Training training,
        TrainingAssignmentDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await MayAsync(training, TrainingPermissions.Assign))
        {
            return (StaffResult.Forbidden, null);
        }

        if (!IsAssignable(training.State))
        {
            return Refuse("state", "training:errors.trainingNotAssignable");
        }

        if (payload.TrainerVid <= 0)
        {
            return Refuse("trainerVid", "errors.required");
        }

        if (payload.TrainerVid == training.TrainerVid)
        {
            return Refuse("trainerVid", "training:errors.trainerAlready");
        }

        var candidate = (await StaffAsync(training.OwnerDepartment, payload.TrainerVid, cancellationToken)).FirstOrDefault()?.Facts
            ?? TrainerFacts.Unknown(payload.TrainerVid);
        if (TrainerChoice.Refusal(training, candidate, vocabulary) is { } refusal)
        {
            return Refuse("trainerVid", refusal);
        }

        var department = training.OwnerDepartment;
        var scope = Training.ScopeOf(training.Id);
        var previous = training.TrainerVid;

        // The grant first: the trainer never holds the training without it.
        if (await grants.GiveAsync(payload.TrainerVid, TrainingPermissions.Conduct, department, scope, GrantReason, cancellationToken) is { } refused)
        {
            return Refuse("trainerVid", refused);
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        training.TrainerVid = payload.TrainerVid;
        training.AssignedBy = currentUser.Vid;
        training.AssignedAt = clock.UtcNow;
        if (training.State == TrainingState.Accepted)
        {
            training.State = TrainingState.Assigned;
        }

        database.Slots.RemoveRange(await database.Slots.Where(slot => slot.TrainingId == training.Id).ToListAsync(cancellationToken));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody moved the training meanwhile: the conflict is what the reader hears, whatever happens to the grant.
            await TakeBackAfterConflictAsync(training.Id, payload.TrainerVid, department, scope);
            throw;
        }

        if (previous is { } before)
        {
            await grants.TakeAsync(before, TrainingPermissions.Conduct, department, scope, cancellationToken);
        }

        await TellAssignedAsync(training, payload.TrainerVid, cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// After a conflict on an assignment, the grant it gave goes back — unless the training now names this very trainer: two
    /// assignments of the same person from one version, and the other one saved the row. Whichever of the two wrote the grant, it
    /// is the trainer's now, and taking it would leave them the training without it.
    /// <para>Never throws, so the conflict still reaches the reader as a 409. A grant this cannot judge, because the training
    /// could not be read again, stays: the night takes it back an hour after it was written if the training names somebody else
    /// (<see cref="TrainingExpiryJob"/>).</para>
    /// </summary>
    private async Task TakeBackAfterConflictAsync(long id, int trainerVid, Department department, string scope)
    {
        try
        {
            if ((await FindAsync(id, tracked: false, CancellationToken.None))?.TrainerVid != trainerVid)
            {
                await grants.TakeAsync(trainerVid, TrainingPermissions.Conduct, department, scope, CancellationToken.None);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "After a conflict on training {Training}, the grant of {Trainer} is left to the night", id, trainerVid);
        }
    }

    /// <summary>
    /// The mail of a trainer assigned (§5.2): to the trainee, pointing at their trainings, and to the trainer, pointing at the
    /// training's page — each in their language, with the sentence that is theirs.
    /// </summary>
    private async Task TellAssignedAsync(Training training, int trainerVid, CancellationToken cancellationToken)
    {
        var names = await people.NamesAsync([training.TraineeVid, trainerVid], cancellationToken);
        var trainer = TrainingPeople.Label(trainerVid, names);
        var trainee = TrainingPeople.Label(training.TraineeVid, names);

        await mail.SendAsync(
            TrainingNotifications.TrainerAssigned,
            training.TraineeVid,
            training,
            TrainingMail.MinePath,
            (data, locale) => Fill(data, locale, "training:mail.training.assignedTrainee"),
            cancellationToken);

        await mail.SendAsync(
            TrainingNotifications.TrainerAssigned,
            trainerVid,
            training,
            TrainingMail.StaffPath(training.Id),
            (data, locale) => Fill(data, locale, "training:mail.training.assignedTrainer"),
            cancellationToken);

        void Fill(IDictionary<string, string> data, string locale, string next)
        {
            data["trainer"] = trainer;
            data["trainee"] = trainee;
            data["next"] = mail.Word(locale, next);
        }
    }

    /// <summary>
    /// The staff of the training the hub knows (§2.4; plan §16.13, the roster is whoever signed in): whoever holds a position of the
    /// direction or of the module's department — its coordinator, assistant, advisors and trainers —, with those positions and
    /// their ratings. With <paramref name="only"/>, that member alone, if they are.
    /// </summary>
    private async Task<List<StaffMember>> StaffAsync(Department department, int? only, CancellationToken cancellationToken)
    {
        var positions = await hub.UserStaffPositions.AsNoTracking()
            .Where(position => (position.Department == department || position.Department == Department.HQ)
                && (only == null || position.Vid == only))
            .Select(position => new { position.Vid, position.Position })
            .ToListAsync(cancellationToken);

        var held = positions
            .GroupBy(position => position.Vid)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)[.. group.Select(position => position.Position).Order(StringComparer.Ordinal)]);
        var vids = held.Keys.ToList();

        var users = await hub.Users.AsNoTracking()
            .Where(user => vids.Contains(user.Vid))
            .Select(user => new { user.Vid, user.FirstName, user.LastName, user.RatingAtc, user.RatingPilot })
            .ToListAsync(cancellationToken);

        return
        [
            .. users.Select(user => new StaffMember(
                new TrainerFacts(user.Vid, IsTrainingStaff: true, user.RatingAtc, user.RatingPilot),
                $"{user.FirstName} {user.LastName}".Trim(),
                held[user.Vid])),
        ];
    }

    private static (StaffResult, IReadOnlyDictionary<string, string[]>) Refuse(string field, string key) =>
        (StaffResult.Refused, new Refusals().Add(field, key).Errors);

    /// <summary>A member of the staff of the training: what the choice reads, their name, and their positions of that staff.</summary>
    private sealed record StaffMember(TrainerFacts Facts, string Name, IReadOnlyList<string> Positions);
}
