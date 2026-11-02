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
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The parallel activities of an event in person (M4, E17, design §4-bis; note 2026-10-15-le-attivita-e-la-conservazione-breve), through
/// the real host: the coordinator of the events writes an activity with its turns and its seats; a member registered takes a seat in a free
/// turn, never in one over another of theirs, and gives it back; the last seat of a turn taken by two members in the same instant goes to one
/// of them; withdrawing, or being taken away, takes the seats with the registration, and an activity with seats taken stays; «Duplicate»
/// copies the questions and the activities and nothing of the members; the short retention keeps the sums in the statistics and deletes the
/// registrations and the seats; and erasing a person deletes their seats of an event to come and keeps the ones of an event over.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsActivitiesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range E17 was given (761150–761179), free in every branch on 15 October 2026: three members — the first with an address, so the
    // mails are queued —, the coordinator of the events without one (the rule of the module's tests), the person erased and the superadmin
    // who erases them.
    private const int MemberVid = 761150;
    private const int OtherVid = 761151;
    private const int ThirdVid = 761152;
    private const int CoordinatorVid = 761153;
    private const int PersonVid = 761154;
    private const int SuperadminVid = 761155;

    private const string SlugStem = "evt-test-e17";

    /// <summary>A text answer of a registration the retention deletes: its copies in the audit lose it (E10s).</summary>
    private const string RetiredWords = "evt-test-e17 retired words";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly int[] People = [MemberVid, OtherVid, ThirdVid, CoordinatorVid, PersonVid, SuperadminVid];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(MemberVid, position: null, "e17-member@example.org", superadmin: false, token);
        await SeedUserAsync(OtherVid, position: null, email: null, superadmin: false, token);
        await SeedUserAsync(ThirdVid, position: null, email: null, superadmin: false, token);
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
    public async Task TheStaffWriteTheActivitiesOfAnEventInPersonWithTheirRules()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, starts) = await InPersonEventAsync(coordinator, "rules", token);

        // Four turns of thirty minutes between ten and noon, the last one ending at noon; a window of ten to a quarter past noon has the
        // same four: the turns that fit whole.
        var created = await CreatedAsync(
            await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(id, "Simulator", starts.AddHours(1), starts.AddHours(3).AddMinutes(15), 30, 2), token),
            token);
        Assert.Equal(4, created.GetProperty("turns").GetInt32());

        // Outside the event, with no turn, with too many: each refused on its field.
        Assert.Equal(["events:errors.activityOutsideEvent"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(id, "Before", starts.AddHours(-1), starts.AddHours(1), 30, 1), token),
            token))["fromUtc"]);
        Assert.Equal(["events:errors.activityNoTurn"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(id, "Short", starts.AddHours(1), starts.AddHours(1).AddMinutes(20), 30, 1), token),
            token))["slotMinutes"]);
        var wholeDay = await EventRowAsync("rules-long", starts, starts.AddDays(1), token);
        Assert.Equal(["events:errors.activityTooManyTurns"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(wholeDay, "Many", starts, starts.AddDays(1), 5, 1), token),
            token))["slotMinutes"]);

        // A name in one language only, a place written in one only: the members read both in theirs.
        var half = Activity(id, "Half", starts.AddHours(1), starts.AddHours(2), 30, 1) with
        {
            Name = new Localized<string>(new Dictionary<string, string> { ["en"] = "Half" }),
            Place = new Localized<string>(new Dictionary<string, string> { ["en"] = "Hangar" }),
        };
        var refused = await RefusedAsync(await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, half, token), token);
        Assert.Contains("name", refused.Keys);
        Assert.Contains("place", refused.Keys);

        // An event not in person takes no activity, and a member writes none.
        var online = await EventRowAsync("rules-online", starts, starts.AddHours(9), token, row => row.InPerson = false);
        Assert.Equal(["events:errors.activityNotInPerson"], (await RefusedAsync(
            await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(online, "Online", starts.AddHours(1), starts.AddHours(2), 30, 1), token),
            token))["eventId"]);
        using (var member = await SignedInAsync(MemberVid, token))
        using (var response = await member.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(id, "Mine", starts.AddHours(1), starts.AddHours(2), 30, 1), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // With an activity, the switch «in person» is not turned off.
        var row = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        Assert.Equal(["events:errors.inPersonHasQuestions"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", Payload("rules", starts, inPerson: false) with { RowVersion = row.GetProperty("rowVersion").GetDateTime() }, token),
            token))["inPerson"]);

        // The page of the event shows each turn with its free seats, to a visitor too.
        using var visitor = _factory.CreateApiClient();
        var page = await OkAsync(await visitor.GetAsync($"/api/events/public/{Slug("rules")}", token), token);
        var activity = Assert.Single(page.GetProperty("activities").EnumerateArray());
        Assert.Equal([2, 2, 2, 2], activity.GetProperty("turns").EnumerateArray().Select(turn => turn.GetProperty("free").GetInt32()));
    }

    [Fact]
    public async Task AMemberTakesASeatOnlyRegisteredInAFreeTurnNotOverAnotherAndGivesItBack()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, starts) = await InPersonEventAsync(coordinator, "seats", token);
        var simulator = await ActivityAsync(coordinator, id, "Simulator", starts.AddHours(1), starts.AddHours(3), 30, seats: 1, token);
        var tower = await ActivityAsync(coordinator, id, "Tower", starts.AddHours(1), starts.AddHours(3), 60, seats: 5, token);
        using var member = await SignedInAsync(MemberVid, token);
        using var other = await SignedInAsync(OtherVid, token);

        // Not registered: no seat.
        Assert.Equal(["events:errors.seatNotRegistered"], (await RefusedAsync(
            await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token),
            token))[MemberSeats.TurnField]);

        await CreatedAsync(await member.PutAsJsonAsync($"{RegistrationEndpoints.MinePattern}/{id}", new RegistrationRequest(new JsonObject()), token), token);
        await CreatedAsync(await other.PutAsJsonAsync($"{RegistrationEndpoints.MinePattern}/{id}", new RegistrationRequest(new JsonObject()), token), token);

        // A seat at ten in the simulator.
        var seat = await CreatedAsync(await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token), token);
        Assert.Equal(starts.AddHours(1).AddMinutes(30), seat.GetProperty("endsAtUtc").GetDateTime());

        // Not a turn of the activity; the same turn again; the tower from ten, which meets it; the simulator's turn taken by another, full.
        Assert.Equal(["events:errors.seatNoTurn"], (await RefusedAsync(
            await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1).AddMinutes(10)), token), token))[MemberSeats.TurnField]);
        Assert.Equal(["events:errors.seatAlreadyYours"], (await RefusedAsync(
            await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token), token))[MemberSeats.TurnField]);
        Assert.Equal(["events:errors.seatOverlaps"], (await RefusedAsync(
            await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(tower, starts.AddHours(1)), token), token))[MemberSeats.TurnField]);
        Assert.Equal(["events:errors.seatFull"], (await RefusedAsync(
            await other.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token), token))[MemberSeats.TurnField]);

        // A turn that touches it without meeting it is theirs too: the simulator at half past ten.
        await CreatedAsync(await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1).AddMinutes(30)), token), token);

        // The page counts the free seats, never whose they are; the member reads their seats under the registration, and the mail of the
        // registration says them, as the last change left them.
        using var visitor = _factory.CreateApiClient();
        var page = await OkAsync(await visitor.GetAsync($"/api/events/public/{Slug("seats")}", token), token);
        Assert.Equal(
            [0, 0, 1, 1],
            page.GetProperty("activities").EnumerateArray().First().GetProperty("turns").EnumerateArray().Select(turn => turn.GetProperty("free").GetInt32()));
        Assert.DoesNotContain(MemberVid.ToString(CultureInfo.InvariantCulture), page.GetRawText(), StringComparison.Ordinal);

        var mine = Assert.Single((await OkAsync(await member.GetAsync(RegistrationEndpoints.MinePattern, token), token)).EnumerateArray(), row => row.GetProperty("eventId").GetInt64() == id);
        var seats = mine.GetProperty("seats").EnumerateArray().ToList();
        Assert.Equal([starts.AddHours(1), starts.AddHours(1).AddMinutes(30)], seats.Select(row => row.GetProperty("startsAtUtc").GetDateTime()));

        var mails = await MailsAsync(MemberVid, EventsNotifications.RegistrationReceived, token);
        Assert.Equal(3, mails.Count);
        Assert.Contains("evt-test-e17 Simulator", mails[^1].DataJson, StringComparison.Ordinal);
        Assert.Contains(starts.AddHours(1).AddMinutes(30).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), mails[^1].DataJson, StringComparison.Ordinal);

        // The staff read the seats, with the members named.
        var list = (await OkAsync(await coordinator.GetAsync($"{RegistrationEndpoints.SeatsPattern}?filter[eventId]={id}", token), token))
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([MemberVid, MemberVid], list.Select(row => row.GetProperty("member").GetProperty("vid").GetInt32()));
        Assert.Equal("Test Activities", list[0].GetProperty("member").GetProperty("name").GetString());

        // Another member gives back nothing of theirs; they give back the first seat, and the turn is free for the other.
        var first = seats[0].GetProperty("id").GetInt64();
        using (var response = await other.DeleteAsync($"{SeatsPattern}/{first}", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (var response = await member.DeleteAsync($"{SeatsPattern}/{first}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await CreatedAsync(await other.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token), token);
        Assert.Equal(4, (await MailsAsync(MemberVid, EventsNotifications.RegistrationReceived, token)).Count);
    }

    [Fact]
    public async Task TheLastSeatOfATurnTakenByTwoMembersInTheSameInstantGoesToOne()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Today.AddDays(5).AddHours(9);
        var id = await EventRowAsync("same-instant", starts, starts.AddHours(6), token);
        var activity = await ActivityRowAsync(id, starts.AddHours(1), starts.AddHours(2), seats: 1, token);
        await RegistrationRowAsync(id, MemberVid, token);
        await RegistrationRowAsync(id, OtherVid, token);
        using var member = await SignedInAsync(MemberVid, token);

        // A transaction of the test does what the other member's request does: it locks the activity's row, counts, and takes the last
        // seat — not committed yet.
        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await database.Database.SqlQuery<long>($"SELECT id AS `Value` FROM evt_activities WHERE id = {activity} FOR UPDATE").ToListAsync(token);
            database.Seats.Add(Seat(id, activity, OtherVid, starts.AddHours(1)));
            await database.SaveChangesAsync(token);

            // The request takes the member's registration, then waits on the activity's row the test holds.
            var request = member.PostAsJsonAsync(SeatsPattern, new SeatRequest(activity, starts.AddHours(1)), token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        // It counted the seat committed under the lock: the turn is full, and the seat is the other's alone.
        Assert.Equal(["events:errors.seatFull"], (await RefusedAsync(answered, token))[MemberSeats.TurnField]);
        Assert.Equal([OtherVid], (await SeatsAsync(id, token)).Select(seat => seat.Vid));
    }

    [Fact]
    public async Task TheSeatsGoWithTheRegistrationAndAnActivityWithSeatsStays()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, starts) = await InPersonEventAsync(coordinator, "with-seats", token);
        var simulator = await ActivityAsync(coordinator, id, "Simulator", starts.AddHours(1), starts.AddHours(3), 30, seats: 2, token);
        using var member = await SignedInAsync(MemberVid, token);
        using var other = await SignedInAsync(OtherVid, token);
        using var third = await SignedInAsync(ThirdVid, token);

        foreach (var client in new[] { member, other, third })
        {
            await CreatedAsync(await client.PutAsJsonAsync($"{RegistrationEndpoints.MinePattern}/{id}", new RegistrationRequest(new JsonObject()), token), token);
        }

        await CreatedAsync(await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token), token);
        await CreatedAsync(await other.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(1)), token), token);
        await CreatedAsync(await third.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(2)), token), token);

        // Deleted: refused, the members hold its turns. Changed so that a turn would offer fewer seats than are taken, or a seat would
        // fall on no turn: refused on the field.
        Assert.Equal(["events:errors.activityHasSeats"], (await RefusedAsync(
            await coordinator.DeleteAsync($"{EventActivityEndpoints.Pattern}/{simulator}", token), token))["id"]);
        var read = await OkAsync(await coordinator.GetAsync($"{EventActivityEndpoints.Pattern}/{simulator}", token), token);
        var version = read.GetProperty("rowVersion").GetDateTime();
        Assert.Equal(["events:errors.activitySeatsTaken"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventActivityEndpoints.Pattern}/{simulator}", Activity(id, "Simulator", starts.AddHours(1), starts.AddHours(3), 30, 1) with { RowVersion = version }, token),
            token))["seatsPerSlot"]);
        Assert.Equal(["events:errors.activitySeatsTaken"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventActivityEndpoints.Pattern}/{simulator}", Activity(id, "Simulator", starts.AddHours(1), starts.AddHours(3), 45, 2) with { RowVersion = version }, token),
            token))["slotMinutes"]);

        // Longer, with more seats: every seat still on a turn.
        await OkAsync(await coordinator.PutAsJsonAsync(
            $"{EventActivityEndpoints.Pattern}/{simulator}",
            Activity(id, "Simulator", starts.AddHours(1), starts.AddHours(4), 30, 3) with { RowVersion = version },
            token), token);

        // The member withdraws: their seat goes with the registration. The staff take the other away: theirs too.
        using (var response = await member.DeleteAsync($"{RegistrationEndpoints.MinePattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var otherRegistration = (await OkAsync(await coordinator.GetAsync($"{RegistrationEndpoints.StaffPattern}?filter[eventId]={id}", token), token))
            .GetProperty("items").EnumerateArray().Single(row => row.GetProperty("member").GetProperty("vid").GetInt32() == OtherVid)
            .GetProperty("id").GetInt64();
        using (var response = await coordinator.PostAsJsonAsync($"{RegistrationEndpoints.StaffPattern}/{otherRegistration}/remove", new { reason = "evt-test-e17 why" }, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal([ThirdVid], (await SeatsAsync(id, token)).Select(seat => seat.Vid));

        // The list of the activities counts the one left.
        var activities = await OkAsync(await coordinator.GetAsync($"{EventActivityEndpoints.Pattern}?filter[eventId]={id}", token), token);
        Assert.Equal(1, Assert.Single(activities.GetProperty("items").EnumerateArray()).GetProperty("seatsTaken").GetInt32());
    }

    [Fact]
    public async Task DuplicateCopiesTheQuestionsAndTheActivitiesAndNothingOfTheMembers()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, starts) = await InPersonEventAsync(coordinator, "duplicate", token);
        var question = await CreatedAsync(await coordinator.PostAsJsonAsync(
            EventQuestionEndpoints.Pattern,
            new EventQuestionWriteDto(id, 1, QuestionType.Choice, Text("Seat"), [new QuestionChoiceWriteDto(null, Text("Window")), new QuestionChoiceWriteDto(null, Text("Aisle"))], Required: true, default),
            token), token);
        await ActivityAsync(coordinator, id, "Simulator", starts.AddHours(1), starts.AddHours(3), 30, seats: 2, token);

        using (var member = await SignedInAsync(MemberVid, token))
        {
            var key = question.GetProperty("choices")[1].GetProperty("key").GetString()!;
            await CreatedAsync(await member.PutAsJsonAsync(
                $"{RegistrationEndpoints.MinePattern}/{id}",
                new RegistrationRequest(new JsonObject { [EventQuestion.AnswerKey(question.GetProperty("id").GetInt64())] = key }),
                token), token);
        }

        // A week later: the question with its choices and their keys, the activity with its window moved, and no registration, no seat.
        var copy = (await CreatedAsync(await coordinator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}/duplicate",
                new EventDuplicateRequest(starts.AddDays(7), Slug("duplicate-copy"), PublicSlots: false),
                token), token))
            .GetProperty("event").GetProperty("id").GetInt64();

        var copied = Assert.Single((await OkAsync(await coordinator.GetAsync($"{EventQuestionEndpoints.Pattern}?filter[eventId]={copy}", token), token)).GetProperty("items").EnumerateArray());
        Assert.Equal(
            question.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("key").GetString()),
            copied.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("key").GetString()));
        Assert.True(copied.GetProperty("required").GetBoolean());

        var activity = Assert.Single((await OkAsync(await coordinator.GetAsync($"{EventActivityEndpoints.Pattern}?filter[eventId]={copy}", token), token)).GetProperty("items").EnumerateArray());
        Assert.Equal(starts.AddDays(7).AddHours(1), activity.GetProperty("fromUtc").GetDateTime());
        Assert.Equal(2, activity.GetProperty("seatsPerSlot").GetInt32());
        Assert.Empty(await RegistrationsAsync(copy, token));
        Assert.Empty(await SeatsAsync(copy, token));
    }

    [Fact]
    public async Task TheRetentionKeepsTheSumsAndDeletesTheRegistrationsAndTheSeats()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Today.AddHours(DateTime.UtcNow.Hour);

        // An event in person that ended four months ago, past the three of the setting; and one that ended a month ago, within them.
        var old = await EventRowAsync("retired", now.AddMonths(-4), now.AddMonths(-4).AddHours(6), token);
        var recent = await EventRowAsync("recent", now.AddMonths(-1), now.AddMonths(-1).AddHours(6), token);
        var dinner = await YesNoRowAsync(old, token);
        var activity = await ActivityRowAsync(old, now.AddMonths(-4).AddHours(1), now.AddMonths(-4).AddHours(2), seats: 3, token);
        await RegistrationRowAsync(old, MemberVid, token, $$"""{"q{{dinner}}":true}""");
        await RegistrationRowAsync(old, OtherVid, token, $$"""{"q{{dinner}}":false}""");
        await RegistrationRowAsync(old, ThirdVid, token, $$"""{"q{{dinner}}":true}""");
        await SeatRowAsync(old, activity, MemberVid, now.AddMonths(-4).AddHours(1), token);
        await SeatRowAsync(old, activity, OtherVid, now.AddMonths(-4).AddHours(1).AddMinutes(30), token);
        await RegistrationRowAsync(recent, MemberVid, token);

        // Statistics written after the event, before the run (E13a will): past the date, the registrations still there are what the lists
        // read, not the statistics (the review of #271, point 1).
        await StatsRowAsync(old, token);
        using (var staff = await SignedInAsync(CoordinatorVid, token))
        {
            var live = Assert.Single((await OkAsync(await staff.GetAsync($"{EventQuestionEndpoints.Pattern}?filter[eventId]={old}", token), token)).GetProperty("items").EnumerateArray());
            Assert.Equal(2, live.GetProperty("sum").GetProperty("yes").GetInt32());
        }

        // At least this one: the shared database may hold another class's event past its retention.
        var outcome = await RunRetentionAsync(token);
        Assert.True(outcome is { Events: >= 1, Registrations: >= 3, Seats: >= 2 }, $"{outcome}");

        // The registrations and the seats are gone; the statistics keep how many registered, the sums, the seats by activity.
        Assert.Empty(await RegistrationsAsync(old, token));
        Assert.Empty(await SeatsAsync(old, token));
        Assert.Single(await RegistrationsAsync(recent, token));
        var stats = await StatsAsync(old, token);
        Assert.Equal(3, stats.Registrations);
        Assert.Equal(2, InPersonRetention.Seats(stats)[activity]);
        var sum = InPersonRetention.Sums(stats)[dinner];
        Assert.Equal((3, 2, 1), (sum.Answered, sum.Yes, sum.No));

        // The staff's lists read them back from the statistics.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var question = Assert.Single((await OkAsync(await coordinator.GetAsync($"{EventQuestionEndpoints.Pattern}?filter[eventId]={old}", token), token)).GetProperty("items").EnumerateArray());
        Assert.Equal(2, question.GetProperty("sum").GetProperty("yes").GetInt32());
        var row = Assert.Single((await OkAsync(await coordinator.GetAsync($"{EventActivityEndpoints.Pattern}?filter[eventId]={old}", token), token)).GetProperty("items").EnumerateArray());
        Assert.Equal(2, row.GetProperty("seatsTaken").GetInt32());

        // Run again: nothing left to do; the run says so in the log of the jobs, finished.
        Assert.Equal(0, (await RunRetentionAsync(token)).Events);
        await using var scope = _factory.Services.CreateAsyncScope();
        var log = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .Where(entry => entry.Job == EventsRetentionJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(token);
        Assert.Equal("succeeded", log.Status);
        Assert.NotNull(log.FinishedAt);
    }

    [Fact]
    public async Task ASeatIsTakenAndGivenBackOnlyBeforeTheStartAndGivenBackFromACancelledEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Today.AddHours(DateTime.UtcNow.Hour);

        // Started an hour ago: no seat taken, none given back (the review of #271, point 8).
        var started = await EventRowAsync("seats-started", now.AddHours(-1), now.AddHours(5), token);
        var during = await ActivityRowAsync(started, now.AddHours(1), now.AddHours(2), seats: 2, token);
        await RegistrationRowAsync(started, MemberVid, token);
        await SeatRowAsync(started, during, MemberVid, now.AddHours(1), token);
        var held = Assert.Single(await SeatsAsync(started, token)).Id;

        using var member = await SignedInAsync(MemberVid, token);
        Assert.Equal([RegistrationRules.ClosedKey], (await RefusedAsync(
            await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(during, now.AddHours(1).AddMinutes(30)), token), token))[MemberSeats.TurnField]);
        Assert.Equal([RegistrationRules.ClosedKey], (await RefusedAsync(await member.DeleteAsync($"{SeatsPattern}/{held}", token), token))["id"]);
        Assert.Single(await SeatsAsync(started, token));

        // Cancelled, still to start: no seat taken, and the one held is given back, as a registration is withdrawn.
        var cancelled = await EventRowAsync(
            "seats-cancelled",
            now.AddDays(5),
            now.AddDays(5).AddHours(6),
            token,
            row => (row.CancelledAt, row.CancelledBy, row.CancellationNote) = (now, CoordinatorVid, Text("evt-test-e17 cancelled")));
        var later = await ActivityRowAsync(cancelled, now.AddDays(5).AddHours(1), now.AddDays(5).AddHours(2), seats: 2, token);
        await RegistrationRowAsync(cancelled, MemberVid, token);
        await SeatRowAsync(cancelled, later, MemberVid, now.AddDays(5).AddHours(1), token);

        Assert.Equal([RegistrationRules.ClosedKey], (await RefusedAsync(
            await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(later, now.AddDays(5).AddHours(1).AddMinutes(30)), token), token))[MemberSeats.TurnField]);
        using (var response = await member.DeleteAsync($"{SeatsPattern}/{Assert.Single(await SeatsAsync(cancelled, token)).Id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Empty(await SeatsAsync(cancelled, token));
    }

    [Fact]
    public async Task ASeatGivenBackWhileItsMemberWithdrawsIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Today.AddDays(5).AddHours(9);
        var id = await EventRowAsync("give-back-race", starts, starts.AddHours(6), token);
        var activity = await ActivityRowAsync(id, starts.AddHours(1), starts.AddHours(2), seats: 1, token);
        await RegistrationRowAsync(id, MemberVid, token);
        await SeatRowAsync(id, activity, MemberVid, starts.AddHours(1), token);
        var seat = Assert.Single(await SeatsAsync(id, token)).Id;
        using var member = await SignedInAsync(MemberVid, token);

        // A transaction of the test does what a withdrawal from another window does: it takes the registration's lock first.
        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await database.Database.SqlQuery<long>($"SELECT id AS `Value` FROM evt_registrations WHERE event_id = {id} AND vid = {MemberVid} FOR UPDATE").ToListAsync(token);

            // The give-back waits on that lock (the review of #271, point 2) — a give-back that took no lock would not wait, and would delete
            // the seat under the withdrawal's feet. Then the withdrawal deletes the seat and the registration, and commits.
            var request = member.DeleteAsync($"{SeatsPattern}/{seat}", token);
            await WaitForALockWaitAsync(token);
            await database.Seats.IgnoreQueryFilters().Where(row => row.EventId == id).ExecuteDeleteAsync(token);
            await database.Registrations.IgnoreQueryFilters().Where(row => row.EventId == id).ExecuteDeleteAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        // Withdrawn meanwhile: nothing left to give back — never a 500.
        using (answered)
        {
            Assert.Equal(HttpStatusCode.NotFound, answered.StatusCode);
        }
    }

    [Fact]
    public async Task AWithdrawalWhileASeatIsGivenBackPastTheLockIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Today.AddDays(5).AddHours(9);
        var id = await EventRowAsync("withdraw-race", starts, starts.AddHours(6), token);
        var activity = await ActivityRowAsync(id, starts.AddHours(1), starts.AddHours(2), seats: 1, token);
        await RegistrationRowAsync(id, MemberVid, token);
        await SeatRowAsync(id, activity, MemberVid, starts.AddHours(1), token);
        using var member = await SignedInAsync(MemberVid, token);

        // A transaction of the test deletes the seat without the registration's lock — not committed yet — as a write that went past it would.
        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await database.Seats.IgnoreQueryFilters().Where(row => row.EventId == id).ExecuteDeleteAsync(token);

            // The withdrawal reads the seat still committed, and waits on its row to delete it.
            var request = member.DeleteAsync($"{RegistrationEndpoints.MinePattern}/{id}", token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        // Its delete found the seat gone: «nothing left to withdraw», never a 500 (the review of #271, point 2, the catch `main` had).
        using (answered)
        {
            Assert.Equal(HttpStatusCode.NotFound, answered.StatusCode);
        }
    }

    [Fact]
    public async Task AnActivityDeletedWhileASeatIsTakenStaysWithItsRefusal()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Today.AddDays(5).AddHours(9);
        var id = await EventRowAsync("delete-race", starts, starts.AddHours(6), token);
        var activity = await ActivityRowAsync(id, starts.AddHours(1), starts.AddHours(2), seats: 1, token);
        await RegistrationRowAsync(id, MemberVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // A seat taken in the same instant — not committed yet — holds the activity's row through its key.
        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            database.Seats.Add(Seat(id, activity, MemberVid, starts.AddHours(1)));
            await database.SaveChangesAsync(token);

            // The delete reads no seat, and waits on the activity's row (the review of #271, point 3).
            var request = coordinator.DeleteAsync($"{EventActivityEndpoints.Pattern}/{activity}", token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        // The key refused it, and the refusal is the module's: the activity stays with its seat.
        Assert.Equal(["events:errors.activityHasSeats"], (await RefusedAsync(answered, token))["id"]);
        Assert.Single(await SeatsAsync(id, token));
    }

    /// <summary>
    /// A change of an activity while a seat is taken in it (#322, item 4): the seat holds the activity's row, as a member taking it does, and the
    /// change — the window cut to its first turn, the seat on the second — waits for it and reads it, so it is refused; never an activity
    /// whose seat falls on no turn of it.
    /// </summary>
    [Fact]
    public async Task AnActivityChangedWhileASeatIsTakenReadsTheSeat()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Today.AddDays(5).AddHours(9);
        var id = await EventRowAsync("change-race", starts, starts.AddHours(6), token);
        var activity = await ActivityRowAsync(id, starts.AddHours(1), starts.AddHours(2), seats: 1, token);
        await RegistrationRowAsync(id, MemberVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var version = (await OkAsync(await coordinator.GetAsync($"{EventActivityEndpoints.Pattern}/{activity}", token), token)).GetProperty("rowVersion").GetDateTime();

        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            // The seat on the second turn, taken as the member's verb takes it: under the activity's row, not committed yet.
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await database.Database.SqlQuery<long>($"SELECT id AS `Value` FROM evt_activities WHERE id = {activity} FOR UPDATE").ToListAsync(token);
            database.Seats.Add(Seat(id, activity, MemberVid, starts.AddHours(1).AddMinutes(30)));
            await database.SaveChangesAsync(token);

            var request = coordinator.PutAsJsonAsync(
                $"{EventActivityEndpoints.Pattern}/{activity}",
                Activity(id, "activity", starts.AddHours(1), starts.AddHours(1).AddMinutes(30), 30, 1) with { RowVersion = version },
                token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        Assert.Equal(["events:errors.activitySeatsTaken"], (await RefusedAsync(answered, token))["toUtc"]);
        Assert.Single(await SeatsAsync(id, token));
    }

    [Fact]
    public async Task TheEventIsNotMovedNorShortenedPastItsActivitiesNorTheActivityPastItsSeats()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (id, starts) = await InPersonEventAsync(coordinator, "move", token);
        var simulator = await ActivityAsync(coordinator, id, "Simulator", starts.AddHours(1), starts.AddHours(3), 30, seats: 1, token);

        // The event moved two hours later, or ending at noon: the activity from ten to noon would be outside it (the review of #271, point 4).
        var row = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        var version = row.GetProperty("rowVersion").GetDateTime();
        Assert.Equal(["events:errors.eventActivitiesOutside"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", Payload("move", starts.AddHours(2), inPerson: true) with { RowVersion = version }, token),
            token))["startsAtUtc"]);
        Assert.Equal(["events:errors.eventActivitiesOutside"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}",
                Payload("move", starts, inPerson: true) with { EndsAtUtc = starts.AddHours(2), RowVersion = version },
                token),
            token))["endsAtUtc"]);

        // A seat at half past eleven, the last turn: the window cut at eleven, or started at noon, leaves it off.
        using var member = await SignedInAsync(MemberVid, token);
        await CreatedAsync(await member.PutAsJsonAsync($"{RegistrationEndpoints.MinePattern}/{id}", new RegistrationRequest(new JsonObject()), token), token);
        await CreatedAsync(await member.PostAsJsonAsync(SeatsPattern, new SeatRequest(simulator, starts.AddHours(2).AddMinutes(30)), token), token);
        var read = await OkAsync(await coordinator.GetAsync($"{EventActivityEndpoints.Pattern}/{simulator}", token), token);
        var activityVersion = read.GetProperty("rowVersion").GetDateTime();
        Assert.Equal(["events:errors.activitySeatsTaken"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventActivityEndpoints.Pattern}/{simulator}",
                Activity(id, "Simulator", starts.AddHours(1), starts.AddHours(2), 30, 1) with { RowVersion = activityVersion },
                token),
            token))["toUtc"]);
        Assert.Equal(["events:errors.activitySeatsTaken"], (await RefusedAsync(
            await coordinator.PutAsJsonAsync(
                $"{EventActivityEndpoints.Pattern}/{simulator}",
                Activity(id, "Simulator", starts.AddHours(3), starts.AddHours(4), 30, 1) with { RowVersion = activityVersion },
                token),
            token))["fromUtc"]);
    }

    [Fact]
    public async Task ARegistrationTheRetentionDeletesLeavesNoAnswerInTheAudit()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Today.AddHours(DateTime.UtcNow.Hour);

        // A registration of an event in person, its answer written and then changed — a created copy and both sides of an updated one
        // name it in the audit —; the event then ends past its retention.
        var id = await EventRowAsync("retired-audit", now.AddDays(5), now.AddDays(5).AddHours(6), token);
        var note = await TextRowAsync(id, token);
        using (var member = await SignedInAsync(MemberVid, token))
        {
            await CreatedAsync(await member.PutAsJsonAsync($"{RegistrationEndpoints.MinePattern}/{id}", Answer(note, RetiredWords), token), token);
            await OkAsync(await member.PutAsJsonAsync($"{RegistrationEndpoints.MinePattern}/{id}", Answer(note, $"{RetiredWords} again"), token), token);
        }

        await MoveEventAsync(id, now.AddMonths(-4), now.AddMonths(-4).AddHours(6), token);
        var registered = Assert.Single(await RegistrationsAsync(id, token)).Id;
        Assert.True(await AuditMentionsAsync(RetiredWords, token), "Without the answers in the audit, the check below would prove nothing.");

        // Retired: the registration is gone, and its copies in the audit keep that it was — created, changed, deleted, by whom — without the
        // answers (E10s, Carmine's answer 3 on issue #267).
        await RunRetentionAsync(token);
        Assert.Empty(await RegistrationsAsync(id, token));
        Assert.False(await AuditMentionsAsync(RetiredWords, token));
        Assert.Equal(["created", "updated", "deleted"], await AuditActionsAsync("evt_registrations", registered, token));
    }

    [Fact]
    public async Task ErasingAPersonDeletesTheirSeatsToComeAndKeepsTheOnesOver()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Today.AddHours(DateTime.UtcNow.Hour);
        var toCome = await EventRowAsync("erase-to-come", now.AddDays(5), now.AddDays(5).AddHours(6), token);
        var over = await EventRowAsync("erase-over", now.AddDays(-2), now.AddDays(-2).AddHours(6), token);
        var later = await ActivityRowAsync(toCome, now.AddDays(5).AddHours(1), now.AddDays(5).AddHours(2), seats: 1, token);
        var done = await ActivityRowAsync(over, now.AddDays(-2).AddHours(1), now.AddDays(-2).AddHours(2), seats: 1, token);
        await RegistrationRowAsync(toCome, PersonVid, token);
        await RegistrationRowAsync(over, PersonVid, token);
        await SeatRowAsync(toCome, later, PersonVid, now.AddDays(5).AddHours(1), token);
        await SeatRowAsync(over, done, PersonVid, now.AddDays(-2).AddHours(1), token);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.seatsToCome", 1, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.seatsOver", 1, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, SeatLines(await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", token), token)));

        var erased = await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token);
        Assert.Equal(lines, SeatLines(erased));

        // The seat of the event to come is gone, its turn free; the one of the event over stays, under the pseudonym.
        Assert.Empty(await SeatsAsync(toCome, token));
        Assert.Equal([erased.GetProperty("pseudonym").GetInt32()], (await SeatsAsync(over, token)).Select(seat => seat.Vid));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private const string SeatsPattern = RegistrationEndpoints.MySeatsPattern;

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>Midnight of today in UTC: a turn is an instant to the minute, and the database keeps microseconds.</summary>
    private static DateTime Today => DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>An activity as its form sends it: its name in every language, no place, its window, its turns and its seats.</summary>
    private static EventActivityWriteDto Activity(long eventId, string name, DateTime from, DateTime to, int minutes, int seats) =>
        new(eventId, Text($"evt-test-e17 {name}"), Place: null, from, to, minutes, seats, default);

    private static async Task<long> ActivityAsync(
        HttpClient coordinator,
        long eventId,
        string name,
        DateTime from,
        DateTime to,
        int minutes,
        int seats,
        CancellationToken cancellationToken) =>
        (await CreatedAsync(
            await coordinator.PostAsJsonAsync(EventActivityEndpoints.Pattern, Activity(eventId, name, from, to, minutes, seats), cancellationToken),
            cancellationToken)).GetProperty("id").GetInt64();

    /// <summary>An event in person of the class, written and published by the coordinator through the API: from 09:00 to 18:00 of a day a month away.</summary>
    private static async Task<(long Id, DateTime Starts)> InPersonEventAsync(HttpClient coordinator, string name, CancellationToken cancellationToken)
    {
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(30).AddHours(9), DateTimeKind.Utc);
        var id = (await CreatedAsync(
                await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, Payload(name, starts, inPerson: true), cancellationToken),
                cancellationToken))
            .GetProperty("id").GetInt64();

        await OkAsync(await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), cancellationToken), cancellationToken);
        return (id, starts);
    }

    /// <summary>An event of the class as its form sends it: from 09:00 to 18:00 of <paramref name="starts"/>'s day, in person or not.</summary>
    private static EventWriteDto Payload(string name, DateTime starts, bool inPerson) => new(
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
        Venue: Text("evt-test-e17 venue"));

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
            Venue = Text("evt-test-e17 venue"),
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

    /// <summary>An activity of an event, written by the installation: turns of thirty minutes.</summary>
    private async Task<long> ActivityRowAsync(long eventId, DateTime from, DateTime to, int seats, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var activity = new EventActivity
        {
            EventId = eventId,
            Name = Text("evt-test-e17 activity"),
            FromUtc = from,
            ToUtc = to,
            SlotMinutes = 30,
            SeatsPerSlot = seats,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Activities.Add(activity);
        await database.SaveChangesAsync(cancellationToken);
        return activity.Id;
    }

    /// <summary>A yes or no of an event, written by the installation.</summary>
    private async Task<long> YesNoRowAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var question = new EventQuestion
        {
            EventId = eventId,
            Ordinal = 1,
            Type = QuestionType.YesNo,
            Label = Text("evt-test-e17 dinner"),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Questions.Add(question);
        await database.SaveChangesAsync(cancellationToken);
        return question.Id;
    }

    /// <summary>A registration of a member, written by the installation.</summary>
    private async Task RegistrationRowAsync(long eventId, int vid, CancellationToken cancellationToken, string answers = "{}")
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        database.Registrations.Add(new EventRegistration
        {
            EventId = eventId,
            Vid = vid,
            AnswersJson = answers,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private static EventActivitySeat Seat(long eventId, long activityId, int vid, DateTime starts) => new()
    {
        ActivityId = activityId,
        EventId = eventId,
        Vid = vid,
        StartsAtUtc = starts,
        CreatedAt = DateTime.UtcNow,
        OwnerDepartment = Department.ED,
        OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
    };

    /// <summary>A seat of a member, written by the installation.</summary>
    private async Task SeatRowAsync(long eventId, long activityId, int vid, DateTime starts, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        database.Seats.Add(Seat(eventId, activityId, vid, starts));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<EventsRetentionJob.Outcome> RunRetentionAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsRetentionJob>().RunAsync(cancellationToken);
    }

    private async Task<EventStats> StatsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Stats.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(stats => stats.EventId == eventId, cancellationToken);
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

    /// <summary>The seats of an event as stored, read past every filter.</summary>
    private async Task<List<EventActivitySeat>> SeatsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Seats.IgnoreQueryFilters().AsNoTracking()
            .Where(seat => seat.EventId == eventId)
            .OrderBy(seat => seat.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The statistics of an event as a job after it would write them: in its care, every number zero.</summary>
    private async Task StatsRowAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        database.Stats.Add(new EventStats
        {
            EventId = eventId,
            ComputedAt = DateTime.UtcNow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A text question of an event, written by the installation.</summary>
    private async Task<long> TextRowAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var question = new EventQuestion
        {
            EventId = eventId,
            Ordinal = 1,
            Type = QuestionType.Text,
            Label = Text("evt-test-e17 anything else"),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Questions.Add(question);
        await database.SaveChangesAsync(cancellationToken);
        return question.Id;
    }

    /// <summary>One answer as the member's form sends it.</summary>
    private static RegistrationRequest Answer(long question, string text) =>
        new(new JsonObject { [EventQuestion.AnswerKey(question)] = text });

    /// <summary>An event moved to other times past every rule: one that has ended could not be made any other way.</summary>
    private async Task MoveEventAsync(long id, DateTime starts, DateTime ends, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
            .Where(row => row.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.StartsAtUtc, starts).SetProperty(row => row.EndsAtUtc, ends), cancellationToken);
    }

    /// <summary>Whether a row of the audit log still copies a text with the marker in it, before or after its change.</summary>
    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    /// <summary>What the audit log says happened to one row of a table, in order.</summary>
    private async Task<List<string>> AuditActionsAsync(string table, long key, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var text = key.ToString(CultureInfo.InvariantCulture);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .Where(row => row.Entity == table && row.EntityId == text)
            .OrderBy(row => row.Id)
            .Select(row => row.Action)
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

    /// <summary>The lines of the seats in an erasure's preview or result: what, how many, and what becomes of them.</summary>
    private static (string Key, int Count, string Outcome)[] SeatLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{EventsModule.ModuleKey}:erasure.seats", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

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
        user.LastName = "Activities";
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
