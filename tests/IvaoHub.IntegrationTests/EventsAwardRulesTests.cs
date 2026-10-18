using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Preferences;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Reports;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Quartz;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The rules of award, the reports the system writes for whoever asked, and the validators' digest (M4, E14b; design §1.9, §1.11, §5.1, §5.4,
/// §8.3, §8.4; note 2026-10-17-le-regole-di-award), through the real host with the division file of this repository — its grants to the events'
/// and the members' departments, nothing given by hand:
/// <list type="bullet">
/// <item>the events' coordinator writes the rules of an event, with their refusals, and «Duplicate» copies them;</item>
/// <item><c>events-after</c> writes an <c>Auto</c> report of a controller only for whoever switched the preference on, and never touches a
/// report its member sent;</item>
/// <item><c>events-digest</c> writes to each validator once a day, the reports of the events they may validate, their own left out.</item>
/// </list>
/// <para>The network is a double of the core's source that answers the sessions a test gives it. The evening is in 2001, and the jobs run as of
/// a day of 2001 with a clock of their own: every event of the other classes is still to come then. ⚠️ The people are 761262–761268 (E14b's
/// range, 761262–761299).</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsAwardRulesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int CoordinatorVid = 761262;
    private const int MdVid = 761263;
    private const int OtherMdVid = 761264;
    private const int PilotVid = 761265;
    private const int ControllerVid = 761266;
    private const int UnaskedVid = 761267;
    private const int SenderVid = 761268;

    private static readonly int[] People = [CoordinatorVid, MdVid, OtherMdVid, PilotVid, ControllerVid, UnaskedVid, SenderVid];

    private const string Home = "XEB1";
    private const string Away = "XEB2";
    private const string Tower = "XEB1_TWR";

    private const string SlugStem = "evt-test-e14b";

    private static readonly DateTime Start = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    /// <summary>When <c>events-after</c> is first due on the evening: six hours after its end.</summary>
    private static readonly DateTime Due = Start.AddHours(8);

    private static readonly string[] Locales = ["it", "en"];

    private readonly Network _network = new();
    private readonly List<string> _seededAirports = [];
    private readonly List<long> _awards = [];
    private readonly HashSet<string> _seenSources = new(StringComparer.Ordinal);
    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private string? _settingsBefore;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(_network);
            services.AddScoped<INetworkSessionSource>(provider => provider.GetRequiredService<Network>());
            services.AddScoped<IAtcActivitySource>(_ => new UnavailableAtcActivitySource());
        }));
        var token = TestContext.Current.CancellationToken;

        // The host runs the jobs of the class on their own schedule: paused, the only runs are the test's (TourTests, AwardQueueMailTests).
        var scheduler = await _factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(token);
        await scheduler.PauseJob(new JobKey(AwardQueueMailJob.JobName), token);
        await scheduler.PauseJob(new JobKey(ReportsDigestJob.JobName), token);
        await scheduler.PauseJob(new JobKey(EventsAfterJob.JobName), token);

        await ForgetAsync(token);

        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", token);
        await SeedPersonAsync(MdVid, $"{Division().Code}-MC", token);
        await SeedPersonAsync(OtherMdVid, $"{Division().Code}-MAC", token);
        await SeedPersonAsync(PilotVid, position: null, token);
        await SeedPersonAsync(ControllerVid, position: null, token);
        await SeedPersonAsync(UnaskedVid, position: null, token);
        await SeedPersonAsync(SenderVid, position: null, token);
        await SeedAirportsAsync(token);

        _settingsBefore = await WriteSettingsAsync("""{"reportDays":14,"attendanceMinimumMinutes":30}""", token);
    }

    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.IvaoAirports.Where(airport => _seededAirports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.AwardAssignments.Where(assignment => _awards.Contains(assignment.AwardId)).ExecuteDeleteAsync();
            await database.Awards.Where(award => _awards.Contains(award.Id)).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>
    /// The events' coordinator writes the rules of an event in its tab (§1.9): the award and the criterion with its parameters — the kind is the
    /// criterion's, what it does not take is emptied —, refused on its field when the award is retired or unknown, an airport unknown, a number
    /// out of its range, or on an event in person; a member writes none. The list names the award. «Duplicate» copies them (§2.3-bis).
    /// </summary>
    [Fact]
    public async Task TheCoordinatorWritesTheRulesOfAnEventAndDuplicateCopiesThem()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("rules", inPerson: false, token);
        var inPersonId = await EventRowAsync("in-person", inPerson: true, token);
        var award = await AwardAsync($"{SlugStem} pilot support", active: true, token);
        var retired = await AwardAsync($"{SlugStem} retired", active: false, token);
        var coordinator = await SignedInAsync(CoordinatorVid, token);

        var created = await CreatedAsync(
            await coordinator.PostAsJsonAsync(
                EventAwardRuleEndpoints.Pattern,
                Rule(eventId, award, "RouteTimes", departure: "xeb1", arrival: Away, count: 2, minutes: 99),
                token),
            token);
        Assert.Equal(("Pilot", "XEB1", (int?)2, (int?)null), (
            created.GetProperty("kind").GetString(),
            created.GetProperty("departureIcao").GetString(),
            Number(created, "count"),
            Number(created, "minutes")));

        await CreatedAsync(
            await coordinator.PostAsJsonAsync(
                EventAwardRuleEndpoints.Pattern, Rule(eventId, award, "MinAtcMinutes", minutes: 60), token),
            token);

        // The refusals, each on its field.
        Assert.Equal(
            [AwardRules.AwardUnknownKey],
            (await RefusedAsync(await coordinator.PostAsJsonAsync(EventAwardRuleEndpoints.Pattern, Rule(eventId, retired, "AnyFlight"), token), token))["awardId"]);
        Assert.Equal(
            ["events:errors.airportUnknown"],
            (await RefusedAsync(
                await coordinator.PostAsJsonAsync(EventAwardRuleEndpoints.Pattern, Rule(eventId, award, "Route", departure: Home, arrival: "XEB9"), token),
                token))["arrivalIcao"]);
        Assert.Equal(
            ["errors.number.range"],
            (await RefusedAsync(
                await coordinator.PostAsJsonAsync(EventAwardRuleEndpoints.Pattern, Rule(eventId, award, "MinLegDistance", distance: 0), token),
                token))["distanceNm"]);
        Assert.Equal(
            ["events:errors.awardRuleInPerson"],
            (await RefusedAsync(await coordinator.PostAsJsonAsync(EventAwardRuleEndpoints.Pattern, Rule(inPersonId, award, "AnyFlight"), token), token))["eventId"]);

        // A member writes no rule.
        var pilot = await SignedInAsync(PilotVid, token);
        using (var refused = await pilot.PostAsJsonAsync(EventAwardRuleEndpoints.Pattern, Rule(eventId, award, "AnyFlight"), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var rows = await PageAsync(coordinator, $"{EventAwardRuleEndpoints.Pattern}?filter[eventId]={eventId}", token);
        Assert.Equal(["RouteTimes", "MinAtcMinutes"], rows.Select(row => row.GetProperty("criterion").GetString()));
        Assert.All(rows, row => Assert.Equal($"{SlugStem} pilot support", row.GetProperty("awardName").GetProperty("en").GetString()));

        // The form's choices: the active awards, not the retired one.
        var choices = await OkAsync(await coordinator.GetAsync($"{EventAwardRuleEndpoints.Pattern}/choices", token), token);
        var offered = choices.GetProperty("awards").EnumerateArray().Select(row => row.GetProperty("id").GetInt64()).ToList();
        Assert.Contains(award, offered);
        Assert.DoesNotContain(retired, offered);

        // «Duplicate» copies the rules as they are, in the copy's care.
        var copy = await CreatedAsync(
            await coordinator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{eventId}/duplicate",
                new { startsAtUtc = Start.AddYears(1), slug = Slug("rules-copy"), publicSlots = false },
                token),
            token);
        var copyId = copy.GetProperty("event").GetProperty("id").GetInt64();
        var copied = await RulesAsync(copyId, token);
        Assert.Equal(
            [(AwardCriterion.RouteTimes, "XEB1", "XEB2", (int?)2, (int?)null), (AwardCriterion.MinAtcMinutes, null, null, null, 60)],
            copied.Select(rule => (rule.Criterion, rule.DepartureIcao, rule.ArrivalIcao, rule.Count, rule.Minutes)));
        Assert.All(copied, rule => Assert.Equal(award, rule.AwardId));
    }

    /// <summary>
    /// The «done when» of E14b on the server (§5.4): the members' coordinator accepts a pilot report, and its verified items meet two of the
    /// event's five rules — any flight, and the route flown —, not the route twice (one verified flight of it), not the long leg, and not the
    /// controller's rule; one signal per rule met lands in the core's queue in the same transaction, waiting, with the member, the rule's award and
    /// the event in its reason; whoever assigns is told by the core's mail (E10d). The statistics count the two.
    /// </summary>
    [Fact]
    public async Task AnAcceptedReportProjectsTheSignalsOfTheRulesItMeetsAndNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("signals", inPerson: false, token);
        var anyFlight = await AwardAsync($"{SlugStem} any flight", active: true, token);
        var route = await AwardAsync($"{SlugStem} route", active: true, token);
        var twice = await AwardAsync($"{SlugStem} twice", active: true, token);
        var far = await AwardAsync($"{SlugStem} far", active: true, token);
        var shift = await AwardAsync($"{SlugStem} shift", active: true, token);
        await RuleRowAsync(eventId, anyFlight, AwardCriterion.AnyFlight, token);
        await RuleRowAsync(eventId, route, AwardCriterion.Route, token, departure: Home, arrival: Away);
        await RuleRowAsync(eventId, twice, AwardCriterion.RouteTimes, token, departure: Home, arrival: Away, count: 2);
        await RuleRowAsync(eventId, far, AwardCriterion.MinLegDistance, token, distance: 5000);
        await RuleRowAsync(eventId, shift, AwardCriterion.AnyShift, token);
        var reportId = await PilotReportAsync(eventId, PilotVid, token);

        var md = await SignedInAsync(MdVid, token);
        await NoContentAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token);

        var signals = await SignalsOfAsync(eventId, token);
        Assert.Equal([anyFlight, route], signals.Select(signal => signal.AwardId!.Value).Order());
        Assert.All(signals, signal =>
        {
            Assert.Equal((PilotVid, AwardSignalStatus.Pending), (signal.Vid, signal.Status));
            Assert.StartsWith($"{Slug("signals")}: ", signal.Reason, StringComparison.Ordinal);
        });

        // Whoever assigns is told by the core (E10d): the members' coordinator holds Awards.Assign.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await ActivatorUtilities.CreateInstance<AwardQueueMailJob>(scope.ServiceProvider).RunAsync(token);
        }

        Assert.NotEmpty(await MailsAsync(MdVid, NotificationTypes.AwardToAssign, token));
        Assert.All(await SignalsOfAsync(eventId, token), signal => Assert.NotNull(signal.NotifiedAt));

        // The statistics count them.
        var coordinator = await SignedInAsync(CoordinatorVid, token);
        using (var recalculated = await coordinator.PostAsync(new Uri($"{EventEndpoints.Pattern}/{eventId}/stats", UriKind.Relative), content: null, token))
        {
            Assert.True(recalculated.IsSuccessStatusCode, await recalculated.Content.ReadAsStringAsync(token));
        }

        Assert.Equal(2, await StatsAwardSignalsAsync(eventId, token));
    }

    /// <summary>
    /// A decision that changes (Carmine's choice 1 and his limit on #287): rejected after being accepted, the report's signals still waiting leave
    /// the queue and one dismissed by whoever assigns stays as it was; accepted again, the rules are evaluated again. Once an award is assigned
    /// from a signal of a report, its decision no longer changes — refused with its own key — and the staff's list no longer offers it.
    /// </summary>
    [Fact]
    public async Task RejectingWithdrawsTheWaitingSignalsAndAnAssignedAwardFreezesTheDecision()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("withdraw", inPerson: false, token);
        var anyFlight = await AwardAsync($"{SlugStem} any flight", active: true, token);
        var route = await AwardAsync($"{SlugStem} route", active: true, token);
        await RuleRowAsync(eventId, anyFlight, AwardCriterion.AnyFlight, token);
        await RuleRowAsync(eventId, route, AwardCriterion.Route, token, departure: Home, arrival: Away);
        var withdrawn = await PilotReportAsync(eventId, PilotVid, token);
        var frozen = await PilotReportAsync(eventId, SenderVid, token);
        var md = await SignedInAsync(MdVid, token);

        // Accepted, then the route's signal dismissed by whoever assigns, then rejected: the waiting one goes, the dismissed one stays.
        await NoContentAsync(await DecideAsync(md, withdrawn, "Accepted", note: null, token), token);
        var dismissed = (await SignalsOfAsync(eventId, token)).Single(signal => signal.Vid == PilotVid && signal.AwardId == route);
        await SetStatusAsync(dismissed.Id, AwardSignalStatus.Dismissed, token);
        await NoContentAsync(await DecideAsync(md, withdrawn, "Rejected", note: $"{SlugStem} not this time", token), token);
        var left = (await SignalsOfAsync(eventId, token)).Where(signal => signal.Vid == PilotVid).ToList();
        Assert.Equal([(route, AwardSignalStatus.Dismissed)], left.Select(signal => (signal.AwardId!.Value, signal.Status)));

        // Accepted again: evaluated again, each rule on its own row (the review of #293, point 1) — the one withdrawn waits again, and the one
        // dismissed stays dismissed: no new signal of it next to the old one.
        await NoContentAsync(await DecideAsync(md, withdrawn, "Accepted", note: null, token), token);
        Assert.Equal(
            [(anyFlight, AwardSignalStatus.Pending), (route, AwardSignalStatus.Dismissed)],
            (await SignalsOfAsync(eventId, token)).Where(signal => signal.Vid == PilotVid).Select(signal => (signal.AwardId!.Value, signal.Status)).Order());
        Assert.Equal(2, await RowsOfAsync(withdrawn, token));

        // An award assigned from a signal of the other report, by whoever assigns, through the core's register.
        await NoContentAsync(await DecideAsync(md, frozen, "Accepted", note: null, token), token);
        var signal = (await SignalsOfAsync(eventId, token)).Single(candidate => candidate.Vid == SenderVid && candidate.AwardId == anyFlight);
        using (var assigned = await md.PostAsJsonAsync(
                   "/api/award-assignments",
                   new { awardId = anyFlight, vid = SenderVid, reason = $"{SlugStem} assigned", signalId = signal.Id, rowVersion = DateTime.MinValue },
                   token))
        {
            Assert.True(assigned.StatusCode == HttpStatusCode.Created, await assigned.Content.ReadAsStringAsync(token));
        }

        Assert.Equal(
            [ReportAwards.AssignedKey],
            (await RefusedAsync(await DecideAsync(md, frozen, "Rejected", note: $"{SlugStem} too late", token), token))["state"]);
        Assert.Equal(ReportState.Accepted, (await ReportsOfAsync(eventId, token)).Single(report => report.Id == frozen).State);

        var rows = await PageAsync(md, $"{ReportEndpoints.Pattern}?filter[eventId]={eventId}", token);
        Assert.False(rows.Single(row => row.GetProperty("id").GetInt64() == frozen).GetProperty("decidable").GetBoolean());
        Assert.True(rows.Single(row => row.GetProperty("id").GetInt64() == withdrawn).GetProperty("decidable").GetBoolean());
    }

    /// <summary>
    /// The path of the review of #293, point 1: accepted, its signal dismissed, the report rejected — and whoever assigns opens the dismissed
    /// signal again and assigns the award from it, which the core allows. The row of the rule is still there, so the award is seen: the report's
    /// decision no longer changes, whichever it is (Carmine's limit on #287).
    /// </summary>
    [Fact]
    public async Task ADismissedSignalAssignedAfterARejectionStillFreezesTheDecision()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("reopened", inPerson: false, token);
        var anyFlight = await AwardAsync($"{SlugStem} any flight", active: true, token);
        await RuleRowAsync(eventId, anyFlight, AwardCriterion.AnyFlight, token);
        var reportId = await PilotReportAsync(eventId, PilotVid, token);
        var md = await SignedInAsync(MdVid, token);

        await NoContentAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token);
        var signal = Assert.Single(await SignalsOfAsync(eventId, token));
        await SetStatusAsync(signal.Id, AwardSignalStatus.Dismissed, token);
        await NoContentAsync(await DecideAsync(md, reportId, "Rejected", note: $"{SlugStem} not this time", token), token);

        // Whoever assigns opens it again and assigns the award, through the core's queue and register.
        using (var reopened = await md.PutAsJsonAsync($"/api/award-signals/{signal.Id}", new { status = "Pending" }, token))
        {
            Assert.True(reopened.IsSuccessStatusCode, await reopened.Content.ReadAsStringAsync(token));
        }

        using (var assigned = await md.PostAsJsonAsync(
                   "/api/award-assignments",
                   new { awardId = anyFlight, vid = PilotVid, reason = $"{SlugStem} assigned", signalId = signal.Id, rowVersion = DateTime.MinValue },
                   token))
        {
            Assert.True(assigned.StatusCode == HttpStatusCode.Created, await assigned.Content.ReadAsStringAsync(token));
        }

        Assert.Equal(
            [ReportAwards.AssignedKey],
            (await RefusedAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token))["state"]);
        Assert.Equal(ReportState.Rejected, (await ReportsOfAsync(eventId, token)).Single().State);
        Assert.False(Assert.Single(await PageAsync(md, $"{ReportEndpoints.Pattern}?filter[eventId]={eventId}", token)).GetProperty("decidable").GetBoolean());
    }

    /// <summary>
    /// A decision and an assignment in the same instant (the review of #293, point 2): a transaction of the test holds the report's signal as an
    /// assignment does while it marks it assigned; the change of the decision waits for it at the lock of the signals' rows (<c>FOR UPDATE</c>),
    /// then reads the signal assigned and is refused — never a rejection next to an award.
    /// </summary>
    [Fact]
    public async Task ADecisionWaitsForAnAssignmentInTheSameInstantAndIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("race", inPerson: false, token);
        var anyFlight = await AwardAsync($"{SlugStem} any flight", active: true, token);
        await RuleRowAsync(eventId, anyFlight, AwardCriterion.AnyFlight, token);
        var reportId = await PilotReportAsync(eventId, PilotVid, token);
        var md = await SignedInAsync(MdVid, token);
        await NoContentAsync(await DecideAsync(md, reportId, "Accepted", note: null, token), token);
        var signal = Assert.Single(await SignalsOfAsync(eventId, token));

        await using var connection = new MySqlConnection(mariaDb.ConnectionString);
        await connection.OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token);
        await using (var hold = new MySqlCommand($"SELECT id FROM cms_award_signals WHERE id = {signal.Id} FOR UPDATE", connection, transaction))
        {
            await hold.ExecuteScalarAsync(token);
        }

        // The rejection starts, and waits in the database for the assignment the test is making.
        var decision = DecideAsync(md, reportId, "Rejected", note: $"{SlugStem} too late", token);
        await WaitForALockWaitAsync(token);

        await using (var assign = new MySqlCommand($"UPDATE cms_award_signals SET status = 'Handled' WHERE id = {signal.Id}", connection, transaction))
        {
            await assign.ExecuteNonQueryAsync(token);
        }

        await transaction.CommitAsync(token);

        Assert.Equal([ReportAwards.AssignedKey], (await RefusedAsync(await decision, token))["state"]);
        Assert.Equal(ReportState.Accepted, (await ReportsOfAsync(eventId, token)).Single().State);
    }

    /// <summary>
    /// The «Auto» reports (§1.11, §5.1): after the evening, <c>events-after</c> writes the ATC report of the controller who switched the preference
    /// on — one shift on the event's position, verified with its minutes, <c>Auto</c> and waiting for a validator —, nothing for the controller
    /// who did not, nor for one who switched it off; the report a member sent by hand stays as it is. The event closes once every member is
    /// looked at, and a second run writes nothing.
    /// </summary>
    [Fact]
    public async Task EventsAfterWritesAutoReportsOnlyForWhoeverAskedForThem()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("auto", inPerson: false, token);
        await PositionRowAsync(eventId, token);
        await PreferenceAsync(ControllerVid, "true", token);
        await PreferenceAsync(PilotVid, "false", token);
        await PreferenceAsync(SenderVid, "true", token);

        _network.Atc[ControllerVid] = [new NetworkSession(9001, ControllerVid, Tower, Start.AddMinutes(-10), Start.AddMinutes(70))];
        _network.Atc[UnaskedVid] = [new NetworkSession(9002, UnaskedVid, Tower, Start.AddMinutes(70), Start.AddMinutes(110))];
        _network.Atc[PilotVid] = [new NetworkSession(9003, PilotVid, Tower, Start, Start.AddHours(1))];
        _network.Atc[SenderVid] = [new NetworkSession(9004, SenderVid, Tower, Start, Start.AddHours(1))];

        // Their flights, with the airports of the plan (E10v): the controller also flew to the event's airport; the one who did not ask flew too.
        _network.Pilot[ControllerVid] = [new NetworkSession(9005, ControllerVid, "tst14b", Start.AddMinutes(-90), Start.AddMinutes(-20), Away, Home)];
        _network.Pilot[UnaskedVid] = [new NetworkSession(9006, UnaskedVid, "TST14C", Start, Start.AddHours(1), Home, Away)];

        // The member who sent theirs by hand, before the job.
        var sent = await ManualReportAsync(eventId, SenderVid, token);

        await RunAfterAsync(Due, token);

        var reports = await ReportsOfAsync(eventId, token);
        var auto = Assert.Single(reports, report => report.Vid == ControllerVid && report.Kind == ReportKind.Atc);
        Assert.Equal((ReportKind.Atc, ReportSource.Auto, ReportState.Submitted, ReportVerification.Valid), (auto.Kind, auto.Source, auto.State, auto.Verification));
        var item = Assert.Single(await ItemsAsync(auto.Id, token));
        Assert.Equal(
            (Tower, Start.AddMinutes(-10), Start.AddMinutes(70), (int?)80, (bool?)true),
            (item.Callsign, item.FromUtc!.Value, item.ToUtc!.Value, item.Minutes, item.Verified));

        // The controller's flight: a pilot report of their own, Auto, the flight verified with its session and the distance the core measures.
        var flight = Assert.Single(reports, report => report.Vid == ControllerVid && report.Kind == ReportKind.Pilot);
        Assert.Equal((ReportSource.Auto, ReportVerification.Valid), (flight.Source, flight.Verification));
        var leg = Assert.Single(await ItemsAsync(flight.Id, token));
        Assert.Equal(("TST14B", Away, Home, (long?)9005, (int?)70, (bool?)true), (leg.Callsign, leg.DepartureIcao, leg.ArrivalIcao, leg.SessionId, leg.Minutes, leg.Verified));
        Assert.NotNull(leg.DistanceNm);

        Assert.DoesNotContain(reports, report => report.Vid is UnaskedVid or PilotVid);
        var mine = Assert.Single(reports, report => report.Vid == SenderVid);
        Assert.Equal((sent, ReportSource.Manual), (mine.Id, mine.Source));
        Assert.Equal("XEB1_APP", Assert.Single(await ItemsAsync(mine.Id, token)).Callsign);

        var row = await EventAsync(eventId, token);
        Assert.Equal(Due, row.AfterDoneAt);
        Assert.NotNull(row.AutoReportsCursor);

        // Run again: the event is closed, nothing more.
        await RunAfterAsync(Due.AddHours(1), token);
        Assert.Equal(reports.Count, (await ReportsOfAsync(eventId, token)).Count);
    }

    /// <summary>
    /// A run that has no budget left for every member stops, keeps the members done, and the next goes on from the last one (§5.1, reading 8):
    /// the network not answering leaves the member to the next run, and nothing is written for them.
    /// </summary>
    [Fact]
    public async Task ANetworkThatDoesNotAnswerLeavesTheMemberToTheNextRun()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("auto-down", inPerson: false, token);
        await PositionRowAsync(eventId, token);
        await PreferenceAsync(ControllerVid, "true", token);
        _network.Atc[ControllerVid] = [new NetworkSession(9011, ControllerVid, Tower, Start, Start.AddHours(1))];

        _network.Answers = false;
        await RunAfterAsync(Due, token);
        Assert.Empty(await ReportsOfAsync(eventId, token));
        Assert.Null((await EventAsync(eventId, token)).AfterDoneAt);

        _network.Answers = true;
        await RunAfterAsync(Due.AddHours(1), token);
        Assert.Single(await ReportsOfAsync(eventId, token), report => report.Vid == ControllerVid);
        Assert.Equal(Due.AddHours(1), (await EventAsync(eventId, token)).AfterDoneAt);
    }

    /// <summary>
    /// The digest (§5.3, §8.4): to each validator, the reports waiting on the events they may validate, their own left out — the members'
    /// coordinator has one of their own —; nothing to whoever writes the events only. A second run on the same day of the division sends nothing
    /// and says so; the next day sends again. Its trigger says the division's zone.
    /// </summary>
    [Fact]
    public async Task TheDigestGoesOnceADayToTheValidators()
    {
        var token = TestContext.Current.CancellationToken;
        var eventId = await EventRowAsync("digest", inPerson: false, token);
        await ManualReportAsync(eventId, PilotVid, token);
        await ManualReportAsync(eventId, SenderVid, token);
        await ManualReportAsync(eventId, MdVid, token);

        var morning = new DateTime(2001, 1, 3, 6, 15, 0, DateTimeKind.Utc);
        Assert.True(await RunDigestAsync(morning, token) >= 2);

        Assert.Equal($"- {Slug("digest")}: 2", Line(Assert.Single(await DigestsAsync(MdVid, token))));
        Assert.Equal($"- {Slug("digest")}: 3", Line(Assert.Single(await DigestsAsync(OtherMdVid, token))));
        Assert.Empty(await DigestsAsync(CoordinatorVid, token));

        // Again the same day of the division, late in the evening: nothing.
        Assert.Equal(0, await RunDigestAsync(morning.AddHours(16), token));
        Assert.Equal(ReportsDigestJob.AlreadySent, await LastDigestMessageAsync(token));
        Assert.Single(await DigestsAsync(MdVid, token));

        // The next morning: again.
        await RunDigestAsync(morning.AddDays(1), token);
        Assert.Equal(2, (await DigestsAsync(MdVid, token)).Count);

        var trigger = _factory.Services.GetRequiredService<IOptions<QuartzOptions>>().Value.Triggers
            .Single(candidate => candidate.Key.Name == ReportsDigestJob.TriggerName);
        var cron = Assert.IsAssignableFrom<ICronTrigger>(trigger);
        Assert.Equal((ReportsDigestJob.Cron, Division().ResolveTimeZone().Id), (cron.CronExpressionString, cron.TimeZone.Id));
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static object Rule(
        long eventId,
        long awardId,
        string criterion,
        string? departure = null,
        string? arrival = null,
        int? count = null,
        int? distance = null,
        int? minutes = null,
        string? positionType = null) => new
        {
            eventId,
            awardId,
            criterion,
            departureIcao = departure,
            arrivalIcao = arrival,
            count,
            distanceNm = distance,
            minutes,
            positionType,
            rowVersion = DateTime.MinValue,
        };

    private static int? Number(JsonElement row, string name) =>
        row.GetProperty(name).ValueKind == JsonValueKind.Null ? null : row.GetProperty(name).GetInt32();

    /// <summary>The line of the class's event in a digest, without the oldest's day and the link.</summary>
    private static string Line(Notification digest)
    {
        var events = JsonDocument.Parse(digest.DataJson).RootElement.GetProperty("events").GetString()!;
        var line = events.Split('\n').Single(candidate => candidate.Contains(SlugStem, StringComparison.Ordinal));
        return line[..line.IndexOf(" (", StringComparison.Ordinal)];
    }

    private async Task RunAfterAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<EventsAfterJob>(scope.ServiceProvider, new FixedClock(now)).RunAsync(cancellationToken);
    }

    private async Task<int> RunDigestAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await ActivatorUtilities.CreateInstance<ReportsDigestJob>(scope.ServiceProvider, new FixedClock(now)).RunAsync(cancellationToken);
    }

    private async Task<string?> LastDigestMessageAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .Where(entry => entry.Job == ReportsDigestJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .Select(entry => entry.Message)
            .FirstAsync(cancellationToken);
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

    // ---- rows and people ------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private async Task<long> EventRowAsync(string name, bool inPerson, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfe",
            HasRoster = !inPerson,
            InPerson = inPerson,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = Start,
            EndsAtUtc = Start.AddHours(2),
            Status = PublishStatus.Published,
            PublishedAt = Start.AddDays(-20),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        database.Airports.Add(new EventAirport
        {
            EventId = row.Id,
            Icao = Home,
            Ordinal = 1,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);

        return row.Id;
    }

    private async Task PositionRowAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        database.AtcPositions.Add(new EventAtcPosition
        {
            EventId = eventId,
            Callsign = Tower,
            Fir = "XEBB",
            FromUtc = Start,
            ToUtc = Start.AddHours(2),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task RuleRowAsync(
        long eventId,
        long awardId,
        AwardCriterion criterion,
        CancellationToken cancellationToken,
        string? departure = null,
        string? arrival = null,
        int? count = null,
        int? distance = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        database.AwardRules.Add(new EventAwardRule
        {
            EventId = eventId,
            AwardId = awardId,
            Criterion = criterion,
            Kind = AwardRules.KindOf(criterion),
            DepartureIcao = departure,
            ArrivalIcao = arrival,
            Count = count,
            DistanceNm = distance,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A pilot's report sent by hand, waiting for a validator: the route flown and found, and the way back not found.</summary>
    private async Task<long> PilotReportAsync(long eventId, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var report = new EventReport
        {
            EventId = eventId,
            Vid = vid,
            Kind = ReportKind.Pilot,
            Source = ReportSource.Manual,
            State = ReportState.Submitted,
            Verification = ReportVerification.Partial,
            SubmittedAt = Start.AddHours(3),
            CheckedAt = Start.AddHours(3),
            CreatedAt = Start.AddHours(3),
            UpdatedAt = Start.AddHours(3),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Reports.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        foreach (var (ordinal, from, to, verified) in new[] { (0, Home, Away, true), (1, Away, Home, false) })
        {
            database.ReportItems.Add(new EventReportItem
            {
                ReportId = report.Id,
                EventId = eventId,
                Vid = vid,
                Ordinal = ordinal,
                Callsign = "TST1",
                DepartureIcao = from,
                ArrivalIcao = to,
                FlightDay = DateOnly.FromDateTime(Start),
                SessionId = verified ? 9100 + ordinal : null,
                Minutes = verified ? 70 : null,
                DistanceNm = 300m,
                Verified = verified,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        return report.Id;
    }

    /// <summary>A decision on what the validator sees now: the report's state and its last change, as the list gives them (E14a, #289).</summary>
    private async Task<HttpResponseMessage> DecideAsync(HttpClient client, long reportId, string state, string? note, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var seen = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Reports.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(report => report.Id == reportId, cancellationToken);

        return await client.PostAsJsonAsync(
            $"{ReportEndpoints.Pattern}/{reportId}/decision",
            new { state, note, seen = seen.State.ToString(), updatedAt = DateTime.SpecifyKind(seen.UpdatedAt, DateTimeKind.Utc) },
            cancellationToken);
    }

    private static async Task NoContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.IsSuccessStatusCode,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
    }

    /// <summary>The signals the reports of an event put in the core's queue, through the rows of the rules they met.</summary>
    private async Task<List<AwardSignal>> SignalsOfAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var awards = (await scope.ServiceProvider.GetRequiredService<EventsDbContext>().ReportAwards.AsNoTracking()
                .Where(award => award.EventId == eventId)
                .Select(award => award.Id)
                .ToListAsync(cancellationToken))
            .Select(EventReportAward.SourceOf);

        // A dismissed signal stays when its row is gone: it is found by the rows the class saw before.
        _seenSources.UnionWith(awards);
        var sources = _seenSources.ToList();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AwardSignals.AsNoTracking()
            .Where(signal => signal.SourceModule == EventsModule.ModuleKey && sources.Contains(signal.SourceId))
            .OrderBy(signal => signal.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>How many rows of the rules met a report has, pointing out or not: never deleted.</summary>
    private async Task<int> RowsOfAsync(long reportId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().ReportAwards.CountAsync(award => award.ReportId == reportId, cancellationToken);
    }

    /// <summary>Until a transaction waits for a lock: the request under test, behind the transaction the test holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms (CONTRIBUTING.md): read every 250 ms.</remarks>
    private async Task WaitForALockWaitAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'", connection);
        for (var tries = 0; tries < 40; tries++)
        {
            await Task.Delay(250, cancellationToken);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("No request waited for the lock of the signal's row.");
    }

    private async Task SetStatusAsync(long signalId, AwardSignalStatus status, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().AwardSignals
            .Where(signal => signal.Id == signalId)
            .ExecuteUpdateAsync(set => set.SetProperty(signal => signal.Status, status), cancellationToken);
    }

    private async Task<int> StatsAwardSignalsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Stats.IgnoreQueryFilters().AsNoTracking()
            .Where(stats => stats.EventId == eventId)
            .Select(stats => stats.AwardSignals)
            .SingleAsync(cancellationToken);
    }

    private async Task<List<Notification>> MailsAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == type && row.Vid == vid)
            .ToListAsync(cancellationToken);
    }

    /// <summary>A controller's report sent by hand, waiting for a validator: one shift on the approach, found.</summary>
    private async Task<long> ManualReportAsync(long eventId, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var report = new EventReport
        {
            EventId = eventId,
            Vid = vid,
            Kind = ReportKind.Atc,
            Source = ReportSource.Manual,
            State = ReportState.Submitted,
            Verification = ReportVerification.Valid,
            SubmittedAt = Start.AddHours(3),
            CheckedAt = Start.AddHours(3),
            CreatedAt = Start.AddHours(3),
            UpdatedAt = Start.AddHours(3),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Reports.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        database.ReportItems.Add(new EventReportItem
        {
            ReportId = report.Id,
            EventId = eventId,
            Vid = vid,
            Callsign = "XEB1_APP",
            FromUtc = Start,
            ToUtc = Start.AddHours(1),
            Minutes = 60,
            Verified = true,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);

        return report.Id;
    }

    private async Task<long> AwardAsync(string name, bool active, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var award = new Award { OwnerDepartment = Department.FOD, Name = Text(name), IsActive = active };
        hub.Awards.Add(award);
        await hub.SaveChangesAsync(cancellationToken);
        _awards.Add(award.Id);
        return award.Id;
    }

    private async Task PreferenceAsync(int vid, string value, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        hub.UserPreferences.Add(new UserPreference { Vid = vid, Key = AutoReports.PreferenceKey, ValueJson = value, UpdatedAt = Start });
        await hub.SaveChangesAsync(cancellationToken);
    }

    private async Task<Event> EventAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<List<EventAwardRule>> RulesAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AwardRules.IgnoreQueryFilters().AsNoTracking()
            .Where(rule => rule.EventId == eventId)
            .OrderBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
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

    /// <summary>The digests to one of the class's people.</summary>
    private async Task<List<Notification>> DigestsAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == EventsNotifications.ReportsToValidate && row.Vid == vid)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// What the class leaves, taken back: its events with every row of them, the people's digests and preferences, and the digest's runs of the
    /// days of 2001 it runs on — a run left there would make the next run of the class «already sent today».
    /// </summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await hub.Notifications
            .Where(row => row.Type == EventsNotifications.ReportsToValidate && People.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
        await hub.UserPreferences.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);

        // A signal dismissed and left without its row by a rejection (the withdrawal's test).
        await hub.AwardSignals
            .Where(row => row.SourceModule == EventsModule.ModuleKey && People.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
        await hub.Notifications
            .Where(row => row.Type == NotificationTypes.AwardToAssign && People.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
        await hub.JobsLog
            .Where(entry => entry.Job == ReportsDigestJob.JobName && entry.StartedAt < new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .ExecuteDeleteAsync(cancellationToken);
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

    /// <summary>The airports of the class in the core's snapshot, away from every country; only those it adds go at the end.</summary>
    private async Task SeedAirportsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var (icao, latitude, longitude) in new[] { (Home, 45.0, 9.0), (Away, 41.0, 12.0) })
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

    private async Task SeedPersonAsync(int vid, string? position, CancellationToken cancellationToken)
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
        user.LastName = "Award";
        user.Email = $"e14b-{vid}@example.org";
        user.Locale = "en";
        user.IsStaff = position is not null;
        user.IsSuperadmin = false;
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

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    /// <summary>The network's sessions a test gives, by VID and kind; null while it does not answer.</summary>
    private sealed class Network : INetworkSessionSource
    {
        public bool Answers { get; set; } = true;

        public Dictionary<int, NetworkSession[]> Atc { get; } = [];

        public Dictionary<int, NetworkSession[]> Pilot { get; } = [];

        public Task<IReadOnlyList<NetworkSession>?> SessionsAsync(NetworkSessionQuery query, CancellationToken cancellationToken = default)
        {
            if (!Answers)
            {
                return Task.FromResult<IReadOnlyList<NetworkSession>?>(null);
            }

            var sessions = query is { Vid: { } vid, Connection: { } connection }
                ? (connection == NetworkConnection.Atc ? Atc : Pilot).GetValueOrDefault(vid, [])
                : [];

            return Task.FromResult<IReadOnlyList<NetworkSession>?>([.. sessions
                .Where(session => session.StartedAt >= query.FromUtc && session.StartedAt <= query.ToUtc)
                .OrderByDescending(session => session.StartedAt)]);
        }
    }
}
