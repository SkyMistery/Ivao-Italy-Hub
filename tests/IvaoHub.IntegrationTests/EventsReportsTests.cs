using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Airspace;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registers;
using IvaoHub.Modules.Events.Reports;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The reports of support (M4, E14a; design §1.8, §5.2, §5.3, §7.2, §7.3; note 2026-10-17-il-pirep-di-supporto), through the real host with
/// the division file of this repository — its grants to the members' department, nothing given by hand —, and the evening of Rome Fiumicino
/// the fixtures recorded (<c>tests/fixtures/ivao/tracker-airport-LIRF.json</c>), moved to an evening two days ago:
/// <list type="bullet">
/// <item>the «done when» of E14a: a pilot's report, checked on the fixtures, is accepted by whoever validates — the members' department —,
/// the member is told, the staff's queue counts it while it waits, and the statistics count it; the events' staff read it and do not decide
/// it;</item>
/// <item>nobody validates their own report, the super administrator included;</item>
/// <item>whoever validates touches nothing else of the events;</item>
/// <item>the check finds the flights and the shifts of the fixtures, and says «not checked» with the source off;</item>
/// <item>a report after <c>reportDays</c> is refused, as a flight touching none of the event's airports;</item>
/// <item>a decision and a correction in the same instant meet at the lock of the report's row.</item>
/// </list>
/// <para>The network is a double of the core's source that answers from the fixture's rows, moved in time, for the class's people as the
/// evening's own: the pilot flies as 761023 (TST004, LIRF–LICR), the controller as 761024 (LIRF_TWR). No archive of the sessions: the shifts
/// are checked on the tracker, as on a division at <c>atcData: none</c>. ⚠️ The people are 761230–761236 (E14a's range, 761230–761259): two of
/// the members' department, the super administrator, a pilot, a controller, the coordinator of the events, and a pilot whose data is erased —
/// a new VID at every run would be cleaner, but the erasure leaves a pseudonym and no person, so the next run seeds them again.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsReportsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int MdVid = 761230;
    private const int OtherMdVid = 761231;
    private const int SuperadminVid = 761232;
    private const int PilotVid = 761233;
    private const int ControllerVid = 761234;
    private const int CoordinatorVid = 761235;
    private const int ErasedVid = 761236;

    private static readonly int[] People = [MdVid, OtherMdVid, SuperadminVid, PilotVid, ControllerVid, CoordinatorVid, ErasedVid];

    /// <summary>Who the class's people fly and control as, on the evening of the fixtures.</summary>
    private static readonly Dictionary<int, int> AsTheEvening = new() { [PilotVid] = 761023, [ControllerVid] = 761024, [ErasedVid] = 761023 };

    private const string Home = "LIRF";
    private const string Away = "LICR";
    private const string Elsewhere = "XER1";
    private const string Farther = "XER2";

    private const string SlugStem = "evt-test-e14a";

    /// <summary>The start of the evening the fixtures recorded.</summary>
    private static readonly DateTime FixtureStart = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Locales = ["it", "en"];

    private readonly Sources _sources = new();
    private readonly List<string> _seededAirports = [];
    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private string? _settingsBefore;

    /// <summary>The evening of this class: two days ago at ten, so that every session falls on the same day as the event.</summary>
    private static DateTime Start => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-2).AddHours(10), DateTimeKind.Utc);

    public async ValueTask InitializeAsync()
    {
        _sources.Shift = Start - FixtureStart;
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(_sources);
            services.AddScoped<INetworkSessionSource>(provider => new EveningTracker(provider.GetRequiredService<HubPaths>(), _sources));
            services.AddScoped<IAtcActivitySource>(_ => new UnavailableAtcActivitySource());
        }));
        var token = TestContext.Current.CancellationToken;

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);
        await ForgetMailsAsync(token);

        await SeedPersonAsync(MdVid, $"{Division().Code}-MC", superadmin: false, token);
        await SeedPersonAsync(OtherMdVid, $"{Division().Code}-MAC", superadmin: false, token);
        await SeedPersonAsync(SuperadminVid, position: null, superadmin: true, token);
        await SeedPersonAsync(PilotVid, position: null, superadmin: false, token);
        await SeedPersonAsync(ControllerVid, position: null, superadmin: false, token);
        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", superadmin: false, token);
        await SeedPersonAsync(ErasedVid, position: null, superadmin: false, token);
        await SeedAirportsAsync(token);

        _settingsBefore = await WriteSettingsAsync("""{"reportDays":14,"attendanceMinimumMinutes":30}""", token);
    }

    public async ValueTask DisposeAsync()
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, CancellationToken.None);
        await ForgetMailsAsync(CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.Users.Where(user => People.Contains(user.Vid)).ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false));
            await database.IvaoAirports.Where(airport => _seededAirports.Contains(airport.Icao)).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of E14a: the pilot sends their flight of the evening, the check finds it on the fixtures — the session, its minutes, the
    /// distance between the two airports — and the report is valid; the events' coordinator reads it and may not decide it; the members'
    /// department finds it in its queue and in the list, accepts it, and the pilot is told and reads it accepted, no longer to correct. The
    /// statistics count it, and the decision changes to rejected — with a note, required.
    /// </summary>
    [Fact]
    public async Task ACheckedPilotReportIsAcceptedByWhoeverValidates()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("done", Start, Start.AddHours(2), [Home], token);

        using var pilot = await SignedInAsync(PilotVid, token);
        using var md = await SignedInAsync(MdVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // The event is open to the pilot, with its airport.
        var open = Assert.Single((await OkAsync(await pilot.GetAsync(ReportEndpoints.MinePattern, token), token)).GetProperty("open").EnumerateArray(),
            entry => entry.GetProperty("id").GetInt64() == id);
        Assert.Equal([Home], open.GetProperty("airports").EnumerateArray().Select(airport => airport.GetString()));

        var sent = await CreatedAsync(await SendAsync(pilot, id, "pilot", [Flight("tst004", Home, Away, Start)], token), token);
        Assert.Equal(("Submitted", "Valid", true), (sent.GetProperty("state").GetString(), sent.GetProperty("verification").GetString(), sent.GetProperty("correctable").GetBoolean()));
        var item = Assert.Single(sent.GetProperty("items").EnumerateArray());
        Assert.Equal("TST004", item.GetProperty("callsign").GetString());
        Assert.Equal(1000007L, item.GetProperty("sessionId").GetInt64());
        Assert.Equal(139, item.GetProperty("minutes").GetInt32());
        Assert.Equal(await DistanceAsync(Home, Away, token), item.GetProperty("distanceNm").GetDecimal());
        Assert.True(item.GetProperty("verified").GetBoolean());
        var reportId = sent.GetProperty("id").GetInt64();

        // The events' staff read the reports of their events, and validate none.
        Assert.False(Row(await PageAsync(coordinator, ReportsUri(id), token), reportId).GetProperty("decidable").GetBoolean());
        using (var refused = await DecideAsync(coordinator, reportId, "Accepted", note: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The members' department: the queue counts it, the list may decide it, with each item's check.
        Assert.Equal(1, Queue(await OkAsync(await md.GetAsync(BlockUri(StaffQueueProvider.BlockType), token), token), id).GetProperty("reports").GetInt32());
        var row = Row(await PageAsync(md, ReportsUri(id), token), reportId);
        Assert.True(row.GetProperty("decidable").GetBoolean());
        Assert.Equal((PilotVid, "Valid"), (row.GetProperty("member").GetProperty("vid").GetInt32(), row.GetProperty("verification").GetString()));
        Assert.Equal(1000007L, Assert.Single(row.GetProperty("items").EnumerateArray()).GetProperty("sessionId").GetInt64());

        await NoContentAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token);

        var report = await ReportAsync(reportId, token);
        Assert.Equal((ReportState.Accepted, (int?)MdVid), (report.State, report.DecidedBy));
        Assert.Single(await MailsAsync(PilotVid, token));
        Assert.DoesNotContain(
            (await OkAsync(await md.GetAsync(BlockUri(StaffQueueProvider.BlockType), token), token)).GetProperty("events").EnumerateArray(),
            entry => entry.GetProperty("id").GetInt64() == id);

        // The pilot reads it accepted, and corrects it no more.
        var mine = Assert.Single((await OkAsync(await pilot.GetAsync(ReportEndpoints.MinePattern, token), token)).GetProperty("reports").EnumerateArray(),
            entry => entry.GetProperty("id").GetInt64() == reportId);
        Assert.Equal(("Accepted", false), (mine.GetProperty("state").GetString(), mine.GetProperty("correctable").GetBoolean()));
        Assert.Equal(
            [ReportRules.DecidedKey],
            (await RefusedAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token))["id"]);

        // The statistics count it, once the coordinator recalculates them.
        await OkAsync(await coordinator.PostAsync(new Uri($"/api/events/events/{id}/stats", UriKind.Relative), content: null, token), token);
        var stats = await StatsAsync(id, token);
        Assert.Equal((1, 1), (stats.ReportsSubmitted, stats.ReportsAccepted));

        // An event that started two days ago is kept (Carmine's answer on reading 13).
        Assert.Equal([EventSaving.StartedKey], (await RefusedAsync(await coordinator.DeleteAsync(new Uri($"/api/events/events/{id}", UriKind.Relative), token), token))["id"]);

        // A decision changes (choice 1): to reject, a note.
        Assert.Equal(["errors.required"], (await RefusedAsync(await DecideAsync(md, reportId, "Rejected", note: null, token), token))["note"]);
        Assert.Equal([ReportDecisions.SameKey], (await RefusedAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token))["state"]);
        await NoContentAsync(await DecideAsync(md, reportId, "Rejected", $"{SlugStem} wrong evening", token), token);
        Assert.Equal((ReportState.Rejected, $"{SlugStem} wrong evening"), ((await ReportAsync(reportId, token)).State, (await ReportAsync(reportId, token)).DecisionNote));
        Assert.Equal(2, (await MailsAsync(PilotVid, token)).Count);
    }

    /// <summary>
    /// Nobody validates their own report (§5.3), the super administrator included — <c>DeniedToStakeholder</c> on <c>EventReports.Edit</c>, no
    /// rule of the module —: the members' coordinator and the super administrator each send one and are refused on it, read the other's in
    /// their queue and never their own, and decide each other's.
    /// </summary>
    [Fact]
    public async Task NobodyValidatesTheirOwnReportTheSuperAdministratorIncluded()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("own", Start, Start.AddHours(2), [Home], token);

        using var md = await SignedInAsync(MdVid, token);
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var mdReport = (await CreatedAsync(await SendAsync(md, id, "atc", [Shift("LIRF_APP", Start, Start.AddHours(1))], token), token)).GetProperty("id").GetInt64();
        var superReport = (await CreatedAsync(await SendAsync(superadmin, id, "pilot", [Flight("TST999", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();

        Assert.Equal(1, Queue(await OkAsync(await md.GetAsync(BlockUri(StaffQueueProvider.BlockType), token), token), id).GetProperty("reports").GetInt32());
        Assert.Equal(1, Queue(await OkAsync(await superadmin.GetAsync(BlockUri(StaffQueueProvider.BlockType), token), token), id).GetProperty("reports").GetInt32());

        using (var own = await DecideAsync(md, mdReport, "Accepted", note: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        using (var own = await DecideAsync(superadmin, superReport, "Accepted", note: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        Assert.False(Row(await PageAsync(md, ReportsUri(id), token), mdReport).GetProperty("decidable").GetBoolean());
        Assert.True(Row(await PageAsync(md, ReportsUri(id), token), superReport).GetProperty("decidable").GetBoolean());
        Assert.False(Row(await PageAsync(superadmin, ReportsUri(id), token), superReport).GetProperty("decidable").GetBoolean());

        await NoContentAsync(await DecideAsync(md, superReport, "Rejected", $"{SlugStem} not flown", token), token);
        await NoContentAsync(await DecideAsync(superadmin, mdReport, "Accepted", note: null, token), token);

        Assert.Equal((ReportState.Rejected, (int?)MdVid), ((await ReportAsync(superReport, token)).State, (await ReportAsync(superReport, token)).DecidedBy));
        Assert.Equal((ReportState.Accepted, (int?)SuperadminVid), ((await ReportAsync(mdReport, token)).State, (await ReportAsync(mdReport, token)).DecidedBy));
    }

    /// <summary>
    /// Whoever validates touches nothing else (§6.2): the members' department decides a report — and only the columns of the decision change,
    /// the items and the event as they were — and is refused on the event, its statistics, its shifts and its bookings.
    /// </summary>
    [Fact]
    public async Task WhoeverValidatesTouchesNothingElse()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("md", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var md = await SignedInAsync(MdVid, token);

        var reportId = (await CreatedAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();
        var before = await ReportAsync(reportId, token);
        var itemsBefore = await ItemsAsync(reportId, token);
        var eventBefore = await EventAsync(id, token);

        await NoContentAsync(await DecideAsync(md, reportId, "Accepted", note: $"{SlugStem} fine", token), token);

        var after = await ReportAsync(reportId, token);
        Assert.Equal(
            (before.Vid, before.Kind, before.Verification, before.SubmittedAt, before.CheckedAt),
            (after.Vid, after.Kind, after.Verification, after.SubmittedAt, after.CheckedAt));
        Assert.Equal(
            itemsBefore.Select(item => (item.Id, item.Callsign, item.SessionId, item.Minutes, item.Verified)),
            (await ItemsAsync(reportId, token)).Select(item => (item.Id, item.Callsign, item.SessionId, item.Minutes, item.Verified)));
        Assert.Equal(eventBefore.RowVersion, (await EventAsync(id, token)).RowVersion);

        foreach (var uri in new[]
                 {
                     $"/api/events/events/{id}/stats",
                     $"/api/events/events/{id}/cancel",
                     "/api/events/atc/shifts/1/attendance",
                     "/api/events/bookings/1/excuse",
                 })
        {
            using var refused = await md.PostAsJsonAsync(uri, new { note = $"{SlugStem} no" }, token);
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{uri}: {refused.StatusCode}");
        }

        using (var write = await md.DeleteAsync(new Uri($"/api/events/events/{id}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        }
    }

    /// <summary>
    /// The check on the fixtures (§5.3, reading 5): a flight flown is found with its session and one never flown is not — the report is partial
    /// —; a shift on the tower of the evening has its forty minutes, past the thirty the settings ask, and a later one none — partial too —.
    /// With the network down, sending again leaves every item not checked and the report unavailable, never a guess.
    /// </summary>
    [Fact]
    public async Task TheCheckFindsTheEveningAndSaysUnavailableWithTheSourceOff()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("check", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var controller = await SignedInAsync(ControllerVid, token);

        var flights = await CreatedAsync(
            await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start), Flight("TST004", Away, Home, Start)], token),
            token);
        Assert.Equal("Partial", flights.GetProperty("verification").GetString());
        Assert.Equal([true, false], flights.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("verified").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, flights.GetProperty("items")[1].GetProperty("sessionId").ValueKind);
        Assert.Equal(await DistanceAsync(Away, Home, token), flights.GetProperty("items")[1].GetProperty("distanceNm").GetDecimal());

        var shifts = await CreatedAsync(
            await SendAsync(controller, id, "atc", [Shift("lirf_twr", Start.AddMinutes(30), Start.AddMinutes(90)), Shift("LIRF_TWR", Start.AddMinutes(90), Start.AddHours(2))], token),
            token);
        Assert.Equal("Partial", shifts.GetProperty("verification").GetString());
        Assert.Equal([(40, true), (0, false)], shifts.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("minutes").GetInt32(), item.GetProperty("verified").GetBoolean())));

        // Only the first shift: valid.
        var one = await OkAsync(await SendAsync(controller, id, "atc", [Shift("LIRF_TWR", Start.AddMinutes(30), Start.AddMinutes(90))], token), token);
        Assert.Equal("Valid", one.GetProperty("verification").GetString());
        Assert.Single(await ItemsAsync(one.GetProperty("id").GetInt64(), token));

        _sources.TrackerAnswers = false;
        try
        {
            var down = await OkAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token);
            Assert.Equal("Unavailable", down.GetProperty("verification").GetString());
            var item = Assert.Single(down.GetProperty("items").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("verified").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("sessionId").ValueKind);

            // The distance is a fact of the leg: written without the network.
            Assert.Equal(await DistanceAsync(Home, Away, token), item.GetProperty("distanceNm").GetDecimal());

            var atcDown = await OkAsync(await SendAsync(controller, id, "atc", [Shift("LIRF_TWR", Start.AddMinutes(30), Start.AddMinutes(90))], token), token);
            Assert.Equal("Unavailable", atcDown.GetProperty("verification").GetString());
        }
        finally
        {
            _sources.TrackerAnswers = true;
        }
    }

    /// <summary>
    /// A send asks the network once, however many items it holds (#322, item 1): three flights on three routes and three shifts at three hours are
    /// one search each, never one per item — the network is the whole hub's, and the members' limit counts requests, not calls.
    /// </summary>
    [Fact]
    public async Task ASendAsksTheNetworkOnceForAllItsItems()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("once", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var controller = await SignedInAsync(ControllerVid, token);

        var flights = await CreatedAsync(
            await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start), Flight("TST004", Away, Home, Start), Flight("TST004", Home, Elsewhere, Start)], token),
            token);
        Assert.Equal([true, false, false], flights.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("verified").GetBoolean()));
        Assert.Equal(1, _sources.Asked);

        var shifts = await CreatedAsync(
            await SendAsync(
                controller,
                id,
                "atc",
                [Shift("LIRF_TWR", Start, Start.AddMinutes(30)), Shift("LIRF_TWR", Start.AddMinutes(30), Start.AddMinutes(90)), Shift("LIRF_TWR", Start.AddMinutes(90), Start.AddHours(2))],
                token),
            token);
        Assert.Equal([40, 0], shifts.GetProperty("items").EnumerateArray().Skip(1).Select(item => item.GetProperty("minutes").GetInt32()));
        Assert.Equal(2, _sources.Asked);
    }

    /// <summary>
    /// The one search's answer given out by day (#322, item 1): over an event of two days, the flight of the second day is not credited with the
    /// session of the first, on the same route, even listed before it.
    /// </summary>
    [Fact]
    public async Task AFlightIsCreditedOnlyWithASessionOfItsOwnDay()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("two-days", Start, Start.AddDays(1).AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var flights = await CreatedAsync(
            await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start.AddDays(1)), Flight("TST004", Home, Away, Start)], token),
            token);
        Assert.Equal([false, true], flights.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("verified").GetBoolean()));
        Assert.Equal(1, _sources.Asked);
    }

    /// <summary>
    /// An answer as long as the most a search reads (#322, item 1) may have been cut, and the session left out could be the one the item flew:
    /// the item is not asked and the report unavailable, never «not flown».
    /// </summary>
    [Fact]
    public async Task AnAnswerAsLongAsTheMostASearchReadsLeavesTheItemsNotAsked()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("cut", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);

        _sources.Padding = NetworkSessionQuery.MaxLimit;
        var cut = await CreatedAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token);
        Assert.Equal("Unavailable", cut.GetProperty("verification").GetString());
        Assert.Equal(JsonValueKind.Null, Assert.Single(cut.GetProperty("items").EnumerateArray()).GetProperty("verified").ValueKind);
    }

    /// <summary>
    /// The rules of a send (readings 3 and 4): an event over more than <c>reportDays</c> ago takes no report, and is not offered; a flight
    /// touching none of the event's airports, an airport the hub does not know, a day outside the event and shifts that meet are refused on
    /// their fields; an event not over yet takes none either.
    /// </summary>
    [Fact]
    public async Task AReportPastReportDaysIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var old = Start.AddDays(-20);
        var past = await EventRowAsync("past", old, old.AddHours(2), [Home], token);
        var coming = await EventRowAsync("coming", Start.AddDays(5), Start.AddDays(5).AddHours(2), [Home], token);
        var id = await EventRowAsync("rules", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);

        Assert.Equal([ReportRules.ClosedKey], (await RefusedAsync(await SendAsync(pilot, past, "pilot", [Flight("TST004", Home, Away, old)], token), token))["id"]);
        Assert.Equal([ReportRules.ClosedKey], (await RefusedAsync(await SendAsync(pilot, coming, "pilot", [Flight("TST004", Home, Away, Start)], token), token))["id"]);

        var open = (await OkAsync(await pilot.GetAsync(ReportEndpoints.MinePattern, token), token)).GetProperty("open").EnumerateArray()
            .Select(entry => entry.GetProperty("id").GetInt64())
            .ToList();
        Assert.Contains(id, open);
        Assert.DoesNotContain(past, open);
        Assert.DoesNotContain(coming, open);

        var refused = await RefusedAsync(
            await SendAsync(pilot, id, "pilot", [Flight("TST004", Elsewhere, Farther, Start), Flight("TST004", Home, "ZZZZ", Start), Flight("TST004", Home, Away, Start.AddDays(-3))], token),
            token);
        Assert.Equal([ReportRules.NotAtTheEventKey], refused["items[0].arrivalIcao"]);
        Assert.Equal([ReportRules.AirportUnknownKey], refused["items[1].arrivalIcao"]);
        Assert.Equal([ReportRules.OutsideTheEventKey], refused["items[2].flightDay"]);

        Assert.Equal(
            [ReportRules.OverlapKey],
            (await RefusedAsync(await SendAsync(pilot, id, "atc", [Shift("LIRF_TWR", Start, Start.AddHours(1)), Shift("LIRF_APP", Start.AddMinutes(30), Start.AddHours(2))], token), token))["items[1].fromUtc"]);
        Assert.Equal([ReportRules.NoItemsKey], (await RefusedAsync(await SendAsync(pilot, id, "pilot", [], token), token))["items"]);

        // Nothing was written.
        Assert.Empty(await ReportsOfAsync(id, token));
    }

    /// <summary>
    /// A decision and a correction in the same instant (reading 6): a transaction of the test holds the report's row as a decision does, the
    /// member's correction waits for it in the database after its check, and the decision commits — the correction is then refused as decided,
    /// and the items are the ones the decision saw.
    /// </summary>
    [Fact]
    public async Task ACorrectionWaitsForADecisionInTheSameInstantAndIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("race", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var reportId = (await CreatedAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();
        var itemsBefore = (await ItemsAsync(reportId, token)).Select(item => item.Id).ToList();

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM evt_reports WHERE id = {reportId} FOR UPDATE", token);

        var correction = SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start), Flight("TST004", Away, Home, Start)], token);
        await WaitForALockWaitAsync(token);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE evt_reports SET state = 'Accepted', decided_by = {OtherMdVid}, decided_at = {DateTime.UtcNow} WHERE id = {reportId}",
            token);
        await transaction.CommitAsync(token);

        Assert.Equal([ReportRules.DecidedKey], (await RefusedAsync(await correction, token))["id"]);
        Assert.Equal(itemsBefore, (await ItemsAsync(reportId, token)).Select(item => item.Id));
    }

    /// <summary>
    /// The erasure of a member's data (§11, reading 12): their report stays with the pseudonym — the statistics and the rules of award count
    /// its items —, without the validator's note about them, and its items without the callsign and the session; an item a correction took
    /// away leaves its callsign nowhere in the audit log either (E10n). The preview says the reports kept.
    /// </summary>
    [Fact]
    public async Task AnErasedMemberLeavesTheirReportsWithoutTheirWords()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("erased", Start, Start.AddHours(2), [Home], token);
        using var erased = await SignedInAsync(ErasedVid, token);
        using var md = await SignedInAsync(MdVid, token);
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var reportId = (await CreatedAsync(await SendAsync(erased, id, "pilot", [Flight("XEZ146", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();
        await OkAsync(await SendAsync(erased, id, "pilot", [Flight("XEZ147", Home, Away, Start)], token), token);
        var shiftsId = (await CreatedAsync(await SendAsync(erased, id, "atc", [Shift("XEZ1_APP", Start, Start.AddHours(1))], token), token)).GetProperty("id").GetInt64();
        await NoContentAsync(await DecideAsync(md, reportId, "Rejected", $"{SlugStem} about the member", token), token);
        Assert.True(await AuditMentionsAsync("XEZ146", token), "Without the callsign in the audit log, the check below would prove nothing.");

        var preview = await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{ErasedVid}", token), token);
        Assert.Equal(2, Assert.Single(preview.GetProperty("lines").EnumerateArray(), line => line.GetProperty("key").GetString() == "events:erasure.reports").GetProperty("count").GetInt32());

        var pseudonym = (await OkAsync(await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{ErasedVid}", UriKind.Relative), content: null, token), token))
            .GetProperty("pseudonym").GetInt32();

        var report = await ReportAsync(reportId, token);
        Assert.Equal((pseudonym, ReportState.Rejected, (string?)null), (report.Vid, report.State, report.DecisionNote));
        var item = Assert.Single(await ItemsAsync(reportId, token));
        Assert.Equal((pseudonym, string.Empty, (long?)null, Home, Away, true), (item.Vid, item.Callsign, item.SessionId, item.DepartureIcao, item.ArrivalIcao, item.Verified));
        Assert.False(await AuditMentionsAsync("XEZ146", token) || await AuditMentionsAsync("XEZ147", token) || await AuditMentionsAsync($"{SlugStem} about the member", token));

        // A shift's callsign names a position, not the person: it stays (Carmine's answer on reading 12).
        var shift = Assert.Single(await ItemsAsync(shiftsId, token));
        Assert.Equal((pseudonym, "XEZ1_APP"), (shift.Vid, shift.Callsign));
    }

    /// <summary>
    /// A decision on what the validator saw (the review of #289, point 1): two validators open the same report; one accepts it, and the other's
    /// rejection, taken on the report as it was, answers 409 and changes nothing — one decision, one mail. And a member's correction after the
    /// validator read the report: their acceptance, taken on items they never saw, answers 409 too.
    /// </summary>
    [Fact]
    public async Task ADecisionOnAReportThatMovedSinceItWasReadIsAConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("moved", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var md = await SignedInAsync(MdVid, token);
        using var otherMd = await SignedInAsync(OtherMdVid, token);

        var reportId = (await CreatedAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();
        var bothSaw = await ReportAsync(reportId, token);

        await NoContentAsync(await DecideAsync(md, reportId, "Accepted", note: null, token, seen: bothSaw), token);
        using (var late = await DecideAsync(otherMd, reportId, "Rejected", $"{SlugStem} too late", token, seen: bothSaw))
        {
            Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        }

        var report = await ReportAsync(reportId, token);
        Assert.Equal((ReportState.Accepted, (int?)MdVid, (string?)null), (report.State, report.DecidedBy, report.DecisionNote));
        Assert.Single(await MailsAsync(PilotVid, token));

        // The member corrects a report the validator already read: their acceptance is on items they never saw.
        var other = await EventRowAsync("moved-again", Start, Start.AddHours(2), [Home], token);
        var secondId = (await CreatedAsync(await SendAsync(pilot, other, "pilot", [Flight("TST004", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();
        var read = await ReportAsync(secondId, token);
        await OkAsync(await SendAsync(pilot, other, "pilot", [Flight("TST004", Away, Home, Start)], token), token);
        using (var stale = await DecideAsync(md, secondId, "Accepted", note: null, token, seen: read))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        Assert.Equal(ReportState.Submitted, (await ReportAsync(secondId, token)).State);
    }

    /// <summary>
    /// Two first sends of the same member in the same instant (reading 6; the review of #289, point 5): a transaction of the test writes the
    /// report a first send would, and does not commit; the request waits for it in the database — at the lock it takes on the report's row,
    /// which the other's insert holds —; the test commits, and the request becomes a correction of the report the other wrote: one report, its
    /// items the request's, answered 200. ⚠️ The retry on the unique index (<c>MemberReports.SendAsync</c>) covers the instant between that
    /// read and the insert, which no transaction of a test reaches without a hook in the code; it stays as the database's guarantee.
    /// </summary>
    [Fact]
    public async Task TwoFirstSendsInTheSameInstantMakeOneReport()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("twice", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        var now = DateTime.UtcNow;
        var first = new EventReport
        {
            EventId = id,
            Vid = PilotVid,
            Kind = ReportKind.Pilot,
            SubmittedAt = now,
            CheckedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Reports.Add(first);
        await database.SaveChangesAsync(token);

        var second = SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token);
        await WaitForALockWaitAsync(token);
        await transaction.CommitAsync(token);

        var answer = await OkAsync(await second, token);
        Assert.Equal(first.Id, answer.GetProperty("id").GetInt64());
        var report = Assert.Single(await ReportsOfAsync(id, token));
        Assert.Equal(ReportVerification.Valid, report.Verification);
        Assert.Equal("TST004", Assert.Single(await ItemsAsync(report.Id, token)).Callsign);
    }

    /// <summary>
    /// The reports are the staff's (the review of #289, point 5): a member who holds no <c>EventReports.View</c> is refused the list, and reads
    /// their own on their page alone. And a report of an event whose end was moved later is not decided (point 3).
    /// </summary>
    [Fact]
    public async Task AMemberIsRefusedTheListAndAnEventNotOverTakesNoDecision()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("refused", Start, Start.AddHours(2), [Home], token);
        using var pilot = await SignedInAsync(PilotVid, token);
        using var md = await SignedInAsync(MdVid, token);
        var reportId = (await CreatedAsync(await SendAsync(pilot, id, "pilot", [Flight("TST004", Home, Away, Start)], token), token)).GetProperty("id").GetInt64();

        using (var list = await pilot.GetAsync(ReportsUri(id), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
                .Where(row => row.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.EndsAtUtc, DateTime.UtcNow.AddDays(1)), token);
        }

        Assert.Equal([ReportDecisions.NotOverKey], (await RefusedAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token))["id"]);
        Assert.False(Row(await PageAsync(md, ReportsUri(id), token), reportId).GetProperty("decidable").GetBoolean());
    }

    /// <summary>
    /// What may still be deleted (Carmine's answer on reading 13, on #289): an event started twenty-nine minutes ago goes, one started
    /// thirty-one minutes ago stays, rows or none — whichever comes first —, and an event of twenty minutes, over, with a report sent, stays
    /// though it started less than half an hour ago.
    /// </summary>
    [Fact]
    public async Task AnEventIsKeptOnceItStartedHalfAnHourAgoOrOnceItHasAReport()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var fresh = await EventRowAsync("fresh", now.AddMinutes(-29), now.AddHours(2), [Home], token);
        var started = await EventRowAsync("started", now.AddMinutes(-31), now.AddHours(2), [Home], token);
        var brief = await EventRowAsync("brief", now.AddMinutes(-25), now.AddMinutes(-5), [Home], token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        await CreatedAsync(await SendAsync(pilot, brief, "pilot", [Flight("TST004", Home, Away, now.AddMinutes(-25))], token), token);

        Assert.Equal([EventSaving.StartedKey], (await RefusedAsync(await coordinator.DeleteAsync(new Uri($"/api/events/events/{started}", UriKind.Relative), token), token))["id"]);
        Assert.Equal(["events:errors.eventHasReports"], (await RefusedAsync(await coordinator.DeleteAsync(new Uri($"/api/events/events/{brief}", UriKind.Relative), token), token))["id"]);
        await NoContentAsync(await coordinator.DeleteAsync(new Uri($"/api/events/events/{fresh}", UriKind.Relative), token), token);

        Assert.Empty(await ReportsOfAsync(fresh, token));
        Assert.Single(await ReportsOfAsync(brief, token));
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static object Flight(string callsign, string departure, string arrival, DateTime day) =>
        new { callsign, departureIcao = departure, arrivalIcao = arrival, flightDay = day.Date.ToString("yyyy-MM-dd'T'00:00:00'Z'", CultureInfo.InvariantCulture) };

    private static object Shift(string callsign, DateTime from, DateTime to) => new { callsign, fromUtc = from, toUtc = to };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, long eventId, string kind, object[] items, CancellationToken cancellationToken) =>
        client.PutAsJsonAsync($"{ReportEndpoints.MinePattern}/{eventId}/{kind}", new { items }, cancellationToken);

    /// <summary>
    /// A decision on a report as the staff's page sends it: with what it saw — by default the report as it is now, or <paramref name="seen"/>,
    /// a row read earlier.
    /// </summary>
    private async Task<HttpResponseMessage> DecideAsync(
        HttpClient client,
        long reportId,
        string state,
        string? note,
        CancellationToken cancellationToken,
        EventReport? seen = null)
    {
        seen ??= await ReportAsync(reportId, cancellationToken);
        return await client.PostAsJsonAsync(
            $"{ReportEndpoints.Pattern}/{reportId}/decision",
            new { state, note, seen = seen.State.ToString(), updatedAt = DateTime.SpecifyKind(seen.UpdatedAt, DateTimeKind.Utc) },
            cancellationToken);
    }

    private static string ReportsUri(long eventId) => $"{ReportEndpoints.Pattern}?filter[eventId]={eventId}&pageSize=100";

    private static Uri BlockUri(string type)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return new Uri($"/api/blocks/data/{type}?props={encoded}", UriKind.Relative);
    }

    private static JsonElement Queue(JsonElement answer, long eventId)
    {
        Assert.True(answer.GetProperty("signedIn").GetBoolean());
        return Assert.Single(answer.GetProperty("events").EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == eventId);
    }

    private static JsonElement Row(List<JsonElement> rows, long id) => Assert.Single(rows, row => row.GetProperty("id").GetInt64() == id);

    private static async Task NoContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.NoContent,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
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

    private static async Task<List<JsonElement>> PageAsync(HttpClient reader, string uri, CancellationToken cancellationToken) =>
        [.. (await OkAsync(await reader.GetAsync(uri, cancellationToken), cancellationToken)).GetProperty("items").EnumerateArray()];

    /// <summary>The keys of a 400, field by field.</summary>
    private static async Task<Dictionary<string, string[]>> RefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {text}");

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject()
                .ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray());
        }
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>Until a transaction waits for a lock: the request under test, behind the transaction the test holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms (CONTRIBUTING.md).</remarks>
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

    // ---- rows and people ------------------------------------------------------------------------------------------------------

    /// <summary>An event of the class, published and over at once or later, with its airports.</summary>
    private async Task<long> EventRowAsync(string name, DateTime starts, DateTime ends, string[] airports, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfe",
            PublicSlots = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = ends,
            BookingOpensAtUtc = starts.AddDays(-30),
            Status = PublishStatus.Published,
            PublishedAt = starts.AddDays(-30),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        for (var index = 0; index < airports.Length; index++)
        {
            database.Airports.Add(new EventAirport
            {
                EventId = row.Id,
                Icao = airports[index],
                Ordinal = index,
                OwnerDepartment = row.OwnerDepartment,
                OwnerDepartmentMask = row.OwnerDepartmentMask,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task<Event> EventAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<EventReport> ReportAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Reports.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(report => report.Id == id, cancellationToken);
    }

    private async Task<List<EventReport>> ReportsOfAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Reports.IgnoreQueryFilters().AsNoTracking()
            .Where(report => report.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<EventReportItem>> ItemsAsync(long reportId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().ReportItems.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.ReportId == reportId)
            .OrderBy(item => item.Ordinal)
            .ToListAsync(cancellationToken);
    }

    private async Task<EventStats> StatsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Stats.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(stats => stats.EventId == eventId, cancellationToken);
    }

    /// <summary>Whether a row of the audit log still copies a text with the marker in it, before or after its change.</summary>
    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    /// <summary>The mails «report decided» to one of the class's people.</summary>
    private async Task<List<Notification>> MailsAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == EventsNotifications.ReportDecided && row.Vid == vid && row.DataJson.Contains(SlugStem))
            .ToListAsync(cancellationToken);
    }

    private async Task ForgetMailsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .Where(row => row.Type == EventsNotifications.ReportDecided && People.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The distance between two airports as the core measures it, from the directory's coordinates.</summary>
    private async Task<decimal> DistanceAsync(string from, string to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var known = await scope.ServiceProvider.GetRequiredService<IAirportDirectory>().FindAsync([from, to], cancellationToken);
        return GreatCircle.DistanceNmRounded(
            new GeoPoint(known[from].Latitude!.Value, known[from].Longitude!.Value),
            new GeoPoint(known[to].Latitude!.Value, known[to].Longitude!.Value));
    }

    private async Task<string?> WriteSettingsAsync(string? json, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = ModuleSettingsStore.SettingsKey(EventsModule.ModuleKey);
        var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        var before = row?.ValueJson;

        if (json is null)
        {
            if (row is not null)
            {
                database.DivisionSettings.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new DivisionSetting { Key = key };
                database.DivisionSettings.Add(row);
            }

            row.ValueJson = json;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await database.SaveChangesAsync(cancellationToken);
        return before;
    }

    /// <summary>
    /// The airports of the class in the core's snapshot, where an item's are checked and measured: the two of the evening with their places,
    /// and two away from every country; only those it adds go at the end.
    /// </summary>
    private async Task SeedAirportsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var (icao, latitude, longitude) in new[] { (Home, 41.8003, 12.2389), (Away, 38.0712, 15.6516), (Elsewhere, 45.0, 9.0), (Farther, 44.0, 8.0) })
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport
                {
                    Icao = icao,
                    Name = $"evt-test {icao}",
                    CountryId = "XX",
                    Latitude = latitude,
                    Longitude = longitude,
                    SyncedAt = clock.UtcNow,
                });
                _seededAirports.Add(icao);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPersonAsync(int vid, string? position, bool superadmin, CancellationToken cancellationToken)
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
        user.LastName = "Report";
        // Only the members who send are told by mail: the staff are seeded without an address, as in the other classes of the events.
        user.Email = position is null && !superadmin ? $"e14a-{vid}@example.org" : null;
        user.Locale = "en";
        user.IsStaff = position is not null;
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.UserStaffPositions.Where(row => row.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        if (position is not null)
        {
            var parsed = StaffRoleMap.Parse(position, Division().Code, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Assert.NotNull(parsed);
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = parsed.Raw,
                Department = parsed.Department,
                Level = parsed.Level,
                Fir = parsed.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private DivisionOptions Division() => _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value;

    // ---- the network of the evening -------------------------------------------------------------------------------------------

    /// <summary>What the double answers: the tracker up or down, and how far the evening is moved.</summary>
    private sealed class Sources
    {
        public bool TrackerAnswers { get; set; } = true;

        /// <summary>How many times the network was asked (#322, item 1).</summary>
        public int Asked { get; set; }

        public TimeSpan Shift { get; set; }

        /// <summary>Sessions the tracker answers on top of the evening's, at the head of its answer (#322, item 1: a cut answer).</summary>
        public int Padding { get; set; }
    }

    /// <summary>
    /// The network's sessions answered from the evening of Fiumicino moved to the class's evening — the question moved back, the answers
    /// forward —, the class's pilot and controller asking as the evening's own, filtered by the tracker's own rule
    /// (<see cref="IvaoTrackerReader.Answers"/>); null while it is down.
    /// </summary>
    private sealed class EveningTracker(HubPaths paths, Sources sources) : INetworkSessionSource
    {
        public Task<IReadOnlyList<NetworkSession>?> SessionsAsync(NetworkSessionQuery query, CancellationToken cancellationToken = default)
        {
            sources.Asked++;
            if (!sources.TrackerAnswers)
            {
                return Task.FromResult<IReadOnlyList<NetworkSession>?>(null);
            }

            var vid = query.Vid is { } asked && AsTheEvening.TryGetValue(asked, out var evening) ? evening : query.Vid;
            var question = new IvaoSessionQuery(
                vid,
                query.FromUtc - sources.Shift,
                query.ToUtc - sources.Shift,
                query.DepartureIcao,
                query.ArrivalIcao,
                query.Connection switch
                {
                    NetworkConnection.Pilot => IvaoConnectionType.Pilot,
                    NetworkConnection.Atc => IvaoConnectionType.Atc,
                    _ => null,
                })
            {
                Limit = query.Limit,
            };

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(paths.Root, FixtureIvaoApiClient.Directory, "tracker-airport-LIRF.json")));
            return Task.FromResult<IReadOnlyList<NetworkSession>?>(
            [
                .. Enumerable.Range(1, sources.Padding)
                    .Select(index => new NetworkSession(9_000_000 + index, query.Vid ?? 0, "PAD", query.ToUtc, query.ToUtc)),
                .. document.RootElement.EnumerateArray()
                    .Where(row => IvaoTrackerReader.Answers(row, question))
                    .Select(IvaoTrackerReader.ReadSession)
                    .OfType<IvaoTrackerSessionDto>()
                    .OrderByDescending(session => session.StartedAt)
                    .Take(query.Limit)
                    .Select(session => Moved(session, query.Vid ?? session.Vid, sources.Shift)),
            ]);
        }

        /// <summary>
        /// A recorded session moved in time, with the airports of its plan's revisions as the core's source gives them (E10v): the check of a
        /// flight reads the last one (E14b).
        /// </summary>
        private static NetworkSession Moved(IvaoTrackerSessionDto session, int vid, TimeSpan shift)
        {
            NetworkRoute[] routes = [.. IvaoTrackerReader.ReadPlannedRoutes(session).Select(route => new NetworkRoute(route.DepartureIcao, route.ArrivalIcao))];

            return new NetworkSession(
                session.Id,
                vid,
                session.Callsign,
                session.StartedAt + shift,
                session.EndedAt + shift,
                routes.LastOrDefault()?.DepartureIcao,
                routes.LastOrDefault()?.ArrivalIcao)
            {
                Routes = routes,
            };
        }
    }
}
