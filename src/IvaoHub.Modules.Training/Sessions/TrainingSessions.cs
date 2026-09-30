using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Sheets;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Sessions;

/// <summary>
/// After the session (design M3 §2.6, §2.7, §2.8): one of three roads, which whoever conducts the training records once its session
/// has started — its trainer, to whom it is assigned (A7b), the coordinator and the assistant on any (§3.3). Rescheduled for
/// too little traffic (R.5): the session becomes a row with the internal notes, the training goes back to its dates (A8), and there
/// is no report. Not attended: the session becomes a row, the training closes, and the waiting of a no-show runs (§2.2 point 3).
/// Reported: the sheet filled from the active items, the comments and the three boxes; the session held; the training completed —
/// and «ready for the mock exam» makes the trainee's next request on the rating a mock exam (§2.8, <see cref="RequestRules.IsMockExam"/>).
/// <para>Every write is a write of the training, which the write guard lets whoever conducts it make (A3, A7), never its trainee (the
/// permission is denied to whoever the training is about); a version the writer did not see is a 409. The child rows go with it, in
/// the same transaction. The mails go after the save, through <see cref="TrainingMail"/>: the closing of a no-show, and the report
/// published, to the trainee — never with a field of the staff's in them.</para>
/// </summary>
public sealed class TrainingSessions(
    TrainingDbContext database,
    ModuleSettingsStore settingsStore,
    StaffTrainings staff,
    TrainingPeople people,
    TrainingMail mail,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>How a session went is recorded only while the training is dated and its session has started.</summary>
    public const string NotRecordable = "training:errors.sessionNotRecordable";

    /// <summary>«Ready for the mock exam» on a mock exam: its next training is not one again (§2.8).</summary>
    public const string MockExamAgain = "training:errors.reportMockExamAgain";

    /// <summary>
    /// Whether how the session went may be recorded now (§2.6): the training is dated and its session has started. It shows as held
    /// from the day after (§1.2), and the trainer need not wait for that to report an evening's session.
    /// </summary>
    public static bool IsRecordable(Training training, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(training);

        return training.State == TrainingState.Scheduled && training.ScheduledStartUtc is { } start && start <= now;
    }

    /// <summary>
    /// The session rescheduled for too little traffic (§2.6, R.5): a row <see cref="SessionOutcome.Rescheduled"/> with the internal notes,
    /// and the training back to <c>Assigned</c> with no date, so that its dates are proposed again (A8); its session leaves the
    /// calendar. No report, and no mail: the next one is the dates proposed.
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> RescheduleAsync(
        Training training,
        TrainingRescheduleDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await staff.MayAsync(training, TrainingPermissions.Conduct))
        {
            return (StaffResult.Forbidden, null);
        }

        if (!IsRecordable(training, clock.UtcNow))
        {
            return Refuse("state", NotRecordable);
        }

        var notes = Trimmed(payload.Notes);
        if (notes?.Length > Training.MaxTextLength)
        {
            return Refuse("notes", "errors.text.tooLong");
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        Record(training, SessionOutcome.Rescheduled, notes);
        training.State = TrainingState.Assigned;
        Undate(training);
        await database.SaveChangesAsync(cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// The trainee did not come (§2.6, d2): a row <see cref="SessionOutcome.NoShow"/>, and the training closed as <c>NoShow</c> by
    /// whoever records it; its session leaves the calendar, and the waiting of a no-show runs from now (<c>noShowCooldownDays</c>). The
    /// trainee is written to, with the session and until when they wait (§5.2).
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> NoShowAsync(
        Training training,
        TrainingNoShowDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await staff.MayAsync(training, TrainingPermissions.Conduct))
        {
            return (StaffResult.Forbidden, null);
        }

        if (!IsRecordable(training, clock.UtcNow))
        {
            return Refuse("state", NotRecordable);
        }

        var session = training.ScheduledStartUtc!.Value;
        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        Record(training, SessionOutcome.NoShow, notes: null);
        training.State = TrainingState.NoShow;
        training.ClosedBy = currentUser.Vid;
        training.ClosedAt = clock.UtcNow;
        Undate(training);
        await database.SaveChangesAsync(cancellationToken);

        var until = RequestRules.WaitUntil([training], training.Kind, await SettingsAsync(cancellationToken));
        await mail.SendAsync(
            TrainingNotifications.TrainingClosed,
            training.TraineeVid,
            training,
            TrainingMail.MinePathOf(training.Id),
            (data, locale) =>
            {
                data["why"] = mail.Word(locale, "training:mail.training.closedNoShow", "session", TrainingMail.Moment(session));
                data["after"] = WaitSentence(locale, until);
            },
            cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// The report (§2.7), published at once by whoever conducts the training (d2): the sheet filled from the items active now on the
    /// training's ladder and rating — each a copy of its item, an item left out not applicable (d4) —, the comment for the trainee and
    /// the one for the staff, and the boxes: ready for the mock exam — never on a mock exam —, ready for the exam, no waiting. The
    /// session is held, the training completed, and its session stays in the calendar (§5.1). The trainee is written to (§5.2).
    /// </summary>
    public async Task<(StaffResult Result, IReadOnlyDictionary<string, string[]>? Problems)> ReportAsync(
        Training training,
        TrainingReportDto payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(payload);

        if (!await staff.MayAsync(training, TrainingPermissions.Conduct))
        {
            return (StaffResult.Forbidden, null);
        }

        if (!IsRecordable(training, clock.UtcNow))
        {
            return Refuse("state", NotRecordable);
        }

        var refusals = new Refusals();
        var general = Trimmed(payload.GeneralComment);
        var reserved = Trimmed(payload.StaffComment);
        if (general?.Length > Training.MaxCommentLength)
        {
            refusals.Add("generalComment", "errors.text.tooLong");
        }

        if (reserved?.Length > Training.MaxCommentLength)
        {
            refusals.Add("staffComment", "errors.text.tooLong");
        }

        if (payload.ReadyForMockExam && training.IsMockExam)
        {
            refusals.Add("readyForMockExam", MockExamAgain);
        }

        var items = await EvaluationSheet.ItemsOf(CrudSource.BackOffice<SheetItem>(database).AsNoTracking(), training.Kind, training.Rating)
            .ToListAsync(cancellationToken);
        var (sheet, problems) = EvaluationSheet.Fill(items, payload.Sheet ?? []);
        foreach (var (field, keys) in problems ?? new Dictionary<string, string[]>())
        {
            foreach (var key in keys)
            {
                refusals.Add(field, key);
            }
        }

        if (!refusals.IsEmpty)
        {
            return (StaffResult.Refused, refusals.Errors);
        }

        database.Entry(training).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
        foreach (var evaluation in sheet!)
        {
            evaluation.TrainingId = training.Id;
        }

        database.Evaluations.AddRange(sheet);
        Record(training, SessionOutcome.Held, notes: null);
        training.State = TrainingState.Completed;
        training.CompletedAt = clock.UtcNow;
        training.GeneralComment = general;
        training.StaffComment = reserved;
        training.ReadyForMockExam = payload.ReadyForMockExam;
        training.ReadyForExam = payload.ReadyForExam;
        training.CooldownWaived = payload.CooldownWaived;
        await database.SaveChangesAsync(cancellationToken);

        await TellPublishedAsync(training, cancellationToken);

        return (StaffResult.Done, null);
    }

    /// <summary>
    /// The mail of a report published (§5.2), to the trainee: who published it, what the trainer marked them ready for, until when they
    /// wait — and the page where they read it. Nothing of the staff's: the report itself is read on the page.
    /// </summary>
    private async Task TellPublishedAsync(Training training, CancellationToken cancellationToken)
    {
        var names = await people.NamesAsync([currentUser.Vid], cancellationToken);
        var until = RequestRules.WaitUntil([training], training.Kind, await SettingsAsync(cancellationToken));

        await mail.SendAsync(
            TrainingNotifications.ReportPublished,
            training.TraineeVid,
            training,
            TrainingMail.MinePathOf(training.Id),
            (data, locale) =>
            {
                string?[] ready =
                [
                    training.ReadyForMockExam ? mail.Word(locale, "training:mail.training.readyForMockExam") : null,
                    training.ReadyForExam ? mail.Word(locale, "training:mail.training.readyForExam") : null,
                ];

                mail.Name(data, locale, "trainer", currentUser.Vid, names);
                data["ready"] = string.Concat(ready.OfType<string>().Select(sentence => $"\n\n{sentence}"));
                data["after"] = training.CooldownWaived
                    ? mail.Word(locale, "training:mail.training.waitWaived")
                    : WaitSentence(locale, until);
            },
            cancellationToken);
    }

    /// <summary>Until when the trainee waits before asking again on the ladder, as a mail says it: from a date, or not at all.</summary>
    private string WaitSentence(string locale, DateTime? until) =>
        until is { } end && end > clock.UtcNow
            ? mail.Word(locale, "training:mail.training.waitUntil", "date", TrainingMail.Moment(end))
            : mail.Word(locale, "training:mail.training.noWait");

    /// <summary>The session in hand, recorded as over: a row of the sessions at the training's date, with what it came to.</summary>
    private void Record(Training training, SessionOutcome outcome, string? notes) =>
        database.Sessions.Add(new TrainingSession
        {
            TrainingId = training.Id,
            StartsAtUtc = training.ScheduledStartUtc!.Value,
            Outcome = outcome,
            InternalNotes = notes,
        });

    /// <summary>The training without its session in hand, so the calendar lets it go and a new date is reminded afresh.</summary>
    private static void Undate(Training training)
    {
        training.ScheduledStartUtc = null;
        training.ChosenSlotId = null;
        training.RemindedAt = null;
    }

    private Task<TrainingSettings> SettingsAsync(CancellationToken cancellationToken) =>
        settingsStore.GetAsync<TrainingSettings>(TrainingModule.ModuleKey, cancellationToken);

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static (StaffResult, IReadOnlyDictionary<string, string[]>) Refuse(string field, string key) =>
        (StaffResult.Refused, new Refusals().Add(field, key).Errors);
}
