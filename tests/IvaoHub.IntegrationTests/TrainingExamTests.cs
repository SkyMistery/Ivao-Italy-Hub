using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Blocks;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Public;
using IvaoHub.Modules.Training.Reference;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The exams in the calendar (M3, A10c; design M3 §1.5, §2.8, §4.1, §5.1; notes <c>2026-09-26-gli-esaminatori</c>,
/// <c>2026-09-26-le-righe-affidate-a-chi-scrive</c> §3.6, <c>il-training-in-pubblico</c>), through the real host:
/// <list type="bullet">
/// <item>an advisor — <c>Training.ManageExams</c> without <c>Training.Edit</c> — enters an exam of their own, and they alone of the
/// advisors change it and take it off the calendar; a trainer enters none (the «done when» of A10c through the API);</item>
/// <item>the coordinator and the assistant, who hold <c>Edit</c> and do not examine, change and remove it through the endpoint: the
/// exam declares its area, so the write guard asks <c>Training.Edit</c> and not <c>Exams.Edit</c> (the reviewer's point 1 on #146);
/// </item>
/// <item>DELETE is the examiner's and whoever edits the area's, nobody else's (point 2); the handler and the guard answer alike on
/// every exam, the candidate who would be the examiner too included, and the endpoint never lets the candidate be the examiner
/// (point 3);</item>
/// <item>the list says which exams are the reader's and which they may change; the form offers an advisor themselves as examiner and
/// the coordinator every examiner; an exam is refused field by field;</item>
/// <item>a visitor reads the exams still to come on <c>/training</c> and in the block with no VID and no name, a signed in member reads
/// the VIDs, and each exam is a public entry of the calendar that names nobody.</item>
/// </list>
/// <para>⚠️ The staff of the training holds its permissions by grants to a VID, with no position and no address (<c>CONTRIBUTING.md</c>,
/// "Tests"). The shared database may hold other exams: a test looks for its own among them.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingExamTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790067 are A1's to A10b's, 790080–790089
    // A11a's; these nine are A10c's.
    private const int AdvisorVid = 790090;
    private const int OtherAdvisorVid = 790091;
    private const int CoordinatorVid = 790092;
    private const int AssistantVid = 790093;
    private const int TrainerVid = 790094;
    private const int CandidateVid = 790068;
    private const int MemberVid = 790069;
    private const int OtherCandidateVid = 790070;

    /// <summary>A VID the hub knows nothing of: it holds nothing, so it examines nothing.</summary>
    private const int StrangerVid = 790071;

    private static readonly int[] Vids =
        [AdvisorVid, OtherAdvisorVid, CoordinatorVid, AssistantVid, TrainerVid, CandidateVid, MemberVid, OtherCandidateVid, StrangerVid];

    private const string GrantReason = "trn-test";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The positions an ATC exam is on come from the snapshot of the reference data, read from the recorded answers.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunAsync(token);
        }

        // A run that failed half way leaves its exams, their entries of the calendar and its grants.
        await CleanAsync(token);

        await SeedPersonAsync(CandidateVid, "Test", "Candidate", token);
        await SeedPersonAsync(OtherCandidateVid, "Other", "Candidate", token);
        await SeedPersonAsync(MemberVid, "Some", "Member", token);

        // The advisors (TA) examine and hold no Edit; the coordinator (TC) and the assistant (TAC) hold Edit and examine too; the
        // trainer reads. As the division gives them by position (design M3 §3.2), here to the VID.
        foreach (var (vid, first) in new[] { (AdvisorVid, "Test"), (OtherAdvisorVid, "Other") })
        {
            await SeedPersonAsync(vid, first, "Advisor", token, isStaff: true);
            await GrantAsync(vid, token, TrainingPermissions.View, TrainingPermissions.Approve, TrainingPermissions.ManageExams);
        }

        foreach (var (vid, last) in new[] { (CoordinatorVid, "Coordinator"), (AssistantVid, "Assistant") })
        {
            await SeedPersonAsync(vid, "Test", last, token, isStaff: true);
            await GrantAsync(vid, token, TrainingPermissions.View, TrainingPermissions.Edit, TrainingPermissions.ManageExams);
        }

        await SeedPersonAsync(TrainerVid, "Test", "Trainer", token, isStaff: true);
        await GrantAsync(TrainerVid, token, TrainingPermissions.View);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of A10c (<c>08-piano-implementazione-m3.md</c>), the staff's half, through the API: an advisor enters an exam of
    /// their own, which goes into the calendar; they may not enter one for another advisor, nor may a trainer enter any; and of the
    /// advisors only the one it is assigned to changes it and takes it off the calendar.
    /// </summary>
    [Fact]
    public async Task AnAdvisorEntersTheirOwnExamAndOnlyTheyOfTheAdvisorsChangeAndRemoveIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        var position = await PositionAsync(atc, token);
        var starts = Minute(DateTime.UtcNow.AddDays(3));

        using var advisor = await SignedInAsync(AdvisorVid, token);
        var created = await ReadAsync(await PostAsync(advisor, Body(atc, position, starts, CandidateVid, AdvisorVid), token), HttpStatusCode.Created, token);
        var id = created.GetProperty("id").GetInt64();

        var stored = await FindAsync(id, token);
        Assert.Equal((AdvisorVid, AdvisorVid, CandidateVid), (stored!.CreatedBy, stored.ExaminerVid, stored.CandidateVid));
        var entry = await CalendarEntryAsync(id, token);
        Assert.Equal((Exam.CalendarKind, Visibility.Public, Exam.PublicPath), (entry!.Kind, entry.Visibility, entry.Url));
        Assert.Equal(starts, entry.StartsAtUtc, TimeSpan.FromSeconds(1));

        // Not for another advisor, and not by a trainer, who puts no exam in the calendar.
        using (var forAnother = await PostAsync(advisor, Body(atc, position, starts, CandidateVid, OtherAdvisorVid), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forAnother.StatusCode);
        }

        using var trainer = await SignedInAsync(TrainerVid, token);
        using (var byTrainer = await PostAsync(trainer, Body(atc, position, starts, CandidateVid, AdvisorVid), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, byTrainer.StatusCode);
        }

        // The other advisor holds the same permission the same way, and the exam is not theirs.
        using var other = await SignedInAsync(OtherAdvisorVid, token);
        using (var changed = await PutAsync(other, id, Body(atc, position, starts.AddHours(1), CandidateVid, AdvisorVid), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, changed.StatusCode);
        }

        using (var removed = await DeleteAsync(other, id, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        }

        Assert.Equal(starts, (await FindAsync(id, token))!.StartsAtUtc, TimeSpan.FromSeconds(1));

        // Its examiner moves it, and the entry of the calendar moves with it; then takes it off, and the entry goes too.
        var later = starts.AddHours(2);
        await ReadAsync(await PutAsync(advisor, id, Body(atc, position, later, CandidateVid, AdvisorVid), token), HttpStatusCode.OK, token);
        Assert.Equal(later, (await CalendarEntryAsync(id, token))!.StartsAtUtc, TimeSpan.FromSeconds(1));

        using (var taken = await DeleteAsync(advisor, id, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, taken.StatusCode);
        }

        Assert.Null(await FindAsync(id, token));
        Assert.Null(await CalendarEntryAsync(id, token));
    }

    /// <summary>
    /// The reviewer's point 1 on #146, the backbone through the endpoint: the coordinator and the assistant hold <c>Training.Edit</c> and
    /// are not the examiner, and change, hand over and remove an advisor's exam, and enter one for an advisor. The handler falls back on
    /// <c>Training.Edit</c> (the area of the permission) and so does the guard (the area the exam declares): without the declaration the
    /// guard would ask <c>Exams.Edit</c>, which nobody holds, and refuse what the endpoint allowed.
    /// </summary>
    [Fact]
    public async Task TheCoordinatorAndTheAssistantWriteAnAdvisorsExamThroughTheEndpoint()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();
        var position = await PositionAsync(atc, token);
        var starts = Minute(DateTime.UtcNow.AddDays(4));
        var theAdvisors = await AddExamAsync(atc, position, starts, CandidateVid, AdvisorVid, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        await ReadAsync(await PutAsync(coordinator, theAdvisors, Body(atc, position, starts.AddDays(1), CandidateVid, OtherAdvisorVid), token), HttpStatusCode.OK, token);
        var handedOver = await FindAsync(theAdvisors, token);
        Assert.Equal((OtherAdvisorVid, CoordinatorVid), (handedOver!.ExaminerVid, handedOver.UpdatedBy));

        var forAnAdvisor = await ReadAsync(await PostAsync(coordinator, Body(pilot, null, starts, OtherCandidateVid, AdvisorVid), token), HttpStatusCode.Created, token);
        Assert.Equal(AdvisorVid, (await FindAsync(forAnAdvisor.GetProperty("id").GetInt64(), token))!.ExaminerVid);

        using var assistant = await SignedInAsync(AssistantVid, token);
        await ReadAsync(await PutAsync(assistant, theAdvisors, Body(atc, position, starts, OtherCandidateVid, OtherAdvisorVid), token), HttpStatusCode.OK, token);
        using (var removed = await DeleteAsync(assistant, theAdvisors, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Null(await FindAsync(theAdvisors, token));
        Assert.Null(await CalendarEntryAsync(theAdvisors, token));
    }

    /// <summary>
    /// The reviewer's point 2 on #146: what DELETE does. An exam is taken off the calendar by its examiner and by whoever edits the area
    /// (Carmine, answer 4 on #131), and by nobody else — not another advisor, not a trainer, not a member.
    /// </summary>
    [Fact]
    public async Task AnExamIsTakenOffTheCalendarByItsExaminerAndByWhoeverEditsTheAreaOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        var starts = Minute(DateTime.UtcNow.AddDays(5));
        var first = await AddExamAsync(pilot, null, starts, CandidateVid, AdvisorVid, token);
        var second = await AddExamAsync(pilot, null, starts.AddHours(1), CandidateVid, AdvisorVid, token);

        foreach (var vid in new[] { OtherAdvisorVid, TrainerVid, MemberVid })
        {
            using var nobody = await SignedInAsync(vid, token);
            using var refused = await DeleteAsync(nobody, first, token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.NotNull(await FindAsync(first, token));

        using var advisor = await SignedInAsync(AdvisorVid, token);
        using (var taken = await DeleteAsync(advisor, first, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, taken.StatusCode);
        }

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using (var taken = await DeleteAsync(coordinator, second, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, taken.StatusCode);
        }

        Assert.Null(await FindAsync(first, token));
        Assert.Null(await FindAsync(second, token));
    }

    /// <summary>An advisor neither hands their exam over to another examiner nor takes somebody else's: that is for whoever edits the area.</summary>
    [Fact]
    public async Task AnAdvisorNeitherHandsTheirExamOverNorTakesAnothers()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        var starts = Minute(DateTime.UtcNow.AddDays(6));
        var mine = await AddExamAsync(pilot, null, starts, CandidateVid, AdvisorVid, token);
        var theirs = await AddExamAsync(pilot, null, starts, OtherCandidateVid, OtherAdvisorVid, token);

        using var advisor = await SignedInAsync(AdvisorVid, token);
        using (var handedOver = await PutAsync(advisor, mine, Body(pilot, null, starts, CandidateVid, OtherAdvisorVid), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, handedOver.StatusCode);
        }

        using (var taken = await PutAsync(advisor, theirs, Body(pilot, null, starts, OtherCandidateVid, AdvisorVid), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, taken.StatusCode);
        }

        Assert.Equal(AdvisorVid, (await FindAsync(mine, token))!.ExaminerVid);
        Assert.Equal(OtherAdvisorVid, (await FindAsync(theirs, token))!.ExaminerVid);
    }

    /// <summary>
    /// The reviewer's point 3 on #146, and the rule of A3b once more on the exams themselves: the single handler — asked on the row, as
    /// the CRUD engine asks it — and the write guard — with no endpoint in the way — give the same answer on every exam: the advisor's own
    /// and another's, created, changed and removed; the coordinator on an advisor's; the trainer; and the advisor who is both the
    /// candidate and the examiner, on whom the two agree because the exam is about nobody and <c>ManageExams</c> is denied to nobody
    /// (design M3 §3.1). Such an exam exists only written by hand: through the endpoint the candidate is never the examiner.
    /// </summary>
    [Fact]
    public async Task TheHandlerAndTheGuardAnswerAlikeOnEveryExam()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        var starts = Minute(DateTime.UtcNow.AddDays(7));

        var advisor = Holding(AdvisorVid, TrainingPermissions.View, TrainingPermissions.ManageExams);
        var coordinator = Holding(CoordinatorVid, TrainingPermissions.View, TrainingPermissions.Edit, TrainingPermissions.ManageExams);
        var trainer = Holding(TrainerVid, TrainingPermissions.View);

        // Created: their own, and for another examiner.
        await AlikeAsync(advisor, New(pilot, starts, CandidateVid, AdvisorVid), expected: true, database => database.Exams.Add(New(pilot, starts, CandidateVid, AdvisorVid)), token);
        await AlikeAsync(advisor, New(pilot, starts, CandidateVid, OtherAdvisorVid), expected: false, database => database.Exams.Add(New(pilot, starts, CandidateVid, OtherAdvisorVid)), token);
        await AlikeAsync(trainer, New(pilot, starts, CandidateVid, AdvisorVid), expected: false, database => database.Exams.Add(New(pilot, starts, CandidateVid, AdvisorVid)), token);

        // Changed and removed: their own, another's, and an advisor's by the coordinator.
        var mine = await AddExamAsync(pilot, null, starts, CandidateVid, AdvisorVid, token);
        var theirs = await AddExamAsync(pilot, null, starts, CandidateVid, OtherAdvisorVid, token);
        await AlikeAsync(advisor, await FindAsync(mine, token), expected: true, Change(mine, starts.AddHours(1)), token);
        await AlikeAsync(advisor, await FindAsync(theirs, token), expected: false, Change(theirs, starts.AddHours(1)), token);
        await AlikeAsync(advisor, await FindAsync(theirs, token), expected: false, Remove(theirs), token);
        await AlikeAsync(trainer, await FindAsync(mine, token), expected: false, Remove(mine), token);
        await AlikeAsync(coordinator, await FindAsync(theirs, token), expected: true, Change(theirs, starts.AddHours(2)), token);
        await AlikeAsync(advisor, await FindAsync(mine, token), expected: true, Remove(mine), token);

        // The advisor is the candidate and the examiner both: a row no endpoint lets exist, written here as the installation. The exam
        // is about nobody, so both let its examiner write it, as any exam of theirs.
        var theirOwn = await AddExamAsync(pilot, null, starts, AdvisorVid, AdvisorVid, token);
        await AlikeAsync(advisor, await FindAsync(theirOwn, token), expected: true, Change(theirOwn, starts.AddHours(1)), token);
        await AlikeAsync(advisor, await FindAsync(theirOwn, token), expected: true, Remove(theirOwn), token);
        await AlikeAsync(advisor, New(pilot, starts, AdvisorVid, AdvisorVid), expected: true, database => database.Exams.Add(New(pilot, starts, AdvisorVid, AdvisorVid)), token);

        // Through the endpoint the candidate is never the examiner: refused on its field, before any permission is asked.
        using var advisorClient = await SignedInAsync(AdvisorVid, token);
        var refused = await ReadAsync(await PostAsync(advisorClient, Body(pilot, null, starts, AdvisorVid, AdvisorVid), token), HttpStatusCode.BadRequest, token);
        Assert.Contains(TrainingExams.ExaminerIsCandidate, Refusal(refused, "examinerVid"));
    }

    /// <summary>
    /// How an advisor sees which exams are theirs (note 2026-09-26-le-righe-affidate-a-chi-scrive §3.6): every reader of the training
    /// reads the list, and each exam says whether it is the reader's and whether they may change it — the one handler's answer on the row.
    /// <c>filter[examinerVid]</c> keeps the reader's own.
    /// </summary>
    [Fact]
    public async Task TheListSaysWhichExamsAreTheReadersAndWhichTheyMayChange()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        var starts = Minute(DateTime.UtcNow.AddDays(8));
        var mine = await AddExamAsync(pilot, null, starts, CandidateVid, AdvisorVid, token);
        var theirs = await AddExamAsync(pilot, null, starts, OtherCandidateVid, OtherAdvisorVid, token);

        using var advisor = await SignedInAsync(AdvisorVid, token);
        var list = await ListAsync(advisor, "?pageSize=100&dir=desc", token);
        Assert.Equal((true, true), MineAndMay(Row(list, mine)));
        Assert.Equal((false, false), MineAndMay(Row(list, theirs)));
        Assert.Equal(pilot.ShortName, Row(list, mine).GetProperty("ratingShortName").GetString());

        var own = await ListAsync(advisor, $"?pageSize=100&filter[examinerVid]={AdvisorVid}", token);
        Assert.Contains(own.EnumerateArray(), row => Id(row) == mine);
        Assert.DoesNotContain(own.EnumerateArray(), row => Id(row) == theirs);
        Assert.All(own.EnumerateArray(), row => Assert.Equal(AdvisorVid, row.GetProperty("examinerVid").GetInt32()));

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var theCoordinators = await ListAsync(coordinator, "?pageSize=100", token);
        Assert.Equal((false, true), MineAndMay(Row(theCoordinators, mine)));
        Assert.Equal((false, true), MineAndMay(Row(theCoordinators, theirs)));

        using var trainer = await SignedInAsync(TrainerVid, token);
        var theTrainers = await ListAsync(trainer, "?pageSize=100", token);
        Assert.Equal((false, false), MineAndMay(Row(theTrainers, mine)));

        using var member = await SignedInAsync(MemberVid, token);
        using (var none = await member.GetAsync(new Uri(ExamEndpoints.Pattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, none.StatusCode);
        }
    }

    /// <summary>
    /// How the coordinator, the assistant and the direction choose the examiner of an exam they enter for somebody else (note §3.6): the
    /// form offers the examiners the one handler lets the reader give an exam to — an advisor themselves, the coordinator every one the
    /// hub knows, never a trainer —, every rating of the two ladders (#178), and the positions; a trainer is offered nothing.
    /// </summary>
    [Fact]
    public async Task TheFormOffersAnAdvisorThemselvesAndTheCoordinatorEveryExaminer()
    {
        var token = TestContext.Current.CancellationToken;

        using var advisor = await SignedInAsync(AdvisorVid, token);
        var theAdvisors = await ReadAsync(await advisor.GetAsync(new Uri(ExamEndpoints.ChoicesPattern, UriKind.Relative), token), HttpStatusCode.OK, token);
        var only = Assert.Single(theAdvisors.GetProperty("examiners").EnumerateArray());
        Assert.Equal((AdvisorVid, "Test Advisor"), (only.GetProperty("vid").GetInt32(), only.GetProperty("name").GetString()));
        Assert.NotEmpty(theAdvisors.GetProperty("positions").EnumerateArray());
        Assert.Equal(
            [.. Enum.GetValues<RatingKind>().SelectMany(Vocabulary().Ladder).Select(rating => $"{rating.Kind} {rating.Number} {rating.ShortName}")],
            theAdvisors.GetProperty("ratings").EnumerateArray()
                .Select(rating => $"{rating.GetProperty("kind").GetString()} {rating.GetProperty("number").GetInt32()} {rating.GetProperty("shortName").GetString()}"));

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var theCoordinators = await ReadAsync(await coordinator.GetAsync(new Uri(ExamEndpoints.ChoicesPattern, UriKind.Relative), token), HttpStatusCode.OK, token);
        var offered = theCoordinators.GetProperty("examiners").EnumerateArray().Select(member => member.GetProperty("vid").GetInt32()).ToList();
        Assert.Contains(AdvisorVid, offered);
        Assert.Contains(OtherAdvisorVid, offered);
        Assert.Contains(CoordinatorVid, offered);
        Assert.Contains(AssistantVid, offered);
        Assert.DoesNotContain(TrainerVid, offered);
        Assert.DoesNotContain(MemberVid, offered);

        using var trainer = await SignedInAsync(TrainerVid, token);
        using (var none = await trainer.GetAsync(new Uri(ExamEndpoints.ChoicesPattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, none.StatusCode);
        }
    }

    /// <summary>
    /// An exam is refused field by field, before any permission is asked: a rating that is not one of its ladder, a position missing,
    /// unknown or on a rating examined without one, no date, no candidate, and an examiner who is the candidate, who puts no exam in the
    /// calendar, or whom the hub does not know.
    /// </summary>
    [Fact]
    public async Task AnExamIsRefusedFieldByField()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();
        var position = await PositionAsync(atc, token);
        var starts = Minute(DateTime.UtcNow.AddDays(9));
        var unknown = Unknown(atc.Kind);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        async Task RefusedAsync(object body, string field, string key)
        {
            var problem = await ReadAsync(await PostAsync(coordinator, body, token), HttpStatusCode.BadRequest, token);
            Assert.Contains(key, Refusal(problem, field));
        }

        await RefusedAsync(Body(unknown, null, starts, CandidateVid, AdvisorVid), "rating", TrainingExams.RatingUnknown);
        await RefusedAsync(Body(atc, null, starts, CandidateVid, AdvisorVid), "position", "errors.required");
        await RefusedAsync(Body(atc, "TRNTEST_NOWHERE", starts, CandidateVid, AdvisorVid), "position", TrainingExams.PositionUnknown);
        await RefusedAsync(Body(pilot, position, starts, CandidateVid, AdvisorVid), "position", TrainingExams.PositionNotAsked);
        await RefusedAsync(Body(pilot, null, null, CandidateVid, AdvisorVid), "startsAtUtc", "errors.required");
        await RefusedAsync(Body(pilot, null, starts, 0, AdvisorVid), "candidateVid", "errors.required");
        await RefusedAsync(Body(pilot, null, starts, CandidateVid, CandidateVid), "examinerVid", TrainingExams.ExaminerIsCandidate);
        await RefusedAsync(Body(pilot, null, starts, CandidateVid, TrainerVid), "examinerVid", TrainingExams.ExaminerNotExaminer);
        await RefusedAsync(Body(pilot, null, starts, CandidateVid, StrangerVid), "examinerVid", TrainingExams.ExaminerNotExaminer);

        // The position as the directory spells it, whatever the case it was written in.
        var created = await ReadAsync(await PostAsync(coordinator, Body(atc, $" {position.ToLowerInvariant()} ", starts, CandidateVid, AdvisorVid), token), HttpStatusCode.Created, token);
        Assert.Equal(position, created.GetProperty("position").GetString());
    }

    /// <summary>
    /// An exam takes any rating of its ladder, the ones nobody trains for too (the maintainer's answer on #178): the eighth rating of each
    /// ladder — the network examines it, and the division trains nobody for it — is scheduled without a position, and a position on it is
    /// refused, as on a pilot's exam; so is the first rating of a ladder, since the vocabulary does not say which ratings have an exam.
    /// </summary>
    [Fact]
    public async Task AnExamTakesAnyRatingOfItsLadderTheEighthToo()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Minute(DateTime.UtcNow.AddDays(11));
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        foreach (var kind in Enum.GetValues<RatingKind>())
        {
            var eighth = Vocabulary().Find(kind, 8);
            Assert.NotNull(eighth);
            Assert.False(eighth.HasPracticalTraining, $"Nobody trains for the eighth {kind} rating.");
            Assert.Null(eighth.PositionType);

            var created = await ReadAsync(await PostAsync(coordinator, Body(eighth, null, starts, CandidateVid, AdvisorVid), token), HttpStatusCode.Created, token);
            Assert.Equal(
                (kind.ToString(), 8, JsonValueKind.Null),
                (created.GetProperty("kind").GetString(), created.GetProperty("rating").GetInt32(), created.GetProperty("position").ValueKind));
            var entry = await CalendarEntryAsync(created.GetProperty("id").GetInt64(), token);
            Assert.Equal(eighth.ShortName, entry!.Title.Get("en"));

            var problem = await ReadAsync(await PostAsync(coordinator, Body(eighth, "TRNTEST_POS", starts, CandidateVid, AdvisorVid), token), HttpStatusCode.BadRequest, token);
            Assert.Contains(TrainingExams.PositionNotAsked, Refusal(problem, "position"));
        }

        var first = Vocabulary().Ladder(RatingKind.Atc)[0];
        Assert.False(first.HasPracticalTraining, "Nobody trains for the first rating of a ladder.");
        await ReadAsync(await PostAsync(coordinator, Body(first, null, starts, OtherCandidateVid, AdvisorVid), token), HttpStatusCode.Created, token);
    }

    /// <summary>
    /// The «done when» of A10c, the site's half: a visitor reads the exams still to come — on the list of <c>/training</c> and in the
    /// block — by their rating, position and time, with nobody's VID or name; a past one is not among them, and each is still a public
    /// entry of the calendar, titled without VIDs, that points at <c>/training</c>; a signed in member reads the two VIDs.
    /// </summary>
    [Fact]
    public async Task AVisitorSeesTheExamsStillToComeWithoutVidsAndASignedInMemberSeesThem()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        var position = await PositionAsync(atc, token);
        var starts = Minute(DateTime.UtcNow.AddDays(2));
        var coming = await AddExamAsync(atc, position, starts, CandidateVid, AdvisorVid, token);
        var past = await AddExamAsync(atc, position, Minute(DateTime.UtcNow.AddDays(-3)), OtherCandidateVid, AdvisorVid, token);
        string[] secrets =
        [
            CandidateVid.ToString(CultureInfo.InvariantCulture),
            AdvisorVid.ToString(CultureInfo.InvariantCulture),
            OtherCandidateVid.ToString(CultureInfo.InvariantCulture),
            "Test Candidate",
            "Test Advisor",
        ];

        using var anonymous = _factory.CreateApiClient();
        var (listText, list) = await ReadWithTextAsync(await anonymous.GetAsync(new Uri(PublicSessionEndpoints.ExamsPattern, UriKind.Relative), token), token);
        var item = Assert.Single(list.EnumerateArray(), exam => Id(exam) == coming);
        Assert.Equal(("Atc", atc.ShortName, position), (item.GetProperty("kind").GetString(), item.GetProperty("ratingShortName").GetString(), item.GetProperty("position").GetString()));
        Assert.Equal(starts, item.GetProperty("startsAtUtc").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
        Assert.Equal(JsonValueKind.Null, item.GetProperty("candidateVid").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("examinerVid").ValueKind);
        Assert.DoesNotContain(list.EnumerateArray(), exam => Id(exam) == past);
        AssertSaysNone(listText, secrets);

        var (blockText, block) = await ReadWithTextAsync(await anonymous.GetAsync(new Uri($"/api/blocks/data/{UpcomingSessionsProvider.BlockType}", UriKind.Relative), token), token);
        Assert.False(block.GetProperty("signedIn").GetBoolean());
        Assert.Single(block.GetProperty("exams").EnumerateArray(), exam => Id(exam) == coming);
        AssertSaysNone(blockText, secrets);

        // In the calendar, the one to come and the past one: public, by rating and position, pointing at /training.
        foreach (var exam in new[] { coming, past })
        {
            var entry = await CalendarEntryAsync(exam, token);
            Assert.Equal((Exam.CalendarKind, Visibility.Public, Exam.PublicPath), (entry!.Kind, entry.Visibility, entry.Url));
            Assert.Equal($"{atc.ShortName} · {position}", entry.Title.Get("en"));
            AssertSaysNone(string.Join(" ", entry.Title.Values), secrets);
        }

        // Signed in, any member: the candidate and the examiner, by VID and nothing else.
        using var member = await SignedInAsync(MemberVid, token);
        var (seenText, seen) = await ReadWithTextAsync(await member.GetAsync(new Uri(PublicSessionEndpoints.ExamsPattern, UriKind.Relative), token), token);
        var theirs = Assert.Single(seen.EnumerateArray(), exam => Id(exam) == coming);
        Assert.Equal((CandidateVid, AdvisorVid), (theirs.GetProperty("candidateVid").GetInt32(), theirs.GetProperty("examinerVid").GetInt32()));
        Assert.DoesNotContain("Test Candidate", seenText, StringComparison.Ordinal);
    }

    // ---- the handler and the guard -------------------------------------------------------------------------------------------

    /// <summary>
    /// What the single handler answers <paramref name="who"/> for <c>Training.ManageExams</c> on <paramref name="row"/>, as the engine
    /// asks it, and whether the guard lets <paramref name="write"/> through as them: both <paramref name="expected"/>.
    /// </summary>
    private async Task AlikeAsync(ClaimsPrincipal who, Exam? row, bool expected, Action<TrainingDbContext> write, CancellationToken cancellationToken)
    {
        Assert.NotNull(row);
        var allowed = await InScopeAsync(who, async services =>
            (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(who, row, TrainingPermissions.ManageExams)).Succeeded);
        Assert.Equal(expected, allowed);

        try
        {
            await InScopeAsync(who, async services =>
            {
                var database = services.GetRequiredService<TrainingDbContext>();
                write(database);
                return await database.SaveChangesAsync(cancellationToken);
            });
            Assert.True(expected, "The guard let through a write the handler refuses.");
        }
        catch (ForbiddenDomainException refused)
        {
            Assert.False(expected, "The guard refused a write the handler allows.");
            Assert.Equal(TrainingPermissions.Edit, refused.Permission);
        }
    }

    private static Action<TrainingDbContext> Change(long id, DateTime starts) => database =>
    {
        var exam = database.Exams.Single(row => row.Id == id);
        exam.StartsAtUtc = starts;
    };

    private static Action<TrainingDbContext> Remove(long id) => database => database.Exams.Remove(database.Exams.Single(row => row.Id == id));

    /// <summary>A signed in member holding exactly these permissions on the training department, as a login writes them into the cookie.</summary>
    private static ClaimsPrincipal Holding(int vid, params string[] permissions) =>
        new(HubClaims.BuildIdentity(
            vid,
            firstName: "Test",
            lastName: "Examiner",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: [],
            permissions: [.. permissions.Select(permission => new EffectivePermission(permission, Department.TD, $"{EffectivePermissionsCalculator.GrantSourcePrefix}test"))]));

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own, as <paramref name="who"/>: the identity goes where the cookie middleware puts
    /// it, the request, and the host's current user reads it from there, for the handler as for the guard.
    /// </summary>
    private async Task<T> InScopeAsync<T>(ClaimsPrincipal who, Func<IServiceProvider, Task<T>> work)
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

    // ---- reading ---------------------------------------------------------------------------------------------------------------

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static JsonElement Row(JsonElement items, long id) => Assert.Single(items.EnumerateArray(), row => Id(row) == id);

    private static (bool Mine, bool MayEdit) MineAndMay(JsonElement row) => (row.GetProperty("mine").GetBoolean(), row.GetProperty("mayEdit").GetBoolean());

    /// <summary>The keys a refusal names on one field of the <c>ProblemDetails</c>.</summary>
    private static List<string?> Refusal(JsonElement problem, string field) =>
        [.. problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(key => key.GetString())];

    private static void AssertSaysNone(string text, IEnumerable<string> secrets)
    {
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    private static async Task<JsonElement> ListAsync(HttpClient client, string query, CancellationToken cancellationToken) =>
        (await ReadAsync(await client.GetAsync(new Uri($"{ExamEndpoints.Pattern}{query}", UriKind.Relative), cancellationToken), HttpStatusCode.OK, cancellationToken))
            .GetProperty("items");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {text}");
            return JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private static async Task<(string Text, JsonElement Json)> ReadWithTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {text}");
            return (text, JsonDocument.Parse(text).RootElement.Clone());
        }
    }

    private async Task<Exam?> FindAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Exams.AsNoTracking().FirstOrDefaultAsync(exam => exam.Id == id, cancellationToken);
    }

    private async Task<CalendarEntry?> CalendarEntryAsync(long exam, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var source = Exam.SourceIdOf(exam);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.SourceModule == TrainingModule.ModuleKey && entry.SourceId == source, cancellationToken);
    }

    // ---- writing ---------------------------------------------------------------------------------------------------------------

    /// <summary>An exam as the form sends it; the version of a new row, or none for a change — the row as it is now.</summary>
    private static object Body(Rating rating, string? position, DateTime? starts, int candidate, int examiner) => new
    {
        kind = rating.Kind.ToString(),
        rating = rating.Number,
        position,
        startsAtUtc = starts,
        candidateVid = candidate,
        examinerVid = examiner,
        rowVersion = default(DateTime),
    };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri(ExamEndpoints.Pattern, UriKind.Relative), body, cancellationToken);

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, long id, object body, CancellationToken cancellationToken) =>
        client.PutAsJsonAsync(new Uri($"{ExamEndpoints.Pattern}/{id}", UriKind.Relative), body, cancellationToken);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        client.DeleteAsync(new Uri($"{ExamEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken);

    /// <summary>An exam of the base department as it would be stored, not saved: what the engine asks about on a create.</summary>
    private static Exam New(Rating rating, DateTime starts, int candidate, int examiner) => new()
    {
        Kind = rating.Kind,
        Rating = rating.Number,
        StartsAtUtc = starts,
        CandidateVid = candidate,
        ExaminerVid = examiner,
        OwnerDepartment = Department.TD,
        OwnerDepartmentMask = DepartmentMask.Of([Department.TD]),
    };

    /// <summary>An exam written as the installation, which the guard leaves alone; it puts its entry into the calendar as every write does.</summary>
    private async Task<long> AddExamAsync(Rating rating, string? position, DateTime starts, int candidate, int examiner, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var exam = New(rating, starts, candidate, examiner);
        exam.Position = position;

        database.Exams.Add(exam);
        await database.SaveChangesAsync(cancellationToken);
        return exam.Id;
    }

    /// <summary>The first rating of each ladder with a practical training: what the exams of this class are for.</summary>
    private (Rating Atc, Rating Pilot) Trained()
    {
        using var scope = _factory.Services.CreateScope();
        var ratings = scope.ServiceProvider.GetRequiredService<TrainingReference>().Ratings;
        return (ratings.First(rating => rating.Kind == RatingKind.Atc), ratings.First(rating => rating.Kind == RatingKind.Pilot));
    }

    /// <summary>The ratings the host knows: the core's vocabulary.</summary>
    private RatingVocabulary Vocabulary()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RatingVocabulary>();
    }

    /// <summary>A rating the vocabulary does not know on the ladder: one past its highest.</summary>
    private Rating Unknown(RatingKind kind) =>
        new(kind, Vocabulary().Ladder(kind).Max(rating => rating.Number) + 1, "TRNTEST", HasPracticalTraining: false, PositionType: null);

    /// <summary>A position of the division the rating is trained on, as the core's directory spells it.</summary>
    private async Task<string> PositionAsync(Rating rating, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var positions = await scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>().ForRatingAsync(rating, cancellationToken);
        Assert.NotEmpty(positions);
        return positions[0].Callsign;
    }

    /// <summary>A moment to the minute: what a form sends, and what a comparison after a trip through the database keeps.</summary>
    private static DateTime Minute(DateTime moment) =>
        new(moment.Year, moment.Month, moment.Day, moment.Hour, moment.Minute, 0, DateTimeKind.Utc);

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A person of this class: their name, and whether the hub counts them as staff. Nobody has an address.</summary>
    private async Task SeedPersonAsync(int vid, string firstName, string lastName, CancellationToken cancellationToken, bool isStaff = false)
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

    /// <summary>What this class leaves: the exams of its people with their entries of the calendar, and their grants.</summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var training = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var exams = training.Exams.Where(exam => Vids.Contains(exam.CandidateVid) || Vids.Contains(exam.ExaminerVid));
        var sources = (await exams.Select(exam => exam.Id).ToListAsync(cancellationToken)).Select(Exam.SourceIdOf).ToList();
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && sources.Contains(entry.SourceId))
            .ExecuteDeleteAsync(cancellationToken);

        await exams.ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
    }
}
