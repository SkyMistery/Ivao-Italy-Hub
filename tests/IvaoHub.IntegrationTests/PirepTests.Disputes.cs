using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Threads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Disputes, clarifications and issues on the legs through the real host (M2, T14b; note
/// 2026-09-23-contestazioni-chiarimenti-segnalazioni): the dispute that opens its thread once, with the validator in it, and
/// frees the next legs; who decides it — <c>Tours.ReopenDecisions</c>, never whoever decided the report —, the answer that
/// reaches the pilot through the thread, upheld back to the queue, turned down holding again; the clarification that cites the
/// tours' objects and brings the validator in; the issue on a leg that reaches the mailbox and that the staff close; the block.
/// </summary>
public sealed partial class PirepTests
{
    // T14b takes 88: an assistant coordinator of the FOD, who judges the disputes of the coordinator's decisions.
    private const int AssistantVid = 780088;

    /// <summary>The mailbox of the FOD as these tests spell it: given to the host here, never written into the division's file.</summary>
    private const string FodMailbox = "fo-test-fod@example.invalid";

    /// <summary>
    /// The "done when" of T14 without the browser: the pilot disputes a rejection — once, and only with a text —, the thread opens
    /// with the validator who decided in it and the next leg is free; the validator reads it and answers; whoever decided cannot
    /// judge it, the coordinator turns it down with an answer the pilot reads in the thread, and the profile counts it.
    /// </summary>
    [Fact]
    public async Task APilotDisputesARejectionAndTheCoordinatorTurnsItDownInTheThread()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (dangerous, _, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        await GrantValidateAsync(ValidatorVid, tourId, token);
        using var validator = await SignedInAsync(ValidatorVid, token);

        var id = await RejectedAsync(pilot, validator, tourId, legs[0], dangerous, token);

        // Decided two days ago: past its grace, the rejection holds the next leg (§2.5), and it is still in its window.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await Everything<Pirep>(database).Where(row => row.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.DecidedAt, DateTime.UtcNow.AddDays(-2)), token);
        }

        var mine = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.DoesNotContain(legs[1], mine.GetProperty("flyable").EnumerateArray().Select(leg => leg.GetInt64()));
        var report = mine.GetProperty("reports").EnumerateArray().Single(row => Id(row) == id);
        Assert.Equal(JsonValueKind.String, report.GetProperty("disputableUntil").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("threadId").ValueKind);

        // A dispute says what to look at again.
        await RefusedAsync(pilot, $"{PirepEndpoints.Pattern}/{id}/dispute", new DisputeOpenDto(" ", RowVersion(report)), "text", "errors.required", token);
        var disputed = await OkAsync(
            await pilot.PostAsJsonAsync($"{PirepEndpoints.Pattern}/{id}/dispute", new DisputeOpenDto("fo-test-pirep the SID was the one on the chart", RowVersion(report)), token),
            token);
        Assert.Equal("Open", disputed.GetProperty("disputeStatus").GetString());
        Assert.True(disputed.GetProperty("isDisputed").GetBoolean());
        var threadId = disputed.GetProperty("threadId").GetInt64();

        // Once only.
        await RefusedAsync(pilot, $"{PirepEndpoints.Pattern}/{id}/dispute", new DisputeOpenDto("again", RowVersion(disputed)), "status", "flightops:errors.disputeAlready", token);

        // The thread: the pilot's, with the validator who decided in it, citing the report — opened in the same save.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var opened = await CrudThreads(hub).SingleAsync(message => message.SourceModule == FlightOpsModule.ModuleKey && message.SourceId == Pirep.ReferenceOf(id), token);
            Assert.Equal(threadId, opened.Id);
            Assert.Equal(ContactKinds.Dispute, opened.Kind);
            Assert.Equal(Department.FOD, opened.OwnerDepartment);
            Assert.Equal(PilotVid, opened.CreatedBy);
            Assert.Equal([ValidatorVid], opened.AddedParticipants);
            Assert.Equal("fo-test-pirep the SID was the one on the chart", opened.Body);

            var reference = await hub.ContactReferences.AsNoTracking().SingleAsync(row => row.MessageId == threadId, token);
            Assert.Equal(Pirep.ReferenceOf(id), reference.SourceId);
            Assert.Contains("fo-test-pirep-", reference.Label.Get("en"), StringComparison.Ordinal);
            Assert.Contains($"{Rome} → {Milan}", reference.Label.Get("it"), StringComparison.Ordinal);
        }

        await MailAsync(ValidatorVid, NotificationTypes.ContactThreadOpened, token);

        // Disputed, the rejection no longer holds the next leg (§3.8).
        mine = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.Contains(legs[1], mine.GetProperty("flyable").EnumerateArray().Select(leg => leg.GetInt64()));
        Assert.Equal(threadId, mine.GetProperty("reports").EnumerateArray().Single(row => Id(row) == id).GetProperty("threadId").GetInt64());

        // The validator takes part: reads the thread and answers, and the pilot hears it.
        var read = await OkAsync(await validator.GetAsync($"{ContactsEndpoints.Pattern}/{threadId}/thread", token), token);
        Assert.Equal($"/staff/tours/review/{id}", Assert.Single(read.GetProperty("references").EnumerateArray()).GetProperty("url").GetString());
        await OkAsync(await validator.PostAsJsonAsync($"{ContactsEndpoints.Pattern}/{threadId}/replies", new ContactReplyWriteDto("fo-test-pirep we are looking"), token), token);
        await MailAsync(PilotVid, NotificationTypes.ContactThreadReplied, token);

        // In the queue of the disputes, decided and so off the default view.
        var queue = await OkAsync(
            await coordinator.GetAsync($"{ReviewEndpoints.QueuePattern}?filter[tourId]={tourId}&filter[open]=false&filter[disputed]=true", token),
            token);
        Assert.True(Assert.Single(queue.GetProperty("items").EnumerateArray()).GetProperty("isDisputed").GetBoolean());

        // Whoever decided takes part and does not judge; the pilot never; the coordinator does. Nor does whoever decided reopen
        // it aside: under an open dispute, reopening is upholding.
        var page = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.False(page.GetProperty("actions").GetProperty("canDecideDispute").GetBoolean());
        Assert.False(page.GetProperty("actions").GetProperty("canReopen").GetBoolean());
        await RefusedStepAsync(validator, id, "reopen", new ReviewReopenDto("mine", RowVersion(page)), "status", "flightops:errors.reviewDisputed", token);
        Assert.Equal(threadId, page.GetProperty("dispute").GetProperty("threadId").GetInt64());
        using (var forbidden = await validator.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{id}/dispute", new DisputeDecisionDto(true, "mine", RowVersion(page)), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.True(page.GetProperty("actions").GetProperty("canDecideDispute").GetBoolean());
        Assert.Equal("fo-test-pirep the SID was the one on the chart", page.GetProperty("dispute").GetProperty("text").GetString());
        Assert.Equal(1, page.GetProperty("profile").GetProperty("disputesOpen").GetInt32());

        await RefusedStepAsync(coordinator, id, "dispute", new DisputeDecisionDto(false, " ", RowVersion(page)), "answer", "errors.required", token);
        page = await StepAsync(coordinator, id, "dispute", new DisputeDecisionDto(false, "fo-test-pirep the chart was out of date", RowVersion(page)), token);
        Assert.Equal("Rejected", page.GetProperty("status").GetString());
        Assert.Equal("Dismissed", page.GetProperty("dispute").GetProperty("status").GetString());
        Assert.False(page.GetProperty("isDisputed").GetBoolean());
        Assert.Equal(CoordinatorVid, page.GetProperty("dispute").GetProperty("decidedBy").GetProperty("vid").GetInt32());
        Assert.Equal(1, page.GetProperty("profile").GetProperty("disputesDismissed").GetInt32());
        Assert.Contains(page.GetProperty("history").EnumerateArray(), step => step.GetProperty("note").GetString() == "flightops:events.disputeDismissed");

        // The answer is the department's in the thread: the pilot reads it without a name, and their mail carries it.
        var thread = await OkAsync(await pilot.GetAsync($"{ContactsEndpoints.Pattern}/{threadId}/thread", token), token);
        var answer = thread.GetProperty("replies").EnumerateArray().Last();
        Assert.Equal("Department", answer.GetProperty("side").GetString());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("authorVid").ValueKind);
        Assert.Equal("Answered", thread.GetProperty("status").GetString());
        var mail = await MailAsync(PilotVid, NotificationTypes.ContactThreadReplied, token);
        Assert.Contains("fo-test-pirep the chart was out of date", mail.DataJson, StringComparison.Ordinal);

        // Decided, it cannot be decided again, nor disputed again.
        await RefusedStepAsync(coordinator, id, "dispute", new DisputeDecisionDto(true, "again", RowVersion(page)), "status", "flightops:errors.disputeNotOpen", token);
    }

    /// <summary>
    /// The window and the state a dispute needs; the coordinator who decided a report cannot judge its dispute, the assistant
    /// upholds it and the report is back in the queue for anybody, the old decision counting for nothing meanwhile.
    /// </summary>
    [Fact]
    public async Task ADisputeNeedsItsWindowAndUpheldSendsTheReportBackToTheQueue()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var assistant = await SignedInAsync(AssistantVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (dangerous, _, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);

        // Rejected by the coordinator.
        var id = await RejectedAsync(pilot, coordinator, tourId, legs[0], dangerous, token);

        // Accepted — the next leg, flown within the grace of that rejection —: nothing to dispute.
        var accepted = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[1], _flights.Add(PilotVid, "XAA300", Milan, London, DateTime.UtcNow.AddDays(-1).AddHours(3))), token));
        var page = await TakeAsync(coordinator, accepted, token);
        page = await StepAsync(coordinator, accepted, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), token);
        await RefusedAsync(pilot, $"{PirepEndpoints.Pattern}/{accepted}/dispute", new DisputeOpenDto("why", RowVersion(page)), "status", "flightops:errors.disputeNotRejected", token);

        // Too late.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await Everything<Pirep>(database).Where(row => row.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.DecidedAt, DateTime.UtcNow.AddDays(-8)), token);
        }

        var read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{id}", token), token);
        await RefusedAsync(pilot, $"{PirepEndpoints.Pattern}/{id}/dispute", new DisputeOpenDto("late", RowVersion(read)), "status", "flightops:errors.disputeWindowClosed", token);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await Everything<Pirep>(database).Where(row => row.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.DecidedAt, DateTime.UtcNow.AddDays(-6)), token);
        }

        read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{id}", token), token);
        await OkAsync(await pilot.PostAsJsonAsync($"{PirepEndpoints.Pattern}/{id}/dispute", new DisputeOpenDto("fo-test-pirep in time", RowVersion(read)), token), token);

        // The coordinator decided it: they take part, they do not judge.
        page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.False(page.GetProperty("actions").GetProperty("canDecideDispute").GetBoolean());
        using (var forbidden = await coordinator.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{id}/dispute", new DisputeDecisionDto(true, "mine", RowVersion(page)), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        // The assistant upholds it: back in the queue, held by nobody, for a new decision.
        page = await OkAsync(await assistant.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.True(page.GetProperty("actions").GetProperty("canDecideDispute").GetBoolean());
        page = await StepAsync(assistant, id, "dispute", new DisputeDecisionDto(true, "fo-test-pirep you were right", RowVersion(page)), token);
        Assert.Equal("Queued", page.GetProperty("status").GetString());
        Assert.Equal("Upheld", page.GetProperty("dispute").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("assignedTo").ValueKind);
        Assert.Equal(1, page.GetProperty("profile").GetProperty("disputesUpheld").GetInt32());
        Assert.Contains(page.GetProperty("history").EnumerateArray(), step => step.GetProperty("note").GetString() == "flightops:events.disputeUpheld");
        await MailAsync(PilotVid, NotificationTypes.ContactThreadReplied, token);

        var row = await QueueRowAsync(assistant, tourId, id, token);
        Assert.Equal("Queued", row.GetProperty("status").GetString());
        Assert.True(row.GetProperty("canTake").GetBoolean());

        // Upheld is its one dispute: rejected again, it is not disputable a second time.
        page = await TakeAsync(assistant, id, token);
        page = await StepAsync(assistant, id, "decide", Decision(PirepStatus.Rejected, [dangerous], RowVersion(page)), token);
        read = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("reports").EnumerateArray().Single(entry => Id(entry) == id).GetProperty("disputableUntil").ValueKind);
        await RefusedAsync(pilot, $"{PirepEndpoints.Pattern}/{id}/dispute", new DisputeOpenDto("again", RowVersion(page)), "status", "flightops:errors.disputeAlready", token);
    }

    /// <summary>
    /// A clarification from the tour's pages (§3.10): through the core's form, citing a decided report, a leg and a rule of the
    /// tour; the validator of the report takes part; nothing of the report changes. A report somebody else flew, or a rule not in
    /// force on the tour, is refused as unknown.
    /// </summary>
    [Fact]
    public async Task AClarificationCitesTheToursObjectsAndTakesTheValidatorIn()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var other = await SignedInAsync(OtherPilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (dangerous, _, code) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        var ruleId = _rules[^1];
        await GrantValidateAsync(ValidatorVid, tourId, token);
        using var validator = await SignedInAsync(ValidatorVid, token);

        var id = await RejectedAsync(pilot, validator, tourId, legs[0], dangerous, token);

        ContactSubmitDto Clarification(params string[] references) => new(
            Department.FOD,
            "fo-test-pirep why",
            "fo-test-pirep please explain the rule",
            ContactKinds.Clarification,
            [.. references.Select(reference => new ContactReferenceInput(FlightOpsModule.ModuleKey, reference))]);

        await RefusedAsync(other, ContactsEndpoints.Pattern, Clarification(Pirep.ReferenceOf(id)), "references", "errors.contacts.referenceUnknown", token);
        await RefusedAsync(pilot, ContactsEndpoints.Pattern, Clarification(FlightOpsReferences.RuleReference(tourId, 999_999_999)), "references", "errors.contacts.referenceUnknown", token);

        var sent = await OkAsync(
            await pilot.PostAsJsonAsync(
                ContactsEndpoints.Pattern,
                Clarification(Pirep.ReferenceOf(id), FlightOpsReferences.LegReference(legs[1]), FlightOpsReferences.RuleReference(tourId, ruleId)),
                token),
            token);

        var thread = await OkAsync(await validator.GetAsync($"{ContactsEndpoints.Pattern}/{Id(sent)}/thread", token), token);
        Assert.Equal("clarification", thread.GetProperty("kind").GetString());
        Assert.Equal(ValidatorVid, Assert.Single(thread.GetProperty("participants").EnumerateArray()).GetInt32());
        var labels = thread.GetProperty("references").EnumerateArray().Select(reference => reference.GetProperty("label").GetString()!).ToList();
        Assert.Equal(3, labels.Count);
        Assert.Contains(labels, label => label.Contains($"{Milan} → {London}", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Contains(code, StringComparison.Ordinal));

        // The pilot is sent to the tour's page, not to the validation.
        var theirs = await OkAsync(await pilot.GetAsync($"{ContactsEndpoints.Pattern}/{Id(sent)}/thread", token), token);
        Assert.All(theirs.GetProperty("references").EnumerateArray(), reference => Assert.StartsWith("/tours/", reference.GetProperty("url").GetString(), StringComparison.Ordinal));

        // Nothing of the report moved.
        var read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{id}", token), token);
        Assert.Equal("Rejected", read.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("disputeStatus").ValueKind);
    }

    /// <summary>
    /// An issue on a leg (§3.11): the pilot writes it, the mailbox of the tour's department hears it, the staff read it in the
    /// generated list and close it; a pilot does not read the list. The block counts what waits — the issue, the dispute and the
    /// clarification nobody answered — for the staff, and nothing for the pilot.
    /// </summary>
    [Fact]
    public async Task APilotReportsAnIssueOnALegAndTheBlockCountsWhatWaits()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var issues = $"/api/flightops/tours/{tourId}/legs/{legs[1]}/issues";

        await RefusedAsync(pilot, issues, new LegIssueReportDto(" "), "body", "errors.required", token);
        using (var unknown = await pilot.PostAsJsonAsync($"/api/flightops/tours/{tourId}/legs/999999999/issues", new LegIssueReportDto("x"), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }

        using (var created = await pilot.PostAsJsonAsync(issues, new LegIssueReportDto("fo-test-pirep the arrival airport is closed"), token))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var mail = await hub.Notifications.AsNoTracking()
                .Where(row => row.Address == FodMailbox && row.Type == FlightOpsNotifications.LegIssueReported)
                .OrderByDescending(row => row.Id)
                .FirstAsync(token);
            Assert.Contains("fo-test-pirep the arrival airport is closed", mail.DataJson, StringComparison.Ordinal);
            Assert.Equal($"2 {Milan} → {London}", JsonDocument.Parse(mail.DataJson).RootElement.GetProperty("leg").GetString());
        }

        using (var refused = await pilot.GetAsync(LegIssueEndpoints.Pattern, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var list = await OkAsync(await coordinator.GetAsync($"{LegIssueEndpoints.Pattern}?filter[tourId]={tourId}", token), token);
        var issue = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(PilotVid, issue.GetProperty("pilot").GetProperty("vid").GetInt32());
        Assert.Equal(2, issue.GetProperty("legNumber").GetInt32());
        Assert.Equal("Open", issue.GetProperty("status").GetString());

        // What waits, for the staff: the issue — and, once the pilot asks something, the clarification too.
        var block = await OkAsync(await coordinator.GetAsync($"/api/blocks/data/{OpenIssuesProvider.BlockType}", token), token);
        var issuesBefore = block.GetProperty("legIssues").GetInt32();
        var clarificationsBefore = block.GetProperty("clarifications").GetInt32();
        Assert.True(issuesBefore >= 1);

        await OkAsync(
            await pilot.PostAsJsonAsync(
                ContactsEndpoints.Pattern,
                new ContactSubmitDto(
                    Department.FOD,
                    "fo-test-pirep a question",
                    "fo-test-pirep what does leg 2 ask",
                    ContactKinds.Clarification,
                    [new ContactReferenceInput(FlightOpsModule.ModuleKey, FlightOpsReferences.LegReference(legs[1]))]),
                token),
            token);

        block = await OkAsync(await coordinator.GetAsync($"/api/blocks/data/{OpenIssuesProvider.BlockType}", token), token);
        Assert.Equal(clarificationsBefore + 1, block.GetProperty("clarifications").GetInt32());
        Assert.Equal("fod", block.GetProperty("department").GetString());

        // Closed, it counts no more.
        await OkAsync(
            await coordinator.PutAsJsonAsync($"{LegIssueEndpoints.Pattern}/{Id(issue)}", new LegIssueWriteDto(LegIssueStatus.Resolved, "fo-test-pirep NOTAM checked", RowVersion(issue)), token),
            token);
        block = await OkAsync(await coordinator.GetAsync($"/api/blocks/data/{OpenIssuesProvider.BlockType}", token), token);
        Assert.Equal(issuesBefore - 1, block.GetProperty("legIssues").GetInt32());

        // The pilot counts nothing: none of it is theirs to handle.
        block = await OkAsync(await pilot.GetAsync($"/api/blocks/data/{OpenIssuesProvider.BlockType}", token), token);
        Assert.Equal(0, block.GetProperty("legIssues").GetInt32());
        Assert.Equal(0, block.GetProperty("clarifications").GetInt32());
    }

    /// <summary>A report of the pilot on a leg, taken and rejected by the validator on a dangerous error; its id.</summary>
    private async Task<long> RejectedAsync(HttpClient pilot, HttpClient validator, long tourId, long legId, long dangerous, CancellationToken cancellationToken)
    {
        var flown = _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1));
        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legId, flown), cancellationToken));
        var page = await TakeAsync(validator, id, cancellationToken);
        await StepAsync(validator, id, "decide", Decision(PirepStatus.Rejected, [dangerous], RowVersion(page)), cancellationToken);
        return id;
    }

    private static async Task RefusedAsync<T>(HttpClient client, string uri, T body, string field, string key, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, body, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        var errors = JsonDocument.Parse(text).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var keys) && keys.EnumerateArray().Any(item => item.GetString() == key), text);
    }

    private static IQueryable<ContactMessage> CrudThreads(HubDbContext hub) =>
        Core.Data.Crud.CrudSource.BackOffice<ContactMessage>(hub).AsNoTracking();

    /// <summary>
    /// The threads these tests opened and what they sent about them, taken back: the replies and references first (they keep
    /// their message), then the messages; and the mails about them to anybody — the FOD staff of other classes included.
    /// </summary>
    private async Task CleanThreadsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var vids = new[] { PilotVid, OtherPilotVid };

        var threads = CrudThreads(hub).Where(message => vids.Contains(message.CreatedBy)).Select(message => message.Id);
        await hub.ContactReplies.Where(reply => threads.Contains(reply.MessageId)).ExecuteDeleteAsync(cancellationToken);
        await hub.ContactReferences.Where(reference => threads.Contains(reference.MessageId)).ExecuteDeleteAsync(cancellationToken);
        await Core.Data.Crud.CrudSource.BackOffice<ContactMessage>(hub).Where(message => vids.Contains(message.CreatedBy)).ExecuteDeleteAsync(cancellationToken);

        await hub.Notifications
            .Where(row => row.Address == FodMailbox || (row.Type.StartsWith("contact.") && row.DataJson.Contains("fo-test-pirep")))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
