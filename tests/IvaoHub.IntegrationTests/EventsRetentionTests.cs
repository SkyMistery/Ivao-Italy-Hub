using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registers;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The last phase of M4b (E15b; design §2.3-bis, §4.4, §11, §15; note 2026-10-17-la-conservazione-di-m4b), through the real host with the
/// division file of this repository — its grants to the coordinator and to the ATC operations, nothing given by hand:
/// <list type="bullet">
/// <item>the retention deletes the pilots' bookings and reports after <c>pilotRetentionMonths</c> and the availabilities with the event over,
/// keeps the shifts, the reports of the controllers and the requests to hand a shift over, and leaves the statistics and the register of the
/// pilots counted the same — and nothing of a deleted flight in the audit;</item>
/// <item>erasing a controller and a pilot leaves the two registers counted the same, under the pseudonym;</item>
/// <item>«Duplicate» copies the positions with their hours and the rules of award, and nothing of the members nor of the jobs;</item>
/// <item>the bookings of the network stand beside the roster, read when asked, «not available» when the network cannot say.</item>
/// </list>
/// <para>⚠️ The people are 761300–761308 (E15b's range, 761300–761329): the coordinator of the events, the ATC operations, the super
/// administrator, two controllers and two pilots — one of each erased —, a member, and the second erased controller's partner. The bookings of
/// the network are the recorded day of E15a (<c>atc-bookings-day.json</c>), whose people 761070–761079 the hub does not know.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsRetentionTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int CoordinatorVid = 761300;
    private const int AodVid = 761301;
    private const int SuperadminVid = 761302;
    private const int ControllerVid = 761303;
    private const int OtherControllerVid = 761304;
    private const int PilotVid = 761305;
    private const int ErasedControllerVid = 761306;
    private const int ErasedPilotVid = 761307;
    private const int MemberVid = 761308;

    private static readonly int[] People =
        [CoordinatorVid, AodVid, SuperadminVid, ControllerVid, OtherControllerVid, PilotVid, ErasedControllerVid, ErasedPilotVid, MemberVid];

    /// <summary>The recorded day of the network (E15a): the tower of Fiumicino 18–20 by 761079, the exam on Frankfurt's approach 18:30–20:30 by 761071.</summary>
    private const string RecordedTower = "LIRF_TWR";
    private const string RecordedApproach = "EDDF_APP";
    private const string RecordedOther = "LIMC_TWR";
    private const int TowerBooker = 761079;
    private const int ExamBooker = 761071;

    private const string Tower = "XXAA_TWR";
    private const string Fir = "XXAA";
    private const string Home = "XEM1";
    private const string Away = "XEM2";
    private const string Type = "XE3A";

    private const string SlugStem = "evt-test-e15b";
    private const string FlightWords = "EVTE15BX";

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private string? _settingsBefore;
    private readonly List<long> _awards = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);

        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", superadmin: false, token);
        await SeedPersonAsync(AodVid, $"{Division().Code}-AOC", superadmin: false, token);
        await SeedPersonAsync(SuperadminVid, position: null, superadmin: true, token);
        foreach (var vid in new[] { ControllerVid, OtherControllerVid, PilotVid, ErasedControllerVid, ErasedPilotVid, MemberVid })
        {
            await SeedPersonAsync(vid, position: null, superadmin: false, token);
        }

        // The design's numbers, written out so that a class before this one leaves nothing of its own.
        _settingsBefore = await WriteSettingsAsync(
            """{"pilotRetentionMonths":24,"inPersonRetentionMonths":3,"unflownThreshold":3,"noShowWeight":1}""",
            token);
    }

    public async ValueTask DisposeAsync()
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.AwardSignals.Where(signal => People.Contains(signal.Vid)).ExecuteDeleteAsync();
            await database.Awards.Where(award => _awards.Contains(award.Id)).ExecuteDeleteAsync();
            await database.UserGrants.Where(grant => grant.Vid != null && People.Contains(grant.Vid.Value)).ExecuteDeleteAsync();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.Users.Where(user => People.Contains(user.Vid)).ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false));
        }

        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The retention of M4b (§11, readings 1–4): an event that ended 25 months ago loses its bookings and its pilots' report — with its items
    /// and its signal of award, gone from the core's queue too —, keeps its shift, its controller's report and its request to hand a shift over,
    /// and its statistics say what was there, also after «Recalculate». One that ended 23 months ago keeps its bookings; every event over loses
    /// its availabilities, one to come keeps them. The register of the pilots counts the same before and after, and the audit keeps nothing of
    /// the flight of a deleted booking.
    /// </summary>
    [Fact]
    public async Task TheRetentionDeletesThePilotsRowsAfterThePeriodAndKeepsTheShiftsAndTheStatistics()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Hour(DateTime.UtcNow);

        var old = await EventRowAsync("retired", now.AddMonths(-25), now.AddMonths(-25).AddHours(6), token);
        var kept = await EventRowAsync("kept", now.AddMonths(-23), now.AddMonths(-23).AddHours(6), token);
        var ended = await EventRowAsync("ended", now.AddDays(-2), now.AddDays(-2).AddHours(6), token);
        var toCome = await EventRowAsync("to-come", now.AddDays(10), now.AddDays(10).AddHours(6), token, checksDone: false);

        // The old event: a shift and how it went, a request to hand it over, a controller's report, an availability with its window; two
        // bookings — one flown, one private and not flown, with its flight —, a pilot's report with its item and the signal of a rule it met.
        var position = await PositionRowAsync(old, Tower, now.AddMonths(-25), now.AddMonths(-25).AddHours(6), token);
        var shift = await ShiftRowAsync(old, position, ControllerVid, now.AddMonths(-25), now.AddMonths(-25).AddHours(1), ShiftAttendance.Attended, token);
        var transfer = await TransferRowAsync(old, shift, ControllerVid, OtherControllerVid, token);
        var atcReport = await ReportRowAsync(old, ControllerVid, ReportKind.Atc, token);
        await AvailabilityRowAsync(old, ControllerVid, now.AddMonths(-25), now.AddMonths(-25).AddHours(6), token);
        await BookingRowAsync(old, PilotVid, flown: true, callsign: null, token);
        await BookingRowAsync(old, PilotVid, flown: false, callsign: FlightWords, token);
        var pilotReport = await ReportRowAsync(old, PilotVid, ReportKind.Pilot, token);
        var award = await AwardAsync($"{SlugStem} retention", token);
        var signalSource = await ReportAwardRowAsync(old, pilotReport, PilotVid, award, token);

        // Within the period: a booking not flown the register counts. Over or to come: an availability each.
        await BookingRowAsync(kept, PilotVid, flown: false, callsign: null, token);
        await AvailabilityRowAsync(kept, ControllerVid, now.AddMonths(-23), now.AddMonths(-23).AddHours(6), token);
        await AvailabilityRowAsync(ended, ControllerVid, now.AddDays(-2), now.AddDays(-2).AddHours(6), token);
        await BookingRowAsync(ended, PilotVid, flown: true, callsign: null, token);
        await AvailabilityRowAsync(toCome, ControllerVid, now.AddDays(10), now.AddDays(10).AddHours(6), token);

        Assert.True(await AuditMentionsAsync(FlightWords, token), "Without the flight in the audit, the check below would prove nothing.");
        Assert.True(await SignalPendingAsync(signalSource, token));

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var before = RegisterPilot(await PageAsync(coordinator, $"{RegisterEndpoints.PilotsPattern}?q={PilotVid}", token), PilotVid);
        Assert.Equal(1, before.GetProperty("counted").GetInt32());

        // At least these: the shared database may hold another class's events past a retention.
        var outcome = await RunRetentionAsync(token);
        Assert.True(outcome is { Events: >= 3, Bookings: >= 2, Reports: >= 1, Availabilities: >= 3 }, $"{outcome}");

        // The pilots' rows of the old event are gone, the controllers' stay.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            Assert.Empty(await database.Bookings.IgnoreQueryFilters().Where(row => row.EventId == old).ToListAsync(token));
            Assert.Equal([atcReport], await database.Reports.IgnoreQueryFilters().Where(row => row.EventId == old).Select(row => row.Id).ToListAsync(token));
            Assert.Single(await database.ReportItems.IgnoreQueryFilters().Where(row => row.EventId == old).ToListAsync(token));
            Assert.Empty(await database.ReportAwards.Where(row => row.EventId == old).ToListAsync(token));
            Assert.Equal([shift], await database.AtcShifts.IgnoreQueryFilters().Where(row => row.EventId == old).Select(row => row.Id).ToListAsync(token));
            Assert.Equal([transfer], await database.AtcTransfers.IgnoreQueryFilters().Where(row => row.EventId == old).Select(row => row.Id).ToListAsync(token));

            // The availabilities of every event over go, with their windows; the one to come stays.
            foreach (var over in new[] { old, kept, ended })
            {
                Assert.Empty(await database.AtcAvailabilities.IgnoreQueryFilters().Where(row => row.EventId == over).ToListAsync(token));
                Assert.Empty(await database.AtcWindows.IgnoreQueryFilters().Where(row => row.EventId == over).ToListAsync(token));
            }

            Assert.Single(await database.AtcAvailabilities.IgnoreQueryFilters().Where(row => row.EventId == toCome).ToListAsync(token));
            Assert.Single(await database.AtcWindows.IgnoreQueryFilters().Where(row => row.EventId == toCome).ToListAsync(token));

            // Within the period, and over two days ago: the bookings stay.
            Assert.Single(await database.Bookings.IgnoreQueryFilters().Where(row => row.EventId == kept).ToListAsync(token));
            Assert.Single(await database.Bookings.IgnoreQueryFilters().Where(row => row.EventId == ended).ToListAsync(token));
        }

        // The signal waiting in the core's queue went with its row; the flight left the audit.
        Assert.False(await SignalPendingAsync(signalSource, token));
        Assert.False(await AuditMentionsAsync(FlightWords, token));

        // The statistics say what was there, and «Recalculate» keeps them.
        var expected = (Booked: 2, Flown: 1, Unflown: 1, Reports: 2, Accepted: 0, Signals: 1, Availabilities: 1, Shifts: 1, Attended: 1);
        Assert.Equal(expected, Numbers(await StatsAsync(old, token)));
        await SuccessAsync(await coordinator.PostAsync(StatsEndpoints.RecalculatePattern.Replace("{id:long}", old.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), content: null, token), token);
        Assert.Equal(expected, Numbers(await StatsAsync(old, token)));
        Assert.Equal(1, (await StatsAsync(ended, token)).AtcAvailabilities);

        // The register counts the same: the old booking was out of it already, the one within the period still is.
        var after = RegisterPilot(await PageAsync(coordinator, $"{RegisterEndpoints.PilotsPattern}?q={PilotVid}", token), PilotVid);
        Assert.Equal(before.GetProperty("counted").GetInt32(), after.GetProperty("counted").GetInt32());

        // Run again: nothing of the class's left to do.
        var again = await RunRetentionAsync(token);
        Assert.Equal(0, again.Events);
    }

    /// <summary>
    /// Erasing a controller and a pilot (§11.1: «the register becomes nobody's»): their shifts and bookings of an event over stay under the
    /// pseudonym, and each register counts them the same — the controller's attended, no-show and penalty, the pilot's bookings not flown —;
    /// the shift of an event to come goes, and the statistics of the event over do not move.
    /// </summary>
    [Fact]
    public async Task AnErasedControllerAndAnErasedPilotLeaveTheRegistersCountedTheSame()
    {
        var token = TestContext.Current.CancellationToken;
        var now = Hour(DateTime.UtcNow);
        var over = await EventRowAsync("erase-over", now.AddDays(-5), now.AddDays(-5).AddHours(6), token);
        var toCome = await EventRowAsync("erase-to-come", now.AddDays(5), now.AddDays(5).AddHours(6), token, checksDone: false);
        var position = await PositionRowAsync(over, Tower, now.AddDays(-5), now.AddDays(-5).AddHours(6), token);
        var later = await PositionRowAsync(toCome, Tower, now.AddDays(5), now.AddDays(5).AddHours(6), token);
        await ShiftRowAsync(over, position, ErasedControllerVid, now.AddDays(-5), now.AddDays(-5).AddHours(1), ShiftAttendance.Attended, token);
        await ShiftRowAsync(over, position, ErasedControllerVid, now.AddDays(-5).AddHours(2), now.AddDays(-5).AddHours(3), ShiftAttendance.NoShow, token);
        await ShiftRowAsync(over, position, OtherControllerVid, now.AddDays(-5).AddHours(1), now.AddDays(-5).AddHours(2), ShiftAttendance.Attended, token);
        await ShiftRowAsync(toCome, later, ErasedControllerVid, now.AddDays(5), now.AddDays(5).AddHours(1), ShiftAttendance.Unknown, token);
        await BookingRowAsync(over, ErasedPilotVid, flown: false, callsign: null, token);
        await BookingRowAsync(over, ErasedPilotVid, flown: false, callsign: null, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        await SuccessAsync(await coordinator.PostAsync(StatsUri(over), content: null, token), token);
        var statsBefore = Numbers(await StatsAsync(over, token));
        var controllerBefore = Counts(RegisterRow(await PageAsync(coordinator, $"{RegisterEndpoints.ControllersPattern}?q={ErasedControllerVid}", token), "controller", ErasedControllerVid));
        var pilotBefore = RegisterRow(await PageAsync(coordinator, $"{RegisterEndpoints.PilotsPattern}?q={ErasedPilotVid}", token), "pilot", ErasedPilotVid)
            .GetProperty("counted").GetInt32();
        Assert.Equal((1, 1, 0), (controllerBefore.Attended, controllerBefore.NoShows, controllerBefore.Excused));
        Assert.Equal(2, pilotBefore);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var controller = await ErasedAsync(superadmin, ErasedControllerVid, token);
        var pilot = await ErasedAsync(superadmin, ErasedPilotVid, token);

        // Under the pseudonym, counted the same; nothing left under the VID.
        var controllerAfter = Counts(RegisterRow(await PageAsync(coordinator, $"{RegisterEndpoints.ControllersPattern}?q={controller}", token), "controller", controller));
        Assert.Equal(controllerBefore, controllerAfter);
        Assert.DoesNotContain(
            await PageAsync(coordinator, $"{RegisterEndpoints.ControllersPattern}?q={ErasedControllerVid}", token),
            row => row.GetProperty("controller").GetProperty("vid").GetInt32() == ErasedControllerVid);
        Assert.Equal(
            pilotBefore,
            RegisterRow(await PageAsync(coordinator, $"{RegisterEndpoints.PilotsPattern}?q={pilot}", token), "pilot", pilot).GetProperty("counted").GetInt32());

        // The shift to come is uncovered again; the event over counts the same.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            Assert.Empty(await database.AtcShifts.IgnoreQueryFilters().Where(row => row.EventId == toCome).ToListAsync(token));
        }

        await SuccessAsync(await coordinator.PostAsync(StatsUri(over), content: null, token), token);
        Assert.Equal(statsBefore, Numbers(await StatsAsync(over, token)));
    }

    /// <summary>
    /// «Duplicate» for the ATC (§2.3-bis): the copy has the positions with their hours moved by the same difference, their callsign, FIR and
    /// note, and the rules of award as they were; none of the members' rows — no availability, shift, request, report or signal — and none of
    /// the marks of the jobs: its roster is proposed and published again at its own dates.
    /// </summary>
    [Fact]
    public async Task DuplicateForTheAtcCopiesThePositionsAndTheRulesAndNothingOfTheMembers()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(-3));
        var source = await EventRowAsync("duplicate", starts, starts.AddHours(6), token);
        var tower = await PositionRowAsync(source, Tower, starts, starts.AddHours(6), token, note: $"{SlugStem} the tower");
        var late = await PositionRowAsync(source, "XXAA_APP", starts.AddHours(2), starts.AddHours(5), token);
        var shift = await ShiftRowAsync(source, tower, ControllerVid, starts, starts.AddHours(1), ShiftAttendance.Attended, token);
        await TransferRowAsync(source, shift, ControllerVid, OtherControllerVid, token);
        await AvailabilityRowAsync(source, ControllerVid, starts, starts.AddHours(6), token);
        var award = await AwardAsync($"{SlugStem} duplicate", token);
        await RuleRowAsync(source, award, token);
        var report = await ReportRowAsync(source, ControllerVid, ReportKind.Atc, token);
        await ReportAwardRowAsync(source, report, ControllerVid, award, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var copy = (await CreatedAsync(await coordinator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{source}/duplicate",
                new EventDuplicateRequest(starts.AddDays(7), Slug("duplicate-copy"), PublicSlots: false),
                token), token))
            .GetProperty("event").GetProperty("id").GetInt64();

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == copy, token);
        Assert.True(row.HasRoster);
        Assert.Equal(PublishStatus.Draft, row.Status);
        Assert.Equal(
            ((DateTime?)null, (DateTime?)null, (DateTime?)null, (int?)null),
            (row.RosterProposedAt, row.RosterPublishedAt, row.AfterDoneAt, row.AutoReportsAfterVid));

        var positions = await database.AtcPositions.IgnoreQueryFilters().AsNoTracking()
            .Where(position => position.EventId == copy)
            .OrderBy(position => position.FromUtc)
            .ToListAsync(token);
        var originals = await database.AtcPositions.IgnoreQueryFilters().AsNoTracking()
            .Where(position => position.Id == tower || position.Id == late)
            .OrderBy(position => position.FromUtc)
            .ToListAsync(token);
        Assert.Equal(
            originals.Select(position => (position.Callsign, position.Fir, position.FromUtc.AddDays(7), position.ToUtc.AddDays(7), position.Note)),
            positions.Select(position => (position.Callsign, position.Fir, position.FromUtc, position.ToUtc, position.Note)));

        var rule = Assert.Single(await database.AwardRules.IgnoreQueryFilters().AsNoTracking().Where(candidate => candidate.EventId == copy).ToListAsync(token));
        Assert.Equal((award, ReportKind.Atc, AwardCriterion.AnyShift), (rule.AwardId, rule.Kind, rule.Criterion));

        Assert.Empty(await database.AtcAvailabilities.IgnoreQueryFilters().Where(candidate => candidate.EventId == copy).ToListAsync(token));
        Assert.Empty(await database.AtcWindows.IgnoreQueryFilters().Where(candidate => candidate.EventId == copy).ToListAsync(token));
        Assert.Empty(await database.AtcShifts.IgnoreQueryFilters().Where(candidate => candidate.EventId == copy).ToListAsync(token));
        Assert.Empty(await database.AtcTransfers.IgnoreQueryFilters().Where(candidate => candidate.EventId == copy).ToListAsync(token));
        Assert.Empty(await database.Reports.IgnoreQueryFilters().Where(candidate => candidate.EventId == copy).ToListAsync(token));
        Assert.Empty(await database.ReportAwards.Where(candidate => candidate.EventId == copy).ToListAsync(token));
    }

    /// <summary>
    /// The bookings of the network beside the roster (§4.4, §9.1, reading 6): of the recorded day, the ones on the event's callsigns in their
    /// positions' hours — the tower booked by the controller who holds the shift, the approach booked for an exam by somebody not on the roster
    /// —, never one on a callsign the event does not open or outside its position's hours; searched like the roster. Whoever does not read the
    /// ATC is refused; an event without a roster is not found; an event longer than the week the core asks says «not available».
    /// </summary>
    [Fact]
    public async Task TheBookingsOfTheNetworkStandBesideTheRoster()
    {
        var token = TestContext.Current.CancellationToken;
        var day = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(3), DateTimeKind.Utc);
        var id = await EventRowAsync("network", day.AddHours(17), day.AddHours(23), token, checksDone: false);
        var tower = await PositionRowAsync(id, RecordedTower, day.AddHours(17), day.AddHours(23), token);
        await PositionRowAsync(id, RecordedApproach, day.AddHours(17), day.AddHours(23), token);
        await PositionRowAsync(id, RecordedOther, day.AddHours(17), day.AddHours(18).AddMinutes(30), token);
        await ShiftRowAsync(id, tower, TowerBooker, day.AddHours(18), day.AddHours(20), ShiftAttendance.Unknown, token);

        using var aod = await SignedInAsync(AodVid, token);
        var answer = await OkAsync(await aod.GetAsync(NetworkUri(id), token), token);
        Assert.True(answer.GetProperty("available").GetBoolean());
        var rows = answer.GetProperty("items").EnumerateArray()
            .Select(row => (
                row.GetProperty("callsign").GetString(),
                row.GetProperty("fromUtc").GetDateTime(),
                row.GetProperty("toUtc").GetDateTime(),
                row.GetProperty("booker").GetProperty("vid").GetInt32(),
                row.GetProperty("kind").GetString(),
                row.GetProperty("onRoster").GetBoolean()))
            .ToList();
        Assert.Equal(
            [
                (RecordedTower, day.AddHours(18), day.AddHours(20), TowerBooker, "Controlling", true),
                (RecordedApproach, day.AddHours(18).AddMinutes(30), day.AddHours(20).AddMinutes(30), ExamBooker, "Exam", false),
            ],
            rows);

        // Nobody the hub knows booked them: named by their number alone.
        Assert.All(answer.GetProperty("items").EnumerateArray(), row => Assert.Equal(JsonValueKind.Null, row.GetProperty("booker").GetProperty("name").ValueKind));

        var searched = await OkAsync(await aod.GetAsync($"{NetworkUri(id)}?q=eddf", token), token);
        Assert.Equal([RecordedApproach], searched.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("callsign").GetString()));

        using (var member = await SignedInAsync(MemberVid, token))
        using (var refused = await member.GetAsync(NetworkUri(id), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var without = await EventRowAsync("network-no-roster", day.AddHours(17), day.AddHours(23), token, checksDone: false, hasRoster: false);
        using (var missing = await aod.GetAsync(NetworkUri(without), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        // Nine days of UTC: more than the core asks the network for — «not available», never «nobody booked».
        var week = await EventRowAsync("network-week", day, day.AddDays(8).AddHours(1), token, checksDone: false);
        await PositionRowAsync(week, RecordedTower, day, day.AddDays(8), token);
        var unavailable = await OkAsync(await aod.GetAsync(NetworkUri(week), token), token);
        Assert.False(unavailable.GetProperty("available").GetBoolean());
        Assert.Empty(unavailable.GetProperty("items").EnumerateArray());
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string NetworkUri(long eventId) => NetworkBookings.Pattern.Replace("{id:long}", eventId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string StatsUri(long eventId) => StatsEndpoints.RecalculatePattern.Replace("{id:long}", eventId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static DateTime Hour(DateTime instant) =>
        DateTime.SpecifyKind(new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, 0, 0), DateTimeKind.Utc);

    private static (int Booked, int Flown, int Unflown, int Reports, int Accepted, int Signals, int Availabilities, int Shifts, int Attended) Numbers(EventStats stats) =>
        (stats.Booked, stats.BookedFlown, stats.BookedUnflown, stats.ReportsSubmitted, stats.ReportsAccepted, stats.AwardSignals, stats.AtcAvailabilities, stats.AtcShifts, stats.AtcAttended);

    private static (int Attended, int NoShows, int Excused, double Penalty) Counts(JsonElement row) =>
        (row.GetProperty("attended").GetInt32(), row.GetProperty("noShows").GetInt32(), row.GetProperty("excused").GetInt32(), row.GetProperty("penalty").GetDouble());

    private static JsonElement RegisterPilot(List<JsonElement> rows, int vid) => RegisterRow(rows, "pilot", vid);

    private static JsonElement RegisterRow(List<JsonElement> rows, string person, int vid) =>
        Assert.Single(rows, row => row.GetProperty(person).GetProperty("vid").GetInt32() == vid);

    private static async Task<int> ErasedAsync(HttpClient superadmin, int vid, CancellationToken cancellationToken) =>
        (await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{vid}", content: null, cancellationToken), cancellationToken))
            .GetProperty("pseudonym").GetInt32();

    private static async Task SuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.IsSuccessStatusCode,
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

    private async Task<EventsRetentionJob.Outcome> RunRetentionAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsRetentionJob>().RunAsync(cancellationToken);
    }

    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    private async Task<bool> SignalPendingAsync(string source, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AwardSignals.AsNoTracking()
            .AnyAsync(signal => signal.SourceModule == EventsModule.ModuleKey && signal.SourceId == source && signal.Status == AwardSignalStatus.Pending, cancellationToken);
    }

    private async Task<EventStats> StatsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Stats.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(stats => stats.EventId == eventId, cancellationToken);
    }

    // ---- rows and people ------------------------------------------------------------------------------------------------------

    /// <summary>An event of the class, published, with a roster proposed and published and, once over, its checks done.</summary>
    private async Task<long> EventRowAsync(
        string name,
        DateTime starts,
        DateTime ends,
        CancellationToken cancellationToken,
        bool checksDone = true,
        bool hasRoster = true)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfe",
            PublicSlots = true,
            HasRoster = hasRoster,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = ends,
            BookingOpensAtUtc = starts.AddDays(-30),
            Status = PublishStatus.Published,
            PublishedAt = starts.AddDays(-30),
            Visibility = Visibility.Public,
            RosterProposedAt = hasRoster ? starts.AddDays(-3) : null,
            RosterPublishedAt = hasRoster ? starts.AddDays(-2) : null,
            AfterDoneAt = checksDone ? ends.AddHours(7) : null,
            AutoReportsAfterVid = checksDone ? MemberVid : null,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task<long> PositionRowAsync(long eventId, string callsign, DateTime from, DateTime to, CancellationToken cancellationToken, string? note = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var position = new EventAtcPosition
        {
            EventId = eventId,
            Callsign = callsign,
            Fir = Fir,
            FromUtc = from,
            ToUtc = to,
            Note = note,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcPositions.Add(position);
        await database.SaveChangesAsync(cancellationToken);
        return position.Id;
    }

    private async Task<long> ShiftRowAsync(
        long eventId,
        long positionId,
        int vid,
        DateTime from,
        DateTime to,
        ShiftAttendance attendance,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var shift = new EventAtcShift
        {
            EventId = eventId,
            PositionId = positionId,
            ControllerVid = vid,
            Fir = Fir,
            FromUtc = from,
            ToUtc = to,
            Origin = ShiftOrigin.Staff,
            NotifiedAt = from.AddDays(-2),
            Attendance = attendance,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcShifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken);
        return shift.Id;
    }

    private async Task<long> TransferRowAsync(long eventId, long shiftId, int vid, int to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var transfer = new EventAtcTransfer
        {
            EventId = eventId,
            ShiftId = shiftId,
            ControllerVid = vid,
            ToVid = to,
            Fir = Fir,
            Note = $"{SlugStem} a request",
            Status = TransferStatus.Refused,
            DecidedBy = AodVid,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcTransfers.Add(transfer);
        await database.SaveChangesAsync(cancellationToken);
        return transfer.Id;
    }

    private async Task AvailabilityRowAsync(long eventId, int vid, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var availability = new EventAtcAvailability
        {
            EventId = eventId,
            ControllerVid = vid,
            Note = $"{SlugStem} my evening",
            CreatedAt = from.AddDays(-10),
            UpdatedAt = from.AddDays(-10),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.AtcAvailabilities.Add(availability);
        await database.SaveChangesAsync(cancellationToken);

        database.AtcWindows.Add(new EventAtcWindow
        {
            AvailabilityId = availability.Id,
            EventId = eventId,
            ControllerVid = vid,
            FromUtc = from,
            ToUtc = to,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A booking of a public slot, checked after the event; with a callsign, the flight of a private slot as the pilot wrote it.</summary>
    private async Task BookingRowAsync(long eventId, int vid, bool flown, string? callsign, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var row = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == eventId, cancellationToken);

        var slot = new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Public,
            EventAirportIcao = Home,
            IsArrival = false,
            Callsign = $"XEN{Random.Shared.Next(10000, 99999)}",
            AircraftTypes = [Type],
            DepartureIcao = Home,
            ArrivalIcao = Away,
            OffBlockUtc = row.StartsAtUtc.AddMinutes(30),
            OnBlockUtc = row.StartsAtUtc.AddMinutes(90),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);

        database.Bookings.Add(new EventBooking
        {
            EventId = eventId,
            SlotId = slot.Id,
            BookerVid = vid,
            AircraftIcao = Type,
            Callsign = callsign,
            CreatedAt = row.StartsAtUtc.AddDays(-5),
            FlownCheckedAt = row.EndsAtUtc.AddHours(7),
            FlownAt = flown ? row.StartsAtUtc.AddMinutes(35) : null,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A report of support sent and waiting, with one item found: a flight for a pilot, a shift for a controller.</summary>
    private async Task<long> ReportRowAsync(long eventId, int vid, ReportKind kind, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var row = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == eventId, cancellationToken);

        var report = new EventReport
        {
            EventId = eventId,
            Vid = vid,
            Kind = kind,
            Source = ReportSource.Manual,
            State = ReportState.Submitted,
            Verification = ReportVerification.Valid,
            DecisionNote = $"{SlugStem} a note",
            SubmittedAt = row.EndsAtUtc.AddHours(1),
            CheckedAt = row.EndsAtUtc.AddHours(1),
            CreatedAt = row.EndsAtUtc.AddHours(1),
            UpdatedAt = row.EndsAtUtc.AddHours(1),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Reports.Add(report);
        await database.SaveChangesAsync(cancellationToken);

        database.ReportItems.Add(kind == ReportKind.Pilot
            ? new EventReportItem
            {
                ReportId = report.Id,
                EventId = eventId,
                Vid = vid,
                Callsign = "XEP1",
                DepartureIcao = Home,
                ArrivalIcao = Away,
                FlightDay = DateOnly.FromDateTime(row.StartsAtUtc),
                SessionId = 9150,
                Minutes = 60,
                DistanceNm = 300m,
                Verified = true,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            }
            : new EventReportItem
            {
                ReportId = report.Id,
                EventId = eventId,
                Vid = vid,
                Callsign = Tower,
                FromUtc = row.StartsAtUtc,
                ToUtc = row.StartsAtUtc.AddHours(1),
                Minutes = 60,
                Verified = true,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });
        await database.SaveChangesAsync(cancellationToken);

        return report.Id;
    }

    /// <summary>A rule met by a report, with its signal in the core's queue; returns the signal's source.</summary>
    private async Task<string> ReportAwardRowAsync(long eventId, long reportId, int vid, long awardId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new EventReportAward
        {
            ReportId = reportId,
            RuleId = 0,
            EventId = eventId,
            Vid = vid,
            AwardId = awardId,
            Reason = $"{SlugStem} the reason",
            CreatedAt = DateTime.UtcNow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.ReportAwards.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return EventReportAward.SourceOf(row.Id);
    }

    private async Task RuleRowAsync(long eventId, long awardId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        database.AwardRules.Add(new EventAwardRule
        {
            EventId = eventId,
            AwardId = awardId,
            Kind = ReportKind.Atc,
            Criterion = AwardCriterion.AnyShift,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<long> AwardAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var award = new Award { OwnerDepartment = Department.FOD, Name = Text(name), IsActive = true };
        hub.Awards.Add(award);
        await hub.SaveChangesAsync(cancellationToken);
        _awards.Add(award.Id);
        return award.Id;
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
        user.LastName = "Retention";
        user.Email = null;
        user.Locale = "en";
        user.IsStaff = position is not null;
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.UserStaffPositions.Where(row => row.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        if (position is not null)
        {
            var parsed = StaffRoleMap.Parse(position, Division().Code, new HashSet<string>([Fir], StringComparer.OrdinalIgnoreCase));
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
}
