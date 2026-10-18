using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The ATC of the events (M4, E11a; design §1.7, §4.1, §4.2, §6.3; note 2026-10-12-le-postazioni-e-la-disponibilita), through the real host
/// with the division file of this repository — its grants to the ATC operations and to the team of a FIR, and <c>firStaffScope: own</c>,
/// nothing given here by hand:
/// <list type="bullet">
/// <item>the grants of the division to the team of a FIR on the area of the ATC take effect at a start, now that the area has a row that
/// says its FIR;</item>
/// <item>the ATC operations and the chief of a FIR write the positions of an event and not its text; with <c>own</c> the chief of a FIR
/// writes, lists and reads the candidates of the positions of their FIR alone — the endpoint, the one handler, the generated list and the
/// write guard saying the same —, and a position fits its event;</item>
/// <item>a member gives their availability with their windows, is refused below the least rating and after the close, changes it and
/// withdraws it until then; the staff read it on the positions it meets;</item>
/// <item>erasing a controller's data deletes their availability for an event to come and keeps the one of an event over without their note.</item>
/// </list>
/// The FIRs are two of the test's own, known through a directory of FIRs and a directory of positions that know only them, as in
/// <c>TrainingFirHeadsTests</c>: nothing is written into the shared reference data.
/// <para>⚠️ The people are 761106–761113, handed to E11a by dalberone on 12 October 2026: the ATC operations, the chief of one FIR and the
/// assistant chief of the other, two members — rated at the least the class sets and below it —, the coordinator of the events, the person
/// erased and the superadmin. None has an address: the contacts tests count the recipients of the departments.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsAtcTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int AodVid = 761106;
    private const int ChiefVid = 761107;
    private const int AssistantChiefVid = 761108;
    private const int ControllerVid = 761109;
    private const int TraineeVid = 761110;
    private const int CoordinatorVid = 761111;
    private const int PersonVid = 761112;
    private const int SuperadminVid = 761113;

    private static readonly int[] People = [AodVid, ChiefVid, AssistantChiefVid, ControllerVid, TraineeVid, CoordinatorVid, PersonVid, SuperadminVid];

    // Two FIRs nobody has, and a position in each: the directories of this host know them and nothing else.
    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";
    private const string Tower = "XXAA_TWR";
    private const string Approach = "XXBB_APP";

    private const string SlugStem = "evt-test-e11a";

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
        }));
        var token = TestContext.Current.CancellationToken;

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);

        await SeedPersonAsync(AodVid, $"{Division().Code}-AOC", rating: null, superadmin: false, token);
        await SeedPersonAsync(ChiefVid, $"{Fir}-CH", rating: null, superadmin: false, token);
        await SeedPersonAsync(AssistantChiefVid, $"{OtherFir}-ACH", rating: null, superadmin: false, token);
        await SeedPersonAsync(ControllerVid, position: null, Least.Number, superadmin: false, token);
        await SeedPersonAsync(TraineeVid, position: null, IvaoRatings.Vocabulary.Ladder(RatingKind.Atc)[1].Number, superadmin: false, token);
        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", Least.Number, superadmin: false, token);
        await SeedPersonAsync(PersonVid, position: null, Least.Number, superadmin: false, token);
        await SeedPersonAsync(SuperadminVid, position: null, rating: null, superadmin: true, token);

        // The least rating of the class, and the availabilities closing three days before an event, over whatever the hub kept.
        _settingsBefore = await WriteSettingsAsync($$"""{"minimumAtcRating":{{Least.Number}},"applicationsCloseDays":3}""", token);
    }

    public async ValueTask DisposeAsync()
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserGrants.Where(grant => grant.Vid != null && People.Contains(grant.Vid.Value) && grant.Reason == SlugStem).ExecuteDeleteAsync();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false).SetProperty(user => user.RatingAtc, (int?)null));
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>The least ATC rating of the class: the third of the ladder, so that one below it exists.</summary>
    private static Rating Least => IvaoRatings.Vocabulary.Ladder(RatingKind.Atc)[2];

    /// <summary>
    /// The grants of the division to the team of a FIR on the ATC (E2) waited for a row of the area that says its FIR, and were skipped at
    /// every start until then: with the positions (E11a) the catalogue knows the area, and a start applies them — here the seeder run as a
    /// start runs it, with the two taken back first, as on an installation that never had them.
    /// </summary>
    [Fact]
    public async Task TheGrantsToTheTeamOfAFirTakeEffectWithTheFirstRowOfTheAreaThatSaysItsFir()
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(FirStaffScope.Own, Division().FirStaffScope);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var catalogue = scope.ServiceProvider.GetRequiredService<PermissionCatalog>();
            Assert.True(catalogue.IsOfAnAreaWithAFir(EventsPermissions.AtcView));
            Assert.True(catalogue.IsOfAnAreaWithAFir(EventsPermissions.AtcEdit));
            Assert.False(catalogue.IsOfAnAreaWithAFir(EventsPermissions.View));
        }

        string[] atc = [EventsPermissions.AtcView, EventsPermissions.AtcEdit];
        Assert.Equal(atc.Order(StringComparer.Ordinal), await FirTeamGrantsAsync(token));

        // As on an installation whose last start had no such row: the two grants and their fingerprints gone.
        var seeds = Division().PositionGrants.Where(seed => seed.FirTeam && atc.Contains(seed.Permission)).ToList();
        Assert.Equal(2, seeds.Count);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserGrants.Where(grant => grant.PositionFirTeam && atc.Contains(grant.Value)).ExecuteDeleteAsync(token);
            var setting = await database.DivisionSettings.SingleAsync(row => row.Key == PositionGrantSeeder.AppliedSettingKey, token);
            var applied = JsonSerializer.Deserialize<List<string>>(setting.ValueJson)!;
            applied.RemoveAll(fingerprint => seeds.Any(seed => PositionGrantSeeder.Fingerprint(seed) == fingerprint));
            setting.ValueJson = JsonSerializer.Serialize(applied);
            await database.SaveChangesAsync(token);
        }

        Assert.Empty(await FirTeamGrantsAsync(token));

        // The start: applied, once.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<PositionGrantSeeder>();
            Assert.Equal(2, await seeder.SeedAsync(token));
            Assert.Equal(0, await seeder.SeedAsync(token));
        }

        Assert.Equal(atc.Order(StringComparer.Ordinal), await FirTeamGrantsAsync(token));

        // And they reach the chief of a FIR: the section of the ATC is in their back office — with the register of the controllers (E13b),
        // read with the same permission — and nothing else of the events.
        using var chief = await SignedInAsync(ChiefVid, token);
        var me = await chief.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Equal(
            atc.Order(StringComparer.Ordinal),
            me.GetProperty("permissions").EnumerateArray()
                .Select(permission => permission.GetProperty("name").GetString()!)
                .Where(name => EventsPermissions.All.Any(permission => permission.Name == name))
                .Distinct()
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            ["/staff/events/atc", "/staff/events/controllers"],
            me.GetProperty("navigation").GetProperty("staff").EnumerateArray()
                .Where(entry => entry.TryGetProperty("module", out var module) && module.GetString() == EventsModule.ModuleKey)
                .Select(entry => entry.GetProperty("path").GetString()!));
    }

    /// <summary>
    /// The «done when» of E11a on the server: the ATC operations open two positions of an event — each a position of the division, its FIR
    /// copied by the server, the event's window when no hours are written — and the chief of a FIR one of their FIR; neither writes the
    /// event's text. The heads of the events with a roster are read by whoever holds the ATC, the chief who does not read the events too.
    /// </summary>
    [Fact]
    public async Task TheAtcOperationsAndAChiefOfAFirWriteThePositionsAndNotTheEventsText()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 20);
        var id = await EventRowAsync("write", starts, ends, hasRoster: true, token);

        // The approach for the whole event, no hours written; the tower from the second hour, its callsign as the core spells it.
        using var aod = await SignedInAsync(AodVid, token);
        var approach = await CreatedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Approach), token), token);
        Assert.Equal(OtherFir, approach.GetProperty("fir").GetString());
        Assert.Equal(starts, approach.GetProperty("fromUtc").GetDateTime());
        Assert.Equal(ends, approach.GetProperty("toUtc").GetDateTime());
        var tower = await CreatedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower.ToLowerInvariant(), starts.AddHours(1), ends), token), token);
        Assert.Equal(Tower, tower.GetProperty("callsign").GetString());
        Assert.Equal(Fir, tower.GetProperty("fir").GetString());

        // The chief of the tower's FIR, the first hour of it.
        using var chief = await SignedInAsync(ChiefVid, token);
        await CreatedAsync(
            await chief.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower, starts, starts.AddHours(1)) with { Note = "evt-test-e11a the first hour" }, token),
            token);
        Assert.Equal(3, (await PositionsAsync(id, token)).Count);

        // The event itself: neither writes it.
        var payload = await EventPayloadAsync(id, token);
        foreach (var who in new[] { aod, chief })
        {
            using var response = await who.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", payload, token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // Its head, for the staff of the ATC: the chief, who does not read the event, too; a member, no.
        var heads = await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.EventsPattern}?q={SlugStem}-write&pageSize=100", token), token);
        var head = Assert.Single(heads.GetProperty("items").EnumerateArray());
        Assert.Equal(starts.AddDays(-3), head.GetProperty("closesAtUtc").GetDateTime());
        Assert.Equal(60, head.GetProperty("shiftMinutes").GetInt32());
        using (var response = await chief.GetAsync($"{EventEndpoints.Pattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var member = await SignedInAsync(ControllerVid, token);
        using (var response = await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>
    /// With <c>firStaffScope: own</c> the chief of a FIR writes the positions of their FIR alone (§4.1): a position of the other FIR is not
    /// created, not moved into, not deleted, not listed, and its candidates are not read — the endpoint, the one handler, the generated
    /// list and the write guard saying the same —; the assistant chief of the other FIR the other way round. The ATC operations, staff of a
    /// department, are never held to a FIR.
    /// </summary>
    [Fact]
    public async Task WithOwnTheChiefOfAFirWritesOnlyThePositionsOfTheirFir()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 21);
        var id = await EventRowAsync("own", starts, ends, hasRoster: true, token);

        using var chief = await SignedInAsync(ChiefVid, token);
        using var assistant = await SignedInAsync(AssistantChiefVid, token);
        using var aod = await SignedInAsync(AodVid, token);

        var ours = (await CreatedAsync(await chief.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower), token), token)).GetProperty("id").GetInt64();
        var theirs = (await CreatedAsync(await assistant.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Approach), token), token)).GetProperty("id").GetInt64();

        // Not created on the other FIR, and nothing written.
        using (var response = await chief.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Approach, starts.AddHours(1), ends), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal(2, (await PositionsAsync(id, token)).Count);

        // Its own not moved onto the other FIR; the other's neither changed nor deleted.
        var read = await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.Pattern}/{ours}", token), token);
        using (var response = await chief.PutAsJsonAsync($"{AtcPositionEndpoints.Pattern}/{ours}", Position(id, Approach) with { RowVersion = read.GetProperty("rowVersion").GetDateTime() }, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        foreach (var request in new[]
        {
            new HttpRequestMessage(HttpMethod.Get, $"{AtcPositionEndpoints.Pattern}/{theirs}"),
            new HttpRequestMessage(HttpMethod.Delete, $"{AtcPositionEndpoints.Pattern}/{theirs}"),
            new HttpRequestMessage(HttpMethod.Get, $"{AtcPositionEndpoints.Pattern}/{theirs}/candidates"),
        })
        {
            using (request)
            using (var response = await chief.SendAsync(request, token))
            {
                Assert.True(
                    response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                    $"{request.Method} {request.RequestUri}: {response.StatusCode}");
            }
        }

        Assert.Equal([(Tower, Fir), (Approach, OtherFir)], (await PositionsAsync(id, token)).Select(position => (position.Callsign, position.Fir)));

        // The list: each chief their FIR's; the ATC operations both.
        Assert.Equal([Tower], await ListedAsync(chief, id, token));
        Assert.Equal([Approach], await ListedAsync(assistant, id, token));
        Assert.Equal([Tower, Approach], (await ListedAsync(aod, id, token)).Order(StringComparer.Ordinal));

        // The write guard, with no endpoint in the way: the chief's identity creates a position of the other FIR and is stopped.
        var chiefIdentity = await IdentityOfAsync(ChiefVid, token);
        var refused = await Assert.ThrowsAsync<ForbiddenDomainException>(() => AsAsync(chiefIdentity, async services =>
        {
            var database = services.GetRequiredService<EventsDbContext>();
            database.AtcPositions.Add(new EventAtcPosition
            {
                EventId = id,
                Callsign = Approach,
                Fir = OtherFir,
                FromUtc = starts,
                ToUtc = ends,
                OwnerDepartment = Department.ED,
                OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
            });
            return await database.SaveChangesAsync(token);
        }));
        Assert.Equal(EventsPermissions.AtcEdit, refused.Permission);

        // Its own: changed, and deleted.
        var changed = await OkAsync(
            await chief.PutAsJsonAsync($"{AtcPositionEndpoints.Pattern}/{ours}", Position(id, Tower, starts.AddHours(1), ends) with { RowVersion = read.GetProperty("rowVersion").GetDateTime() }, token),
            token);
        Assert.Equal(starts.AddHours(1), changed.GetProperty("fromUtc").GetDateTime());
        using (var response = await chief.DeleteAsync($"{AtcPositionEndpoints.Pattern}/{ours}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    /// <summary>
    /// A position fits its event (readings 3 and 4 of the note): an event with a roster — whose switch then stays on while it has positions —,
    /// a position of the division, inside the event's window, once at a time.
    /// </summary>
    [Fact]
    public async Task APositionIsOneOfTheDivisionInsideAnEventWithARosterOnceAtATime()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 22);
        var id = await EventRowAsync("fit", starts, ends, hasRoster: true, token);
        var without = await EventRowAsync("fit-no-roster", starts, ends, hasRoster: false, token);
        using var aod = await SignedInAsync(AodVid, token);

        Assert.Equal(["events:errors.eventHasNoRoster"], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(without, Tower), token), token))["eventId"]);
        Assert.Equal(["events:errors.positionUnknown"], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, "XXCC_CTR"), token), token))["callsign"]);
        Assert.Equal(["events:errors.positionOutsideEvent"], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower, starts.AddHours(-1), ends), token), token))["fromUtc"]);
        Assert.Equal([AtcRules.EndsBeforeItStartsKey], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower, ends, starts), token), token))["toUtc"]);

        await CreatedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower, starts, starts.AddHours(2)), token), token);
        Assert.Equal(["events:errors.positionTwice"], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower, starts.AddHours(1), ends), token), token))["callsign"]);

        // Touching is not meeting: the same position again from the hour the first closes.
        await CreatedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(id, Tower, starts.AddHours(2), ends), token), token);

        // The switch of the roster stays on while the event has positions.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var payload = await EventPayloadAsync(id, token) with { HasRoster = false };
        Assert.Equal(["events:errors.hasRosterRows"], (await RefusedAsync(await coordinator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", payload, token), token))["hasRoster"]);
    }

    /// <summary>
    /// A member's availability (§4.2): given with their windows from the event's page — its rules field by field —, refused below the least
    /// rating and on an event whose availabilities closed; changed and withdrawn until the close, withdrawing deleting it with its windows,
    /// as the audit says.
    /// </summary>
    [Fact]
    public async Task AnAvailabilityIsRefusedBelowTheLeastRatingAndAfterTheClose()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 23);
        var open = await EventRowAsync("give", starts, ends, hasRoster: true, token);
        var (soon, soonEnds) = Window(days: 2);
        var closed = await EventRowAsync("give-closed", soon, soonEnds, hasRoster: true, token);
        await PositionRowAsync(open, Tower, starts, ends, token);
        await PositionRowAsync(closed, Tower, soon, soonEnds, token);

        // Below the least rating: refused, and nothing stored. The page says so before anybody tries.
        using var trainee = await SignedInAsync(TraineeVid, token);
        Assert.Equal([AtcRules.RatingTooLowKey], (await RefusedAsync(await trainee.PutAsJsonAsync(Mine(open), Request((starts, ends)), token), token))["id"]);
        Assert.Empty(await AvailabilitiesAsync(open, token));
        var page = await OkAsync(await trainee.GetAsync($"/api/events/public/{SlugStem}-give", token), token);
        Assert.False(page.GetProperty("atc").GetProperty("ratedEnough").GetBoolean());
        Assert.Equal(Least.ShortName, page.GetProperty("atc").GetProperty("minimumRating").GetString());

        // The windows, each on its field: one that ends before it starts, one outside the event, two that meet; and none at all.
        using var controller = await SignedInAsync(ControllerVid, token);
        var wrong = await RefusedAsync(
            await controller.PutAsJsonAsync(
                Mine(open),
                Request((starts.AddHours(2), starts.AddHours(1)), (starts.AddHours(-1), starts.AddHours(1)), (starts.AddHours(2), starts.AddHours(4)), (starts.AddHours(3), starts.AddHours(5))),
                token),
            token);
        Assert.Equal([AtcRules.EndsBeforeItStartsKey], wrong["windows[0].toUtc"]);
        Assert.Equal([AtcRules.OutsideEventKey], wrong["windows[1].fromUtc"]);
        Assert.Equal(["events:errors.windowsOverlap"], wrong["windows[2].fromUtc"]);
        Assert.Equal(["errors.required"], (await RefusedAsync(await controller.PutAsJsonAsync(Mine(open), Request(), token), token))["windows"]);

        // Given: two windows, in their order of time, and the note.
        var given = await CreatedAsync(
            await controller.PutAsJsonAsync(Mine(open), Request((starts.AddHours(3), ends), (starts, starts.AddHours(2))) with { Note = "  evt-test-e11a with my trainee  " }, token),
            token);
        Assert.Equal([starts, starts.AddHours(3)], given.GetProperty("windows").EnumerateArray().Select(window => window.GetProperty("fromUtc").GetDateTime()));
        Assert.Equal("evt-test-e11a with my trainee", given.GetProperty("note").GetString());
        Assert.True(given.GetProperty("changeable").GetBoolean());

        // Changed: the same availability, its windows replaced.
        var changed = await OkAsync(await controller.PutAsJsonAsync(Mine(open), Request((starts.AddHours(1), ends)), token), token);
        Assert.Equal(given.GetProperty("id").GetInt64(), changed.GetProperty("id").GetInt64());
        Assert.Equal([(starts.AddHours(1), ends)], (await WindowsAsync(open, token)).Select(window => (window.FromUtc, window.ToUtc)));

        // After the close: neither given, nor changed, nor withdrawn.
        Assert.Equal([AtcRules.ClosedKey], (await RefusedAsync(await controller.PutAsJsonAsync(Mine(closed), Request((soon, soonEnds)), token), token))["id"]);
        await AvailabilityRowAsync(closed, ControllerVid, soon, soonEnds, note: null, token);
        Assert.Equal([AtcRules.ClosedKey], (await RefusedAsync(await controller.DeleteAsync(Mine(closed), token), token))["id"]);
        Assert.Single(await AvailabilitiesAsync(closed, token));

        // Their own list: both, the closed one neither changeable nor withdrawable; another member reads nothing of it.
        var mine = (await OkAsync(await controller.GetAsync(AvailabilityEndpoints.MinePattern, token), token)).EnumerateArray()
            .ToDictionary(row => row.GetProperty("eventId").GetInt64(), row => (row.GetProperty("changeable").GetBoolean(), row.GetProperty("withdrawable").GetBoolean()));
        Assert.Equal((true, true), mine[open]);
        Assert.Equal((false, false), mine[closed]);
        Assert.Empty((await OkAsync(await trainee.GetAsync(AvailabilityEndpoints.MinePattern, token), token)).EnumerateArray());

        // Withdrawn before the close: the availability and its windows gone, by the member, and the audit says so.
        using (var response = await controller.DeleteAsync(Mine(open), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Empty(await AvailabilitiesAsync(open, token));
        Assert.Empty(await WindowsAsync(open, token));
        await using var scope = _factory.Services.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog;
        Assert.True(await audit.AnyAsync(row => row.Entity == "evt_atc_availability" && row.Action == "deleted" && row.Vid == ControllerVid, token));
        Assert.True(await audit.AnyAsync(row => row.Entity == "evt_atc_availability_windows" && row.Action == "deleted" && row.Vid == ControllerVid, token));
    }

    /// <summary>
    /// The staff read the availabilities per position (design §6.3, E2's form): the controllers with a window that meets its hours, named,
    /// with their rating, their windows and whether they cover it whole; how many on each position of the list. A member reads none.
    /// </summary>
    [Fact]
    public async Task TheStaffReadTheAvailabilitiesOnThePositionsTheyMeet()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 24);
        var id = await EventRowAsync("read", starts, ends, hasRoster: true, token);
        var early = await PositionRowAsync(id, Tower, starts, starts.AddHours(2), token);
        var late = await PositionRowAsync(id, Approach, starts.AddHours(4), ends, token);

        using var controller = await SignedInAsync(ControllerVid, token);
        await CreatedAsync(await controller.PutAsJsonAsync(Mine(id), Request((starts, starts.AddHours(1)), (starts.AddHours(1), starts.AddHours(3))), token), token);
        // The coordinator of the events controls too: a member like any other here.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        await CreatedAsync(await coordinator.PutAsJsonAsync(Mine(id), Request((starts.AddHours(1), starts.AddHours(5))) with { Note = "evt-test-e11a late" }, token), token);

        using var aod = await SignedInAsync(AodVid, token);
        var candidates = (await OkAsync(await aod.GetAsync($"{AtcPositionEndpoints.Pattern}/{early}/candidates", token), token))
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([ControllerVid, CoordinatorVid], candidates.Select(row => row.GetProperty("controller").GetProperty("vid").GetInt32()));

        // The first covers the position end to end with two windows that touch; the second only from its second hour.
        Assert.Equal([true, false], candidates.Select(row => row.GetProperty("wholePosition").GetBoolean()));
        Assert.Equal(Least.ShortName, candidates[0].GetProperty("rating").GetString());
        Assert.Equal(2, candidates[0].GetProperty("windows").GetInt32());

        // From when to when inside the position, as instants the list draws (Carmine's answer (B) on #256): the first from its opening to
        // its closing; the second from their first hour inside it, their window running on past its closing clipped to it.
        Assert.Equal(
            [(starts, starts.AddHours(2)), (starts.AddHours(1), starts.AddHours(2))],
            candidates.Select(row => (row.GetProperty("availableFromUtc").GetDateTime(), row.GetProperty("availableToUtc").GetDateTime())));
        Assert.Equal("evt-test-e11a late", candidates[1].GetProperty("note").GetString());

        // The late position meets only the second; the list counts them.
        Assert.Equal(
            [CoordinatorVid],
            (await OkAsync(await aod.GetAsync($"{AtcPositionEndpoints.Pattern}/{late}/candidates", token), token))
                .GetProperty("items").EnumerateArray().Select(row => row.GetProperty("controller").GetProperty("vid").GetInt32()));
        var counts = (await OkAsync(await aod.GetAsync($"{AtcPositionEndpoints.Pattern}?filter[eventId]={id}", token), token))
            .GetProperty("items").EnumerateArray().ToDictionary(row => row.GetProperty("id").GetInt64(), row => row.GetProperty("candidates").GetInt32());
        Assert.Equal(2, counts[early]);
        Assert.Equal(1, counts[late]);

        // The chief of a FIR reads the candidates of their positions; a member reads none.
        using var chief = await SignedInAsync(ChiefVid, token);
        await OkAsync(await chief.GetAsync($"{AtcPositionEndpoints.Pattern}/{early}/candidates", token), token);
        using (var response = await controller.GetAsync($"{AtcPositionEndpoints.Pattern}/{early}/candidates", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // An event with availabilities is not deleted: it is cancelled.
        Assert.Equal(["events:errors.eventHasAvailabilities"], (await RefusedAsync(await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token), token))["id"]);
    }

    /// <summary>
    /// Erasing a controller's data (§11.1, the rule of the registrations): their availability for an event still to end goes with its
    /// windows; the one of an event over stays under the pseudonym — the windows too —, without their note.
    /// </summary>
    [Fact]
    public async Task AnErasedControllerLeavesTheAvailabilitiesOfEventsOverWithoutTheirNoteAndNoneToCome()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var toCome = await EventRowAsync("erase-to-come", now.AddDays(10), now.AddDays(10).AddHours(6), hasRoster: true, token);
        var over = await EventRowAsync("erase-over", now.AddDays(-10), now.AddDays(-10).AddHours(6), hasRoster: true, token);
        await AvailabilityRowAsync(toCome, PersonVid, now.AddDays(10), now.AddDays(10).AddHours(2), "evt-test-e11a to come", token);
        var kept = await AvailabilityRowAsync(over, PersonVid, now.AddDays(-10), now.AddDays(-10).AddHours(2), "evt-test-e11a their words", token);

        // An availability the person gave and withdrew before the erasure: no eraser finds it, and its copies in the audit hold their note
        // until the core empties them (E10n, the mark on the note).
        var (starts, ends) = Window(days: 25);
        var withdrawnFrom = await EventRowAsync("erase-withdrawn", starts, ends, hasRoster: true, token);
        await PositionRowAsync(withdrawnFrom, Tower, starts, ends, token);
        using (var person = await SignedInAsync(PersonVid, token))
        {
            await CreatedAsync(await person.PutAsJsonAsync(Mine(withdrawnFrom), Request((starts, ends)) with { Note = GoneWords }, token), token);
            using var withdrawn = await person.DeleteAsync(Mine(withdrawnFrom), token);
            Assert.Equal(HttpStatusCode.NoContent, withdrawn.StatusCode);
        }

        Assert.True(await AuditMentionsAsync(GoneWords, token), "Without the note in the audit, the check below would prove nothing.");

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.availabilitiesToCome", 1, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.availabilitiesOver", 1, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, AvailabilityLines(await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", token), token)));

        var erased = await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token);
        Assert.Equal(lines, AvailabilityLines(erased));
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();

        Assert.Empty(await AvailabilitiesAsync(toCome, token));
        Assert.Empty(await WindowsAsync(toCome, token));
        var stored = Assert.Single(await AvailabilitiesAsync(over, token));
        Assert.Equal((kept, pseudonym, (string?)null), (stored.Id, stored.ControllerVid, stored.Note));
        Assert.Equal([pseudonym], (await WindowsAsync(over, token)).Select(window => window.ControllerVid));

        // The availability withdrawn before: nothing of its note in the audit.
        Assert.False(await AuditMentionsAsync(GoneWords, token));
    }

    /// <summary>A note that says who wrote it, on an availability withdrawn before the erasure.</summary>
    private const string GoneWords = "evt-test-e11a gone words";

    /// <summary>Whether a row of the audit log still copies a text with the marker in it, before or after its change.</summary>
    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    /// <summary>
    /// «Duplicate» carries the positions (the review of #256, point 1): copied with their hours moved by the difference, their FIR and their
    /// note as they were, and asked with <c>EventAtc.Edit</c> — whoever writes events without the ATC is refused, and nothing is written —;
    /// and deleting an event takes its positions with it, each with its row in the audit.
    /// </summary>
    [Fact]
    public async Task DuplicateCopiesThePositionsMovedAndDeletingAnEventTakesThem()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 26);
        var id = await EventRowAsync("dup", starts, ends, hasRoster: true, token);
        await PositionRowAsync(id, Tower, starts, starts.AddHours(2), token);
        await PositionRowAsync(id, Approach, starts.AddHours(1), ends, token);
        var moved = starts.AddDays(7);

        // Whoever writes events and not the ATC: refused, and no event made.
        await GrantAsync(TraineeVid, EventsPermissions.View, token);
        await GrantAsync(TraineeVid, EventsPermissions.Edit, token);
        using (var writer = await SignedInAsync(TraineeVid, token))
        using (var response = await writer.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/duplicate", new EventDuplicateRequest(moved, Slug("dup-refused"), PublicSlots: false), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AnyAsync(row => row.Slug == Slug("dup-refused"), token));
        }

        // The same member given the ATC too: duplicated — the 403 above was the ATC's, and no other check's.
        await GrantAsync(TraineeVid, EventsPermissions.AtcEdit, token);
        using (var writer = await SignedInAsync(TraineeVid, token))
        {
            var allowed = (await CreatedAsync(
                await writer.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/duplicate", new EventDuplicateRequest(moved.AddDays(7), Slug("dup-allowed"), PublicSlots: false), token),
                token)).GetProperty("event").GetProperty("id").GetInt64();
            Assert.Equal(2, (await PositionsAsync(allowed, token)).Count);
        }

        // The coordinator: the copy has the two positions, a week later.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var copy = (await CreatedAsync(
            await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/duplicate", new EventDuplicateRequest(moved, Slug("dup-copy"), PublicSlots: false), token),
            token)).GetProperty("event").GetProperty("id").GetInt64();
        Assert.Equal(
            [(Tower, Fir, moved, moved.AddHours(2)), (Approach, OtherFir, moved.AddHours(1), ends.AddDays(7))],
            (await PositionsAsync(copy, token)).OrderBy(position => position.FromUtc).Select(position => (position.Callsign, position.Fir, position.FromUtc, position.ToUtc)));

        // Deleted: its positions go with it, and the audit says so.
        using (var response = await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{copy}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Empty(await PositionsAsync(copy, token));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.CountAsync(
                row => row.Entity == "evt_atc_positions" && row.Action == "deleted" && row.Vid == CoordinatorVid,
                token));
        }
    }

    /// <summary>
    /// The small points of the review of #256: no position written on an event cancelled or over (point 6); a position keeps the FIR it
    /// was written with while its callsign does not change, so the chief of that FIR still deletes it after the directory moved the
    /// callsign (point 7); the positions offered to the chief of a FIR are their FIR's (point 8); and a member takes back the availability
    /// they gave to an event they no longer see (point 3).
    /// </summary>
    [Fact]
    public async Task ThePositionsAndTheAvailabilitiesKeepToTheirEventAndTheirFir()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 27);
        using var aod = await SignedInAsync(AodVid, token);

        // Cancelled, and over: nothing opened.
        var cancelled = await EventRowAsync("small-cancelled", starts, ends, hasRoster: true, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(row => row.Id == cancelled)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.CancelledAt, DateTime.UtcNow), token);
        }

        var over = await EventRowAsync("small-over", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-2).AddHours(4), hasRoster: true, token);
        Assert.Equal(["events:errors.positionOnCancelledEvent"], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(cancelled, Tower), token), token))["eventId"]);
        Assert.Equal(["events:errors.positionOnEventOver"], (await RefusedAsync(await aod.PostAsJsonAsync(AtcPositionEndpoints.Pattern, Position(over, Tower), token), token))["eventId"]);

        // A position written when the approach was the first FIR's: its chief still deletes it, on the FIR it was written with.
        var id = await EventRowAsync("small", starts, ends, hasRoster: true, token);
        var written = await PositionRowAsync(id, Approach, starts, ends, token, fir: Fir);
        using var chief = await SignedInAsync(ChiefVid, token);
        using (var response = await chief.DeleteAsync($"{AtcPositionEndpoints.Pattern}/{written}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        // The positions offered: the chief their FIR's, the ATC operations every one.
        Assert.Equal([Tower], await OfferedAsync(chief, id, token));
        Assert.Equal([Tower, Approach], (await OfferedAsync(aod, id, token)).Order(StringComparer.Ordinal));

        // An event the member no longer sees — its «seen from» moved after they gave their availability —: they still read and withdraw it.
        var unseen = await EventRowAsync("small-unseen", starts, ends, hasRoster: true, token);
        await AvailabilityRowAsync(unseen, ControllerVid, starts, ends, note: null, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(row => row.Id == unseen)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.VisibleFromUtc, starts.AddDays(-1)), token);
        }

        using var controller = await SignedInAsync(ControllerVid, token);
        Assert.Contains(unseen, (await OkAsync(await controller.GetAsync(AvailabilityEndpoints.MinePattern, token), token)).EnumerateArray().Select(row => row.GetProperty("eventId").GetInt64()));
        using (var response = await controller.DeleteAsync(Mine(unseen), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    /// <summary>
    /// A change of an availability that meets another change of the same member, from another window, answers 409 (the review of #256,
    /// point 4), never «not found» for an availability that is there. Made deterministic as the races of the bookings are (E6a): a
    /// transaction of the test takes away the window the request is about to replace and holds it; the request reads the window — not
    /// committed away yet —, tries to delete it and waits; the test commits; the request's delete finds nothing, and it answers 409.
    /// </summary>
    [Fact]
    public async Task AChangeThatMeetsAnotherChangeOfTheSameMemberIsAConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var (starts, ends) = Window(days: 28);
        var id = await EventRowAsync("conflict", starts, ends, hasRoster: true, token);
        await PositionRowAsync(id, Tower, starts, ends, token);
        await AvailabilityRowAsync(id, ControllerVid, starts, ends, note: null, token);
        var window = Assert.Single(await WindowsAsync(id, token)).Id;
        using var controller = await SignedInAsync(ControllerVid, token);

        HttpResponseMessage answered;
        await using (var held = _factory.Services.CreateAsyncScope())
        {
            var database = held.ServiceProvider.GetRequiredService<EventsDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            database.AtcWindows.Remove(await database.AtcWindows.IgnoreQueryFilters().SingleAsync(row => row.Id == window, token));
            await database.SaveChangesAsync(token);

            var request = controller.PutAsJsonAsync(Mine(id), Request((starts.AddHours(1), ends)), token);
            await WaitForALockWaitAsync(token);
            await transaction.CommitAsync(token);
            answered = await request;
        }

        using (answered)
        {
            Assert.Equal(HttpStatusCode.Conflict, answered.StatusCode);
        }

        // Nothing of the request written: the availability as the other change left it, with no window.
        Assert.Single(await AvailabilitiesAsync(id, token));
        Assert.Empty(await WindowsAsync(id, token));
    }

    /// <summary>Until a transaction waits for a lock: the request under test, behind the transaction the test holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms: asked more often, it keeps answering what
    /// it saw the first time (CONTRIBUTING.md).</remarks>
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

    /// <summary>The least ATC rating of the settings is one of the core's vocabulary: another number is refused on its field.</summary>
    [Fact]
    public async Task TheLeastRatingIsOneOfTheVocabulary()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var settings = ModuleSettingsEndpoints.Pattern.Replace("{key}", EventsModule.ModuleKey, StringComparison.Ordinal);

        var current = await OkAsync(await coordinator.GetAsync(settings, token), token);
        var written = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(current.GetRawText())!;
        written["minimumAtcRating"] = JsonSerializer.SerializeToElement(IvaoRatings.Vocabulary.Ladder(RatingKind.Atc).Max(rating => rating.Number) + 1);

        Assert.Equal(["events:errors.ratingUnknown"], (await RefusedAsync(await coordinator.PutAsJsonAsync(settings, written, token), token))["minimumAtcRating"]);
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string Mine(long eventId) => $"{AvailabilityEndpoints.MinePattern}/{eventId}";

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>A day so many days away, from 17:00 to 23:00 UTC.</summary>
    private static (DateTime Starts, DateTime Ends) Window(int days)
    {
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days).AddHours(17), DateTimeKind.Utc);
        return (starts, starts.AddHours(6));
    }

    private static EventAtcPositionWriteDto Position(long eventId, string callsign, DateTime? from = null, DateTime? to = null) =>
        new(eventId, callsign, from, to, Note: null, default);

    private static AvailabilityRequest Request(params (DateTime From, DateTime To)[] windows) =>
        new([.. windows.Select(window => new AtcWindowDto(window.From, window.To))], Note: null);

    /// <summary>The event as its form would send it back, read past every filter.</summary>
    private async Task<EventWriteDto> EventPayloadAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == id, cancellationToken);

        return new EventWriteDto(
            row.Kind,
            row.PublicSlots,
            row.PrivateSlots,
            row.WholeDivision,
            row.Organizer,
            row.ExternalUrl,
            Text("evt-test-e11a another title"),
            row.Slug,
            row.Summary,
            Body: null,
            row.BannerMediaId,
            row.VisibleFromUtc,
            row.BookingOpensAtUtc,
            row.StartsAtUtc,
            row.EndsAtUtc,
            row.Visibility,
            row.RowVersion,
            row.InPerson,
            row.Venue,
            row.HasRoster,
            row.ShiftMinutes);
    }

    /// <summary>The callsigns of the positions of the division a reader is offered on an event.</summary>
    private static async Task<List<string>> OfferedAsync(HttpClient reader, long eventId, CancellationToken cancellationToken) =>
        [
            .. (await OkAsync(await reader.GetAsync($"{AtcPositionEndpoints.DivisionPositionsPattern}?eventId={eventId}", cancellationToken), cancellationToken))
                .EnumerateArray().Select(row => row.GetProperty("callsign").GetString()!),
        ];

    /// <summary>A permission on the events department to a VID, as the permissions screen writes one; taken back with the class.</summary>
    private async Task GrantAsync(int vid, string permission, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.ED,
            Effect = GrantEffect.Grant,
            Reason = SlugStem,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The callsigns of an event's positions a reader finds in the generated list.</summary>
    private static async Task<List<string>> ListedAsync(HttpClient reader, long eventId, CancellationToken cancellationToken) =>
        [
            .. (await OkAsync(await reader.GetAsync($"{AtcPositionEndpoints.Pattern}?filter[eventId]={eventId}&pageSize=100", cancellationToken), cancellationToken))
                .GetProperty("items").EnumerateArray().Select(row => row.GetProperty("callsign").GetString()!),
        ];

    /// <summary>The events' lines of an erasure about the ATC: what, how many, and what becomes of them.</summary>
    private static (string Key, int Count, string Outcome)[] AvailabilityLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{EventsModule.ModuleKey}:erasure.availabilities", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

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

    // ---- the write guard, with no endpoint in the way --------------------------------------------------------------------------

    /// <summary>The identity a login of <paramref name="vid"/> would put in the cookie now: positions, grants, and the FIR of each.</summary>
    private async Task<ClaimsPrincipal> IdentityOfAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var signedIn = (await scope.ServiceProvider.GetRequiredService<UserSyncService>().LoadAsync(vid, cancellationToken))!;

        return new ClaimsPrincipal(HubClaims.BuildIdentity(
            signedIn.User.Vid,
            signedIn.User.FirstName,
            signedIn.User.LastName,
            signedIn.User.Locale ?? Division().DefaultLocale,
            signedIn.User.SecurityStamp,
            signedIn.User.IsSuperadmin,
            signedIn.User.IsStaff,
            signedIn.Positions,
            signedIn.Permissions));
    }

    private async Task<T> AsAsync<T>(ClaimsPrincipal who, Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = who, RequestServices = scope.ServiceProvider };

        try
        {
            return await work(scope.ServiceProvider);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    // ---- rows and people ------------------------------------------------------------------------------------------------------

    /// <summary>A published event of the class, written by the installation: one whose availabilities closed could not be made any other way.</summary>
    private async Task<long> EventRowAsync(string name, DateTime starts, DateTime ends, bool hasRoster, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            HasRoster = hasRoster,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = ends,
            Status = PublishStatus.Published,
            PublishedAt = DateTime.UtcNow.AddDays(-20),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    /// <summary>
    /// A position of an event, written by the installation, with the FIR the class's directory gives it — or another one, as written on a
    /// day the directory said otherwise.
    /// </summary>
    private async Task<long> PositionRowAsync(long eventId, string callsign, DateTime from, DateTime to, CancellationToken cancellationToken, string? fir = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var position = new EventAtcPosition
        {
            EventId = eventId,
            Callsign = callsign,
            Fir = fir ?? (callsign == Tower ? Fir : OtherFir),
            FromUtc = from,
            ToUtc = to,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcPositions.Add(position);
        await database.SaveChangesAsync(cancellationToken);
        return position.Id;
    }

    /// <summary>An availability with one window, written by the installation.</summary>
    private async Task<long> AvailabilityRowAsync(long eventId, int vid, DateTime from, DateTime to, string? note, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var availability = new EventAtcAvailability
        {
            EventId = eventId,
            ControllerVid = vid,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
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
        return availability.Id;
    }

    private async Task<List<EventAtcPosition>> PositionsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcPositions.IgnoreQueryFilters().AsNoTracking()
            .Where(position => position.EventId == eventId)
            .OrderBy(position => position.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<EventAtcAvailability>> AvailabilitiesAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcAvailabilities.IgnoreQueryFilters().AsNoTracking()
            .Where(availability => availability.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<EventAtcWindow>> WindowsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcWindows.IgnoreQueryFilters().AsNoTracking()
            .Where(window => window.EventId == eventId)
            .OrderBy(window => window.FromUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The permissions the seeded grants to the team of a FIR give on the area of the ATC.</summary>
    private async Task<List<string>> FirTeamGrantsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants.AsNoTracking()
            .Where(grant => grant.PositionFirTeam && grant.Value.StartsWith(EventsPermissions.AtcArea + "."))
            .Select(grant => grant.Value)
            .OrderBy(value => value)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The settings of the events as JSON, or none: writes these, and answers what was there.</summary>
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

    /// <summary>A member in English with one position or none, an ATC rating or none, the superadmin or not; never an address.</summary>
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
        user.LastName = "Atc";
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

    /// <summary>The positions of this host's division: a tower in one FIR and an approach in the other.</summary>
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
}
