using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The date of a training (M3, A8; design M3 §2.5, §5.1, §5.3), through the real host: the trainer's dates with their warnings,
/// confirmed under <c>Warn</c>, refused under <c>Block</c>, not looked at under <c>None</c>; the trainee's choice among them; the date
/// set by hand by whoever conducts the training, and by nobody else; the session in the public calendar with nobody's name, which
/// follows the date and leaves when the training closes without a session held; the reminder, once per date; the closing by the
/// night only when the division sets a time to choose, and by the staff with a reason; and the «done when» of the phase, the whole
/// round through the API.
/// <para>⚠️ The staff of the training is seeded without an address (<c>CONTRIBUTING.md</c>, "Tests"): the trainer whose mail is
/// looked for holds a position of the direction instead, as in <c>TrainingStaffTests</c> — the direction conducts every training
/// —, and the trainer who conducts by the grant on one training alone has no mailbox. The entries of the calendar this class
/// writes are of a kind of its own, so that nothing another class leaves behind warns a date of this one.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingDatesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790031 are A1's to A7's, 790040–790044
    // and 790050–790051 A3b's; 790072–790079 were handed to the review of A8a.
    private const int TraineeVid = 790032;
    private const int TrainerVid = 790033;
    private const int ScopedTrainerVid = 790034;
    private const int CoordinatorVid = 790035;
    private const int AdvisorVid = 790036;
    private const int OtherTraineeVid = 790037;
    private const int StrangerVid = 790038;
    private const int UnassignedTrainerVid = 790072;
    private const int StaffTraineeVid = 790073;

    private static readonly int[] Vids =
        [TraineeVid, TrainerVid, ScopedTrainerVid, CoordinatorVid, AdvisorVid, OtherTraineeVid, StrangerVid, UnassignedTrainerVid, StaffTraineeVid];

    private const string GrantReason = "trn-test";

    /// <summary>A kind of the calendar nobody else writes: the entries of other classes never warn these dates.</summary>
    private const string EventKind = "trn-test-event";

    private const string EventSourcePrefix = "trn-test-";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, entries, grants and positions.
        await CleanAsync(token);

        var (topAtc, topPilot) = (Ladder(RatingKind.Atc)[^1], Ladder(RatingKind.Pilot)[^1]);
        var code = Division().Code;

        // The trainees: the first has a mailbox for the mails of the dates.
        await SeedPersonAsync(TraineeVid, "Test", "Trainee", $"trn-test-{TraineeVid}@example.invalid", token);
        await SeedPersonAsync(OtherTraineeVid, "Other", "Trainee", email: null, token);
        await SeedPersonAsync(StrangerVid, "Some", "Member", email: null, token);

        // The trainer whose mail is looked for: the direction, which conducts every training, with a mailbox.
        await SeedPersonAsync(
            TrainerVid, "Mailed", "Trainer", $"trn-test-{TrainerVid}@example.invalid", token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-ADIR");

        // A trainer of the department with no mailbox, who conducts only by the grant on one training.
        await SeedPersonAsync(
            ScopedTrainerVid, "Scoped", "Trainer", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-T94");

        // The coordinator conducts and assigns every training, the advisor approves and closes: grants to their VID.
        await SeedPersonAsync(CoordinatorVid, "Test", "Coordinator", email: null, token, isStaff: true);
        await GrantAsync(CoordinatorVid, TrainingPermissions.View, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Assign, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Conduct, token);
        await SeedPersonAsync(AdvisorVid, "Test", "Advisor", email: null, token, isStaff: true);
        await GrantAsync(AdvisorVid, TrainingPermissions.View, token);
        await GrantAsync(AdvisorVid, TrainingPermissions.Approve, token);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ADateWithAWarningIsConfirmedUnderWarnRefusedUnderBlockAndNotLookedAtUnderNone()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(30);

        // That day: another training's session, whoever trains it, and an event of a kind the division checks.
        var other = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: ScopedTrainerVid, start: day.AddHours(10));
        await SeedEventAsync(day.AddHours(19), day.AddHours(21), token);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await GiveConductAsync(ScopedTrainerVid, id, token);

        // "training" among the kinds too: the other session is one warning, not two.
        await WriteSettingsAsync(new { conflictPolicy = "Warn", conflictKinds = new[] { EventKind, Training.CalendarKind } }, token);

        using var trainer = await SignedInAsync(ScopedTrainerVid, token);

        // What the date meets, before anybody writes it: the other session by its rating and position, and the event.
        var conflicts = await trainer.GetFromJsonAsync<JsonElement>(Conflicts(id, day.AddHours(16), day.AddHours(18)), token);
        Assert.Equal("Warn", conflicts.GetProperty("policy").GetString());
        var found = conflicts.GetProperty("warnings").EnumerateArray().ToList();
        Assert.Equal(["Training", "Calendar"], found.Select(warning => warning.GetProperty("kind").GetString()));
        Assert.Equal(other, found[0].GetProperty("trainingId").GetInt64());
        Assert.Equal(EventKind, found[1].GetProperty("calendarKind").GetString());
        Assert.Equal("trn-test evening", found[1].GetProperty("title").GetProperty("en").GetString());

        // The day after meets nothing.
        var nextDay = await trainer.GetFromJsonAsync<JsonElement>(Conflicts(id, day.AddDays(1).AddHours(16), day.AddDays(1).AddHours(18)), token);
        Assert.Empty(nextDay.GetProperty("warnings").EnumerateArray());

        // Proposed without the confirmation the policy asks for: refused, and nothing written.
        var version = (await PageAsync(trainer, id, token)).GetProperty("rowVersion").GetDateTime();
        object[] both = [Slot(day.AddHours(16), day.AddHours(18)), Slot(day.AddDays(1).AddHours(16), day.AddDays(1).AddHours(18))];
        using (var unconfirmed = await StepAsync(trainer, id, "slots", new { slots = both, confirmed = false, rowVersion = version }, token))
        {
            await AssertRefusedAsync(unconfirmed, "confirmed", DateConflicts.NotConfirmed, token);
        }

        Assert.Empty(await SlotsOfAsync(id, token));

        // Confirmed: both written, each with its own warnings and who proposed it; the trainee written to once.
        var page = await DoneAsync(await StepAsync(trainer, id, "slots", new { slots = both, confirmed = true, rowVersion = version }, token), token);
        var slots = page.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(2, slots.Count);
        Assert.Equal(2, slots[0].GetProperty("warnings").GetArrayLength());
        Assert.Equal(0, slots[1].GetProperty("warnings").GetArrayLength());
        Assert.Equal(ScopedTrainerVid, slots[0].GetProperty("proposedBy").GetProperty("vid").GetInt32());
        Assert.True(page.GetProperty("actions").GetProperty("canConduct").GetBoolean());
        Assert.False(page.GetProperty("actions").GetProperty("canClose").GetBoolean());

        // The data of a mail is JSON, which writes the dash between the hours escaped: the moments are looked for.
        var proposed = await MailAsync(TraineeVid, TrainingNotifications.DatesProposed, token);
        Assert.Contains($"/training/mine/{id}", proposed, StringComparison.Ordinal);
        Assert.Contains(TrainingMail.Moment(day.AddHours(16)), proposed, StringComparison.Ordinal);
        Assert.Contains(TrainingMail.Moment(day.AddDays(1).AddHours(16)), proposed, StringComparison.Ordinal);

        // What a date keeps names nobody: not the other trainee, not their trainer.
        foreach (var stored in await SlotsOfAsync(id, token))
        {
            Assert.DoesNotContain(OtherTraineeVid.ToString(CultureInfo.InvariantCulture), stored.WarningsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Other", stored.WarningsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Scoped", stored.WarningsJson, StringComparison.Ordinal);
        }

        // Block: a date that meets the other session is refused on its own row, confirmed or not.
        await WriteSettingsAsync(new { conflictPolicy = "Block", conflictKinds = new[] { EventKind } }, token);
        version = page.GetProperty("rowVersion").GetDateTime();
        using (var blocked = await StepAsync(trainer, id, "slots", new { slots = new[] { Slot(day.AddHours(12), day.AddHours(14)) }, confirmed = true, rowVersion = version }, token))
        {
            await AssertRefusedAsync(blocked, "slots[0].startsAtUtc", DateConflicts.Blocked, token);
        }

        // None: nobody looks — written with no warning and no confirmation.
        await WriteSettingsAsync(new { conflictPolicy = "None", conflictKinds = new[] { EventKind } }, token);
        var unchecked_ = await DoneAsync(await StepAsync(trainer, id, "slots", new { slots = new[] { Slot(day.AddHours(12), day.AddHours(14)) }, confirmed = false, rowVersion = version }, token), token);
        var added = unchecked_.GetProperty("slots").EnumerateArray().Single(slot => slot.GetProperty("startsAtUtc").GetDateTime() == day.AddHours(12));
        Assert.Equal(0, added.GetProperty("warnings").GetArrayLength());
        Assert.Equal(2, await MailsAsync(TraineeVid, TrainingNotifications.DatesProposed, token));

        // A date gone by, one ending before it starts, one proposed twice: each on its row.
        version = unchecked_.GetProperty("rowVersion").GetDateTime();
        object[] wrong = [Slot(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1)), Slot(day.AddDays(2), day.AddDays(2).AddHours(-1)), Slot(day.AddHours(16), day.AddHours(17))];
        using (var refused = await StepAsync(trainer, id, "slots", new { slots = wrong, confirmed = true, rowVersion = version }, token))
        {
            var problem = await ProblemAsync(refused, token);
            Assert.Contains(TrainingDates.SlotPassed, Keys(problem, "slots[0].startsAtUtc"));
            Assert.Contains(TrainingDates.SlotEndsBeforeItStarts, Keys(problem, "slots[1].endsAtUtc"));
            Assert.Contains(TrainingDates.SlotTwice, Keys(problem, "slots[2].startsAtUtc"));
        }

        // A date taken back; and dates are the business of whoever conducts the training, not of the trainee.
        var withdrawn = page.GetProperty("slots")[1].GetProperty("id").GetInt64();
        var after = await DoneAsync(await StepAsync(trainer, id, $"slots/{withdrawn}/withdraw", new { rowVersion = version }, token), token);
        Assert.DoesNotContain(withdrawn, after.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("id").GetInt64()));

        using var trainee = await SignedInAsync(TraineeVid, token);
        using (var own = await trainee.GetAsync(Conflicts(id, day.AddHours(16), null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }
    }

    /// <summary>
    /// What a date keeps of its warnings is read by whoever reads the training (review of A8a): whoever proposes it may read more of
    /// the calendar — the direction reads the entries each department keeps for itself —, so the date keeps what the staff at large
    /// may read, and an entry of one department stays with whoever was shown it.
    /// </summary>
    [Fact]
    public async Task ADateKeepsOnlyTheWarningsEveryReaderOfTheTrainingMayRead()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(35);

        // That evening: an event of the events department for its own people, and one for the whole staff.
        await SeedEventAsync(day.AddHours(19), day.AddHours(21), token, Visibility.Department, "trn-test department");
        await SeedEventAsync(day.AddHours(20), day.AddHours(22), token, Visibility.Staff, "trn-test staff");
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        await WriteSettingsAsync(new { conflictPolicy = "Warn", conflictKinds = new[] { EventKind } }, token);

        // The direction conducts every training and reads every department's entries: it is shown both, and confirms them.
        using var director = await SignedInAsync(TrainerVid, token);
        var shown = await director.GetFromJsonAsync<JsonElement>(Conflicts(id, day.AddHours(16), day.AddHours(18)), token);
        Assert.Equal(["trn-test department", "trn-test staff"], Titles(shown.GetProperty("warnings")));
        await ProposeAsync(director, id, token, Slot(day.AddHours(16), day.AddHours(18)));

        // The date keeps the staff's alone: a trainer of the department, who reads the training but not that entry, does not read
        // it through the date either.
        using var trainer = await SignedInAsync(ScopedTrainerVid, token);
        var slot = Assert.Single((await PageAsync(trainer, id, token)).GetProperty("slots").EnumerateArray());
        Assert.Equal(["trn-test staff"], Titles(slot.GetProperty("warnings")));
        Assert.DoesNotContain("trn-test department", Assert.Single(await SlotsOfAsync(id, token)).WarningsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTraineeChoosesADateAndItsSessionIsInThePublicCalendarWithoutANameUntilTheTrainingCloses()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(40);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);

        using var trainer = await SignedInAsync(TrainerVid, token);
        var (first, second) = (day.AddHours(16), day.AddDays(1).AddHours(16));
        await ProposeAsync(trainer, id, token, Slot(first, first.AddHours(2)), Slot(second, second.AddHours(2)));

        // The trainee reads the dates to choose from, and when — nothing of the warnings, which are the staff's.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var mine = await trainee.GetFromJsonAsync<JsonElement>(Mine(id), token);
        var offered = mine.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal([first, second], offered.Select(slot => slot.GetProperty("startsAtUtc").GetDateTime()));
        Assert.All(offered, slot => Assert.False(slot.TryGetProperty("warnings", out _)));
        Assert.Equal(TrainerVid, mine.GetProperty("trainer").GetProperty("vid").GetInt32());
        var version = mine.GetProperty("rowVersion").GetDateTime();
        var chosen = offered[1].GetProperty("id").GetInt64();

        // Nobody chooses for somebody else, nor a date of another training.
        using var stranger = await SignedInAsync(StrangerVid, token);
        using (var theirs = await stranger.PostAsJsonAsync(Choose(id), new { slotId = chosen, rowVersion = version }, token))
        {
            Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        }

        using (var unknown = await trainee.PostAsJsonAsync(Choose(id), new { slotId = -1L, rowVersion = version }, token))
        {
            await AssertRefusedAsync(unknown, "slotId", TrainingDates.SlotUnknown, token);
        }

        // Chosen: dated, the dates gone, both written to.
        using var response = await trainee.PostAsJsonAsync(Choose(id), new { slotId = chosen, rowVersion = version }, token);
        var dated = await DoneAsync(response, token);
        Assert.Equal(nameof(TrainingState.Scheduled), dated.GetProperty("state").GetString());
        Assert.Equal(second, dated.GetProperty("scheduledStartUtc").GetDateTime());
        Assert.Empty(dated.GetProperty("slots").EnumerateArray());
        Assert.False(dated.GetProperty("held").GetBoolean());

        var stored = await StoredAsync(id, token);
        Assert.Equal((chosen, (DateTime?)null), (stored.ChosenSlotId!.Value, stored.RemindedAt));
        Assert.Empty(await SlotsOfAsync(id, token));
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.DateConfirmed, token));
        Assert.Contains($"/staff/training/{id}", await MailAsync(TrainerVid, TrainingNotifications.DateConfirmed, token), StringComparison.Ordinal);

        var page = await PageAsync(trainer, id, token);
        Assert.True(page.GetProperty("dateChosenByTrainee").GetBoolean());
        Assert.Empty(page.GetProperty("slots").EnumerateArray());

        // The session in the calendar: public, at its start, by its rating and position — nobody's name, nobody's VID.
        var entry = Assert.Single(await CalendarOfAsync(id, token));
        Assert.Equal((Training.CalendarKind, Visibility.Public, second, $"/training/sessions/{id}"), (entry.Kind, entry.Visibility, entry.StartsAtUtc, entry.Url));
        var title = entry.Title.Get("en")!;
        Assert.Equal($"{Trained().Atc.ShortName} · TRNTEST_POS", title);
        foreach (var person in new[] { "Test", "Trainee", "Mailed", "Trainer", TraineeVid.ToString(CultureInfo.InvariantCulture), TrainerVid.ToString(CultureInfo.InvariantCulture) })
        {
            Assert.DoesNotContain(person, title, StringComparison.Ordinal);
        }

        // A visitor finds it in the calendar of the site.
        using var visitor = _factory.CreateApiClient();
        var calendar = await visitor.GetFromJsonAsync<JsonElement>(CalendarBlock(second.AddDays(-1), second.AddDays(1)), token);
        Assert.Contains(calendar.GetProperty("items").EnumerateArray(), item => item.GetProperty("url").GetString() == $"/training/sessions/{id}");

        // Dated, it is not chosen again.
        using (var again = await trainee.PostAsJsonAsync(Choose(id), new { slotId = chosen, rowVersion = dated.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(again, "state", TrainingDates.NotChoosable, token);
        }

        // Closed by the staff with a reason: the calendar lets its session go, the record keeps its date; the trainee reads why.
        using var advisor = await SignedInAsync(AdvisorVid, token);
        var closedVersion = (await PageAsync(advisor, id, token)).GetProperty("rowVersion").GetDateTime();
        using (var noReason = await StepAsync(advisor, id, "close", new { reason = "  ", rowVersion = closedVersion }, token))
        {
            await AssertRefusedAsync(noReason, "reason", "errors.required", token);
        }

        var closed = await DoneAsync(await StepAsync(advisor, id, "close", new { reason = "trn-test: no answer", rowVersion = closedVersion }, token), token);
        Assert.Equal(nameof(TrainingState.Closed), closed.GetProperty("state").GetString());
        Assert.Equal("trn-test: no answer", closed.GetProperty("closeReason").GetString());
        Assert.Equal(AdvisorVid, closed.GetProperty("closedBy").GetProperty("vid").GetInt32());
        Assert.Equal(second, closed.GetProperty("scheduledStartUtc").GetDateTime());
        Assert.True(closed.GetProperty("dateChosenByTrainee").GetBoolean());
        Assert.Empty(await CalendarOfAsync(id, token));
        Assert.Contains("trn-test: no answer", await MailAsync(TraineeVid, TrainingNotifications.TrainingClosed, token), StringComparison.Ordinal);

        var theirsNow = await trainee.GetFromJsonAsync<JsonElement>(Mine(id), token);
        Assert.Equal("trn-test: no answer", theirsNow.GetProperty("closeReason").GetString());

        using (var twice = await StepAsync(advisor, id, "close", new { reason = "trn-test again", rowVersion = closed.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(twice, "state", TrainingDates.NotClosable, token);
        }
    }

    [Fact]
    public async Task WhoeverConductsTheTrainingSetsItsDateByHandAndTheCalendarFollowsIt()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(50);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await GiveConductAsync(ScopedTrainerVid, id, token);
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A date proposed before: the one set by hand takes its place.
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            database.Slots.Add(new TrainingSlot { TrainingId = id, StartsAtUtc = day.AddHours(9), EndsAtUtc = day.AddHours(11) });
            await database.SaveChangesAsync(token);
        }

        // Not the trainee's, not the advisor's: they conduct nothing.
        using var trainee = await SignedInAsync(TraineeVid, token);
        using var advisor = await SignedInAsync(AdvisorVid, token);
        var version = (await StoredAsync(id, token)).RowVersion;
        foreach (var nobody in new[] { trainee, advisor })
        {
            using var refused = await StepAsync(nobody, id, "date", new { startsAtUtc = day.AddHours(15), confirmed = false, rowVersion = version }, token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The trainer, by the grant on this training: dated by hand, the dates proposed gone, the calendar at the date.
        using var trainer = await SignedInAsync(ScopedTrainerVid, token);
        var set = await DoneAsync(await StepAsync(trainer, id, "date", new { startsAtUtc = day.AddHours(15), confirmed = false, rowVersion = version }, token), token);
        Assert.Equal(nameof(TrainingState.Scheduled), set.GetProperty("state").GetString());
        Assert.Equal(day.AddHours(15), set.GetProperty("scheduledStartUtc").GetDateTime());
        Assert.False(set.GetProperty("dateChosenByTrainee").GetBoolean());
        Assert.Empty(set.GetProperty("slots").EnumerateArray());
        Assert.Null((await StoredAsync(id, token)).ChosenSlotId);
        Assert.Equal(day.AddHours(15), Assert.Single(await CalendarOfAsync(id, token)).StartsAtUtc);
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.DateConfirmed, token));

        // The trainer may not close it: closing is the staff's who approve.
        using (var close = await StepAsync(trainer, id, "close", new { reason = "trn-test", rowVersion = set.GetProperty("rowVersion").GetDateTime() }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, close.StatusCode);
        }

        // The coordinator, who conducts every training, moves it again — among the warnings of Warn, confirmed —: the calendar follows.
        var other = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: day.AddDays(2).AddHours(8));
        await WriteSettingsAsync(new { conflictPolicy = "Warn", conflictKinds = new[] { EventKind } }, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        version = set.GetProperty("rowVersion").GetDateTime();
        using (var unconfirmed = await StepAsync(coordinator, id, "date", new { startsAtUtc = day.AddDays(2).AddHours(18), confirmed = false, rowVersion = version }, token))
        {
            await AssertRefusedAsync(unconfirmed, "confirmed", DateConflicts.NotConfirmed, token);
        }

        var moved = await DoneAsync(await StepAsync(coordinator, id, "date", new { startsAtUtc = day.AddDays(2).AddHours(18), confirmed = true, rowVersion = version }, token), token);
        Assert.Equal(day.AddDays(2).AddHours(18), moved.GetProperty("scheduledStartUtc").GetDateTime());
        Assert.Equal(day.AddDays(2).AddHours(18), Assert.Single(await CalendarOfAsync(id, token)).StartsAtUtc);
        Assert.Single(await CalendarOfAsync(other, token));
        Assert.Equal(2, await MailsAsync(TraineeVid, TrainingNotifications.DateConfirmed, token));

        // A session held shows so from the day after its own, and nothing writes it.
        var yesterday = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddDays(-2));
        Assert.True((await PageAsync(coordinator, yesterday, token)).GetProperty("held").GetBoolean());
        Assert.False(moved.GetProperty("held").GetBoolean());
        using var otherTrainee = await SignedInAsync(OtherTraineeVid, token);
        Assert.True((await otherTrainee.GetFromJsonAsync<JsonElement>(Mine(yesterday), token)).GetProperty("held").GetBoolean());
    }

    /// <summary>
    /// §3.3 on the verbs of the dates (review of A8a): a trainer of the department reads every training and conducts only the one given
    /// to them — with <c>Training.View</c> by position and no grant on this training, they neither ask what a date meets nor propose,
    /// withdraw or set one. Nor does the trainee, not even one of the staff who conducts every other training: nobody conducts a
    /// training of their own (§3.1). Nothing is written.
    /// </summary>
    [Fact]
    public async Task OnlyWhoeverConductsTheTrainingTouchesItsDates()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(45);
        var (topAtc, topPilot) = (Ladder(RatingKind.Atc)[^1], Ladder(RatingKind.Pilot)[^1]);
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);

        // A trainer of the department who does not train this training: another trainer does, by the grant on it.
        await SeedPersonAsync(
            UnassignedTrainerVid, "Unassigned", "Trainer", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{Division().Code}-T93");
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await GiveConductAsync(ScopedTrainerVid, id, token);
        await AddSlotAsync(id, proposedDaysAgo: 1, token);
        var slot = Assert.Single(await SlotsOfAsync(id, token)).Id;

        // One of the staff of the training who conducts every training, as a coordinator does, and asks for one of their own.
        await SeedPersonAsync(StaffTraineeVid, "Staff", "Trainee", email: null, token, isStaff: true);
        foreach (var permission in new[] { TrainingPermissions.View, TrainingPermissions.Conduct, TrainingPermissions.Edit })
        {
            await GrantAsync(StaffTraineeVid, permission, token);
        }

        var own = await AddTrainingAsync(StaffTraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await AddSlotAsync(own, proposedDaysAgo: 1, token);
        var ownSlot = Assert.Single(await SlotsOfAsync(own, token)).Id;

        using var unassigned = await SignedInAsync(UnassignedTrainerVid, token);
        using var staffTrainee = await SignedInAsync(StaffTraineeVid, token);
        foreach (var (reader, training, proposal) in new[] { (unassigned, id, slot), (staffTrainee, own, ownSlot) })
        {
            // Each reads the training, and may not conduct it.
            var page = await PageAsync(reader, training, token);
            Assert.False(page.GetProperty("actions").GetProperty("canConduct").GetBoolean());
            var version = page.GetProperty("rowVersion").GetDateTime();

            using (var conflicts = await reader.GetAsync(Conflicts(training, day.AddHours(16), day.AddHours(18)), token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, conflicts.StatusCode);
            }

            (string Verb, object Body)[] steps =
            [
                ("slots", new { slots = new[] { Slot(day.AddHours(16), day.AddHours(18)) }, confirmed = true, rowVersion = version }),
                ($"slots/{proposal}/withdraw", new { rowVersion = version }),
                ("date", new { startsAtUtc = day.AddHours(16), confirmed = true, rowVersion = version }),
            ];
            foreach (var (verb, body) in steps)
            {
                using var refused = await StepAsync(reader, training, verb, body, token);
                Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{verb}: {refused.StatusCode}");
            }
        }

        // The same member of the staff conducts the trainings of others: what refuses them is whose training it is.
        Assert.True((await PageAsync(staffTrainee, id, token)).GetProperty("actions").GetProperty("canConduct").GetBoolean());

        // The trainee, who reads their training from their own page, takes back no date either.
        using var trainee = await SignedInAsync(TraineeVid, token);
        using (var theirs = await StepAsync(trainee, id, $"slots/{slot}/withdraw", new { rowVersion = (await StoredAsync(id, token)).RowVersion }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, theirs.StatusCode);
        }

        // Nothing was written: each still waits for its date, with the one date proposed.
        foreach (var (training, proposal) in new[] { (id, slot), (own, ownSlot) })
        {
            Assert.Equal(TrainingState.Assigned, (await StoredAsync(training, token)).State);
            Assert.Equal(proposal, Assert.Single(await SlotsOfAsync(training, token)).Id);
        }
    }

    /// <summary>
    /// The rules of a proposal the other tests leave out (review of A8a): a date longer than a session; more dates waiting than the
    /// trainee is fairly offered, proposed together or one on top of those waiting; and the states — dates are proposed and taken back
    /// only while the training waits for its date, and set by hand only while it has its trainer and goes on.
    /// </summary>
    [Fact]
    public async Task ADateLastsASessionTenWaitAtMostAndEachVerbHasItsStates()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(55);
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        using var trainer = await SignedInAsync(TrainerVid, token);
        var version = (await PageAsync(trainer, id, token)).GetProperty("rowVersion").GetDateTime();

        // Longer than a session.
        var start = day.AddHours(8);
        object[] tooLong = [Slot(start, start + TrainingDates.MaxSlotLength + TimeSpan.FromMinutes(30))];
        using (var refused = await StepAsync(trainer, id, "slots", new { slots = tooLong, confirmed = false, rowVersion = version }, token))
        {
            await AssertRefusedAsync(refused, "slots[0].endsAtUtc", TrainingDates.SlotTooLong, token);
        }

        // Half an hour each, one an hour: more than ten at once are too many; ten wait, and one more on top is too many.
        object[] Hourly(int from, int count) =>
            [.. Enumerable.Range(from, count).Select(hour => Slot(day.AddHours(hour), day.AddHours(hour).AddMinutes(30)))];
        using (var eleven = await StepAsync(trainer, id, "slots", new { slots = Hourly(0, TrainingDates.MaxOpenSlots + 1), confirmed = false, rowVersion = version }, token))
        {
            await AssertRefusedAsync(eleven, "slots", TrainingDates.SlotsTooMany, token);
        }

        var ten = await DoneAsync(await StepAsync(trainer, id, "slots", new { slots = Hourly(0, TrainingDates.MaxOpenSlots), confirmed = false, rowVersion = version }, token), token);
        Assert.Equal(TrainingDates.MaxOpenSlots, ten.GetProperty("slots").GetArrayLength());
        version = ten.GetProperty("rowVersion").GetDateTime();
        using (var more = await StepAsync(trainer, id, "slots", new { slots = Hourly(12, 1), confirmed = false, rowVersion = version }, token))
        {
            await AssertRefusedAsync(more, "slots", TrainingDates.SlotsTooMany, token);
        }

        // Dated, the training takes no date proposed and gives none back.
        var dated = await DoneAsync(await StepAsync(trainer, id, "date", new { startsAtUtc = day.AddHours(20), confirmed = false, rowVersion = version }, token), token);
        version = dated.GetProperty("rowVersion").GetDateTime();
        using (var proposed = await StepAsync(trainer, id, "slots", new { slots = Hourly(12, 1), confirmed = false, rowVersion = version }, token))
        {
            await AssertRefusedAsync(proposed, "state", TrainingDates.NotProposable, token);
        }

        var gone = ten.GetProperty("slots")[0].GetProperty("id").GetInt64();
        using (var withdrawn = await StepAsync(trainer, id, $"slots/{gone}/withdraw", new { rowVersion = version }, token))
        {
            await AssertRefusedAsync(withdrawn, "state", TrainingDates.NotProposable, token);
        }

        // Set by hand only while it has its trainer and goes on: not before a trainer is assigned, not once it is over.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        foreach (var (kind, state) in new[] { (RatingKind.Atc, TrainingState.Accepted), (RatingKind.Pilot, TrainingState.Closed) })
        {
            var other = await AddTrainingAsync(OtherTraineeVid, kind, state, token);
            var body = new { startsAtUtc = day.AddHours(20), confirmed = false, rowVersion = (await PageAsync(coordinator, other, token)).GetProperty("rowVersion").GetDateTime() };
            using var set = await StepAsync(coordinator, other, "date", body, token);
            await AssertRefusedAsync(set, "state", TrainingDates.NotSettable, token);
        }
    }

    /// <summary>
    /// A version gone stale is a conflict (review of A8a): the trainer who proposes from a page read before another proposal, and the
    /// trainee who chooses among dates that changed since they read them. Nothing is written, and the page read again writes.
    /// </summary>
    [Fact]
    public async Task AVersionGoneStaleIsAConflictForTheProposalAndForTheChoice()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(65);
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        using var trainer = await SignedInAsync(TrainerVid, token);
        var (first, second, third) = (day.AddHours(16), day.AddDays(1).AddHours(16), day.AddDays(2).AddHours(16));

        // Two proposals from the same page: the second finds the training moved, and writes nothing.
        var read = (await PageAsync(trainer, id, token)).GetProperty("rowVersion").GetDateTime();
        await DoneAsync(await StepAsync(trainer, id, "slots", new { slots = new[] { Slot(first, first.AddHours(2)) }, confirmed = false, rowVersion = read }, token), token);
        using (var staleProposal = await StepAsync(trainer, id, "slots", new { slots = new[] { Slot(second, second.AddHours(2)) }, confirmed = false, rowVersion = read }, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleProposal.StatusCode);
        }

        Assert.Equal([first], (await SlotsOfAsync(id, token)).Select(slot => slot.StartsAtUtc));
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.DatesProposed, token));

        // The trainee reads the date to choose; the trainer proposes another meanwhile; the choice from the page read before is a
        // conflict, and the training still waits.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var mine = await trainee.GetFromJsonAsync<JsonElement>(Mine(id), token);
        var chosen = Assert.Single(mine.GetProperty("slots").EnumerateArray()).GetProperty("id").GetInt64();
        await ProposeAsync(trainer, id, token, Slot(third, third.AddHours(2)));
        using (var staleChoice = await trainee.PostAsJsonAsync(Choose(id), new { slotId = chosen, rowVersion = mine.GetProperty("rowVersion").GetDateTime() }, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleChoice.StatusCode);
        }

        Assert.Equal(TrainingState.Assigned, (await StoredAsync(id, token)).State);
        Assert.Equal(2, (await SlotsOfAsync(id, token)).Count);
        Assert.Equal(0, await MailsAsync(TraineeVid, TrainingNotifications.DateConfirmed, token));

        // Read again, the same choice is written.
        var again = await trainee.GetFromJsonAsync<JsonElement>(Mine(id), token);
        var dated = await DoneAsync(await trainee.PostAsJsonAsync(Choose(id), new { slotId = chosen, rowVersion = again.GetProperty("rowVersion").GetDateTime() }, token), token);
        Assert.Equal(first, dated.GetProperty("scheduledStartUtc").GetDateTime());
    }

    [Fact]
    public async Task TheReminderLeavesOnceAndAgainForANewDate()
    {
        var token = TestContext.Current.CancellationToken;
        await WriteSettingsAsync(new { conflictPolicy = "None", reminderLeadHours = 48 }, token);

        // One session inside the lead, one beyond it.
        var near = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(30));
        var far = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddHours(60));
        var before = await StoredAsync(near, token);
        var audited = await AuditRowsAsync(near, token);

        await RunRemindersAsync(token);

        // Both of them written to, once; the one beyond the lead left for later. The mark is the job's bookkeeping: the training
        // did not change for anybody.
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.Reminder, token));
        Assert.Equal(1, await MailsAsync(TrainerVid, TrainingNotifications.Reminder, token));
        var reminded = await StoredAsync(near, token);
        Assert.NotNull(reminded.RemindedAt);
        Assert.Equal(before.UpdatedAt, reminded.UpdatedAt);
        Assert.Equal(audited, await AuditRowsAsync(near, token));
        Assert.Null((await StoredAsync(far, token)).RemindedAt);

        var reminder = await MailAsync(TraineeVid, TrainingNotifications.Reminder, token);
        Assert.Contains(TrainingMail.Moment(before.ScheduledStartUtc!.Value), reminder, StringComparison.Ordinal);
        Assert.Contains($"/training/mine/{near}", reminder, StringComparison.Ordinal);

        // Once is once.
        await RunRemindersAsync(token);
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.Reminder, token));

        // A new date is reminded in its turn.
        using var trainer = await SignedInAsync(TrainerVid, token);
        await DoneAsync(await StepAsync(trainer, near, "date", new { startsAtUtc = DateTime.UtcNow.AddHours(40), confirmed = true, rowVersion = reminded.RowVersion }, token), token);
        Assert.Null((await StoredAsync(near, token)).RemindedAt);

        await RunRemindersAsync(token);
        Assert.Equal(2, await MailsAsync(TraineeVid, TrainingNotifications.Reminder, token));
        Assert.Equal(2, await MailsAsync(TrainerVid, TrainingNotifications.Reminder, token));
    }

    [Fact]
    public async Task TheNightClosesATrainingWithNoDateChosenInTimeOnlyWhenTheDivisionSetsTheTime()
    {
        var token = TestContext.Current.CancellationToken;

        // Dates proposed five days ago and none chosen; a proposal of yesterday; a training still without dates.
        var waiting = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await GiveConductAsync(ScopedTrainerVid, waiting, token);
        await AddSlotAsync(waiting, proposedDaysAgo: 5, token);
        var recent = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await AddSlotAsync(recent, proposedDaysAgo: 1, token);
        var none = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: ScopedTrainerVid);

        // No time set — the default —: nothing closes by itself, and the ladder stays taken — the key of «one open training per
        // ladder» refuses a second.
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        await RunExpiryAsync(token);
        Assert.Equal(TrainingState.Assigned, (await StoredAsync(waiting, token)).State);
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token));

        // Three days to choose: the first closes, as the hub, and its trainer's grant goes the same night.
        await WriteSettingsAsync(new { conflictPolicy = "None", maxResponseDays = 3 }, token);
        await RunExpiryAsync(token);

        var closed = await StoredAsync(waiting, token);
        Assert.Equal((TrainingState.Closed, (int?)null, (string?)null), (closed.State, closed.ClosedBy, closed.CloseReason));
        Assert.NotNull(closed.ClosedAt);
        Assert.Empty(await SlotsOfAsync(waiting, token));
        Assert.Empty(await HoldersOfConductAsync(waiting, token));
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.TrainingClosed, token));

        // Closed through the row, not around it: the key reads a column only the row writes, and the ladder is free again.
        await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token);

        Assert.Equal(TrainingState.Assigned, (await StoredAsync(recent, token)).State);
        Assert.Single(await SlotsOfAsync(recent, token));
        Assert.Equal(TrainingState.Assigned, (await StoredAsync(none, token)).State);
    }

    [Fact]
    public async Task AnotherTrainerTakesTheTrainingWithoutTheDatesTheFirstOneProposed()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: ScopedTrainerVid);
        await AddSlotAsync(id, proposedDaysAgo: 1, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var version = (await PageAsync(coordinator, id, token)).GetProperty("rowVersion").GetDateTime();
        var assigned = await DoneAsync(await StepAsync(coordinator, id, "assign", new { trainerVid = TrainerVid, rowVersion = version }, token), token);

        Assert.Equal(TrainerVid, assigned.GetProperty("trainer").GetProperty("vid").GetInt32());
        Assert.Empty(assigned.GetProperty("slots").EnumerateArray());
        Assert.Empty(await SlotsOfAsync(id, token));
    }

    /// <summary>
    /// The «done when» of A8 (<c>08-piano-implementazione-m3.md</c>), through the API: the trainer proposes two dates, one of them
    /// warned of another training that day; the trainee chooses one; its session is in the public calendar with no name; and the
    /// reminder leaves once. The pages and the mailbox are A8b's round on the bench.
    /// </summary>
    [Fact]
    public async Task TheTrainerProposesTwoDatesTheTraineeChoosesOneTheCalendarShowsItAndTheReminderLeavesOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var day = Day(3);
        await WriteSettingsAsync(new { conflictPolicy = "Warn", conflictKinds = new[] { EventKind }, reminderLeadHours = 168 }, token);

        await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: ScopedTrainerVid, start: day.AddHours(10));
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);

        // Two dates, the first on the day of the other session: warned, and confirmed.
        using var trainer = await SignedInAsync(TrainerVid, token);
        var (warned, free) = (day.AddHours(16), day.AddDays(1).AddHours(16));
        var page = await ProposeAsync(trainer, id, token, Slot(warned, warned.AddHours(2)), Slot(free, free.AddHours(2)));
        var slots = page.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal("Training", Assert.Single(slots[0].GetProperty("warnings").EnumerateArray()).GetProperty("kind").GetString());
        Assert.Empty(slots[1].GetProperty("warnings").EnumerateArray());

        // The trainee chooses the first.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var mine = await trainee.GetFromJsonAsync<JsonElement>(Mine(id), token);
        var chosen = mine.GetProperty("slots").EnumerateArray().Single(slot => slot.GetProperty("startsAtUtc").GetDateTime() == warned);
        await DoneAsync(await trainee.PostAsJsonAsync(Choose(id), new { slotId = chosen.GetProperty("id").GetInt64(), rowVersion = mine.GetProperty("rowVersion").GetDateTime() }, token), token);

        // In the calendar of the site, for a visitor: by rating and position, nobody's name.
        using var visitor = _factory.CreateApiClient();
        var calendar = await visitor.GetFromJsonAsync<JsonElement>(CalendarBlock(warned.AddHours(-1), warned.AddHours(1)), token);
        var item = calendar.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("url").GetString() == $"/training/sessions/{id}");
        var title = item.GetProperty("title").ToString();
        Assert.Contains("TRNTEST_POS", title, StringComparison.Ordinal);
        Assert.DoesNotContain("Trainee", title, StringComparison.Ordinal);
        Assert.DoesNotContain(TraineeVid.ToString(CultureInfo.InvariantCulture), title, StringComparison.Ordinal);

        // The reminder, once to each of them.
        await RunRemindersAsync(token);
        await RunRemindersAsync(token);
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.Reminder, token));
        Assert.Equal(1, await MailsAsync(TrainerVid, TrainingNotifications.Reminder, token));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>The first moment of a day some days ahead, in UTC: far enough that no date of it has gone by.</summary>
    private static DateTime Day(int days) => DateTime.UtcNow.Date.AddDays(days);

    private static object Slot(DateTime start, DateTime end) => new { startsAtUtc = start, endsAtUtc = end };

    private static Uri Conflicts(long id, DateTime start, DateTime? end) =>
        new(
            $"{StaffEndpoints.Pattern}/{id}/conflicts?startsAtUtc={Instant(start)}" + (end is { } until ? $"&endsAtUtc={Instant(until)}" : string.Empty),
            UriKind.Relative);

    private static string Instant(DateTime utc) => Uri.EscapeDataString(utc.ToString("O", CultureInfo.InvariantCulture));

    private static Uri Mine(long id) => new($"{RequestEndpoints.Pattern}/{id}", UriKind.Relative);

    private static Uri Choose(long id) => new($"{RequestEndpoints.Pattern}/{id}/choose", UriKind.Relative);

    /// <summary>The calendar of the site as a visitor's browser asks it: the calendar block, for a window of time.</summary>
    private static Uri CalendarBlock(DateTime from, DateTime to)
    {
        var props = new JsonObject { ["from"] = BlockProps.Instant(from), ["to"] = BlockProps.Instant(to), ["limit"] = 50 }.ToJsonString();
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return new Uri($"/api/blocks/data/{CoreBlocks.Calendar}?props={encoded}", UriKind.Relative);
    }

    private static Task<HttpResponseMessage> StepAsync(HttpClient client, long id, string verb, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{StaffEndpoints.Pattern}/{id}/{verb}", UriKind.Relative), body, cancellationToken);

    private static async Task<JsonElement> PageAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"{StaffEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken);

    /// <summary>Dates proposed by the verb of the page, confirmed, at the version the page has now.</summary>
    private static async Task<JsonElement> ProposeAsync(HttpClient client, long id, CancellationToken cancellationToken, params object[] slots)
    {
        var version = (await PageAsync(client, id, cancellationToken)).GetProperty("rowVersion").GetDateTime();
        return await DoneAsync(await StepAsync(client, id, "slots", new { slots, confirmed = true, rowVersion = version }, cancellationToken), cancellationToken);
    }

    private static async Task<JsonElement> DoneAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {text}");
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

    /// <summary>The English titles of warnings of the calendar, in their order.</summary>
    private static IEnumerable<string?> Titles(JsonElement warnings) =>
        warnings.EnumerateArray().Select(warning => warning.GetProperty("title").GetProperty("en").GetString());

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        var problem = await ProblemAsync(response, cancellationToken);
        Assert.Contains(key, Keys(problem, field));
    }

    private async Task RunRemindersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TrainingRemindersJob>().RunAsync(cancellationToken);
    }

    private async Task RunExpiryAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TrainingExpiryJob>().RunAsync(cancellationToken);
    }

    private async Task<Training> StoredAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(training => training.Id == id, cancellationToken);
    }

    private async Task<List<TrainingSlot>> SlotsOfAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Slots.AsNoTracking()
            .Where(slot => slot.TrainingId == id)
            .OrderBy(slot => slot.StartsAtUtc)
            .ToListAsync(cancellationToken);
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

    private async Task<int> AuditRowsAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var key = id.ToString(CultureInfo.InvariantCulture);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .CountAsync(entry => entry.Entity == "trn_trainings" && entry.EntityId == key, cancellationToken);
    }

    /// <summary>Who holds <c>Training.Conduct</c> on this training by a grant with its scope.</summary>
    private async Task<List<int>> HoldersOfConductAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var scopeOf = Training.ScopeOf(id);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants.AsNoTracking()
            .Where(grant => grant.Value == TrainingPermissions.Conduct && grant.ResourceScope == scopeOf && grant.Vid != null)
            .Select(grant => grant.Vid!.Value)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A training of the first trained rating of its ladder, written as the installation straight into the state the test starts
    /// from — past the request and the assignment, which A6a's and A7's tests prove.
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
            DecidedBy = AdvisorVid,
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

    /// <summary>A date proposed some days ago, written as the installation: the day it was proposed is what the night counts from.</summary>
    private async Task AddSlotAsync(long id, int proposedDaysAgo, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var slot = new TrainingSlot { TrainingId = id, StartsAtUtc = Day(20).AddHours(16), EndsAtUtc = Day(20).AddHours(18) };
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE trn_slots SET created_at = {DateTime.UtcNow.AddDays(-proposedDaysAgo)} WHERE id = {slot.Id}",
            cancellationToken);
    }

    /// <summary>The grant the assignment writes (A7): <c>Training.Conduct</c> on this training alone.</summary>
    private async Task GiveConductAsync(int vid, long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ModuleGrants>()
            .GiveAsync(vid, TrainingPermissions.Conduct, Department.TD, Training.ScopeOf(id), GrantReason, cancellationToken));
    }

    /// <summary>
    /// An event of this class's own kind, of the events department, as the staff writes one in the calendar: public unless said
    /// otherwise, with its English title.
    /// </summary>
    private async Task SeedEventAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken,
        Visibility visibility = Visibility.Public,
        string english = "trn-test evening")
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.CalendarEntries.Add(new CalendarEntry
        {
            OwnerDepartment = Department.ED,
            Visibility = visibility,
            Kind = EventKind,
            SourceModule = ProjectionSource.Core,
            SourceId = $"{EventSourcePrefix}{Guid.NewGuid():N}",
            StartsAtUtc = start,
            EndsAtUtc = end,
            Url = "/trn-test-event",
            Title = "trn-test serata".L(english),
        });
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
    /// What this class leaves: the trainings of its trainees with their dates and their entries of the calendar, its events, the
    /// grants and positions of its people, their mails, the settings, and the addresses — so no later class counts them.
    /// </summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var training = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var ids = await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).Select(row => row.Id).ToListAsync(cancellationToken);
        var sources = ids.Select(Training.SourceIdOf).ToList();
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(entry => (entry.SourceModule == TrainingModule.ModuleKey && sources.Contains(entry.SourceId)) || entry.SourceId.StartsWith(EventSourcePrefix))
            .ExecuteDeleteAsync(cancellationToken);

        // The dates go with their trainings (the key cascades).
        await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => Vids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => Vids.Contains(user.Vid))
            .ExecuteUpdateAsync(user => user.SetProperty(row => row.Email, (string?)null), cancellationToken);

        var key = ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey);
        await hub.DivisionSettings.Where(setting => setting.Key == key).ExecuteDeleteAsync(cancellationToken);
    }
}
