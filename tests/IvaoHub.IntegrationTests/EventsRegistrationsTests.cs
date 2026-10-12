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

        // Changed: the same registration, the optional answers taken out, and the words in the member's language.
        var changed = await OkAsync(await member.PutAsJsonAsync(Mine(id), Request((q.Dinner, false), (q.Seat, "1")), token), token);
        Assert.Equal(registered.GetProperty("id").GetInt64(), changed.GetProperty("id").GetInt64());
        Assert.Equal(["Coming to dinner: No", "Seat: Window"], changed.GetProperty("lines").EnumerateArray().Select(line => line.GetString()));

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

        // The sums of each question, in the reader's language, in the order of the form.
        var questions = await OkAsync(await coordinator.GetAsync($"{EventQuestionEndpoints.Pattern}?filter[eventId]={id}", token), token);
        Assert.Equal(
            [
                ["Yes 2", "No 0"],
                ["Window 1", "Aisle 1"],
                ["PC 1", "Monitor 2"],
                ["Total 5"],
                ["Answered 1"],
            ],
            questions.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("sums").EnumerateArray().Select(sum => sum.GetString()!).ToArray()));

        // The registrations, with the members named and their answers in words.
        var list = (await OkAsync(await coordinator.GetAsync($"{RegistrationEndpoints.StaffPattern}?filter[eventId]={id}", token), token))
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([MemberVid, OtherVid], list.Select(row => row.GetProperty("member").GetProperty("vid").GetInt32()));
        Assert.Equal(
            ["Coming to dinner: Yes", "Seat: Window", "Equipment: PC, Monitor", "Chairs: 2", "Anything else: vegetarian"],
            list[0].GetProperty("answers").EnumerateArray().Select(line => line.GetString()));

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
        var kept = await RegistrationRowAsync(over, PersonVid, $$"""{"q{{text}}":"{{TheirWords}}","q{{dinner}}":true}""", token);
        await RegistrationRowAsync(over, OtherVid, $$"""{"q{{text}}":"evt-test-e16 another's words"}""", token);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.bookingsToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.bookingsOver", 0, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.registrationsToCome", 1, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.registrationsOver", 1, nameof(ErasureOutcome.Anonymised)),
            ("events:erasure.availabilitiesToCome", 0, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.availabilitiesOver", 0, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, EventsLines(await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", token), token)));

        var erased = await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token);
        Assert.Equal(lines, EventsLines(erased));
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();

        // To come: gone. Over: under the pseudonym, the yes or no counted, their words gone; the other member's as it was.
        Assert.Empty(await RegistrationsAsync(toCome, token));
        var stored = (await RegistrationsAsync(over, token)).ToDictionary(row => row.Id);
        Assert.Equal(pseudonym, stored[kept].Vid);
        Assert.Equal(JsonNode.Parse($$"""{"q{{dinner}}":true}"""), JsonNode.Parse(stored[kept].AnswersJson), JsonNode.DeepEquals);
        Assert.Contains(stored.Values, row => row.Vid == OtherVid && row.AnswersJson.Contains("another's words", StringComparison.Ordinal));

        // Run again: nothing left about the VID.
        Assert.All(
            EventsLines(await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token)),
            line => Assert.Equal(0, line.Count));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

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
        var payload = new EventWriteDto(
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
            InPerson: true,
            Venue: Text("evt-test-e16 venue"));
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

    /// <summary>A published event in person of the class, written by the installation: one that started or ended could not be made any other way.</summary>
    private async Task<long> EventRowAsync(string name, DateTime starts, DateTime ends, CancellationToken cancellationToken)
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
