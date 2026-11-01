using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Bans;

/// <summary>
/// A ban as the list, the form and the trainee's path read it (design M3 §2.9, §4.2): who, why, since when — it holds from the moment it
/// was given — and until when, who gave it, who lifted it and when, and whether it holds now. Never an address.
/// </summary>
/// <param name="Id">The ban.</param>
/// <param name="Trainee">The member banned.</param>
/// <param name="Reason">Why, as the mail to the member says it.</param>
/// <param name="CreatedAt">When it was given: it holds from then.</param>
/// <param name="GivenBy">Who gave it.</param>
/// <param name="EndsAt">Until when; none, until somebody lifts it.</param>
/// <param name="LiftedBy">Who lifted it before its end; none while nobody did.</param>
/// <param name="LiftedAt">When it was lifted.</param>
/// <param name="Holds">Whether it holds now: given, not over, not lifted.</param>
/// <param name="RowVersion">The version, for «lift the ban».</param>
public sealed record TraineeBanDto(
    long Id,
    TrainingMemberDto Trainee,
    string Reason,
    DateTime CreatedAt,
    TrainingMemberDto? GivenBy,
    DateTime? EndsAt,
    TrainingMemberDto? LiftedBy,
    DateTime? LiftedAt,
    bool Holds,
    DateTime RowVersion);

/// <summary>What the staff writes to ban a member (§2.9): who, why, and until when — none, until somebody lifts it.</summary>
public sealed record TraineeBanWriteDto(int Vid, string? Reason, DateTime? EndsAt);

/// <summary>The version of the ban the reader saw when they pressed «lift the ban».</summary>
public sealed record TraineeBanLiftDto(DateTime RowVersion);

/// <summary>The rules of a ban on its own: whom, why — within the bound of a reason of the staff —, and an end still to come.</summary>
public sealed class TraineeBanWriteDtoValidator : AbstractValidator<TraineeBanWriteDto>
{
    public TraineeBanWriteDtoValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(ban => ban.Vid).GreaterThan(0).WithMessage("errors.required");
        RuleFor(ban => ban.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("errors.required")
            .MaximumLength(Training.MaxTextLength).WithMessage("errors.text.tooLong");
        RuleFor(ban => ban.EndsAt)
            .Must(end => end is null || end > clock.UtcNow)
            .WithMessage(TrainingBans.EndsInThePast);
    }
}

