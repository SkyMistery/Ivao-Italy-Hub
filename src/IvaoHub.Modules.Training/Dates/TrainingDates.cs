using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// The date of a training (design M3 §2.5): the dates the trainer proposes — each with the warnings the division checks, which its
/// policy shows and asks to confirm, refuses, or leaves alone, and which the date keeps as far as every reader of the training may
/// read them —, the trainee's choice among them, the date set by hand, and the closing of a training that found no date: by the
/// staff with a reason, or by the night when the division gives the trainee a time to choose (§12 n.9).
/// <para>Every write is a write of the training. The proposals are child rows written with it (§1.1), so proposing and
/// withdrawing touch it: the write guard then asks for the training's own permission — <c>Training.Conduct</c>, which reaches the
/// training assigned to the writer alone (A7b), or <c>Training.Edit</c>, which the coordinator and the assistant hold —, and a
/// version the writer did not see is a 409. The proposals live only while the training waits for a date and all go when it has
/// one or closes (§1.3, §6); the training keeps the date, and which proposal it was (§1.2).</para>
/// <para>A date fixed puts the session into the calendar by itself: the training projects it (<see cref="Training.Project"/>).
/// The mails go after the save, through <see cref="TrainingMail"/>: the dates proposed to the trainee, the date fixed to both, the
/// closing to the trainee.</para>
/// </summary>
public sealed class TrainingDates(
    TrainingDbContext database,
    HubDbContext hub,
    RatingVocabulary vocabulary,
    ModuleSettingsStore settingsStore,
    StaffTrainings staff,
    TrainingPeople people,
    TrainingMail mail,
    ICurrentUser currentUser,
    IOptions<DivisionOptions> division,
    IClock clock)
{
    /// <summary>Dates are proposed only while the training has its trainer and waits for its date.</summary>
    public const string NotProposable = "training:errors.datesNotProposable";

    /// <summary>A date proposed that has already gone by, or is going by now.</summary>
    public const string SlotPassed = "training:errors.slotPassed";

    public const string SlotEndsBeforeItStarts = "training:errors.slotEndsBeforeItStarts";

    public const string SlotTooLong = "training:errors.slotTooLong";

    /// <summary>A date proposed twice: once is enough.</summary>
    public const string SlotTwice = "training:errors.slotTwice";

    public const string SlotsTooMany = "training:errors.slotsTooMany";

    /// <summary>A date that is not, or no longer, among the proposals of the training.</summary>
    public const string SlotUnknown = "training:errors.slotUnknown";

    /// <summary>The trainee chooses only while the training waits for its date.</summary>
    public const string NotChoosable = "training:errors.slotNotChoosable";

    /// <summary>A date is set by hand only while the training has its trainer and goes on.</summary>
    public const string NotSettable = "training:errors.dateNotSettable";

    public const string NotClosable = "training:errors.trainingNotClosable";

    /// <summary>The longest a date proposed may last: a session of an evening, and room to spare.</summary>
    public static readonly TimeSpan MaxSlotLength = TimeSpan.FromHours(12);

    /// <summary>How many dates to come may wait for the trainee at once.</summary>
    public const int MaxOpenSlots = 10;

    /// <summary>The states in which a training is dated, or has its date changed, by whoever conducts it (§2.1).</summary>
    public static bool IsDatable(TrainingState state) => state is TrainingState.Assigned or TrainingState.Scheduled;

    /// <summary>The states the staff closes a training from (§2.1): accepted and still going on.</summary>
    public static bool IsClosable(TrainingState state) =>
        state is TrainingState.Accepted or TrainingState.Assigned or TrainingState.Scheduled;

    /// <summary>
    /// The trainings the trainee has let wait longer than the division gives them to choose (§2.5, §12 n.9): with their trainer and
    /// dates proposed, the last of them proposed before <paramref name="before"/>. The trainee who never had a date to choose from
    /// is not one of them.
    /// </summary>
    public static IQueryable<Training> Unanswered(IQueryable<Training> trainings, IQueryable<TrainingSlot> slots, DateTime before)
    {
        ArgumentNullException.ThrowIfNull(trainings);
        ArgumentNullException.ThrowIfNull(slots);

        return trainings.Where(training => training.State == TrainingState.Assigned
            && slots.Any(slot => slot.TrainingId == training.Id)
            && !slots.Any(slot => slot.TrainingId == training.Id && slot.CreatedAt > before));
    }

    /// <summary>
    /// What the hub finds on the days of a date (§2.5), before anybody writes it: the division's policy, and the warnings — none when
    /// the policy is not to look.
    /// </summary>
    public async Task<DateConflictsDto> ConflictsAsync(Training training, DateTime startsAtUtc, DateTime? endsAtUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        var settings = await SettingsAsync(cancellationToken);
        var found = await WarningsAsync(training, Utc(startsAtUtc), endsAtUtc is { } end ? Utc(end) : null, settings, cancellationToken);

        return new DateConflictsDto(settings.ConflictPolicy, found.Shown);
    }

    /// <summary>
    /// The trainer's dates (§2.5), proposed together: each written whole, to come, ending after it starts and within a session's
    /// length, not proposed already, with no more of them waiting than the trainee is fairly offered; each with its warnings, which
    /// the policy turns into a refusal of that date or into a confirmation asked of the proposal. Each keeps the warnings every
    /// reader of the training may read (<see cref="DateConflicts.Kept"/>). Mails the trainee once.
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> ProposeAsync(
        Training training,
        TrainingSlotsWriteDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await staff.MayAsync(training, TrainingPermissions.Conduct))
        {
            return (StaffResult.Forbidden, null);
        }

        if (training.State != TrainingState.Assigned)
        {
            return Refuse("state", NotProposable);
        }

        var proposed = payload.Slots ?? [];
        if (proposed.Count == 0)
        {
            return Refuse("slots", "errors.required");
        }

        var now = clock.UtcNow;
        var existing = await SlotsAsync(training.Id, cancellationToken);
        if (existing.Count(slot => slot.StartsAtUtc > now) + proposed.Count > MaxOpenSlots)
        {
            return Refuse("slots", SlotsTooMany);
        }

        var refusals = new Refusals();
        var moments = new List<(DateTime Start, DateTime End)>();
        for (var index = 0; index < proposed.Count; index++)
        {
            var field = $"slots[{index}]";
            var start = proposed[index].StartsAtUtc is { } written ? Utc(written) : (DateTime?)null;
            var end = proposed[index].EndsAtUtc is { } until ? Utc(until) : (DateTime?)null;

            // A box left empty is required on its own field (A8b); what can be said of the other one still is.
            if (start is not { } from)
            {
                refusals.Add($"{field}.startsAtUtc", "errors.required");
            }
            else if (from <= now)
            {
                refusals.Add($"{field}.startsAtUtc", SlotPassed);
            }
            else if (existing.Exists(slot => slot.StartsAtUtc == from) || moments.Exists(moment => moment.Start == from))
            {
                refusals.Add($"{field}.startsAtUtc", SlotTwice);
            }

            if (end is not { } to)
            {
                refusals.Add($"{field}.endsAtUtc", "errors.required");
            }
            else if (start is { } since)
            {
                if (to <= since)
                {
                    refusals.Add($"{field}.endsAtUtc", SlotEndsBeforeItStarts);
                }
                else if (to - since > MaxSlotLength)
                {
                    refusals.Add($"{field}.endsAtUtc", SlotTooLong);
                }

                // Only a whole date is a moment: one with a box left empty is refused, and nothing below looks at it.
                moments.Add((since, to));
            }
        }

        if (!refusals.IsEmpty)
        {
            return (StaffResult.Refused, refusals.Errors);
        }

        // The policy on each date, on what its writer is shown: a blocked one is refused on its own row, a warned one asks the
        // proposal to be confirmed.
        var settings = await SettingsAsync(cancellationToken);
        var warnings = new List<Found>();
        for (var index = 0; index < moments.Count; index++)
        {
            var found = await WarningsAsync(training, moments[index].Start, moments[index].End, settings, cancellationToken);
            warnings.Add(found);

            if (DateConflicts.Refusal(settings.ConflictPolicy, found.Shown.Count, payload.Confirmed) is { } refusal)
            {
                refusals.Add(refusal == DateConflicts.Blocked ? $"slots[{index}].startsAtUtc" : "confirmed", refusal);
            }
        }

        if (!refusals.IsEmpty)
        {
            return (StaffResult.Refused, refusals.Errors);
        }

        var slots = moments
            .Select((moment, index) => new TrainingSlot
            {
                TrainingId = training.Id,
                StartsAtUtc = moment.Start,
                EndsAtUtc = moment.End,
                WarningsJson = DateWarnings.Write(warnings[index].Kept),
            })
            .ToList();

        Touch(training, payload.RowVersion);
        database.Slots.AddRange(slots);
        await database.SaveChangesAsync(cancellationToken);

        await TellProposedAsync(training, slots, cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>A date taken back before the trainee chose it (§2.5): the trainer changed their mind. No mail: the page shows it.</summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> WithdrawAsync(
        Training training,
        long slotId,
        DateTime rowVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        if (!await staff.MayAsync(training, TrainingPermissions.Conduct))
        {
            return (StaffResult.Forbidden, null);
        }

        if (training.State != TrainingState.Assigned)
        {
            return Refuse("state", NotProposable);
        }

        var slot = (await SlotsAsync(training.Id, cancellationToken)).Find(candidate => candidate.Id == slotId);
        if (slot is null)
        {
            return Refuse("slotId", SlotUnknown);
        }

        Touch(training, rowVersion);
        database.Slots.Remove(slot);
        await database.SaveChangesAsync(cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// The date set by hand (§2.5, d2), by whoever conducts the training — its trainer, the coordinator and the assistant —: any
    /// moment, among the dates proposed or not, with the same warnings and the same policy. The proposals go, and both are mailed
    /// as for a date chosen.
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> SetAsync(
        Training training,
        TrainingDateWriteDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await staff.MayAsync(training, TrainingPermissions.Conduct))
        {
            return (StaffResult.Forbidden, null);
        }

        if (!IsDatable(training.State))
        {
            return Refuse("state", NotSettable);
        }

        if (payload.StartsAtUtc is not { } written || written == default)
        {
            return Refuse("startsAtUtc", "errors.required");
        }

        var start = Utc(written);
        var settings = await SettingsAsync(cancellationToken);
        var found = await WarningsAsync(training, start, endsAtUtc: null, settings, cancellationToken);
        if (DateConflicts.Refusal(settings.ConflictPolicy, found.Shown.Count, payload.Confirmed) is { } refusal)
        {
            return Refuse(refusal == DateConflicts.Blocked ? "startsAtUtc" : "confirmed", refusal);
        }

        var slots = await SlotsAsync(training.Id, cancellationToken);
        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        Date(training, start, chosen: null, slots);
        await database.SaveChangesAsync(cancellationToken);

        await mail.SessionAsync(TrainingNotifications.DateConfirmed, training, cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// The trainee's choice among the dates proposed (§2.5, d1), on a training of their own — written through the one exception of
    /// the write guard for a member's own row —: one still to come, while the training waits for its date. The training is dated,
    /// the proposals go, and both are mailed. Returns the refusals, or none.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> ChooseAsync(
        Training training,
        TrainingSlotChoiceDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (training.State != TrainingState.Assigned)
        {
            return new Refusals().Add("state", NotChoosable).Errors;
        }

        var slots = await SlotsAsync(training.Id, cancellationToken);
        var chosen = slots.Find(slot => slot.Id == payload.SlotId);
        if (chosen is null)
        {
            return new Refusals().Add("slotId", SlotUnknown).Errors;
        }

        if (chosen.StartsAtUtc <= clock.UtcNow)
        {
            return new Refusals().Add("slotId", SlotPassed).Errors;
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        Date(training, chosen.StartsAtUtc, chosen.Id, slots);
        await database.SaveChangesAsync(cancellationToken);

        await mail.SessionAsync(TrainingNotifications.DateConfirmed, training, cancellationToken);

        return null;
    }

    /// <summary>
    /// A training closed by the staff (§2.5, R.3: the trainee never answered, or anything else), with <c>Training.Approve</c> and a
    /// reason the trainee reads: accepted and still going on — dated too, whose session then leaves the calendar and stays on
    /// record. Nobody closes a training of their own. Its trainer conducts it no more: nothing is left to conduct.
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> CloseAsync(
        Training training,
        TrainingClosureDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await staff.MayAsync(training, TrainingPermissions.Approve))
        {
            return (StaffResult.Forbidden, null);
        }

        if (!IsClosable(training.State))
        {
            return Refuse("state", NotClosable);
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

        var slots = await SlotsAsync(training.Id, cancellationToken);
        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        Close(training, currentUser.Vid, reason, slots);
        await database.SaveChangesAsync(cancellationToken);

        await mail.SendAsync(
            TrainingNotifications.TrainingClosed,
            training.TraineeVid,
            training,
            TrainingMail.MinePathOf(training.Id),
            (data, locale) =>
            {
                data["why"] = mail.Word(locale, "training:mail.training.closedByStaff", "reason", reason);
                data["after"] = mail.Word(locale, "training:mail.training.closedNoWait");
            },
            cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// Closes, as the hub, the trainings the trainee has let wait longer than <c>maxResponseDays</c> (§2.5, §12 n.9), and mails
    /// them; nothing when the division has not set it, which is the default. One at a time: a trainee who chooses meanwhile keeps
    /// the date, and the next night looks again. Returns how many were closed.
    /// <para>The hub's closing is told by what it leaves empty: nobody closed it, and no reason was written. The hub closes for this
    /// one reason, which its mail names (<c>closedUnanswered</c>), and a page tells it by the reason left empty. Through the row,
    /// never around it: the key of one open training per ladder reads a column that only the row writes
    /// (<see cref="Training.OpenKind"/>).</para>
    /// </summary>
    public async Task<int> CloseUnansweredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken);
        if (settings.MaxResponseDays is not { } days)
        {
            return 0;
        }

        var before = now.AddDays(-days);
        var ids = await Unanswered(CrudSource.BackOffice<Training>(database).AsNoTracking(), database.Slots, before)
            .Select(training => training.Id)
            .ToListAsync(cancellationToken);

        var closed = 0;
        foreach (var id in ids)
        {
            var training = await Unanswered(CrudSource.BackOffice<Training>(database), database.Slots, before)
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (training is null)
            {
                continue;
            }

            Close(training, by: null, reason: null, await SlotsAsync(training.Id, cancellationToken), now);

            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                database.ChangeTracker.Clear();
                continue;
            }

            await mail.SendAsync(
                TrainingNotifications.TrainingClosed,
                training.TraineeVid,
                training,
                TrainingMail.MinePathOf(training.Id),
                (data, locale) =>
                {
                    data["why"] = mail.Word(locale, "training:mail.training.closedUnanswered");
                    data["after"] = mail.Word(locale, "training:mail.training.closedNoWait");
                },
                cancellationToken);
            closed++;
        }

        return closed;
    }

    /// <summary>
    /// The warnings of a moment (§2.5), as the division's policy asks: none when it is not to look. The other trainings are read as
    /// the staff reads them, every one; the calendar as whoever writes the date reads it, and all of it is shown to them and counted
    /// by the policy — but a date keeps only what every reader of the training may read (<see cref="DateConflicts.Kept"/>).
    /// </summary>
    private async Task<Found> WarningsAsync(
        Training training,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        TrainingSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.ConflictPolicy == ConflictPolicy.None)
        {
            return new Found([], []);
        }

        var (from, to) = DivisionDays.Touched(startsAtUtc, endsAtUtc, division.Value.ResolveTimeZone());

        var sessions = await DateConflicts.Sessions(CrudSource.BackOffice<Training>(database).AsNoTracking(), training.Id, from, to)
            .ToListAsync(cancellationToken);

        var kinds = settings.ConflictKinds.ToList();
        var entries = kinds.Count == 0
            ? []
            : await DateConflicts.Entries(hub.CalendarEntries.AsNoTracking(), kinds, from, to).ToListAsync(cancellationToken);

        return new Found(
            DateConflicts.Warnings(sessions, entries, vocabulary),
            DateConflicts.Warnings(sessions, DateConflicts.Kept(entries), vocabulary));
    }

    /// <summary>The training dated: at that start, from that proposal or by hand; its reminder to leave again, its proposals gone.</summary>
    private void Date(Training training, DateTime start, long? chosen, IEnumerable<TrainingSlot> slots)
    {
        training.State = TrainingState.Scheduled;
        training.ScheduledStartUtc = start;
        training.ChosenSlotId = chosen;
        training.RemindedAt = null;
        database.Slots.RemoveRange(slots);
    }

    /// <summary>
    /// The training closed, by somebody with their reason or by the hub, and its proposals gone with it. The date of its session
    /// stays on record, with which proposal it was and whether it was reminded: the register keeps states and dates (§6), and a
    /// session that may have been held is not erased. Nothing else needs it gone: only a dated training is in the calendar, is
    /// reminded or shows as held.
    /// </summary>
    private void Close(Training training, int? by, string? reason, IEnumerable<TrainingSlot> slots, DateTime? at = null)
    {
        training.State = TrainingState.Closed;
        training.ClosedBy = by;
        training.ClosedAt = at ?? clock.UtcNow;
        training.CloseReason = reason;
        database.Slots.RemoveRange(slots);
    }

    /// <summary>
    /// A write of the proposals is a write of their training: touched at the version the writer saw, it goes through the write
    /// guard with the training's own permission, and a version gone stale is a conflict.
    /// </summary>
    private void Touch(Training training, DateTime rowVersion)
    {
        database.Entry(training).Property(row => row.RowVersion).OriginalValue = rowVersion;
        database.Entry(training).Property(row => row.UpdatedAt).IsModified = true;
    }

    /// <summary>The mail of the dates proposed (§5.2), to the trainee: who proposed them for their training, and when they are.</summary>
    private async Task TellProposedAsync(Training training, IReadOnlyList<TrainingSlot> slots, CancellationToken cancellationToken)
    {
        var names = await people.NamesAsync([training.TrainerVid], cancellationToken);
        var trainer = training.TrainerVid is { } vid ? TrainingPeople.Label(vid, names) : string.Empty;
        var dates = string.Join('\n', slots.OrderBy(slot => slot.StartsAtUtc).Select(slot => $"- {TrainingMail.Span(slot.StartsAtUtc, slot.EndsAtUtc)}"));

        await mail.SendAsync(
            TrainingNotifications.DatesProposed,
            training.TraineeVid,
            training,
            TrainingMail.MinePathOf(training.Id),
            (data, _) =>
            {
                data["trainer"] = trainer;
                data["dates"] = dates;
            },
            cancellationToken);
    }

    /// <summary>Every proposal of a training, tracked: a write takes them away.</summary>
    private Task<List<TrainingSlot>> SlotsAsync(long trainingId, CancellationToken cancellationToken) =>
        database.Slots.Where(slot => slot.TrainingId == trainingId).ToListAsync(cancellationToken);

    private Task<TrainingSettings> SettingsAsync(CancellationToken cancellationToken) =>
        settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);

    /// <summary>An instant as the payload said it, in UTC: one with an offset is moved there, one without any is taken as UTC.</summary>
    internal static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static (StaffResult, IReadOnlyDictionary<string, string[]>) Refuse(string field, string key) =>
        (StaffResult.Refused, new Refusals().Add(field, key).Errors);

    /// <summary>What the hub found on the days of a date: every warning its writer is shown, and those the date may keep.</summary>
    private sealed record Found(IReadOnlyList<DateWarning> Shown, IReadOnlyList<DateWarning> Kept);
}
