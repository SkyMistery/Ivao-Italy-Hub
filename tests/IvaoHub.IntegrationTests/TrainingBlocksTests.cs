using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Blocks;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Public;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The four blocks of the training and its public pages (M3, A10b; design M3 §2.5, §4.1, §4.3; note <c>il-training-in-pubblico</c>),
/// through the real host: a visitor reads the sessions still to be held — on <c>/training</c>, on the page of each, and in the block —
/// by their rating, position and time and with nobody's VID or name, and a signed in reader reads who, the «done when» of A10b through
/// the API; the address of the entry of the calendar is the page of its session; the three personal blocks tell a visitor only that
/// they are not signed in, and answer each reader with theirs — the trainee with the answer of their own page, the trainer with the
/// dates to propose, the choices late after <c>responseReminderDays</c> and the reports to write, the staff with the requests they may
/// accept and the trainings they may assign, never their own.
/// <para>⚠️ The staff of the training holds its permissions by grants to a VID, with no position and no address (<c>CONTRIBUTING.md</c>,
/// "Tests"). The trainings are written as the installation, straight into the state a test starts from; A6a's to A9a's tests prove
/// the steps that lead there. The shared database may hold other sessions: a test looks for its own among them.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingBlocksTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790059 are A1's to A10a's, A3b's among them.
    private const int TraineeVid = 790060;
    private const int TrainerVid = 790061;
    private const int AdvisorVid = 790062;
    private const int CoordinatorVid = 790063;
    private const int OtherTraineeVid = 790064;
    private const int MemberVid = 790065;
    private const int ThirdTraineeVid = 790066;
    private const int FourthTraineeVid = 790067;

    private static readonly int[] Vids = [TraineeVid, TrainerVid, AdvisorVid, CoordinatorVid, OtherTraineeVid, MemberVid, ThirdTraineeVid, FourthTraineeVid];

    private const string GrantReason = "trn-test";

    /// <summary>The position of this class's trainings: made up, so no division's is named.</summary>
    private const string Position = "TRNTEST_POS";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, their dates and entries of the calendar, grants and settings.
        await CleanAsync(token);

        var (atc, pilot) = Trained();
        foreach (var (vid, first, last) in new[]
                 {
                     (TraineeVid, "Test", "Trainee"),
                     (OtherTraineeVid, "Other", "Trainee"),
                     (ThirdTraineeVid, "Third", "Trainee"),
                     (FourthTraineeVid, "Fourth", "Trainee"),
                     (MemberVid, "Some", "Member"),
                 })
        {
            await SeedPersonAsync(vid, first, last, token, atc: RungBelow(atc).Number, pilot: RungBelow(pilot).Number);
        }

        // The trainer conducts; the advisor accepts and refuses; the coordinator accepts, refuses, assigns and conducts — and asks for
        // a training of their own, which nobody moves in their own queue.
        await SeedPersonAsync(TrainerVid, "Test", "Trainer", token, isStaff: true);
        await GrantAsync(TrainerVid, token, TrainingPermissions.View, TrainingPermissions.Conduct);
        await SeedPersonAsync(AdvisorVid, "Test", "Advisor", token, isStaff: true);
        await GrantAsync(AdvisorVid, token, TrainingPermissions.View, TrainingPermissions.Approve);
        await SeedPersonAsync(CoordinatorVid, "Test", "Coordinator", token, isStaff: true, atc: RungBelow(atc).Number, pilot: RungBelow(pilot).Number);
        await GrantAsync(
            CoordinatorVid,
            token,
            TrainingPermissions.View,
            TrainingPermissions.Approve,
            TrainingPermissions.Assign,
            TrainingPermissions.Conduct,
            TrainingPermissions.Edit);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of A10b (<c>08-piano-implementazione-m3.md</c>), through the API: a visitor reads the session still to be held —
    /// on the list of <c>/training</c>, on its own page and in the block — by rating, position and time, with no VID and no name; the page
    /// of a session held is there too, where its entry of the calendar points, and a training with no date has none; a signed in member
    /// reads who, by VID and name.
    /// </summary>
    [Fact]
    public async Task AVisitorSeesTheSessionsWithoutVidsOrNamesAndASignedInMemberSeesWho()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        var now = DateTime.UtcNow;

        var dated = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: now.AddHours(3));
        var held = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Completed, token, trainer: TrainerVid, start: now.AddDays(-2), completedAt: now.AddDays(-2).AddHours(2));
        var undated = await AddTrainingAsync(ThirdTraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        string[] secrets =
        [
            TraineeVid.ToString(CultureInfo.InvariantCulture),
            TrainerVid.ToString(CultureInfo.InvariantCulture),
            OtherTraineeVid.ToString(CultureInfo.InvariantCulture),
            "Test Trainee",
            "Test Trainer",
            "Other Trainee",
        ];

        using var anonymous = _factory.CreateApiClient();

        // The list of /training: the session to come by its rating, position and time, and nobody; not the held one, not the undated one.
        var (listText, list) = await ReadAsync(await anonymous.GetAsync(new Uri(PublicSessionEndpoints.Pattern, UriKind.Relative), token), HttpStatusCode.OK, token);
        var item = Assert.Single(list.EnumerateArray(), entry => Id(entry) == dated);
        Assert.Equal(("Atc", atc.ShortName, Position), (item.GetProperty("kind").GetString(), item.GetProperty("ratingShortName").GetString(), item.GetProperty("position").GetString()));
        Assert.Equal(now.AddHours(3), item.GetProperty("startsAtUtc").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
        Assert.False(item.GetProperty("held").GetBoolean());
        AssertNobody(item);
        Assert.DoesNotContain(list.EnumerateArray(), entry => Id(entry) == held || Id(entry) == undated);
        AssertSaysNone(listText, secrets);

        // The page of a session, by the address its entry of the calendar points at — the held one too —; a training with no date has none.
        var entry = await CalendarEntryAsync(dated, token);
        Assert.Equal((Training.SessionPath(dated), Visibility.Public), (entry.Url, entry.Visibility));
        var (pageText, page) = await ReadAsync(await anonymous.GetAsync(new Uri($"/api{entry.Url}", UriKind.Relative), token), HttpStatusCode.OK, token);
        Assert.Equal((dated, atc.ShortName, Position), (Id(page), page.GetProperty("ratingShortName").GetString(), page.GetProperty("position").GetString()));
        AssertNobody(page);
        AssertSaysNone(pageText, secrets);

        var (heldText, heldPage) = await ReadAsync(await anonymous.GetAsync(new Uri($"{PublicSessionEndpoints.Pattern}/{held}", UriKind.Relative), token), HttpStatusCode.OK, token);
        Assert.True(heldPage.GetProperty("held").GetBoolean());
        AssertNobody(heldPage);
        AssertSaysNone(heldText, secrets);

        using (var none = await anonymous.GetAsync(new Uri($"{PublicSessionEndpoints.Pattern}/{undated}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        }

        // The block says the same to a visitor, whatever page shows it, and at most as many as its property asks.
        var (blockText, block) = await ReadAsync(await anonymous.GetAsync(BlockUri(UpcomingSessionsProvider.BlockType), token), HttpStatusCode.OK, token);
        Assert.False(block.GetProperty("signedIn").GetBoolean());
        AssertNobody(Assert.Single(block.GetProperty("items").EnumerateArray(), entry => Id(entry) == dated));
        AssertSaysNone(blockText, secrets);
        var (_, one) = await ReadAsync(await anonymous.GetAsync(BlockUri(UpcomingSessionsProvider.BlockType, """{"limit":1}"""), token), HttpStatusCode.OK, token);
        Assert.Single(one.GetProperty("items").EnumerateArray());

        // Signed in, any member: who, by VID and name — on the page of the session and in the block.
        using var member = await SignedInAsync(MemberVid, token);
        var (_, seen) = await ReadAsync(await member.GetAsync(new Uri($"{PublicSessionEndpoints.Pattern}/{dated}", UriKind.Relative), token), HttpStatusCode.OK, token);
        Assert.Equal((TraineeVid, "Test Trainee"), Person(seen.GetProperty("trainee")));
        Assert.Equal((TrainerVid, "Test Trainer"), Person(seen.GetProperty("trainer")));

        var (_, seenBlock) = await ReadAsync(await member.GetAsync(BlockUri(UpcomingSessionsProvider.BlockType), token), HttpStatusCode.OK, token);
        Assert.True(seenBlock.GetProperty("signedIn").GetBoolean());
        var mine = Assert.Single(seenBlock.GetProperty("items").EnumerateArray(), entry => Id(entry) == dated);
        Assert.Equal((TraineeVid, "Test Trainee"), Person(mine.GetProperty("trainee")));
        Assert.Equal((TrainerVid, "Test Trainer"), Person(mine.GetProperty("trainer")));
    }

    [Fact]
    public async Task APersonalBlockTellsAVisitorOnlyThatTheyAreNotSignedIn()
    {
        var token = TestContext.Current.CancellationToken;
        await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        using var anonymous = _factory.CreateApiClient();

        foreach (var type in new[] { MyTrainingProvider.BlockType, TrainerQueueProvider.BlockType, ApprovalQueueProvider.BlockType })
        {
            var (_, block) = await ReadAsync(await anonymous.GetAsync(BlockUri(type), token), HttpStatusCode.OK, token);
            var only = Assert.Single(block.EnumerateObject());
            Assert.Equal(("signedIn", false), (only.Name, only.Value.GetBoolean()));
        }
    }

    /// <summary>
    /// The trainee's block is the answer of their own page, <c>GET /api/training/mine</c>: the same ladders — the open training on the
    /// one, and what it waits for — and the same trainings, with the dates to choose from; nothing of another member's, and no field of
    /// the staff's.
    /// </summary>
    [Fact]
    public async Task TheTraineesBlockIsTheAnswerOfTheirOwnPage()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var waiting = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        await AddSlotAsync(waiting, starts: now.AddDays(2), proposedAgo: TimeSpan.FromHours(1), token);
        var another = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Requested, token);

        using var trainee = await SignedInAsync(TraineeVid, token);
        var (_, page) = await ReadAsync(await trainee.GetAsync(new Uri(RequestEndpoints.Pattern, UriKind.Relative), token), HttpStatusCode.OK, token);
        var (_, block) = await ReadAsync(await trainee.GetAsync(BlockUri(MyTrainingProvider.BlockType), token), HttpStatusCode.OK, token);

        Assert.True(block.GetProperty("signedIn").GetBoolean());
        Assert.Equal(Ladders(page), Ladders(block));
        Assert.Equal(Trainings(page), Trainings(block));

        var atcLadder = Assert.Single(block.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "Atc");
        Assert.Equal((RequestRules.Open, waiting), (atcLadder.GetProperty("refusal").GetString(), atcLadder.GetProperty("openTrainingId").GetInt64()));
        var training = Assert.Single(block.GetProperty("trainings").EnumerateArray());
        Assert.Equal(waiting, Id(training));
        Assert.Single(training.GetProperty("slots").EnumerateArray());
        Assert.False(training.TryGetProperty("staffComment", out _));
        Assert.DoesNotContain(block.GetProperty("trainings").EnumerateArray(), entry => Id(entry) == another);
    }

    /// <summary>
    /// The trainer's queue: the trainings assigned to them whose dates are theirs to propose, the ones whose trainee has let the dates
    /// wait longer than <c>responseReminderDays</c> — with the days —, and the ones whose session has started, to report; a date just
    /// proposed and a session to come are nobody's to move, another trainer's training is theirs, and whoever conducts nothing has none.
    /// </summary>
    [Fact]
    public async Task ATrainersQueueHasTheDatesToProposeTheLateChoicesAndTheReportsToWrite()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        var toPropose = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        var late = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        await AddSlotAsync(late, starts: now.AddDays(3), proposedAgo: TimeSpan.FromDays(5).Add(TimeSpan.FromHours(1)), token);
        var fresh = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: TrainerVid);
        await AddSlotAsync(fresh, starts: now.AddDays(3), proposedAgo: TimeSpan.FromHours(1), token);
        var toReport = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: now.AddHours(-1));
        await AddTrainingAsync(ThirdTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: now.AddDays(1));
        var theCoordinators = await AddTrainingAsync(FourthTraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: CoordinatorVid);

        using var trainer = await SignedInAsync(TrainerVid, token);
        var queue = await BlockAsync(trainer, TrainerQueueProvider.BlockType, token);
        Assert.True(queue.GetProperty("signedIn").GetBoolean());
        Assert.Equal([toPropose], Ids(queue.GetProperty("toPropose")));
        var waiting = Assert.Single(queue.GetProperty("waiting").EnumerateArray());
        Assert.Equal((late, 5), (Id(waiting.GetProperty("training")), waiting.GetProperty("days").GetInt32()));
        Assert.Equal("Other Trainee", waiting.GetProperty("training").GetProperty("trainee").GetProperty("name").GetString());
        Assert.Equal([toReport], Ids(queue.GetProperty("toReport")));

        // Another trainer's is theirs.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var theirs = await BlockAsync(coordinator, TrainerQueueProvider.BlockType, token);
        Assert.Equal([theCoordinators], Ids(theirs.GetProperty("toPropose")));
        Assert.Empty(theirs.GetProperty("waiting").EnumerateArray());
        Assert.Empty(theirs.GetProperty("toReport").EnumerateArray());

        // With more days given, the trainee who has not chosen for five is still within them.
        await WriteSettingsAsync(new { responseReminderDays = 6 }, token);
        queue = await BlockAsync(trainer, TrainerQueueProvider.BlockType, token);
        Assert.Empty(queue.GetProperty("waiting").EnumerateArray());
        Assert.Equal([toPropose], Ids(queue.GetProperty("toPropose")));

        // Whoever conducts nothing has nothing to move.
        using var member = await SignedInAsync(MemberVid, token);
        var nothing = await BlockAsync(member, TrainerQueueProvider.BlockType, token);
        Assert.True(nothing.GetProperty("signedIn").GetBoolean());
        foreach (var part in new[] { "toPropose", "waiting", "toReport" })
        {
            Assert.Empty(nothing.GetProperty(part).EnumerateArray());
        }
    }

    /// <summary>
    /// The queue of the staff: the requests the reader may accept or refuse and the trainings they may assign, the oldest request first,
    /// as the one handler answers on each row — the advisor accepts and assigns nothing, the coordinator does both and never on a request
    /// of their own, and whoever may do neither has none.
    /// </summary>
    [Fact]
    public async Task TheApprovalQueueHoldsWhatTheReaderMayAcceptOrAssignAndNeverTheirOwn()
    {
        var token = TestContext.Current.CancellationToken;
        var requested = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        var theCoordinators = await AddTrainingAsync(CoordinatorVid, RatingKind.Atc, TrainingState.Requested, token);
        var accepted = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Accepted, token);

        using var advisor = await SignedInAsync(AdvisorVid, token);
        var queue = await BlockAsync(advisor, ApprovalQueueProvider.BlockType, token);
        Assert.True(queue.GetProperty("signedIn").GetBoolean());
        var toApprove = Ids(queue.GetProperty("toApprove").GetProperty("oldest"));
        Assert.Equal([requested, theCoordinators], toApprove.Where(id => id == requested || id == theCoordinators));
        Assert.True(queue.GetProperty("toApprove").GetProperty("count").GetInt32() >= 2);
        Assert.Equal(0, queue.GetProperty("toAssign").GetProperty("count").GetInt32());
        Assert.Empty(queue.GetProperty("toAssign").GetProperty("oldest").EnumerateArray());

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        queue = await BlockAsync(coordinator, ApprovalQueueProvider.BlockType, token);
        toApprove = Ids(queue.GetProperty("toApprove").GetProperty("oldest"));
        Assert.Contains(requested, toApprove);
        Assert.DoesNotContain(theCoordinators, toApprove);
        var toAssign = queue.GetProperty("toAssign").GetProperty("oldest");
        var row = Assert.Single(toAssign.EnumerateArray(), entry => Id(entry) == accepted);
        Assert.Equal((OtherTraineeVid, "Other Trainee"), Person(row.GetProperty("trainee")));

        foreach (var vid in new[] { TrainerVid, MemberVid })
        {
            using var nobody = await SignedInAsync(vid, token);
            var none = await BlockAsync(nobody, ApprovalQueueProvider.BlockType, token);
            Assert.Equal((0, 0), (none.GetProperty("toApprove").GetProperty("count").GetInt32(), none.GetProperty("toAssign").GetProperty("count").GetInt32()));
        }
    }

    // ---- reading ---------------------------------------------------------------------------------------------------------------

    private static long Id(JsonElement entry) => entry.GetProperty("id").GetInt64();

    private static List<long> Ids(JsonElement rows) => [.. rows.EnumerateArray().Select(Id)];

    private static (int Vid, string? Name) Person(JsonElement person) => (person.GetProperty("vid").GetInt32(), person.GetProperty("name").GetString());

    private static void AssertNobody(JsonElement session)
    {
        Assert.Equal(JsonValueKind.Null, session.GetProperty("trainee").ValueKind);
        Assert.Equal(JsonValueKind.Null, session.GetProperty("trainer").ValueKind);
    }

    private static void AssertSaysNone(string text, IEnumerable<string> secrets)
    {
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    /// <summary>The ladders as the rules left them: which, what refuses a request, and the open training.</summary>
    private static List<(string?, string?, long?)> Ladders(JsonElement answer) =>
    [
        .. answer.GetProperty("paths").EnumerateArray().Select(path => (
            path.GetProperty("kind").GetString(),
            path.GetProperty("refusal").GetString(),
            path.GetProperty("openTrainingId").ValueKind == JsonValueKind.Null ? (long?)null : path.GetProperty("openTrainingId").GetInt64())),
    ];

    private static List<(long, string?)> Trainings(JsonElement answer) =>
        [.. answer.GetProperty("trainings").EnumerateArray().Select(training => (Id(training), training.GetProperty("state").GetString()))];

    private static Uri BlockUri(string type, string? props = null) =>
        new(
            props is null
                ? $"/api/blocks/data/{type}"
                : $"/api/blocks/data/{type}?props={Convert.ToBase64String(Encoding.UTF8.GetBytes(props)).Replace('+', '-').Replace('/', '_').TrimEnd('=')}",
            UriKind.Relative);

    private static async Task<JsonElement> BlockAsync(HttpClient client, string type, CancellationToken cancellationToken) =>
        (await ReadAsync(await client.GetAsync(BlockUri(type), cancellationToken), HttpStatusCode.OK, cancellationToken)).Json;

    private static async Task<(string Text, JsonElement Json)> ReadAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
            return (text, JsonDocument.Parse(text).RootElement.Clone());
        }
    }

    private async Task<CalendarEntry> CalendarEntryAsync(long training, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var source = Training.SourceIdOf(training);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(entry => entry.SourceModule == TrainingModule.ModuleKey && entry.SourceId == source, cancellationToken);
    }

    // ---- writing ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A training of the first trained rating of its ladder, written as the installation straight into the state the test starts from;
    /// a dated one puts its session into the calendar, as the interceptor does for every write.
    /// </summary>
    private async Task<long> AddTrainingAsync(
        int trainee,
        RatingKind kind,
        TrainingState state,
        CancellationToken cancellationToken,
        int? trainer = null,
        DateTime? start = null,
        DateTime? completedAt = null)
    {
        var (atc, pilot) = Trained();
        var now = DateTime.UtcNow;

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = new Training
        {
            Kind = kind,
            Rating = kind == RatingKind.Pilot ? pilot.Number : atc.Number,
            Position = kind == RatingKind.Pilot ? null : Position,
            TraineeVid = trainee,
            TraineeHoursAtRequest = 500m,
            TheoryConfirmedAt = now,
            State = state,
            DecidedBy = state == TrainingState.Requested ? null : CoordinatorVid,
            DecidedAt = state == TrainingState.Requested ? null : now,
            TrainerVid = trainer,
            AssignedBy = trainer is null ? null : CoordinatorVid,
            AssignedAt = trainer is null ? null : now,
            ScheduledStartUtc = start,
            CompletedAt = completedAt,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

    /// <summary>A date proposed for a training, as if its trainer had proposed it <paramref name="proposedAgo"/> ago.</summary>
    private async Task AddSlotAsync(long training, DateTime starts, TimeSpan proposedAgo, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var slot = new TrainingSlot { TrainingId = training, StartsAtUtc = starts, EndsAtUtc = starts.AddHours(2) };
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE trn_slots SET created_at = {DateTime.UtcNow - proposedAgo} WHERE id = {slot.Id}",
            cancellationToken);
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

    /// <summary>The rung below a rating on its ladder: a member's, for whom it is the next training.</summary>
    private Rating RungBelow(Rating rating)
    {
        using var scope = _factory.Services.CreateScope();
        var ladder = scope.ServiceProvider.GetRequiredService<RatingVocabulary>().Ladder(rating.Kind);
        var index = ladder.ToList().FindIndex(rung => rung.Number == rating.Number);
        Assert.True(index > 0, "The first trained rating has a rung below it.");
        return ladder[index - 1];
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A person of this class: their name, whether the hub counts them as staff, and their ratings. Nobody has an address.</summary>
    private async Task SeedPersonAsync(
        int vid,
        string firstName,
        string lastName,
        CancellationToken cancellationToken,
        bool isStaff = false,
        int? atc = null,
        int? pilot = null)
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
        user.Email = null;
        user.IsStaff = isStaff;
        user.IsSuperadmin = false;
        user.RatingAtc = atc;
        user.RatingPilot = pilot;
        user.HoursAtc = 500m;
        user.HoursPilot = 500m;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Permissions on the training department, to one VID.</summary>
    private async Task GrantAsync(int vid, CancellationToken cancellationToken, params string[] permissions)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        foreach (var permission in permissions)
        {
            database.UserGrants.Add(new UserGrant
            {
                Vid = vid,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = Department.TD,
                Effect = GrantEffect.Grant,
                Reason = GrantReason,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What this class leaves: the trainings of its people with their dates and their entries of the calendar, the grants of its
    /// people, their mails and the settings — so no later class counts them.
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

        // The dates go with their trainings (the key cascades).
        await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);

        var key = ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey);
        await hub.DivisionSettings.Where(setting => setting.Key == key).ExecuteDeleteAsync(cancellationToken);
    }
}
