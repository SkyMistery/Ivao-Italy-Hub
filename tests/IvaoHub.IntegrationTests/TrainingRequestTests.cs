using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The request of a training (M3, A6a; design M3 §2.2), through the real host and the trainee's own endpoints: the next
/// training on each ladder, one at a time on each — the ATC one and the pilot's together, yes —; a «no» to the theory recorded
/// with no mail, a «yes» with the mail in the queue; a banned member asking for nothing; the cancellation, only while nobody
/// accepted and only by the trainee; the refusals field by field, the settings read; and nobody reading another's trainings
/// through these endpoints.
/// <para>The trainees are members, not staff: no position, no grant. The one whose mail is looked for has an address, which is
/// safe because the contacts tests count the staff of a department, and a trainee is none (<c>CONTRIBUTING.md</c>, "Tests").
/// The positions are the reference data of the night, read from the recorded answers, as in the tests of the skeleton.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingRequestTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md): 790001–790016 are A1's to A5's.
    private const int TraineeVid = 790017;
    private const int DeclarerVid = 790018;
    private const int BannedVid = 790019;
    private const int OtherVid = 790020;
    private const int CancellerVid = 790021;

    private static readonly int[] Vids = [TraineeVid, DeclarerVid, BannedVid, OtherVid, CancellerVid];

    private static readonly Uri Mine = new(RequestEndpoints.Pattern, UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunAsync(token);
        }

        // A run that failed half way leaves its trainings, and one of them would be open.
        await CleanAsync(token);

        foreach (var vid in Vids)
        {
            await SeedMemberAsync(vid, vid == DeclarerVid ? $"trn-test-{vid}@example.invalid" : null, token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ATraineeAsksForTheNextTrainingOnEachLadderOneAtATime()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();

        using var trainee = await SignedInAsync(TraineeVid, token);

        // The page: who they are, the rating after theirs on each ladder, and the positions the ATC one is trained on.
        var page = await trainee.GetFromJsonAsync<JsonElement>(Mine, token);
        Assert.Equal(TraineeVid, page.GetProperty("vid").GetInt32());
        Assert.Equal("Test Trainee", page.GetProperty("name").GetString());
        Assert.True(page.GetProperty("asksTheory").GetBoolean());
        Assert.False(page.TryGetProperty("email", out _));

        var atcPath = Path(page, RatingKind.Atc);
        Assert.Equal(atc.Number, atcPath.GetProperty("next").GetProperty("number").GetInt32());
        Assert.Equal(RungBelow(atc).ShortName, atcPath.GetProperty("ratingShortName").GetString());
        Assert.Equal(JsonValueKind.Null, atcPath.GetProperty("refusal").ValueKind);
        Assert.True(atcPath.GetProperty("asksPosition").GetBoolean());
        var positions = atcPath.GetProperty("positions").EnumerateArray().Select(position => position.GetProperty("callsign").GetString()!).ToList();
        Assert.Equal(await OfferedAsync(atc, token), positions);

        var pilotPath = Path(page, RatingKind.Pilot);
        Assert.Equal(pilot.Number, pilotPath.GetProperty("next").GetProperty("number").GetInt32());
        Assert.False(pilotPath.GetProperty("asksPosition").GetBoolean());
        Assert.Empty(pilotPath.GetProperty("positions").EnumerateArray());

        // The ATC training, on a position chosen among the ones offered.
        var chosen = await PositionAsync(atc, token);
        var asked = await RequestedAsync(trainee, Request(atc, chosen.Callsign, theoryPassed: true), token);
        Assert.Equal(nameof(TrainingState.Requested), asked.GetProperty("state").GetString());
        Assert.Equal(chosen.Callsign, asked.GetProperty("position").GetString());
        Assert.Equal(atc.ShortName, asked.GetProperty("ratingShortName").GetString());

        // The row: the trainee's, the base department's, with what the request copies — the rating and the hours as the hub
        // knew them, the airport and the FIR of the position — and the moment the theory was declared passed.
        var id = asked.GetProperty("id").GetInt64();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(training => training.Id == id, token);

            Assert.Equal(TraineeVid, stored.TraineeVid);
            Assert.Equal(TraineeVid, stored.CreatedBy);
            Assert.Equal(Department.TD, stored.OwnerDepartment);
            Assert.Equal(RungBelow(atc).Number, stored.TraineeRatingAtRequest);
            Assert.Equal(TraineeHours, stored.TraineeHoursAtRequest);
            Assert.Equal((chosen.AirportIcao, chosen.Fir), (stored.AirportIcao, stored.Fir));
            Assert.NotNull(stored.TheoryConfirmedAt);
            Assert.False(stored.IsMockExam);
            Assert.Equal("trn-test availability", stored.AvailabilityText);

            var key = id.ToString(CultureInfo.InvariantCulture);
            Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
                .AnyAsync(entry => entry.Entity == "trn_trainings" && entry.EntityId == key && entry.Vid == TraineeVid, token));
        }

        // Theirs to read, and the ladder is taken while it is open.
        var reread = await trainee.GetFromJsonAsync<JsonElement>(Mine, token);
        Assert.Contains(reread.GetProperty("trainings").EnumerateArray(), training => training.GetProperty("id").GetInt64() == id);
        Assert.Equal(RequestRules.Open, Path(reread, RatingKind.Atc).GetProperty("refusal").GetString());
        Assert.Equal(id, Path(reread, RatingKind.Atc).GetProperty("openTrainingId").GetInt64());

        using (var second = await trainee.PostAsJsonAsync(Mine, Request(atc, chosen.Callsign, theoryPassed: true), token))
        {
            await AssertRefusedAsync(second, "kind", RequestRules.Open, token);
        }

        // The other ladder is another path (d4): a pilot's training goes, with no position.
        var flown = await RequestedAsync(trainee, Request(pilot, position: null, theoryPassed: true), token);
        Assert.Equal(nameof(TrainingState.Requested), flown.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, flown.GetProperty("position").ValueKind);
    }

    [Fact]
    public async Task ANoToTheTheoryIsRecordedWithNoMailAndAYesQueuesOne()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        var chosen = await PositionAsync(atc, token);

        using var declarer = await SignedInAsync(DeclarerVid, token);

        // Not answered: nothing is written.
        using (var unanswered = await declarer.PostAsJsonAsync(Mine, Request(atc, chosen.Callsign, theoryPassed: null), token))
        {
            await AssertRefusedAsync(unanswered, "theoryPassed", "errors.required", token);
        }

        Assert.Equal(0, await TrainingsOfAsync(DeclarerVid, token));

        // «No»: refused by the hub and recorded, the screen says why, and no mail leaves.
        var refused = await RequestedAsync(declarer, Request(atc, chosen.Callsign, theoryPassed: false), token);
        Assert.Equal(nameof(TrainingState.Rejected), refused.GetProperty("state").GetString());
        Assert.Equal(nameof(TrainingRejection.TheoryNotPassed), refused.GetProperty("rejection").GetString());
        Assert.NotEqual(JsonValueKind.Null, refused.GetProperty("decidedAt").ValueKind);
        Assert.Equal(0, await MailsOfAsync(DeclarerVid, token));

        // A refusal keeps nobody waiting: «yes» right after, and the mail is in the queue.
        var asked = await RequestedAsync(declarer, Request(atc, chosen.Callsign, theoryPassed: true), token);
        Assert.Equal(nameof(TrainingState.Requested), asked.GetProperty("state").GetString());
        Assert.Equal(1, await MailsOfAsync(DeclarerVid, token));
        Assert.Equal(2, await TrainingsOfAsync(DeclarerVid, token));
    }

    [Fact]
    public async Task OnlyTheTraineeCancelsARequestAndOnlyWhileNobodyAcceptedIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();

        using var trainee = await SignedInAsync(CancellerVid, token);
        using var other = await SignedInAsync(OtherVid, token);

        var asked = await RequestedAsync(trainee, Request(pilot, position: null, theoryPassed: true), token);
        var id = asked.GetProperty("id").GetInt64();
        var cancel = new Uri($"{RequestEndpoints.Pattern}/{id}/cancel", UriKind.Relative);
        var version = asked.GetProperty("rowVersion").GetDateTime();

        // Somebody else: the request is not theirs to cancel, nor to see.
        using (var notTheirs = await other.PostAsJsonAsync(cancel, new { rowVersion = version }, token))
        {
            Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        }

        // A version the trainee did not see: somebody changed it meanwhile.
        using (var stale = await trainee.PostAsJsonAsync(cancel, new { rowVersion = version.AddSeconds(-1) }, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        using (var cancelled = await trainee.PostAsJsonAsync(cancel, new { rowVersion = version }, token))
        {
            Assert.True(cancelled.StatusCode == HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync(token));
            var body = await cancelled.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal(nameof(TrainingState.Cancelled), body.GetProperty("state").GetString());
            Assert.NotEqual(JsonValueKind.Null, body.GetProperty("closedAt").ValueKind);
            version = body.GetProperty("rowVersion").GetDateTime();
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(training => training.Id == id, token);
            Assert.Equal(CancellerVid, stored.ClosedBy);
        }

        // Cancelled is not requested: it stays as it is.
        using (var again = await trainee.PostAsJsonAsync(cancel, new { rowVersion = version }, token))
        {
            await AssertRefusedAsync(again, "state", "training:errors.requestNotCancellable", token);
        }

        // A cancellation keeps nobody waiting, and frees the ladder in the database's key too: the trainee asks again at once.
        var againAsked = await RequestedAsync(trainee, Request(pilot, position: null, theoryPassed: true), token);
        Assert.Equal(nameof(TrainingState.Requested), againAsked.GetProperty("state").GetString());

        // A request the hub refused is not cancelled either.
        var (atc, _) = Trained();
        var refused = await RequestedAsync(trainee, Request(atc, (await PositionAsync(atc, token)).Callsign, theoryPassed: false), token);
        using (var notRequested = await trainee.PostAsJsonAsync(
            new Uri($"{RequestEndpoints.Pattern}/{refused.GetProperty("id").GetInt64()}/cancel", UriKind.Relative),
            new { rowVersion = refused.GetProperty("rowVersion").GetDateTime() },
            token))
        {
            await AssertRefusedAsync(notRequested, "state", "training:errors.requestNotCancellable", token);
        }
    }

    [Fact]
    public async Task NobodyReadsAnothersTrainingsThroughTheTraineesEndpoints()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, pilot) = Trained();

        using var trainee = await SignedInAsync(TraineeVid, token);
        using var other = await SignedInAsync(OtherVid, token);

        var id = (await RequestedAsync(trainee, Request(pilot, position: null, theoryPassed: true), token)).GetProperty("id").GetInt64();

        // Their own, yes.
        using (var own = await trainee.GetAsync(new Uri($"{RequestEndpoints.Pattern}/{id}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        }

        // Another member's, never: not by its address, not in their own page.
        using (var notTheirs = await other.GetAsync(new Uri($"{RequestEndpoints.Pattern}/{id}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        }

        var theirs = await other.GetFromJsonAsync<JsonElement>(Mine, token);
        Assert.Equal(OtherVid, theirs.GetProperty("vid").GetInt32());
        Assert.DoesNotContain(theirs.GetProperty("trainings").EnumerateArray(), training => training.GetProperty("id").GetInt64() == id);

        // And nobody who has not signed in.
        using var anonymous = _factory.CreateApiClient();
        using var refused = await anonymous.GetAsync(Mine, token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task ABannedMemberAsksForNothingUntilTheBanIsLifted()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();
        var chosen = await PositionAsync(atc, token);
        var until = DateTime.UtcNow.AddDays(3);

        long banId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            var ban = new TraineeBan { Vid = BannedVid, Reason = "trn-test ban", EndsAt = until };
            database.TraineeBans.Add(ban);
            await database.SaveChangesAsync(token);
            banId = ban.Id;
        }

        using var banned = await SignedInAsync(BannedVid, token);

        // The page says it on both ladders, and until when.
        var page = await banned.GetFromJsonAsync<JsonElement>(Mine, token);
        foreach (var kind in new[] { RatingKind.Atc, RatingKind.Pilot })
        {
            var path = Path(page, kind);
            Assert.Equal(RequestRules.Banned, path.GetProperty("refusal").GetString());
            Assert.Equal(until, path.GetProperty("bannedUntil").GetDateTime(), TimeSpan.FromSeconds(1));
        }

        using (var refusedAtc = await banned.PostAsJsonAsync(Mine, Request(atc, chosen.Callsign, theoryPassed: true), token))
        {
            await AssertRefusedAsync(refusedAtc, "kind", RequestRules.Banned, token);
        }

        using (var refusedPilot = await banned.PostAsJsonAsync(Mine, Request(pilot, position: null, theoryPassed: false), token))
        {
            await AssertRefusedAsync(refusedPilot, "kind", RequestRules.Banned, token);
        }

        // Nothing written, not even the «no».
        Assert.Equal(0, await TrainingsOfAsync(BannedVid, token));

        // Lifted: the request goes.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            var ban = await database.TraineeBans.SingleAsync(row => row.Id == banId, token);
            ban.LiftedAt = DateTime.UtcNow.AddSeconds(-1);
            ban.LiftedBy = OtherVid;
            await database.SaveChangesAsync(token);
        }

        var asked = await RequestedAsync(banned, Request(pilot, position: null, theoryPassed: true), token);
        Assert.Equal(nameof(TrainingState.Requested), asked.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ARequestIsRefusedFieldByFieldWithTheSettingsOfTheDivision()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();
        var offered = await OfferedAsync(atc, token);
        Assert.True(offered.Count > 1, "The recorded answers offer more than one position to the first trained ATC rating.");
        var hidden = offered[0];
        var kept = offered[1];

        using var trainee = await SignedInAsync(OtherVid, token);

        await WriteSettingsAsync(
            new
            {
                hiddenPositions = new[] { hidden },
                minimumHours = new[] { new { kind = nameof(RatingKind.Atc), rating = atc.Number, hours = 1000 } },
            },
            token);

        try
        {
            // The page leaves the hidden position out, and shows the threshold the hours are below.
            var path = Path(await trainee.GetFromJsonAsync<JsonElement>(Mine, token), RatingKind.Atc);
            Assert.Equal(RequestRules.HoursTooFew, path.GetProperty("refusal").GetString());
            Assert.Equal(1000, path.GetProperty("minimumHours").GetInt32());
            Assert.Equal(TraineeHours, path.GetProperty("hours").GetDecimal());
            var positions = path.GetProperty("positions").EnumerateArray().Select(position => position.GetProperty("callsign").GetString()).ToList();
            Assert.DoesNotContain(hidden, positions);
            Assert.Contains(kept, positions);

            // Each refusal on its field: the trainee on the ladder, the position among the ones offered, the texts within
            // their bound — and nothing written.
            var tooLong = new string('x', Training.MaxTextLength + 1);
            using (var refused = await trainee.PostAsJsonAsync(
                Mine,
                new { kind = nameof(RatingKind.Atc), rating = atc.Number, position = hidden, availabilityText = tooLong, notesText = tooLong, theoryPassed = true },
                token))
            {
                await AssertRefusedAsync(refused, "kind", RequestRules.HoursTooFew, token);
                await AssertRefusedAsync(refused, "position", "training:errors.requestPositionUnknown", token);
                await AssertRefusedAsync(refused, "availabilityText", "errors.text.tooLong", token);
                await AssertRefusedAsync(refused, "notesText", "errors.text.tooLong", token);
            }

            // A rating that is not the one proposed, a position forgotten, and one where there is none to choose.
            using (var wrongRating = await trainee.PostAsJsonAsync(
                Mine,
                new { kind = nameof(RatingKind.Pilot), rating = pilot.Number + 1, position = kept, theoryPassed = true },
                token))
            {
                await AssertRefusedAsync(wrongRating, "rating", "training:errors.requestRatingNotNext", token);
                await AssertRefusedAsync(wrongRating, "position", "training:errors.requestPositionNotAsked", token);
            }

            using (var noPosition = await trainee.PostAsJsonAsync(Mine, Request(atc, position: null, theoryPassed: true), token))
            {
                await AssertRefusedAsync(noPosition, "position", "errors.required", token);
            }

            Assert.Equal(0, await TrainingsOfAsync(OtherVid, token));
        }
        finally
        {
            // Put back as a fresh installation has them, for whoever reads them next.
            await ForgetSettingsAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TheDatabaseKeepsOneOpenTrainingPerTraineeAndLadder()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();

        // Written as the application, past every check: what two requests sent at the same moment would do.
        await AddAsync(new Training { Kind = RatingKind.Atc, Rating = atc.Number, TraineeVid = OtherVid, State = TrainingState.Requested }, token);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
            AddAsync(new Training { Kind = RatingKind.Atc, Rating = atc.Number, TraineeVid = OtherVid, State = TrainingState.Accepted }, token));

        // The other ladder, and a training that is not open, are not held.
        await AddAsync(new Training { Kind = RatingKind.Pilot, Rating = pilot.Number, TraineeVid = OtherVid, State = TrainingState.Scheduled }, token);
        await AddAsync(new Training { Kind = RatingKind.Atc, Rating = atc.Number, TraineeVid = OtherVid, State = TrainingState.Completed }, token);
        await AddAsync(new Training { Kind = RatingKind.Atc, Rating = atc.Number, TraineeVid = OtherVid, State = TrainingState.Rejected }, token);

        Assert.Equal(4, await TrainingsOfAsync(OtherVid, token));

        async Task AddAsync(Training training, CancellationToken cancellationToken)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            database.Trainings.Add(training);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>The hours every member of this class has on both ladders.</summary>
    private const decimal TraineeHours = 500m;

    private static object Request(Rating rating, string? position, bool? theoryPassed) => new
    {
        kind = rating.Kind.ToString(),
        rating = rating.Number,
        position,
        availabilityText = "trn-test availability",
        notesText = "trn-test notes",
        theoryPassed,
    };

    /// <summary>The first rating of each ladder with a practical training: what a member on the rung below is offered.</summary>
    private (Rating Atc, Rating Pilot) Trained()
    {
        using var scope = _factory.Services.CreateScope();
        var ratings = scope.ServiceProvider.GetRequiredService<TrainingReference>().Ratings;
        return (ratings.First(rating => rating.Kind == RatingKind.Atc), ratings.First(rating => rating.Kind == RatingKind.Pilot));
    }

    /// <summary>The rung of the ladder right below a rating, as the core's vocabulary orders it.</summary>
    private Rating RungBelow(Rating rating)
    {
        using var scope = _factory.Services.CreateScope();
        var ladder = scope.ServiceProvider.GetRequiredService<RatingVocabulary>().Ladder(rating.Kind);
        return ladder[ladder.ToList().IndexOf(rating) - 1];
    }

    /// <summary>The positions of the division a rating is trained on, by callsign, as the core's directory gives them.</summary>
    private async Task<List<string>> OfferedAsync(Rating rating, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var positions = await scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>().ForRatingAsync(rating, cancellationToken);
        return [.. positions.Select(position => position.Callsign)];
    }

    private async Task<AtcPositionDto> PositionAsync(Rating rating, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var positions = await scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>().ForRatingAsync(rating, cancellationToken);
        return positions.First(position => position.Fir is not null);
    }

    private static JsonElement Path(JsonElement page, RatingKind kind) =>
        page.GetProperty("paths").EnumerateArray().Single(path => path.GetProperty("kind").GetString() == kind.ToString());

    private static async Task<JsonElement> RequestedAsync(HttpClient client, object payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(Mine, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        var problem = JsonDocument.Parse(text).RootElement;
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var keys), text);
        Assert.Contains(key, keys.EnumerateArray().Select(error => error.GetString()));
    }

    private async Task<int> TrainingsOfAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().Trainings.IgnoreQueryFilters()
            .CountAsync(training => training.TraineeVid == vid, cancellationToken);
    }

    private async Task<int> MailsOfAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .CountAsync(notification => notification.Vid == vid && notification.Type == TrainingNotifications.RequestReceived, cancellationToken);
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

    /// <summary>What this class leaves: the trainings and bans of its members, and their mails.</summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        await database.Trainings.IgnoreQueryFilters().Where(training => Vids.Contains(training.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await database.TraineeBans.Where(ban => Vids.Contains(ban.Vid)).ExecuteDeleteAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .Where(notification => Vids.Contains(notification.Vid))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>
    /// A member who is not staff, on the rung below the first trained rating of each ladder, with hours on both; an address only
    /// when the test looks for their mail.
    /// </summary>
    private async Task SeedMemberAsync(int vid, string? email, CancellationToken cancellationToken)
    {
        var (atc, pilot) = Trained();
        var ratingAtc = RungBelow(atc).Number;
        var ratingPilot = RungBelow(pilot).Number;

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
        user.LastName = "Trainee";
        user.Email = email;
        user.IsStaff = false;
        user.RatingAtc = ratingAtc;
        user.RatingPilot = ratingPilot;
        user.HoursAtc = TraineeHours;
        user.HoursPilot = TraineeHours;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
