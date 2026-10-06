using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Blocks;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The heads of a FIR in the training (M3, A11b; design M3 §2.4, §3.2, §4.2, §4.3; note 2026-09-27-i-capi-fir-sul-loro-fir §3.8),
/// through the real host with the division file of this repository — its two grants to the team of a FIR and
/// <c>firStaffScope: own</c>, nothing given here by hand:
/// <list type="bullet">
/// <item>the chief of a FIR assigns a training of their FIR, and is refused on one of another FIR and on a pilot's, which has no FIR
/// — the endpoint, the single handler asked on the row and the write guard saying the same —; the assistant chief of the other FIR
/// the other way round; neither accepts a request, nor conducts;</item>
/// <item>they read the trainings of their FIR alone: in the list, which the CRUD engine narrows, on the page of each, in the block of
/// the requests and the trainings to assign, and on a trainee's path, which gives them neither the ladders nor the bans; the lists
/// of the exams and of the bans, whose rows say no FIR, stay closed to them;</item>
/// <item>the history of a training's changes, which travels with its page: read on one of their FIR, none of another FIR (A13d).</item>
/// </list>
/// The FIRs are two of the test's own, known through a directory of FIRs that knows only them, as in <c>FirTeamPermissionTests</c>:
/// nothing is written into the shared reference data. Who writes a training without an endpoint is the identity a sign in puts in the
/// cookie, read by the host's own current user, as in <c>TrainingStaffTests</c>.
/// <para>⚠️ The people are 790074–790078, handed to A11b by dalberone on 29 September 2026 (the review of A8a had them, and did not
/// use them). None of them has an address: the contacts tests count the recipients of the training department.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingFirHeadsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int ChiefVid = 790074;
    private const int AssistantChiefVid = 790075;
    private const int TraineeVid = 790076;
    private const int OtherTraineeVid = 790077;
    private const int TrainerVid = 790078;

    private static readonly int[] Vids = [ChiefVid, AssistantChiefVid, TraineeVid, OtherTraineeVid, TrainerVid];

    // Two FIRs nobody has: the directory of this host knows them and nothing else.
    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";

    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IFirDirectory, TwoFirs>()));
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, bans and positions.
        await CleanAsync(token);

        var (topAtc, topPilot) = (Ladder(RatingKind.Atc)[^1], Ladder(RatingKind.Pilot)[^1]);

        // The chief of one FIR and the assistant chief of the other: a position of the FIR, and nothing of the training department.
        await SeedPersonAsync(ChiefVid, "Fir", "Chief", $"{Fir}-CH", token);
        await SeedPersonAsync(AssistantChiefVid, "Fir", "Assistant", $"{OtherFir}-ACH", token);

        // The trainees, members; and a trainer of the department, rated above every training, whom the heads assign.
        await SeedPersonAsync(TraineeVid, "Test", "Trainee", position: null, token);
        await SeedPersonAsync(OtherTraineeVid, "Other", "Trainee", position: null, token);
        await SeedPersonAsync(TrainerVid, "Department", "Trainer", $"{Division().Code}-T95", token, topAtc.Number, topPilot.Number);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of A11b (<c>08-piano-implementazione-m3.md</c>): the chief of a FIR assigns a training of their FIR and is refused
    /// on one of another FIR — and on a pilot's, which has no FIR —, the endpoint, the single handler and the write guard saying the same;
    /// the assistant chief of the other FIR assigns the other one and is refused on the first. What the division gives them is seeing and
    /// assigning, from its file: not accepting, not conducting.
    /// </summary>
    [Fact]
    public async Task TheChiefOfAFirAssignsATrainingOfTheirFirAndIsRefusedOnOneOfAnotherFir()
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(FirStaffScope.Own, Division().FirStaffScope);

        var ours = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, Fir, token);
        var theirs = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Accepted, OtherFir, token);
        var pilots = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Accepted, fir: null, token);

        using var chief = await SignedInAsync(ChiefVid, token);

        // What the division file gives the team of a FIR, and nothing else of the training: on the training department, the FIR
        // travelling with each permission in the cookie (/api/me does not say it, note §3.2).
        Assert.Equal(
            new[] { TrainingPermissions.Assign, TrainingPermissions.View },
            await TrainingPermissionsOfAsync(chief, token));

        // The training of their FIR: the page offers the assignment and no other step, and the trainers who may train it.
        var page = await PageAsync(chief, ours, token);
        var actions = page.GetProperty("actions");
        Assert.True(actions.GetProperty("canAssign").GetBoolean());
        Assert.False(actions.GetProperty("canDecide").GetBoolean());
        Assert.False(actions.GetProperty("canConduct").GetBoolean());
        Assert.False(actions.GetProperty("canClose").GetBoolean());
        Assert.Contains(TrainerVid, await CandidatesAsync(chief, ours, token));

        var assigned = await DoneAsync(
            await StepAsync(chief, ours, "assign", new { trainerVid = TrainerVid, rowVersion = page.GetProperty("rowVersion").GetDateTime() }, token),
            token);
        Assert.Equal(nameof(TrainingState.Assigned), assigned.GetProperty("state").GetString());
        Assert.Equal(TrainerVid, assigned.GetProperty("trainer").GetProperty("vid").GetInt32());
        Assert.Equal(ChiefVid, assigned.GetProperty("assignedBy").GetProperty("vid").GetInt32());

        var stored = await StoredAsync(ours, token);
        Assert.Equal((TrainingState.Assigned, (int?)TrainerVid, (int?)ChiefVid), (stored.State, stored.TrainerVid, stored.AssignedBy));

        // A training of another FIR, and a pilot's: not read, no trainers offered, the assignment refused, and nothing written.
        foreach (var id in new[] { theirs, pilots })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(chief, Page(id), token));
            Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(chief, Trainers(id), token));

            using (var refused = await StepAsync(chief, id, "assign", new { trainerVid = TrainerVid, rowVersion = (await StoredAsync(id, token)).RowVersion }, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            }

            Assert.Null((await StoredAsync(id, token)).TrainerVid);
        }

        // The single handler asked on each row, and the write guard with no endpoint in the way: the same answers. Nothing else of the
        // training is theirs on their FIR either — accepting, conducting, editing.
        var identity = await IdentityOfAsync(ChiefVid, token);
        Assert.True(await MayAsync(identity, ours, TrainingPermissions.Assign, token));
        Assert.True(await MayAsync(identity, ours, TrainingPermissions.View, token));
        foreach (var permission in new[] { TrainingPermissions.Approve, TrainingPermissions.Conduct, TrainingPermissions.Edit })
        {
            Assert.False(await MayAsync(identity, ours, permission, token));
        }

        foreach (var id in new[] { theirs, pilots })
        {
            Assert.False(await MayAsync(identity, id, TrainingPermissions.Assign, token));
            Assert.False(await MayAsync(identity, id, TrainingPermissions.View, token));
        }

        await WriteAsync(identity, ours, training => training.AssignedAt = DateTime.UtcNow, token);
        await AssertTheGuardRefusesAsync(identity, theirs, training => training.TrainerVid = TrainerVid, token);
        await AssertTheGuardRefusesAsync(identity, pilots, training => training.TrainerVid = TrainerVid, token);
        Assert.Null((await StoredAsync(theirs, token)).TrainerVid);

        // The assistant chief of the other FIR, the other way round: theirs is assigned, ours is not read nor assigned.
        using var assistant = await SignedInAsync(AssistantChiefVid, token);
        Assert.Equal(
            new[] { TrainingPermissions.Assign, TrainingPermissions.View },
            await TrainingPermissionsOfAsync(assistant, token));

        var given = await AssignAsync(assistant, theirs, TrainerVid, token);
        Assert.Equal(AssistantChiefVid, given.GetProperty("assignedBy").GetProperty("vid").GetInt32());

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(assistant, Page(ours), token));
        using (var refused = await StepAsync(assistant, ours, "assign", new { trainerVid = TrainerVid, rowVersion = (await StoredAsync(ours, token)).RowVersion }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.False(await MayAsync(await IdentityOfAsync(AssistantChiefVid, token), ours, TrainingPermissions.Assign, token));
        Assert.Equal(ChiefVid, (await StoredAsync(ours, token)).AssignedBy);
    }

    /// <summary>
    /// What a head of a FIR reads (design M3 §4.2, §4.3): the trainings of their FIR, in every view of the list and in no other; on the
    /// dashboard, those of their FIR to assign and nothing to accept; of a trainee, the trainings of their FIR, and neither where the
    /// trainee stands on each ladder nor the bans, which are the training department's — whose staff reads all of it. The exams and the
    /// bans, rows that say no FIR, stay closed to them.
    /// </summary>
    [Fact]
    public async Task TheChiefOfAFirReadsTheTrainingsOfTheirFirAloneInTheListTheBlockAndThePath()
    {
        var token = TestContext.Current.CancellationToken;

        var requested = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, Fir, token);
        var toAssign = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Accepted, Fir, token);
        var elsewhere = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Completed, OtherFir, token);
        var pilots = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Accepted, fir: null, token);
        await AddBanAsync(OtherTraineeVid, token);

        using var chief = await SignedInAsync(ChiefVid, token);

        // The list: the trainings of their FIR, a request they may not accept included, in the views they belong to; none of another FIR,
        // no pilot's.
        Assert.Equal([requested, toAssign], (await ListedAsync(chief, query: null, token)).Order());
        Assert.Equal([requested], await ListedAsync(chief, $"filter[queue]={StaffQueue.ToApprove}", token));
        Assert.Equal([toAssign], await ListedAsync(chief, $"filter[queue]={StaffQueue.ToAssign}", token));
        Assert.Empty(await ListedAsync(chief, $"filter[queue]={StaffQueue.History}", token));
        Assert.Empty(await ListedAsync(chief, $"filter[kind]={RatingKind.Pilot}", token));

        // The request of their FIR is read, and not theirs to accept; the trainings of another FIR and the pilot's are not read.
        Assert.False((await PageAsync(chief, requested, token)).GetProperty("actions").GetProperty("canDecide").GetBoolean());
        using (var accept = await StepAsync(chief, requested, "accept", new { rowVersion = (await StoredAsync(requested, token)).RowVersion }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, accept.StatusCode);
        }

        Assert.Equal(TrainingState.Requested, (await StoredAsync(requested, token)).State);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(chief, Page(elsewhere), token));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(chief, Page(pilots), token));

        // The dashboard: the training of their FIR to assign, nothing to accept; nothing to conduct.
        var queue = await BlockAsync(chief, ApprovalQueueProvider.BlockType, token);
        Assert.Equal(0, queue.GetProperty("toApprove").GetProperty("count").GetInt32());
        Assert.Equal(1, queue.GetProperty("toAssign").GetProperty("count").GetInt32());
        Assert.Equal([toAssign], Ids(queue.GetProperty("toAssign").GetProperty("oldest")));

        var theirsToConduct = await BlockAsync(chief, TrainerQueueProvider.BlockType, token);
        foreach (var part in new[] { "toPropose", "waiting", "toReport" })
        {
            Assert.Empty(theirsToConduct.GetProperty(part).EnumerateArray());
        }

        // A trainee's path: the trainings of their FIR; not where the trainee stands, not the bans; and no «ban».
        var path = await PathAsync(chief, OtherTraineeVid, token);
        Assert.Equal([toAssign], Ids(path.GetProperty("trainings")));
        Assert.Equal(JsonValueKind.Null, path.GetProperty("ladders").ValueKind);
        Assert.Equal(JsonValueKind.Null, path.GetProperty("bans").ValueKind);
        Assert.False(path.GetProperty("canBan").GetBoolean());
        Assert.Equal([requested], Ids((await PathAsync(chief, TraineeVid, token)).GetProperty("trainings")));

        // The staff of the training department reads all of it: every training, where the trainee stands, the ban.
        using var trainer = await SignedInAsync(TrainerVid, token);
        var whole = await PathAsync(trainer, OtherTraineeVid, token);
        Assert.Equal([toAssign, elsewhere], Ids(whole.GetProperty("trainings")).Order());
        Assert.Equal(2, whole.GetProperty("ladders").GetArrayLength());
        Assert.Single(whole.GetProperty("bans").EnumerateArray());

        // The exams and the bans say no FIR: their lists stay closed to a head of a FIR, who belongs to no department.
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(chief, new Uri($"{ExamEndpoints.Pattern}?pageSize=100", UriKind.Relative), token));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(chief, new Uri($"{BanEndpoints.Pattern}?pageSize=100", UriKind.Relative), token));
    }

    /// <summary>
    /// The history of a training's changes (A13b) from the side of a head of a FIR (A13d; the review of A13b on #197): it travels with
    /// the page of the training, so they read it on a training of their FIR — the request the installation wrote, by nobody, and their
    /// own assignment — and get none of a training of another FIR, whose page is not theirs (A11b); the assistant chief of that FIR
    /// reads it there.
    /// </summary>
    [Fact]
    public async Task TheChiefOfAFirReadsTheHistoryOfATrainingOfTheirFirAndNoneOfAnotherFir()
    {
        var token = TestContext.Current.CancellationToken;

        var ours = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, Fir, token);
        var theirs = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Accepted, OtherFir, token);

        using var chief = await SignedInAsync(ChiefVid, token);
        using var assistant = await SignedInAsync(AssistantChiefVid, token);
        await AssignAsync(chief, ours, TrainerVid, token);
        await AssignAsync(assistant, theirs, TrainerVid, token);

        // Their FIR's: every step, with who took it, and the trainer they gave it.
        var page = await PageAsync(chief, ours, token);
        Assert.Equal([("Requested", (int?)null), ("Assigned", ChiefVid)], Events(page));
        Assert.Equal(TrainerVid, page.GetProperty("history")[1].GetProperty("trainer").GetProperty("vid").GetInt32());

        // Another FIR's: the page is refused, and the history with it — nothing of it in the answer.
        using (var refused = await chief.GetAsync(Page(theirs), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.DoesNotContain("history", await refused.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        }

        Assert.Equal([("Requested", (int?)null), ("Assigned", AssistantChiefVid)], Events(await PageAsync(assistant, theirs, token)));
    }

    // ---- the API -----------------------------------------------------------------------------------------------------------

    private static Uri Page(long id) => new($"{StaffEndpoints.Pattern}/{id}", UriKind.Relative);

    private static Uri Trainers(long id) => new($"{StaffEndpoints.Pattern}/{id}/trainers", UriKind.Relative);

    private static Task<HttpResponseMessage> StepAsync(HttpClient client, long id, string verb, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{StaffEndpoints.Pattern}/{id}/{verb}", UriKind.Relative), body, cancellationToken);

    private static async Task<JsonElement> PageAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(Page(id), cancellationToken);

    /// <summary>What the history of a page says, step by step: what happened, and who did it — none for the hub.</summary>
    private static List<(string?, int?)> Events(JsonElement page) =>
    [
        .. page.GetProperty("history").EnumerateArray().Select(entry => (
            entry.GetProperty("event").GetString(),
            entry.GetProperty("by").ValueKind == JsonValueKind.Null ? (int?)null : entry.GetProperty("by").GetProperty("vid").GetInt32())),
    ];

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, Uri uri, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, cancellationToken);
        return response.StatusCode;
    }

    /// <summary>The trainer given by the verb of the page, at the version the page has now.</summary>
    private static async Task<JsonElement> AssignAsync(HttpClient client, long id, int trainerVid, CancellationToken cancellationToken)
    {
        var version = (await PageAsync(client, id, cancellationToken)).GetProperty("rowVersion").GetDateTime();
        return await DoneAsync(await StepAsync(client, id, "assign", new { trainerVid, rowVersion = version }, cancellationToken), cancellationToken);
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

    /// <summary>The trainers a training may be given, as the page of the assignment reads them.</summary>
    private static async Task<List<int>> CandidatesAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
    [
        .. (await client.GetFromJsonAsync<JsonElement>(Trainers(id), cancellationToken))
            .EnumerateArray()
            .Select(candidate => candidate.GetProperty("vid").GetInt32()),
    ];

    /// <summary>The trainings of this class's trainees the list shows the client, with the filters of <paramref name="query"/>.</summary>
    private static async Task<List<long>> ListedAsync(HttpClient client, string? query, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        foreach (var trainee in new[] { TraineeVid, OtherTraineeVid })
        {
            var filters = query is null ? string.Empty : $"&{query}";
            var page = await client.GetFromJsonAsync<JsonElement>(
                new Uri($"{StaffEndpoints.QueuePattern}?filter[traineeVid]={trainee}&pageSize=100{filters}", UriKind.Relative),
                cancellationToken);
            ids.AddRange(Ids(page.GetProperty("items")));
        }

        return ids;
    }

    private static async Task<JsonElement> PathAsync(HttpClient client, int vid, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"{TraineePathEndpoints.Pattern}/{vid}", UriKind.Relative), cancellationToken);

    private static async Task<JsonElement> BlockAsync(HttpClient client, string type, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/blocks/data/{type}", UriKind.Relative), cancellationToken);

    private static List<long> Ids(JsonElement rows) => [.. rows.EnumerateArray().Select(row => row.GetProperty("id").GetInt64())];

    /// <summary>The permissions of the training <c>/api/me</c> says the signed in member holds, by name, each once.</summary>
    private static async Task<List<string>> TrainingPermissionsOfAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var me = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/me", UriKind.Relative), cancellationToken);

        return
        [
            .. me.GetProperty("permissions").EnumerateArray()
                .Where(permission => permission.GetProperty("department").GetString() == nameof(Department.TD))
                .Select(permission => permission.GetProperty("name").GetString()!)
                .Where(name => name.StartsWith($"{TrainingPermissions.Area}.", StringComparison.Ordinal))
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    // ---- the handler and the guard, with no endpoint in the way ---------------------------------------------------------------

    /// <summary>The identity a login of <paramref name="vid"/> would put in the cookie now: positions, grants, and the FIR of each.</summary>
    private async Task<ClaimsPrincipal> IdentityOfAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var signedIn = (await scope.ServiceProvider.GetRequiredService<UserSyncService>().LoadAsync(vid, cancellationToken))!;

        return new ClaimsPrincipal(HubClaims.BuildIdentity(
            signedIn.User.Vid,
            signedIn.User.FirstName,
            signedIn.User.LastName,
            signedIn.User.Locale ?? Division().DefaultLocale,
            signedIn.User.SecurityStamp,
            signedIn.User.IsSuperadmin,
            signedIn.User.IsStaff,
            signedIn.Positions,
            signedIn.Permissions));
    }

    private Task<bool> MayAsync(ClaimsPrincipal who, long id, string permission, CancellationToken cancellationToken) =>
        AsAsync(who, async services =>
        {
            var training = await services.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(row => row.Id == id, cancellationToken);
            return (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(who, training, permission)).Succeeded;
        });

    private Task WriteAsync(ClaimsPrincipal who, long id, Action<Training> change, CancellationToken cancellationToken) =>
        AsAsync(who, async services =>
        {
            var database = services.GetRequiredService<TrainingDbContext>();
            change(await database.Trainings.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken));
            return await database.SaveChangesAsync(cancellationToken);
        });

    /// <summary>A write of the training the guard refuses <paramref name="who"/>, asking the <c>Training.Edit</c> they do not hold.</summary>
    private async Task AssertTheGuardRefusesAsync(ClaimsPrincipal who, long id, Action<Training> change, CancellationToken cancellationToken)
    {
        var refused = await Assert.ThrowsAsync<ForbiddenDomainException>(() => WriteAsync(who, id, change, cancellationToken));
        Assert.Equal(TrainingPermissions.Edit, refused.Permission);
    }

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own as <paramref name="who"/>: the identity goes where the cookie middleware puts
    /// it, the request, and the host's current user reads it from there.
    /// </summary>
    private async Task<T> AsAsync<T>(ClaimsPrincipal who, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = who, RequestServices = scope.ServiceProvider };

        try
        {
            return await work(scope.ServiceProvider);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    // ---- rows and people ------------------------------------------------------------------------------------------------------

    private async Task<Training> StoredAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(training => training.Id == id, cancellationToken);
    }

    /// <summary>
    /// A training of the first trained rating of its ladder, on a position of <paramref name="fir"/> — none for a pilot's —, written as
    /// the installation: past the request of A6a, straight into the state the test starts from.
    /// </summary>
    private async Task<long> AddTrainingAsync(int trainee, RatingKind kind, TrainingState state, string? fir, CancellationToken cancellationToken)
    {
        var (atc, pilot) = Trained();
        var now = DateTime.UtcNow;
        var decided = state is not (TrainingState.Requested or TrainingState.Cancelled);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = new Training
        {
            Kind = kind,
            Rating = kind == RatingKind.Pilot ? pilot.Number : atc.Number,
            Position = fir is null ? null : $"{fir}_CTR",
            Fir = fir,
            TraineeVid = trainee,
            TraineeHoursAtRequest = 500m,
            TheoryConfirmedAt = now,
            AvailabilityText = "trn-test availability",
            State = state,
            DecidedAt = decided ? now : null,
            CompletedAt = state == TrainingState.Completed ? now : null,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

    /// <summary>A ban until somebody lifts it, written as the installation.</summary>
    private async Task AddBanAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        database.TraineeBans.Add(new TraineeBan { Vid = vid, Reason = "trn-test: written by the installation" });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A person of this class, never with an address: their name, their ratings, and a position as the network spells it — a FIR's,
    /// known to this host's directory, or one of the training department.
    /// </summary>
    private async Task SeedPersonAsync(
        int vid,
        string firstName,
        string lastName,
        string? position,
        CancellationToken cancellationToken,
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
        user.IsStaff = position is not null;
        user.IsSuperadmin = false;
        user.RatingAtc = atc;
        user.RatingPilot = pilot;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null)
        {
            var parsed = StaffRoleMap.Parse(position, Division().Code, TwoFirs.Ids);
            Assert.NotNull(parsed);
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = parsed.Raw,
                Department = parsed.Department,
                Level = parsed.Level,
                Fir = parsed.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What this class leaves: the trainings of its trainees, with the entries of the calendar their sessions project, their bans, the
    /// grants and positions of its people and their mails — so no later class counts them.
    /// </summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var training = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var sources = (await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).Select(row => row.Id).ToListAsync(cancellationToken))
            .Select(Training.SourceIdOf)
            .ToList();
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && sources.Contains(entry.SourceId))
            .ExecuteDeleteAsync(cancellationToken);
        await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await training.TraineeBans.IgnoreQueryFilters().Where(ban => Vids.Contains(ban.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => Vids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
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

    /// <summary>The airspace of this host: two FIRs of the test's own, and no airport.</summary>
    private sealed class TwoFirs : IFirDirectory
    {
        public static readonly IReadOnlySet<string> Ids = new HashSet<string>([Fir, OtherFir], StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Ids);

        public Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new IvaoAirspace(Ids, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        public void Invalidate()
        {
        }
    }
}
