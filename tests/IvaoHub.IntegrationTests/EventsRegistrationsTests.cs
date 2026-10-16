using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The events in person (M4, E16, design §4-bis; note 2026-10-12-le-domande-e-l-iscrizione), through the real host: the coordinator of
/// the events writes the questions of an event in person and publishes it; a member registers, and the server checks each answer by
/// the type of its question and refuses a required one left out; the member changes the answers and is told each time; the staff read
/// the registrations with the answers in words and the sums of each question, and take one away with a reason; a member withdraws until
/// the start and not after; a question somebody answered keeps its type and its choices; cancelling the event tells whoever registered;
/// and erasing a registered member's data deletes their registration to an event to come and keeps the one of an event over, without
/// the texts they wrote.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsRegistrationsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range E16 was given by dalberone (761101–761105, after 761100 of E10n), free in every branch on 12 October 2026: two members —
    // the first with an address, so the mails are queued —, the coordinator of the events without one (the rule of the module's tests),
    // the person erased and the superadmin who erases them.
    private const int MemberVid = 761101;
    private const int OtherVid = 761102;
    private const int CoordinatorVid = 761103;
    private const int PersonVid = 761104;
    private const int SuperadminVid = 761105;

    private const string SlugStem = "evt-test-e16";

    /// <summary>A text answer that says who wrote it: the erasure takes it away.</summary>
    private const string TheirWords = "evt-test-e16 their words";

    /// <summary>The answer of a registration withdrawn before the erasure: the audit's copies lose it (E10n).</summary>
    private const string GoneWords = "evt-test-e16 withdrawn words";

    /// <summary>A text answer the member took out of their registration before it was kept by the erasure (the review of #255, point 2).</summary>
    private const string EarlierWords = "evt-test-e16 earlier words";

    /// <summary>A text stored as the answer to a choice question: no choice of it, so the erasure does not keep it.</summary>
    private const string TextAsAChoice = "evt-test-e16 a text as a choice";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly int[] People = [MemberVid, OtherVid, CoordinatorVid, PersonVid, SuperadminVid];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(MemberVid, position: null, "e16-member@example.org", superadmin: false, token);
        await SeedUserAsync(OtherVid, position: null, "e16-other@example.org", superadmin: false, token);
        await SeedUserAsync(CoordinatorVid, "IT-EC", email: null, superadmin: false, token);
        await SeedUserAsync(PersonVid, position: null, email: null, superadmin: false, token);
        await SeedUserAsync(SuperadminVid, position: null, email: null, superadmin: true, token);
        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, its mails, its position, the superadmin's role, the addresses.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.Email, (string?)null).SetProperty(user => user.IsSuperadmin, false));
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheAnswersAreCheckedByTheirTypeAndARequiredOneLeftOutIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, q) = await InPersonEventAsync(coordinator, "types", token);
        using var member = await SignedInAsync(MemberVid, token);

        // Every answer of the wrong kind, each on its own field: a word for a yes or no, a choice the question does not have, a key
        // that is not one of the several, a quantity that is not a whole number, a number for a text.
        var wrong = await RefusedAsync(
            await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, "yes"), (q.Seat, "9"), (q.Gear, new[] { "1", "7" }), (q.Chairs, 2.5), (q.Note, 4)), token),
            token);
        Assert.Equal(
            new Dictionary<string, string[]>
            {
                [$"answers.q{q.Dinner}"] = ["events:errors.answerYesNo"],
                [$"answers.q{q.Seat}"] = ["events:errors.answerChoice"],
                [$"answers.q{q.Gear}"] = ["events:errors.answerChoice"],
                [$"answers.q{q.Chairs}"] = ["events:errors.answerQuantity"],
                [$"answers.q{q.Note}"] = ["events:errors.answerText"],
            },
            wrong);

        // A quantity out of its range, too.
        Assert.Equal(["events:errors.answerQuantity"], (await RefusedAsync(
            await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, true), (q.Seat, "1"), (q.Chairs, RegistrationAnswers.MaxQuantity + 1)), token),
            token))[$"answers.q{q.Chairs}"]);

        // The two required questions left out — the choice empty is no answer —: refused, each on its field, and nothing stored.
        var missing = await RefusedAsync(await member.PutAsJsonAsync(Mine(id), Request((q.Seat, "")), token), token);
        Assert.Equal(["errors.required"], missing[$"answers.q{q.Dinner}"]);
        Assert.Equal(["errors.required"], missing[$"answers.q{q.Seat}"]);
        Assert.Empty(await RegistrationsAsync(id, token));

        // Right: registered, with the answers as the column keeps them — several choices in the order of the question, the text trimmed,
        // an answer to a question the event does not have left out.
        var registered = await CreatedAsync(
            await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, true), (q.Seat, "2"), (q.Gear, new[] { "2", "1" }), (q.Chairs, 3), (q.Note, "  a note  "), (999999, "nobody's")), token),
            token);
        Assert.Equal(
            JsonNode.Parse($$"""{"q{{q.Dinner}}":true,"q{{q.Seat}}":"2","q{{q.Gear}}":["1","2"],"q{{q.Chairs}}":3,"q{{q.Note}}":"a note"}"""),
            JsonNode.Parse(Assert.Single(await RegistrationsAsync(id, token)).AnswersJson),
            JsonNode.DeepEquals);
        Assert.True(registered.GetProperty("changeable").GetBoolean());

        // Changed: the same registration, the optional answers taken out; the answers as data with the event's questions, which the page
        // writes in words in the language on screen (Carmine's answer 6 on #255).
        var changed = await OkAsync(await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, false), (q.Seat, "1")), token), token);
        Assert.Equal(registered.GetProperty("id").GetInt64(), changed.GetProperty("id").GetInt64());
        Assert.Equal(
            JsonNode.Parse($$"""{"q{{q.Dinner}}":false,"q{{q.Seat}}":"1"}"""),
            JsonNode.Parse(changed.GetProperty("answers").GetRawText()),
            JsonNode.DeepEquals);
        Assert.Equal(
            [q.Dinner, q.Seat, q.Gear, q.Chairs, q.Note],
            changed.GetProperty("questions").EnumerateArray().Select(question => question.GetProperty("id").GetInt64()));

        // Told each time, with the answers as they were then.
        var mails = await MailsAsync(MemberVid, EventsNotifications.RegistrationReceived, token);
        Assert.Equal(2, mails.Count);
        Assert.Contains("Coming to dinner: No", mails[^1].DataJson, StringComparison.Ordinal);
        Assert.Contains("evt-test-e16 venue", mails[^1].DataJson, StringComparison.Ordinal);

        // Their own page lists it; another member reads nothing of it.
        Assert.Equal([id], (await OkAsync(await member.GetAsync(MinePattern, token), token)).EnumerateArray().Select(row => row.GetProperty("eventId").GetInt64()));
        using var other = await SignedInAsync(OtherVid, token);
        Assert.Empty((await OkAsync(await other.GetAsync(MinePattern, token), token)).EnumerateArray());
    }

    [Fact]
    public async Task TheStaffReadTheRegistrationsWithTheSumsAndTakeOneAwayWithAReason()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, q) = await InPersonEventAsync(coordinator, "sums", token);
        using var member = await SignedInAsync(MemberVid, token);
        using var other = await SignedInAsync(OtherVid, token);

        await CreatedAsync(await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, true), (q.Seat, "1"), (q.Gear, new[] { "1", "2" }), (q.Chairs, 2), (q.Note, "vegetarian")), token), token);
        await CreatedAsync(await other.PutAsJsonAsync(Mine(id), Request((q.Dinner, true), (q.Seat, "2"), (q.Gear, new[] { "2" }), (q.Chairs, 3)), token), token);

        // The sums of each question, in numbers by the keys of the question, in the order of the form: the page writes the words.
        var questions = await OkAsync(await coordinator.GetAsync($"{EventQuestionEndpoints.Pattern}?filter[eventId]={id}", token), token);
        Assert.Equal(
            [
                "answered 2, yes 2, no 0, total 0, choices ",
                "answered 2, yes 0, no 0, total 0, choices 1=1 2=1",
                "answered 2, yes 0, no 0, total 0, choices 1=1 2=2",
                "answered 2, yes 0, no 0, total 5, choices ",
                "answered 1, yes 0, no 0, total 0, choices ",
            ],
            questions.GetProperty("items").EnumerateArray().Select(row => Sum(row.GetProperty("sum"))));

        // The registrations, with the members named and their answers as they gave them.
        var list = (await OkAsync(await coordinator.GetAsync($"{RegistrationEndpoints.StaffPattern}?filter[eventId]={id}", token), token))
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([MemberVid, OtherVid], list.Select(row => row.GetProperty("member").GetProperty("vid").GetInt32()));
        Assert.Equal(
            JsonNode.Parse($$"""{"q{{q.Dinner}}":true,"q{{q.Seat}}":"1","q{{q.Gear}}":["1","2"],"q{{q.Chairs}}":2,"q{{q.Note}}":"vegetarian"}"""),
            JsonNode.Parse(list[0].GetProperty("answers").GetRawText()),
            JsonNode.DeepEquals);

        // A member reads nobody's registrations there.
        using (var response = await member.GetAsync($"{RegistrationEndpoints.StaffPattern}?filter[eventId]={id}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // «Take away», with a reason: the member is no longer registered, and told why; without a reason, refused.
        var registration = list[0].GetProperty("id").GetInt64();
        Assert.Equal(["errors.required"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync($"{RegistrationEndpoints.StaffPattern}/{registration}/remove", new BookingRemovalRequest(" "), token),
            token))["reason"]);
        using (var response = await coordinator.PostAsJsonAsync(
            $"{RegistrationEndpoints.StaffPattern}/{registration}/remove",
            new BookingRemovalRequest("evt-test-e16 the room is full"),
            token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal([OtherVid], (await RegistrationsAsync(id, token)).Select(row => row.Vid));
        Assert.Contains("evt-test-e16 the room is full", Assert.Single(await MailsAsync(MemberVid, EventsNotifications.RegistrationRemoved, token)).DataJson, StringComparison.Ordinal);

        // A member takes away nobody's registration.
        using (var response = await other.PostAsJsonAsync($"{RegistrationEndpoints.StaffPattern}/{registration}/remove", new BookingRemovalRequest("no"), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task AMemberWithdrawsUntilTheStartAndNotAfter()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        // One event to come, and one started an hour ago: written by the installation, a registration on each.
        var toCome = await EventRowAsync("to-come", now.AddDays(5), now.AddDays(5).AddHours(6), token);
        var started = await EventRowAsync("started", now.AddHours(-1), now.AddHours(5), token);
        await RegistrationRowAsync(toCome, MemberVid, "{}", token);
        await RegistrationRowAsync(started, MemberVid, "{}", token);
        using var member = await SignedInAsync(MemberVid, token);

        // After the start: neither withdrawn nor changed, and the page says so.
        Assert.Equal(["events:errors.registrationClosed"], (await RefusedAsync(await member.DeleteAsync(Mine(started), token), token))["id"]);
        Assert.Equal(["events:errors.registrationClosed"], (await RefusedAsync(await member.PutAsJsonAsync(Mine(started), Request(), token), token))["id"]);
        Assert.Single(await RegistrationsAsync(started, token));
        var mine = (await OkAsync(await member.GetAsync(MinePattern, token), token)).EnumerateArray()
            .ToDictionary(row => row.GetProperty("eventId").GetInt64(), row => (row.GetProperty("changeable").GetBoolean(), row.GetProperty("withdrawable").GetBoolean()));
        Assert.Equal((false, false), mine[started]);
        Assert.Equal((true, true), mine[toCome]);

        // Before it: the row deleted, by the member, and the audit says so.
        using (var response = await member.DeleteAsync(Mine(toCome), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Empty(await RegistrationsAsync(toCome, token));
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AnyAsync(
            row => row.Entity == "evt_registrations" && row.Action == "deleted" && row.Vid == MemberVid,
            token));

        // Withdrawn already: nothing there.
        using (var response = await member.DeleteAsync(Mine(toCome), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task AnAnsweredQuestionKeepsItsTypeAndItsChoicesAndCancellingTellsWhoeverRegistered()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, q) = await InPersonEventAsync(coordinator, "answered", token);

        // Not answered yet: a choice is taken out, freely.
        var gear = await OkAsync(await coordinator.GetAsync($"{EventQuestionEndpoints.Pattern}/{q.Gear}", token), token);
        await OkAsync(await coordinator.PutAsJsonAsync($"{EventQuestionEndpoints.Pattern}/{q.Gear}", Question(id, 3, QuestionType.MultiChoice, "Equipment", gear, ("1", "PC"), (null, "Monitor"), (null, "Keyboard")), token), token);

        using var member = await SignedInAsync(MemberVid, token);
        await CreatedAsync(await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, true), (q.Seat, "1"), (q.Gear, new[] { "1" })), token), token);

        // Answered: another type, refused; a choice taken out, refused; one rewritten and one added, accepted — the new one with a key of
        // its own, the old ones with theirs.
        gear = await OkAsync(await coordinator.GetAsync($"{EventQuestionEndpoints.Pattern}/{q.Gear}", token), token);
        var keys = gear.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("key").GetString()).ToList();
        Assert.Equal(["1", "3", "4"], keys);
        Assert.Equal(["events:errors.questionAnsweredType"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventQuestionEndpoints.Pattern}/{q.Gear}", Question(id, 3, QuestionType.Choice, "Equipment", gear, ("1", "PC"), ("3", "Monitor"), ("4", "Keyboard")), token),
            token))["type"]);
        Assert.Equal(["events:errors.questionAnsweredChoices"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventQuestionEndpoints.Pattern}/{q.Gear}", Question(id, 3, QuestionType.MultiChoice, "Equipment", gear, ("3", "Monitor"), ("4", "Keyboard")), token),
            token))["choices"]);
        var saved = await OkAsync(
            await coordinator.PutAsJsonAsync($"{EventQuestionEndpoints.Pattern}/{q.Gear}", Question(id, 3, QuestionType.MultiChoice, "Equipment", gear, ("1", "Laptop"), ("3", "Monitor"), ("4", "Keyboard"), (null, "Mouse")), token),
            token);
        Assert.Equal(["1", "3", "4", "5"], saved.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("key").GetString()));

        // The event is not deleted with somebody registered, nor turned off «in person»; it is cancelled, and they are told.
        using (var response = await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await OkAsync(
            await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/cancel", new EventCancelRequest(Text("evt-test-e16 cancelled"), default), token),
            token);
        Assert.Single(await MailsAsync(MemberVid, EventsNotifications.EventCancelled, token));
    }

    [Fact]
    public async Task AnErasedMemberLeavesTheRegistrationsOfEventsOverWithoutTheirWordsAndNoneToCome()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        var toCome = await EventRowAsync("erase-to-come", now.AddDays(5), now.AddDays(5).AddHours(6), token);
        var over = await EventRowAsync("erase-over", now.AddDays(-5), now.AddDays(-5).AddHours(6), token);
        var (text, dinner) = await QuestionRowsAsync(over, token);
        await RegistrationRowAsync(toCome, PersonVid, "{}", token);

        // Two questions the staff deleted after the person answered them — a text and a yes or no —: the answers stay in the row until
        // the member's next change, which never comes once the event is over (the review of #255, point 1).
        var (deletedText, deletedYesNo) = await QuestionRowsAsync(over, token);

        // And a choice holding a text — written while the question was a text, in the instant before it became a choice — beside several
        // choices that are the question's: the first is no choice and goes, the second is counted and stays (the second reading, small 1).
        var seat = await ChoiceQuestionRowAsync(over, QuestionType.Choice, token);
        var gear = await ChoiceQuestionRowAsync(over, QuestionType.MultiChoice, token);
        var kept = await RegistrationRowAsync(
            over,
            PersonVid,
            $$"""{"q{{text}}":"{{TheirWords}}","q{{dinner}}":true,"q{{deletedText}}":"evt-test-e16 coeliac","q{{deletedYesNo}}":false,"q{{seat}}":"{{TextAsAChoice}}","q{{gear}}":["1","2"]}""",
            token);
        await DeleteQuestionsAsync([deletedText, deletedYesNo], token);
        await RegistrationRowAsync(over, OtherVid, $$"""{"q{{text}}":"evt-test-e16 another's words"}""", token);

        // A registration the person sent and withdrew before the erasure: no eraser finds it, and its copies in the audit hold their words
        // until the core empties them (E10n, the mark on the answers).
        var withdrawnFrom = await EventRowAsync("erase-withdrawn", now.AddDays(6), now.AddDays(6).AddHours(6), token);
        var (withdrawnText, _) = await QuestionRowsAsync(withdrawnFrom, token);
        using (var person = await SignedInAsync(PersonVid, token))
        {
            await CreatedAsync(await person.PutAsJsonAsync(Mine(withdrawnFrom), Request((withdrawnText, GoneWords)), token), token);
            using var withdrawn = await person.DeleteAsync(Mine(withdrawnFrom), token);
            Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        }

        Assert.True(await AuditMentionsAsync(GoneWords, token), "Without the answer in the audit, the check below would prove nothing.");

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.bookingsToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.bookingsOver", 0, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.registrationsToCome", 1, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.registrationsOver", 1, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.availabilitiesToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.availabilitiesOver", 0, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.shiftsToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.shiftsOver", 0, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, EventsLines(await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", token), token)));

        var erased = await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token);
        Assert.Equal(lines, EventsLines(erased));
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();

        // To come: gone. Over: under the pseudonym, only the yes or no of a question the event has, counted; their words gone, and the
        // answers to the questions deleted, a text and a yes or no alike, and the text stored as a choice; the choices that are the
        // question's stay. The other member's as it was.
        Assert.Empty(await RegistrationsAsync(toCome, token));
        var stored = (await RegistrationsAsync(over, token)).ToDictionary(row => row.Id);
        Assert.Equal(pseudonym, stored[kept].Vid);
        Assert.Equal(JsonNode.Parse($$"""{"q{{dinner}}":true,"q{{gear}}":["1","2"]}"""), JsonNode.Parse(stored[kept].AnswersJson), JsonNode.DeepEquals);
        Assert.Contains(stored.Values, row => row.Vid == OtherVid && row.AnswersJson.Contains("another's words", StringComparison.Ordinal));

        // The registration withdrawn before: nothing of its answers in the audit.
        Assert.False(await AuditMentionsAsync(GoneWords, token));
        Assert.False(await AuditMentionsAsync("evt-test-e16 coeliac", token));
        Assert.False(await AuditMentionsAsync(TextAsAChoice, token));

        // Run again: nothing left about the VID.
        Assert.All(
            EventsLines(await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token)),
            line => Assert.Equal(0, line.Count));
    }

    [Fact]
    public async Task AKeptRegistrationKeepsNoWordsOfAnEarlierAnswerInTheAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        // The person registers with a text, then saves again without it; the event then ends.
        var id = await EventRowAsync("erase-history", now.AddDays(5), now.AddDays(5).AddHours(6), token);
        var (text, dinner) = await QuestionRowsAsync(id, token);
        using (var person = await SignedInAsync(PersonVid, token))
        {
            await CreatedAsync(await person.PutAsJsonAsync(Mine(id), Request((text, EarlierWords), (dinner, true)), token), token);
            await OkAsync(await person.PutAsJsonAsync(Mine(id), Request((dinner, true)), token), token);
        }

        await MoveEventAsync(id, now.AddDays(-2), now.AddDays(-2).AddHours(6), token);
        Assert.True(await AuditMentionsAsync(EarlierWords, token), "Without the earlier answer in the audit, the check below would prove nothing.");

        // Erased: the row stays under the pseudonym with the yes or no, and its earlier copies in the audit lose the words (the review of
        // #255, point 2: a row whose answers do not change would otherwise count as changed only in its person column).
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var pseudonym = (await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token))
            .GetProperty("pseudonym").GetInt32();

        var kept = Assert.Single(await RegistrationsAsync(id, token));
        Assert.Equal(pseudonym, kept.Vid);
        Assert.Equal(JsonNode.Parse($$"""{"q{{dinner}}":true}"""), JsonNode.Parse(kept.AnswersJson), JsonNode.DeepEquals);
        Assert.False(await AuditMentionsAsync(EarlierWords, token));
    }

    [Fact]
    public async Task TheSwitchTheVenueAndTheQuestionsKeepTheirRules()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // An event in person without a venue is not published: the venue is asked in every language.
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(30).AddHours(9), DateTimeKind.Utc);
        var draft = await CreatedAsync(await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, Payload("rules", starts, inPerson: true, venue: null), token), token);
        var id = draft.GetProperty("id").GetInt64();
        Assert.Contains("venue", (await RefusedAsync(
            await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), token),
            token)).Keys);

        // With a question, the switch is not turned off.
        await CreatedAsync(await coordinator.PostAsJsonAsync(EventQuestionEndpoints.Pattern, Question(id, 1, QuestionType.YesNo, "Dinner", read: null), token), token);
        var row = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        Assert.Equal(["events:errors.inPersonHasQuestions"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", Payload("rules", starts, inPerson: false, venue: null) with { RowVersion = row.GetProperty("rowVersion").GetDateTime() }, token),
            token))["inPerson"]);

        // A member writes no question.
        using (var member = await SignedInAsync(MemberVid, token))
        using (var response = await member.PostAsJsonAsync(EventQuestionEndpoints.Pattern, Question(id, 2, QuestionType.YesNo, "Mine", read: null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // At most thirty questions.
        await QuestionRowsAsync(id, EventQuestion.MaxPerEvent - 1, token);
        Assert.Equal(["events:errors.questionsTooMany"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventQuestionEndpoints.Pattern, Question(id, 99, QuestionType.YesNo, "One too many", read: null), token),
            token))["eventId"]);

        // An event not in person takes no question.
        var online = (await CreatedAsync(await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, Payload("rules-online", starts, inPerson: false, venue: null), token), token))
            .GetProperty("id").GetInt64();
        Assert.Equal(["events:errors.questionNotInPerson"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventQuestionEndpoints.Pattern, Question(online, 1, QuestionType.YesNo, "Dinner", read: null), token),
            token))["eventId"]);
    }

    [Fact]
    public async Task NobodyRegistersWhereTheEventTakesNoRegistration()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var (starts, ends) = (now.AddDays(5), now.AddDays(5).AddHours(6));

        var notInPerson = await EventRowAsync("not-in-person", starts, ends, token, row => row.InPerson = false);
        var cancelled = await EventRowAsync("cancelled", starts, ends, token, row => (row.CancelledAt, row.CancelledBy, row.CancellationNote) = (now, CoordinatorVid, Text("evt-test-e16 cancelled")));
        var draft = await EventRowAsync("draft", starts, ends, token, row => (row.Status, row.PublishedAt) = (PublishStatus.Draft, null));
        var notSeenYet = await EventRowAsync("not-seen-yet", starts, ends, token, row => row.VisibleFromUtc = now.AddDays(1));
        using var member = await SignedInAsync(MemberVid, token);

        foreach (var hidden in new[] { notInPerson, draft, notSeenYet })
        {
            using var response = await member.PutAsJsonAsync(Mine(hidden), Request(), token);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Equal(["events:errors.registrationClosed"], (await RefusedAsync(await member.PutAsJsonAsync(Mine(cancelled), Request(), token), token))["id"]);
        foreach (var id in new[] { notInPerson, cancelled, draft, notSeenYet })
        {
            Assert.Empty(await RegistrationsAsync(id, token));
        }
    }

    [Fact]
    public async Task TheSecondRegistrationOfTheSameInstantIsAChangeOfTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var id = await EventRowAsync("same-instant", now.AddDays(5), now.AddDays(5).AddHours(6), token);
        var (_, dinner) = await QuestionRowsAsync(id, token);
        using var member = await SignedInAsync(MemberVid, token);

        // A transaction of the test holds what another window of the member would hold: their registration, inserted, not yet committed.
        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            database.Registrations.Add(new EventRegistration
            {
                EventId = id,
                Vid = MemberVid,
                AnswersJson = $$"""{"q{{dinner}}":false}""",
                CreatedAt = now,
                UpdatedAt = now,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });
            await database.SaveChangesAsync(token);

            // The request reads no registration — the other is not committed —, inserts its own, and waits on the unique index.
            var request = member.PutAsJsonAsync(Mine(id), Request((dinner, true)), token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        // The index answered, and the request changed the registration it found: one row, with the answers it sent.
        await OkAsync(answered, token);
        var stored = Assert.Single(await RegistrationsAsync(id, token));
        Assert.Equal(JsonNode.Parse($$"""{"q{{dinner}}":true}"""), JsonNode.Parse(stored.AnswersJson), JsonNode.DeepEquals);
    }
    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>The sums of a question in one line, the choices by their keys in order.</summary>
    private static string Sum(JsonElement sum) =>
        $"answered {sum.GetProperty("answered").GetInt32()}, yes {sum.GetProperty("yes").GetInt32()}, no {sum.GetProperty("no").GetInt32()}, "
        + $"total {sum.GetProperty("total").GetInt32()}, choices "
        + string.Join(' ', sum.GetProperty("choices").EnumerateObject().OrderBy(choice => choice.Name, StringComparer.Ordinal).Select(choice => $"{choice.Name}={choice.Value.GetInt32()}"));

    private const string MinePattern = RegistrationEndpoints.MinePattern;

    /// <summary>The five questions of the class's events, by their ids.</summary>
    private sealed record Questions(long Dinner, long Seat, long Gear, long Chairs, long Note);

    private static string Mine(long eventId) => $"{MinePattern}/{eventId}";

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>The answers as the member's form sends them, by question.</summary>
    private static RegistrationRequest Request(params (long Question, object Answer)[] answers) =>
        new(JsonSerializer.SerializeToNode(answers.ToDictionary(answer => EventQuestion.AnswerKey(answer.Question), answer => answer.Answer))!.AsObject());

    /// <summary>A question as its form sends it, with the version read and its choices — a null key is a new one.</summary>
    private static EventQuestionWriteDto Question(long eventId, int ordinal, QuestionType type, string label, JsonElement? read, params (string? Key, string Label)[] choices) =>
        new(
            eventId,
            ordinal,
            type,
            Text(label),
            [.. choices.Select(choice => new QuestionChoiceWriteDto(choice.Key, Text(choice.Label)))],
            Required: false,
            read is { } row ? row.GetProperty("rowVersion").GetDateTime() : default);

    /// <summary>
    /// An event in person of the class, written and published by the coordinator through the API, with its five questions: a yes or no
    /// and a choice required, several choices, a quantity and a text not. From 09:00 to 18:00 of a day a month away.
    /// </summary>
    private static async Task<(long Id, Questions Questions)> InPersonEventAsync(HttpClient coordinator, string name, CancellationToken cancellationToken)
    {
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(30).AddHours(9), DateTimeKind.Utc);
        var payload = Payload(name, starts, inPerson: true, venue: Text("evt-test-e16 venue"));
        var id = (await CreatedAsync(await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, payload, cancellationToken), cancellationToken)).GetProperty("id").GetInt64();

        async Task<long> AddAsync(int ordinal, QuestionType type, string label, bool required, params (string? Key, string Label)[] choices) =>
            (await CreatedAsync(
                await coordinator.PostAsJsonAsync(EventQuestionEndpoints.Pattern, Question(id, ordinal, type, label, read: null, choices) with { Required = required }, cancellationToken),
                cancellationToken)).GetProperty("id").GetInt64();

        var questions = new Questions(
            await AddAsync(1, QuestionType.YesNo, "Coming to dinner", required: true),
            await AddAsync(2, QuestionType.Choice, "Seat", required: true, (null, "Window"), (null, "Aisle")),
            await AddAsync(3, QuestionType.MultiChoice, "Equipment", required: false, (null, "PC"), (null, "Monitor")),
            await AddAsync(4, QuestionType.Quantity, "Chairs", required: false),
            await AddAsync(5, QuestionType.Text, "Anything else", required: false));

        await OkAsync(await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), cancellationToken), cancellationToken);
        return (id, questions);
    }

    /// <summary>An event of the class as its form sends it: from 09:00 to 18:00 of <paramref name="starts"/>'s day, in person or not.</summary>
    private static EventWriteDto Payload(string name, DateTime starts, bool inPerson, Localized<string>? venue) => new(
        "rfo",
        PublicSlots: false,
        PrivateSlots: false,
        WholeDivision: false,
        EventOrganizer.Division,
        ExternalUrl: null,
        Text(Slug(name)),
        Slug(name),
        Text(Slug(name)),
        Body: null,
        BannerMediaId: null,
        VisibleFromUtc: null,
        BookingOpensAtUtc: null,
        starts,
        starts.AddHours(9),
        Visibility.Public,
        default,
        InPerson: inPerson,
        Venue: venue);

    /// <summary>Questions of an event written by the installation, yes or no, as many as asked.</summary>
    private async Task QuestionRowsAsync(long eventId, int count, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        database.Questions.AddRange(Enumerable.Range(1, count).Select(ordinal => new EventQuestion
        {
            EventId = eventId,
            Ordinal = ordinal,
            Type = QuestionType.YesNo,
            Label = Text($"evt-test-e16 {ordinal}"),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        }));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A question of an event with two choices, keys «1» and «2», written by the installation: one choice or several.</summary>
    private async Task<long> ChoiceQuestionRowAsync(long eventId, QuestionType type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var question = new EventQuestion
        {
            EventId = eventId,
            Ordinal = 10,
            Type = type,
            Label = Text($"evt-test-e16 {type}"),
            Choices = [new QuestionChoice("1", Text("evt-test-e16 one")), new QuestionChoice("2", Text("evt-test-e16 two"))],
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Questions.Add(question);
        await database.SaveChangesAsync(cancellationToken);
        return question.Id;
    }

    /// <summary>Questions deleted, as the staff delete them after somebody answered.</summary>
    private async Task DeleteQuestionsAsync(long[] ids, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Questions.IgnoreQueryFilters()
            .Where(question => ids.Contains(question.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>An event moved to other times past every rule: one that has ended could not be made any other way.</summary>
    private async Task MoveEventAsync(long id, DateTime starts, DateTime ends, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
            .Where(row => row.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.StartsAtUtc, starts).SetProperty(row => row.EndsAtUtc, ends), cancellationToken);
    }

    /// <summary>Until a transaction waits for a lock: the request under test, behind the transaction the test holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms: asked more often, it keeps answering what
    /// it saw the first time (CONTRIBUTING.md).</remarks>
    private async Task WaitForALockWaitAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'", connection);
        for (var tries = 0; tries < 40; tries++)
        {
            await Task.Delay(250, cancellationToken);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("The request never waited for the transaction the test holds.");
    }
    /// <summary>A published event in person of the class, written by the installation: one that started or ended could not be made any other way.</summary>
    private async Task<long> EventRowAsync(string name, DateTime starts, DateTime ends, CancellationToken cancellationToken, Action<Event>? write = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            InPerson = true,
            Venue = Text("evt-test-e16 venue"),
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = ends,
            Status = PublishStatus.Published,
            PublishedAt = starts.AddDays(-10),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        write?.Invoke(row);
        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    /// <summary>A text question and a yes or no of an event, written by the installation.</summary>
    private async Task<(long Text, long YesNo)> QuestionRowsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        EventQuestion Row(int ordinal, QuestionType type) => new()
        {
            EventId = eventId,
            Ordinal = ordinal,
            Type = type,
            Label = Text($"evt-test-e16 {type}"),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        var text = Row(1, QuestionType.Text);
        var yesNo = Row(2, QuestionType.YesNo);
        database.Questions.AddRange(text, yesNo);
        await database.SaveChangesAsync(cancellationToken);
        return (text.Id, yesNo.Id);
    }

    /// <summary>A registration of a member, written by the installation.</summary>
    private async Task<long> RegistrationRowAsync(long eventId, int vid, string answers, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var registration = new EventRegistration
        {
            EventId = eventId,
            Vid = vid,
            AnswersJson = answers,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Registrations.Add(registration);
        await database.SaveChangesAsync(cancellationToken);
        return registration.Id;
    }

    /// <summary>The registrations of an event as stored, read past every filter.</summary>
    private async Task<List<EventRegistration>> RegistrationsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Registrations.IgnoreQueryFilters().AsNoTracking()
            .Where(registration => registration.EventId == eventId)
            .OrderBy(registration => registration.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Whether a row of the audit log still copies a text with the marker in it, before or after its change.</summary>
    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    /// <summary>The mails of one type queued for a member of the class, about an event of the class, oldest first.</summary>
    private async Task<List<Notification>> MailsAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == type && row.Vid == vid && row.DataJson.Contains(SlugStem))
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The events' lines of an erasure's preview or result: what, how many, and what becomes of them.</summary>
    private static (string Key, int Count, string Outcome)[] EventsLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{EventsModule.ModuleKey}:", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

    private static async Task<JsonElement> CreatedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    /// <summary>The keys of a 400, field by field.</summary>
    private static async Task<Dictionary<string, string[]>> RefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject()
                .ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray());
        }
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member in English, with one position of the staff or none, with an address or not, the superadmin or not.</summary>
    private async Task SeedUserAsync(int vid, string? position, string? email, bool superadmin, CancellationToken cancellationToken)
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

        user.FirstName = "Test";
        user.LastName = "Registrations";
        user.Email = email;
        user.Locale = "en";
        user.IsStaff = position is not null;
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        if (position is not null && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows and what they projected, and the mails of its people.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .Where(row => People.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
