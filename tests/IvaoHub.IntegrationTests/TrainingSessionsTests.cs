using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Sessions;
using IvaoHub.Modules.Training.Sheets;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// After the session (M3, A9; design M3 §1.3, §1.4, §2.6, §2.7, §2.8, §4.1, §5.1, §5.2), through the real host: the report, with the sheet
/// of the active items copied and an item left not applicable, which completes the training, keeps its session in the calendar, mails the
/// trainee and makes them wait; the session rescheduled with its notes, which takes the training back to its dates and makes nobody wait;
/// the no-show, which closes it with the waiting of a no-show; the box of the mock exam and the next request; who may record none of it;
/// a session that has started, which is recorded and never dated again nor closed (#149); an item a report marked, which is not deleted
/// any more; and the rule of note <c>le-note-riservate-e-il-trainee</c>: a trainer who is also
/// a trainee reads the staff's page of their own training without what is reserved, and another's with it, and the trainee reads nothing
/// reserved from their own endpoints. The «done when» of A9 through the API.
/// <para>⚠️ The staff of the training is seeded without an address (<c>CONTRIBUTING.md</c>, "Tests"): the trainers hold a position of
/// the training department and no mailbox; only the trainee, who holds no position, has one. The items of the sheet this class writes
/// carry <see cref="ItemStem"/> in their title, and the assertions read those; other items of the same rating may be on the sheet
/// too.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingSessionsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790038 are A1's to A8a's, 790040–790044
    // and 790050–790051 A3b's.
    private const int TraineeVid = 790039;
    private const int TrainerVid = 790045;
    private const int CoordinatorVid = 790046;
    private const int TrainerTraineeVid = 790047;
    private const int OtherTraineeVid = 790048;
    private const int StrangerVid = 790049;

    private static readonly int[] Vids = [TraineeVid, TrainerVid, CoordinatorVid, TrainerTraineeVid, OtherTraineeVid, StrangerVid];

    private const string GrantReason = "trn-test";

    /// <summary>What the title of every item of the sheet this class writes starts with.</summary>
    private const string ItemStem = "trn-test-a9";

    /// <summary>
    /// The fields of the staff's page that are reserved (note <c>le-note-riservate-e-il-trainee</c> §4): listed here, so that a reserved
    /// field a later phase adds, and the rule forgets, shows in the review of this list.
    /// </summary>
    private static readonly string[] ReservedOnThePage = ["staffComment", "sheet[].staffNote", "sessions[].internalNotes"];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, items, entries, grants and positions.
        await CleanAsync(token);

        var (topAtc, topPilot) = (Ladder(RatingKind.Atc)[^1], Ladder(RatingKind.Pilot)[^1]);
        var code = Division().Code;

        // The trainee has a mailbox, for the mails of the report and of the no-show; their pilot rating is the one below the first
        // trained, so that they may ask for that training.
        await SeedPersonAsync(TraineeVid, "Test", "Trainee", $"trn-test-{TraineeVid}@example.invalid", token, pilot: RungBelow(Trained().Pilot).Number);
        await SeedPersonAsync(OtherTraineeVid, "Other", "Trainee", email: null, token);
        await SeedPersonAsync(StrangerVid, "Some", "Member", email: null, token);

        // A trainer of the department, who conducts the trainings assigned to them with the Training.Conduct of their position
        // (A7b); and a trainer who is a trainee too.
        await SeedPersonAsync(TrainerVid, "Department", "Trainer", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-T96");
        await SeedPersonAsync(TrainerTraineeVid, "Trainer", "Trainee", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-T97");

        // The coordinator conducts every training, and manages the sheet: grants to their VID. A training assigned to somebody else
        // they conduct with Training.Edit (A7b), and they hold neither Approve nor Assign, so the write guard lets them through on
        // Edit alone.
        await SeedPersonAsync(CoordinatorVid, "Test", "Coordinator", email: null, token, isStaff: true);
        await GrantAsync(CoordinatorVid, TrainingPermissions.View, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Conduct, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.ManageSheets, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Edit, token);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheReportCompletesTheTrainingWithACopyOfTheSheetAndTheWaitingRunsFromIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        await WriteSettingsAsync(new { conflictPolicy = "None", cooldownDays = 5 }, token);

        // The sheet of the rating: two items of practice and one of theory, and one switched off.
        var taxi = await AddItemAsync(atc, SheetSection.Practice, "taxi", sort: 901, token);
        var airspace = await AddItemAsync(atc, SheetSection.Theory, "airspace", sort: 902, token);
        var departure = await AddItemAsync(atc, SheetSection.Practice, "departure", sort: 903, token);
        var off = await AddItemAsync(atc, SheetSection.Practice, "switched off", sort: 904, token, active: false);

        var session = DateTime.UtcNow.AddHours(-2);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: session);

        // Before the report: the trainer may record the session, and the page shows the sheet a report would fill — the active items.
        using var trainer = await SignedInAsync(TrainerVid, token);
        var before = await PageAsync(trainer, id, token);
        Assert.True(before.GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        var blank = Mine(before.GetProperty("sheet"));
        Assert.Equal([taxi, airspace, departure], blank.Select(item => item.GetProperty("itemId").GetInt64()));
        Assert.All(blank, item => Assert.Equal(JsonValueKind.Null, item.GetProperty("grade").ValueKind));
        Assert.DoesNotContain(off, before.GetProperty("sheet").EnumerateArray().Select(item => item.GetProperty("itemId").GetInt64()));
        var version = before.GetProperty("rowVersion").GetDateTime();

        // Refused on its fields: a grade beyond five, a mark on practice, a grade on theory, an item that is not on the sheet.
        using (var wrong = await StepAsync(trainer, id, "report", Report(version, [Entry(taxi, grade: 6), Entry(airspace, grade: 3), Entry(departure, mark: TheoryMark.Done)]), token))
        {
            var problem = await ProblemAsync(wrong, token);
            Assert.Contains("errors.number.range", Keys(problem, "sheet[0].grade"));
            Assert.Contains(EvaluationSheet.GradeOnTheory, Keys(problem, "sheet[1].grade"));
            Assert.Contains(EvaluationSheet.MarkOnPractice, Keys(problem, "sheet[2].mark"));
        }

        using (var unknown = await StepAsync(trainer, id, "report", Report(version, [Entry(off, grade: 3)]), token))
        {
            await AssertRefusedAsync(unknown, "sheet", EvaluationSheet.Changed, token);
        }

        Assert.Equal(TrainingState.Scheduled, (await StoredAsync(id, token)).State);

        // Published: the first item graded, the second marked, the third left not applicable.
        var published = await DoneAsync(
            await StepAsync(
                trainer,
                id,
                "report",
                Report(
                    version,
                    [
                        Entry(taxi, grade: 4, comment: "trn-test: a clean taxi", note: "trn-test-reserved: slow on the readbacks"),
                        Entry(airspace, mark: TheoryMark.ToImprove, comment: "trn-test: revise the sectors"),
                    ],
                    general: "trn-test: a good first session",
                    staff: "trn-test-reserved: a second look at the approaches",
                    readyForExam: true),
                token),
            token);

        Assert.Equal(nameof(TrainingState.Completed), published.GetProperty("state").GetString());
        Assert.False(published.GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        Assert.False(published.GetProperty("reservedLeftOut").GetBoolean());
        Assert.Equal("trn-test-reserved: a second look at the approaches", published.GetProperty("staffComment").GetString());
        Assert.True(published.GetProperty("readyForExam").GetBoolean());

        // The copy of the sheet: every active item of the rating, in its order, how the session went on each, the untouched one N/A.
        var sheet = Mine(published.GetProperty("sheet"));
        Assert.Equal([taxi, airspace, departure], sheet.Select(item => item.GetProperty("itemId").GetInt64()));
        Assert.Equal(4, sheet[0].GetProperty("grade").GetInt32());
        Assert.Equal("trn-test-reserved: slow on the readbacks", sheet[0].GetProperty("staffNote").GetString());
        Assert.Equal(nameof(TheoryMark.ToImprove), sheet[1].GetProperty("mark").GetString());
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (sheet[2].GetProperty("grade").ValueKind, sheet[2].GetProperty("mark").ValueKind));
        var stored = await StoredAsync(id, token);
        var activeNow = await ActiveItemsAsync(atc, token);
        Assert.Equal(activeNow, (await EvaluationsOfAsync(id, token)).Count);

        // The session is held, and stays in the calendar: the training keeps its date.
        var held = Assert.Single(published.GetProperty("sessions").EnumerateArray());
        Assert.Equal((nameof(SessionOutcome.Held), TrainerVid), (held.GetProperty("outcome").GetString(), held.GetProperty("recordedBy").GetProperty("vid").GetInt32()));
        Assert.Equal(stored.ScheduledStartUtc, held.GetProperty("startsAtUtc").GetDateTime());
        Assert.Equal(stored.ScheduledStartUtc, Assert.Single(await CalendarOfAsync(id, token)).StartsAtUtc);

        // A copy: the item changed afterwards leaves the report as it was.
        await RenameItemAsync(taxi, $"{ItemStem} renamed", token);
        var reread = await PageAsync(trainer, id, token);
        Assert.StartsWith($"{ItemStem} taxi", Mine(reread.GetProperty("sheet"))[0].GetProperty("title").GetProperty("en").GetString(), StringComparison.Ordinal);

        // Nothing more is recorded on it.
        using (var again = await StepAsync(trainer, id, "no-show", new { rowVersion = published.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(again, "state", TrainingSessions.NotRecordable, token);
        }

        // The trainee: the report without anything reserved, and the waiting on the ladder from its publication.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var theirs = await ReadMineAsync(trainee, id, token);
        Assert.Equal("trn-test: a good first session", theirs.Element.GetProperty("generalComment").GetString());
        var read = theirs.Element.GetProperty("sheet").EnumerateArray().ToList();
        Assert.Contains(read, item => item.GetProperty("traineeComment").GetString() == "trn-test: a clean taxi" && item.GetProperty("grade").GetInt32() == 4);
        AssertNothingReserved(theirs.Text);

        var path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Atc);
        Assert.Equal(RequestRules.Waiting, path.GetProperty("refusal").GetString());
        Assert.Equal(stored.CompletedAt!.Value.AddDays(5), path.GetProperty("waitUntil").GetDateTime());

        // The mail: the page of the training, the box of the exam, until when — nothing reserved.
        var mail = await MailAsync(TraineeVid, TrainingNotifications.ReportPublished, token);
        Assert.Contains($"/training/mine/{id}", mail, StringComparison.Ordinal);
        Assert.Contains(TrainingMail.Moment(stored.CompletedAt!.Value.AddDays(5)), mail, StringComparison.Ordinal);
        AssertNothingReserved(mail);

        // The item a report marked is not deleted any more; one no report marked still is.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using (var used = await coordinator.DeleteAsync(new Uri($"{SheetItemEndpoints.Pattern}/{taxi}", UriKind.Relative), token))
        {
            await AssertRefusedAsync(used, "id", "training:errors.sheetItemUsed", token);
        }

        using (var unused = await coordinator.DeleteAsync(new Uri($"{SheetItemEndpoints.Pattern}/{off}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, unused.StatusCode);
        }
    }

    [Fact]
    public async Task ASessionRescheduledTakesTheTrainingBackToItsDatesWithItsNotesAndMakesNobodyWait()
    {
        var token = TestContext.Current.CancellationToken;
        await WriteSettingsAsync(new { conflictPolicy = "None", cooldownDays = 5 }, token);

        var session = DateTime.UtcNow.AddHours(-1);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: session);
        Assert.Single(await CalendarOfAsync(id, token));

        using var trainer = await SignedInAsync(TrainerVid, token);
        var version = (await PageAsync(trainer, id, token)).GetProperty("rowVersion").GetDateTime();
        using (var tooLong = await StepAsync(trainer, id, "reschedule", new { notes = new string('x', Training.MaxTextLength + 1), rowVersion = version }, token))
        {
            await AssertRefusedAsync(tooLong, "notes", "errors.text.tooLong", token);
        }

        // Rescheduled: a session with its notes, the training back to its dates, its session out of the calendar, and no mail.
        var rescheduled = await DoneAsync(await StepAsync(trainer, id, "reschedule", new { notes = "  trn-test-reserved: little traffic  ", rowVersion = version }, token), token);
        Assert.Equal(nameof(TrainingState.Assigned), rescheduled.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, rescheduled.GetProperty("scheduledStartUtc").ValueKind);
        var row = Assert.Single(rescheduled.GetProperty("sessions").EnumerateArray());
        Assert.Equal(nameof(SessionOutcome.Rescheduled), row.GetProperty("outcome").GetString());
        Assert.Equal("trn-test-reserved: little traffic", row.GetProperty("internalNotes").GetString());
        Assert.Equal(TrainerVid, row.GetProperty("recordedBy").GetProperty("vid").GetInt32());
        Assert.True(rescheduled.GetProperty("actions").GetProperty("canConduct").GetBoolean());
        Assert.False(rescheduled.GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        Assert.Empty(rescheduled.GetProperty("sheet").EnumerateArray());
        Assert.Empty(await CalendarOfAsync(id, token));
        Assert.Equal(0, await MailsAsync(TraineeVid, TrainingNotifications.TrainingClosed, token));

        // The dates go on as before (A8): the trainer proposes new ones.
        var day = DateTime.UtcNow.Date.AddDays(10);
        var proposed = await DoneAsync(
            await StepAsync(trainer, id, "slots", new { slots = new[] { new { startsAtUtc = day.AddHours(16), endsAtUtc = day.AddHours(18) } }, confirmed = true, rowVersion = rescheduled.GetProperty("rowVersion").GetDateTime() }, token),
            token);
        Assert.Single(proposed.GetProperty("slots").EnumerateArray());

        // The trainee reads the session without its notes, and waits for nothing: the training is still open on the ladder.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var theirs = await ReadMineAsync(trainee, id, token);
        var theirSession = Assert.Single(theirs.Element.GetProperty("sessions").EnumerateArray());
        Assert.Equal((nameof(SessionOutcome.Rescheduled), session), (theirSession.GetProperty("outcome").GetString(), theirSession.GetProperty("startsAtUtc").GetDateTime()), new InstantComparer());
        AssertNothingReserved(theirs.Text);
        var path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Atc);
        Assert.Equal(RequestRules.Open, path.GetProperty("refusal").GetString());
        Assert.Equal(JsonValueKind.Null, path.GetProperty("waitUntil").ValueKind);

        // A second session, set by hand and then started a moment ago — nobody dates a training in the past (#149) —, reported with the
        // waiting taken away: nothing to wait for after it.
        await DoneAsync(
            await StepAsync(trainer, id, "date", new { startsAtUtc = DateTime.UtcNow.AddHours(1), confirmed = true, rowVersion = proposed.GetProperty("rowVersion").GetDateTime() }, token),
            token);
        await StartedAMomentAgoAsync(id, token);
        var set = await PageAsync(trainer, id, token);
        var completed = await DoneAsync(
            await StepAsync(trainer, id, "report", Report(set.GetProperty("rowVersion").GetDateTime(), [], cooldownWaived: true), token),
            token);
        Assert.Equal(
            [nameof(SessionOutcome.Rescheduled), nameof(SessionOutcome.Held)],
            completed.GetProperty("sessions").EnumerateArray().Select(each => each.GetProperty("outcome").GetString()));
        Assert.True(completed.GetProperty("cooldownWaived").GetBoolean());

        path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Atc);
        Assert.NotEqual(RequestRules.Waiting, path.GetProperty("refusal").GetString());
        Assert.NotEqual(RequestRules.Open, path.GetProperty("refusal").GetString());
        Assert.Equal(JsonValueKind.Null, path.GetProperty("waitUntil").ValueKind);
        Assert.Contains($"/training/mine/{id}", await MailAsync(TraineeVid, TrainingNotifications.ReportPublished, token), StringComparison.Ordinal);
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.ReportPublished, token));
    }

    [Fact]
    public async Task ANoShowClosesTheTrainingAndTheWaitingOfANoShowRuns()
    {
        var token = TestContext.Current.CancellationToken;
        await WriteSettingsAsync(new { conflictPolicy = "None", cooldownDays = 5, noShowCooldownDays = 9 }, token);

        var session = DateTime.UtcNow.AddDays(-1);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: session);

        // The coordinator, who conducts every training, records it.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var version = (await PageAsync(coordinator, id, token)).GetProperty("rowVersion").GetDateTime();
        var closed = await DoneAsync(await StepAsync(coordinator, id, "no-show", new { rowVersion = version }, token), token);

        Assert.Equal(nameof(TrainingState.NoShow), closed.GetProperty("state").GetString());
        Assert.Equal(CoordinatorVid, closed.GetProperty("closedBy").GetProperty("vid").GetInt32());
        Assert.Equal(JsonValueKind.Null, closed.GetProperty("scheduledStartUtc").ValueKind);
        var row = Assert.Single(closed.GetProperty("sessions").EnumerateArray());
        Assert.Equal(nameof(SessionOutcome.NoShow), row.GetProperty("outcome").GetString());
        Assert.Empty(await CalendarOfAsync(id, token));

        // Nothing more is recorded on it.
        foreach (var verb in new[] { "reschedule", "report" })
        {
            using var refused = await StepAsync(coordinator, id, verb, Body(verb, closed.GetProperty("rowVersion").GetDateTime()), token);
            await AssertRefusedAsync(refused, "state", TrainingSessions.NotRecordable, token);
        }

        // The trainee waits the days of a no-show, not those of a training, from when it was recorded; the mail says so.
        var stored = await StoredAsync(id, token);
        using var trainee = await SignedInAsync(TraineeVid, token);
        var path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Atc);
        Assert.Equal(RequestRules.Waiting, path.GetProperty("refusal").GetString());
        Assert.Equal(stored.ClosedAt!.Value.AddDays(9), path.GetProperty("waitUntil").GetDateTime());

        var mail = await MailAsync(TraineeVid, TrainingNotifications.TrainingClosed, token);
        Assert.Contains(TrainingMail.Moment(session), mail, StringComparison.Ordinal);
        Assert.Contains(TrainingMail.Moment(stored.ClosedAt!.Value.AddDays(9)), mail, StringComparison.Ordinal);
        Assert.Contains($"/training/mine/{id}", mail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NobodyButWhoeverConductsTheTrainingRecordsItsSessionAndNotBeforeItStarts()
    {
        var token = TestContext.Current.CancellationToken;
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);

        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: CoordinatorVid, start: DateTime.UtcNow.AddHours(-1));
        await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(-1));

        // A trainer who is the trainee of a training assigned to themselves — a row no assignment writes — holds Training.Conduct by
        // their position, and still may not: it is denied to whoever the training is about.
        var theirOwn = await AddTrainingAsync(TrainerTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerTraineeVid, start: DateTime.UtcNow.AddHours(-1));
        var version = (await StoredAsync(id, token)).RowVersion;

        // The trainee on their own training; the trainer of another training; a member with no permission.
        using var trainee = await SignedInAsync(TraineeVid, token);
        using var trainerOfAnother = await SignedInAsync(TrainerVid, token);
        using var stranger = await SignedInAsync(StrangerVid, token);
        foreach (var nobody in new[] { trainee, trainerOfAnother, stranger })
        {
            foreach (var verb in new[] { "reschedule", "no-show", "report" })
            {
                using var refused = await StepAsync(nobody, id, verb, Body(verb, version), token);
                Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            }
        }

        using var trainerTrainee = await SignedInAsync(TrainerTraineeVid, token);
        var ownVersion = (await StoredAsync(theirOwn, token)).RowVersion;
        foreach (var verb in new[] { "reschedule", "no-show", "report" })
        {
            using var refused = await StepAsync(trainerTrainee, theirOwn, verb, Body(verb, ownVersion), token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.False((await PageAsync(trainerOfAnother, id, token)).GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        Assert.False((await PageAsync(trainerTrainee, theirOwn, token)).GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        Assert.Equal(TrainingState.Scheduled, (await StoredAsync(id, token)).State);
        Assert.Equal(TrainingState.Scheduled, (await StoredAsync(theirOwn, token)).State);

        // Before the session starts, nothing is recorded, even by whoever conducts it: its date may still move.
        var later = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(3));
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var page = await PageAsync(coordinator, later, token);
        Assert.False(page.GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        foreach (var verb in new[] { "reschedule", "no-show", "report" })
        {
            using var early = await StepAsync(coordinator, later, verb, Body(verb, page.GetProperty("rowVersion").GetDateTime()), token);
            await AssertRefusedAsync(early, "state", TrainingSessions.NotRecordable, token);
        }

        // The coordinator may on the one that has started; a version somebody else moved on is a conflict.
        Assert.True((await PageAsync(coordinator, id, token)).GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());
        using (var stale = await StepAsync(coordinator, id, "no-show", new { rowVersion = version.AddSeconds(-1) }, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        Assert.Empty(await SessionsOfAsync(id, token));
        Assert.Empty(await SessionsOfAsync(later, token));
    }

    /// <summary>
    /// The reviewer's point 1 on #146, the backbone through the endpoint (A7b): the coordinator — <c>Training.Conduct</c> and
    /// <c>Training.Edit</c>, neither <c>Approve</c> nor <c>Assign</c>, which the write guard would let through anyway — is not the
    /// trainer of a training and conducts it all the same: the session rescheduled, then the date set again by hand. On a training
    /// assigned to somebody else <c>Training.Conduct</c> is worth <c>Training.Edit</c>, in the single handler (the area of the
    /// permission) and in the write guard (the area the training declares, <c>[PermissionArea("Training")]</c>): without that
    /// declaration the guard would ask <c>Trainings.Edit</c>, which nobody holds, and refuse what the endpoint allowed. The assistant
    /// holds the same by position (design M3 §3.2).
    /// </summary>
    [Fact]
    public async Task TheCoordinatorConductsATrainingAssignedToSomebodyElseThroughTheEndpoint()
    {
        var token = TestContext.Current.CancellationToken;
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var id = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(-1));

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var page = await PageAsync(coordinator, id, token);
        Assert.Equal(TrainerVid, page.GetProperty("trainer").GetProperty("vid").GetInt32());
        Assert.True(page.GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());

        var rescheduled = await DoneAsync(await StepAsync(coordinator, id, "reschedule", Body("reschedule", page.GetProperty("rowVersion").GetDateTime()), token), token);
        Assert.Equal(nameof(TrainingState.Assigned), rescheduled.GetProperty("state").GetString());
        Assert.True(rescheduled.GetProperty("actions").GetProperty("canConduct").GetBoolean());

        var starts = DateTime.UtcNow.AddDays(3);
        var dated = await DoneAsync(
            await StepAsync(coordinator, id, "date", new { startsAtUtc = starts, confirmed = true, rowVersion = rescheduled.GetProperty("rowVersion").GetDateTime() }, token),
            token);
        Assert.Equal(nameof(TrainingState.Scheduled), dated.GetProperty("state").GetString());

        // Written by the coordinator, and still the trainer's: conducting a training gives it to nobody.
        var stored = await StoredAsync(id, token);
        Assert.Equal((TrainerVid, CoordinatorVid), (stored.TrainerVid, stored.UpdatedBy));
        Assert.Single(await SessionsOfAsync(id, token));
    }

    /// <summary>
    /// A session that has started is recorded — reported, a no-show or rescheduled —, never dated again by hand nor closed over (the
    /// maintainer's answer on #149): both are refused on the state, the page no longer offers them, and nothing is written. Nor is a
    /// training that waits for its date dated in the past, which would be a session nobody held.
    /// </summary>
    [Fact]
    public async Task ASessionThatHasStartedIsRecordedNeitherDatedAgainNorClosed()
    {
        var token = TestContext.Current.CancellationToken;
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);

        // The coordinator closes trainings too, here: without Training.Approve the close would be forbidden, not refused.
        await GrantAsync(CoordinatorVid, TrainingPermissions.Approve, token);
        var started = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: CoordinatorVid, start: DateTime.UtcNow.AddMinutes(-30));
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var page = await PageAsync(coordinator, started, token);
        var version = page.GetProperty("rowVersion").GetDateTime();
        Assert.False(page.GetProperty("actions").GetProperty("canConduct").GetBoolean());
        Assert.False(page.GetProperty("actions").GetProperty("canClose").GetBoolean());
        Assert.True(page.GetProperty("actions").GetProperty("canRecordOutcome").GetBoolean());

        using (var moved = await StepAsync(coordinator, started, "date", new { startsAtUtc = DateTime.UtcNow.AddDays(1), confirmed = true, rowVersion = version }, token))
        {
            await AssertRefusedAsync(moved, "state", TrainingDates.SessionStarted, token);
        }

        using (var closed = await StepAsync(coordinator, started, "close", new { reason = "trn-test: closed over a session", rowVersion = version }, token))
        {
            await AssertRefusedAsync(closed, "state", TrainingDates.SessionStarted, token);
        }

        var stored = await StoredAsync(started, token);
        Assert.Equal(TrainingState.Scheduled, stored.State);
        Assert.Equal(version, stored.RowVersion);

        // A training that waits for its date is not dated in the past either.
        var waiting = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: CoordinatorVid);
        var waitingVersion = (await PageAsync(coordinator, waiting, token)).GetProperty("rowVersion").GetDateTime();
        using (var past = await StepAsync(coordinator, waiting, "date", new { startsAtUtc = DateTime.UtcNow.AddMinutes(-10), confirmed = true, rowVersion = waitingVersion }, token))
        {
            await AssertRefusedAsync(past, "startsAtUtc", TrainingDates.SlotPassed, token);
        }

        Assert.Equal(TrainingState.Assigned, (await StoredAsync(waiting, token)).State);
        Assert.Empty(await SessionsOfAsync(started, token));
    }

    /// <summary>
    /// The test of note <c>le-note-riservate-e-il-trainee</c> (§4; design M3 §10, §12 n.13): a trainer — <c>Training.View</c> by their
    /// position — who is also a trainee reads the staff's page of their own training without what is reserved, and another trainee's
    /// with it; the fields are the ones <see cref="ReservedOnThePage"/> lists. From their own endpoints, a trainee reads none of them.
    /// </summary>
    [Fact]
    public async Task ATrainerWhoIsATraineeReadsTheReservedNotesOfAnotherTrainingAndNotThoseOfTheirOwn()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var item = await AddItemAsync(atc, SheetSection.Practice, "hold short", sort: 911, token);

        // Two trainings with everything reserved written on them — the notes of a session rescheduled, the note on an item, the comment
        // for the staff —: the trainer-trainee's own, and another trainee's, both conducted by the coordinator.
        var own = await ReportedWithNotesAsync(TrainerTraineeVid, item, "own", token);
        var another = await ReportedWithNotesAsync(OtherTraineeVid, item, "another", token);

        using var trainerTrainee = await SignedInAsync(TrainerTraineeVid, token);

        // Another trainee's: every reserved field, as written.
        var theirs = await PageAsync(trainerTrainee, another, token);
        Assert.False(theirs.GetProperty("reservedLeftOut").GetBoolean());
        Assert.Equal(
            ["trn-test-reserved: another, staff", "trn-test-reserved: another, item", "trn-test-reserved: another, session"],
            ReservedValues(theirs, item));

        // Their own: readable — the row is theirs to read like any other —, and every reserved field left out.
        var mine = await PageAsync(trainerTrainee, own, token);
        Assert.True(mine.GetProperty("reservedLeftOut").GetBoolean());
        Assert.Equal(new string?[] { null, null, null }, ReservedValues(mine, item));
        Assert.DoesNotContain("trn-test-reserved", mine.ToString(), StringComparison.Ordinal);

        // What they read anyway stays: the grade and the comment for them, the general comment, the sessions.
        Assert.Equal("trn-test: own, general", mine.GetProperty("generalComment").GetString());
        var graded = Mine(mine.GetProperty("sheet")).Single(each => each.GetProperty("itemId").GetInt64() == item);
        Assert.Equal((3, "trn-test: own, item"), (graded.GetProperty("grade").GetInt32(), graded.GetProperty("traineeComment").GetString()));
        Assert.Equal(2, mine.GetProperty("sessions").GetArrayLength());

        // The list of the staff reads both, as before: this is the shape of an answer, not a refusal.
        using (var list = await trainerTrainee.GetAsync(new Uri($"{StaffEndpoints.QueuePattern}?filter[traineeVid]={TrainerTraineeVid}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        }

        // From their own endpoints, nothing reserved ever: the page of the training and their page of trainings.
        var fromMine = await ReadMineAsync(trainerTrainee, own, token);
        AssertNothingReserved(fromMine.Text);
        AssertNothingReserved(await trainerTrainee.GetStringAsync(RequestEndpoints.Pattern, token));
    }

    /// <summary>
    /// The «done when» of A9 (<c>08-piano-implementazione-m3.md</c>), through the API: the trainer publishes a report with an item not
    /// applicable and «ready for the mock exam»; the trainee reads it without anything reserved; and their next request on the rating
    /// is a mock exam — the page says «this will be a mock exam, as agreed with the trainer» (A6b) —, whose own report cannot mark it
    /// ready for another. The pages are A9b's round on the bench.
    /// </summary>
    [Fact]
    public async Task TheTrainerReportsAnItemNotApplicableAndReadyForTheMockExamAndTheNextRequestIsAMockExam()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        await WriteSettingsAsync(new { conflictPolicy = "None", cooldownDays = 5 }, token);
        var flown = await AddItemAsync(pilot, SheetSection.Practice, "circuit", sort: 921, token);
        var untouched = await AddItemAsync(pilot, SheetSection.Theory, "weather", sort: 922, token);

        var id = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(-3));

        // The report: one item graded, the other left not applicable, ready for the mock exam, the waiting taken away.
        using var trainer = await SignedInAsync(TrainerVid, token);
        var version = (await PageAsync(trainer, id, token)).GetProperty("rowVersion").GetDateTime();
        await DoneAsync(
            await StepAsync(
                trainer,
                id,
                "report",
                Report(
                    version,
                    [Entry(flown, grade: 5, comment: "trn-test: a tidy circuit", note: "trn-test-reserved: ready indeed")],
                    general: "trn-test: ready for the mock exam",
                    staff: "trn-test-reserved: book the mock exam soon",
                    readyForMockExam: true,
                    cooldownWaived: true),
                token),
            token);

        // The trainee reads the report: the item not applicable, the box — and nothing reserved.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var report = await ReadMineAsync(trainee, id, token);
        Assert.True(report.Element.GetProperty("readyForMockExam").GetBoolean());
        var notApplicable = report.Element.GetProperty("sheet").EnumerateArray()
            .Single(each => each.GetProperty("title").GetProperty("en").GetString()!.StartsWith($"{ItemStem} weather", StringComparison.Ordinal));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (notApplicable.GetProperty("grade").ValueKind, notApplicable.GetProperty("mark").ValueKind));
        AssertNothingReserved(report.Text);
        Assert.Contains(untouched, await EvaluatedItemsAsync(id, token));

        // Their next request on the rating is a mock exam, as agreed with the trainer: the page says so, and the training is one.
        var path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Pilot);
        Assert.Equal(JsonValueKind.Null, path.GetProperty("refusal").ValueKind);
        Assert.True(path.GetProperty("isMockExam").GetBoolean());

        using var request = await trainee.PostAsJsonAsync(
            RequestEndpoints.Pattern,
            new { kind = nameof(RatingKind.Pilot), rating = pilot.Number, position = (string?)null, availabilityText = "trn-test", notesText = (string?)null, theoryPassed = true },
            token);
        var asked = await ReadAsync(request, HttpStatusCode.Created, token);
        Assert.True(asked.GetProperty("isMockExam").GetBoolean());
        var mockExam = asked.GetProperty("id").GetInt64();

        // The mail of the report said so: the page of the training, and the box — "mock exam" is the same in both languages.
        var mail = await MailAsync(TraineeVid, TrainingNotifications.ReportPublished, token);
        Assert.Contains($"/training/mine/{id}", mail, StringComparison.Ordinal);
        Assert.Contains("mock exam", mail, StringComparison.Ordinal);
        AssertNothingReserved(mail);

        // The mock exam's own report may not mark it ready for another: the next one after a mock exam is a training again. Given to
        // the same trainer, it is theirs to report in the session they already have: nothing about them changed (A7b).
        await DateInThePastAsync(mockExam, TrainerVid, token);
        var mockVersion = (await PageAsync(trainer, mockExam, token)).GetProperty("rowVersion").GetDateTime();
        using (var refused = await StepAsync(trainer, mockExam, "report", Report(mockVersion, [], readyForMockExam: true), token))
        {
            await AssertRefusedAsync(refused, "readyForMockExam", TrainingSessions.MockExamAgain, token);
        }

        await DoneAsync(await StepAsync(trainer, mockExam, "report", Report(mockVersion, [], readyForExam: true, cooldownWaived: true), token), token);
        path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Pilot);
        Assert.False(path.GetProperty("isMockExam").GetBoolean());
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>A report as the page sends it.</summary>
    private static object Report(
        DateTime version,
        object[] sheet,
        string? general = null,
        string? staff = null,
        bool readyForMockExam = false,
        bool readyForExam = false,
        bool cooldownWaived = false) =>
        new
        {
            sheet,
            generalComment = general,
            staffComment = staff,
            readyForMockExam,
            readyForExam,
            cooldownWaived,
            rowVersion = version,
        };

    /// <summary>The least each verb after the session sends, at that version: no notes, an empty report.</summary>
    private static object Body(string verb, DateTime version) => verb switch
    {
        "reschedule" => new { notes = (string?)null, rowVersion = version },
        "no-show" => new { rowVersion = version },
        _ => Report(version, []),
    };

    private static object Entry(long item, int? grade = null, TheoryMark? mark = null, string? comment = null, string? note = null) =>
        new { itemId = item, grade, mark = mark?.ToString(), traineeComment = comment, staffNote = note };

    /// <summary>
    /// A training of <paramref name="trainee"/> taken through every step that writes something reserved, by the coordinator: a session
    /// rescheduled with its notes, a date set by hand a moment ago, and the report with a note on the item and a comment for the staff.
    /// </summary>
    private async Task<long> ReportedWithNotesAsync(int trainee, long item, string whose, CancellationToken cancellationToken)
    {
        var id = await AddTrainingAsync(trainee, RatingKind.Atc, TrainingState.Scheduled, cancellationToken, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(-2));
        using var coordinator = await SignedInAsync(CoordinatorVid, cancellationToken);

        var version = (await PageAsync(coordinator, id, cancellationToken)).GetProperty("rowVersion").GetDateTime();
        var rescheduled = await DoneAsync(await StepAsync(coordinator, id, "reschedule", new { notes = $"trn-test-reserved: {whose}, session", rowVersion = version }, cancellationToken), cancellationToken);
        await DoneAsync(
            await StepAsync(coordinator, id, "date", new { startsAtUtc = DateTime.UtcNow.AddHours(1), confirmed = true, rowVersion = rescheduled.GetProperty("rowVersion").GetDateTime() }, cancellationToken),
            cancellationToken);
        await StartedAMomentAgoAsync(id, cancellationToken);
        var set = await PageAsync(coordinator, id, cancellationToken);
        await DoneAsync(
            await StepAsync(
                coordinator,
                id,
                "report",
                Report(
                    set.GetProperty("rowVersion").GetDateTime(),
                    [Entry(item, grade: 3, comment: $"trn-test: {whose}, item", note: $"trn-test-reserved: {whose}, item")],
                    general: $"trn-test: {whose}, general",
                    staff: $"trn-test-reserved: {whose}, staff"),
                cancellationToken),
            cancellationToken);

        return id;
    }

    /// <summary>The value of each field of <see cref="ReservedOnThePage"/>, in its order: on the item of this class, and on the session rescheduled.</summary>
    private static List<string?> ReservedValues(JsonElement page, long item)
    {
        Assert.Equal(["staffComment", "sheet[].staffNote", "sessions[].internalNotes"], ReservedOnThePage);

        var onTheItem = page.GetProperty("sheet").EnumerateArray().Single(each => each.GetProperty("itemId").GetInt64() == item);
        var rescheduled = page.GetProperty("sessions").EnumerateArray()
            .Single(each => each.GetProperty("outcome").GetString() == nameof(SessionOutcome.Rescheduled));

        return
        [
            page.GetProperty("staffComment").GetString(),
            onTheItem.GetProperty("staffNote").GetString(),
            rescheduled.GetProperty("internalNotes").GetString(),
        ];
    }

    /// <summary>
    /// Nothing reserved in what a trainee reads: none of the words this class writes into a reserved field, and none of the reserved
    /// fields' names — their endpoints have a DTO without them.
    /// </summary>
    private static void AssertNothingReserved(string text)
    {
        Assert.DoesNotContain("trn-test-reserved", text, StringComparison.Ordinal);
        Assert.DoesNotContain("staffComment", text, StringComparison.Ordinal);
        Assert.DoesNotContain("staffNote", text, StringComparison.Ordinal);
        Assert.DoesNotContain("internalNotes", text, StringComparison.Ordinal);
    }

    /// <summary>The items of a sheet this class wrote, in their order: others of the same rating may be on it too.</summary>
    private static List<JsonElement> Mine(JsonElement sheet) =>
        [.. sheet.EnumerateArray().Where(item => item.GetProperty("title").GetProperty("en").GetString()!.StartsWith(ItemStem, StringComparison.Ordinal))];

    private static JsonElement Path(JsonElement mine, RatingKind kind) =>
        mine.GetProperty("paths").EnumerateArray().Single(path => path.GetProperty("kind").GetString() == kind.ToString());

    private static Task<HttpResponseMessage> StepAsync(HttpClient client, long id, string verb, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{StaffEndpoints.Pattern}/{id}/{verb}", UriKind.Relative), body, cancellationToken);

    private static async Task<JsonElement> PageAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"{StaffEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken);

    /// <summary>One training as its trainee reads it: the answer, and its text as it came.</summary>
    private static async Task<(JsonElement Element, string Text)> ReadMineAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        var text = await client.GetStringAsync(new Uri($"{RequestEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken);
        return (JsonDocument.Parse(text).RootElement.Clone(), text);
    }

    private static Task<JsonElement> DoneAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        ReadAsync(response, HttpStatusCode.OK, cancellationToken);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
            return JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static IEnumerable<string?> Keys(JsonElement problem, string field) =>
        problem.GetProperty("errors").TryGetProperty(field, out var keys) ? keys.EnumerateArray().Select(key => key.GetString()) : [];

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        using (response)
        {
            var problem = await ProblemAsync(response, cancellationToken);
            Assert.Contains(key, Keys(problem, field));
        }
    }

    private async Task<Training> StoredAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(training => training.Id == id, cancellationToken);
    }

    private async Task<List<TrainingSession>> SessionsOfAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Sessions.AsNoTracking()
            .Where(session => session.TrainingId == id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<TrainingEvaluation>> EvaluationsOfAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Evaluations.AsNoTracking()
            .Where(evaluation => evaluation.TrainingId == id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<long>> EvaluatedItemsAsync(long id, CancellationToken cancellationToken) =>
        [.. (await EvaluationsOfAsync(id, cancellationToken)).Select(evaluation => evaluation.SheetItemId)];

    /// <summary>How many items the sheet of a rating has now: the ones a report marks.</summary>
    private async Task<int> ActiveItemsAsync(Rating rating, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().SheetItems.IgnoreQueryFilters()
            .CountAsync(item => item.Kind == rating.Kind && item.Rating == rating.Number && item.IsActive, cancellationToken);
    }

    /// <summary>The entries of the calendar a training projects, whoever may read them.</summary>
    private async Task<List<CalendarEntry>> CalendarOfAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var source = Training.SourceIdOf(id);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && entry.SourceId == source)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A training of the first trained rating of its ladder, written as the installation straight into the state the test starts from
    /// — past the request, the assignment and the date, which A6a's, A7's and A8a's tests prove.
    /// </summary>
    private async Task<long> AddTrainingAsync(
        int trainee,
        RatingKind kind,
        TrainingState state,
        CancellationToken cancellationToken,
        int? trainer = null,
        DateTime? start = null)
    {
        var (atc, pilot) = Trained();
        var now = DateTime.UtcNow;

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = new Training
        {
            Kind = kind,
            Rating = kind == RatingKind.Pilot ? pilot.Number : atc.Number,
            Position = kind == RatingKind.Pilot ? null : "TRNTEST_POS",
            TraineeVid = trainee,
            TraineeHoursAtRequest = 500m,
            TheoryConfirmedAt = now,
            State = state,
            DecidedBy = CoordinatorVid,
            DecidedAt = now,
            TrainerVid = trainer,
            AssignedBy = trainer is null ? null : CoordinatorVid,
            AssignedAt = trainer is null ? null : now,
            ScheduledStartUtc = start,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

    /// <summary>
    /// A dated training whose session the installation moves to ten minutes ago, so that it may be recorded: nobody sets a date in
    /// the past by hand (the maintainer's answer on #149).
    /// </summary>
    private async Task StartedAMomentAgoAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = await database.Trainings.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken);
        training.ScheduledStartUtc = DateTime.UtcNow.AddMinutes(-10);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A training requested through the API, taken by the installation to a session held a moment ago with its trainer.</summary>
    private async Task DateInThePastAsync(long id, int trainer, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = await database.Trainings.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken);
        training.State = TrainingState.Scheduled;
        training.DecidedBy = CoordinatorVid;
        training.DecidedAt = DateTime.UtcNow;
        training.TrainerVid = trainer;
        training.AssignedBy = CoordinatorVid;
        training.AssignedAt = DateTime.UtcNow;
        training.ScheduledStartUtc = DateTime.UtcNow.AddHours(-1);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>An item of the sheet of a rating, written as the installation, with this class's stem in its title in every language.</summary>
    private async Task<long> AddItemAsync(Rating rating, SheetSection section, string title, int sort, CancellationToken cancellationToken, bool active = true)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var item = new SheetItem
        {
            Kind = rating.Kind,
            Rating = rating.Number,
            Section = section,
            Title = new Localized<string>(Division().Locales.Select(locale => KeyValuePair.Create(locale, $"{ItemStem} {title} ({locale})"))),
            Sort = sort,
            IsActive = active,
        };

        database.SheetItems.Add(item);
        await database.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    private async Task RenameItemAsync(long id, string title, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var item = await database.SheetItems.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken);
        item.Title = new Localized<string>(Division().Locales.Select(locale => KeyValuePair.Create(locale, title)));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> MailsAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .CountAsync(notification => notification.Vid == vid && notification.Type == type, cancellationToken);
    }

    /// <summary>What the last mail of that type to that member says, as its data holds it.</summary>
    private async Task<string> MailAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mail = await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(notification => notification.Vid == vid && notification.Type == type)
            .OrderByDescending(notification => notification.Id)
            .FirstAsync(cancellationToken);
        return mail.DataJson;
    }

    /// <summary>The settings of the module as a saved row holds them: only what is written, over the defaults.</summary>
    private async Task WriteSettingsAsync(object values, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey);

        var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        if (row is null)
        {
            row = new DivisionSetting { Key = key };
            database.DivisionSettings.Add(row);
        }

        row.ValueJson = JsonSerializer.Serialize(values);
        row.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The first rating of each ladder with a practical training: what the trainings of this class are for.</summary>
    private (Rating Atc, Rating Pilot) Trained()
    {
        using var scope = _factory.Services.CreateScope();
        var ratings = scope.ServiceProvider.GetRequiredService<TrainingReference>().Ratings;
        return (ratings.First(rating => rating.Kind == RatingKind.Atc), ratings.First(rating => rating.Kind == RatingKind.Pilot));
    }

    private IReadOnlyList<Rating> Ladder(RatingKind kind)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RatingVocabulary>().Ladder(kind);
    }

    /// <summary>The rung below a rating on its ladder: the trainee's, for whom it is the next training.</summary>
    private Rating RungBelow(Rating rating)
    {
        var ladder = Ladder(rating.Kind);
        var index = ladder.ToList().FindIndex(rung => rung.Number == rating.Number);
        Assert.True(index > 0, "The first trained rating has a rung below it.");
        return ladder[index - 1];
    }

    private DivisionOptions Division() => _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value;

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>
    /// A person of this class: their name, a mailbox only when a test looks for their mail, whether the hub counts them as staff,
    /// their ratings, and a position as the network spells it.
    /// </summary>
    private async Task SeedPersonAsync(
        int vid,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken,
        bool isStaff = false,
        int? atc = null,
        int? pilot = null,
        string? position = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        user.IsStaff = isStaff;
        user.IsSuperadmin = false;
        user.RatingAtc = atc;
        user.RatingPilot = pilot;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null)
        {
            var parsed = StaffRoleMap.Parse(position, Division().Code, new HashSet<string>());
            Assert.NotNull(parsed);
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = parsed.Raw,
                Department = parsed.Department,
                Level = parsed.Level,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A permission on the training department, to one VID.</summary>
    private async Task GrantAsync(int vid, string permission, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.TD,
            Effect = GrantEffect.Grant,
            Reason = GrantReason,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What this class leaves: the trainings of its people with their dates, sessions and sheets and their entries of the calendar, its
    /// items of the sheet, the grants and positions of its people, their mails, the settings, and the addresses — so no later class
    /// counts them.
    /// </summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var training = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var ids = await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).Select(row => row.Id).ToListAsync(cancellationToken);
        var sources = ids.Select(Training.SourceIdOf).ToList();
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && sources.Contains(entry.SourceId))
            .ExecuteDeleteAsync(cancellationToken);

        // The dates, the sessions and the sheets go with their trainings (the keys cascade).
        await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await training.Database.ExecuteSqlAsync($"DELETE FROM trn_sheet_items WHERE title_i18n LIKE {$"%{ItemStem}%"}", cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => Vids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => Vids.Contains(user.Vid))
            .ExecuteUpdateAsync(user => user.SetProperty(row => row.Email, (string?)null), cancellationToken);

        var key = ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey);
        await hub.DivisionSettings.Where(setting => setting.Key == key).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Two moments of a session as the same: the database keeps microseconds, the clock of the test ticks finer.</summary>
    private sealed class InstantComparer : IEqualityComparer<(string?, DateTime)>
    {
        public bool Equals((string?, DateTime) x, (string?, DateTime) y) =>
            x.Item1 == y.Item1 && Math.Abs((x.Item2 - y.Item2).TotalMilliseconds) < 1;

        public int GetHashCode((string?, DateTime) obj) => obj.Item1?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
