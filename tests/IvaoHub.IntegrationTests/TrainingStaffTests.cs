using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The staff's side of a training (M3, A7; design M3 §2.3, §2.4, §3.3, §4.2, §5.3), through the real host: an advisor — by a
/// grant, without <c>Training.Edit</c> — accepts and does not assign; a refusal carries the reason the trainee reads; the
/// coordinator assigns a trainer with the rating and not one below it; the trainer gets the grant on that training alone, in
/// <c>/api/me</c> with its scope, conducts it and no other, and loses it when the training is given to somebody else; an
/// assignment from a version somebody moved is a conflict, which takes back the grant it wrote and never the one of the trainer
/// the training names; nobody approves or assigns a training of their own, the super administrator included; the night takes
/// the grant back once the training is over; and the list holds its views for whoever reads trainings.
/// <para>⚠️ The staff of the training is seeded without an address (<c>CONTRIBUTING.md</c>, "Tests": the contacts tests count the
/// recipients of the training department): the trainers hold a trainer's position and no mailbox. The trainer whose mail is
/// looked for holds a position of the direction instead — the direction is staff of the training too (§2.4) —, and their
/// address and position are taken back with everything else this class writes. Who writes the training without an endpoint is
/// the identity a login puts in the cookie, read by the host's own current user, as in <c>AlternativeWritePermissionTests</c>:
/// a permission held on one row is exactly what these tests prove.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingStaffTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790021 are A1's to A6a's.
    private const int TraineeVid = 790022;
    private const int AdvisorVid = 790023;
    private const int CoordinatorVid = 790024;
    private const int TrainerVid = 790025;
    private const int SecondTrainerVid = 790026;
    private const int LowTrainerVid = 790027;
    private const int StaffTraineeVid = 790028;
    private const int SuperadminVid = 790029;
    private const int DirectionTrainerVid = 790030;
    private const int OtherTraineeVid = 790031;

    private static readonly int[] Vids =
    [
        TraineeVid, AdvisorVid, CoordinatorVid, TrainerVid, SecondTrainerVid, LowTrainerVid, StaffTraineeVid, SuperadminVid,
        DirectionTrainerVid, OtherTraineeVid,
    ];

    private const string GrantReason = "trn-test";

    private static readonly Uri Queue = new(StaffEndpoints.QueuePattern, UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, grants and positions.
        await CleanAsync(token);

        var (atc, pilot) = Trained();
        var (topAtc, topPilot) = (Ladder(RatingKind.Atc)[^1], Ladder(RatingKind.Pilot)[^1]);
        var code = Division().Code;

        // The trainees: members, and one of them has a mailbox for the mails of the decisions.
        await SeedPersonAsync(TraineeVid, "Test", "Trainee", $"trn-test-{TraineeVid}@example.invalid", token);
        await SeedPersonAsync(OtherTraineeVid, "Other", "Trainee", email: null, token);

        // The advisor and the coordinator, by a grant to their VID: approving, assigning, never Training.Edit.
        await SeedPersonAsync(AdvisorVid, "Test", "Advisor", email: null, token, isStaff: true);
        await GrantAsync(AdvisorVid, TrainingPermissions.View, token);
        await GrantAsync(AdvisorVid, TrainingPermissions.Approve, token);
        await SeedPersonAsync(CoordinatorVid, "Test", "Coordinator", email: null, token, isStaff: true);
        await GrantAsync(CoordinatorVid, TrainingPermissions.View, token);
        await GrantAsync(CoordinatorVid, TrainingPermissions.Assign, token);

        // The trainers: the department's own positions, no mailbox; one with a rating below the one trained.
        await SeedPersonAsync(TrainerVid, "First", "Trainer", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-T91");
        await SeedPersonAsync(SecondTrainerVid, "Second", "Trainer", email: null, token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-T92");
        await SeedPersonAsync(LowTrainerVid, "Low", "Trainer", email: null, token, isStaff: true, atc: RungBelow(atc).Number, pilot: RungBelow(pilot).Number, position: $"{code}-T93");

        // The direction is staff of the training too, and this one has a mailbox: the trainer's mail is looked for there.
        await SeedPersonAsync(
            DirectionTrainerVid, "Direction", "Trainer", $"trn-test-{DirectionTrainerVid}@example.invalid", token, isStaff: true, atc: topAtc.Number, pilot: topPilot.Number, position: $"{code}-ADIR");

        // Somebody of the staff who is a trainee too, and a super administrator who is one.
        await SeedPersonAsync(StaffTraineeVid, "Staff", "Trainee", email: null, token, isStaff: true);
        await GrantAsync(StaffTraineeVid, TrainingPermissions.View, token);
        await GrantAsync(StaffTraineeVid, TrainingPermissions.Approve, token);
        await GrantAsync(StaffTraineeVid, TrainingPermissions.Assign, token);
        await SeedPersonAsync(SuperadminVid, "Test", "Superadmin", email: null, token, isStaff: true, isSuperadmin: true);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AnAdvisorAcceptsARequestAndAssignsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        await WriteSettingsAsync(new { theoryExamUrl = "https://trn-test.example.invalid/theory" }, token);

        try
        {
            using var advisor = await SignedInAsync(AdvisorVid, token);

            // The page: the request, the reminder's site of the exam, and what the advisor may do — decide, not assign.
            var page = await PageAsync(advisor, id, token);
            Assert.Equal(nameof(TrainingState.Requested), page.GetProperty("state").GetString());
            Assert.Equal(TraineeVid, page.GetProperty("trainee").GetProperty("vid").GetInt32());
            Assert.Equal("Test Trainee", page.GetProperty("trainee").GetProperty("name").GetString());
            Assert.Equal("https://trn-test.example.invalid/theory", page.GetProperty("theoryExamUrl").GetString());
            Assert.NotEqual(JsonValueKind.Null, page.GetProperty("theoryConfirmedAt").ValueKind);

            // The comment for the staff arrived with A9's one function (note le-note-riservate-e-il-trainee): empty on a request, and
            // nothing is left out for an advisor, who is not its trainee.
            Assert.Equal(JsonValueKind.Null, page.GetProperty("staffComment").ValueKind);
            Assert.False(page.GetProperty("reservedLeftOut").GetBoolean());
            Assert.True(page.GetProperty("actions").GetProperty("canDecide").GetBoolean());
            Assert.False(page.GetProperty("actions").GetProperty("canAssign").GetBoolean());
            var version = page.GetProperty("rowVersion").GetDateTime();

            // Assigning is not theirs, nor the list of the trainers.
            using (var trainers = await advisor.GetAsync(Trainers(id), token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, trainers.StatusCode);
            }

            using (var assign = await StepAsync(advisor, id, "assign", new { trainerVid = TrainerVid, rowVersion = version }, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
            }

            // A version they did not see: somebody moved the training meanwhile.
            using (var stale = await StepAsync(advisor, id, "accept", new { rowVersion = version.AddSeconds(-1) }, token))
            {
                Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            }

            var accepted = await DoneAsync(await StepAsync(advisor, id, "accept", new { rowVersion = version }, token), token);
            Assert.Equal(nameof(TrainingState.Accepted), accepted.GetProperty("state").GetString());
            Assert.Equal(AdvisorVid, accepted.GetProperty("decidedBy").GetProperty("vid").GetInt32());
            Assert.False(accepted.GetProperty("actions").GetProperty("canDecide").GetBoolean());

            // Written by the advisor, through the write guard with Training.Approve on the row; audited as theirs.
            var stored = await StoredAsync(id, token);
            Assert.Equal((TrainingState.Accepted, AdvisorVid), (stored.State, stored.DecidedBy));
            Assert.NotNull(stored.DecidedAt);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var key = id.ToString(CultureInfo.InvariantCulture);
                Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
                    .AnyAsync(entry => entry.Entity == "trn_trainings" && entry.EntityId == key && entry.Vid == AdvisorVid, token));
            }

            // The trainee is told, and a request accepted is not accepted twice.
            Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.RequestAccepted, token));
            using (var again = await StepAsync(advisor, id, "accept", new { rowVersion = accepted.GetProperty("rowVersion").GetDateTime() }, token))
            {
                await AssertRefusedAsync(again, "state", "training:errors.trainingNotRequested", token);
            }
        }
        finally
        {
            await ForgetSettingsAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ARefusalCarriesTheReasonTheTraineeReads()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Requested, token);

        using var advisor = await SignedInAsync(AdvisorVid, token);
        var version = (await PageAsync(advisor, id, token)).GetProperty("rowVersion").GetDateTime();

        // A reason is asked for, and bounded.
        foreach (var missing in new[] { null, "   " })
        {
            using var refused = await StepAsync(advisor, id, "reject", new { reason = missing, rowVersion = version }, token);
            await AssertRefusedAsync(refused, "reason", "errors.required", token);
        }

        using (var tooLong = await StepAsync(advisor, id, "reject", new { reason = new string('x', Training.MaxTextLength + 1), rowVersion = version }, token))
        {
            await AssertRefusedAsync(tooLong, "reason", "errors.text.tooLong", token);
        }

        var rejected = await DoneAsync(
            await StepAsync(advisor, id, "reject", new { reason = "  trn-test: 20 more hours as a controller.  ", rowVersion = version }, token),
            token);
        Assert.Equal(nameof(TrainingState.Rejected), rejected.GetProperty("state").GetString());
        Assert.Equal(nameof(TrainingRejection.Staff), rejected.GetProperty("rejection").GetString());
        Assert.Equal("trn-test: 20 more hours as a controller.", rejected.GetProperty("rejectionReason").GetString());

        // The trainee reads it on their page, and in the mail.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var mine = await trainee.GetFromJsonAsync<JsonElement>(new Uri(RequestEndpoints.Pattern, UriKind.Relative), token);
        var theirs = mine.GetProperty("trainings").EnumerateArray().Single(training => training.GetProperty("id").GetInt64() == id);
        Assert.Equal("trn-test: 20 more hours as a controller.", theirs.GetProperty("rejectionReason").GetString());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var mail = await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
                .SingleAsync(notification => notification.Vid == TraineeVid && notification.Type == TrainingNotifications.RequestRejected, token);
            Assert.Contains("trn-test: 20 more hours as a controller.", mail.DataJson, StringComparison.Ordinal);
        }

        // Refused is not waiting: it is neither accepted nor refused again.
        using (var accept = await StepAsync(advisor, id, "accept", new { rowVersion = rejected.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(accept, "state", "training:errors.trainingNotRequested", token);
        }
    }

    [Fact]
    public async Task TheCoordinatorAssignsATrainerWithTheRatingAndNotOneBelowIt()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var page = await PageAsync(coordinator, id, token);
        Assert.True(page.GetProperty("actions").GetProperty("canAssign").GetBoolean());
        Assert.False(page.GetProperty("actions").GetProperty("canDecide").GetBoolean());
        var version = page.GetProperty("rowVersion").GetDateTime();

        // The candidates: the staff of the training the hub knows, with the rating — the direction included —, and nobody else.
        var candidates = (await coordinator.GetFromJsonAsync<JsonElement>(Trainers(id), token))
            .EnumerateArray().Select(candidate => candidate.GetProperty("vid").GetInt32()).ToList();
        Assert.Contains(TrainerVid, candidates);
        Assert.Contains(SecondTrainerVid, candidates);
        Assert.Contains(DirectionTrainerVid, candidates);
        Assert.DoesNotContain(LowTrainerVid, candidates);
        Assert.DoesNotContain(TraineeVid, candidates);
        Assert.DoesNotContain(AdvisorVid, candidates);
        Assert.DoesNotContain(CoordinatorVid, candidates);

        // The server asks the rule again, whatever is sent: the rating, the staff, the trainee, somebody at all.
        (int Vid, string Key)[] refusals =
        [
            (LowTrainerVid, TrainerChoice.RatingTooLow),
            (AdvisorVid, TrainerChoice.NotStaff),
            (TraineeVid, TrainerChoice.IsTrainee),
            (0, "errors.required"),
        ];
        foreach (var (vid, key) in refusals)
        {
            using var refused = await StepAsync(coordinator, id, "assign", new { trainerVid = vid, rowVersion = version }, token);
            await AssertRefusedAsync(refused, "trainerVid", key, token);
        }

        // Accepting is not the coordinator's here: they hold Training.Assign alone.
        using (var accept = await StepAsync(coordinator, id, "accept", new { rowVersion = version }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, accept.StatusCode);
        }

        var assigned = await DoneAsync(
            await StepAsync(coordinator, id, "assign", new { trainerVid = DirectionTrainerVid, rowVersion = version }, token),
            token);
        Assert.Equal(nameof(TrainingState.Assigned), assigned.GetProperty("state").GetString());
        Assert.Equal(DirectionTrainerVid, assigned.GetProperty("trainer").GetProperty("vid").GetInt32());
        Assert.Equal(CoordinatorVid, assigned.GetProperty("assignedBy").GetProperty("vid").GetInt32());

        // The grant on that training, and the two mails: the trainee's, and the trainer's that points at the training's page.
        Assert.Equal([DirectionTrainerVid], await HoldersOfConductAsync(id, token));
        Assert.Equal(1, await MailsAsync(TraineeVid, TrainingNotifications.TrainerAssigned, token));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var mail = await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
                .SingleAsync(notification => notification.Vid == DirectionTrainerVid && notification.Type == TrainingNotifications.TrainerAssigned, token);
            Assert.Contains($"/staff/training/{id}", mail.DataJson, StringComparison.Ordinal);
            Assert.Contains("Test Trainee", mail.DataJson, StringComparison.Ordinal);
        }

        // The same trainer again is nothing to do.
        using (var same = await StepAsync(coordinator, id, "assign", new { trainerVid = DirectionTrainerVid, rowVersion = assigned.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(same, "trainerVid", "training:errors.trainerAlready", token);
        }
    }

    [Fact]
    public async Task TheTrainerConductsTheirTrainingAndNoOtherUntilItIsGivenToSomebodyElse()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, token);
        var another = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Accepted, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        await AssignAsync(coordinator, theirs, TrainerVid, token);

        // The trainer signs in again and holds Training.Conduct on that training alone, with its scope.
        using var trainer = await SignedInAsync(TrainerVid, token);
        Assert.Equal([Training.ScopeOf(theirs)], await ConductScopesAsync(trainer, token));

        // The one handler, on the row: theirs, and not the other.
        var identity = await IdentityOfAsync(TrainerVid, token);
        Assert.True(await MayAsync(identity, theirs, TrainingPermissions.Conduct, token));
        Assert.False(await MayAsync(identity, another, TrainingPermissions.Conduct, token));

        // The write guard, on the row: what conducting writes — here a date, as the override of A8 will — goes on theirs and not
        // on the other.
        var session = DateTime.UtcNow.AddDays(7);
        await WriteAsync(
            identity,
            theirs,
            training =>
            {
                training.ScheduledStartUtc = session;
                training.State = TrainingState.Scheduled;
            },
            token);
        await Assert.ThrowsAsync<ForbiddenDomainException>(() => WriteAsync(identity, another, training => training.ScheduledStartUtc = session, token));
        Assert.Null((await StoredAsync(another, token)).ScheduledStartUtc);

        // Given to somebody else: the first trainer's grant goes, the second's comes; the dated training keeps its state and date.
        var reassigned = await AssignAsync(coordinator, theirs, SecondTrainerVid, token);
        Assert.Equal(SecondTrainerVid, reassigned.GetProperty("trainer").GetProperty("vid").GetInt32());
        Assert.Equal(nameof(TrainingState.Scheduled), reassigned.GetProperty("state").GetString());
        Assert.Equal(session, reassigned.GetProperty("scheduledStartUtc").GetDateTime(), TimeSpan.FromSeconds(1));
        Assert.Equal([SecondTrainerVid], await HoldersOfConductAsync(theirs, token));

        // The grant taken signs the first trainer out; signed in again, they hold nothing on it.
        using (var stale = await trainer.GetAsync(Queue, token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        }

        using var again = await SignedInAsync(TrainerVid, token);
        Assert.Empty(await ConductScopesAsync(again, token));
        Assert.False(await MayAsync(await IdentityOfAsync(TrainerVid, token), theirs, TrainingPermissions.Conduct, token));
    }

    [Fact]
    public async Task AnAssignmentFromAVersionSomebodyMovedIsAConflictAndTakesNothingFromTheTrainerTheTrainingNames()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var version = (await PageAsync(coordinator, id, token)).GetProperty("rowVersion").GetDateTime();
        var identity = await IdentityOfAsync(CoordinatorVid, token);

        // The same trainer twice from the version the page showed — a double submit, or two coordinators at once: the second
        // read the training before the first saved it, finds the grant the first wrote, and its save is a conflict.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => AsAsync(identity, async services =>
        {
            var staff = services.GetRequiredService<StaffTrainings>();
            var read = (await staff.FindAsync(id, tracked: true, token))!;

            await AssignAsync(coordinator, id, TrainerVid, token);

            return await staff.AssignAsync(read, new TrainingAssignmentDto(TrainerVid, version), token);
        }));

        // The training is the first one's, and so is the grant: the conflict took nothing from the trainer it names.
        Assert.Equal(TrainerVid, (await StoredAsync(id, token)).TrainerVid);
        Assert.Equal([TrainerVid], await HoldersOfConductAsync(id, token));

        // Somebody else from that old version: a 409, and the grant this assignment wrote goes back with it.
        using (var stale = await StepAsync(coordinator, id, "assign", new { trainerVid = SecondTrainerVid, rowVersion = version }, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        Assert.Equal(TrainerVid, (await StoredAsync(id, token)).TrainerVid);
        Assert.Equal([TrainerVid], await HoldersOfConductAsync(id, token));
    }

    [Fact]
    public async Task NobodyApprovesOrAssignsTheirOwnTrainingTheSuperAdministratorIncluded()
    {
        var token = TestContext.Current.CancellationToken;

        foreach (var vid in new[] { StaffTraineeVid, SuperadminVid })
        {
            var request = await AddTrainingAsync(vid, RatingKind.Atc, TrainingState.Requested, token);
            var accepted = await AddTrainingAsync(vid, RatingKind.Pilot, TrainingState.Accepted, token);

            using var own = await SignedInAsync(vid, token);

            // Their own training they read — the core never denies reading —, with nothing to press.
            var page = await PageAsync(own, request, token);
            Assert.False(page.GetProperty("actions").GetProperty("canDecide").GetBoolean());
            var version = page.GetProperty("rowVersion").GetDateTime();
            Assert.False((await PageAsync(own, accepted, token)).GetProperty("actions").GetProperty("canAssign").GetBoolean());

            foreach (var verb in new[] { "accept", "reject" })
            {
                using var decided = await StepAsync(own, request, verb, new { reason = "trn-test mine", rowVersion = version }, token);
                Assert.Equal(HttpStatusCode.Forbidden, decided.StatusCode);
            }

            using (var trainers = await own.GetAsync(Trainers(accepted), token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, trainers.StatusCode);
            }

            using (var assigned = await StepAsync(
                own,
                accepted,
                "assign",
                new { trainerVid = TrainerVid, rowVersion = (await StoredAsync(accepted, token)).RowVersion },
                token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
            }

            Assert.Equal(TrainingState.Requested, (await StoredAsync(request, token)).State);
            Assert.Null((await StoredAsync(accepted, token)).TrainerVid);
            Assert.Empty(await HoldersOfConductAsync(accepted, token));
        }

        // And both of them on somebody else's: yes.
        var others = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        using var staff = await SignedInAsync(StaffTraineeVid, token);
        var accept = await DoneAsync(await StepAsync(staff, others, "accept", new { rowVersion = (await StoredAsync(others, token)).RowVersion }, token), token);
        Assert.Equal(nameof(TrainingState.Accepted), accept.GetProperty("state").GetString());

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var given = await AssignAsync(superadmin, others, TrainerVid, token);
        Assert.Equal(TrainerVid, given.GetProperty("trainer").GetProperty("vid").GetInt32());
    }

    [Fact]
    public async Task TheNightTakesBackTheGrantOfATrainingThatIsOver()
    {
        var token = TestContext.Current.CancellationToken;
        var over = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, token);
        var going = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Accepted, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        await AssignAsync(coordinator, over, TrainerVid, token);
        await AssignAsync(coordinator, going, SecondTrainerVid, token);

        // The first one is reported (A9 writes it; here the installation does).
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            var training = await database.Trainings.IgnoreQueryFilters().SingleAsync(row => row.Id == over, token);
            training.State = TrainingState.Completed;
            training.CompletedAt = DateTime.UtcNow;
            await database.SaveChangesAsync(token);
        }

        // Two grants left behind on the training still going, by assignments that stopped half way: one of hours ago, one
        // of a moment ago — which may be an assignment still writing, and is left alone.
        var department = (await StoredAsync(going, token)).OwnerDepartment;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var grants = scope.ServiceProvider.GetRequiredService<ModuleGrants>();
            Assert.Null(await grants.GiveAsync(TrainerVid, TrainingPermissions.Conduct, department, Training.ScopeOf(going), GrantReason, token));
            Assert.Null(await grants.GiveAsync(DirectionTrainerVid, TrainingPermissions.Conduct, department, Training.ScopeOf(going), GrantReason, token));

            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var old = await hub.UserGrants.AsNoTracking()
                .SingleAsync(grant => grant.Vid == TrainerVid && grant.ResourceScope == Training.ScopeOf(going), token);
            await hub.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE hub_user_grants SET created_at = {DateTime.UtcNow.AddHours(-3)} WHERE id = {old.Id}",
                token);
        }

        int taken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            taken = await scope.ServiceProvider.GetRequiredService<TrainingExpiryJob>().RunAsync(token);
        }

        Assert.True(taken >= 2, $"{taken} grant(s) taken back");
        Assert.Empty(await HoldersOfConductAsync(over, token));
        Assert.Equal([SecondTrainerVid, DirectionTrainerVid], await HoldersOfConductAsync(going, token));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var run = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
                .Where(entry => entry.Job == TrainingExpiryJob.JobName)
                .OrderByDescending(entry => entry.Id)
                .FirstAsync(token);
            Assert.Equal("succeeded", run.Status);
        }
    }

    [Fact]
    public async Task TheListHoldsItsViewsForWhoeverReadsTrainings()
    {
        var token = TestContext.Current.CancellationToken;
        var yesterday = DateTime.UtcNow.AddDays(-2);

        var requested = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        var accepted = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Accepted, token);
        var completed = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Completed, token);
        var rejected = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Rejected, token);
        var assigned = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
        var held = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: yesterday);

        // A trainer reads every training (R.1): their position gives them Training.View.
        using var trainer = await SignedInAsync(TrainerVid, token);

        Assert.Equal([requested], await ViewAsync(trainer, StaffQueue.ToApprove, token));
        Assert.Equal([accepted], await ViewAsync(trainer, StaffQueue.ToAssign, token));
        Assert.Equal([assigned], await ViewAsync(trainer, StaffQueue.InProgress, token));
        Assert.Equal([held], await ViewAsync(trainer, StaffQueue.ToClose, token));
        Assert.Equal([completed, rejected], (await ViewAsync(trainer, StaffQueue.History, token)).Order());

        // A row names the trainee and the trainer, and the rating by its short name.
        var row = await RowAsync(trainer, $"?filter[queue]={StaffQueue.InProgress}&filter[trainerVid]={TrainerVid}&q={OtherTraineeVid}", assigned, token);
        Assert.Equal("Other Trainee", row.GetProperty("trainee").GetProperty("name").GetString());
        Assert.Equal("First Trainer", row.GetProperty("trainer").GetProperty("name").GetString());
        Assert.Equal(Trained().Atc.ShortName, row.GetProperty("ratingShortName").GetString());

        // A search by VID, a view that does not exist, and a member who reads no trainings.
        var found = await IdsAsync(trainer, $"?q={TraineeVid}&pageSize=100", token);
        Assert.Equal([requested, accepted, completed, rejected], found.Order());

        using (var unknown = await trainer.GetAsync(new Uri($"{Queue}?filter[queue]=someday", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        }

        using var member = await SignedInAsync(TraineeVid, token);
        using (var refused = await member.GetAsync(Queue, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (var page = await member.GetAsync(new Uri($"{StaffEndpoints.Pattern}/{requested}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static Uri Trainers(long id) => new($"{StaffEndpoints.Pattern}/{id}/trainers", UriKind.Relative);

    private static Task<HttpResponseMessage> StepAsync(HttpClient client, long id, string verb, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{StaffEndpoints.Pattern}/{id}/{verb}", UriKind.Relative), body, cancellationToken);

    private static async Task<JsonElement> PageAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>(new Uri($"{StaffEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken);

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

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        var problem = JsonDocument.Parse(text).RootElement;
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var keys), text);
        Assert.Contains(key, keys.EnumerateArray().Select(error => error.GetString()));
    }

    /// <summary>The trainings of one view, among those of this class's trainees.</summary>
    private async Task<List<long>> ViewAsync(HttpClient client, string view, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        foreach (var trainee in new[] { TraineeVid, OtherTraineeVid })
        {
            ids.AddRange(await IdsAsync(client, $"?filter[queue]={view}&filter[traineeVid]={trainee}&pageSize=100", cancellationToken));
        }

        return ids;
    }

    private async Task<List<long>> IdsAsync(HttpClient client, string query, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"{Queue}{query}", UriKind.Relative), cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("id").GetInt64())];
    }

    private async Task<JsonElement> RowAsync(HttpClient client, string query, long id, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(new Uri($"{Queue}{query}", UriKind.Relative), cancellationToken);
        return page.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("id").GetInt64() == id);
    }

    /// <summary>The scopes on which <c>/api/me</c> says the signed in member conducts a training.</summary>
    private static async Task<List<string?>> ConductScopesAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var me = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/me", UriKind.Relative), cancellationToken);
        return
        [
            .. me.GetProperty("permissions").EnumerateArray()
                .Where(permission => permission.GetProperty("name").GetString() == TrainingPermissions.Conduct)
                .Select(permission => permission.GetProperty("resourceScope").GetString()),
        ];
    }

    /// <summary>Who holds <c>Training.Conduct</c> on this training by a grant with its scope, in the order they were given.</summary>
    private async Task<List<int>> HoldersOfConductAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var scopeOf = Training.ScopeOf(id);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants.AsNoTracking()
            .Where(grant => grant.Value == TrainingPermissions.Conduct && grant.ResourceScope == scopeOf && grant.Vid != null)
            .OrderBy(grant => grant.Id)
            .Select(grant => grant.Vid!.Value)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The identity a login of <paramref name="vid"/> would put in the cookie now: positions, grants, scopes.</summary>
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

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own as <paramref name="who"/>: the identity goes where the cookie middleware
    /// puts it, the request, and the host's current user reads it from there.
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

    private async Task<Training> StoredAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(training => training.Id == id, cancellationToken);
    }

    /// <summary>
    /// A training of the first trained rating of its ladder, written as the installation: past the request of A6a, which its own
    /// tests prove, straight into the state the test starts from.
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
        var decided = state is not (TrainingState.Requested or TrainingState.Cancelled);

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
            AvailabilityText = "trn-test availability",
            State = state,
            Rejection = state == TrainingState.Rejected ? TrainingRejection.Staff : null,
            RejectionReason = state == TrainingState.Rejected ? "trn-test refused" : null,
            DecidedBy = decided ? AdvisorVid : null,
            DecidedAt = decided ? now : null,
            TrainerVid = trainer,
            AssignedBy = trainer is null ? null : CoordinatorVid,
            AssignedAt = trainer is null ? null : now,
            ScheduledStartUtc = start,
            CompletedAt = state == TrainingState.Completed ? now : null,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

    private async Task<int> MailsAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .CountAsync(notification => notification.Vid == vid && notification.Type == type, cancellationToken);
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

    private async Task ForgetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey);
        await database.DivisionSettings.Where(setting => setting.Key == key).ExecuteDeleteAsync(cancellationToken);
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

    /// <summary>The rung of the ladder right below a rating, as the core's vocabulary orders it.</summary>
    private Rating RungBelow(Rating rating)
    {
        var ladder = Ladder(rating.Kind);
        return ladder[ladder.ToList().IndexOf(rating) - 1];
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
    /// A person of this class: their name, a mailbox only when a test looks for their mail, whether the hub counts them as staff
    /// or as a super administrator, their ratings, and a position as the network spells it.
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
    /// What this class leaves: the trainings of its trainees, with the entries of the calendar their dated sessions project
    /// (A8: deleted in bulk, a training would leave them behind), the grants and positions of its people, their mails, and the
    /// address and the powers of the ones that had them — so no later class counts them.
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
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserStaffPositions.Where(position => Vids.Contains(position.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => Vids.Contains(user.Vid))
            .ExecuteUpdateAsync(user => user.SetProperty(row => row.Email, (string?)null).SetProperty(row => row.IsSuperadmin, false), cancellationToken);
    }
}
