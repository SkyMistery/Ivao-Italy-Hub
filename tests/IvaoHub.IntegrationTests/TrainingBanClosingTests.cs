using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A ban closes what is open (issue #312, Carmine's answer; note <c>2026-11-01-il-ban-chiude-cio-che-e-aperto</c>), through the real host:
/// giving it closes every training of the member still open — a request waiting, one accepted, one with its trainer and its dates
/// proposed, one dated — in the ban's own save, by whoever gives it and with no reason; the dates proposed go, a dated session leaves the
/// calendar, its trainer is told by mail; a session that has started waits for its trainer, and rescheduled under the ban it closes; when
/// the ban is lifted nothing reopens. <c>Training.Ban</c> alone gives and lifts a ban, and nothing else does. And the two writes that can
/// meet a ban: a step on a training it closes, and a request sent while it is given.
/// <para>⚠️ The trainees and the trainer have a mailbox, for the mails of the ban; the staff who bans and the one who edits have none
/// (<c>CONTRIBUTING.md</c>, "Tests").</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingBanClosingTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range of the fix of #312 in the shared database: 790140–790159.
    private const int TraineeVid = 790140;
    private const int OtherTraineeVid = 790141;
    private const int StartedTraineeVid = 790142;
    private const int LateTraineeVid = 790143;
    private const int BannerVid = 790144;
    private const int EditorVid = 790145;
    private const int TrainerVid = 790146;

    private static readonly int[] Vids = [TraineeVid, OtherTraineeVid, StartedTraineeVid, LateTraineeVid, BannerVid, EditorVid, TrainerVid];

    private const string GrantReason = "trn-test-312";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, bans, grants and mails.
        await CleanAsync(token);

        // The trainees may ask for a training on either ladder: their ratings are the ones below the first trained.
        var (atc, pilot) = Trained();
        foreach (var vid in new[] { TraineeVid, OtherTraineeVid, StartedTraineeVid, LateTraineeVid })
        {
            await SeedPersonAsync(vid, "Test", $"Trainee {vid}", $"trn-test-{vid}@example.invalid", token, atc: RungBelow(atc).Number, pilot: RungBelow(pilot).Number);
        }

        // Whoever bans holds the ban and the view, not Edit (the second point of #312); whoever edits holds everything but the ban.
        await SeedPersonAsync(BannerVid, "Test", "Banner", email: null, token, isStaff: true);
        await GrantAsync(BannerVid, TrainingPermissions.View, token);
        await GrantAsync(BannerVid, TrainingPermissions.Ban, token);
        await SeedPersonAsync(EditorVid, "Test", "Editor", email: null, token, isStaff: true);
        await GrantAsync(EditorVid, TrainingPermissions.View, token);
        await GrantAsync(EditorVid, TrainingPermissions.Edit, token);
        await GrantAsync(EditorVid, TrainingPermissions.Approve, token);

        // The trainer conducts the trainings assigned to them, and has a mailbox for the mail of a training a ban closed.
        await SeedPersonAsync(TrainerVid, "Test", "Trainer", $"trn-test-{TrainerVid}@example.invalid", token, isStaff: true);
        await GrantAsync(TrainerVid, TrainingPermissions.View, token);
        await GrantAsync(TrainerVid, TrainingPermissions.Conduct, token);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The answer of #312, through the API: two members with every open state between them are banned by whoever holds only
    /// <c>Training.Ban</c>; each training closes, by them, with no reason and marked as the ban's; the dates proposed go and the dated
    /// session leaves the calendar; the trainer is told, with the date that is now free; each page says it; the trainee may not choose a
    /// date any more. The ban lifted, nothing reopens, and the member asks again.
    /// </summary>
    [Fact]
    public async Task ABanClosesEveryOpenTrainingOfTheMemberTellsTheTrainerAndFreesTheDate()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();
        var start = DateTime.UtcNow.AddDays(3);

        // One member with a request waiting and a training with its trainer and a date proposed; another with one accepted and one dated.
        var requested = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        var assigned = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: TrainerVid);
        var slot = await AddSlotAsync(assigned, DateTime.UtcNow.AddDays(5), token);
        var accepted = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Accepted, token);
        var scheduled = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Scheduled, token, trainer: TrainerVid, start: start);
        Assert.Single(await CalendarOfAsync(scheduled, token));

        using var banner = await SignedInAsync(BannerVid, token);
        var given = await ReadAsync(await BanAsync(banner, TraineeVid, token), HttpStatusCode.Created, token);
        await ReadAsync(await BanAsync(banner, OtherTraineeVid, token), HttpStatusCode.Created, token);

        // Every one closed by the ban, by whoever gave it, with no reason of its own; its date kept on record.
        foreach (var id in new[] { requested, assigned, accepted, scheduled })
        {
            var stored = await StoredAsync(id, token);
            Assert.Equal((TrainingState.Closed, true, (int?)BannerVid, (string?)null), (stored.State, stored.ClosedByBan, stored.ClosedBy, stored.CloseReason));
            Assert.NotNull(stored.ClosedAt);
        }

        Assert.Equal(start, (await StoredAsync(scheduled, token)).ScheduledStartUtc!.Value, TimeSpan.FromSeconds(1));

        // The dates proposed are gone, and the session dated is out of the calendar: the trainer's day is free.
        Assert.Equal(0, await SlotsOfAsync(assigned, token));
        Assert.Empty(await CalendarOfAsync(scheduled, token));

        // The trainer is told of each training of theirs, the date of the dated one in it; nobody is told of the two without a trainer.
        var mails = await MailsAsync(TrainerVid, TrainingNotifications.ClosedByBan, token);
        Assert.Equal(2, mails.Count);
        Assert.Contains(mails, mail => mail.Contains(TrainingMail.Moment(start), StringComparison.Ordinal) && mail.Contains(TrainingMail.StaffPath(scheduled), StringComparison.Ordinal));
        Assert.Contains(mails, mail => mail.Contains(TrainingMail.StaffPath(assigned), StringComparison.Ordinal) && !mail.Contains(" UTC", StringComparison.Ordinal));
        // Each member's mail says their trainings are closed.
        Assert.NotEmpty(Closed(Assert.Single(await MailsAsync(TraineeVid, TrainingNotifications.Banned, token))));
        Assert.NotEmpty(Closed(Assert.Single(await MailsAsync(OtherTraineeVid, TrainingNotifications.Banned, token))));

        // The staff's page says the ban closed it, and who gave it; so does its history.
        var page = await banner.GetFromJsonAsync<JsonElement>(new Uri($"{StaffEndpoints.Pattern}/{scheduled}", UriKind.Relative), token);
        Assert.True(page.GetProperty("closedByBan").GetBoolean());
        Assert.Equal(BannerVid, page.GetProperty("closedBy").GetProperty("vid").GetInt32());
        var last = page.GetProperty("history").EnumerateArray().Last();
        Assert.Equal((nameof(TrainingHistoryEvent.ClosedByBan), BannerVid), (last.GetProperty("event").GetString(), last.GetProperty("by").GetProperty("vid").GetInt32()));

        // The trainee's page says so too, and the date proposed is no longer theirs to choose.
        using var trainee = await SignedInAsync(TraineeVid, token);
        var mine = await trainee.GetFromJsonAsync<JsonElement>(new Uri($"{RequestEndpoints.Pattern}/{assigned}", UriKind.Relative), token);
        Assert.True(mine.GetProperty("closedByBan").GetBoolean());
        using (var chosen = await trainee.PostAsJsonAsync(Choose(assigned), new { slotId = slot, rowVersion = mine.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(chosen, "state", TrainingDates.NotChoosable, token);
        }

        // Lifted with Training.Ban alone: nothing reopens, and the member asks again.
        await ReadAsync(await LiftAsync(banner, given.GetProperty("id").GetInt64(), given.GetProperty("rowVersion").GetDateTime(), token), HttpStatusCode.OK, token);
        foreach (var id in new[] { requested, assigned })
        {
            Assert.Equal(TrainingState.Closed, (await StoredAsync(id, token)).State);
        }

        var asked = await ReadAsync(await RequestPilotAsync(trainee, pilot, token), HttpStatusCode.Created, token);
        Assert.Equal(nameof(TrainingState.Requested), asked.GetProperty("state").GetString());
    }

    /// <summary>
    /// A session that has started is recorded, never closed over (the maintainer's answer on #149): the ban leaves it to its trainer,
    /// and tells them nothing. Rescheduled while the ban holds, it does not go back to its dates — a banned member gets none —: it closes,
    /// by whoever gave the ban.
    /// </summary>
    [Fact]
    public async Task ASessionThatHasStartedWaitsForItsTrainerAndRescheduledUnderTheBanItCloses()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(StartedTraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: TrainerVid, start: DateTime.UtcNow.AddMinutes(-10));

        using var banner = await SignedInAsync(BannerVid, token);
        await ReadAsync(await BanAsync(banner, StartedTraineeVid, token), HttpStatusCode.Created, token);
        Assert.Equal(TrainingState.Scheduled, (await StoredAsync(id, token)).State);
        Assert.Empty(await MailsAsync(TrainerVid, TrainingNotifications.ClosedByBan, token));

        // Nothing closed: the member's mail says nothing of closed trainings.
        Assert.Empty(Closed(Assert.Single(await MailsAsync(StartedTraineeVid, TrainingNotifications.Banned, token))));

        using var trainer = await SignedInAsync(TrainerVid, token);
        var version = (await StoredAsync(id, token)).RowVersion;
        var page = await ReadAsync(await StepAsync(trainer, id, "reschedule", new { notes = "trn-test: nobody around", rowVersion = version }, token), HttpStatusCode.OK, token);
        Assert.Equal(nameof(TrainingState.Closed), page.GetProperty("state").GetString());

        var stored = await StoredAsync(id, token);
        Assert.Equal((TrainingState.Closed, true, (int?)BannerVid, (DateTime?)null), (stored.State, stored.ClosedByBan, stored.ClosedBy, stored.ScheduledStartUtc));

        // The session is on record as rescheduled, then the ban's closing.
        var history = (await trainer.GetFromJsonAsync<JsonElement>(new Uri($"{StaffEndpoints.Pattern}/{id}", UriKind.Relative), token))
            .GetProperty("history").EnumerateArray().Select(line => line.GetProperty("event").GetString()).ToList();
        Assert.Equal([nameof(TrainingHistoryEvent.Rescheduled), nameof(TrainingHistoryEvent.ClosedByBan)], history.TakeLast(2));
    }

    /// <summary>
    /// <c>Training.Ban</c> and nothing else gives and lifts a ban: whoever holds <c>Training.Edit</c> and <c>Training.Approve</c> but not the
    /// ban gives none and lifts none, and the member's trainings stay as they are.
    /// </summary>
    [Fact]
    public async Task OnlyTrainingBanGivesOrLiftsABan()
    {
        var token = TestContext.Current.CancellationToken;
        var open = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Accepted, token);

        using var editor = await SignedInAsync(EditorVid, token);
        using (var refused = await BanAsync(editor, TraineeVid, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        Assert.Equal(0, await CountBansAsync(TraineeVid, token));
        Assert.Equal(TrainingState.Accepted, (await StoredAsync(open, token)).State);

        var ban = await AddBanAsync(OtherTraineeVid, token);
        using (var lift = await LiftAsync(editor, ban.Id, ban.RowVersion, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, lift.StatusCode);
        }

        // Whoever holds the ban alone lifts it.
        using var banner = await SignedInAsync(BannerVid, token);
        await ReadAsync(await LiftAsync(banner, ban.Id, ban.RowVersion, token), HttpStatusCode.OK, token);
    }

    /// <summary>
    /// <c>Training.Ban</c> lets the write guard pass a training only for the ban's closing: every step of the staff asks its own permission
    /// on the row first. Whoever holds the ban and the reading alone accepts, rejects, assigns, dates and closes nothing.
    /// </summary>
    [Fact]
    public async Task TrainingBanAloneTakesNoStepOnATraining()
    {
        var token = TestContext.Current.CancellationToken;
        var requested = await AddTrainingAsync(OtherTraineeVid, RatingKind.Atc, TrainingState.Requested, token);
        var accepted = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Accepted, token);

        using var banner = await SignedInAsync(BannerVid, token);
        foreach (var (id, verb) in new[] { (requested, "accept"), (requested, "reject"), (accepted, "assign"), (accepted, "date"), (accepted, "close") })
        {
            var stored = await StoredAsync(id, token);
            using var step = await StepAsync(banner, id, verb, new { rowVersion = stored.RowVersion, trainerVid = TrainerVid, reason = "trn-test-312" }, token);
            Assert.True(step.StatusCode == HttpStatusCode.Forbidden, $"{verb}: {step.StatusCode}");
            Assert.Equal(stored.State, (await StoredAsync(id, token)).State);
        }
    }

    /// <summary>
    /// A ban and a step on a training it closes meet on the training's version. The trainee chooses a date after the ban read the
    /// training and before it saved: the ban's save is a conflict, and nothing of it is written — no ban, the training dated. The ban
    /// saved first: the trainee's choice, read before it, finds the date proposed gone with the ban, and dates nothing.
    /// </summary>
    [Fact]
    public async Task ADateChosenWhileTheBanIsSavedMakesItAConflictAndAChoiceAfterTheBanDatesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: TrainerVid);
        var slot = await AddSlotAsync(id, DateTime.UtcNow.AddDays(5), token);
        using var trainee = await SignedInAsync(TraineeVid, token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // The ban read the training and closed it, not saved yet …
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            var ban = new TraineeBan { Vid = TraineeVid, Reason = "trn-test-312: meanwhile" };
            database.TraineeBans.Add(ban);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<TrainingBans>().BeforeGivingAsync(ban, isNew: true, token));

            // … the trainee chooses the date …
            var version = (await StoredAsync(id, token)).RowVersion;
            await ReadAsync(await trainee.PostAsJsonAsync(Choose(id), new { slotId = slot, rowVersion = version }, token), HttpStatusCode.OK, token);

            // … and the ban's save meets the version the choice moved: none of it is written.
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => database.SaveChangesAsync(token));
        }

        Assert.Equal(0, await CountBansAsync(TraineeVid, token));
        Assert.Equal(TrainingState.Scheduled, (await StoredAsync(id, token)).State);

        // The other way round, on another training: the trainee read it before the ban, which then closes it.
        var other = await AddTrainingAsync(OtherTraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: TrainerVid);
        var proposed = await AddSlotAsync(other, DateTime.UtcNow.AddDays(5), token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            var stale = await database.Trainings.IgnoreQueryFilters().SingleAsync(row => row.Id == other, token);

            using var banner = await SignedInAsync(BannerVid, token);
            await ReadAsync(await BanAsync(banner, OtherTraineeVid, token), HttpStatusCode.Created, token);

            var refused = await scope.ServiceProvider.GetRequiredService<TrainingDates>()
                .ChooseAsync(stale, new TrainingSlotChoiceDto(proposed, stale.RowVersion), token);
            Assert.NotNull(refused);
            Assert.Equal(new[] { TrainingDates.SlotUnknown }, refused["slotId"]);
        }

        var closed = await StoredAsync(other, token);
        Assert.Equal((TrainingState.Closed, true, (DateTime?)null), (closed.State, closed.ClosedByBan, closed.ScheduledStartUtc));
    }

    /// <summary>
    /// A request sent while the ban is given is a new row, which no version guards; one of the two reads after the saves sees the other.
    /// The request saved after the ban read the trainings and before it saved: the ban, once saved, reads them again and closes it. The
    /// ban saved after the request read the bans and before it saved: the request, once saved, reads them again, closes itself and says
    /// the member is banned.
    /// </summary>
    [Fact]
    public async Task ARequestSentWhileTheBanIsGivenIsClosedByIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();

        // The ban's read of the trainings comes first, and finds none; the request is saved; then the ban.
        long swept;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            var bans = scope.ServiceProvider.GetRequiredService<TrainingBans>();
            var ban = new TraineeBan { Vid = TraineeVid, Reason = "trn-test-312: meanwhile" };
            database.TraineeBans.Add(ban);
            Assert.Null(await bans.BeforeGivingAsync(ban, isNew: true, token));

            using var trainee = await SignedInAsync(TraineeVid, token);
            swept = (await ReadAsync(await RequestPilotAsync(trainee, pilot, token), HttpStatusCode.Created, token)).GetProperty("id").GetInt64();

            await database.SaveChangesAsync(token);
            await bans.AfterGivingAsync(ban, token);
        }

        // Written by the installation: nobody closed it.
        var stored = await StoredAsync(swept, token);
        Assert.Equal((TrainingState.Closed, true, (int?)null), (stored.State, stored.ClosedByBan, stored.ClosedBy));

        // The request's read of the bans comes first — the theory is asked after it —, the ban is saved while the theory is asked, then
        // the request.
        await using var late = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ITheoryExamSource>(_ => new BannedWhileAsked(_factory))));
        using var client = late.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        using (var signIn = await client.PostAsync(new Uri($"{TestSignInStartupFilter.Path}?vid={LateTraineeVid}", UriKind.Relative), content: null, token))
        {
            signIn.EnsureSuccessStatusCode();
        }

        using (var refused = await RequestPilotAsync(client, pilot, token))
        {
            await AssertRefusedAsync(refused, "kind", RequestRules.Banned, token);
        }

        var request = Assert.Single(await TrainingsOfAsync(LateTraineeVid, token));
        Assert.Equal((TrainingState.Closed, true, (int?)null), (request.State, request.ClosedByBan, request.ClosedBy));
        Assert.Empty(await MailsAsync(LateTraineeVid, TrainingNotifications.RequestReceived, token));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The theory as the trainee declares it, with a ban of theirs written by the installation while it is asked: outside the flow of
    /// the request, which would otherwise lend it the trainee as its writer.
    /// </summary>
    private sealed class BannedWhileAsked(HubWebApplicationFactory factory) : ITheoryExamSource
    {
        public bool AsksTheTrainee => true;

        public async Task<bool?> HasPassedAsync(int vid, Rating rating, bool? declared, CancellationToken cancellationToken = default)
        {
            Task written;
            using (ExecutionContext.SuppressFlow())
            {
                written = Task.Run(
                    async () =>
                    {
                        await using var scope = factory.Services.CreateAsyncScope();
                        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
                        database.TraineeBans.Add(new TraineeBan { Vid = vid, Reason = "trn-test-312: while the theory is asked" });
                        await database.SaveChangesAsync(cancellationToken);
                    },
                    cancellationToken);
            }

            await written;
            return declared;
        }
    }

    private static Task<HttpResponseMessage> BanAsync(HttpClient client, int vid, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri(BanEndpoints.Pattern, UriKind.Relative), new { vid, reason = "trn-test-312: repeated no-shows", endsAt = (DateTime?)null }, cancellationToken);

    private static Task<HttpResponseMessage> LiftAsync(HttpClient client, long id, DateTime version, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{BanEndpoints.Pattern}/{id}/lift", UriKind.Relative), new { rowVersion = version }, cancellationToken);

    private static Task<HttpResponseMessage> RequestPilotAsync(HttpClient client, Rating pilot, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            RequestEndpoints.Pattern,
            new { kind = nameof(RatingKind.Pilot), rating = pilot.Number, position = (string?)null, availabilityText = "trn-test-312", notesText = (string?)null, theoryPassed = true },
            cancellationToken);

    private static Task<HttpResponseMessage> StepAsync(HttpClient client, long id, string verb, object body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(new Uri($"{StaffEndpoints.Pattern}/{id}/{verb}", UriKind.Relative), body, cancellationToken);

    private static Uri Choose(long id) => new($"{RequestEndpoints.Pattern}/{id}/choose", UriKind.Relative);

    /// <summary>What the mail of a ban says of the trainings it closed, as its data holds it: nothing when it closed none.</summary>
    private static string Closed(string data) => JsonDocument.Parse(data).RootElement.GetProperty("closed").GetString()!;

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

    private async Task<Training> StoredAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(training => training.Id == id, cancellationToken);
    }

    private async Task<List<Training>> TrainingsOfAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
            .Where(training => training.TraineeVid == vid)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> SlotsOfAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Slots.CountAsync(slot => slot.TrainingId == id, cancellationToken);
    }

    private async Task<int> CountBansAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().TraineeBans.IgnoreQueryFilters()
            .CountAsync(ban => ban.Vid == vid, cancellationToken);
    }

    /// <summary>The entries of the calendar a training projects, whoever may read them.</summary>
    private async Task<List<CalendarEntry>> CalendarOfAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var source = Training.SourceIdOf(id);
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && entry.SourceId == source)
            .ToListAsync(cancellationToken);
    }

    /// <summary>What every mail of that type to that member says, as its data holds it, the oldest first.</summary>
    private async Task<List<string>> MailsAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(notification => notification.Vid == vid && notification.Type == type)
            .OrderBy(notification => notification.Id)
            .Select(notification => notification.DataJson)
            .ToListAsync(cancellationToken);
    }

    /// <summary>A ban until somebody lifts it, written as the installation.</summary>
    private async Task<TraineeBan> AddBanAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var ban = new TraineeBan { Vid = vid, Reason = "trn-test-312: written by the installation" };
        database.TraineeBans.Add(ban);
        await database.SaveChangesAsync(cancellationToken);
        return ban;
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
        DateTime? start = null)
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
            DecidedBy = state == TrainingState.Requested ? null : EditorVid,
            DecidedAt = state == TrainingState.Requested ? null : now,
            TrainerVid = trainer,
            AssignedBy = trainer is null ? null : EditorVid,
            AssignedAt = trainer is null ? null : now,
            ScheduledStartUtc = start,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

    /// <summary>A date proposed for a training, two hours from <paramref name="start"/>, written as the installation.</summary>
    private async Task<long> AddSlotAsync(long id, DateTime start, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var slot = new TrainingSlot { TrainingId = id, StartsAtUtc = start, EndsAtUtc = start.AddHours(2) };
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    /// <summary>The first rating of each ladder with a practical training: what the trainings of this class are for.</summary>
    private (Rating Atc, Rating Pilot) Trained()
    {
        using var scope = _factory.Services.CreateScope();
        var ratings = scope.ServiceProvider.GetRequiredService<TrainingReference>().Ratings;
        return (ratings.First(rating => rating.Kind == RatingKind.Atc), ratings.First(rating => rating.Kind == RatingKind.Pilot));
    }

    /// <summary>The rung below a rating on its ladder: the trainee's, for whom it is the next training.</summary>
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

    /// <summary>A person of this class: their name, a mailbox only when a test looks for their mail, whether the hub counts them as staff, their ratings.</summary>
    private async Task SeedPersonAsync(
        int vid,
        string firstName,
        string lastName,
        string? email,
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
        user.Email = email;
        user.IsStaff = isStaff;
        user.RatingAtc = atc;
        user.RatingPilot = pilot;
        user.HoursAtc = 500m;
        user.HoursPilot = 500m;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

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
    /// What this class leaves: the trainings of its people with their dates and sessions and their entries of the calendar, their bans,
    /// the grants of its people, their mails and their addresses — so no later class counts them.
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

        // The dates and the sessions go with their trainings (the keys cascade).
        await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await training.TraineeBans.IgnoreQueryFilters().Where(ban => Vids.Contains(ban.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
        await hub.Users.Where(user => Vids.Contains(user.Vid))
            .ExecuteUpdateAsync(user => user.SetProperty(row => row.Email, (string?)null), cancellationToken);
    }
}
