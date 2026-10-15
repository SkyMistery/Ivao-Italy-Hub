using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
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
/// The roster of the events (M4, E11b; design §4.3, §4.4, §8.4; note 2026-10-13-la-proposta-del-roster), through the real host with the
/// division file of this repository — its grants to the ATC operations, to the team of a FIR and to the coordinator, nothing given by hand:
/// <list type="bullet">
/// <item><c>events-roster</c> proposes the roster of an event whose availabilities closed, once — run again it adds nothing —, with the why
/// of every shift and the experience the archive gives, without moving the row version of the event; a draft and an event still open are
/// not proposed;</item>
/// <item>the staff correct it with warnings and refusals: nobody who never signed in, nothing outside the position or on a piece held,
/// never a shift of one's own, and nothing before the proposal; with <c>own</c> the chief of a FIR reads and corrects the roster of their
/// positions alone;</item>
/// <item>a position with shifts keeps its callsign and its hours around them, takes them away until the start, and an event with shifts is
/// not deleted;</item>
/// <item>erasing a controller's data deletes their shifts to come and keeps the ones of an event over without the staff's note and the why,
/// in the rows and in the audit.</item>
/// </list>
/// The FIRs and the positions are the test's own, as in <c>EventsAtcTests</c>; the archive of sessions is a double that knows the
/// connections of one controller of the class.
/// <para>⚠️ The people are 761114–761121, after E11a's: the ATC operations, the chief of the tower's FIR, two controllers rated the same — one
/// with connections on the tower —, a trainee below the least rating, the coordinator of the events, the person erased and the superadmin;
/// <see cref="NeverVid"/> is nobody the hub knows. None has an address.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsRosterTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int AodVid = 761114;
    private const int ChiefVid = 761115;
    private const int ExperiencedVid = 761116;
    private const int EarlierVid = 761117;
    private const int TraineeVid = 761118;
    private const int CoordinatorVid = 761119;
    private const int PersonVid = 761120;
    private const int SuperadminVid = 761121;

    /// <summary>A VID nobody of the hub has: the class makes sure of it.</summary>
    private const int NeverVid = 761199;

    /// <summary>A controller whose history the class's archive cannot read: an availability of theirs makes an event's proposal fail.</summary>
    private const int BrokenVid = 761198;

    private static readonly int[] People = [AodVid, ChiefVid, ExperiencedVid, EarlierVid, TraineeVid, CoordinatorVid, PersonVid, SuperadminVid];

    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";
    private const string Tower = "XXAA_TWR";
    private const string Approach = "XXBB_APP";

    private const string SlugStem = "evt-test-e11b";

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private string? _settingsBefore;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IFirDirectory, TwoFirs>();
            services.AddScoped<IAtcPositionDirectory, TwoPositions>();
            services.AddScoped<IAtcActivitySource, ArchiveOfTheClass>();
        }));
        var token = TestContext.Current.CancellationToken;

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);

        var ladder = IvaoRatings.Vocabulary.Ladder(RatingKind.Atc);
        await SeedPersonAsync(AodVid, $"{Division().Code}-AOC", rating: null, superadmin: false, token);
        await SeedPersonAsync(ChiefVid, $"{Fir}-CH", rating: null, superadmin: false, token);
        await SeedPersonAsync(ExperiencedVid, position: null, ladder[3].Number, superadmin: false, token);
        await SeedPersonAsync(EarlierVid, position: null, ladder[3].Number, superadmin: false, token);
        await SeedPersonAsync(TraineeVid, position: null, ladder[1].Number, superadmin: false, token);
        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", ladder[3].Number, superadmin: false, token);
        await SeedPersonAsync(PersonVid, position: null, ladder[3].Number, superadmin: false, token);
        await SeedPersonAsync(SuperadminVid, position: null, rating: null, superadmin: true, token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<HubDbContext>().Users.Where(user => user.Vid == NeverVid).ExecuteDeleteAsync(token);
        }

        // The least rating the third of the ladder, the close three days before, an hour a shift, two in a row and one of break.
        _settingsBefore = await WriteSettingsAsync(
            $$"""{"minimumAtcRating":{{ladder[2].Number}},"applicationsCloseDays":3,"rosterPublishDays":2,"shiftMinutes":60,"maxConsecutiveShifts":2,"breakShifts":1}""",
            token);
    }

    public async ValueTask DisposeAsync()
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false).SetProperty(user => user.RatingAtc, (int?)null));
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of E11b on the server: at the close the job proposes the roster — the tower and the approach of an evening, two
    /// controllers rated the same, two shifts in a row and one of break —, every shift with its why, the experience the archive gave on
    /// the tower in it, and the hour nobody may take any more left uncovered; run again, nothing changes. The mark leaves the row version of
    /// the event as it was. A draft and an event whose availabilities are still open wait.
    /// </summary>
    [Fact]
    public async Task TheJobProposesTheRosterOnceAtTheCloseWithTheWhyOfEveryShift()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, _) = Window(days: 1);
        // A day of its own: the other events of the class, at the same hours with the same controllers, would warn of another shift.
        var id = await ProposableAsync("propose", starts, token);
        var draft = await ProposableAsync("propose-draft", starts, token, status: PublishStatus.Draft);
        var open = await ProposableAsync("propose-open", Window(days: 20).Starts, token);
        var versionBefore = (await EventAsync(id, token)).RowVersion;

        Assert.True(await RunJobAsync(token) >= 1);

        // At five the tower to whoever opened it before — the experience —, the approach to the other, the only one left; at six the
        // same; at seven both had two in a row.
        var positions = await PositionIdsAsync(id, token);
        var shifts = (await ShiftsAsync(id, token)).OrderBy(shift => shift.FromUtc).ThenBy(shift => shift.PositionId != positions[Tower]).ToList();
        Assert.Equal(
            [
                (positions[Tower], ExperiencedVid, starts),
                (positions[Approach], EarlierVid, starts),
                (positions[Tower], ExperiencedVid, starts.AddHours(1)),
                (positions[Approach], EarlierVid, starts.AddHours(1)),
            ],
            shifts.Select(shift => (shift.PositionId, shift.ControllerVid, shift.FromUtc)));
        Assert.All(shifts, shift => Assert.Equal(ShiftOrigin.Proposed, shift.Origin));
        Assert.All(shifts, shift => Assert.Equal(shift.PositionId == positions[Tower] ? Fir : OtherFir, shift.Fir));

        // The why as the database keeps it: the criterion as its word.
        Assert.Contains("\"decidedBy\":\"Experience\"", shifts[0].ReasonJson, StringComparison.Ordinal);
        var tower = shifts[0].Reason!;
        Assert.Equal((2, ShiftCriterion.Experience, 3, 3), (tower.Candidates, tower.DecidedBy, tower.CallsignConnections, tower.TypeConnections));
        var approach = shifts[1].Reason!;
        Assert.Equal((2, ShiftCriterion.Only, 0, 0), (approach.Candidates, approach.DecidedBy, approach.CallsignConnections, approach.TypeConnections));

        var row = await EventAsync(id, token);
        Assert.NotNull(row.RosterProposedAt);
        Assert.Equal(versionBefore, row.RowVersion);
        Assert.Null((await EventAsync(draft, token)).RosterProposedAt);
        Assert.Null((await EventAsync(open, token)).RosterProposedAt);
        Assert.Empty(await ShiftsAsync(draft, token));

        // The roster the staff read: the four shifts, and the tower's last hour uncovered — two could take it, both at their most in a row.
        using var aod = await SignedInAsync(AodVid, token);
        var roster = await RosterAsync(aod, id, token);
        Assert.Equal(5, roster.Count);
        var uncovered = Assert.Single(roster, entry => entry.GetProperty("state").GetString() == "Uncovered");
        Assert.Equal((Tower, starts.AddHours(2), 2), (uncovered.GetProperty("callsign").GetString(), uncovered.GetProperty("fromUtc").GetDateTime(), uncovered.GetProperty("candidates").GetInt32()));
        // No warning on the tower; the approach went below its preferred rating — nobody held it —, and the roster says so.
        var proposedWarnings = roster.Where(entry => entry.GetProperty("state").GetString() == "Proposed")
            .Select(entry => (entry.GetProperty("callsign").GetString(), string.Join(',', entry.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()))))
            .ToList();
        Assert.All(proposedWarnings.Where(entry => entry.Item1 == Tower), entry => Assert.Equal(string.Empty, entry.Item2));
        Assert.All(proposedWarnings.Where(entry => entry.Item1 == Approach), entry => Assert.Equal(nameof(ShiftWarning.BelowPreferred), entry.Item2));

        // Run again: the same roster, the same mark.
        await RunJobAsync(token);
        Assert.Equal(4, (await ShiftsAsync(id, token)).Count);
        Assert.Equal(row.RosterProposedAt, (await EventAsync(id, token)).RosterProposedAt);

        // Published since the close: it is proposed at the next run.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(candidate => candidate.Id == draft)
                .ExecuteUpdateAsync(set => set.SetProperty(candidate => candidate.Status, PublishStatus.Published), token);
        }

        await RunJobAsync(token);
        Assert.NotNull((await EventAsync(draft, token)).RosterProposedAt);
    }

    /// <summary>
    /// The correction (§4.4): nothing before the proposal; after it the ATC operations add on an uncovered piece — anybody who signed in,
    /// a trainee below the least rating who never gave availability too, with the warnings on the roster —, and are refused somebody who
    /// never signed in, a piece held, hours outside the position; they move a proposed shift, which becomes the staff's and loses its why, and
    /// take one away, whose piece is uncovered again. Nobody writes a shift of their own: the coordinator gives one to the ATC operations,
    /// who can neither move it nor take it away.
    /// </summary>
    [Fact]
    public async Task TheStaffCorrectTheRosterWithWarningsAndNeverAShiftOfTheirOwn()
    {
        var token = TestContext.Current.CancellationToken;
        using var aod = await SignedInAsync(AodVid, token);

        // Before the proposal: nothing.
        var waiting = await ProposableAsync("correct-waiting", Window(days: 20).Starts, token);
        var waitingTower = (await PositionIdsAsync(waiting, token))[Tower];
        Assert.Equal(
            ["events:errors.rosterNotProposed"],
            (await RefusedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(waitingTower, EarlierVid, Window(days: 20).Starts, 1), token), token))["positionId"]);

        var (starts, _) = Window(days: 2);
        var id = await ProposableAsync("correct", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);
        var late = starts.AddHours(2);

        // Somebody who never signed in; a piece held; hours outside the tower.
        Assert.Equal(["events:errors.controllerNeverSignedIn"], (await RefusedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], NeverVid, late, 1), token), token))["controllerVid"]);
        Assert.Equal(["events:errors.shiftTaken"], (await RefusedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], TraineeVid, starts.AddMinutes(30), 1), token), token))["fromUtc"]);
        Assert.Equal(["events:errors.shiftOutsidePosition"], (await RefusedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], TraineeVid, starts.AddHours(3), 1), token), token))["fromUtc"]);

        // The trainee on the tower's last hour: written, the staff's, with the warnings.
        var added = await CreatedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], TraineeVid, late, 1), token), token);
        Assert.Equal(nameof(ShiftOrigin.Staff), added.GetProperty("origin").GetString());
        var row = Assert.Single(await RosterAsync(aod, id, token), entry => entry.GetProperty("shiftId").ValueKind == JsonValueKind.Number && entry.GetProperty("shiftId").GetInt64() == added.GetProperty("id").GetInt64());
        var warnings = row.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()).ToList();
        Assert.Contains(nameof(ShiftWarning.NotAvailable), warnings);
        Assert.Contains(nameof(ShiftWarning.BelowMinimum), warnings);
        Assert.Equal(TraineeVid, row.GetProperty("controller").GetProperty("vid").GetInt32());

        // A proposed shift moved to the trainee: the staff's, without the why; then taken away, and its piece uncovered again.
        var proposed = (await ShiftsAsync(id, token)).Single(shift => shift.PositionId == positions[Tower] && shift.FromUtc == starts);
        var moved = await OkAsync(await aod.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{proposed.Id}", Shift(positions[Tower], TraineeVid, starts, 1) with { RowVersion = proposed.RowVersion }, token), token);
        Assert.Equal(nameof(ShiftOrigin.Staff), moved.GetProperty("origin").GetString());
        var stored = (await ShiftsAsync(id, token)).Single(shift => shift.Id == proposed.Id);
        Assert.Equal((TraineeVid, (string?)null), (stored.ControllerVid, stored.ReasonJson));

        using (var response = await aod.DeleteAsync($"{ShiftEndpoints.Pattern}/{proposed.Id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Contains(
            await RosterAsync(aod, id, token),
            entry => entry.GetProperty("state").GetString() == "Uncovered" && entry.GetProperty("callsign").GetString() == Tower && entry.GetProperty("fromUtc").GetDateTime() == starts);

        // Their own: refused to them, the super administrator's rule too; the coordinator gives it, and they cannot move it or take it away.
        using (var response = await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], AodVid, starts, 1), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var theirs = await CreatedAsync(await coordinator.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], AodVid, starts, 1), token), token);
        var theirsId = theirs.GetProperty("id").GetInt64();
        using (var response = await aod.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{theirsId}", Shift(positions[Tower], AodVid, starts, 1) with { RowVersion = theirs.GetProperty("rowVersion").GetDateTime() }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using (var response = await aod.DeleteAsync($"{ShiftEndpoints.Pattern}/{theirsId}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>
    /// With <c>firStaffScope: own</c> the chief of the tower's FIR reads the roster of the tower alone, corrects it, and is refused the
    /// approach of the other FIR — the roster, the endpoint and the write guard saying the same.
    /// </summary>
    [Fact]
    public async Task WithOwnTheChiefOfAFirReadsAndCorrectsTheRosterOfTheirPositionsAlone()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, _) = Window(days: 2);
        var id = await ProposableAsync("own", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);

        using var chief = await SignedInAsync(ChiefVid, token);
        var roster = await RosterAsync(chief, id, token);
        Assert.NotEmpty(roster);
        Assert.All(roster, entry => Assert.Equal(Tower, entry.GetProperty("callsign").GetString()));

        var approachShift = (await ShiftsAsync(id, token)).First(shift => shift.PositionId == positions[Approach]);
        using (var response = await chief.DeleteAsync($"{ShiftEndpoints.Pattern}/{approachShift.Id}", token))
        {
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"{response.StatusCode}");
        }

        using (var response = await chief.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Approach], TraineeVid, starts.AddHours(1).AddMinutes(30), 0) with { ToUtc = starts.AddHours(2) }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal(4, (await ShiftsAsync(id, token)).Count);

        var towerShift = (await ShiftsAsync(id, token)).First(shift => shift.PositionId == positions[Tower]);
        using (var response = await chief.DeleteAsync($"{ShiftEndpoints.Pattern}/{towerShift.Id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        await CreatedAsync(await chief.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Tower], TraineeVid, towerShift.FromUtc, 1), token), token);
    }

    /// <summary>
    /// A position with shifts (reading 8): its callsign does not change and its hours do not leave a shift out; deleted before the start it
    /// takes its shifts with it, each with its row in the audit; after the start it stays. An event with shifts is not deleted.
    /// </summary>
    [Fact]
    public async Task APositionWithShiftsKeepsThemAndAnEventWithShiftsIsNotDeleted()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 2);
        var id = await ProposableAsync("positions", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);
        using var aod = await SignedInAsync(AodVid, token);

        var tower = (await PositionsAsync(id, token)).Single(position => position.Id == positions[Tower]);
        Assert.Equal(
            ["events:errors.positionHasShifts"],
            (await RefusedAsync(await aod.PutAsJsonAsync($"{AtcPositionEndpoints.Pattern}/{tower.Id}", new EventAtcPositionWriteDto(id, Approach, tower.FromUtc, tower.ToUtc, null, tower.RowVersion), token), token))["callsign"]);
        Assert.Equal(
            ["events:errors.positionHasShifts"],
            (await RefusedAsync(await aod.PutAsJsonAsync($"{AtcPositionEndpoints.Pattern}/{tower.Id}", new EventAtcPositionWriteDto(id, Tower, tower.FromUtc.AddHours(1), tower.ToUtc, null, tower.RowVersion), token), token))["fromUtc"]);

        // An event with a shift and nobody's availability — written by the staff for somebody who never gave one —: not deleted.
        var named = await EventRowAsync("positions-named", starts, ends, PublishStatus.Published, token);
        var namedPosition = await PositionRowAsync(named, Tower, starts, ends, token);
        await ShiftRowAsync(named, namedPosition, Fir, TraineeVid, starts, starts.AddHours(1), note: null, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        Assert.Equal(["events:errors.eventHasShifts"], (await RefusedAsync(await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{named}", token), token))["id"]);

        // Deleted before the start: its two shifts go, and the audit says so.
        var towerShifts = (await ShiftsAsync(id, token)).Where(shift => shift.PositionId == tower.Id).Select(shift => shift.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(2, towerShifts.Count);
        using (var response = await aod.DeleteAsync($"{AtcPositionEndpoints.Pattern}/{tower.Id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.DoesNotContain(await ShiftsAsync(id, token), shift => shift.PositionId == tower.Id);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.CountAsync(
                row => row.Entity == "evt_atc_shifts" && row.Action == "deleted" && row.Vid == AodVid && towerShifts.Contains(row.EntityId),
                token));
        }

        // An event in progress: its position with a shift stays.
        var now = DateTime.UtcNow;
        var running = await EventRowAsync("positions-running", now.AddHours(-1), now.AddHours(5), PublishStatus.Published, token);
        var position = await PositionRowAsync(running, Tower, now.AddHours(-1), now.AddHours(5), token);
        await ShiftRowAsync(running, position, Fir, EarlierVid, now.AddHours(-1), now, note: null, token);
        Assert.Equal(["events:errors.positionHasShifts"], (await RefusedAsync(await aod.DeleteAsync($"{AtcPositionEndpoints.Pattern}/{position}", token), token))["id"]);
    }

    /// <summary>
    /// Erasing a controller's data (§11.1, the rule of the availabilities): their shift of an event still to end goes — uncovered again —;
    /// the one of an event over stays under the pseudonym without the staff's note and the why; and a shift taken away before the erasure
    /// leaves nothing of the note in the audit (E10n, the marks on the two columns).
    /// </summary>
    [Fact]
    public async Task AnErasedControllerLeavesTheShiftsOfEventsOverWithoutTheirNotesAndNoneToCome()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var toCome = await EventRowAsync("erase-to-come", now.AddDays(10), now.AddDays(10).AddHours(6), PublishStatus.Published, token);
        var over = await EventRowAsync("erase-over", now.AddDays(-10), now.AddDays(-10).AddHours(6), PublishStatus.Published, token);
        var toComePosition = await PositionRowAsync(toCome, Tower, now.AddDays(10), now.AddDays(10).AddHours(6), token);
        var overPosition = await PositionRowAsync(over, Tower, now.AddDays(-10), now.AddDays(-10).AddHours(6), token);
        await ShiftRowAsync(toCome, toComePosition, Fir, PersonVid, now.AddDays(10), now.AddDays(10).AddHours(1), note: null, token);
        var kept = await ShiftRowAsync(over, overPosition, Fir, PersonVid, now.AddDays(-10), now.AddDays(-10).AddHours(1), "evt-test-e11b their attendance", token);

        // A shift with a note, taken away before the erasure: its copies in the audit hold the note until the core empties them.
        var gone = await ShiftRowAsync(over, overPosition, Fir, PersonVid, now.AddDays(-10).AddHours(2), now.AddDays(-10).AddHours(3), GoneWords, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            database.AtcShifts.Remove(await database.AtcShifts.IgnoreQueryFilters().SingleAsync(shift => shift.Id == gone, token));
            await database.SaveChangesAsync(token);
        }

        Assert.True(await AuditMentionsAsync(GoneWords, token), "Without the note in the audit, the check below would prove nothing.");

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.shiftsToCome", 1, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.shiftsOver", 1, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, ShiftLines(await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", token), token)));

        var erased = await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token);
        Assert.Equal(lines, ShiftLines(erased));
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();

        Assert.Empty(await ShiftsAsync(toCome, token));
        var stored = Assert.Single(await ShiftsAsync(over, token));
        Assert.Equal((kept, pseudonym, (string?)null, (string?)null), (stored.Id, stored.ControllerVid, stored.AttendanceNote, stored.ReasonJson));
        Assert.False(await AuditMentionsAsync(GoneWords, token));
    }

    private const string GoneWords = "evt-test-e11b gone words";

    /// <summary>
    /// Two corrections of the same uncovered piece at once (the review of #262, point 1): the second waits on the event's row the first holds
    /// — here a transaction of the test, which writes its shift there and commits —, then reads it and is refused. A request that read before
    /// the other committed would have passed: the lock is what makes the check true.
    /// </summary>
    [Fact]
    public async Task TwoCorrectionsOfOnePieceAtOnceMeetOnTheEventsRow()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, _) = Window(days: 2);
        var id = await ProposableAsync("race", starts, token);
        await RunJobAsync(token);
        var tower = (await PositionIdsAsync(id, token))[Tower];
        var late = starts.AddHours(2);
        using var aod = await SignedInAsync(AodVid, token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var held = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            await database.Database.SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {id} FOR UPDATE").ToListAsync(token);

            var request = aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(tower, TraineeVid, late, 1), token);
            await WaitForALockWaitAsync(token);

            database.AtcShifts.Add(new EventAtcShift
            {
                EventId = id,
                PositionId = tower,
                ControllerVid = CoordinatorVid,
                Fir = Fir,
                FromUtc = late,
                ToUtc = late.AddHours(1),
                Origin = ShiftOrigin.Staff,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });
            await database.SaveChangesAsync(token);
            await held.CommitAsync(token);

            Assert.Equal(["events:errors.shiftTaken"], (await RefusedAsync(await request, token))["fromUtc"]);
        }

        Assert.Equal([CoordinatorVid], (await ShiftsAsync(id, token)).Where(shift => shift.FromUtc == late).Select(shift => shift.ControllerVid));
    }

    /// <summary>
    /// A position's change and deletion wait on the event's row too (the second reading of #262, point 1): a position shortened while a shift
    /// is added in the hours it loses reads that shift and refuses; a position deleted while a shift is added takes that shift with it, with its
    /// row in the audit.
    /// </summary>
    [Fact]
    public async Task APositionChangedOrDeletedWhileAShiftIsAddedMeetsItOnTheEventsRow()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 2);
        var id = await EventRowAsync("position-race", starts, ends, PublishStatus.Published, token);
        var shortened = await PositionRowAsync(id, Tower, starts, ends, token);
        var deleted = await PositionRowAsync(id, Approach, starts, ends, token);
        using var aod = await SignedInAsync(AodVid, token);

        var stored = (await PositionsAsync(id, token)).Single(position => position.Id == shortened);
        var change = new EventAtcPositionWriteDto(id, Tower, starts.AddHours(1), ends, null, stored.RowVersion);
        await HoldWhileAsync(id, () => aod.PutAsJsonAsync($"{AtcPositionEndpoints.Pattern}/{shortened}", change, token), shortened, Fir, starts, token, async request =>
            Assert.Equal(["events:errors.positionHasShifts"], (await RefusedAsync(await request, token))["fromUtc"]));

        long added = 0;
        await HoldWhileAsync(id, () => aod.DeleteAsync($"{AtcPositionEndpoints.Pattern}/{deleted}", token), deleted, OtherFir, starts, token, async request =>
        {
            using var response = await request;
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }, shift => added = shift);

        Assert.DoesNotContain(await PositionsAsync(id, token), position => position.Id == deleted);
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AnyAsync(
            row => row.Entity == "evt_atc_shifts" && row.Action == "deleted" && row.EntityId == added.ToString(System.Globalization.CultureInfo.InvariantCulture),
            token));
    }

    /// <summary>
    /// A request started while the test holds the event's row: once it waits on the lock, the test writes a shift on the position in its first
    /// hour and commits, and the request goes on and is judged.
    /// </summary>
    private async Task HoldWhileAsync(
        long eventId,
        Func<Task<HttpResponseMessage>> start,
        long positionId,
        string fir,
        DateTime from,
        CancellationToken cancellationToken,
        Func<Task<HttpResponseMessage>, Task> judge,
        Action<long>? written = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using var held = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await database.Database.SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} FOR UPDATE").ToListAsync(cancellationToken);

        var request = start();
        await WaitForALockWaitAsync(cancellationToken);

        var shift = new EventAtcShift
        {
            EventId = eventId,
            PositionId = positionId,
            ControllerVid = CoordinatorVid,
            Fir = fir,
            FromUtc = from,
            ToUtc = from.AddHours(1),
            Origin = ShiftOrigin.Staff,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.AtcShifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken);
        await held.CommitAsync(cancellationToken);
        written?.Invoke(shift.Id);

        await judge(request);
    }

    /// <summary>
    /// The refusals of the correction the first round did not read (the review of #262, point 6, and Carmine's answer on #259): a position no
    /// event has, a position of another event, a controller holding another position of the event at those hours — and in another event at
    /// the same hours only a warning —; for the chief of a FIR, moving a shift to another FIR's position; and the rows the roster says the
    /// reader may write — never their own shift.
    /// </summary>
    [Fact]
    public async Task TheCorrectionKeepsAShiftToItsEventItsFirAndOneControllerAtATime()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, _) = Window(days: 2);
        var id = await ProposableAsync("refusals", starts, token);
        var other = await ProposableAsync("refusals-other", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);
        var otherTower = (await PositionIdsAsync(other, token))[Tower];
        using var aod = await SignedInAsync(AodVid, token);

        Assert.Equal(["events:errors.positionUnknownForShift"], (await RefusedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(long.MaxValue, TraineeVid, starts, 1), token), token))["positionId"]);

        var towerShift = (await ShiftsAsync(id, token)).Single(shift => shift.PositionId == positions[Tower] && shift.FromUtc == starts);
        Assert.Equal(
            ["events:errors.positionOfAnotherEvent"],
            (await RefusedAsync(await aod.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{towerShift.Id}", Shift(otherTower, towerShift.ControllerVid, starts, 1) with { RowVersion = towerShift.RowVersion }, token), token))["positionId"]);

        // The approach at five freed, and the controller of the tower at five put there too: one controller, two positions at once.
        var approachShift = (await ShiftsAsync(id, token)).Single(shift => shift.PositionId == positions[Approach] && shift.FromUtc == starts);
        using (var response = await aod.DeleteAsync($"{ShiftEndpoints.Pattern}/{approachShift.Id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(["events:errors.controllerBusy"], (await RefusedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Approach], towerShift.ControllerVid, starts, 1), token), token))["controllerVid"]);

        // The same on a change (the second reading of #262, point 2): the approach at six given to the controller of the tower at six.
        var approachLater = (await ShiftsAsync(id, token)).Single(shift => shift.PositionId == positions[Approach] && shift.FromUtc == starts.AddHours(1));
        var towerLater = (await ShiftsAsync(id, token)).Single(shift => shift.PositionId == positions[Tower] && shift.FromUtc == starts.AddHours(1));
        Assert.Equal(
            ["events:errors.controllerBusy"],
            (await RefusedAsync(await aod.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{approachLater.Id}", Shift(positions[Approach], towerLater.ControllerVid, approachLater.FromUtc, 1) with { RowVersion = approachLater.RowVersion }, token), token))["controllerVid"]);

        // In another event at the same hours the same controller holds the tower too: a warning, never a refusal.
        var roster = await RosterAsync(aod, id, token);
        var row = Assert.Single(roster, entry => entry.GetProperty("shiftId").ValueKind == JsonValueKind.Number && entry.GetProperty("shiftId").GetInt64() == towerShift.Id);
        Assert.Contains(nameof(ShiftWarning.Overlapping), row.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()));

        // The piece freed: two could take it, the controller of the tower is busy, the other free — and the roster says one is free.
        var freed = Assert.Single(roster, entry => entry.GetProperty("state").GetString() == "Uncovered" && entry.GetProperty("callsign").GetString() == Approach && entry.GetProperty("fromUtc").GetDateTime() == starts);
        Assert.Equal((2, 1, true), (freed.GetProperty("candidates").GetInt32(), freed.GetProperty("free").GetInt32(), freed.GetProperty("editable").GetBoolean()));

        // The chief of the tower's FIR: the tower's shift not moved onto the approach of the other FIR; the rows of the tower theirs to write.
        using var chief = await SignedInAsync(ChiefVid, token);
        var current = (await ShiftsAsync(id, token)).Single(shift => shift.Id == towerShift.Id);
        using (var response = await chief.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{towerShift.Id}", Shift(positions[Approach], current.ControllerVid, starts.AddHours(1), 1) with { RowVersion = current.RowVersion }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.All(await RosterAsync(chief, id, token), entry => Assert.True(entry.GetProperty("editable").GetBoolean()));
        var listed = await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.Pattern}?filter[eventId]={id}&pageSize=100", token), token);
        Assert.Equal([(Tower, true)], listed.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("callsign").GetString(), item.GetProperty("editable").GetBoolean())));

        // A shift of the ATC operations themselves: on the roster not theirs to write.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var theirs = await CreatedAsync(await coordinator.PostAsJsonAsync(ShiftEndpoints.Pattern, Shift(positions[Approach], AodVid, starts, 1), token), token);
        var own = Assert.Single(await RosterAsync(aod, id, token), entry => entry.GetProperty("shiftId").ValueKind == JsonValueKind.Number && entry.GetProperty("shiftId").GetInt64() == theirs.GetProperty("id").GetInt64());
        Assert.False(own.GetProperty("editable").GetBoolean());
    }

    /// <summary>
    /// The conditions of the maintainer that follow the clock. From the event's start (Carmine's answer on #262) a shift is not taken away, its
    /// controller, its position and its start do not change, and its end moves only to now or later: a cut at the present moment passes, an end
    /// already past is refused, and the absent controller's replacement is a new shift from the cut. Once the roster is proposed (his answer on
    /// #259) the availabilities stay closed, even when the start moves later.
    /// </summary>
    [Fact]
    public async Task FromTheStartAShiftIsOnlyCutAndAProposedRosterKeepsTheAvailabilitiesClosed()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var running = await EventRowAsync("start-running", now.AddHours(-1), now.AddHours(5), PublishStatus.Published, token);
        var position = await PositionRowAsync(running, Tower, now.AddHours(-1), now.AddHours(5), token);
        var otherPosition = await PositionRowAsync(running, Approach, now.AddHours(-1), now.AddHours(5), token);
        var shift = await ShiftRowAsync(running, position, Fir, EarlierVid, now.AddHours(-1), now.AddHours(2), note: null, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(row => row.Id == running)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.RosterProposedAt, now.AddDays(-3)), token);
        }

        using var aod = await SignedInAsync(AodVid, token);
        Assert.Equal(["events:errors.shiftAfterStart"], (await RefusedAsync(await aod.DeleteAsync($"{ShiftEndpoints.Pattern}/{shift}", token), token))["id"]);

        var stored = (await ShiftsAsync(running, token)).Single(row => row.Id == shift);
        var same = Shift(position, EarlierVid, stored.FromUtc, 3) with { RowVersion = stored.RowVersion };
        foreach (var (change, field) in new[]
        {
            (same with { ControllerVid = TraineeVid }, "controllerVid"),
            (same with { PositionId = otherPosition }, "positionId"),
            (same with { FromUtc = stored.FromUtc.AddMinutes(30) }, "fromUtc"),
            (same with { ToUtc = now.AddMinutes(-10) }, "toUtc"),
        })
        {
            Assert.Equal(["events:errors.shiftAfterStart"], (await RefusedAsync(await aod.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{shift}", change, token), token))[field]);
        }

        // Cut at the present moment — a minute from now, which the save still finds to come —, and a replacement from the cut.
        var soon = DateTime.UtcNow.AddMinutes(1);
        var cut = new DateTime(soon.Year, soon.Month, soon.Day, soon.Hour, soon.Minute, soon.Second, DateTimeKind.Utc);
        var kept = await OkAsync(await aod.PutAsJsonAsync($"{ShiftEndpoints.Pattern}/{shift}", same with { ToUtc = cut }, token), token);
        Assert.Equal((EarlierVid, stored.FromUtc, cut), (kept.GetProperty("controllerVid").GetInt32(), kept.GetProperty("fromUtc").GetDateTime(), kept.GetProperty("toUtc").GetDateTime()));
        await CreatedAsync(await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, new EventAtcShiftWriteDto(position, TraineeVid, cut, stored.ToUtc, default), token), token);

        // Proposed, then moved a month later: the close moves with the start, and the availabilities stay closed all the same.
        var (starts, _) = Window(days: 2);
        var id = await ProposableAsync("start-moved", starts, token);
        await RunJobAsync(token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(row => row.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.StartsAtUtc, starts.AddDays(30)).SetProperty(row => row.EndsAtUtc, starts.AddDays(30).AddHours(6)), token);
        }

        using var earlier = await SignedInAsync(EarlierVid, token);
        var mine = $"{AvailabilityEndpoints.MinePattern}/{id}";
        var request = new AvailabilityRequest([new AtcWindowDto(starts.AddDays(30), starts.AddDays(30).AddHours(2))], Note: null);
        Assert.Equal([AtcRules.ClosedKey], (await RefusedAsync(await earlier.PutAsJsonAsync(mine, request, token), token))["id"]);
        Assert.Equal([AtcRules.ClosedKey], (await RefusedAsync(await earlier.DeleteAsync(mine, token), token))["id"]);

        // And nothing proposes it again.
        var proposedAt = (await EventAsync(id, token)).RosterProposedAt;
        await RunJobAsync(token);
        Assert.Equal(proposedAt, (await EventAsync(id, token)).RosterProposedAt);
    }

    /// <summary>
    /// One event whose proposal fails — here the archive breaks on one of its controllers — is logged, named in the run's row and left for the
    /// next run; the events after it are proposed (the review of #262, point 2).
    /// </summary>
    [Fact]
    public async Task AnEventWhoseProposalFailsIsLeftForTheNextRunAndTheOthersAreProposed()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, _) = Window(days: 2);
        var broken = await ProposableAsync("broken", starts.AddMinutes(-30), token);
        await AvailabilityRowAsync(broken, BrokenVid, starts, starts.AddHours(6), DateTime.UtcNow.AddDays(-3), token);
        var fine = await ProposableAsync("broken-not", starts, token);

        try
        {
            await RunJobAsync(token);

            Assert.Null((await EventAsync(broken, token)).RosterProposedAt);
            Assert.Empty(await ShiftsAsync(broken, token));
            Assert.NotNull((await EventAsync(fine, token)).RosterProposedAt);

            await using var scope = _factory.Services.CreateAsyncScope();
            var run = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
                .Where(entry => entry.Job == EventRosterJob.JobName)
                .OrderByDescending(entry => entry.Id)
                .FirstAsync(token);
            Assert.Equal("succeeded", run.Status);
            Assert.NotNull(run.FinishedAt);
            Assert.Contains(broken.ToString(System.Globalization.CultureInfo.InvariantCulture), run.Message, StringComparison.Ordinal);
        }
        finally
        {
            // Not due any more: the other classes' runs do not meet it.
            await using var scope = _factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(row => row.Id == broken)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.CancelledAt, DateTime.UtcNow), CancellationToken.None);
        }
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
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("The request never waited for the transaction the test holds.");
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>A day so many days away, from 17:00 to 23:00 UTC.</summary>
    private static (DateTime Starts, DateTime Ends) Window(int days)
    {
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days).AddHours(17), DateTimeKind.Utc);
        return (starts, starts.AddHours(6));
    }

    private static EventAtcShiftWriteDto Shift(long positionId, int vid, DateTime from, int hours) =>
        new(positionId, vid, from, from.AddHours(hours), default);

    private async Task<int> RunJobAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventRosterJob>().RunAsync(cancellationToken);
    }

    private static async Task<List<JsonElement>> RosterAsync(HttpClient reader, long eventId, CancellationToken cancellationToken) =>
        [
            .. (await OkAsync(await reader.GetAsync(ShiftEndpoints.RosterPattern.Replace("{id:long}", eventId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), cancellationToken), cancellationToken))
                .GetProperty("items").EnumerateArray(),
        ];

    private static (string Key, int Count, string Outcome)[] ShiftLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{EventsModule.ModuleKey}:erasure.shifts", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
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

    /// <summary>
    /// An event of the class with a roster starting at <paramref name="starts"/>, for six hours: the tower open three hours, the approach two,
    /// and the two controllers of the class available the whole evening — the one without experience first.
    /// </summary>
    private async Task<long> ProposableAsync(string name, DateTime starts, CancellationToken cancellationToken, PublishStatus status = PublishStatus.Published)
    {
        var id = await EventRowAsync(name, starts, starts.AddHours(6), status, cancellationToken);
        await PositionRowAsync(id, Tower, starts, starts.AddHours(3), cancellationToken);
        await PositionRowAsync(id, Approach, starts, starts.AddHours(2), cancellationToken);
        await AvailabilityRowAsync(id, EarlierVid, starts, starts.AddHours(6), DateTime.UtcNow.AddDays(-5), cancellationToken);
        await AvailabilityRowAsync(id, ExperiencedVid, starts, starts.AddHours(6), DateTime.UtcNow.AddDays(-4), cancellationToken);
        return id;
    }

    private async Task<long> EventRowAsync(string name, DateTime starts, DateTime ends, PublishStatus status, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            HasRoster = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = ends,
            Status = status,
            PublishedAt = status == PublishStatus.Published ? DateTime.UtcNow.AddDays(-20) : null,
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task<long> PositionRowAsync(long eventId, string callsign, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var position = new EventAtcPosition
        {
            EventId = eventId,
            Callsign = callsign,
            Fir = callsign == Tower ? Fir : OtherFir,
            FromUtc = from,
            ToUtc = to,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcPositions.Add(position);
        await database.SaveChangesAsync(cancellationToken);
        return position.Id;
    }

    private async Task AvailabilityRowAsync(long eventId, int vid, DateTime from, DateTime to, DateTime given, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var availability = new EventAtcAvailability
        {
            EventId = eventId,
            ControllerVid = vid,
            CreatedAt = given,
            UpdatedAt = given,
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

    /// <summary>A shift written by the installation, with a note of the staff on how it went and a why, or none.</summary>
    private async Task<long> ShiftRowAsync(long eventId, long positionId, string fir, int vid, DateTime from, DateTime to, string? note, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var shift = new EventAtcShift
        {
            EventId = eventId,
            PositionId = positionId,
            ControllerVid = vid,
            Fir = fir,
            FromUtc = from,
            ToUtc = to,
            Origin = ShiftOrigin.Proposed,
            Reason = new ShiftReason(1, ShiftCriterion.Only, Preferred: true, 2, 2, 0, 0),
            AttendanceNote = note,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcShifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken);
        return shift.Id;
    }

    private async Task<Event> EventAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<List<EventAtcShift>> ShiftsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcShifts.IgnoreQueryFilters().AsNoTracking()
            .Where(shift => shift.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<EventAtcPosition>> PositionsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcPositions.IgnoreQueryFilters().AsNoTracking()
            .Where(position => position.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<string, long>> PositionIdsAsync(long eventId, CancellationToken cancellationToken) =>
        (await PositionsAsync(eventId, cancellationToken)).ToDictionary(position => position.Callsign, position => position.Id);

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

    private async Task SeedPersonAsync(int vid, string? position, int? rating, bool superadmin, CancellationToken cancellationToken)
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
        user.LastName = "Roster";
        user.Email = null;
        user.Locale = "en";
        user.IsStaff = position is not null;
        user.IsSuperadmin = superadmin;
        user.RatingAtc = rating;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.UserStaffPositions.Where(row => row.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        if (position is not null)
        {
            var parsed = StaffRoleMap.Parse(position, Division().Code, TwoFirs.Ids);
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

    /// <summary>The airspace of this host: two FIRs of the test's own, and no airport.</summary>
    private sealed class TwoFirs : IFirDirectory
    {
        public static readonly IReadOnlySet<string> Ids = new HashSet<string>([Fir, OtherFir], StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlySet<string>> GetFirIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Ids);

        public Task<IvaoAirspace> GetAirspaceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new IvaoAirspace(Ids, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        public void Invalidate()
        {
        }
    }

    /// <summary>The positions of this host's division: a tower in one FIR and an approach in the other, with no FRA.</summary>
    private sealed class TwoPositions : IAtcPositionDirectory
    {
        private static readonly AtcPositionDto[] All =
        [
            new(Tower, "TWR", "Test Tower", AirportIcao: null, Fir),
            new(Approach, "APP", "Test Approach", AirportIcao: null, OtherFir),
        ];

        public Task<IReadOnlyList<AtcPositionDto>> ForRatingAsync(Rating rating, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AtcPositionDto>>([]);

        public Task<IReadOnlyList<AtcPositionDto>> OfDivisionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AtcPositionDto>>(All);

        public Task<IReadOnlyDictionary<string, AtcPositionDto>> FindAsync(
            IReadOnlyCollection<string> callsigns,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AtcPositionDto>>(
                All.Where(position => callsigns.Contains(position.Callsign, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(position => position.Callsign, StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlyDictionary<string, AtcPositionMinimum>> MinimaAsync(
            IReadOnlyCollection<string> callsigns,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AtcPositionMinimum>>(new Dictionary<string, AtcPositionMinimum>());
    }

    /// <summary>
    /// An archive of sessions that knows three connections of the experienced controller on the tower, nothing of anybody else, and breaks on
    /// <see cref="BrokenVid"/> — a failure of one event's proposal that the job must survive.
    /// </summary>
    private sealed class ArchiveOfTheClass : IAtcActivitySource
    {
        public Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<AtcActivity?>(null);

        public Task<AtcActivity?> SessionsOfAsync(int vid, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        {
            if (vid == BrokenVid)
            {
                throw new InvalidOperationException("evt-test-e11b the archive breaks on this controller");
            }

            IReadOnlyList<AtcPresence> online = vid == ExperiencedVid
                ?
                [
                    .. Enumerable.Range(1, 3).Select(months => new AtcPresence(Tower, Frequency: null, toUtc.AddMonths(-months), toUtc.AddMonths(-months).AddHours(2)) { Vid = vid }),
                ]
                : [];

            return Task.FromResult<AtcActivity?>(new AtcActivity(online, fromUtc, fromUtc, []));
        }
    }
}
