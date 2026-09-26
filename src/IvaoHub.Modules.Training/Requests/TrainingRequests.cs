using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Requests;

/// <summary>
/// The trainee's side of a training (design M3 §2.2, §4.1): where they stand on each ladder, the request with its checks in the
/// design's order — each refusal under the field it is about —, its cancellation, and their own trainings. What a rule decides
/// is <see cref="RequestRules"/>'s, which this class feeds; whether the theory is passed is <see cref="ITheoryExamSource"/>'s.
/// <para>The trainee's rating and hours are the core's, as the network gave them at the last sign-in (§1.7); a training copies
/// them, and the airport and the FIR of the position chosen (A2). The hub's own refusal of a request — the theory not passed —
/// is recorded and sends no mail: the screen says it (d4).</para>
/// </summary>
public sealed class TrainingRequests(
    TrainingDbContext database,
    HubDbContext hub,
    RatingVocabulary vocabulary,
    IAtcPositionDirectory directory,
    ModuleSettingsStore settingsStore,
    ITheoryExamSource theory,
    TrainingMail mail,
    TrainingPeople people,
    ICurrentUser currentUser,
    IOptions<DivisionOptions> division,
    IClock clock)
{
    /// <summary>The trainee's page: who they are, where they stand on each ladder, and their trainings. None without their row.</summary>
    public async Task<MyTrainingDto?> MineAsync(CancellationToken cancellationToken)
    {
        var trainee = await TraineeAsync(cancellationToken);
        if (trainee is null)
        {
            return null;
        }

        var settings = await settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);
        var (history, bans) = await RecordAsync(trainee.Vid, cancellationToken);
        var now = clock.UtcNow;

        var paths = new List<MyTrainingPathDto>();
        foreach (var kind in Enum.GetValues<RatingKind>())
        {
            var (standing, offered) = await StandingAsync(trainee, kind, history, bans, settings, now, cancellationToken);
            var next = standing.Next;

            paths.Add(new MyTrainingPathDto(
                kind,
                vocabulary.Find(kind, trainee.RatingOf(kind))?.ShortName,
                trainee.HoursOf(kind),
                next is null ? null : new TrainingRatingDto(next.Kind, next.Number, next.ShortName, next.NameKey),
                standing.IsMockExam,
                next?.PositionType is not null,
                next is null ? [] : [.. offered.Select(position => new TrainingPositionDto(position.Callsign, position.Name, next.ShortName))],
                standing.Refusal,
                standing.BannedUntil,
                standing.OpenTrainingId,
                standing.WaitUntil,
                standing.MinimumHours));
        }

        return new MyTrainingDto(
            trainee.Vid,
            trainee.Name,
            theory.AsksTheTrainee,
            settings.TheoryExamUrl,
            paths,
            await DtosAsync([.. history.OrderByDescending(training => training.CreatedAt).ThenByDescending(training => training.Id)], cancellationToken));
    }

    /// <summary>
    /// A request (§2.2): the trainee's standing on the ladder, then the request itself — the rating proposed, a position among
    /// the ones offered, the texts within their bound —, and only when nothing refuses, the theory, whose «no» is recorded as
    /// a training refused by the hub. Returns the training written, or the refusals field by field.
    /// </summary>
    public async Task<(Training? Training, IReadOnlyDictionary<string, string[]>? Problems)> RequestAsync(
        TrainingRequestWriteDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var problems = new Refusals();
        if (!Enum.IsDefined(payload.Kind))
        {
            return (null, problems.Add("kind", "errors.required").Errors);
        }

        var trainee = await TraineeAsync(cancellationToken);
        if (trainee is null)
        {
            return (null, problems.Add("kind", RequestRules.NothingToAsk).Errors);
        }

        var settings = await settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);
        var (history, bans) = await RecordAsync(trainee.Vid, cancellationToken);
        var now = clock.UtcNow;
        var (standing, offered) = await StandingAsync(trainee, payload.Kind, history, bans, settings, now, cancellationToken);

        // The trainee on the ladder: the first rule that refuses, in the design's order.
        if (standing.Refusal is { } refusal)
        {
            problems.Add("kind", refusal);
        }

        // The request: the rating the page proposed, still the one proposed; a position among the ones offered, when the rating
        // is trained on one; the texts within their bound.
        AtcPositionDto? chosen = null;
        if (standing.Next is { } next)
        {
            if (payload.Rating != next.Number)
            {
                problems.Add("rating", "training:errors.requestRatingNotNext");
            }

            var callsign = Trimmed(payload.Position);
            if (next.PositionType is not null)
            {
                chosen = offered.FirstOrDefault(position => string.Equals(position.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
                if (callsign is null)
                {
                    problems.Add("position", "errors.required");
                }
                else if (chosen is null)
                {
                    problems.Add("position", "training:errors.requestPositionUnknown");
                }
            }
            else if (callsign is not null)
            {
                problems.Add("position", "training:errors.requestPositionNotAsked");
            }
        }

        var availability = Trimmed(payload.AvailabilityText);
        var notes = Trimmed(payload.NotesText);
        if (availability?.Length > Training.MaxTextLength)
        {
            problems.Add("availabilityText", "errors.text.tooLong");
        }

        if (notes?.Length > Training.MaxTextLength)
        {
            problems.Add("notesText", "errors.text.tooLong");
        }

        if (!problems.IsEmpty)
        {
            return (null, problems.Errors);
        }

        // The theory, last: a «no» is written down, so it is asked only of a request nothing else refuses.
        var rating = standing.Next!;
        var passed = await theory.HasPassedAsync(trainee.Vid, rating, payload.TheoryPassed, cancellationToken);
        if (passed is null)
        {
            return (null, problems.Add("theoryPassed", "errors.required").Errors);
        }

        var training = new Training
        {
            Kind = payload.Kind,
            Rating = rating.Number,
            IsMockExam = standing.IsMockExam,
            Position = chosen?.Callsign,
            AirportIcao = chosen?.AirportIcao,
            Fir = chosen?.Fir,
            TraineeVid = trainee.Vid,
            TraineeRatingAtRequest = trainee.RatingOf(payload.Kind),
            TraineeHoursAtRequest = trainee.HoursOf(payload.Kind),
            AvailabilityText = availability,
            NotesText = notes,
        };

        if (passed.Value)
        {
            training.State = TrainingState.Requested;
            training.TheoryConfirmedAt = now;
        }
        else
        {
            training.State = TrainingState.Rejected;
            training.Rejection = TrainingRejection.TheoryNotPassed;
            training.DecidedAt = now;
        }

        database.Trainings.Add(training);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException && IsDuplicate(exception))
        {
            // Two requests on one ladder at once: the unique key decides, and the second is told what a moment later it would have been.
            database.ChangeTracker.Clear();
            return (null, new Refusals().Add("kind", RequestRules.Open).Errors);
        }

        if (training.State == TrainingState.Requested)
        {
            await TellTheTraineeAsync(training, cancellationToken);
        }

        return (training, null);
    }

    /// <summary>
    /// A request taken back by its trainee (§2.2, d2): only while nobody accepted it. Written through the one exception of the
    /// write guard for a member's own row; a stale version is the caller's 409.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> CancelAsync(Training training, DateTime rowVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        if (training.State != TrainingState.Requested)
        {
            return new Refusals().Add("state", "training:errors.requestNotCancellable").Errors;
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = rowVersion;
        training.State = TrainingState.Cancelled;
        training.ClosedBy = currentUser.Vid;
        training.ClosedAt = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        return null;
    }

    /// <summary>A training of the caller's own; nobody else's, whatever they hold — the staff's side is A7's.</summary>
    public Task<Training?> OwnAsync(long id, bool tracked, CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var trainings = tracked ? database.Trainings : database.Trainings.AsNoTracking();
        return trainings.FirstOrDefaultAsync(training => training.Id == id && training.TraineeVid == vid, cancellationToken);
    }

    /// <summary>The training as its trainee reads it.</summary>
    public async Task<TraineeTrainingDto> ToDtoAsync(Training training, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        return (await DtosAsync([training], cancellationToken))[0];
    }

    /// <summary>
    /// Trainings as their trainee reads them, in the order given: with who trains each, and the dates still to come that its
    /// trainer proposed while it waits for one (A8) — one query for them all, and one for the names.
    /// </summary>
    public async Task<IReadOnlyList<TraineeTrainingDto>> DtosAsync(IReadOnlyList<Training> trainings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        var now = clock.UtcNow;
        var zone = division.Value.ResolveTimeZone();
        var waiting = trainings.Where(training => training.State == TrainingState.Assigned).Select(training => training.Id).ToList();
        List<TrainingSlot> slots = waiting.Count == 0
            ? []
            : await database.Slots.AsNoTracking()
                .Where(slot => waiting.Contains(slot.TrainingId) && slot.StartsAtUtc > now)
                .OrderBy(slot => slot.StartsAtUtc)
                .ThenBy(slot => slot.Id)
                .ToListAsync(cancellationToken);
        var proposed = slots.ToLookup(slot => slot.TrainingId);
        var names = await people.NamesAsync(trainings.Select(training => training.TrainerVid), cancellationToken);

        return
        [
            .. trainings.Select(training => new TraineeTrainingDto(
                training.Id,
                training.Kind,
                training.Rating,
                vocabulary.Find(training.Kind, training.Rating)?.ShortName,
                training.IsMockExam,
                training.Position,
                training.State,
                training.Rejection,
                training.RejectionReason,
                training.AvailabilityText,
                training.NotesText,
                training.CreatedAt,
                training.DecidedAt,
                TrainingPeople.Member(training.TrainerVid, names),
                [.. proposed[training.Id].Select(slot => new TraineeSlotDto(slot.Id, slot.StartsAtUtc, slot.EndsAtUtc))],
                training.ScheduledStartUtc,
                StaffQueue.IsHeld(training, now, zone),
                training.CompletedAt,
                training.ClosedAt,
                training.CloseReason,
                training.ReadyForMockExam,
                training.ReadyForExam,
                training.RowVersion)),
        ];
    }

    /// <summary>Where the trainee stands on one ladder, and the positions its rating proposed is offered on.</summary>
    private async Task<(PathStanding Standing, IReadOnlyList<AtcPositionDto> Offered)> StandingAsync(
        Trainee trainee,
        RatingKind kind,
        IReadOnlyList<Training> history,
        IReadOnlyList<TraineeBan> bans,
        TrainingSettings settings,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var current = trainee.RatingOf(kind);
        var offered = await OfferedAsync(vocabulary.NextTraining(kind, current), settings, cancellationToken);
        var facts = new PathFacts(kind, current, trainee.HoursOf(kind), history, bans, offered.Count);

        return (RequestRules.Standing(facts, settings, vocabulary, now), offered);
    }

    /// <summary>
    /// The positions a rating is offered on (§2.2): the division's that the core's directory gives it, but the ones the department
    /// does not train on. None for a rating trained on no position, which the directory says without asking the database.
    /// </summary>
    private async Task<IReadOnlyList<AtcPositionDto>> OfferedAsync(Rating? rating, TrainingSettings settings, CancellationToken cancellationToken)
    {
        if (rating is null)
        {
            return [];
        }

        var hidden = settings.HiddenPositions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. (await directory.ForRatingAsync(rating, cancellationToken)).Where(position => !hidden.Contains(position.Callsign))];
    }

    /// <summary>Every training and every ban of the trainee: few, and each rule reads its own among them.</summary>
    private async Task<(List<Training> History, List<TraineeBan> Bans)> RecordAsync(int vid, CancellationToken cancellationToken)
    {
        var history = await database.Trainings.AsNoTracking().Where(training => training.TraineeVid == vid).ToListAsync(cancellationToken);
        var bans = await database.TraineeBans.AsNoTracking().Where(ban => ban.Vid == vid).ToListAsync(cancellationToken);
        return (history, bans);
    }

    /// <summary>The signed in member as the core knows them: their name, and their ratings and hours on both ladders.</summary>
    private async Task<Trainee?> TraineeAsync(CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var user = await hub.Users.AsNoTracking().FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);

        return user is null
            ? null
            : new Trainee(
                user.Vid,
                $"{user.FirstName} {user.LastName}".Trim(),
                user.RatingAtc,
                user.RatingPilot,
                user.HoursAtc,
                user.HoursPilot);
    }

    /// <summary>
    /// The mail of a request received (§5.2), in the trainee's language: what they asked for — the ladder, the rating and the
    /// position —, whether it is a mock exam, and their page.
    /// </summary>
    private Task TellTheTraineeAsync(Training training, CancellationToken cancellationToken) =>
        mail.SendAsync(
            TrainingNotifications.RequestReceived,
            training.TraineeVid,
            training,
            TrainingMail.MinePath,
            (data, locale) => data["mockExam"] = training.IsMockExam ? mail.Word(locale, "training:mail.training.mockExam") : string.Empty,
            cancellationToken);

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>A member as a request reads them.</summary>
    private sealed record Trainee(
        int Vid,
        string Name,
        int? RatingAtc,
        int? RatingPilot,
        decimal? HoursAtc,
        decimal? HoursPilot)
    {
        public int? RatingOf(RatingKind kind) => kind == RatingKind.Pilot ? RatingPilot : RatingAtc;

        public decimal? HoursOf(RatingKind kind) => kind == RatingKind.Pilot ? HoursPilot : HoursAtc;
    }
}
