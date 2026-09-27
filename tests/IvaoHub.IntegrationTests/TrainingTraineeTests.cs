using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
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
/// The trainee's path and the bans (M3, A10a; design M3 §2.8, §2.9, §4.2, §5.2), through the real host: the staff bans a trainee, who is
/// told by mail and asks for nothing on either ladder until the ban is over or lifted — the «done when» of A10a through the API —; a
/// ban is never changed nor deleted, and a second one does not pile on one that holds; nobody bans themselves, the super administrator
/// included, and whoever only reads does not ban; the staff's page of the trainee's path, with where they stand on each ladder, every
/// training as the staff's page of it, and the bans; and the rule of note <c>le-note-riservate-e-il-trainee</c> extended to the path: a
/// trainer who is also a trainee reads their own path without what is reserved, and another's with it.
/// <para>⚠️ The staff of the training is seeded without an address (<c>CONTRIBUTING.md</c>, "Tests"): only the trainee, who holds no
/// position, has a mailbox, for the mail of the ban. The items of the sheet this class writes carry <see cref="ItemStem"/> in their
/// title.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingTraineeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790039 and 790045–790049 are A1's to
    // A9a's, 790040–790044 and 790050–790051 A3b's.
    private const int TraineeVid = 790052;
    private const int CoordinatorVid = 790053;
    private const int TrainerTraineeVid = 790054;
    private const int OtherTraineeVid = 790055;
    private const int ReaderVid = 790056;
    private const int SuperadminVid = 790057;
    private const int StrangerVid = 790058;

    /// <summary>A VID nobody in the hub has: no row, no training, no ban.</summary>
    private const int NobodyVid = 790059;

    private static readonly int[] Vids = [TraineeVid, CoordinatorVid, TrainerTraineeVid, OtherTraineeVid, ReaderVid, SuperadminVid, StrangerVid, NobodyVid];

    private const string GrantReason = "trn-test";

    /// <summary>What the title of every item of the sheet this class writes starts with.</summary>
    private const string ItemStem = "trn-test-a10a";

    /// <summary>
    /// The fields of the staff's page of a training that are reserved (note <c>le-note-riservate-e-il-trainee</c> §4), the same three
    /// A9a's test lists: the path shows every training as that page, so the rule has to leave them out there too.
    /// </summary>
    private static readonly string[] ReservedOnThePage = ["staffComment", "sheet[].staffNote", "sessions[].internalNotes"];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, bans, items, grants and positions.
        await CleanAsync(token);

        var (topAtc, topPilot) = (Ladder(RatingKind.Atc)[^1], Ladder(RatingKind.Pilot)[^1]);
        var code = Division().Code;

        // The trainee has a mailbox, for the mail of the ban; their ratings are the ones below the first trained, so that they may ask
        // for a training on either ladder.
        var (atc, pilot) = Trained();
        await SeedPersonAsync(TraineeVid, "Test", "Trainee", $"trn-test-{TraineeVid}@example.invalid", token, atc: RungBelow(atc).Number, pilot: RungBelow(pilot).Number);
        await SeedPersonAsync(OtherTraineeVid, "Other", "Trainee", email: null, token);
        await SeedPersonAsync(StrangerVid, "Some", "Member", email: null, token);

        // A trainer of the department who is a trainee too: Training.View by their position.
        await SeedPersonAsync(TrainerTraineeVid, "Trainer", "Trainee", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-T98");

        // The coordinator bans and conducts every training: grants to their VID. Whoever only reads holds the view alone.
        await SeedPersonAsync(CoordinatorVid, "Test", "Coordinator", email: null, token, isStaff: true);
        await GrantAsync(CoordinatorVid, TrainingPermissions.View, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Ban, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Conduct, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Edit, token);
        await SeedPersonAsync(ReaderVid, "Test", "Reader", email: null, token, isStaff: true);
        await GrantAsync(ReaderVid, TrainingPermissions.View, token);

        await SeedPersonAsync(SuperadminVid, "Test", "Superadmin", email: null, token, isStaff: true, isSuperadmin: true);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of A10a (<c>08-piano-implementazione-m3.md</c>), through the API: the staff bans the trainee — told by mail —, whose
    /// request on either ladder is refused for the ban; the ban is lifted, with who and when, and the request goes through.
    /// </summary>
    [Fact]
    public async Task AStaffMemberBansATraineeWhoAsksForNothingUntilTheBanIsLifted()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // Given until somebody lifts it: it holds from now, and says who gave it.
        var given = await ReadAsync(await BanAsync(coordinator, TraineeVid, "trn-test: repeated no-shows", endsAt: null, token), HttpStatusCode.Created, token);
        var id = given.GetProperty("id").GetInt64();
        Assert.True(given.GetProperty("holds").GetBoolean());
        Assert.Equal(JsonValueKind.Null, given.GetProperty("endsAt").ValueKind);

        var listed = await ReadListAsync(coordinator, $"{BanEndpoints.Pattern}?filter[vid]={TraineeVid}", token);
        var row = Assert.Single(listed);
        Assert.Equal((CoordinatorVid, "Test Coordinator"), (row.GetProperty("givenBy").GetProperty("vid").GetInt32(), row.GetProperty("givenBy").GetProperty("name").GetString()));
        Assert.Equal("Test Trainee", row.GetProperty("trainee").GetProperty("name").GetString());

        // The trainee is told: why, until when, and their page.
        var mail = await MailAsync(TraineeVid, TrainingNotifications.Banned, token);
        Assert.Contains("trn-test: repeated no-shows", mail, StringComparison.Ordinal);
        Assert.Contains(TrainingMail.MinePath, mail, StringComparison.Ordinal);
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.Banned, token));

        // Nothing on either ladder while it holds: the page says so, and a request is refused on its ladder.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var mine = await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token);
        foreach (var kind in new[] { RatingKind.Atc, RatingKind.Pilot })
        {
            var path = Path(mine, kind);
            Assert.Equal(RequestRules.Banned, path.GetProperty("refusal").GetString());
            Assert.Equal(JsonValueKind.Null, path.GetProperty("bannedUntil").ValueKind);
        }

        using (var refused = await RequestPilotAsync(trainee, pilot, token))
        {
            await AssertRefusedAsync(refused, "kind", RequestRules.Banned, token);
        }

        // A ban is not changed through the form, nor deleted: it stays in the member's history.
        using (var changed = await coordinator.PutAsJsonAsync(new Uri($"{BanEndpoints.Pattern}/{id}", UriKind.Relative), new { vid = TraineeVid, reason = "trn-test: changed", endsAt = (DateTime?)null }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, changed.StatusCode);
        }

        using (var deleted = await coordinator.DeleteAsync(new Uri($"{BanEndpoints.Pattern}/{id}", UriKind.Relative), token))
        {
            Assert.False(deleted.IsSuccessStatusCode);
        }

        // Another does not pile on it: to change it, it is lifted and another is given.
        using (var twice = await BanAsync(coordinator, TraineeVid, "trn-test: again", endsAt: null, token))
        {
            await AssertRefusedAsync(twice, "vid", TrainingBans.AlreadyHolds, token);
        }

        // Lifted at a stale version: somebody moved it meanwhile.
        using (var stale = await LiftAsync(coordinator, id, given.GetProperty("rowVersion").GetDateTime().AddSeconds(-1), token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        // Lifted: who and when; it holds no more, and there is nothing left to lift.
        var lifted = await ReadAsync(await LiftAsync(coordinator, id, given.GetProperty("rowVersion").GetDateTime(), token), HttpStatusCode.OK, token);
        Assert.False(lifted.GetProperty("holds").GetBoolean());
        Assert.Equal(CoordinatorVid, lifted.GetProperty("liftedBy").GetProperty("vid").GetInt32());
        var stored = await StoredBanAsync(id, token);
        Assert.Equal(CoordinatorVid, stored.LiftedBy);
        Assert.NotNull(stored.LiftedAt);

        using (var again = await LiftAsync(coordinator, id, lifted.GetProperty("rowVersion").GetDateTime(), token))
        {
            await AssertRefusedAsync(again, "state", TrainingBans.NotHolding, token);
        }

        // Nothing more to tell the trainee, who asks for their training now.
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.Banned, token));
        Assert.Equal(JsonValueKind.Null, Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Pilot).GetProperty("refusal").ValueKind);
        var asked = await ReadAsync(await RequestPilotAsync(trainee, pilot, token), HttpStatusCode.Created, token);
        Assert.Equal(nameof(TrainingState.Requested), asked.GetProperty("state").GetString());
    }

    /// <summary>
    /// A ban with an end holds until it, and the mail says until when; an end already gone by is refused; a ban over asks for nothing,
    /// like a lifted one.
    /// </summary>
    [Fact]
    public async Task ABanWithAnEndHoldsUntilItAndAnEndGoneByIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        using (var past = await BanAsync(coordinator, TraineeVid, "trn-test: past", DateTime.UtcNow.AddHours(-1), token))
        {
            await AssertRefusedAsync(past, "endsAt", TrainingBans.EndsInThePast, token);
        }

        var end = DateTime.UtcNow.AddDays(3);
        var given = await ReadAsync(await BanAsync(coordinator, TraineeVid, "trn-test: three days", end, token), HttpStatusCode.Created, token);
        var endsAt = given.GetProperty("endsAt").GetDateTime();
        Assert.Equal(end, endsAt, TimeSpan.FromSeconds(1));

        // Until when, in the mail and on the trainee's page.
        Assert.Contains(TrainingMail.Moment(endsAt), await MailAsync(TraineeVid, TrainingNotifications.Banned, token), StringComparison.Ordinal);
        using var trainee = await SignedInAsync(TraineeVid, token);
        var path = Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Atc);
        Assert.Equal(RequestRules.Banned, path.GetProperty("refusal").GetString());
        Assert.Equal(endsAt, path.GetProperty("bannedUntil").GetDateTime(), TimeSpan.FromSeconds(1));

        // Over: the ban is moved into the past as the installation, and the trainee asks again.
        await EndBanAsync(given.GetProperty("id").GetInt64(), token);
        Assert.Equal(JsonValueKind.Null, Path(await trainee.GetFromJsonAsync<JsonElement>(RequestEndpoints.Pattern, token), RatingKind.Pilot).GetProperty("refusal").ValueKind);
        var asked = await ReadAsync(await RequestPilotAsync(trainee, pilot, token), HttpStatusCode.Created, token);
        Assert.Equal(nameof(TrainingState.Requested), asked.GetProperty("state").GetString());

        // The list says it is over.
        using var reader = await SignedInAsync(ReaderVid, token);
        Assert.False(Assert.Single(await ReadListAsync(reader, $"{BanEndpoints.Pattern}?filter[vid]={TraineeVid}", token)).GetProperty("holds").GetBoolean());
    }

    /// <summary>
    /// Nobody bans themselves nor lifts a ban of their own — <c>Training.Ban</c> is denied to whoever a ban is about, the super
    /// administrator included —, and whoever only reads the trainings reads the bans and gives none.
    /// </summary>
    [Fact]
    public async Task NobodyBansThemselvesAndWhoeverOnlyReadsBansNobody()
    {
        var token = TestContext.Current.CancellationToken;

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using (var own = await BanAsync(coordinator, CoordinatorVid, "trn-test: myself", endsAt: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        using (var own = await BanAsync(superadmin, SuperadminVid, "trn-test: myself", endsAt: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        // A ban of the super administrator's own, written as the installation: they do not lift it either. Another member's they do.
        var theirs = await AddBanAsync(SuperadminVid, token);
        using (var lift = await LiftAsync(superadmin, theirs.Id, theirs.RowVersion, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, lift.StatusCode);
        }

        var given = await ReadAsync(await BanAsync(superadmin, StrangerVid, "trn-test: by the super administrator", endsAt: null, token), HttpStatusCode.Created, token);
        Assert.True(given.GetProperty("holds").GetBoolean());
        Assert.Equal(0, await CountBansAsync(CoordinatorVid, token));

        // Whoever only reads: the list, and not a ban given nor lifted.
        using var reader = await SignedInAsync(ReaderVid, token);
        Assert.Single(await ReadListAsync(reader, $"{BanEndpoints.Pattern}?filter[vid]={SuperadminVid}", token));
        Assert.Single(await ReadListAsync(reader, $"{BanEndpoints.Pattern}?filter[vid]={StrangerVid}", token));
        using (var ban = await BanAsync(reader, OtherTraineeVid, "trn-test: by a reader", endsAt: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, ban.StatusCode);
        }

        using (var lift = await LiftAsync(reader, given.GetProperty("id").GetInt64(), given.GetProperty("rowVersion").GetDateTime(), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, lift.StatusCode);
        }

        // A member of no staff reads no ban.
        using var stranger = await SignedInAsync(StrangerVid, token);
        using (var list = await stranger.GetAsync(new Uri(BanEndpoints.Pattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        }
    }

    /// <summary>
    /// The staff's page of a trainee's path (design M3 §4.2): who they are, where they stand on each ladder — the mock exam agreed, the
    /// waiting —, every training as the staff's page of it — with the trainer's boxes, «ready for the exam» among them —, the bans, and
    /// whether the reader may ban them; read by whoever does training, and by nobody else; none for a VID the hub knows nothing of.
    /// </summary>
    [Fact]
    public async Task TheStaffReadsATraineesPathWithWhereTheyStandTheirTrainingsAndTheirBans()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        await WriteSettingsAsync(new { cooldownDays = 5 }, token);

        // The trainee's record: a pilot training completed a day ago, ready for the mock exam and for the exam; a request refused; a
        // ban lifted.
        var completed = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Completed, token, completedAt: DateTime.UtcNow.AddDays(-1), readyForMockExam: true, readyForExam: true);
        var refused = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Rejected, token);
        var ban = await AddBanAsync(TraineeVid, token);
        await LiftBanAsync(ban.Id, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var path = await PathAsync(coordinator, TraineeVid, token);

        Assert.Equal((TraineeVid, "Test Trainee"), (path.GetProperty("trainee").GetProperty("vid").GetInt32(), path.GetProperty("trainee").GetProperty("name").GetString()));
        Assert.True(path.GetProperty("canBan").GetBoolean());

        // Where they stand, the answer of their own page: on the pilot ladder a mock exam agreed, and the waiting.
        var onPilot = Ladder(path, RatingKind.Pilot);
        Assert.Equal(pilot.ShortName, onPilot.GetProperty("next").GetProperty("shortName").GetString());
        Assert.True(onPilot.GetProperty("isMockExam").GetBoolean());
        Assert.Equal(RequestRules.Waiting, onPilot.GetProperty("refusal").GetString());

        // Every training, newest first, as the staff's page of each; the bans.
        Assert.Equal([refused, completed], path.GetProperty("trainings").EnumerateArray().Select(training => training.GetProperty("id").GetInt64()));
        var report = path.GetProperty("trainings").EnumerateArray().Single(training => training.GetProperty("id").GetInt64() == completed);
        Assert.True(report.GetProperty("readyForExam").GetBoolean());
        Assert.False(report.GetProperty("reservedLeftOut").GetBoolean());
        var lifted = Assert.Single(path.GetProperty("bans").EnumerateArray());
        Assert.False(lifted.GetProperty("holds").GetBoolean());
        Assert.Equal(CoordinatorVid, lifted.GetProperty("liftedBy").GetProperty("vid").GetInt32());

        // Whoever only reads reads it all, and may not ban.
        using var reader = await SignedInAsync(ReaderVid, token);
        var read = await PathAsync(reader, TraineeVid, token);
        Assert.False(read.GetProperty("canBan").GetBoolean());
        Assert.Equal(2, read.GetProperty("trainings").GetArrayLength());

        // Nobody without the view, not even the trainee; nothing for somebody the hub knows nothing of; a member with nothing yet is
        // read with nothing on it.
        using var trainee = await SignedInAsync(TraineeVid, token);
        using (var own = await trainee.GetAsync(new Uri($"{TraineePathEndpoints.Pattern}/{TraineeVid}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        using (var nobody = await coordinator.GetAsync(new Uri($"{TraineePathEndpoints.Pattern}/{NobodyVid}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
        }

        var stranger = await PathAsync(coordinator, StrangerVid, token);
        Assert.Equal((0, 0), (stranger.GetProperty("trainings").GetArrayLength(), stranger.GetProperty("bans").GetArrayLength()));
        Assert.Equal(2, stranger.GetProperty("ladders").GetArrayLength());

        // The coordinator reading their own path may not ban themselves.
        Assert.False((await PathAsync(coordinator, CoordinatorVid, token)).GetProperty("canBan").GetBoolean());
    }

    /// <summary>
    /// The test of note <c>le-note-riservate-e-il-trainee</c> extended to the path (§4; design M3 §10, §12 n.13; <c>08</c>, A10 point 3):
    /// a trainer — <c>Training.View</c> by their position — who is also a trainee reads their own path with every training of theirs
    /// without what is reserved, and another trainee's path with it; the fields are the ones <see cref="ReservedOnThePage"/> lists.
    /// </summary>
    [Fact]
    public async Task ATrainerWhoIsATraineeReadsTheirOwnPathWithoutWhatIsReservedAndAnothersWithIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var item = await AddItemAsync(atc, SheetSection.Practice, "hold short", sort: 931, token);

        // Two trainings with everything reserved written on them — the notes of a session rescheduled, the note on an item, the comment
        // for the staff —: the trainer-trainee's own, and another trainee's, both conducted by the coordinator.
        var own = await ReportedWithNotesAsync(TrainerTraineeVid, item, "own", token);
        var another = await ReportedWithNotesAsync(OtherTraineeVid, item, "another", token);

        using var trainerTrainee = await SignedInAsync(TrainerTraineeVid, token);

        // Another trainee's path: every reserved field, as written.
        var theirs = OnThePath(await PathAsync(trainerTrainee, OtherTraineeVid, token), another);
        Assert.False(theirs.GetProperty("reservedLeftOut").GetBoolean());
        Assert.Equal(
            ["trn-test-reserved: another, staff", "trn-test-reserved: another, item", "trn-test-reserved: another, session"],
            ReservedValues(theirs, item));

        // Their own: readable, with every reserved field left out — nowhere in the answer.
        var text = await trainerTrainee.GetStringAsync(new Uri($"{TraineePathEndpoints.Pattern}/{TrainerTraineeVid}", UriKind.Relative), token);
        var mine = OnThePath(JsonDocument.Parse(text).RootElement.Clone(), own);
        Assert.True(mine.GetProperty("reservedLeftOut").GetBoolean());
        Assert.Equal(new string?[] { null, null, null }, ReservedValues(mine, item));
        Assert.DoesNotContain("trn-test-reserved", text, StringComparison.Ordinal);

        // What they read anyway stays: the grade and the comment for them, the general comment, the sessions.
        Assert.Equal("trn-test: own, general", mine.GetProperty("generalComment").GetString());
        var graded = mine.GetProperty("sheet").EnumerateArray().Single(each => each.GetProperty("itemId").GetInt64() == item);
        Assert.Equal((3, "trn-test: own, item"), (graded.GetProperty("grade").GetInt32(), graded.GetProperty("traineeComment").GetString()));
        Assert.Equal(2, mine.GetProperty("sessions").GetArrayLength());

        // The coordinator reads the trainer-trainee's path whole: the rule is about who reads, not about whose path it is.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        Assert.Equal(
            ["trn-test-reserved: own, staff", "trn-test-reserved: own, item", "trn-test-reserved: own, session"],
            ReservedValues(OnThePath(await PathAsync(coordinator, TrainerTraineeVid, token), own), item));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> BanAsync(HttpClient client, int vid, string reason, DateTime? endsAt, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri(BanEndpoints.Pattern, UriKind.Relative), new { vid, reason, endsAt }, cancellationToken);

    private static Task<HttpResponseMessage> LiftAsync(HttpClient client, long id, DateTime version, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{BanEndpoints.Pattern}/{id}/lift", UriKind.Relative), new { rowVersion = version }, cancellationToken);

    private static Task<HttpResponseMessage> RequestPilotAsync(HttpClient client, Rating pilot, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            RequestEndpoints.Pattern,
            new { kind = nameof(RatingKind.Pilot), rating = pilot.Number, position = (string?)null, availabilityText = "trn-test", notesText = (string?)null, theoryPassed = true },
            cancellationToken);

    private static async Task<JsonElement> PathAsync(HttpClient client, int vid, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"{TraineePathEndpoints.Pattern}/{vid}", UriKind.Relative), cancellationToken);

    private static JsonElement OnThePath(JsonElement path, long training) =>
        path.GetProperty("trainings").EnumerateArray().Single(each => each.GetProperty("id").GetInt64() == training);

    private static JsonElement Ladder(JsonElement path, RatingKind kind) =>
        path.GetProperty("ladders").EnumerateArray().Single(ladder => ladder.GetProperty("kind").GetString() == kind.ToString());

    private static JsonElement Path(JsonElement mine, RatingKind kind) =>
        mine.GetProperty("paths").EnumerateArray().Single(path => path.GetProperty("kind").GetString() == kind.ToString());

    private static async Task<List<JsonElement>> ReadListAsync(HttpClient client, string address, CancellationToken cancellationToken) =>
        [.. (await client.GetFromJsonAsync<JsonElement>(new Uri(address, UriKind.Relative), cancellationToken)).GetProperty("items").EnumerateArray()];

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
    /// A training of <paramref name="trainee"/> taken through every step that writes something reserved, by the coordinator: a session
    /// rescheduled with its notes, a date set by hand a moment ago, and the report with a note on the item and a comment for the staff.
    /// </summary>
    private async Task<long> ReportedWithNotesAsync(int trainee, long item, string whose, CancellationToken cancellationToken)
    {
        var id = await AddTrainingAsync(trainee, RatingKind.Atc, TrainingState.Scheduled, cancellationToken, trainer: CoordinatorVid, start: DateTime.UtcNow.AddHours(-2));
        using var coordinator = await SignedInAsync(CoordinatorVid, cancellationToken);

        var version = (await PageAsync(coordinator, id, cancellationToken)).GetProperty("rowVersion").GetDateTime();
        var rescheduled = await ReadAsync(await StepAsync(coordinator, id, "reschedule", new { notes = $"trn-test-reserved: {whose}, session", rowVersion = version }, cancellationToken), HttpStatusCode.OK, cancellationToken);
        var set = await ReadAsync(
            await StepAsync(coordinator, id, "date", new { startsAtUtc = DateTime.UtcNow.AddMinutes(-10), confirmed = true, rowVersion = rescheduled.GetProperty("rowVersion").GetDateTime() }, cancellationToken),
            HttpStatusCode.OK,
            cancellationToken);
        await ReadAsync(
            await StepAsync(
                coordinator,
                id,
                "report",
                new
                {
                    sheet = new object[] { new { itemId = item, grade = 3, mark = (string?)null, traineeComment = $"trn-test: {whose}, item", staffNote = $"trn-test-reserved: {whose}, item" } },
                    generalComment = $"trn-test: {whose}, general",
                    staffComment = $"trn-test-reserved: {whose}, staff",
                    readyForMockExam = false,
                    readyForExam = false,
                    cooldownWaived = false,
                    rowVersion = set.GetProperty("rowVersion").GetDateTime(),
                },
                cancellationToken),
            HttpStatusCode.OK,
            cancellationToken);

        return id;
    }

    private static Task<HttpResponseMessage> StepAsync(HttpClient client, long id, string verb, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{StaffEndpoints.Pattern}/{id}/{verb}", UriKind.Relative), body, cancellationToken);

    private static async Task<JsonElement> PageAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"{StaffEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
            return JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");
            var errors = JsonDocument.Parse(text).RootElement.GetProperty("errors");
            Assert.True(errors.TryGetProperty(field, out var keys), text);
            Assert.Contains(key, keys.EnumerateArray().Select(each => each.GetString()));
        }
    }

    private async Task<TraineeBan> StoredBanAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().TraineeBans.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(ban => ban.Id == id, cancellationToken);
    }

    private async Task<int> CountBansAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().TraineeBans.IgnoreQueryFilters()
            .CountAsync(ban => ban.Vid == vid, cancellationToken);
    }

    /// <summary>A ban until somebody lifts it, written as the installation.</summary>
    private async Task<TraineeBan> AddBanAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var ban = new TraineeBan { Vid = vid, Reason = "trn-test: written by the installation" };
        database.TraineeBans.Add(ban);
        await database.SaveChangesAsync(cancellationToken);
        return ban;
    }

    /// <summary>A ban lifted by the coordinator a moment ago, written as the installation.</summary>
    private async Task LiftBanAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var ban = await database.TraineeBans.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken);
        ban.LiftedBy = CoordinatorVid;
        ban.LiftedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A ban moved into the past as the installation: given two days ago, over a moment ago.</summary>
    private async Task EndBanAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        await database.TraineeBans.IgnoreQueryFilters()
            .Where(ban => ban.Id == id)
            .ExecuteUpdateAsync(ban => ban.SetProperty(row => row.EndsAt, DateTime.UtcNow.AddMinutes(-1)), cancellationToken);
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
        DateTime? start = null,
        DateTime? completedAt = null,
        bool readyForMockExam = false,
        bool readyForExam = false)
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
            Rejection = state == TrainingState.Rejected ? TrainingRejection.Staff : null,
            RejectionReason = state == TrainingState.Rejected ? "trn-test: not yet" : null,
            DecidedBy = CoordinatorVid,
            DecidedAt = now,
            TrainerVid = trainer,
            AssignedBy = trainer is null ? null : CoordinatorVid,
            AssignedAt = trainer is null ? null : now,
            ScheduledStartUtc = start,
            CompletedAt = completedAt,
            ReadyForMockExam = readyForMockExam,
            ReadyForExam = readyForExam,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

    /// <summary>An item of the sheet of a rating, written as the installation, with this class's stem in its title in every language.</summary>
    private async Task<long> AddItemAsync(Rating rating, SheetSection section, string title, int sort, CancellationToken cancellationToken)
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
            IsActive = true,
        };

        database.SheetItems.Add(item);
        await database.SaveChangesAsync(cancellationToken);
        return item.Id;
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
    /// A person of this class: their name, a mailbox only when a test looks for their mail, whether the hub counts them as staff or as
    /// its super administrator, their ratings, and a position as the network spells it.
    /// </summary>
    private async Task SeedPersonAsync(
        int vid,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken,
        bool isStaff = false,
        bool isSuperadmin = false,
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
        user.IsSuperadmin = isSuperadmin;
        user.RatingAtc = atc;
        user.RatingPilot = pilot;
        user.HoursAtc = 500m;
        user.HoursPilot = 500m;
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
    /// What this class leaves: the trainings of its people with their dates, sessions and sheets and their entries of the calendar, their
    /// bans, its items of the sheet, the grants and positions of its people, their mails, the settings, the addresses and the super
    /// administrator — so no later class counts them.
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
        await training.TraineeBans.IgnoreQueryFilters().Where(ban => Vids.Contains(ban.Vid)).ExecuteDeleteAsync(cancellationToken);
        await training.Database.ExecuteSqlAsync($"DELETE FROM trn_sheet_items WHERE title_i18n LIKE {$"%{ItemStem}%"}", cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => Vids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => Vids.Contains(user.Vid))
            .ExecuteUpdateAsync(user => user.SetProperty(row => row.Email, (string?)null).SetProperty(row => row.IsSuperadmin, false), cancellationToken);

        var key = ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey);
        await hub.DivisionSettings.Where(setting => setting.Key == key).ExecuteDeleteAsync(cancellationToken);
    }
}