/// <summary>
/// The bans of the trainees (design M3 §2.9): while one holds the member asks for no training, on either ladder (the request reads
/// them, A6a). With <c>Training.Ban</c> — the coordinator and the assistant; the direction and the web for the core — a ban is given
/// with a reason and, if one wants, an end, from the trainee's path or from the list; the member is told by mail. «Lift the ban»
/// records who and when. A ban is never changed nor deleted: it stays in the member's history. Nobody bans themselves nor lifts a ban
/// of their own, the super administrator included: <c>Training.Ban</c> is denied to whoever the ban is about, and the one handler says
/// so on the row; the write guard asks the same of the ban and of the trainings it closes (<c>AlsoWrittenWith</c>).
/// <para><b>A ban closes what is open</b> (issue #312, Carmine's answer; note <c>2026-11-01-il-ban-chiude-cio-che-e-aperto</c>): giving it
/// closes every training of the member still open — a request waiting, one accepted, one with its trainer, one dated —, in the ban's
/// own save, by whoever gives it, with no reason of its own (<see cref="Training.ClosedByBan"/>): the proposals go, a session dated
/// leaves the calendar and its trainer's day is free, and the trainer is told. A session that has started is not closed (the
/// maintainer's answer on #149): its trainer records it, and rescheduling it under a ban closes it (<see cref="CloseIfBannedAsync"/>). When the ban
/// is over or lifted nothing reopens: the member asks again.</para>
/// <para>Two writes can meet a ban. A step on one of the trainings — the trainee choosing a date, the staff assigning — moves its
/// version, so whichever saves second is a 409. A request sent while the ban is given is a new row, which no version guards: after the
/// ban is saved the trainings are read once more and what is still open is closed (<see cref="AfterGivingAsync"/>), and after a request
/// is saved the bans are read once more (<c>TrainingRequests</c>) — one of the two reads always sees the other's row committed.</para>
/// </summary>
public sealed class TrainingBans(
    TrainingDbContext database,
    TrainingPeople people,
    TrainingMail mail,
    ModuleRegistry modules,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>An end that is already over: a ban that ended before it was given says nothing.</summary>
    public const string EndsInThePast = "training:errors.banEndsInThePast";

    /// <summary>A ban holds on the member already: to change it, it is lifted and another is given.</summary>
    public const string AlreadyHolds = "training:errors.banAlreadyHolds";

    /// <summary>The ban no longer holds — over, or lifted —: there is nothing to lift.</summary>
    public const string NotHolding = "training:errors.banNotHolding";

    /// <summary>How many times the trainings still open are read again after a ban, when a step on one of them moved it meanwhile.</summary>
    private const int Attempts = 3;

    /// <summary>The trainings the ban being given closed in its own save, for the mails after it.</summary>
    private readonly List<Closing> _closed = [];

    /// <summary>
    /// The trainings of <paramref name="vid"/> a ban closes at <paramref name="now"/> (issue #312): every one still open — a request
    /// waiting, accepted, with its trainer, dated —, but one whose session has started, which its trainer records (the maintainer's
    /// answer on #149, <c>TrainingSessions.IsRecordable</c>). A query, so the database asks it.
    /// </summary>
    public static IQueryable<Training> Closable(IQueryable<Training> trainings, int vid, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        return trainings.Where(training => training.TraineeVid == vid
            && (training.State == TrainingState.Requested
                || training.State == TrainingState.Accepted
                || training.State == TrainingState.Assigned
                || (training.State == TrainingState.Scheduled && training.ScheduledStartUtc > now)));
    }

    /// <summary>
    /// A training closed by a ban, not saved: by <paramref name="by"/>, with no reason, its proposals gone. The date of its session stays
    /// on record, as every closing keeps it (§6); a closed training is no longer in the calendar, so its trainer's day is free.
    /// </summary>
    public static void Close(TrainingDbContext database, Training training, int? by, IEnumerable<TrainingSlot> slots, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(training);

        training.State = TrainingState.Closed;
        training.ClosedBy = by;
        training.ClosedAt = now;
        training.CloseReason = null;
        training.ClosedByBan = true;
        database.Slots.RemoveRange(slots);
    }

    /// <summary>The ban that holds on the member now, the newest, or none: read as the request reads it.</summary>
    public async Task<TraineeBan?> HoldingAsync(int vid, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var bans = await database.TraineeBans.AsNoTracking().Where(ban => ban.Vid == vid).ToListAsync(cancellationToken);

        return bans.Where(ban => ban.Holds(now)).OrderByDescending(ban => ban.CreatedAt).FirstOrDefault();
    }

    /// <summary>
    /// After a saved write that leaves a training of a member open — their request, a session rescheduled —, the bans read once more
    /// (issue #312): one that holds closes it, by whoever gave it, and the caller says so. A ban saved after this read closes it itself,
    /// in the read it makes once it is saved (<see cref="AfterGivingAsync"/>); one saved before it is seen here. Returns whether a ban
    /// holds.
    /// </summary>
    public async Task<bool> CloseIfBannedAsync(Training training, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        if (!Training.IsOpen(training.State) || await HoldingAsync(training.TraineeVid, cancellationToken) is not { } ban)
        {
            return false;
        }

        var slots = await database.Slots.Where(slot => slot.TrainingId == training.Id).ToListAsync(cancellationToken);
        Close(database, training, ban.CreatedBy == 0 ? null : ban.CreatedBy, slots, clock.UtcNow);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The ban's own read after its save closed it meanwhile.
            database.ChangeTracker.Clear();
        }

        return true;
    }

    /// <summary>
    /// What a ban does before it is saved: the refusal of <see cref="RefusalAsync"/>; and a new one closes every training of its member
    /// still open (<see cref="Closable"/>), in the same save, by the reader who gives it (issue #312). A step on one of them saved
    /// meanwhile moved its version: the ban is then a 409, and nothing of it is written.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> BeforeGivingAsync(TraineeBan ban, bool isNew, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ban);

        if (await RefusalAsync(ban, cancellationToken) is { } refused)
        {
            return refused;
        }

        if (isNew)
        {
            _closed.AddRange(await CloseOpenAsync(ban.Vid, cancellationToken));
        }

        return null;
    }

    /// <summary>
    /// What follows a ban given, once it is saved (§5.2, issue #312): the member's mail; the trainings still open read once more — a
    /// request sent while the ban was being saved — and closed; and a mail to the trainer of every training closed that had one.
    /// </summary>
    public async Task AfterGivingAsync(TraineeBan ban, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ban);

        await TellAsync(ban, cancellationToken);

        List<Closing> closed = [.. _closed, .. await CloseStillOpenAsync(ban, cancellationToken)];
        _closed.Clear();

        foreach (var closing in closed)
        {
            await TellTheTrainerAsync(closing, cancellationToken);
        }
    }

    /// <summary>A ban as the staff reaches it, past the filter of the members: the handler says who may write it.</summary>
    public Task<TraineeBan?> FindAsync(long id, bool tracked, CancellationToken cancellationToken)
    {
        var bans = CrudSource.BackOffice<TraineeBan>(database);
        return (tracked ? bans : bans.AsNoTracking()).FirstOrDefaultAsync(ban => ban.Id == id, cancellationToken);
    }

    /// <summary>Every ban of a member, the newest first: their history, and what holds now.</summary>
    public async Task<IReadOnlyList<TraineeBan>> OfAsync(int vid, CancellationToken cancellationToken) =>
        await CrudSource.BackOffice<TraineeBan>(database).AsNoTracking()
            .Where(ban => ban.Vid == vid)
            .OrderByDescending(ban => ban.CreatedAt)
            .ThenByDescending(ban => ban.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Whether the reader may ban this member now, as the one handler answers on the ban they would write: <c>Training.Ban</c> on the
    /// base department of the module, and never on themselves.
    /// </summary>
    public Task<bool> MayBanAsync(int vid) => MayAsync(BanOf(vid), TrainingPermissions.Ban);

    /// <summary>
    /// Whether the reader may read the bans of this member, as the one handler answers on a ban of theirs: <c>Training.View</c> on the
    /// base department of the module. A ban says no FIR (design M3 §1.5-bis), so the <c>Training.View</c> a head of a FIR holds on
    /// the trainings of their FIR alone reaches none (A11b; note 2026-09-27-i-capi-fir-sul-loro-fir §3.2).
    /// </summary>
    public Task<bool> MayReadAsync(int vid) => MayAsync(BanOf(vid), TrainingPermissions.View);

    /// <summary>
    /// What a new ban may not be, looking at the other bans of the member: one holds already. A member has few bans, and whether one
    /// holds is <see cref="TraineeBan.Holds"/>, asked of each.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> RefusalAsync(TraineeBan ban, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ban);

        var now = clock.UtcNow;
        var others = await CrudSource.BackOffice<TraineeBan>(database).AsNoTracking()
            .Where(other => other.Vid == ban.Vid && other.Id != ban.Id)
            .ToListAsync(cancellationToken);

        return others.Any(other => other.Holds(now)) ? new Refusals().Add("vid", AlreadyHolds).Errors : null;
    }

    /// <summary>
    /// «Lift the ban» (§2.9): who and when, on a ban that holds, with <c>Training.Ban</c> on the row — never by the member it is about —
    /// at the version seen. The member may ask for trainings again at once; no mail, and nothing the ban closed reopens (issue #312).
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> LiftAsync(
        TraineeBan ban,
        DateTime rowVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ban);

        if (!await MayAsync(ban, TrainingPermissions.Ban))
        {
            return (StaffResult.Forbidden, null);
        }

        var now = clock.UtcNow;
        if (!ban.Holds(now))
        {
            return (StaffResult.Refused, new Refusals().Add("state", NotHolding).Errors);
        }

        database.Entry(ban).Property(row => row.RowVersion).OriginalValue = rowVersion;
        ban.LiftedBy = currentUser.Vid;
        ban.LiftedAt = now;
        await database.SaveChangesAsync(cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// The mail of a ban (§5.2), when it is given: why, until when, and the member's page of their trainings — in their language.
    /// </summary>
    public Task TellAsync(TraineeBan ban, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ban);

        return mail.SendAsync(
            TrainingNotifications.Banned,
            ban.Vid,
            TrainingMail.MinePath,
            (data, locale) =>
            {
                data["reason"] = ban.Reason;
                data["until"] = ban.EndsAt is { } end
                    ? mail.Word(locale, "training:mail.training.bannedUntil", "date", TrainingMail.Moment(end))
                    : mail.Word(locale, "training:mail.training.bannedUntilLifted");
            },
            cancellationToken);
    }

    /// <summary>Bans as the list and the path show them, with the names of the members and of the staff: one query for them all.</summary>
    public async Task<IReadOnlyList<TraineeBanDto>> RowsAsync(IReadOnlyList<TraineeBan> bans, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bans);

        var names = await people.NamesAsync(bans.SelectMany(ban => new int?[] { ban.Vid, ban.CreatedBy, ban.LiftedBy }), cancellationToken);
        var now = clock.UtcNow;

        return [.. bans.Select(ban => Row(ban, names, now))];
    }

    /// <summary>
    /// A ban as a row; without the names when the caller has none to give. Nobody gave a ban the installation wrote (<c>0</c>); a ban
    /// given by somebody whose data was erased was given by that deleted person, whom the pages name as such (A12b).
    /// </summary>
    public static TraineeBanDto Row(TraineeBan ban, IReadOnlyDictionary<int, string> names, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(ban);
        ArgumentNullException.ThrowIfNull(names);

        return new TraineeBanDto(
            ban.Id,
            TrainingPeople.Member(ban.Vid, names)!,
            ban.Reason,
            ban.CreatedAt,
            TrainingPeople.Member(ban.CreatedBy != 0 ? ban.CreatedBy : null, names),
            ban.EndsAt,
            TrainingPeople.Member(ban.LiftedBy, names),
            ban.LiftedAt,
            ban.Holds(now),
            ban.RowVersion);
    }

    /// <summary>
    /// Every training of the member a ban closes now, closed by the reader and not saved, with the date each had: the mail to its
    /// trainer names it.
    /// </summary>
    private async Task<List<Closing>> CloseOpenAsync(int vid, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var trainings = await Closable(CrudSource.BackOffice<Training>(database), vid, now).ToListAsync(cancellationToken);
        if (trainings.Count == 0)
        {
            return [];
        }

        var ids = trainings.Select(training => training.Id).ToList();
        var slots = (await database.Slots.Where(slot => ids.Contains(slot.TrainingId)).ToListAsync(cancellationToken)).ToLookup(slot => slot.TrainingId);

        // Nobody, when the installation writes the ban.
        int? by = currentUser.IsAuthenticated ? currentUser.Vid : null;

        List<Closing> closed = [];
        foreach (var training in trainings)
        {
            closed.Add(new Closing(training, training.State == TrainingState.Scheduled ? training.ScheduledStartUtc : null));
            Close(database, training, by, slots[training.Id], now);
        }

        return closed;
    }

    /// <summary>
    /// The trainings still open once the ban is saved, closed and saved: a request sent while it was being saved, which its read before
    /// the save could not see. A step on one of them saved meanwhile moves its version: they are read again, a few times.
    /// </summary>
    private async Task<List<Closing>> CloseStillOpenAsync(TraineeBan ban, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var closed = await CloseOpenAsync(ban.Vid, cancellationToken);
            if (closed.Count == 0)
            {
                return closed;
            }

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                return closed;
            }
            catch (DbUpdateConcurrencyException)
            {
                database.ChangeTracker.Clear();
            }
        }

        return [];
    }

    /// <summary>
    /// The mail to the trainer of a training a ban closed (§5.2, issue #312), in their language: whose training, and the date of its
    /// session, now free. Nothing for a training with no trainer, nor to a trainer whose data was erased (A12b). No reason: the ban's
    /// is the member's and the training department's.
    /// </summary>
    private async Task TellTheTrainerAsync(Closing closing, CancellationToken cancellationToken)
    {
        var training = closing.Training;
        if (training.TrainerVid is not { } trainer || TrainingPeople.IsErased(trainer))
        {
            return;
        }

        var names = await people.NamesAsync([training.TraineeVid], cancellationToken);

        await mail.SendAsync(
            TrainingNotifications.ClosedByBan,
            trainer,
            training,
            TrainingMail.StaffPath(training.Id),
            (data, locale) =>
            {
                mail.Name(data, locale, "trainee", training.TraineeVid, names);
                data["session"] = closing.Session is { } start
                    ? mail.Word(locale, "training:mail.training.closedByBanSession", "session", TrainingMail.Moment(start))
                    : string.Empty;
            },
            cancellationToken);
    }

    /// <summary>A ban of this member as the base department of the module would hold it, not saved: what the handler is asked about.</summary>
    private TraineeBan BanOf(int vid) => new() { Vid = vid, OwnerDepartment = modules.BaseDepartmentOf(typeof(TrainingDbContext)) ?? default };

    /// <summary>Whether the reader holds <paramref name="permission"/> on this ban, as the one handler answers on the row.</summary>
    private async Task<bool> MayAsync(TraineeBan ban, string permission) =>
        http.HttpContext is { } context
        && (await authorization.AuthorizeAsync(context.User, ban, permission)).Succeeded;

    /// <summary>A training a ban closed, with the date its session had when it was dated: none otherwise.</summary>
    private sealed record Closing(Training Training, DateTime? Session);
}
