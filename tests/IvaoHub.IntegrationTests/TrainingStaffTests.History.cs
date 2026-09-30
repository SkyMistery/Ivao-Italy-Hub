using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The history of a training's changes on the staff's page (M3, A13b; note <c>2026-09-30-lo-storico-di-un-training</c>, Carmine's answer
/// (a) on #197), through the real host: every step taken through the endpoints of whoever takes it — and the hub's own through its job
/// —, written into the core's audit log by the one interceptor, and read back by <see cref="TrainingHistory"/> as the page says it.
/// <para>⚠️ <b>The test of the reviewer's condition on #197</b>: the training is the first module to read the audit rows of the core,
/// and depends on their shape — the table's name and the key, the writer, the actions, the changed properties by their C# names,
/// the enums by name, the instants in UTC. When the core writes them otherwise, these tests fail, and the reading, which is all in
/// <see cref="TrainingHistory"/>, is what to look at. The rows the reading has no words for — emptied by an erasure — are
/// <c>TrainingHistoryRulesTests</c>'s (unit).</para>
/// </summary>
public sealed partial class TrainingStaffTests
{
    /// <summary>The day a test of the history began, in UTC: its dates stay the same ones should it run past midnight.</summary>
    private readonly DateTime _today = DateTime.UtcNow.Date;

    [Fact]
    public async Task TheHistorySaysEveryStepOfATrainingWithWhoAndWhenReadFromTheAuditLog()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);

        try
        {
            // The trainee is on the rung below the first pilot training: theirs to ask for.
            await SeedPersonAsync(TraineeVid, "Test", "Trainee", $"trn-test-{TraineeVid}@example.invalid", token, pilot: RungBelow(pilot).Number);
            var since = DateTime.UtcNow.AddSeconds(-1);

            // Asked for by the trainee, accepted by the advisor, given to a trainer by the coordinator, and a date proposed.
            using var trainee = await SignedInAsync(TraineeVid, token);
            var id = await RequestedAsync(trainee, pilot, theoryPassed: true, token);
            using var advisor = await SignedInAsync(AdvisorVid, token);
            await DoneAsync(await StepAsync(advisor, id, "accept", new { rowVersion = await VersionAsync(advisor, id, token) }, token), token);
            using var coordinator = await SignedInAsync(CoordinatorVid, token);
            await AssignAsync(coordinator, id, TrainerVid, token);
            using var trainer = await SignedInAsync(TrainerVid, token);
            await ProposeAsync(trainer, id, [Day(10, 18)], token);

            // Given to another trainer, who proposes two dates and takes one back: the same row of the log both times.
            await AssignAsync(coordinator, id, SecondTrainerVid, token);
            using var second = await SignedInAsync(SecondTrainerVid, token);
            var proposed = await ProposeAsync(second, id, [Day(11, 18), Day(12, 18)], token);
            await DoneAsync(await StepAsync(second, id, $"slots/{proposed[1]}/withdraw", new { rowVersion = await VersionAsync(second, id, token) }, token), token);

            // The trainee chooses the one left, and the trainer moves it by hand.
            var mine = await trainee.GetFromJsonAsync<JsonElement>(new Uri($"{RequestEndpoints.Pattern}/{id}", UriKind.Relative), token);
            using (var chosen = await trainee.PostAsJsonAsync(
                new Uri($"{RequestEndpoints.Pattern}/{id}/choose", UriKind.Relative),
                new { slotId = proposed[0], rowVersion = mine.GetProperty("rowVersion").GetDateTime() },
                token))
            {
                Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
            }

            await SetDateAsync(second, id, Day(13, 19), token);

            // The session starts: moved to a moment ago by the installation, as the tests of the sessions do — nobody dates a training in
            // the past (#149) —, which the log keeps as the hub's.
            var started = await StartedAMomentAgoAsync(id, token);

            // Rescheduled, dated by hand again, and closed by the advisor with a reason.
            await DoneAsync(await StepAsync(second, id, "reschedule", new { notes = (string?)null, rowVersion = await VersionAsync(second, id, token) }, token), token);
            await SetDateAsync(second, id, Day(14, 20), token);
            var closed = await DoneAsync(
                await StepAsync(advisor, id, "close", new { reason = "trn-test: no answer from the trainee", rowVersion = await VersionAsync(advisor, id, token) }, token),
                token);

            // Every step, the oldest first, with who took it — none for the hub —, the trainers and the dates it names.
            var history = closed.GetProperty("history").EnumerateArray().ToList();
            Assert.Equal(
                [
                    ("Requested", TraineeVid), ("Accepted", AdvisorVid), ("Assigned", CoordinatorVid), ("DatesChanged", TrainerVid),
                    ("TrainerChanged", CoordinatorVid), ("DatesChanged", SecondTrainerVid), ("DatesChanged", SecondTrainerVid),
                    ("DateChosen", TraineeVid), ("DateMoved", SecondTrainerVid), ("DateMoved", (int?)null), ("Rescheduled", SecondTrainerVid),
                    ("DateSet", SecondTrainerVid), ("Closed", AdvisorVid),
                ],
                history.Select(entry => (entry.GetProperty("event").GetString(), By(entry))));

            // Named as the page names people.
            Assert.Equal("Test Advisor", history[1].GetProperty("by").GetProperty("name").GetString());
            Assert.Equal(("First Trainer", TrainerVid), Person(history[2], "trainer"));
            Assert.Equal(("First Trainer", TrainerVid), Person(history[4], "previousTrainer"));
            Assert.Equal(("Second Trainer", SecondTrainerVid), Person(history[4], "trainer"));

            // The dates: chosen, moved and moved again, the session rescheduled, and the date set by hand.
            Assert.Equal((Day(11, 18), (DateTime?)null), Dates(history[7]));
            Assert.Equal((Day(13, 19), Day(11, 18)), Dates(history[8]));
            Assert.Equal((started, Day(13, 19)), Dates(history[9]));
            Assert.Equal((started, (DateTime?)null), Dates(history[10]));
            Assert.Equal((Day(14, 20), (DateTime?)null), Dates(history[11]));

            // The reason of the closing, and nothing else a step wrote: the trainee's texts stay in the request.
            Assert.Equal("trn-test: no answer from the trainee", history[12].GetProperty("reason").GetString());
            Assert.All(history.Take(12), entry => Assert.Equal(JsonValueKind.Null, entry.GetProperty("reason").ValueKind));

            // In the order the log was written, within this test.
            var moments = history.Select(entry => entry.GetProperty("at").GetDateTime()).ToList();
            Assert.Equal(moments.Order(), moments);
            Assert.All(moments, moment => Assert.InRange(moment, since, DateTime.UtcNow));

            // Whoever reads the page reads it; the trainee's own page has no history at all.
            Assert.Equal(history.Count, (await PageAsync(coordinator, id, token)).GetProperty("history").GetArrayLength());
            Assert.DoesNotContain("\"history\"", await trainee.GetStringAsync(new Uri($"{RequestEndpoints.Pattern}/{id}", UriKind.Relative), token), StringComparison.Ordinal);
        }
        finally
        {
            await ForgetSettingsAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TheHistorySaysHowATrainingEndedAndTheHubsOwnStepsAreNobodys()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        await WriteSettingsAsync(new { conflictPolicy = "None", maxResponseDays = 1 }, token);

        try
        {
            await SeedPersonAsync(TraineeVid, "Test", "Trainee", $"trn-test-{TraineeVid}@example.invalid", token, pilot: RungBelow(pilot).Number);
            using var trainee = await SignedInAsync(TraineeVid, token);
            using var advisor = await SignedInAsync(AdvisorVid, token);
            using var trainer = await SignedInAsync(TrainerVid, token);

            // The theory not passed: the request and the hub's refusal, at the same moment.
            var theory = await RequestedAsync(trainee, pilot, theoryPassed: false, token);
            Assert.Equal([("Requested", TraineeVid), ("RejectedForTheory", (int?)null)], await EventsAsync(advisor, theory, token));

            // Refused by the staff, with the reason the trainee reads.
            var refused = await RequestedAsync(trainee, pilot, theoryPassed: true, token);
            var rejection = await DoneAsync(
                await StepAsync(advisor, refused, "reject", new { reason = "trn-test: more hours first", rowVersion = await VersionAsync(advisor, refused, token) }, token),
                token);
            Assert.Equal([("Requested", TraineeVid), ("Rejected", AdvisorVid)], await EventsAsync(advisor, refused, token));
            Assert.Equal("trn-test: more hours first", rejection.GetProperty("history")[1].GetProperty("reason").GetString());

            // Taken back by the trainee.
            var cancelled = await RequestedAsync(trainee, pilot, theoryPassed: true, token);
            var theirs = await trainee.GetFromJsonAsync<JsonElement>(new Uri($"{RequestEndpoints.Pattern}/{cancelled}", UriKind.Relative), token);
            using (var cancel = await trainee.PostAsJsonAsync(
                new Uri($"{RequestEndpoints.Pattern}/{cancelled}/cancel", UriKind.Relative),
                new { rowVersion = theirs.GetProperty("rowVersion").GetDateTime() },
                token))
            {
                Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            }

            Assert.Equal([("Requested", TraineeVid), ("Cancelled", TraineeVid)], await EventsAsync(advisor, cancelled, token));

            // Two sessions that started, written by the installation — by nobody —: one the trainee did not come to, one reported.
            var session = Whole(DateTime.UtcNow.AddHours(-2));
            var noShow = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: session);
            await DoneAsync(await StepAsync(trainer, noShow, "no-show", new { rowVersion = await VersionAsync(trainer, noShow, token) }, token), token);
            var notAttended = await PageAsync(advisor, noShow, token);
            Assert.Equal([("Requested", (int?)null), ("NoShow", TrainerVid)], Events(notAttended));
            Assert.Equal(session, notAttended.GetProperty("history")[1].GetProperty("date").GetDateTime());

            var reported = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: session);
            await DoneAsync(
                await StepAsync(
                    trainer,
                    reported,
                    "report",
                    new
                    {
                        sheet = Array.Empty<object>(),
                        generalComment = "trn-test: a good session",
                        staffComment = "trn-test: for the staff",
                        readyForMockExam = false,
                        readyForExam = false,
                        cooldownWaived = false,
                        rowVersion = await VersionAsync(trainer, reported, token),
                    },
                    token),
                token);
            var report = await PageAsync(advisor, reported, token);
            Assert.Equal([("Requested", (int?)null), ("Completed", TrainerVid)], Events(report));

            // What the report says stays in the report.
            Assert.DoesNotContain("trn-test: a good session", report.GetProperty("history").ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("trn-test: for the staff", report.GetProperty("history").ToString(), StringComparison.Ordinal);

            // The night closes a training whose trainee chose no date in time: nobody closed it, and it has no reason.
            var unanswered = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Assigned, token, trainer: TrainerVid);
            await ProposeAsync(trainer, unanswered, [Day(10, 18)], token);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                // Two days on, with one to choose: the date proposed now has waited too long.
                await scope.ServiceProvider.GetRequiredService<TrainingDates>().CloseUnansweredAsync(DateTime.UtcNow.AddDays(2), token);
            }

            var night = await PageAsync(advisor, unanswered, token);
            Assert.Equal([("Requested", (int?)null), ("DatesChanged", TrainerVid), ("Closed", (int?)null)], Events(night));
            Assert.Equal(JsonValueKind.Null, night.GetProperty("history")[2].GetProperty("reason").ValueKind);
        }
        finally
        {
            await ForgetSettingsAsync(CancellationToken.None);
        }
    }

    // ---- the history's helpers ---------------------------------------------------------------------------------------------

    /// <summary>A pilot training asked for by the trainee through their own endpoint, with their answer on the theory.</summary>
    private static async Task<long> RequestedAsync(HttpClient trainee, Rating rating, bool theoryPassed, CancellationToken cancellationToken)
    {
        using var response = await trainee.PostAsJsonAsync(
            new Uri(RequestEndpoints.Pattern, UriKind.Relative),
            new { kind = rating.Kind.ToString(), rating = rating.Number, position = (string?)null, availabilityText = "trn-test availability", notesText = (string?)null, theoryPassed },
            cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{response.StatusCode}: {text}");

        return JsonDocument.Parse(text).RootElement.GetProperty("id").GetInt64();
    }

    private static async Task<DateTime> VersionAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        (await PageAsync(client, id, cancellationToken)).GetProperty("rowVersion").GetDateTime();

    /// <summary>Dates proposed by <paramref name="client"/>, an hour each; the identifiers of the dates the training has then, the soonest first.</summary>
    private static async Task<List<long>> ProposeAsync(HttpClient client, long id, DateTime[] starts, CancellationToken cancellationToken)
    {
        var page = await DoneAsync(
            await StepAsync(
                client,
                id,
                "slots",
                new
                {
                    slots = starts.Select(start => new { startsAtUtc = start, endsAtUtc = start.AddHours(1) }).ToArray(),
                    confirmed = false,
                    rowVersion = await VersionAsync(client, id, cancellationToken),
                },
                cancellationToken),
            cancellationToken);

        return [.. page.GetProperty("slots").EnumerateArray().Select(slot => slot.GetProperty("id").GetInt64())];
    }

    /// <summary>The date set by hand by <paramref name="client"/>, at the version the page has now.</summary>
    private static async Task SetDateAsync(HttpClient client, long id, DateTime start, CancellationToken cancellationToken) =>
        await DoneAsync(
            await StepAsync(client, id, "date", new { startsAtUtc = start, confirmed = false, rowVersion = await VersionAsync(client, id, cancellationToken) }, cancellationToken),
            cancellationToken);

    /// <summary>The session started ten minutes ago, written by the installation; the moment it starts now.</summary>
    private async Task<DateTime> StartedAMomentAgoAsync(long id, CancellationToken cancellationToken)
    {
        var start = Whole(DateTime.UtcNow.AddMinutes(-10));

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = await database.Trainings.IgnoreQueryFilters().SingleAsync(row => row.Id == id, cancellationToken);
        training.ScheduledStartUtc = start;
        await database.SaveChangesAsync(cancellationToken);

        return start;
    }

    /// <summary>What the page's history says, step by step: what happened, and who did it — none for the hub.</summary>
    private static async Task<List<(string?, int?)>> EventsAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        Events(await PageAsync(client, id, cancellationToken));

    private static List<(string?, int?)> Events(JsonElement page) =>
        [.. page.GetProperty("history").EnumerateArray().Select(entry => (entry.GetProperty("event").GetString(), By(entry)))];

    private static int? By(JsonElement entry) =>
        entry.GetProperty("by").ValueKind == JsonValueKind.Null ? null : entry.GetProperty("by").GetProperty("vid").GetInt32();

    private static (string?, int) Person(JsonElement entry, string property) =>
        (entry.GetProperty(property).GetProperty("name").GetString(), entry.GetProperty(property).GetProperty("vid").GetInt32());

    /// <summary>The date a line names, and the one before it.</summary>
    private static (DateTime?, DateTime?) Dates(JsonElement entry) =>
        (Moment(entry.GetProperty("date")), Moment(entry.GetProperty("previousDate")));

    private static DateTime? Moment(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDateTime();

    /// <summary>A day to come, at an hour, in UTC: a date the tests propose and set, counted from the day the test began.</summary>
    private DateTime Day(int days, int hour) => _today.AddDays(days).AddHours(hour);

    /// <summary>An instant to the second: what the database keeps of it is what the log says of it.</summary>
    private static DateTime Whole(DateTime moment) =>
        new(moment.Ticks - (moment.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
}
