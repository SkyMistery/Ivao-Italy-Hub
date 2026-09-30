using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
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
/// The bans of the trainees (design M3 §2.9): while one holds the member asks for no training, on either ladder, and the trainings
/// already open go on (the request reads them, A6a). With <c>Training.Ban</c> — the coordinator and the assistant; the direction and
/// the web for the core — a ban is given with a reason and, if one wants, an end, from the trainee's path or from the list; the member
/// is told by mail. «Lift the ban» records who and when. A ban is never changed nor deleted: it stays in the member's history. Nobody
/// bans themselves nor lifts a ban of their own, the super administrator included: <c>Training.Ban</c> is denied to whoever the ban is
/// about, and the one handler says so on the row.
/// <para>Whoever writes a ban holds <c>Training.Edit</c> as well, which the write guard asks of every row of the staff (§3.1), as with
/// the items of the sheet (A5): in the division the two go together.</para>
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
    /// at the version seen. The member may ask for trainings again at once; no mail.
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

    /// <summary>A ban as a row; without the names when the caller has none to give.</summary>
    public static TraineeBanDto Row(TraineeBan ban, IReadOnlyDictionary<int, string> names, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(ban);
        ArgumentNullException.ThrowIfNull(names);

        return new TraineeBanDto(
            ban.Id,
            TrainingPeople.Member(ban.Vid, names)!,
            ban.Reason,
            ban.CreatedAt,
            TrainingPeople.Member(ban.CreatedBy > 0 ? ban.CreatedBy : null, names),
            ban.EndsAt,
            TrainingPeople.Member(ban.LiftedBy, names),
            ban.LiftedAt,
            ban.Holds(now),
            ban.RowVersion);
    }

    /// <summary>A ban of this member as the base department of the module would hold it, not saved: what the handler is asked about.</summary>
    private TraineeBan BanOf(int vid) => new() { Vid = vid, OwnerDepartment = modules.BaseDepartmentOf(typeof(TrainingDbContext)) ?? default };

    /// <summary>Whether the reader holds <paramref name="permission"/> on this ban, as the one handler answers on the row.</summary>
    private async Task<bool> MayAsync(TraineeBan ban, string permission) =>
        http.HttpContext is { } context
        && (await authorization.AuthorizeAsync(context.User, ban, permission)).Succeeded;
}
