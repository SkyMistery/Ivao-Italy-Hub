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
using IvaoHub.Modules.Training.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The skeleton of the training (M3, A4), through the real host with the division file of this repository: the grants of the
/// training department arrive from <c>positionGrants</c> once and reach its people, whoever holds
/// <c>Training.ManageSettings</c> changes the settings and reads them back and nobody else does, and the settings are chosen
/// from the ratings and the positions the division trains.
/// <para>⚠️ The people of the training department are seeded **without an address**: the tests of the contacts assert who
/// receives a message to a department, the notification service leaves out a member with no address
/// (<c>NotificationService.Resolve</c>), and so these three receive nothing and change no count of theirs. Everybody else
/// holds what they hold by a grant to their VID (<c>CONTRIBUTING.md</c>, "Tests").</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingSkeletonTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md); 790001–790008 are A1's and A3's.
    private const int CoordinatorVid = 790009;
    private const int AdvisorVid = 790010;
    private const int TrainerVid = 790011;
    private const int ManagerVid = 790012;
    private const int ViewerVid = 790013;

    private const string GrantReason = "trn-test";

    private static readonly Uri SettingsUri = new(
        ModuleSettingsEndpoints.Pattern.Replace("{key}", TrainingModule.ModuleKey, StringComparison.Ordinal),
        UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The positions the settings choose from are the reference data of the night, read from the recorded answers.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>().RunAsync(token);
        }

        await SeedUserAsync(CoordinatorVid, "IT-TC", token);
        await SeedUserAsync(AdvisorVid, "IT-TA1", token);
        await SeedUserAsync(TrainerVid, "IT-T03", token);
        await SeedUserAsync(ManagerVid, position: null, token);
        await SeedUserAsync(ViewerVid, position: null, token);
        await GrantAsync(ManagerVid, TrainingPermissions.ManageSettings, token);
        await GrantAsync(ViewerVid, TrainingPermissions.View, token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheGrantsOfTheTrainingDepartmentArriveOnceAndReachItsPeople()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A second start of the application, as a restart would be: nothing is added.
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PositionGrantSeeder>().SeedAsync(token));

            var seeded = await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants.AsNoTracking()
                .Where(grant => grant.Reason == "division.json" && grant.Value.StartsWith(TrainingPermissions.Area + "."))
                .Select(grant => grant.Value)
                .ToListAsync(token);

            Assert.Equal(TrainingPermissions.All.Count, seeded.Count);
            Assert.Equal(seeded.Count, seeded.Distinct().Count());
        }

        // Design M3 §3.2: the coordinator everything, the advisor views, approves and puts exams in the calendar, the trainer
        // views — an exam is assigned only to an examiner, never to a trainer — all of it on the training department.
        Assert.Equal(TrainingPermissions.All.Select(permission => permission.Name).Order(StringComparer.Ordinal), await TrainingPermissionsOfAsync(CoordinatorVid, token));
        Assert.Equal(
            new[] { TrainingPermissions.View, TrainingPermissions.Approve, TrainingPermissions.ManageExams }.Order(StringComparer.Ordinal),
            await TrainingPermissionsOfAsync(AdvisorVid, token));
        Assert.Equal([TrainingPermissions.View], await TrainingPermissionsOfAsync(TrainerVid, token));

        // The section of the back office is there for whoever may follow its entry, and for nobody else.
        Assert.Contains("/staff/training/settings", await StaffEntriesOfAsync(CoordinatorVid, token));
        Assert.DoesNotContain("/staff/training/settings", await StaffEntriesOfAsync(TrainerVid, token));
    }

    [Fact]
    public async Task WhoManagesTheSettingsChangesThemAndReadsThemBackAndNobodyElseDoes()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetSettingsAsync(token);

        try
        {
            using var manager = await SignedInAsync(ManagerVid, token);
            using var viewer = await SignedInAsync(ViewerVid, token);
            using var coordinator = await SignedInAsync(CoordinatorVid, token);

            // An installation that never saved reads the design's defaults.
            var defaults = await manager.GetFromJsonAsync<JsonElement>(SettingsUri, token);
            Assert.Equal(5, defaults.GetProperty("cooldownDays").GetInt32());
            Assert.Equal(JsonValueKind.Null, defaults.GetProperty("maxResponseDays").ValueKind);
            Assert.Equal(["event"], defaults.GetProperty("conflictKinds").EnumerateArray().Select(kind => kind.GetString()));
            Assert.Empty(defaults.GetProperty("minimumHours").EnumerateArray());

            // What the settings are chosen from: a rating the division trains, one it does not, a position it trains on.
            var (trained, untrained, position) = await ChoicesAsync(token);

            var changed = Settings(defaults);
            changed["cooldownDays"] = JsonSerializer.SerializeToElement(7);
            changed["maxResponseDays"] = JsonSerializer.SerializeToElement(10);
            changed["conflictPolicy"] = JsonSerializer.SerializeToElement("Block");
            changed["conflictKinds"] = JsonSerializer.SerializeToElement(new[] { "event", "training" });
            changed["minimumHours"] = JsonSerializer.SerializeToElement(new[] { new { kind = trained.Kind.ToString(), rating = trained.Number, hours = 50 } });
            changed["hiddenPositions"] = JsonSerializer.SerializeToElement(new[] { position });
            changed["theoryExamUrl"] = JsonSerializer.SerializeToElement("https://exam.example.org/theory");

            using (var saved = await manager.PutAsJsonAsync(SettingsUri, changed, token))
            {
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            }

            var reread = await manager.GetFromJsonAsync<JsonElement>(SettingsUri, token);
            Assert.Equal(7, reread.GetProperty("cooldownDays").GetInt32());
            Assert.Equal(10, reread.GetProperty("maxResponseDays").GetInt32());
            Assert.Equal("Block", reread.GetProperty("conflictPolicy").GetString());
            Assert.Equal(["event", "training"], reread.GetProperty("conflictKinds").EnumerateArray().Select(kind => kind.GetString()));
            Assert.Equal(trained.Number, reread.GetProperty("minimumHours")[0].GetProperty("rating").GetInt32());
            Assert.Equal([position], reread.GetProperty("hiddenPositions").EnumerateArray().Select(callsign => callsign.GetString()));
            Assert.Equal("https://exam.example.org/theory", reread.GetProperty("theoryExamUrl").GetString());

            // The coordinator holds the same permission by the position, and reads the same.
            Assert.Equal(7, (await coordinator.GetFromJsonAsync<JsonElement>(SettingsUri, token)).GetProperty("cooldownDays").GetInt32());

            // Every value out of its rules is refused on its field, the rows of a list on the field of the row, and nothing is
            // saved.
            var invalid = Settings(reread);
            invalid["cooldownDays"] = JsonSerializer.SerializeToElement(-1);
            invalid["minimumHours"] = JsonSerializer.SerializeToElement(new[] { new { kind = untrained.Kind.ToString(), rating = untrained.Number, hours = 50 } });
            invalid["conflictKinds"] = JsonSerializer.SerializeToElement(new[] { "trn-test-no-such-kind" });
            invalid["hiddenPositions"] = JsonSerializer.SerializeToElement(new[] { "TRN_TEST_NOWHERE" });
            invalid["theoryExamUrl"] = JsonSerializer.SerializeToElement("javascript:alert(1)");

            using (var refused = await manager.PutAsJsonAsync(SettingsUri, invalid, token))
            {
                Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
                var errors = (await refused.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");

                Assert.Equal(
                    new Dictionary<string, string>
                    {
                        ["cooldownDays"] = "errors.number.range",
                        ["minimumHours[0].rating"] = "training:errors.ratingNotTrained",
                        ["conflictKinds"] = "training:errors.calendarKindUnknown",
                        ["hiddenPositions[0].callsign"] = "training:errors.positionUnknown",
                        ["theoryExamUrl"] = "errors.url.absolute",
                    },
                    errors.EnumerateObject().ToDictionary(field => field.Name, field => field.Value[0].GetString()!));
            }

            Assert.Equal(7, (await manager.GetFromJsonAsync<JsonElement>(SettingsUri, token)).GetProperty("cooldownDays").GetInt32());

            // Viewing the trainings is not managing them.
            using (var read = await viewer.GetAsync(SettingsUri, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
            }

            using (var write = await viewer.PutAsJsonAsync(SettingsUri, changed, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
            }
        }
        finally
        {
            // Put back as a fresh installation has them, for whoever reads them next.
            await ForgetSettingsAsync(token);
        }
    }

    [Fact]
    public async Task TheSettingsAreChosenFromTheRatingsAndThePositionsTheDivisionTrains()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var reference = scope.ServiceProvider.GetRequiredService<TrainingReference>();
        var vocabulary = scope.ServiceProvider.GetRequiredService<RatingVocabulary>();
        var directory = scope.ServiceProvider.GetRequiredService<IAtcPositionDirectory>();

        // The ratings: every one the core's vocabulary trains, ladder by ladder, and to any member.
        using var viewer = await SignedInAsync(ViewerVid, token);
        var ratings = await viewer.GetFromJsonAsync<JsonElement>(ReferenceEndpoints.RatingsPattern, token);

        Assert.Equal(
            Enum.GetValues<RatingKind>().SelectMany(vocabulary.Ladder).Where(rating => rating.HasPracticalTraining)
                .Select(rating => (rating.Kind.ToString(), rating.Number, rating.ShortName, rating.NameKey)),
            ratings.EnumerateArray().Select(rating => (
                rating.GetProperty("kind").GetString()!,
                rating.GetProperty("number").GetInt32(),
                rating.GetProperty("shortName").GetString()!,
                rating.GetProperty("nameKey").GetString()!)));

        using (var anonymous = _factory.CreateApiClient())
        using (var refused = await anonymous.GetAsync(new Uri(ReferenceEndpoints.RatingsPattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        // The positions: those of the division the directory gives each trained rating, each with the rating; only for whoever
        // manages the settings, the one screen that offers them all.
        var expected = new List<string>();
        foreach (var rating in reference.Ratings)
        {
            expected.AddRange((await directory.ForRatingAsync(rating, token)).Select(position => position.Callsign));
        }

        using var manager = await SignedInAsync(ManagerVid, token);
        var positions = await manager.GetFromJsonAsync<JsonElement>(ReferenceEndpoints.PositionsPattern, token);

        Assert.NotEmpty(expected);
        Assert.Equal(expected.Distinct(), positions.EnumerateArray().Select(position => position.GetProperty("callsign").GetString()));
        Assert.All(positions.EnumerateArray(), position => Assert.Contains(
            position.GetProperty("ratingShortName").GetString(),
            reference.Ratings.Select(rating => rating.ShortName)));

        using (var refused = await viewer.GetAsync(new Uri(ReferenceEndpoints.PositionsPattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>A trained rating, one the vocabulary has and does not train, and a position the division trains on.</summary>
    private async Task<(Rating Trained, Rating Untrained, string Position)> ChoicesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var vocabulary = scope.ServiceProvider.GetRequiredService<RatingVocabulary>();
        var reference = scope.ServiceProvider.GetRequiredService<TrainingReference>();

        var positions = await reference.PositionsAsync(cancellationToken);
        Assert.NotEmpty(positions);

        var trained = reference.Ratings.First(rating => rating.Kind == RatingKind.Atc);
        var untrained = vocabulary.Ladder(RatingKind.Atc).First(rating => !rating.HasPracticalTraining);
        return (trained, untrained, positions[0].Callsign);
    }

    /// <summary>The permissions of the training a member holds on the training department, as their session says.</summary>
    private async Task<IEnumerable<string>> TrainingPermissionsOfAsync(int vid, CancellationToken cancellationToken)
    {
        using var client = await SignedInAsync(vid, cancellationToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken);

        return me.GetProperty("permissions").EnumerateArray()
            .Where(permission => permission.GetProperty("department").GetString() == nameof(Department.TD))
            .Select(permission => permission.GetProperty("name").GetString()!)
            .Where(name => name.StartsWith(TrainingPermissions.Area + ".", StringComparison.Ordinal))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The addresses of the back office entries of the training a member is offered.</summary>
    private async Task<IEnumerable<string>> StaffEntriesOfAsync(int vid, CancellationToken cancellationToken)
    {
        using var client = await SignedInAsync(vid, cancellationToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken);

        return me.GetProperty("navigation").GetProperty("staff").EnumerateArray()
            .Where(entry => entry.TryGetProperty("module", out var module) && module.GetString() == TrainingModule.ModuleKey)
            .Select(entry => entry.GetProperty("path").GetString()!)
            .ToList();
    }

    private static Dictionary<string, JsonElement> Settings(JsonElement settings) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settings.GetRawText())!;

    private async Task ForgetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var row = await database.DivisionSettings
            .FirstOrDefaultAsync(setting => setting.Key == ModuleSettingsStore.SettingsKey(TrainingModule.ModuleKey), cancellationToken);

        if (row is not null)
        {
            database.DivisionSettings.Remove(row);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member of the staff, with a position of the training department or none — and never an address.</summary>
    private async Task SeedUserAsync(int vid, string? position, CancellationToken cancellationToken)
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
        user.LastName = "Training";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
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

    /// <summary>A permission on the training department, to one VID, once.</summary>
    private async Task GrantAsync(int vid, string permission, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (await database.UserGrants.AnyAsync(grant => grant.Vid == vid && grant.Value == permission && grant.Reason == GrantReason, cancellationToken))
        {
            return;
        }

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
}
