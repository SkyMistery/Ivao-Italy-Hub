using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A pilot's report through the real host (M2, T11): the sessions the tracker offers, the send with the checks that block it,
/// what it freezes, the claim on a session, the withdrawal — by the pilot, through the one exception of the interceptor's
/// guard, and by the module's job — and the refusals a pilot meets before any flight is looked at.
/// <para>The tracker is a double that answers with flights of yesterday between the test airports, built by each test: the
/// recorded fixtures are flights of June, which no report window reaches. Everything else of IVAO is the fixture client.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed partial class PirepTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 §13); T11 takes 82–84.
    private const int CoordinatorVid = 780082;
    private const int PilotVid = 780083;
    private const int OtherPilotVid = 780084;

    private static readonly string[] Locales = ["it", "en"];

    private readonly FlightShelf _flights = new();
    private readonly List<long> _tours = [];
    private readonly List<long> _rules = [];

    private HubWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _host = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IIvaoApiClient>(provider =>
                new TrackerDouble(provider.GetRequiredService<FixtureIvaoApiClient>(), _flights))));

        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(CoordinatorVid, staffPosition: "IT-FOC", rating: 4, token);
        await SeedUserAsync(PilotVid, staffPosition: null, rating: 4, token);
        await SeedUserAsync(OtherPilotVid, staffPosition: null, rating: 4, token);
        await SeedReviewersAsync(token);
        await FoTestAirports.SeedAsync(_host.Services, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var vids = new[] { PilotVid, OtherPilotVid, CoordinatorVid, SuperadminPilotVid };

            // A tour with a report is never deleted by the application: the test takes its reports back first.
            await Everything<Pirep>(database).Where(report => vids.Contains(report.Vid) || _tours.Contains(report.TourId)).ExecuteDeleteAsync(token);
            await database.Enrolments.Where(row => vids.Contains(row.Vid) || _tours.Contains(row.TourId)).ExecuteDeleteAsync(token);
            await database.Bans.Where(ban => vids.Contains(ban.Vid)).ExecuteDeleteAsync(token);
            await Everything<Tour>(database).Where(tour => _tours.Contains(tour.Id)).ExecuteDeleteAsync(token);
            await database.Rules.Where(rule => _rules.Contains(rule.Id)).ExecuteDeleteAsync(token);
            await database.Errors.Where(error => _errors.Contains(error.Id)).ExecuteDeleteAsync(token);
        }

        await CleanReviewersAsync(token);

        await FoTestAirports.RemoveAsync(_host.Services, token);
        await _host.DisposeAsync();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The "done when" of T11 without the browser: a pilot finds their flight among the sessions of a leg, sends it, and sees
    /// it in the queue on their tour — the leg pending, the next one to fly — with the rules in force frozen with their
    /// parameters. A correction after «to modify» goes back to the queue and freezes nothing again (§5.4).
    /// </summary>
    [Fact]
    public async Task APilotSendsAReportSeesItQueuedAndACorrectionKeepsWhatTheFirstSendFroze()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var code = await RuleAsync(coordinator, tourId, radiusNm: 3, token);

        var flown = _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1));
        _flights.Add(PilotVid, "XAA200", Milan, London, DateTime.UtcNow.AddDays(-1).AddHours(3));

        // Only the flight of the leg's route is offered.
        var sessions = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/sessions?legId={legs[0]}", token), token);
        Assert.Equal([flown], sessions.EnumerateArray().Select(row => row.GetProperty("id").GetInt64()));

        var sent = await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token);
        Assert.Equal("Queued", sent.GetProperty("status").GetString());
        Assert.Equal("I", sent.GetProperty("flightRules").GetString());
        Assert.Equal(Rome, sent.GetProperty("departureIcao").GetString());

        // On the tour: the leg pending, the next one to fly (FlyAhead), and the report in the list.
        var mine = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.Equal(legs[1], mine.GetProperty("next").GetInt64());
        Assert.Equal("Pending", Colour(mine, legs[0]));
        Assert.Equal("Todo", Colour(mine, legs[1]));
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("blocked").ValueKind);
        Assert.Equal(Id(sent), Id(Assert.Single(mine.GetProperty("reports").EnumerateArray())));

        // What it froze: the rule with its parameter, the leg as it was, the history, and the enrolment.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var row = await Everything<Pirep>(database).Include(report => report.Events).SingleAsync(report => report.Id == Id(sent), token);

            var rule = Assert.Single(JsonNode.Parse(row.RulesSnapshotJson)!.AsArray(), node => (string?)node!["code"] == code)!;
            Assert.Equal(3, (int)rule["parameters"]!["radiusNm"]!);
            Assert.Equal(Milan, (string?)JsonNode.Parse(row.LegSnapshotJson)!["arrivalIcao"]);
            Assert.Equal(PirepStatus.Queued, Assert.Single(row.Events).ToStatus);
            Assert.True(await database.Enrolments.AnyAsync(enrolment => enrolment.TourId == tourId && enrolment.Vid == PilotVid, token));

            // A validator sends it back (T13 will); meanwhile the rule changes.
            await Everything<Pirep>(database).Where(report => report.Id == row.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(report => report.Status, PirepStatus.ToModify), token);
            await database.Rules.Where(rule => rule.TourId == tourId)
                .ExecuteUpdateAsync(set => set.SetProperty(rule => rule.ParametersJson, """{"radiusNm":9}"""), token);
        }

        // «To modify» blocks every other leg of the tour until it is corrected (§3.1).
        var blocked = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.Equal("flightops:errors.reportToModifyPending", blocked.GetProperty("blocked").GetString());

        var read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{Id(sent)}", token), token);
        var corrected = await OkAsync(
            await pilot.PutAsJsonAsync(
                $"{PirepEndpoints.Pattern}/{Id(sent)}",
                Payload(legs[0], flown) with { PilotRemarks = "fo-test corrected", RowVersion = read.GetProperty("rowVersion").GetDateTime() },
                token),
            token);
        Assert.Equal("Queued", corrected.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, corrected.GetProperty("resubmittedAt").ValueKind);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var row = await Everything<Pirep>(database).Include(report => report.Events).SingleAsync(report => report.Id == Id(sent), token);
            var rule = Assert.Single(JsonNode.Parse(row.RulesSnapshotJson)!.AsArray(), node => (string?)node!["code"] == code)!;
            Assert.Equal(3, (int)rule["parameters"]!["radiusNm"]!);
            Assert.Equal("fo-test corrected", row.PilotRemarks);
            Assert.Equal(2, row.Events.Count);
        }

        // Somebody else's report is nobody else's business.
        using var other = await SignedInAsync(OtherPilotVid, token);
        using var foreign = await other.GetAsync($"{PirepEndpoints.Pattern}/{Id(sent)}", token);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        // A tour with a report is no longer deleted, and a leg with one is retired (design M2 §1.2.2, §1.4.1).
        var removal = await OkAsync(await coordinator.GetAsync($"/api/flightops/tours/{tourId}/legs/{legs[0]}/removal", token), token);
        Assert.Equal("Retire", removal.GetProperty("outcome").GetString());
    }

    /// <summary>
    /// A session counts for one report (design M2 §3.4): on another tour with the same route it is refused, and once the pilot
    /// withdraws the first report — a member writing a row of the department, through the guard's one exception — it is free.
    /// </summary>
    [Fact]
    public async Task ASessionIsClaimedOnceAndAWithdrawalLetsItGo()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (first, firstLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (second, secondLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var flown = _flights.Add(PilotVid, "XAA300", Rome, Milan, DateTime.UtcNow.AddDays(-2));

        var sent = await CreatedAsync(pilot, Reports(first), Payload(firstLegs[0], flown), token);

        // Claimed: not offered on the second tour, and refused if sent anyway.
        var offered = await OkAsync(await pilot.GetAsync($"{Reports(second)}/sessions?legId={secondLegs[0]}", token), token);
        Assert.DoesNotContain(offered.EnumerateArray(), row => row.GetProperty("id").GetInt64() == flown);
        await RefusedAsync(pilot, Reports(second), Payload(secondLegs[0], flown), "sessionIds", "flightops:errors.reportSessionClaimed", token);

        var withdrawn = await OkAsync(
            await pilot.PostAsJsonAsync(
                $"{PirepEndpoints.Pattern}/{Id(sent)}/withdraw",
                new PirepWithdrawal(sent.GetProperty("rowVersion").GetDateTime()),
                token),
            token);
        Assert.Equal("Withdrawn", withdrawn.GetProperty("status").GetString());

        // Withdrawn once is withdrawn: the second time is refused, not a second step in the history.
        using var again = await pilot.PostAsJsonAsync(
            $"{PirepEndpoints.Pattern}/{Id(sent)}/withdraw",
            new PirepWithdrawal(withdrawn.GetProperty("rowVersion").GetDateTime()),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        await CreatedAsync(pilot, Reports(second), Payload(secondLegs[0], flown), token);

        // The first tour's leg is to fly again.
        var mine = await OkAsync(await pilot.GetAsync($"{Reports(first)}/mine", token), token);
        Assert.Equal("Todo", Colour(mine, firstLegs[0]));
    }

    /// <summary>
    /// What blocks the send (§3.2 point 5, §3.6): the tour's daily limit by the UTC day of the take-off, a flight on the
    /// wrong route, a leg out of order, and a ban — which also says so on the tour, before any flight is chosen.
    /// </summary>
    [Fact]
    public async Task TheDailyLimitTheOrderTheRouteAndABanBlockTheSend()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(OtherPilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 1, token);
        var day = DateTime.UtcNow.Date.AddDays(-1);
        var firstLeg = _flights.Add(OtherPilotVid, "XAA400", Rome, Milan, day.AddHours(8));
        var secondLeg = _flights.Add(OtherPilotVid, "XAA401", Milan, London, day.AddHours(12));
        var wrongRoute = _flights.Add(OtherPilotVid, "XAA402", London, Rome, day.AddHours(16));

        // Out of order: the second leg before the first.
        await RefusedAsync(pilot, Reports(tourId), Payload(legs[1], secondLeg), "legId", "flightops:errors.reportLegLocked", token);

        // The wrong flight for the leg.
        await RefusedAsync(pilot, Reports(tourId), Payload(legs[0], wrongRoute), "sessionIds", "flightops:errors.reportFlightDeparture", token);

        await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], firstLeg), token);

        // One flight a day on this tour: the second of the same UTC day is refused.
        await RefusedAsync(pilot, Reports(tourId), Payload(legs[1], secondLeg), "sessionIds", "flightops:errors.reportTourDailyLimit", token);

        // Banned from the tour: refused, and the tour says so.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            database.Bans.Add(new Ban
            {
                Vid = OtherPilotVid,
                TourId = tourId,
                StartsAt = DateTime.UtcNow.AddHours(-1),
                Reason = "fo-test ban",
                OwnerDepartment = Department.FOD,
                OwnerDepartmentMask = DepartmentMask.Of(Department.FOD),
            });
            await database.SaveChangesAsync(token);
        }

        var mine = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.Equal("flightops:errors.reportBanned", mine.GetProperty("blocked").GetString());
        await RefusedAsync(pilot, Reports(tourId), Payload(legs[1], secondLeg), "tour", "flightops:errors.reportBanned", token);
    }

    /// <summary>
    /// A report left «to modify» beyond the tour's report window is withdrawn by the module (§3.1): the session is free and
    /// the step is the module's own, VID 0.
    /// </summary>
    [Fact]
    public async Task AReportNeverCorrectedIsWithdrawnByTheJob()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var flown = _flights.Add(PilotVid, "XAA500", Rome, Milan, DateTime.UtcNow.AddDays(-1).AddHours(-6));
        var sent = await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token);

        await using var scope = _host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();

        // Sent back to the pilot longer ago than the window (seven days, the division's default).
        await Everything<Pirep>(database).Where(report => report.Id == Id(sent))
            .ExecuteUpdateAsync(set => set.SetProperty(report => report.Status, PirepStatus.ToModify), token);
        database.PirepEvents.Add(new PirepEvent
        {
            PirepId = Id(sent),
            FromStatus = PirepStatus.InReview,
            ToStatus = PirepStatus.ToModify,
            ByVid = CoordinatorVid,
            At = DateTime.UtcNow.AddDays(-8),
        });
        await database.SaveChangesAsync(token);

        var withdrawn = await scope.ServiceProvider.GetRequiredService<PirepWithdrawalJob>().RunAsync(token);
        Assert.True(withdrawn >= 1);

        var row = await Everything<Pirep>(database).AsNoTracking()
            .Include(report => report.Flights)
            .Include(report => report.Events)
            .SingleAsync(report => report.Id == Id(sent), token);
        Assert.Equal(PirepStatus.Withdrawn, row.Status);
        Assert.All(row.Flights, flight => Assert.Null(flight.ClaimedSessionId));
        Assert.Equal(0, row.Events.OrderBy(step => step.At).Last().ByVid);
    }

    /// <summary>
    /// T12 without an archive — a fork's case, and the hub's until vIPI's view exists: the form is told «not available», the
    /// report is sent all the same with the controllers the pilot wrote, and an exemption on one of them is «unverifiable».
    /// </summary>
    [Fact]
    public async Task WithoutAnArchiveTheControllersAreNotAvailableAndTheReportIsSentAllTheSame()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var flown = _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1));

        var proposal = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/atc?sessionIds={flown}", token), token);
        Assert.False(proposal.GetProperty("available").GetBoolean());
        Assert.Empty(proposal.GetProperty("proposed").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(proposal.GetProperty("attribution").GetString()));

        var sent = await CreatedAsync(
            pilot,
            Reports(tourId),
            Payload(legs[0], flown) with
            {
                AtcContacts = [new($"{Rome}_TWR", "118.705")],
                Exemptions = [new($"{Rome}_TWR", ExemptionKind.FreeSpeed, null)],
            },
            token);

        Assert.False(sent.GetProperty("atcArchiveAvailable").GetBoolean());
        var contact = Assert.Single(sent.GetProperty("atcContacts").EnumerateArray());
        Assert.Equal("Added", contact.GetProperty("origin").GetString());
        var exemption = Assert.Single(sent.GetProperty("exemptions").EnumerateArray());
        Assert.Equal("Unverifiable", exemption.GetProperty("status").GetString());
        Assert.Equal([CheckCatalog.Speed250], exemption.GetProperty("softens").EnumerateArray().Select(item => item.GetString()));

        // An exemption on a position the pilot did not declare is refused under its field.
        var again = _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1).AddHours(4));
        await RefusedAsync(
            pilot,
            Reports(tourId),
            Payload(legs[1], again) with { Exemptions = [new($"{Milan}_APP", ExemptionKind.LevelChange, null)] },
            "exemptions",
            "flightops:errors.exemptionNotContacted",
            token);
    }

    /// <summary>
    /// T12's "done when" on a fake of vIPI's view, built here with the very SQL of the contract (plan M2, T12): the positions
    /// of the departure and the arrival online at the right moments are proposed, and nothing else; the report keeps the kept,
    /// the removed and the added, and an exemption is «online» or «not online» as the archive says.
    /// </summary>
    [Fact]
    public async Task WithAnArchiveThePositionsOnlineAlongTheFlightAreProposedAndKept()
    {
        var token = TestContext.Current.CancellationToken;
        var takeoff = DateTime.UtcNow.AddDays(-1);
        takeoff = takeoff.AddTicks(-(takeoff.Ticks % TimeSpan.TicksPerSecond));

        await using var archive = await FakeArchiveAsync(
            token,
            (1, $"{Rome}_TWR", "118.705", takeoff.AddHours(-1), takeoff.AddMinutes(5), false),
            (2, $"{Rome}_GND", "121.805", takeoff.AddHours(2), takeoff.AddHours(3), false),
            (3, $"{Milan}_APP", "126.750", takeoff.AddMinutes(40), null, false),
            (4, $"{London}_TWR", "118.500", takeoff.AddHours(-2), takeoff.AddHours(4), true),
            // The oldest row of each half: where the archive is complete from.
            (5, "XFAX_CTR", null, takeoff.AddYears(-1), takeoff.AddYears(-1).AddHours(1), false),
            (6, "XFBX_CTR", null, takeoff.AddMonths(-1), takeoff.AddMonths(-1).AddHours(1), true));

        await using var host = _host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:AtcData", mariaDb.ConnectionString);
            builder.ConfigureTestServices(services =>
                services.AddScoped<IAtcActivitySource>(provider => provider.GetRequiredService<VipiAtcActivitySource>()));
        });

        using var coordinator = await SignedInAsync(host, CoordinatorVid, token);
        using var pilot = await SignedInAsync(host, PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var flown = _flights.Add(PilotVid, "XAA100", Rome, Milan, takeoff);

        var proposal = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/atc?sessionIds={flown}", token), token);
        Assert.True(proposal.GetProperty("available").GetBoolean());
        Assert.Equal(
            [$"{Rome}_TWR", $"{Milan}_APP"],
            proposal.GetProperty("proposed").EnumerateArray().Select(item => item.GetProperty("callsign").GetString()));

        // The pilot keeps the tower, removes the approach and adds a centre the archive has never seen.
        var sent = await CreatedAsync(
            pilot,
            Reports(tourId),
            Payload(legs[0], flown) with
            {
                AtcContacts = [new($"{Rome}_TWR", null), new("XFBX_CTR", "132.600")],
                Exemptions =
                [
                    new($"{Rome}_TWR", ExemptionKind.FreeSpeed, null),
                    new("XFBX_CTR", ExemptionKind.Other, "fo-test vectors"),
                ],
            },
            token);

        Assert.True(sent.GetProperty("atcArchiveAvailable").GetBoolean());
        Assert.Equal(
            [($"{Rome}_TWR", "Proposed", "118.705"), ($"{Milan}_APP", "Removed", "126.750"), ("XFBX_CTR", "Added", "132.600")],
            sent.GetProperty("atcContacts").EnumerateArray().Select(item => (
                item.GetProperty("callsign").GetString(),
                item.GetProperty("origin").GetString(),
                item.GetProperty("frequency").GetString())));
        Assert.Equal(
            ["Online", "NotOnline"],
            sent.GetProperty("exemptions").EnumerateArray().Select(item => item.GetProperty("status").GetString()));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A table shaped like vIPI's <c>AtcSessions</c> and the view of the contract over it, in the test database; dropped when
    /// the test is done. The view is written as the plan writes it, so a change to the contract is a change here.
    /// </summary>
    private async Task<IAsyncDisposable> FakeArchiveAsync(
        CancellationToken cancellationToken,
        params (long Id, string Callsign, string? Frequency, DateTime Start, DateTime? End, bool Outside)[] sessions)
    {
        var connection = new MySqlConnector.MySqlConnection(mariaDb.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        async Task RunAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await RunAsync("DROP VIEW IF EXISTS v_share_atc_sessions");
        await RunAsync("DROP TABLE IF EXISTS fo_test_atc_sessions");
        await RunAsync("""
            CREATE TABLE fo_test_atc_sessions (
                SessionId bigint NOT NULL PRIMARY KEY, UserId int NOT NULL, Callsign varchar(32) NOT NULL, Position varchar(16) NULL,
                Frequency varchar(16) NULL, StartUtc datetime(6) NOT NULL, EndUtc datetime(6) NULL, DurationSeconds int NOT NULL,
                Rating int NULL, IsOutsideDivision tinyint(1) NOT NULL, TrafficCount int NOT NULL DEFAULT 0)
            """);
        await RunAsync("""
            CREATE OR REPLACE SQL SECURITY DEFINER VIEW v_share_atc_sessions AS
            SELECT SessionId AS session_id, UserId AS vid, Callsign AS callsign, Position AS position, Frequency AS frequency,
                   StartUtc AS start_utc, EndUtc AS end_utc, DurationSeconds AS duration_seconds, Rating AS rating,
                   IsOutsideDivision AS is_outside_division
            FROM fo_test_atc_sessions
            """);

        foreach (var session in sessions)
        {
            await RunAsync(
                "INSERT INTO fo_test_atc_sessions VALUES (@id, 780099, @callsign, NULL, @frequency, @start, @end, 3600, 5, @outside, 0)",
                ("@id", session.Id),
                ("@callsign", session.Callsign),
                ("@frequency", session.Frequency),
                ("@start", session.Start),
                ("@end", session.End),
                ("@outside", session.Outside));
        }

        return new Dropping(connection, cancellationToken);
    }

    private sealed class Dropping(MySqlConnector.MySqlConnection connection, CancellationToken cancellationToken) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "DROP VIEW IF EXISTS v_share_atc_sessions; DROP TABLE IF EXISTS fo_test_atc_sessions;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await connection.DisposeAsync();
        }
    }

    private static string Reports(long tourId) => $"/api/flightops/tours/{tourId}/reports";

    private static PirepWriteDto Payload(long legId, long sessionId) => new(
        legId,
        [sessionId],
        IsDiversion: false,
        DiversionIcao: null,
        DiversionReason: null,
        DiversionNote: null,
        Sid: null,
        Star: null,
        Approach: null,
        PilotRemarks: null,
        RowVersion: default);

    private static string Colour(JsonElement mine, long legId) =>
        mine.GetProperty("legs").EnumerateArray()
            .Single(leg => leg.GetProperty("id").GetInt64() == legId)
            .GetProperty("progress").GetString()!;

    /// <summary>A Sequential tour flying ahead, released two days ago, with two legs: Rome–Milan and Milan–London.</summary>
    private async Task<(long TourId, long[] Legs)> ReadyTourAsync(HttpClient coordinator, int dailyLimit, CancellationToken cancellationToken)
    {
        var slug = $"fo-test-pirep-{Guid.NewGuid():N}"[..27];
        var tour = await CreatedAsync(
            coordinator,
            TourEndpoints.Pattern,
            new TourWriteDto(
                OwnerDepartment: Department.FOD,
                IsTemplate: false,
                Slug: slug,
                Kind: TourKind.Sequential,
                Title: Text(slug),
                Summary: Text($"{slug} summary"),
                Briefing: null,
                CoverMediaId: null,
                BannerMediaId: null,
                ShowPreview: false,
                ReleaseAt: DateTime.UtcNow.AddDays(-4),
                CloseAt: DateTime.UtcNow.AddDays(60),
                ReportWindowDays: null,
                Progression: TourProgression.FlyAhead,
                HubRotationOrder: null,
                RequiresProcedures: false,
                DailyLegLimit: dailyLimit,
                MinPilotRating: 2,
                ReferenceAircraftIcao: null,
                RequiredNm: null,
                AllowedAircraft: null,
                AwardId: null,
                RowVersion: default),
            cancellationToken);
        var tourId = Id(tour);
        _tours.Add(tourId);

        var legs = LegsUri(tourId);
        await OkAsync(await coordinator.PostAsJsonAsync(legs, Leg(Rome, Milan), cancellationToken), cancellationToken);
        var grid = await OkAsync(await coordinator.PostAsJsonAsync(legs, Leg(Milan, London), cancellationToken), cancellationToken);

        await OkAsync(
            await coordinator.PostAsJsonAsync(
                $"{TourEndpoints.Pattern}/{tourId}/status",
                new TourStatusRequest(TourStatusAction.Ready),
                cancellationToken),
            cancellationToken);

        return (tourId, [.. grid.GetProperty("legs").EnumerateArray().Select(Id)]);
    }

    private async Task<string> RuleAsync(HttpClient coordinator, long tourId, int radiusNm, CancellationToken cancellationToken)
    {
        var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var rule = await CreatedAsync(
            coordinator,
            RuleEndpoints.RulesPattern,
            new TourRuleWriteDto(
                tourId,
                code,
                Text(code),
                Text($"The rule {code}."),
                AmendsRuleId: null,
                CheckCatalog.LandingAtArrival,
                JsonNode.Parse(string.Create(CultureInfo.InvariantCulture, $$"""{"radiusNm":{{radiusNm}}}""")),
                ErrorIds: [],
                Sort: 0,
                Retired: false,
                RowVersion: default),
            cancellationToken);
        _rules.Add(Id(rule));
        return code;
    }

    private static string LegsUri(long tourId) => $"/api/flightops/tours/{tourId}/legs";

    private static LegWriteDto Leg(string from, string to) =>
        new(from, to, Callsigns: ["XAA100"], FlightNumbers: null, Aircraft: null, ReleaseAt: null, ChangeReason: null, RowVersion: default);

    private static async Task RefusedAsync(
        HttpClient client,
        string uri,
        PirepWriteDto payload,
        string field,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {body}");

        var errors = JsonDocument.Parse(body).RootElement.GetProperty("errors");
        Assert.True(
            errors.TryGetProperty(field, out var keys) && keys.EnumerateArray().Any(item => item.GetString() == key),
            body);
    }

    private static Core.Localization.Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    /// <summary>Every row of the set, drafts and all: what a test that cleans up after itself needs to find.</summary>
    private static IQueryable<T> Everything<T>(FlightOpsDbContext database)
        where T : class => Core.Data.Crud.CrudSource.BackOffice<T>(database);

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    private static async Task<JsonElement> CreatedAsync<T>(HttpClient client, string uri, T payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken) => SignedInAsync(_host, vid, cancellationToken);

    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> host, int vid, CancellationToken cancellationToken)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(
            new Uri(string.Create(CultureInfo.InvariantCulture, $"{TestSignInStartupFilter.Path}?vid={vid}"), UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>A member of the staff with a position, or a pilot with a rating and nothing else.</summary>
    private async Task SeedUserAsync(int vid, string? staffPosition, int? rating, CancellationToken cancellationToken)
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
        user.LastName = "Pirep";
        user.IsStaff = staffPosition is not null;
        user.RatingPilot = rating;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (staffPosition is not null
            && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == staffPosition, cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(staffPosition, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = staffPosition,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The flights the tracker double knows, by session: each test puts its own.</summary>
    private sealed class FlightShelf
    {
        private static long _nextId = 9_100_000_000 + Random.Shared.Next(0, 1_000_000) * 100L;

        public ConcurrentDictionary<long, (IvaoTrackerSessionDto Session, IvaoFlightPlanDto Plan, IReadOnlyList<IvaoTrackPointDto> Track)> Sessions { get; } = new();

        /// <summary>A flight of an A320: twenty minutes on the ground, an hour in the air.</summary>
        public long Add(int vid, string callsign, string departure, string arrival, DateTime takeoff)
        {
            var id = Interlocked.Increment(ref _nextId);
            var start = takeoff.AddMinutes(-20);
            var session = new IvaoTrackerSessionDto(id, vid, callsign, start, TimeSpan.FromMinutes(100), true, departure, arrival, "A320", "{}");
            var plan = new IvaoFlightPlanDto(
                id * 10, 1, start.AddMinutes(-10), departure, arrival, null, null, "A320", "M", "SDFG", "S", "I", "S",
                "F340", "N0450", "DCT", null, null, null, """{"revision":1}""");
            IReadOnlyList<IvaoTrackPointDto> track =
            [
                new(start, 0, 0, 100, 0, 0, OnGround: true, "Boarding", "2000"),
                new(takeoff, 0, 0, 500, 150, 0, OnGround: false, "Departing", "2000"),
                new(takeoff.AddMinutes(60), 0, 0, 100, 10, 0, OnGround: true, "On Blocks", "2000"),
            ];

            Sessions[id] = (session, plan, track);
            return id;
        }
    }

    /// <summary>The IVAO client of the tests with the tracker answered from the shelf, filtered the way the API filters.</summary>
    private sealed class TrackerDouble(FixtureIvaoApiClient inner, FlightShelf shelf) : IIvaoApiClient
    {
        public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(IvaoSessionQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoTrackerSessionDto>?>(
            [
                .. shelf.Sessions.Values
                    .Select(entry => entry.Session)
                    .Where(session => session.Vid == query.Vid && session.StartedAt >= query.FromUtc && session.StartedAt <= query.ToUtc)
                    .Where(session => query.DepartureIcao is null || session.DepartureIcao == query.DepartureIcao)
                    .Where(session => query.ArrivalIcao is null || session.ArrivalIcao == query.ArrivalIcao),
            ]);

        public Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(long sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoFlightPlanDto>?>(shelf.Sessions.TryGetValue(sessionId, out var entry) ? [entry.Plan] : []);

        public Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(long sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IvaoTrackPointDto>?>(shelf.Sessions.TryGetValue(sessionId, out var entry) ? entry.Track : []);

        public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default) =>
            inner.GetCentersAsync(countryId, cancellationToken);

        public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(string? countryId, bool includeRunways = true, CancellationToken cancellationToken = default) =>
            inner.GetAirportsAsync(countryId, includeRunways, cancellationToken);

        public Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(string icao, CancellationToken cancellationToken = default) =>
            inner.GetRunwaysAsync(icao, cancellationToken);

        public Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(CancellationToken cancellationToken = default) =>
            inner.GetAircraftTypesAsync(cancellationToken);

        public Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)> GetFlightPlanVocabulariesAsync(
            CancellationToken cancellationToken = default) =>
            inner.GetFlightPlanVocabulariesAsync(cancellationToken);

        public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
            inner.GetMeAsync(accessToken, cancellationToken);

        public Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default) =>
            inner.GetMetarAsync(icao, cancellationToken);

        public Task<IvaoNetworkStatus> GetNetworkStatusAsync(IvaoAirspace airspace, CancellationToken cancellationToken = default) =>
            inner.GetNetworkStatusAsync(airspace, cancellationToken);
    }
}
