using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The no-shows, the registers and the limits (M4, E13b; design §3.7, §4.5, §7.2, §7.3; note 2026-10-16-i-no-show-i-registri-e-i-limiti),
/// through the real host with the division file of this repository — its grants to the ATC operations and to the coordinator, nothing given
/// by hand:
/// <list type="bullet">
/// <item>nobody decides how their own shift went, the super administrator included; the staff confirm a proposed no-show and take it away
/// later with a note — it stays taken away, the register and the controller's own record say so, the audit keeps who —;</item>
/// <item>a pilot over the threshold of bookings not flown books no more than the limits per band and per event, the event's own when it has
/// them, and reads until when; a booking taken out of the record does not count, and nobody takes out their own;</item>
/// <item>the block of the staff's queue counts what waits for the reader, never their own.</item>
/// </list>
/// <para>⚠️ The people are 761200–761206 (E13b's range, 761200–761229): the ATC operations, a controller, the super administrator, the
/// coordinator of the events, a pilot with the limits, another pilot with one booking taken out, and a member.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsRegistersTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int AodVid = 761200;
    private const int ControllerVid = 761201;
    private const int SuperadminVid = 761202;
    private const int CoordinatorVid = 761203;
    private const int PilotVid = 761204;
    private const int OtherPilotVid = 761205;
    private const int MemberVid = 761206;

    private static readonly int[] People = [AodVid, ControllerVid, SuperadminVid, CoordinatorVid, PilotVid, OtherPilotVid, MemberVid];

    private const string Tower = "XXAA_TWR";
    private const string Fir = "XXAA";
    private const string Home = "XEM1";
    private const string Away = "XEM2";
    private const string Type = "XE3A";

    private const string SlugStem = "evt-test-e13b";

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private string? _settingsBefore;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);

        await SeedPersonAsync(AodVid, $"{Division().Code}-AOC", superadmin: false, token);
        await SeedPersonAsync(ControllerVid, position: null, superadmin: false, token);
        await SeedPersonAsync(SuperadminVid, position: null, superadmin: true, token);
        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", superadmin: false, token);
        await SeedPersonAsync(PilotVid, position: null, superadmin: false, token);
        await SeedPersonAsync(OtherPilotVid, position: null, superadmin: false, token);
        await SeedPersonAsync(MemberVid, position: null, superadmin: false, token);

        // The design's numbers, written out so that a class before this one leaves nothing of its own.
        _settingsBefore = await WriteSettingsAsync(
            """{"unflownThreshold":3,"restrictedWindowHours":2,"restrictedMaxPerWindow":1,"restrictedMaxPerEvent":2,"pilotRetentionMonths":24,"bookingGapMinutes":10,"noShowWeight":1}""",
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
            await database.Users.Where(user => People.Contains(user.Vid)).ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false));
        }

        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Nobody decides their own shift (§4.5, reading 1), the super administrator included — `DeniedToStakeholder` on <c>EventAtc.Edit</c>,
    /// no rule of the module —: the ATC operations and the super administrator are each refused on their own proposed no-show, and decide the
    /// other's. Before the end nothing is decided; the block of the queue counts for each only the other's.
    /// </summary>
    [Fact]
    public async Task NobodyDecidesTheirOwnShiftTheSuperAdministratorIncluded()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(-3));
        var id = await EventRowAsync("own", starts, starts.AddHours(4), token);
        var position = await PositionRowAsync(id, starts, starts.AddHours(4), token);
        var aodShift = await ShiftRowAsync(id, position, AodVid, starts, starts.AddHours(1), ShiftAttendance.NoShowProposed, token);
        var superShift = await ShiftRowAsync(id, position, SuperadminVid, starts.AddHours(1), starts.AddHours(2), ShiftAttendance.NoShowProposed, token);

        using var aod = await SignedInAsync(AodVid, token);
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        // The queue: each reads the other's no-show, never their own.
        Assert.Equal(1, Queue(await OkAsync(await aod.GetAsync(BlockUri(StaffQueueProvider.BlockType), token), token), id).GetProperty("noShows").GetInt32());
        Assert.Equal(1, Queue(await OkAsync(await superadmin.GetAsync(BlockUri(StaffQueueProvider.BlockType), token), token), id).GetProperty("noShows").GetInt32());

        using (var own = await DecideAsync(aod, aodShift, ShiftAttendance.Excused, $"{SlugStem} mine", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        using (var own = await DecideAsync(superadmin, superShift, ShiftAttendance.NoShow, note: null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        // The list says so too: neither may decide their own row.
        Assert.False(Row(await PageAsync(aod, ShiftsUri(id), token), aodShift).GetProperty("decidable").GetBoolean());
        Assert.True(Row(await PageAsync(aod, ShiftsUri(id), token), superShift).GetProperty("decidable").GetBoolean());

        await NoContentAsync(await DecideAsync(aod, superShift, ShiftAttendance.NoShow, note: null, token), token);
        await NoContentAsync(await DecideAsync(superadmin, aodShift, ShiftAttendance.Attended, note: null, token), token);

        var shifts = await ShiftsAsync(id, token);
        Assert.Equal((ShiftAttendance.NoShow, (int?)AodVid), (shifts[superShift].Attendance, shifts[superShift].AttendanceBy));
        Assert.Equal((ShiftAttendance.Attended, (int?)SuperadminVid), (shifts[aodShift].Attendance, shifts[aodShift].AttendanceBy));
        Assert.All(shifts.Values, shift => Assert.NotNull(shift.AttendanceAt));

        // An event still to end decides nothing: how its shifts went is not known yet.
        var later = Hour(DateTime.UtcNow.AddDays(3));
        var coming = await EventRowAsync("own-coming", later, later.AddHours(4), token);
        var comingShift = await ShiftRowAsync(coming, await PositionRowAsync(coming, later, later.AddHours(4), token), ControllerVid, later, later.AddHours(1), ShiftAttendance.Unknown, token);
        Assert.Equal([RegisterDecisions.NotOverKey], (await RefusedAsync(await DecideAsync(aod, comingShift, ShiftAttendance.NoShow, note: null, token), token))["id"]);
    }

    /// <summary>
    /// A no-show taken away stays taken away, and the register tells it (§4.5, readings 1–4): confirmed, it counts in the controller's
    /// register with its penalty; taken away with a note — none is refused —, it is excused for good: the register and the controller's own
    /// record say so, the audit keeps who took it away, and it does not become a no-show again. The statistics of the event wait for
    /// «Recalculate» of whoever writes the event: the staff of the ATC do not write them.
    /// </summary>
    [Fact]
    public async Task ANoShowTakenAwayStaysTakenAwayAndTheRegisterSaysIt()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(-5));
        var id = await EventRowAsync("taken-away", starts, starts.AddHours(4), token, checksDone: true);
        var position = await PositionRowAsync(id, starts, starts.AddHours(4), token);
        var noShow = await ShiftRowAsync(id, position, ControllerVid, starts, starts.AddHours(1), ShiftAttendance.NoShowProposed, token);
        await ShiftRowAsync(id, position, ControllerVid, starts.AddHours(1), starts.AddHours(2), ShiftAttendance.Attended, token);

        using var aod = await SignedInAsync(AodVid, token);
        await NoContentAsync(await DecideAsync(aod, noShow, ShiftAttendance.NoShow, note: null, token), token);

        var confirmed = RegisterRow(await PageAsync(aod, $"{RegisterEndpoints.ControllersPattern}?q={ControllerVid}", token), ControllerVid);
        Assert.Equal((1, 1, 0), (confirmed.GetProperty("attended").GetInt32(), confirmed.GetProperty("noShows").GetInt32(), confirmed.GetProperty("excused").GetInt32()));
        Assert.Equal(0.25, confirmed.GetProperty("penalty").GetDouble(), precision: 6);

        // Taking it away asks why.
        Assert.Equal(["errors.required"], (await RefusedAsync(await DecideAsync(aod, noShow, ShiftAttendance.Excused, note: null, token), token))["note"]);
        await NoContentAsync(await DecideAsync(aod, noShow, ShiftAttendance.Excused, $"{SlugStem} the network fell", token), token);

        // Taken away for good.
        Assert.Equal([RegisterDecisions.DecidedKey], (await RefusedAsync(await DecideAsync(aod, noShow, ShiftAttendance.NoShow, note: null, token), token))["attendance"]);

        var excused = RegisterRow(await PageAsync(aod, $"{RegisterEndpoints.ControllersPattern}?q={ControllerVid}", token), ControllerVid);
        Assert.Equal((1, 0, 1), (excused.GetProperty("attended").GetInt32(), excused.GetProperty("noShows").GetInt32(), excused.GetProperty("excused").GetInt32()));
        Assert.Equal(0, excused.GetProperty("penalty").GetDouble());

        var stored = (await ShiftsAsync(id, token))[noShow];
        Assert.Equal((ShiftAttendance.Excused, (int?)AodVid, $"{SlugStem} the network fell"), (stored.Attendance, stored.AttendanceBy, stored.AttendanceNote));

        // The controller's own record says it, with the note.
        using var controller = await SignedInAsync(ControllerVid, token);
        var record = (await OkAsync(await controller.GetAsync(RegisterEndpoints.MinePattern, token), token)).GetProperty("controller");
        Assert.Equal((1, 0, 1), (record.GetProperty("attended").GetInt32(), record.GetProperty("noShows").GetInt32(), record.GetProperty("excused").GetInt32()));
        var mine = Assert.Single(record.GetProperty("shifts").EnumerateArray(), shift => shift.GetProperty("id").GetInt64() == noShow);
        Assert.Equal(("Excused", $"{SlugStem} the network fell"), (mine.GetProperty("attendance").GetString(), mine.GetProperty("note").GetString()));

        // The statistics are the event's: «Recalculate» of whoever writes it counts the no-show excused.
        using (var coordinator = await SignedInAsync(CoordinatorVid, token))
        {
            await OkAsync(await coordinator.PostAsync($"/api/events/events/{id}/stats", content: null, token), token);
        }

        var stats = await StatsAsync(id, token);
        Assert.Equal((1, 0, 1), (stats.AtcAttended, stats.AtcNoShows, stats.AtcExcused));

        // The audit keeps who took it away, and when.
        await using var scope = _factory.Services.CreateAsyncScope();
        var audit = await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .Where(entry => entry.AfterJson!.Contains($"{SlugStem} the network fell"))
            .ToListAsync(token);
        Assert.Equal(AodVid, Assert.Single(audit).Vid);
    }

    /// <summary>
    /// A pilot over the threshold does not pass the limits per band and per event (§3.7, reading 7): with three bookings not flown, the
    /// first slot of a band goes and the second is refused; one of the next band goes, and a third in the event is refused. An event with its
    /// own limits takes two in a band. The pilot reads until when, and the register of the staff says they have the limits.
    /// </summary>
    [Fact]
    public async Task APilotOverTheThresholdDoesNotPassTheLimitsPerBandAndPerEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var history = await HistoryAsync("limits-history", token);
        await UnflownAsync(history, PilotVid, 3, excused: 0, token);

        var starts = Hour(DateTime.UtcNow.AddDays(2));
        var id = await EventRowAsync("limits", starts, starts.AddHours(6), token);
        var slots = await SlotsAsync(id, starts, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        await CreatedAsync(await BookAsync(pilot, slots[0], token), token);
        Assert.Equal([BookingLimits.WindowKey], (await RefusedAsync(await BookAsync(pilot, slots[1], token), token))[PilotBookings.SlotField]);
        await CreatedAsync(await BookAsync(pilot, slots[2], token), token);
        Assert.Equal([BookingLimits.EventKey], (await RefusedAsync(await BookAsync(pilot, slots[3], token), token))[PilotBookings.SlotField]);

        // Until when: the end of the event of the first of the three to leave the record, and the months the bookings are kept.
        var record = (await OkAsync(await pilot.GetAsync($"{RegisterEndpoints.MinePattern}?eventId={id}", token), token)).GetProperty("pilot");
        var ended = (await EventAsync(history, token)).EndsAtUtc;
        Assert.True(record.GetProperty("limited").GetBoolean());
        Assert.Equal(3, record.GetProperty("counted").GetInt32());
        Assert.Equal(ended.AddMonths(24), record.GetProperty("limitedUntil").GetDateTime(), TimeSpan.FromSeconds(1));
        Assert.Equal((2, 1, 2), (record.GetProperty("windowHours").GetInt32(), record.GetProperty("maxPerWindow").GetInt32(), record.GetProperty("maxPerEvent").GetInt32()));

        // An event of its own limits: two in a band.
        var own = await EventRowAsync("limits-own", starts, starts.AddHours(6), token, maxPerWindow: 2);
        var ownSlots = await SlotsAsync(own, starts, token);
        await CreatedAsync(await BookAsync(pilot, ownSlots[0], token), token);
        await CreatedAsync(await BookAsync(pilot, ownSlots[1], token), token);

        // A member with nothing in the record has no limits.
        using var member = await SignedInAsync(MemberVid, token);
        var free = await EventRowAsync("limits-free", starts, starts.AddHours(6), token);
        var freeSlots = await SlotsAsync(free, starts, token);
        await CreatedAsync(await BookAsync(member, freeSlots[0], token), token);
        await CreatedAsync(await BookAsync(member, freeSlots[1], token), token);
        await CreatedAsync(await BookAsync(member, freeSlots[2], token), token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var row = RegisterPilot(await PageAsync(coordinator, $"{RegisterEndpoints.PilotsPattern}?q={PilotVid}", token), PilotVid);
        Assert.True(row.GetProperty("limited").GetBoolean());
        Assert.Equal(3, row.GetProperty("counted").GetInt32());
    }

    /// <summary>
    /// An entry taken out does not count (§3.7, readings 5 and 6): with three bookings not flown and one of them taken out by the staff with a
    /// note, a pilot is under the threshold and books two slots in a band. Nobody takes out their own, a flown booking does not come out, and
    /// one taken out does not come out twice.
    /// </summary>
    [Fact]
    public async Task AnEntryTakenOutDoesNotCount()
    {
        var token = TestContext.Current.CancellationToken;
        var history = await HistoryAsync("excused-history", token);
        var unflown = await UnflownAsync(history, OtherPilotVid, 3, excused: 0, token);
        var own = (await UnflownAsync(history, CoordinatorVid, 1, excused: 0, token))[0];
        var flown = await FlownAsync(history, OtherPilotVid, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        Assert.Equal(["errors.required"], (await RefusedAsync(await ExcuseAsync(coordinator, unflown[0], note: null, token), token))["note"]);
        await NoContentAsync(await ExcuseAsync(coordinator, unflown[0], $"{SlugStem} the hub was down", token), token);
        Assert.Equal([RegisterDecisions.ExcusedKey], (await RefusedAsync(await ExcuseAsync(coordinator, unflown[0], "again", token), token))["id"]);
        Assert.Equal([RegisterDecisions.NotUnflownKey], (await RefusedAsync(await ExcuseAsync(coordinator, flown, "flown", token), token))["id"]);

        // Their own entry: who has a stake does not decide (reading 6).
        using (var mine = await ExcuseAsync(coordinator, own, "mine", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, mine.StatusCode);
        }

        var stored = (await BookingsAsync(history, token))[unflown[0]];
        Assert.Equal(((int?)CoordinatorVid, $"{SlugStem} the hub was down"), (stored.UnflownExcusedBy, stored.UnflownExcusedNote));

        var row = RegisterPilot(await PageAsync(coordinator, $"{RegisterEndpoints.PilotsPattern}?q={OtherPilotVid}", token), OtherPilotVid);
        Assert.Equal((2, 1, false), (row.GetProperty("counted").GetInt32(), row.GetProperty("excused").GetInt32(), row.GetProperty("limited").GetBoolean()));

        var starts = Hour(DateTime.UtcNow.AddDays(2));
        var id = await EventRowAsync("excused", starts, starts.AddHours(6), token);
        var slots = await SlotsAsync(id, starts, token);
        using var pilot = await SignedInAsync(OtherPilotVid, token);
        await CreatedAsync(await BookAsync(pilot, slots[0], token), token);
        await CreatedAsync(await BookAsync(pilot, slots[1], token), token);

        var record = (await OkAsync(await pilot.GetAsync(RegisterEndpoints.MinePattern, token), token)).GetProperty("pilot");
        Assert.False(record.GetProperty("limited").GetBoolean());
        var excused = Assert.Single(record.GetProperty("unflown").EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == unflown[0]);
        Assert.Equal((true, $"{SlugStem} the hub was down"), (excused.GetProperty("excused").GetBoolean(), excused.GetProperty("excusedNote").GetString()));
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static DateTime Hour(DateTime instant) =>
        DateTime.SpecifyKind(new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, 0, 0), DateTimeKind.Utc);

    private static string ShiftsUri(long eventId) => $"/api/events/atc/shifts?filter[eventId]={eventId}&pageSize=200";

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

    private static JsonElement RegisterRow(List<JsonElement> rows, int vid) =>
        Assert.Single(rows, row => row.GetProperty("controller").GetProperty("vid").GetInt32() == vid);

    private static JsonElement RegisterPilot(List<JsonElement> rows, int vid) =>
        Assert.Single(rows, row => row.GetProperty("pilot").GetProperty("vid").GetInt32() == vid);

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, long shiftId, ShiftAttendance attendance, string? note, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"/api/events/atc/shifts/{shiftId}/attendance", new { attendance = attendance.ToString(), note }, cancellationToken);

    private static Task<HttpResponseMessage> ExcuseAsync(HttpClient client, long bookingId, string? note, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"/api/events/bookings/{bookingId}/excuse", new UnflownExcuse(note), cancellationToken);

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, long slotId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slotId, Type), cancellationToken);

    private static async Task NoContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.NoContent,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
    }

    private static async Task CreatedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
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

    /// <summary>An event of the class, published and seen, its bookings open, with a roster; its own limits per band when asked.</summary>
    private async Task<long> EventRowAsync(
        string name,
        DateTime starts,
        DateTime ends,
        CancellationToken cancellationToken,
        bool checksDone = false,
        int? maxPerWindow = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfe",
            PublicSlots = true,
            HasRoster = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = ends,
            BookingOpensAtUtc = DateTime.UtcNow.AddDays(-30),
            Status = PublishStatus.Published,
            PublishedAt = DateTime.UtcNow.AddDays(-30),
            Visibility = Visibility.Public,
            RosterProposedAt = starts.AddDays(-3),
            RosterPublishedAt = starts.AddDays(-2),
            AfterDoneAt = checksDone ? ends.AddHours(7) : null,
            RestrictedMaxPerWindow = maxPerWindow,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task<long> PositionRowAsync(long eventId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var position = new EventAtcPosition
        {
            EventId = eventId,
            Callsign = Tower,
            Fir = Fir,
            FromUtc = from,
            ToUtc = to,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcPositions.Add(position);
        await database.SaveChangesAsync(cancellationToken);
        return position.Id;
    }

    /// <summary>A shift written by the installation, as the check after the event left it.</summary>
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

    /// <summary>
    /// Four public departures of an event from its airport: at ten minutes past its start and an hour later — the first band of two hours —,
    /// at three hours — the second — and at five — the third —, half an hour each, apart by more than the gap between two bookings.
    /// </summary>
    private async Task<long[]> SlotsAsync(long eventId, DateTime starts, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var offsets = new[] { 10, 60, 180, 300 };
        var slots = offsets.Select((minutes, index) => new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Public,
            EventAirportIcao = Home,
            IsArrival = false,
            Callsign = $"XEM{eventId % 1000:000}{index}",
            AircraftTypes = [Type],
            DepartureIcao = Home,
            ArrivalIcao = Away,
            OffBlockUtc = starts.AddMinutes(minutes),
            OnBlockUtc = starts.AddMinutes(minutes + 30),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        }).ToList();

        database.Slots.AddRange(slots);
        await database.SaveChangesAsync(cancellationToken);
        return [.. slots.Select(slot => slot.Id)];
    }

    /// <summary>An event of the class ended ten days ago, whose bookings the check after it looked at.</summary>
    private async Task<long> HistoryAsync(string name, CancellationToken cancellationToken)
    {
        var starts = Hour(DateTime.UtcNow.AddDays(-10));
        return await EventRowAsync(name, starts, starts.AddHours(6), cancellationToken, checksDone: true);
    }

    /// <summary>Bookings of a pilot in an event over that the check found not flown, the first <paramref name="excused"/> taken out.</summary>
    private async Task<long[]> UnflownAsync(long eventId, int vid, int count, int excused, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        for (var index = 0; index < count; index++)
        {
            ids.Add(await HistoricBookingAsync(eventId, vid, flown: false, excusedBy: index < excused ? AodVid : null, cancellationToken));
        }

        return [.. ids];
    }

    private Task<long> FlownAsync(long eventId, int vid, CancellationToken cancellationToken) =>
        HistoricBookingAsync(eventId, vid, flown: true, excusedBy: null, cancellationToken);

    private async Task<long> HistoricBookingAsync(long eventId, int vid, bool flown, int? excusedBy, CancellationToken cancellationToken)
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

        var booking = new EventBooking
        {
            EventId = eventId,
            SlotId = slot.Id,
            BookerVid = vid,
            AircraftIcao = Type,
            CreatedAt = row.StartsAtUtc.AddDays(-5),
            FlownCheckedAt = row.EndsAtUtc.AddHours(7),
            FlownAt = flown ? row.StartsAtUtc.AddMinutes(35) : null,
            UnflownExcusedBy = excusedBy,
            UnflownExcusedNote = excusedBy is null ? null : $"{SlugStem} before",
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        database.Bookings.Add(booking);
        await database.SaveChangesAsync(cancellationToken);
        return booking.Id;
    }

    private async Task<Event> EventAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<Dictionary<long, EventAtcShift>> ShiftsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcShifts.IgnoreQueryFilters().AsNoTracking()
            .Where(shift => shift.EventId == eventId)
            .ToDictionaryAsync(shift => shift.Id, cancellationToken);
    }

    private async Task<Dictionary<long, EventBooking>> BookingsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(booking => booking.EventId == eventId)
            .ToDictionaryAsync(booking => booking.Id, cancellationToken);
    }

    private async Task<EventStats> StatsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Stats.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(stats => stats.EventId == eventId, cancellationToken);
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
        user.LastName = "Register";
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
