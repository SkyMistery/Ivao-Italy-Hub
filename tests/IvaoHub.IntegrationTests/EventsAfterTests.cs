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
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The checks after an event and its statistics (M4, E13a; design §1.10, §4.5, §5.1, §10.2; note 2026-10-16-le-verifiche-dopo-l-evento),
/// through the real host with the division file of this repository, and the evening of Rome Fiumicino the fixtures recorded
/// (<c>tests/fixtures/ivao/tracker-airport-LIRF.json</c>, 1 January 2001, 16:00–17:59 UTC): its pilots are 761020–761028, the tower 761024.
/// <list type="bullet">
/// <item>the «done when» of E13a: after an evening, the bookings flown and not flown, who flew without booking as a number, the shift
/// attended and the no-show proposed with who covered, and the statistics — the same when recalculated;</item>
/// <item>a batch stops at its budget and the next run goes on from the rows;</item>
/// <item>a source that does not answer leaves the rows to check, and after thirty days the event is closed with them as they are;</item>
/// <item>no VID of whoever flew without booking is kept anywhere;</item>
/// <item>«Recalculate» is for whoever writes the event, after its end;</item>
/// <item>an event in person is counted from its rows while any is there, and keeps what the retention wrote once they are gone.</item>
/// </list>
/// <para>The job runs as of a day of 2001 (<see cref="RunAtAsync"/>): an event of the fixtures is over long since, and every event of the
/// other classes is still to come then. ⚠️ The people are 761180–761183: the coordinator of the events, the controller who missed a shift, the
/// pilot who did not fly, and a member who writes nothing.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsAfterTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int CoordinatorVid = 761180;
    private const int AbsentVid = 761181;
    private const int GroundedVid = 761182;
    private const int MemberVid = 761183;

    private static readonly int[] People = [CoordinatorVid, AbsentVid, GroundedVid, MemberVid];

    /// <summary>The evening's pilots who booked nothing of the event: never kept.</summary>
    private static readonly int[] Unbooked = [761021, 761022, 761025, 761027, 761028];

    private const int TowerVid = 761024;
    private const string Tower = "LIRF_TWR";

    private const string SlugStem = "evt-test-e13a";

    private static readonly DateTime Start = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    /// <summary>When the job is first due on the evening: six hours after its end.</summary>
    private static readonly DateTime Due = Start.AddHours(8);

    private static readonly string[] Locales = ["it", "en"];

    private readonly Sources _sources = new();
    private HubWebApplicationFactory _hub = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private string? _settingsBefore;
    private DateTime _since;

    public async ValueTask InitializeAsync()
    {
        _hub = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _hub.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(_sources);
            services.AddScoped<INetworkSessionSource>(provider => new EveningTracker(provider.GetRequiredService<HubPaths>(), _sources));
            services.AddScoped<IAtcActivitySource>(provider => _sources.NoArchive
                ? new UnavailableAtcActivitySource()
                : new EveningArchive(provider.GetRequiredService<HubPaths>(), _sources));
        }));
        var token = TestContext.Current.CancellationToken;
        _since = DateTime.UtcNow.AddSeconds(-1);

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);

        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", token);
        await SeedPersonAsync(AbsentVid, position: null, token);
        await SeedPersonAsync(GroundedVid, position: null, token);
        await SeedPersonAsync(MemberVid, position: null, token);

        // A quarter of an hour on the position is enough: the tower of the evening holds 18 minutes of the second hour.
        _settingsBefore = await WriteSettingsAsync("""{"attendanceMinimumMinutes":15}""", token);
    }

    public async ValueTask DisposeAsync()
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of E13a: one run after the evening checks every booking — the two flown, the private arrival found by its pilot's
    /// own flight, the one the pilot never flew, and the second booking of a route its pilot flew once (one session, never two bookings) —, the
    /// two shifts with the archive — the tower attended its hour, the absent controller is a no-show proposed with the tower as who covered —,
    /// counts five pilots who flew without booking, writes the statistics and closes the event without moving its row version. The lists say it
    /// with data; recalculated, the numbers are the same; run again, nothing.
    /// </summary>
    [Fact]
    public async Task TheChecksOfAnEveningFindTheFiveListsAndTheNumbers()
    {
        var token = TestContext.Current.CancellationToken;
        _sources.Archive = true;
        var evening = await EveningAsync("evening", token);
        var versionBefore = (await EventAsync(evening.Id, token)).RowVersion;

        var outcome = await RunAtAsync(Due, token);
        Assert.Equal(new EventsAfterJob.Outcome(1, 5, 2, 1), outcome);

        var row = await EventAsync(evening.Id, token);
        Assert.Equal(Due, row.AfterDoneAt);
        Assert.Equal(versionBefore, row.RowVersion);

        // The bookings: flown at the start of their sessions; the pilot who did not fly and the second booking of a route, checked and not flown.
        var bookings = await BookingsAsync(evening.Id, token);
        Assert.All(bookings.Values, booking => Assert.Equal(Due, booking.FlownCheckedAt));
        Assert.Equal((At(17, 27, 44), 1000010L), (bookings[evening.Flown].FlownAt, bookings[evening.Flown].FlownSessionId));
        Assert.Equal((At(16, 46, 10), 1000007L), (bookings[evening.Arrival].FlownAt, bookings[evening.Arrival].FlownSessionId));
        Assert.Equal((At(16, 26, 16), 1000004L), (bookings[evening.Private].FlownAt, bookings[evening.Private].FlownSessionId));
        Assert.Null(bookings[evening.NotFlown].FlownAt);
        Assert.Null(bookings[evening.SecondOfARoute].FlownAt);

        // The shifts: attended; a no-show proposed, with who covered; nothing the staff decide.
        var shifts = await ShiftsAsync(evening.Id, token);
        Assert.Equal((ShiftAttendance.Attended, (int?)null), (shifts[evening.Attended].Attendance, shifts[evening.Attended].CoveredByVid));
        Assert.Equal((ShiftAttendance.NoShowProposed, (int?)TowerVid), (shifts[evening.Missed].Attendance, shifts[evening.Missed].CoveredByVid));
        Assert.All(shifts.Values, shift => Assert.Null(shift.AttendanceBy));

        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // The numbers, as the tab reads them.
        var stats = Assert.Single(await PageAsync(coordinator, $"{StatsEndpoints.Pattern}?filter[eventId]={evening.Id}", token));
        Assert.Equal(Numbers(4, 1, 5, 3, 2, 5, 0, 2, 1, 1, 0), Numbers(stats));
        Assert.Equal(Due, stats.GetProperty("checksDoneAt").GetDateTime());
        Assert.Equal((1, 1), (stats.GetProperty("flownUnbookedAirports").GetInt32(), stats.GetProperty("airports").GetInt32()));

        // The lists, with data: a booking's check, and a shift's outcome with who covered.
        var listed = await PageAsync(coordinator, $"/api/events/bookings?filter[eventId]={evening.Id}&pageSize=100", token);
        var flown = Assert.Single(listed, booking => booking.GetProperty("id").GetInt64() == evening.Flown);
        Assert.Equal(At(17, 27, 44), flown.GetProperty("flownAt").GetDateTime());
        var notFlown = Assert.Single(listed, booking => booking.GetProperty("id").GetInt64() == evening.NotFlown);
        Assert.Equal(JsonValueKind.Null, notFlown.GetProperty("flownAt").ValueKind);
        Assert.Equal(Due, notFlown.GetProperty("flownCheckedAt").GetDateTime());

        var roster = await PageAsync(coordinator, $"{ShiftEndpoints.Pattern}?filter[eventId]={evening.Id}", token);
        var missed = Assert.Single(roster, shift => shift.GetProperty("id").GetInt64() == evening.Missed);
        Assert.Equal(Tower, missed.GetProperty("callsign").GetString());
        Assert.Equal(nameof(ShiftAttendance.NoShowProposed), missed.GetProperty("attendance").GetString());
        Assert.Equal(AbsentVid, missed.GetProperty("controller").GetProperty("vid").GetInt32());
        Assert.Equal("Test After", missed.GetProperty("controller").GetProperty("name").GetString());
        Assert.Equal(TowerVid, missed.GetProperty("coveredBy").GetProperty("vid").GetInt32());

        // Recalculated: the same numbers, who flew without booking kept.
        var recalculated = await OkAsync(await coordinator.PostAsync(RecalculateUri(evening.Id), content: null, token), token);
        Assert.Equal(Numbers(stats), Numbers(recalculated));

        // Run again: nothing is due.
        Assert.Equal(new EventsAfterJob.Outcome(0, 0, 0, 0), await RunAtAsync(Due.AddHours(1), token));
    }

    /// <summary>
    /// Nobody who flew without booking is kept (reading 3, design §5.1: «nessun VID si salva»): after the run, none of the evening's pilots who
    /// booked nothing is in a row of the events, in the audit written since the test started, or in the job's row.
    /// </summary>
    [Fact]
    public async Task NoVidOfWhoFlewWithoutBookingIsKept()
    {
        var token = TestContext.Current.CancellationToken;
        var evening = await EveningAsync("no-vid", token);

        await RunAtAsync(Due, token);
        Assert.NotNull((await EventAsync(evening.Id, token)).AfterDoneAt);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        Assert.False(await database.Bookings.IgnoreQueryFilters().AnyAsync(booking => Unbooked.Contains(booking.BookerVid), token));
        Assert.False(await database.AtcShifts.IgnoreQueryFilters().AnyAsync(
            shift => Unbooked.Contains(shift.ControllerVid) || (shift.CoveredByVid != null && Unbooked.Contains(shift.CoveredByVid.Value)), token));

        var audit = await hub.AuditLog.AsNoTracking().Where(entry => entry.At >= _since).ToListAsync(token);
        Assert.NotEmpty(audit);
        var logged = await hub.JobsLog.AsNoTracking()
            .Where(entry => entry.Job == EventsAfterJob.JobName && entry.StartedAt == Due)
            .Select(entry => entry.Message)
            .ToListAsync(token);
        Assert.NotEmpty(logged);

        foreach (var vid in Unbooked.Select(vid => vid.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        {
            Assert.DoesNotContain(audit, entry => (entry.BeforeJson ?? string.Empty).Contains(vid, StringComparison.Ordinal)
                || (entry.AfterJson ?? string.Empty).Contains(vid, StringComparison.Ordinal)
                || entry.EntityId == vid);
            Assert.DoesNotContain(logged, message => (message ?? string.Empty).Contains(vid, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// A batch (reading 6): sixty-five bookings of one pilot on as many routes cost a unit each, so the first run checks sixty and leaves the
    /// event open; the next run checks the five left, counts who flew without booking at the airport and closes it.
    /// </summary>
    [Fact]
    public async Task ABatchStopsAtItsBudgetAndTheNextRunGoesOn()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("batch", token);
        for (var leg = 0; leg < 65; leg++)
        {
            var slot = await PublicSlotAsync(id, $"EVB{leg:000}", "LIRF", Start.AddMinutes(leg), $"Z{leg:000}", token);
            await BookingRowAsync(id, slot, GroundedVid, token);
        }

        var first = await RunAtAsync(Due, token);
        Assert.Equal(new EventsAfterJob.Outcome(1, EventsAfterJob.Budget, 0, 0), first);
        Assert.Equal(EventsAfterJob.Budget, (await BookingsAsync(id, token)).Values.Count(booking => booking.FlownCheckedAt != null));
        Assert.Null((await EventAsync(id, token)).AfterDoneAt);
        Assert.Contains($"{EventsAfterJob.Budget} of {EventsAfterJob.Budget} unit(s) spent", await LastMessageAsync(token), StringComparison.Ordinal);

        var second = await RunAtAsync(Due.AddHours(1), token);
        Assert.Equal(new EventsAfterJob.Outcome(1, 5, 0, 1), second);
        Assert.All((await BookingsAsync(id, token)).Values, booking => Assert.NotNull(booking.FlownCheckedAt));
        Assert.NotNull((await EventAsync(id, token)).AfterDoneAt);
    }

    /// <summary>
    /// A source that does not answer (reading 7): the tracker down, the bookings and the shifts stay to check and the event open, and the job's
    /// row says so; the tracker back, the next run checks them — the shift with the tracker by VID, since the division has no archive — and
    /// closes the event.
    /// </summary>
    [Fact]
    public async Task ASourceThatDoesNotAnswerLeavesTheRowsToCheck()
    {
        var token = TestContext.Current.CancellationToken;
        var evening = await EveningAsync("down", token);
        _sources.TrackerAnswers = false;

        Assert.Equal(new EventsAfterJob.Outcome(1, 0, 0, 0), await RunAtAsync(Due, token));
        Assert.All((await BookingsAsync(evening.Id, token)).Values, booking => Assert.Null(booking.FlownCheckedAt));
        Assert.All((await ShiftsAsync(evening.Id, token)).Values, shift => Assert.Equal(ShiftAttendance.Unknown, shift.Attendance));
        Assert.Null((await EventAsync(evening.Id, token)).AfterDoneAt);
        Assert.Contains("the tracker did not answer", await LastMessageAsync(token), StringComparison.Ordinal);

        _sources.TrackerAnswers = true;
        Assert.Equal(new EventsAfterJob.Outcome(1, 5, 2, 1), await RunAtAsync(Due.AddHours(1), token));

        // Without the archive the tracker says the tower was there, and nobody can say who covered the absent controller.
        var shifts = await ShiftsAsync(evening.Id, token);
        Assert.Equal(ShiftAttendance.Attended, shifts[evening.Attended].Attendance);
        Assert.Equal((ShiftAttendance.NoShowProposed, (int?)null), (shifts[evening.Missed].Attendance, shifts[evening.Missed].CoveredByVid));
        Assert.NotNull((await EventAsync(evening.Id, token)).AfterDoneAt);
    }

    /// <summary>
    /// Thirty days after the end (reading 7) the sources are given up: the event is closed with its rows as they were — never marked — and the
    /// statistics count the bookings, none of them flown or not flown, and say that who flew without booking was not counted — empty, never 0
    /// (Carmine's condition on reading 3, #272).
    /// </summary>
    [Fact]
    public async Task ThirtyDaysAfterTheEndTheEventIsClosedWithItsRowsToCheck()
    {
        var token = TestContext.Current.CancellationToken;
        var evening = await EveningAsync("given-up", token);
        _sources.TrackerAnswers = false;

        Assert.Equal(new EventsAfterJob.Outcome(1, 0, 0, 1), await RunAtAsync(Start.AddDays(31), token));

        Assert.NotNull((await EventAsync(evening.Id, token)).AfterDoneAt);
        Assert.All((await BookingsAsync(evening.Id, token)).Values, booking => Assert.Null(booking.FlownCheckedAt));
        Assert.All((await ShiftsAsync(evening.Id, token)).Values, shift => Assert.Equal(ShiftAttendance.Unknown, shift.Attendance));

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var stats = Assert.Single(await PageAsync(coordinator, $"{StatsEndpoints.Pattern}?filter[eventId]={evening.Id}", token));
        Assert.Equal(Numbers(4, 1, 5, 0, 0, null, 0, 2, 0, 0, 0), Numbers(stats));
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("flownUnbookedAirports").ValueKind);
    }

    /// <summary>«Recalculate» is a verb of whoever writes the event, for an event over: a member is refused, and an event still to end too.</summary>
    [Fact]
    public async Task RecalculateIsForWhoeverWritesTheEventAfterItsEnd()
    {
        var token = TestContext.Current.CancellationToken;
        var over = await EventRowAsync("recalculate", token);
        var toCome = await EventRowAsync("recalculate-to-come", token, starts: DateTime.UtcNow.AddDays(3));

        using var member = await SignedInAsync(MemberVid, token);
        using (var refused = await member.PostAsync(RecalculateUri(over), content: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        Assert.Equal(["events:errors.statsBeforeEnd"], (await RefusedAsync(await coordinator.PostAsync(RecalculateUri(toCome), content: null, token), token))["id"]);

        var written = await OkAsync(await coordinator.PostAsync(RecalculateUri(over), content: null, token), token);
        Assert.Equal(over, written.GetProperty("eventId").GetInt64());
        Assert.Equal(JsonValueKind.Null, written.GetProperty("flownUnbooked").ValueKind);

        // Recalculated before any check: who flew without booking is not counted yet, and the page says so.
        Assert.Equal(JsonValueKind.Null, written.GetProperty("checksDoneAt").ValueKind);
    }

    /// <summary>
    /// An event in person (reading 8, E17's review): its registrations are counted from the rows while any is there, even past its retention
    /// when the run of the month has not come yet; once they are gone, the number <c>events-retention</c> kept stays.
    /// </summary>
    [Fact]
    public async Task TheNumbersOfAnEventInPersonAreItsRowsWhileAnyIsThere()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("in-person", token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var row = await database.Events.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == id, token);
            row.InPerson = true;
            database.Registrations.AddRange(
                [.. new[] { AbsentVid, GroundedVid }.Select(vid => new EventRegistration
                {
                    EventId = id,
                    Vid = vid,
                    CreatedAt = Start.AddDays(-5),
                    UpdatedAt = Start.AddDays(-5),
                    OwnerDepartment = Department.ED,
                    OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
                })]);
            await database.SaveChangesAsync(token);
        }

        // Past its retention by years, and the rows still there: they are counted.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var counted = await OkAsync(await coordinator.PostAsync(RecalculateUri(id), content: null, token), token);
        Assert.Equal(2, counted.GetProperty("registrations").GetInt32());

        // The retention takes the rows away: what the statistics hold stays.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Registrations.IgnoreQueryFilters()
                .Where(registration => registration.EventId == id)
                .ExecuteDeleteAsync(token);
        }

        var kept = await OkAsync(await coordinator.PostAsync(RecalculateUri(id), content: null, token), token);
        Assert.Equal(2, kept.GetProperty("registrations").GetInt32());
    }

    /// <summary>
    /// Who flew without booking is counted a few airports per run (reading 6, Carmine's condition on #272): an evening at three airports
    /// counts two in the first run — 44 units, inside the budget — and leaves the event open, then the third, and closes it. Nobody booked:
    /// Fiumicino holds the evening's eight pilots; the other two airports none.
    /// </summary>
    [Fact]
    public async Task WhoFlewWithoutBookingIsCountedAFewAirportsPerRun()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("airports", token, airports: ["LIRF", "LIMC", "LIBD"]);

        Assert.Equal(new EventsAfterJob.Outcome(1, 0, 0, 0), await RunAtAsync(Due, token));
        Assert.Contains($"{2 * 2 * EventsAfterJob.ByAirportCost} of {EventsAfterJob.Budget} unit(s) spent", await LastMessageAsync(token), StringComparison.Ordinal);
        Assert.Null((await EventAsync(id, token)).AfterDoneAt);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var first = Assert.Single(await PageAsync(coordinator, $"{StatsEndpoints.Pattern}?filter[eventId]={id}", token));
        Assert.Equal((8, 2, 3), (first.GetProperty("flownUnbooked").GetInt32(), first.GetProperty("flownUnbookedAirports").GetInt32(), first.GetProperty("airports").GetInt32()));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("checksDoneAt").ValueKind);

        Assert.Equal(new EventsAfterJob.Outcome(1, 0, 0, 1), await RunAtAsync(Due.AddHours(1), token));
        Assert.Contains($"{2 * EventsAfterJob.ByAirportCost} of {EventsAfterJob.Budget} unit(s) spent", await LastMessageAsync(token), StringComparison.Ordinal);

        var closed = Assert.Single(await PageAsync(coordinator, $"{StatsEndpoints.Pattern}?filter[eventId]={id}", token));
        Assert.Equal((8, 3), (closed.GetProperty("flownUnbooked").GetInt32(), closed.GetProperty("flownUnbookedAirports").GetInt32()));
        Assert.Equal(Due.AddHours(1), closed.GetProperty("checksDoneAt").GetDateTime());
    }

    /// <summary>
    /// A division without an archive (`atcData: none`, the review of #278, point 4): the archive is not asked, costs nothing, and the run's row
    /// does not say it «did not answer» — the shifts are read on the tracker by VID.
    /// </summary>
    [Fact]
    public async Task ADivisionWithoutAnArchiveAsksTheTrackerAndSaysNothingOfIt()
    {
        var token = TestContext.Current.CancellationToken;
        _sources.NoArchive = true;
        var evening = await EveningAsync("no-archive", token);

        Assert.Equal(new EventsAfterJob.Outcome(1, 5, 2, 1), await RunAtAsync(Due, token));

        // Four questions for the bookings, one on the tracker for each shift, two for the airport: none for the archive.
        var message = await LastMessageAsync(token);
        Assert.Contains($"{4 + 2 + 2 * EventsAfterJob.ByAirportCost} of {EventsAfterJob.Budget} unit(s) spent", message, StringComparison.Ordinal);
        Assert.DoesNotContain("archive", message, StringComparison.Ordinal);
        Assert.Equal(ShiftAttendance.Attended, (await ShiftsAsync(evening.Id, token))[evening.Attended].Attendance);
    }

    /// <summary>
    /// «Recalculate» in the instant of the job's close (the review of #278, point 3): the first row of the statistics is written under the lock
    /// of the event's row, so the request waits for the transaction that holds it, then reads the row it committed and updates it — one row,
    /// no duplicate key, no 500. The test holds the lock as the job would, and inserts the row before letting go.
    /// </summary>
    [Fact]
    public async Task RecalculateWaitsForTheCloseAndWritesTheSameRow()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await EventRowAsync("race", token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await using (var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, token))
        {
            await database.Database.SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {id} FOR UPDATE").ToListAsync(token);

            var request = coordinator.PostAsync(RecalculateUri(id), content: null, token);
            await WaitForALockWaitAsync(token);

            database.Stats.Add(new EventStats
            {
                EventId = id,
                ComputedAt = Due,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });
            await database.SaveChangesAsync(token);
            await transaction.CommitAsync(token);

            var written = await OkAsync(await request, token);
            Assert.Equal(id, written.GetProperty("eventId").GetInt64());
        }

        Assert.Equal(1, await database.Stats.IgnoreQueryFilters().CountAsync(stats => stats.EventId == id, token));
    }

    // ---- the evening ----------------------------------------------------------------------------------------------------------

    /// <summary>The rows of the evening of the fixtures, by what each is for.</summary>
    private sealed record Evening(long Id, long Flown, long Arrival, long Private, long NotFlown, long SecondOfARoute, long Attended, long Missed);

    /// <summary>
    /// An event of the evening at Rome Fiumicino, 16:00–18:00 UTC, with its airport, four public slots and a private arrival booked, the tower
    /// open, and two shifts on it:
    /// <list type="bullet">
    /// <item>761020 books Fiumicino–Lyon at 17:20 and flies it at 17:27; they book the same route at 18:30 too, and fly it once;</item>
    /// <item>761023 books Parma–Fiumicino at 16:40, the first revision of the flight they started at 16:46;</item>
    /// <item>761026 books a private arrival at 17:30 from Parma at 16:20, and starts that flight at 16:26;</item>
    /// <item>761182 books Fiumicino–Naples at 17:00 and flies nothing;</item>
    /// <item>761181 holds the tower 16:00–17:00 and is not there — 761024 is, from 16:37 —; 761024 holds it 17:00–18:00 and stays until 17:18.</item>
    /// </list>
    /// </summary>
    private async Task<Evening> EveningAsync(string name, CancellationToken cancellationToken)
    {
        var id = await EventRowAsync(name, cancellationToken);

        var lyon = await PublicSlotAsync(id, "EVA001", "LIRF", At(17, 20), "LFKJ", cancellationToken);
        var parma = await PublicSlotAsync(id, "EVA002", "LIPZ", At(16, 40), "LIRF", cancellationToken);
        var naples = await PublicSlotAsync(id, "EVA003", "LIRF", At(17, 0), "LIRN", cancellationToken);
        var lyonAgain = await PublicSlotAsync(id, "EVA004", "LIRF", At(18, 30), "LFKJ", cancellationToken);
        var arrival = await PrivateArrivalAsync(id, At(17, 30), cancellationToken);

        var flown = await BookingRowAsync(id, lyon, 761020, cancellationToken);
        var fromParma = await BookingRowAsync(id, parma, 761023, cancellationToken);
        var notFlown = await BookingRowAsync(id, naples, GroundedVid, cancellationToken);
        var second = await BookingRowAsync(id, lyonAgain, 761020, cancellationToken);
        var own = await BookingRowAsync(id, arrival, 761026, cancellationToken, other: ("LIPZ", At(16, 20), "TST007"));

        var position = await PositionRowAsync(id, cancellationToken);
        var missed = await ShiftRowAsync(id, position, AbsentVid, Start, Start.AddHours(1), cancellationToken);
        var attended = await ShiftRowAsync(id, position, TowerVid, Start.AddHours(1), Start.AddHours(2), cancellationToken);

        return new Evening(id, flown, fromParma, own, notFlown, second, attended, missed);
    }

    private static DateTime At(int hour, int minute, int second = 0) => Start.Date.AddHours(hour).AddMinutes(minute).AddSeconds(second);

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static string RecalculateUri(long id) => StatsEndpoints.RecalculatePattern.Replace("{id:long}", id.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>The numbers of the statistics in their order: slots public and private, booked, flown, not flown, flown without booking (null: not counted), availabilities, shifts, attended, no-shows, excused.</summary>
    private static int?[] Numbers(int publicSlots, int privateSlots, int booked, int flown, int unflown, int? flownUnbooked, int availabilities, int shifts, int attended, int noShows, int excused) =>
        [publicSlots, privateSlots, booked, flown, unflown, flownUnbooked, availabilities, shifts, attended, noShows, excused];

    private static int?[] Numbers(JsonElement stats) =>
    [
        .. new[]
        {
            "publicSlots", "privateSlots", "booked", "bookedFlown", "bookedUnflown", "flownUnbooked", "atcAvailabilities", "atcShifts",
            "atcAttended", "atcNoShows", "atcExcused",
        }.Select(name => stats.GetProperty(name).ValueKind == JsonValueKind.Null ? (int?)null : stats.GetProperty(name).GetInt32()),
    ];

    /// <summary>A run of <c>events-after</c> as of <paramref name="now"/>.</summary>
    private async Task<EventsAfterJob.Outcome> RunAtAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var job = ActivatorUtilities.CreateInstance<EventsAfterJob>(scope.ServiceProvider, new FixedClock(now));
        return await job.RunAsync(cancellationToken);
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

        Assert.Fail("No request waited for the lock of the event's row.");
    }

    private async Task<string> LastMessageAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .Where(entry => entry.Job == EventsAfterJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .Select(entry => entry.Message ?? string.Empty)
            .FirstAsync(cancellationToken);
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

    private async Task<long> EventRowAsync(string name, CancellationToken cancellationToken, DateTime? starts = null, string[]? airports = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var begins = starts ?? Start;

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            PublicSlots = true,
            PrivateSlots = true,
            HasRoster = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = begins,
            EndsAtUtc = begins.AddHours(2),
            Status = PublishStatus.Published,
            PublishedAt = begins.AddDays(-20),
            Visibility = Visibility.Public,
            RosterProposedAt = begins.AddDays(-3),
            RosterPublishedAt = begins.AddDays(-2),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        // Fiumicino, the evening's airport, first; and the others asked for, in their order.
        database.Airports.AddRange(
            [.. (airports ?? ["LIRF"]).Select((icao, index) => new EventAirport
            {
                EventId = row.Id,
                Icao = icao,
                Ordinal = index + 1,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            })]);
        await database.SaveChangesAsync(cancellationToken);

        return row.Id;
    }

    private async Task<long> PublicSlotAsync(long eventId, string callsign, string departure, DateTime offBlock, string arrival, CancellationToken cancellationToken)
    {
        var isArrival = arrival == "LIRF";
        return await SlotRowAsync(new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Public,
            EventAirportIcao = "LIRF",
            IsArrival = isArrival,
            Callsign = callsign,
            AircraftTypes = ["A320"],
            DepartureIcao = departure,
            ArrivalIcao = arrival,
            OffBlockUtc = offBlock,
            OnBlockUtc = offBlock.AddHours(1),
        }, cancellationToken);
    }

    private Task<long> PrivateArrivalAsync(long eventId, DateTime onBlock, CancellationToken cancellationToken) =>
        SlotRowAsync(new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Private,
            EventAirportIcao = "LIRF",
            IsArrival = true,
            OnBlockUtc = onBlock,
            Generated = true,
        }, cancellationToken);

    private async Task<long> SlotRowAsync(EventSlot slot, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        slot.OwnerDepartment = Department.ED;
        slot.OwnerDepartmentMask = DepartmentMask.Of(Department.ED);
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    private async Task<long> BookingRowAsync(
        long eventId,
        long slotId,
        int vid,
        CancellationToken cancellationToken,
        (string Icao, DateTime Time, string Callsign)? other = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var booking = new EventBooking
        {
            EventId = eventId,
            SlotId = slotId,
            BookerVid = vid,
            AircraftIcao = "A320",
            Callsign = other?.Callsign,
            OtherIcao = other?.Icao,
            OtherTimeUtc = other?.Time,
            CreatedAt = Start.AddDays(-5),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Bookings.Add(booking);
        await database.SaveChangesAsync(cancellationToken);
        return booking.Id;
    }

    private async Task<long> PositionRowAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var position = new EventAtcPosition
        {
            EventId = eventId,
            Callsign = Tower,
            Fir = "LIRR",
            FromUtc = Start,
            ToUtc = Start.AddHours(2),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.AtcPositions.Add(position);
        await database.SaveChangesAsync(cancellationToken);
        return position.Id;
    }

    private async Task<long> ShiftRowAsync(long eventId, long positionId, int vid, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var shift = new EventAtcShift
        {
            EventId = eventId,
            PositionId = positionId,
            ControllerVid = vid,
            Fir = "LIRR",
            FromUtc = from,
            ToUtc = to,
            Origin = ShiftOrigin.Staff,
            NotifiedAt = Start.AddDays(-2),
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

    private async Task<Dictionary<long, EventBooking>> BookingsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(booking => booking.EventId == eventId)
            .ToDictionaryAsync(booking => booking.Id, cancellationToken);
    }

    private async Task<Dictionary<long, EventAtcShift>> ShiftsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcShifts.IgnoreQueryFilters().AsNoTracking()
            .Where(shift => shift.EventId == eventId)
            .ToDictionaryAsync(shift => shift.Id, cancellationToken);
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
        user.LastName = "After";
        user.Email = null;
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

    // ---- the sources of the evening --------------------------------------------------------------------------------------------

    /// <summary>What the doubles answer: the tracker up or down, and an archive of the sessions or none.</summary>
    private sealed class Sources
    {
        public bool TrackerAnswers { get; set; } = true;

        public bool Archive { get; set; }

        /// <summary>No archive at all, as the core registers it for <c>atcData: none</c>.</summary>
        public bool NoArchive { get; set; }
    }

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    /// <summary>The rows of the evening of Fiumicino, as recorded.</summary>
    private static JsonElement[] EveningRows(HubPaths paths)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(paths.Root, FixtureIvaoApiClient.Directory, "tracker-airport-LIRF.json")));
        return [.. document.RootElement.EnumerateArray().Select(row => row.Clone())];
    }

    /// <summary>
    /// The network's sessions answered from the evening of Fiumicino — by VID too, which the fixture client reads from a member's own file —,
    /// filtered by the tracker's own rule (<see cref="IvaoTrackerReader.Answers"/>) and given as the core's source gives them; null while it is
    /// down.
    /// </summary>
    private sealed class EveningTracker(HubPaths paths, Sources sources) : INetworkSessionSource
    {
        public Task<IReadOnlyList<NetworkSession>?> SessionsAsync(NetworkSessionQuery query, CancellationToken cancellationToken = default)
        {
            if (!sources.TrackerAnswers)
            {
                return Task.FromResult<IReadOnlyList<NetworkSession>?>(null);
            }

            var asked = new IvaoSessionQuery(
                query.Vid,
                query.FromUtc,
                query.ToUtc,
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

            return Task.FromResult<IReadOnlyList<NetworkSession>?>(
            [
                .. EveningRows(paths)
                    .Where(row => IvaoTrackerReader.Answers(row, asked))
                    .Select(IvaoTrackerReader.ReadSession)
                    .OfType<IvaoTrackerSessionDto>()
                    .OrderByDescending(session => session.StartedAt)
                    .Take(query.Limit)
                    .Select(session => new NetworkSession(session.Id, session.Vid, session.Callsign, session.StartedAt, session.EndedAt)),
            ]);
        }
    }

    /// <summary>
    /// An archive of the sessions holding the evening's tower, complete for the division since 2000; or none, as the division has today.
    /// </summary>
    private sealed class EveningArchive(HubPaths paths, Sources sources) : IAtcActivitySource
    {
        public Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Activity(fromUtc, toUtc, vid: null));

        public Task<AtcActivity?> SessionsOfAsync(int vid, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Activity(fromUtc, toUtc, vid));

        private AtcActivity? Activity(DateTime fromUtc, DateTime toUtc, int? vid)
        {
            if (!sources.Archive)
            {
                return null;
            }

            var since = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return new AtcActivity(
                [
                    .. EveningRows(paths)
                        .Select(IvaoTrackerReader.ReadSession)
                        .OfType<IvaoTrackerSessionDto>()
                        .Where(session => session.ConnectionType == IvaoConnectionType.Atc && (vid is null || session.Vid == vid))
                        .Select(session => new AtcPresence(session.Callsign, null, session.StartedAt, session.EndedAt) { Vid = session.Vid })
                        .Where(presence => presence.Overlaps(fromUtc, toUtc)),
                ],
                since,
                since,
                ["LI"]);
        }
    }
}
