using System.Globalization;
using System.Net;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Public;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The names on a session's public page only until its day (issue #313; note <c>i-nomi-di-una-sessione-fino-al-suo-giorno</c>),
/// through the real host: a signed in member reads who of a session to come and of one on its day — begun, or reported already —, and
/// of a session whose day is over in the division's time zone reads what a visitor reads, the rating, the position and when; the page
/// stays, and says it names nobody. The trainee and the staff still read who on their own pages.
/// <para>The trainings are written as the installation, straight into the state a test starts from. The shared database may hold
/// other sessions: a test reads its own by their addresses.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingPublicNamesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module gave this fix in the shared database (CONTRIBUTING.md): 790100–790119.
    private const int UpcomingTraineeVid = 790100;
    private const int TrainerVid = 790101;
    private const int MemberVid = 790102;
    private const int CoordinatorVid = 790103;
    private const int TodayTraineeVid = 790104;
    private const int ReportedTodayTraineeVid = 790105;
    private const int YesterdayTraineeVid = 790106;
    private const int HeldTraineeVid = 790107;

    private static readonly int[] Vids =
        [UpcomingTraineeVid, TrainerVid, MemberVid, CoordinatorVid, TodayTraineeVid, ReportedTodayTraineeVid, YesterdayTraineeVid, HeldTraineeVid];

    private const string GrantReason = "trn-test";

    /// <summary>The position of this class's trainings: made up, so no division's is named.</summary>
    private const string Position = "TRNTEST_POS";

    private const string TrainerName = "Named Trainer";

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        // A run that failed half way leaves its trainings, their entries of the calendar and grants.
        await CleanAsync(token);

        foreach (var (vid, first) in new[]
                 {
                     (UpcomingTraineeVid, "Upcoming"),
                     (TodayTraineeVid, "Today"),
                     (ReportedTodayTraineeVid, "Reported"),
                     (YesterdayTraineeVid, "Yesterday"),
                     (HeldTraineeVid, "Held"),
                     (MemberVid, "Some"),
                 })
        {
            await SeedPersonAsync(vid, first, "Trainee", token);
        }

        // The trainer conducts, and reads every training from the back office (R.1).
        await SeedPersonAsync(TrainerVid, "Named", "Trainer", token, isStaff: true);
        await GrantAsync(TrainerVid, token, TrainingPermissions.View, TrainingPermissions.Conduct);
    }

    public async ValueTask DisposeAsync()
    {
        await CleanAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Carmine's (b) on #313: until the session's day is over a signed in member reads who — of a session to come, of one begun today,
    /// of one reported today —; once it is over, of a session left without a report or of one held days ago, the page is there with its
    /// rating, its position and when, and with nobody's VID or name, for a member as for a visitor. A visitor never reads who, and is
    /// told whether signing in would.
    /// </summary>
    [Fact]
    public async Task ASessionNamesWhoUntilItsDayIsOverAndNoLonger()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var today = HeldBefore(now);

        var upcoming = await AddTrainingAsync(UpcomingTraineeVid, TrainingState.Scheduled, now.AddHours(3), token);
        var begunToday = await AddTrainingAsync(TodayTraineeVid, TrainingState.Scheduled, today, token);
        var reportedToday = await AddTrainingAsync(ReportedTodayTraineeVid, TrainingState.Completed, today, token, completedAt: now);
        var yesterday = await AddTrainingAsync(YesterdayTraineeVid, TrainingState.Scheduled, today.AddMinutes(-1), token);
        var held = await AddTrainingAsync(HeldTraineeVid, TrainingState.Completed, now.AddDays(-3), token, completedAt: now.AddDays(-3).AddHours(2));

        using var member = await SignedInAsync(MemberVid, token);
        using var anonymous = _factory.CreateApiClient();

        foreach (var (id, trainee, name) in new[]
                 {
                     (upcoming, UpcomingTraineeVid, "Upcoming Trainee"),
                     (begunToday, TodayTraineeVid, "Today Trainee"),
                     (reportedToday, ReportedTodayTraineeVid, "Reported Trainee"),
                 })
        {
            var (_, page) = await SessionAsync(member, id, token);
            Assert.True(page.GetProperty("named").GetBoolean(), $"Session {id} names who on its day.");
            Assert.Equal((trainee, name), Person(page.GetProperty("trainee")));
            Assert.Equal((TrainerVid, TrainerName), Person(page.GetProperty("trainer")));

            var (seenText, seen) = await SessionAsync(anonymous, id, token);
            Assert.True(seen.GetProperty("named").GetBoolean());
            AssertNobody(seen);
            AssertSaysNone(seenText, trainee, name);
        }

        Assert.True((await SessionAsync(member, reportedToday, token)).Json.GetProperty("held").GetBoolean());

        foreach (var (id, trainee, name, start) in new[]
                 {
                     (yesterday, YesterdayTraineeVid, "Yesterday Trainee", today.AddMinutes(-1)),
                     (held, HeldTraineeVid, "Held Trainee", now.AddDays(-3)),
                 })
        {
            foreach (var client in new[] { member, anonymous })
            {
                var (text, page) = await SessionAsync(client, id, token);
                Assert.Equal((Position, true, false), (page.GetProperty("position").GetString(), page.GetProperty("held").GetBoolean(), page.GetProperty("named").GetBoolean()));
                Assert.Equal(Trained().ShortName, page.GetProperty("ratingShortName").GetString());
                Assert.Equal(start, page.GetProperty("startsAtUtc").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
                AssertNobody(page);
                AssertSaysNone(text, trainee, name);
            }
        }
    }

    /// <summary>
    /// The rule is the public page's alone: the trainee reads who trained them on their own page, and the staff reads both from the back
    /// office, of a session held days ago as of any other.
    /// </summary>
    [Fact]
    public async Task TheTraineeAndTheStaffStillReadWhoOfASessionHeld()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var held = await AddTrainingAsync(HeldTraineeVid, TrainingState.Completed, now.AddDays(-3), token, completedAt: now.AddDays(-3).AddHours(2));

        using var trainee = await SignedInAsync(HeldTraineeVid, token);
        var (_, mine) = await ReadAsync(await trainee.GetAsync(new Uri($"{RequestEndpoints.Pattern}/{held}", UriKind.Relative), token), token);
        Assert.Equal((TrainerVid, TrainerName), Person(mine.GetProperty("trainer")));

        using var staff = await SignedInAsync(TrainerVid, token);
        var (_, row) = await ReadAsync(await staff.GetAsync(new Uri($"{StaffEndpoints.Pattern}/{held}", UriKind.Relative), token), token);
        Assert.Equal((HeldTraineeVid, "Held Trainee"), Person(row.GetProperty("trainee")));
        Assert.Equal((TrainerVid, TrainerName), Person(row.GetProperty("trainer")));

        // And the public page of the same session names nobody, to the trainee too.
        AssertNobody((await SessionAsync(trainee, held, token)).Json);
    }

    // ---- reading ---------------------------------------------------------------------------------------------------------------

    private static (int Vid, string? Name) Person(JsonElement person) => (person.GetProperty("vid").GetInt32(), person.GetProperty("name").GetString());

    private static void AssertNobody(JsonElement session)
    {
        Assert.Equal(JsonValueKind.Null, session.GetProperty("trainee").ValueKind);
        Assert.Equal(JsonValueKind.Null, session.GetProperty("trainer").ValueKind);
    }

    /// <summary>Nothing of the trainee nor of the trainer, by VID or by name, anywhere in the answer.</summary>
    private static void AssertSaysNone(string text, int trainee, string name)
    {
        foreach (var secret in new[]
                 {
                     trainee.ToString(CultureInfo.InvariantCulture),
                     TrainerVid.ToString(CultureInfo.InvariantCulture),
                     name,
                     TrainerName,
                 })
        {
            Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
        }
    }

    private static async Task<(string Text, JsonElement Json)> SessionAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await ReadAsync(await client.GetAsync(new Uri($"{PublicSessionEndpoints.Pattern}/{id}", UriKind.Relative), cancellationToken), cancellationToken);

    private static async Task<(string Text, JsonElement Json)> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {text}");
            return (text, JsonDocument.Parse(text).RootElement.Clone());
        }
    }

    /// <summary>The moment today began in the division's time zone: a session from then on is on a day that is not over.</summary>
    private DateTime HeldBefore(DateTime utcNow) =>
        StaffQueue.HeldBefore(utcNow, _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value.ResolveTimeZone());

    /// <summary>The first rating of the ATC ladder with a practical training: what the trainings of this class are for.</summary>
    private Rating Trained()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TrainingReference>().Ratings.First(rating => rating.Kind == RatingKind.Atc);
    }

    // ---- writing ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A dated training of this class's trainer, written as the installation straight into the state the test starts from; its session
    /// goes into the calendar, as the interceptor does for every write.
    /// </summary>
    private async Task<long> AddTrainingAsync(
        int trainee,
        TrainingState state,
        DateTime start,
        CancellationToken cancellationToken,
        DateTime? completedAt = null)
    {
        var now = DateTime.UtcNow;

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var training = new Training
        {
            Kind = RatingKind.Atc,
            Rating = Trained().Number,
            Position = Position,
            TraineeVid = trainee,
            TraineeHoursAtRequest = 500m,
            TheoryConfirmedAt = now,
            State = state,
            DecidedBy = CoordinatorVid,
            DecidedAt = now,
            TrainerVid = TrainerVid,
            AssignedBy = CoordinatorVid,
            AssignedAt = now,
            ScheduledStartUtc = start,
            CompletedAt = completedAt,
        };

        database.Trainings.Add(training);
        await database.SaveChangesAsync(cancellationToken);
        return training.Id;
    }

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
        user.HoursAtc = 500m;
        user.HoursPilot = 500m;
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

    /// <summary>What this class leaves: the trainings of its people with their entries of the calendar, their grants and their mails.</summary>
    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var training = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var ids = await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).Select(row => row.Id).ToListAsync(cancellationToken);
        var sources = ids.Select(Training.SourceIdOf).ToList();
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && sources.Contains(entry.SourceId))
            .ExecuteDeleteAsync(cancellationToken);

        await training.Trainings.IgnoreQueryFilters().Where(row => Vids.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await hub.UserGrants.Where(grant => grant.Vid != null && Vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications.Where(notification => Vids.Contains(notification.Vid)).ExecuteDeleteAsync(cancellationToken);
    }
}
