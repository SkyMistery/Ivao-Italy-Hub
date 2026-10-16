using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Public;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The publication of the roster, its mails and handing a shift over (M4, E12; design §4.4, §4.4-bis, §7.1, §7.3; note
/// 2026-10-14-la-pubblicazione-del-roster-e-la-cessione), through the real host with the division file of this repository — its grants to the
/// ATC operations, to the team of a FIR and to the coordinator, nothing given by hand:
/// <list type="bullet">
/// <item><c>events-roster</c> publishes a roster at its date, once, and tells each controller of their shifts in one mail, once; a roster
/// whose date is still to come is not published, and its controller reads nothing of it;</item>
/// <item>after the publication a shift added, moved or taken away tells its controller at once;</item>
/// <item>a request to hand a shift over passes the shift when the staff approve it, and nobody approves their own, the super administrator
/// included; a refusal wants a note; a request to nobody wants the substitute; from the event's start nothing passes;</item>
/// <item>the page of the roster names nobody to a visitor; the block of the coverage counts the uncovered pieces and the requests;</item>
/// <item>erasing a controller's data takes their requests of an event to come and keeps the ones of an event over without the notes.</item>
/// </list>
/// <para>⚠️ The people are 761122–761128, after E11b's: the ATC operations, the chief of the tower's FIR, two controllers with an address — the
/// one who hands over and the one who takes —, the coordinator of the events, the super administrator and the person erased.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsRosterPublicationTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int AodVid = 761122;
    private const int ChiefVid = 761123;
    private const int ControllerVid = 761124;
    private const int OtherVid = 761125;
    private const int CoordinatorVid = 761126;
    private const int SuperadminVid = 761127;
    private const int PersonVid = 761128;

    private static readonly int[] People = [AodVid, ChiefVid, ControllerVid, OtherVid, CoordinatorVid, SuperadminVid, PersonVid];

    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";
    private const string Tower = "XXAA_TWR";
    private const string Approach = "XXBB_APP";

    private const string SlugStem = "evt-test-e12";

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
            services.AddScoped<IAtcActivitySource, NoArchive>();
        }));
        var token = TestContext.Current.CancellationToken;

        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, token);
        await ForgetMailsAsync(token);

        var ladder = IvaoRatings.Vocabulary.Ladder(RatingKind.Atc);
        await SeedPersonAsync(AodVid, $"{Division().Code}-AOC", rating: null, email: null, superadmin: false, token);
        await SeedPersonAsync(ChiefVid, $"{Fir}-CH", rating: null, email: null, superadmin: false, token);
        await SeedPersonAsync(ControllerVid, position: null, ladder[3].Number, "e12-controller@example.org", superadmin: false, token);
        await SeedPersonAsync(OtherVid, position: null, ladder[3].Number, "e12-other@example.org", superadmin: false, token);
        await SeedPersonAsync(CoordinatorVid, $"{Division().Code}-EC", ladder[3].Number, email: null, superadmin: false, token);
        await SeedPersonAsync(SuperadminVid, position: null, ladder[3].Number, email: null, superadmin: true, token);
        await SeedPersonAsync(PersonVid, position: null, ladder[3].Number, email: null, superadmin: false, token);

        // An hour a shift, two in a row and one of break; the close three days before the start, the publication two.
        _settingsBefore = await WriteSettingsAsync(
            """{"applicationsCloseDays":3,"rosterPublishDays":2,"shiftMinutes":60,"maxConsecutiveShifts":2,"breakShifts":1}""",
            token);
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
            await database.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(user => user.IsSuperadmin, false)
                    .SetProperty(user => user.RatingAtc, (int?)null)
                    .SetProperty(user => user.Email, (string?)null));
        }

        await _factory.DisposeAsync();
        await _hub.DisposeAsync();
    }

    /// <summary>
    /// The «done when» of E12 on the server: the job proposes and publishes the roster of an evening whose date came — the controller told of
    /// their two shifts in one mail —, and leaves the roster of an evening whose date is to come proposed and unpublished; run again, no mail
    /// more. The controller finds the published shifts on their own page and in the block of <c>/me</c>, not the others; the mark leaves the
    /// row version of the event as it was.
    /// </summary>
    [Fact]
    public async Task TheJobPublishesTheRosterAtItsDateAndTellsEachControllerOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var due = await ProposableAsync("publish", Hour(DateTime.UtcNow.AddHours(30)), token);
        var waiting = await ProposableAsync("publish-waiting", Hour(DateTime.UtcNow.AddHours(60)), token);
        var versionBefore = (await EventAsync(due, token)).RowVersion;

        await RunJobAsync(token);

        var published = await EventAsync(due, token);
        Assert.NotNull(published.RosterProposedAt);
        Assert.NotNull(published.RosterPublishedAt);
        Assert.Equal(versionBefore, published.RowVersion);
        Assert.NotNull((await EventAsync(waiting, token)).RosterProposedAt);
        Assert.Null((await EventAsync(waiting, token)).RosterPublishedAt);

        var shifts = await ShiftsAsync(due, token);
        Assert.Equal(2, shifts.Count);
        Assert.All(shifts, shift => Assert.NotNull(shift.NotifiedAt));
        Assert.All(await ShiftsAsync(waiting, token), shift => Assert.Null(shift.NotifiedAt));

        // One mail for the two shifts of the evening, with the roster's page; none for the evening still to publish.
        var mail = Assert.Single(await MailsAsync(ControllerVid, EventsNotifications.AtcShiftAssigned, token));
        Assert.Contains($"{Slug("publish")}/roster", mail.DataJson, StringComparison.Ordinal);
        Assert.Contains("\"count\":\"2\"", mail.DataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(Slug("publish-waiting"), mail.DataJson, StringComparison.Ordinal);

        // Run again: nothing more.
        await RunJobAsync(token);
        Assert.Single(await MailsAsync(ControllerVid, EventsNotifications.AtcShiftAssigned, token));

        // Their own page and the block: the published shifts, not the others.
        using var controller = await SignedInAsync(ControllerVid, token);
        var mine = await ListAsync(await controller.GetAsync(TransferEndpoints.MinePattern, token), token);
        Assert.Equal(2, mine.Count);
        Assert.All(mine, shift => Assert.Equal(due, shift.GetProperty("eventId").GetInt64()));
        Assert.All(mine, shift => Assert.True(shift.GetProperty("transferable").GetBoolean()));

        var block = await OkAsync(await controller.GetAsync(BlockUri("events.myEvents", "{}"), token), token);
        Assert.Equal(2, block.GetProperty("shifts").GetArrayLength());
    }

    /// <summary>
    /// After the publication the correction tells at once (reading 3): a shift added tells its controller it is theirs, a shift moved to other
    /// hours tells them how it was and how it is, a shift taken away tells them it is gone.
    /// </summary>
    [Fact]
    public async Task AChangeAfterThePublicationTellsTheControllerAtOnce()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddHours(30));
        var id = await ProposableAsync("change", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);
        using var aod = await SignedInAsync(AodVid, token);

        // Added on the tower's uncovered hour, for the other controller: told at once.
        var added = await CreatedAsync(
            await aod.PostAsJsonAsync(ShiftEndpoints.Pattern, new EventAtcShiftWriteDto(positions[Tower], OtherVid, starts.AddHours(2), starts.AddHours(3), default), token),
            token);
        Assert.Single(await MailsAsync(OtherVid, EventsNotifications.AtcShiftAssigned, token));
        Assert.NotNull((await ShiftsAsync(id, token)).Single(shift => shift.Id == added.GetProperty("id").GetInt64()).NotifiedAt);

        // Moved to the approach at the same hour: how it was and how it is.
        var moved = await OkAsync(
            await aod.PutAsJsonAsync(
                $"{ShiftEndpoints.Pattern}/{added.GetProperty("id").GetInt64()}",
                new EventAtcShiftWriteDto(positions[Approach], OtherVid, starts.AddHours(1), starts.AddHours(2), added.GetProperty("rowVersion").GetDateTime()),
                token),
            token);
        var changed = Assert.Single(await MailsAsync(OtherVid, EventsNotifications.AtcShiftChanged, token));
        Assert.Contains(Tower, changed.DataJson, StringComparison.Ordinal);
        Assert.Contains(Approach, changed.DataJson, StringComparison.Ordinal);

        // Taken away: gone, and they are told.
        using (var response = await aod.DeleteAsync($"{ShiftEndpoints.Pattern}/{moved.GetProperty("id").GetInt64()}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Single(await MailsAsync(OtherVid, EventsNotifications.AtcShiftRemoved, token));
    }

    /// <summary>
    /// Handing a shift over (§4.4-bis): the controller asks to hand it to the other — once: a second request waits behind the first —; the ATC
    /// operations approve it, the shift passes, the staff's, without the why, and both are told; a refusal wants a note and tells whoever asked;
    /// a request to nobody wants the substitute.
    /// </summary>
    [Fact]
    public async Task AnApprovedRequestPassesTheShiftAndARefusalWantsANote()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await ProposableAsync("transfer", Hour(DateTime.UtcNow.AddHours(30)), token);
        await RunJobAsync(token);
        var shifts = (await ShiftsAsync(id, token)).OrderBy(shift => shift.FromUtc).ToList();
        using var controller = await SignedInAsync(ControllerVid, token);
        using var aod = await SignedInAsync(AodVid, token);

        // Asked to the other controller; asked again, refused.
        var asked = await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[0].Id), new TransferRequest(OtherVid, "evt-test-e12 my note"), token), token);
        Assert.Equal(nameof(TransferStatus.Pending), asked.GetProperty("status").GetString());
        Assert.Equal(
            [MemberShifts.PendingKey],
            (await RefusedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[0].Id), new TransferRequest(null, null), token), token))["id"]);

        // To themselves: refused.
        Assert.Equal(
            [MemberShifts.ToYourselfKey],
            (await RefusedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[1].Id), new TransferRequest(ControllerVid, null), token), token))["toVid"]);

        // The staff's list says it, with the two people.
        var listed = Assert.Single(await PageAsync(aod, $"{TransferEndpoints.StaffPattern}?filter[eventId]={id}", token));
        Assert.Equal((ControllerVid, OtherVid), (listed.GetProperty("from").GetProperty("vid").GetInt32(), listed.GetProperty("to").GetProperty("vid").GetInt32()));

        // Approved: the shift is the other's, the staff's, without the why; whoever asked is told, and the other has their shift.
        using (var response = await aod.PostAsJsonAsync(DecideUri(asked.GetProperty("id").GetInt64(), "approve"), new TransferApproval(null, null), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var passed = (await ShiftsAsync(id, token)).Single(shift => shift.Id == shifts[0].Id);
        Assert.Equal((OtherVid, ShiftOrigin.Staff, (string?)null), (passed.ControllerVid, passed.Origin, passed.ReasonJson));
        Assert.NotNull(passed.NotifiedAt);
        Assert.Single(await MailsAsync(ControllerVid, EventsNotifications.AtcShiftTransferDecided, token));
        Assert.Single(await MailsAsync(OtherVid, EventsNotifications.AtcShiftAssigned, token));
        Assert.Equal(TransferStatus.Approved, (await TransfersAsync(id, token)).Single().Status);

        // To nobody: approving wants the substitute; refusing wants a note, and tells whoever asked.
        var nobody = await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[1].Id), new TransferRequest(null, null), token), token);
        var nobodyId = nobody.GetProperty("id").GetInt64();
        Assert.Equal(
            [StaffTransfers.SubstituteKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(nobodyId, "approve"), new TransferApproval(null, null), token), token))["toVid"]);
        Assert.Equal(
            ["errors.required"],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(nobodyId, "refuse"), new TransferRefusal(null), token), token))["note"]);
        using (var response = await aod.PostAsJsonAsync(DecideUri(nobodyId, "refuse"), new TransferRefusal("evt-test-e12 nobody free"), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(2, (await MailsAsync(ControllerVid, EventsNotifications.AtcShiftTransferDecided, token)).Count);
        Assert.Equal(ControllerVid, (await ShiftsAsync(id, token)).Single(shift => shift.Id == shifts[1].Id).ControllerVid);

        // The controller reads the refusal under their shift, and may ask again.
        var mine = (await ListAsync(await controller.GetAsync(TransferEndpoints.MinePattern, token), token)).Single(shift => shift.GetProperty("id").GetInt64() == shifts[1].Id);
        Assert.Equal(nameof(TransferStatus.Refused), mine.GetProperty("transfer").GetProperty("status").GetString());
        Assert.True(mine.GetProperty("transferable").GetBoolean());
    }

    /// <summary>
    /// Nobody approves their own request, the super administrator included (<c>DeniedToStakeholder</c>): the coordinator gives the super
    /// administrator a shift, the super administrator asks to hand it over, and approving it is refused to them; nor do the staff give a shift to
    /// themselves through a request; and from the event's start a request is no longer approved, and is still refused.
    /// </summary>
    [Fact]
    public async Task NobodyApprovesTheirOwnRequestAndNothingPassesFromTheStart()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddHours(30));
        var id = await ProposableAsync("own", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);

        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var theirs = await CreatedAsync(
            await coordinator.PostAsJsonAsync(ShiftEndpoints.Pattern, new EventAtcShiftWriteDto(positions[Approach], SuperadminVid, starts.AddHours(1), starts.AddHours(2), default), token),
            token);
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var asked = await CreatedAsync(await superadmin.PostAsJsonAsync(TransferUri(theirs.GetProperty("id").GetInt64()), new TransferRequest(OtherVid, null), token), token);
        using (var response = await superadmin.PostAsJsonAsync(DecideUri(asked.GetProperty("id").GetInt64(), "approve"), new TransferApproval(null, null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // The ATC operations giving the controller's shift to themselves: refused on the shift as it would be.
        var shift = (await ShiftsAsync(id, token)).First(candidate => candidate.ControllerVid == ControllerVid);
        using var controller = await SignedInAsync(ControllerVid, token);
        var toTheStaff = await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shift.Id), new TransferRequest(AodVid, null), token), token);
        using var aod = await SignedInAsync(AodVid, token);
        using (var response = await aod.PostAsJsonAsync(DecideUri(toTheStaff.GetProperty("id").GetInt64(), "approve"), new TransferApproval(null, null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // An event in progress: a request waiting is not approved, and is refused.
        var now = DateTime.UtcNow;
        var running = await EventRowAsync("own-running", now.AddHours(-1), now.AddHours(5), PublishStatus.Published, proposed: true, token);
        var position = await PositionRowAsync(running, Tower, now.AddHours(-1), now.AddHours(5), token);
        var held = await ShiftRowAsync(running, position, Fir, ControllerVid, now.AddHours(-1), now, token);
        var waiting = await TransferRowAsync(running, held, Fir, ControllerVid, OtherVid, token);
        Assert.Equal(
            [StaffTransfers.AfterStartKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(waiting, "approve"), new TransferApproval(null, null), token), token))["id"]);
        using (var response = await aod.PostAsJsonAsync(DecideUri(waiting, "refuse"), new TransferRefusal("evt-test-e12 too late"), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        // Nor is one asked: the shift is not handed over once the event started.
        Assert.Equal(
            [MemberShifts.ClosedKey],
            (await RefusedAsync(await controller.PostAsJsonAsync(TransferUri(held), new TransferRequest(OtherVid, null), token), token))["id"]);
    }

    /// <summary>
    /// The page of the roster (reading 5): before the publication only when it comes; after it, the shifts and the uncovered pieces — the names to
    /// a signed in reader, none to a visitor.
    /// </summary>
    [Fact]
    public async Task ThePageOfTheRosterNamesNobodyToAVisitor()
    {
        var token = TestContext.Current.CancellationToken;
        var due = await ProposableAsync("page", Hour(DateTime.UtcNow.AddHours(30)), token);
        await ProposableAsync("page-waiting", Hour(DateTime.UtcNow.AddHours(60)), token);
        await RunJobAsync(token);

        using var visitor = _factory.CreateClient();
        var before = await OkAsync(await visitor.GetAsync(RosterUri("page-waiting"), token), token);
        Assert.False(before.GetProperty("published").GetBoolean());
        Assert.Equal(0, before.GetProperty("rows").GetArrayLength());

        var page = await OkAsync(await visitor.GetAsync(RosterUri("page"), token), token);
        Assert.True(page.GetProperty("published").GetBoolean());
        var rows = page.GetProperty("rows").EnumerateArray().ToList();
        Assert.Contains(rows, row => !row.GetProperty("covered").GetBoolean());
        Assert.Contains(rows, row => row.GetProperty("covered").GetBoolean());
        Assert.All(rows, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("controllerName").ValueKind));
        Assert.All(rows, row => Assert.False(row.GetProperty("controllerErased").GetBoolean()));
        Assert.All(rows, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("rating").ValueKind));
        Assert.DoesNotContain(ControllerVid.ToString(System.Globalization.CultureInfo.InvariantCulture), page.GetRawText(), StringComparison.Ordinal);

        using var member = await SignedInAsync(OtherVid, token);
        // A signed in member reads the controller's name and rating, and never the VID (Carmine's answer on #268, reading 5).
        var named = await OkAsync(await member.GetAsync(RosterUri("page"), token), token);
        Assert.Contains(
            named.GetProperty("rows").EnumerateArray(),
            row => row.GetProperty("covered").GetBoolean() && row.GetProperty("controllerName").GetString() == "Test Publication");
        Assert.DoesNotContain(ControllerVid.ToString(System.Globalization.CultureInfo.InvariantCulture), named.GetRawText(), StringComparison.Ordinal);

        // The event's page links to it.
        var eventPage = await OkAsync(await visitor.GetAsync($"{PublicEventEndpoints.Pattern}/{Slug("page")}", token), token);
        Assert.True(eventPage.GetProperty("atc").GetProperty("rosterPublished").GetBoolean());
        Assert.Equal(due, eventPage.GetProperty("id").GetInt64());
    }

    /// <summary>
    /// The block of the coverage (reading 8): to the ATC operations the evening with its uncovered hour and its request waiting; to the chief of
    /// the tower's FIR the tower's pieces alone; to a visitor nothing.
    /// </summary>
    [Fact]
    public async Task TheBlockOfTheCoverageCountsWhatTheReaderReads()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await ProposableAsync("coverage", Hour(DateTime.UtcNow.AddHours(30)), token);
        await RunJobAsync(token);
        var shift = (await ShiftsAsync(id, token)).First(candidate => candidate.ControllerVid == ControllerVid);
        using var controller = await SignedInAsync(ControllerVid, token);
        await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shift.Id), new TransferRequest(null, null), token), token);

        using var aod = await SignedInAsync(AodVid, token);
        var all = Coverage(await OkAsync(await aod.GetAsync(BlockUri(AtcCoverageProvider.BlockType, """{"days":14}"""), token), token), id);
        // The tower three hours, the approach two: five pieces; the controller held two of the tower, so three are uncovered.
        Assert.Equal((nameof(RosterStage.Published), 5, 3, 1), (all.GetProperty("stage").GetString(), all.GetProperty("pieces").GetInt32(), all.GetProperty("uncovered").GetInt32(), all.GetProperty("transfers").GetInt32()));

        using var chief = await SignedInAsync(ChiefVid, token);
        var theirs = Coverage(await OkAsync(await chief.GetAsync(BlockUri(AtcCoverageProvider.BlockType, """{"days":14}"""), token), token), id);
        Assert.Equal((3, 1, 1), (theirs.GetProperty("pieces").GetInt32(), theirs.GetProperty("uncovered").GetInt32(), theirs.GetProperty("transfers").GetInt32()));

        using var visitor = _factory.CreateClient();
        Assert.False((await OkAsync(await visitor.GetAsync(BlockUri(AtcCoverageProvider.BlockType, "{}"), token), token)).GetProperty("signedIn").GetBoolean());
    }

    /// <summary>
    /// Cancelling an event tells whoever has a shift in it, with whoever booked or registered (reading 4).
    /// </summary>
    [Fact]
    public async Task WhoeverHasAShiftIsToldOfACancellation()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await ProposableAsync("cancel", Hour(DateTime.UtcNow.AddHours(60)), token);
        await RunJobAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var told = await scope.ServiceProvider.GetRequiredService<EventSaving>().PeopleToTellAsync(id, token);
        Assert.Contains(ControllerVid, told);
    }

    /// <summary>
    /// Erasing a controller's data (reading 9): their request of an event still to end goes; the one of an event over stays under the pseudonym,
    /// without the two notes.
    /// </summary>
    [Fact]
    public async Task AnErasedControllerLeavesTheRequestsOfEventsOverWithoutTheNotes()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var toCome = await EventRowAsync("erase-to-come", now.AddDays(10), now.AddDays(10).AddHours(6), PublishStatus.Published, proposed: true, token);
        var over = await EventRowAsync("erase-over", now.AddDays(-10), now.AddDays(-10).AddHours(6), PublishStatus.Published, proposed: true, token);
        var toComePosition = await PositionRowAsync(toCome, Tower, now.AddDays(10), now.AddDays(10).AddHours(6), token);
        var overPosition = await PositionRowAsync(over, Tower, now.AddDays(-10), now.AddDays(-10).AddHours(6), token);
        var toComeShift = await ShiftRowAsync(toCome, toComePosition, Fir, PersonVid, now.AddDays(10), now.AddDays(10).AddHours(1), token);
        var overShift = await ShiftRowAsync(over, overPosition, Fir, OtherVid, now.AddDays(-10), now.AddDays(-10).AddHours(1), token);
        await TransferRowAsync(toCome, toComeShift, Fir, PersonVid, OtherVid, token);
        var kept = await TransferRowAsync(over, overShift, Fir, PersonVid, OtherVid, token);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        (string Key, int Count, string Outcome)[] lines =
        [
            ("events:erasure.transfersToCome", 1, nameof(ErasureOutcome.Deleted)),
            ("events:erasure.transfersOver", 1, nameof(ErasureOutcome.Anonymised)),
        ];
        Assert.Equal(lines, TransferLines(await OkAsync(await superadmin.GetAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", token), token)));

        var erased = await OkAsync(await superadmin.PostAsync($"{ErasureEndpoints.Pattern}/{PersonVid}", content: null, token), token);
        Assert.Equal(lines, TransferLines(erased));
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();

        Assert.Empty(await TransfersAsync(toCome, token));
        Assert.Empty(await ShiftsAsync(toCome, token));
        var stored = Assert.Single(await TransfersAsync(over, token));
        Assert.Equal((kept, pseudonym, (string?)null, (string?)null), (stored.Id, stored.ControllerVid, stored.Note, stored.DecisionNote));
    }

    /// <summary>
    /// The refusals of «Approve» (the review of #268, point 3 and small 4): a member busy on another position of the event at those hours, a
    /// shift the staff gave to somebody else meanwhile, a member who never signed in, an erased person's pseudonym, a cancelled event; and the
    /// warnings on the holder the staff choose, before they approve (Carmine's answer on #268), and the «decided» mail naming who took the shift.
    /// </summary>
    [Fact]
    public async Task ApproveRefusesWhatCannotBeAndNamesWhoTookTheShift()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddHours(30));
        var id = await ProposableAsync("refusals", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);
        var shifts = (await ShiftsAsync(id, token)).OrderBy(shift => shift.FromUtc).ToList();
        using var controller = await SignedInAsync(ControllerVid, token);
        using var aod = await SignedInAsync(AodVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        var asked = (await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[0].Id), new TransferRequest(null, null), token), token)).GetProperty("id").GetInt64();

        // Busy: the other controller holds the approach at the same hour.
        await CreatedAsync(
            await coordinator.PostAsJsonAsync(ShiftEndpoints.Pattern, new EventAtcShiftWriteDto(positions[Approach], OtherVid, starts, starts.AddHours(1), default), token),
            token);
        Assert.Equal(
            [StaffTransfers.BusyKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(asked, "approve"), new TransferApproval(OtherVid, null), token), token))["toVid"]);

        // Never signed in, and an erased person's pseudonym: nobody the hub can tell.
        Assert.Equal(
            [MemberShifts.NeverSignedInKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(asked, "approve"), new TransferApproval(761199, null), token), token))["toVid"]);
        Assert.Equal(
            [MemberShifts.NeverSignedInKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(asked, "approve"), new TransferApproval(-761124, null), token), token))["toVid"]);

        // The warnings on the holder the staff choose: the coordinator gave no availability.
        var warnings = await OkAsync(await aod.GetAsync($"{TransferEndpoints.StaffPattern}/{asked}/warnings?toVid={CoordinatorVid}", token), token);
        Assert.Contains(nameof(ShiftWarning.NotAvailable), warnings.EnumerateArray().Select(warning => warning.GetString()));

        // Approved towards the coordinator: the mail to whoever asked names who took it.
        using (var response = await aod.PostAsJsonAsync(DecideUri(asked, "approve"), new TransferApproval(CoordinatorVid, null), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var decided = Assert.Single(await MailsAsync(ControllerVid, EventsNotifications.AtcShiftTransferDecided, token));
        Assert.Contains($"Test Publication ({CoordinatorVid})", decided.DataJson, StringComparison.Ordinal);

        // Not theirs any more: the staff moved the second shift to the other controller before deciding.
        var second = await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[1].Id), new TransferRequest(null, null), token), token);
        var stored = (await ShiftsAsync(id, token)).Single(shift => shift.Id == shifts[1].Id);
        await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{ShiftEndpoints.Pattern}/{stored.Id}",
                new EventAtcShiftWriteDto(stored.PositionId, OtherVid, stored.FromUtc, stored.ToUtc, stored.RowVersion),
                token),
            token);
        Assert.Equal(
            [StaffTransfers.NotTheirsKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(second.GetProperty("id").GetInt64(), "approve"), new TransferApproval(CoordinatorVid, null), token), token))["id"]);

        // A cancelled event hands nothing over: its request is refused only.
        var cancelled = await EventRowAsync("refusals-cancelled", starts, starts.AddHours(6), PublishStatus.Published, proposed: true, token);
        var tower = await PositionRowAsync(cancelled, Tower, starts, starts.AddHours(3), token);
        var held = await ShiftRowAsync(cancelled, tower, Fir, ControllerVid, starts, starts.AddHours(1), token);
        var waiting = await TransferRowAsync(cancelled, held, Fir, ControllerVid, OtherVid, token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().Where(row => row.Id == cancelled)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.CancelledAt, DateTime.UtcNow), token);
        }

        Assert.Equal(
            [StaffTransfers.CancelledKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(waiting, "approve"), new TransferApproval(null, null), token), token))["id"]);
    }

    /// <summary>
    /// Taking a request back (the review of #268, point 3): a waiting one becomes «withdrawn», and approving it after is refused; one approved
    /// is not taken back.
    /// </summary>
    [Fact]
    public async Task AWithdrawnRequestIsNotApprovedAndAnApprovedOneIsNotWithdrawn()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await ProposableAsync("withdraw", Hour(DateTime.UtcNow.AddHours(30)), token);
        await RunJobAsync(token);
        var shifts = (await ShiftsAsync(id, token)).OrderBy(shift => shift.FromUtc).ToList();
        using var controller = await SignedInAsync(ControllerVid, token);
        using var aod = await SignedInAsync(AodVid, token);

        var asked = (await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[0].Id), new TransferRequest(OtherVid, null), token), token)).GetProperty("id").GetInt64();
        using (var response = await controller.DeleteAsync(TransferUri(shifts[0].Id), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        Assert.Equal(TransferStatus.Withdrawn, (await TransfersAsync(id, token)).Single(transfer => transfer.Id == asked).Status);
        Assert.Equal(
            [StaffTransfers.NotPendingKey],
            (await RefusedAsync(await aod.PostAsJsonAsync(DecideUri(asked, "approve"), new TransferApproval(null, null), token), token))["id"]);

        var again = (await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shifts[0].Id), new TransferRequest(OtherVid, null), token), token)).GetProperty("id").GetInt64();
        using (var response = await aod.PostAsJsonAsync(DecideUri(again, "approve"), new TransferApproval(null, null), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using (var response = await controller.DeleteAsync(TransferUri(shifts[0].Id), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Equal(TransferStatus.Approved, (await TransfersAsync(id, token)).Single(transfer => transfer.Id == again).Status);
    }

    /// <summary>
    /// The FIR of a request (the review of #268, point 3 and small 5): the chief of the tower's FIR decides the tower's requests and not the
    /// approach's; a request follows its shift when the staff move it to the other FIR's position.
    /// </summary>
    [Fact]
    public async Task ARequestIsDecidedByTheTeamOfItsShiftsFirAndFollowsTheShift()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddHours(30));
        var id = await ProposableAsync("fir", starts, token);
        await RunJobAsync(token);
        var positions = await PositionIdsAsync(id, token);
        var shift = (await ShiftsAsync(id, token)).OrderBy(candidate => candidate.FromUtc).First();
        using var controller = await SignedInAsync(ControllerVid, token);
        using var chief = await SignedInAsync(ChiefVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        var asked = (await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shift.Id), new TransferRequest(OtherVid, null), token), token)).GetProperty("id").GetInt64();
        Assert.Equal(Fir, (await TransfersAsync(id, token)).Single().Fir);

        // The shift moved to the approach, the other FIR's: the request goes with it.
        await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{ShiftEndpoints.Pattern}/{shift.Id}",
                new EventAtcShiftWriteDto(positions[Approach], ControllerVid, shift.FromUtc, shift.ToUtc, shift.RowVersion),
                token),
            token);
        Assert.Equal(OtherFir, (await TransfersAsync(id, token)).Single().Fir);

        // The chief of the tower's FIR no longer decides it, nor reads it in the list.
        using (var response = await chief.PostAsJsonAsync(DecideUri(asked, "approve"), new TransferApproval(null, null), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Empty(await PageAsync(chief, $"{TransferEndpoints.StaffPattern}?filter[eventId]={id}", token));
    }

    /// <summary>
    /// A shift given to another controller after the publication (reading 3): whoever held it is told it is gone, the new one that it is theirs.
    /// And the race of the review of #268, point 1, deterministic: the shift loaded by a correction before the job told its controller still
    /// reads them as told under the lock, so the controller who held it is told it is gone.
    /// </summary>
    [Fact]
    public async Task AShiftGivenToAnotherAfterThePublicationTellsBothEvenAfterTheJobRaced()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await ProposableAsync("handed", Hour(DateTime.UtcNow.AddHours(30)), token);
        await RunJobAsync(token);
        var shift = (await ShiftsAsync(id, token)).OrderBy(candidate => candidate.FromUtc).First();
        using var aod = await SignedInAsync(AodVid, token);

        await OkAsync(
            await aod.PutAsJsonAsync(
                $"{ShiftEndpoints.Pattern}/{shift.Id}",
                new EventAtcShiftWriteDto(shift.PositionId, OtherVid, shift.FromUtc, shift.ToUtc, shift.RowVersion),
                token),
            token);
        Assert.Single(await MailsAsync(ControllerVid, EventsNotifications.AtcShiftRemoved, token));
        Assert.Single(await MailsAsync(OtherVid, EventsNotifications.AtcShiftAssigned, token));

        // The race: a shift added after the publication, not told yet; a correction loads it, the job tells it meanwhile, then the correction
        // gives it to another controller. Under the lock the correction reads the mark again: whoever held it hears it is gone.
        var second = (await ShiftsAsync(id, token)).OrderBy(candidate => candidate.FromUtc).Skip(1).First();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcShifts.IgnoreQueryFilters().Where(row => row.Id == second.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.NotifiedAt, (DateTime?)null), token);
        }

        await using (var correction = _factory.Services.CreateAsyncScope())
        {
            var database = correction.ServiceProvider.GetRequiredService<EventsDbContext>();
            var loaded = await database.AtcShifts.IgnoreQueryFilters().SingleAsync(row => row.Id == second.Id, token);
            Assert.Null(loaded.NotifiedAt);

            // The job tells the controller while the correction holds its stale row.
            await RunJobAsync(token);
            Assert.Equal(2, (await MailsAsync(ControllerVid, EventsNotifications.AtcShiftAssigned, token)).Count);

            // Then the correction takes the event's lock, as its save does, and reads the mark again inside it.
            var notices = correction.ServiceProvider.GetRequiredService<ShiftNotices>();
            var shiftLock = correction.ServiceProvider.GetRequiredService<ShiftLock>();
            await shiftLock.TakeAsync(database, loaded.EventId, token);
            loaded.ControllerVid = OtherVid;
            await notices.NoteAsync(loaded, isNew: false, token);
            await database.Database.ExecuteSqlAsync($"UPDATE evt_atc_shifts SET controller_vid = {OtherVid}, notified_at = NULL WHERE id = {loaded.Id}", token);
            await shiftLock.CommitAsync(token);
            await notices.TellAsync(loaded, token);
        }

        Assert.Equal(2, (await MailsAsync(ControllerVid, EventsNotifications.AtcShiftRemoved, token)).Count);
    }

    /// <summary>
    /// Taking a shift away races the job too (the second reading of #268, point 1): the shift loaded by the deletion before the job told its
    /// controller is read again under the lock, and its controller is told it is gone.
    /// </summary>
    [Fact]
    public async Task AShiftTakenAwayWhileTheJobTellsItStillTellsItsControllerItIsGone()
    {
        var token = TestContext.Current.CancellationToken;
        var id = await ProposableAsync("removed-race", Hour(DateTime.UtcNow.AddHours(30)), token);
        await RunJobAsync(token);
        var shift = (await ShiftsAsync(id, token)).OrderBy(candidate => candidate.FromUtc).First();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcShifts.IgnoreQueryFilters().Where(row => row.Id == shift.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.NotifiedAt, (DateTime?)null), token);
        }

        await using (var deletion = _factory.Services.CreateAsyncScope())
        {
            var database = deletion.ServiceProvider.GetRequiredService<EventsDbContext>();
            var loaded = await database.AtcShifts.IgnoreQueryFilters().SingleAsync(row => row.Id == shift.Id, token);
            Assert.Null(loaded.NotifiedAt);

            // The job tells the controller while the deletion holds its stale row; then the deletion takes the lock and removes it.
            await RunJobAsync(token);
            Assert.Equal(2, (await MailsAsync(ControllerVid, EventsNotifications.AtcShiftAssigned, token)).Count);

            var shiftLock = deletion.ServiceProvider.GetRequiredService<ShiftLock>();
            await shiftLock.TakeAsync(database, loaded.EventId, token);
            await deletion.ServiceProvider.GetRequiredService<ShiftNotices>().RemoveAsync([loaded], position: null, shiftLock, token);
        }

        Assert.DoesNotContain(await ShiftsAsync(id, token), candidate => candidate.Id == shift.Id);
        Assert.Single(await MailsAsync(ControllerVid, EventsNotifications.AtcShiftRemoved, token));
    }

    /// <summary>
    /// The rating warnings on the holder the staff choose (the second reading of #268, small 2): a member with no ATC rating is below the
    /// setting's least and below the tower's preferred one.
    /// </summary>
    [Fact]
    public async Task TheWarningsOnTheHolderChosenSayTheRatings()
    {
        var token = TestContext.Current.CancellationToken;
        var ladder = IvaoRatings.Vocabulary.Ladder(RatingKind.Atc);
        await WriteSettingsAsync(
            $$"""{"minimumAtcRating":{{ladder[2].Number}},"applicationsCloseDays":3,"rosterPublishDays":2,"shiftMinutes":60,"maxConsecutiveShifts":2,"breakShifts":1}""",
            token);
        var id = await ProposableAsync("ratings", Hour(DateTime.UtcNow.AddHours(30)), token);
        await RunJobAsync(token);
        var shift = (await ShiftsAsync(id, token)).OrderBy(candidate => candidate.FromUtc).First();
        using var controller = await SignedInAsync(ControllerVid, token);
        var asked = (await CreatedAsync(await controller.PostAsJsonAsync(TransferUri(shift.Id), new TransferRequest(null, null), token), token)).GetProperty("id").GetInt64();

        using var aod = await SignedInAsync(AodVid, token);
        var warnings = (await OkAsync(await aod.GetAsync($"{TransferEndpoints.StaffPattern}/{asked}/warnings?toVid={ChiefVid}", token), token))
            .EnumerateArray().Select(warning => warning.GetString()).ToList();
        Assert.Contains(nameof(ShiftWarning.BelowMinimum), warnings);
        Assert.Contains(nameof(ShiftWarning.BelowPreferred), warnings);
    }

    /// <summary>
    /// An erased controller on the public roster (the second reading of #268, small 2): to a signed in reader, marked erased, with no name and
    /// no VID — the page says the core's word.
    /// </summary>
    [Fact]
    public async Task AnErasedControllerOnThePublicRosterIsNamedByNobody()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddHours(30));
        var id = await EventRowAsync("erased-page", starts, starts.AddHours(6), PublishStatus.Published, proposed: true, token);
        var tower = await PositionRowAsync(id, Tower, starts, starts.AddHours(1), token);
        await ShiftRowAsync(id, tower, Fir, -761188, starts, starts.AddHours(1), token);

        using var member = await SignedInAsync(OtherVid, token);
        var page = await OkAsync(await member.GetAsync(RosterUri("erased-page"), token), token);
        var row = Assert.Single(page.GetProperty("rows").EnumerateArray(), candidate => candidate.GetProperty("covered").GetBoolean());
        Assert.True(row.GetProperty("controllerErased").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("controllerName").ValueKind);
        Assert.DoesNotContain("761188", page.GetRawText(), StringComparison.Ordinal);
    }

    // ---- requests -------------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>An instant on the hour: the shifts of an evening start where the positions start.</summary>
    private static DateTime Hour(DateTime instant) =>
        DateTime.SpecifyKind(new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, 0, 0), DateTimeKind.Utc);

    private static string TransferUri(long shiftId) => $"{TransferEndpoints.MinePattern}/{shiftId}/transfer";

    private static string DecideUri(long transferId, string verb) => $"{TransferEndpoints.StaffPattern}/{transferId}/{verb}";

    private static string RosterUri(string name) => $"{PublicEventEndpoints.Pattern}/{Slug(name)}/roster";

    private static Uri BlockUri(string type, string props)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return new Uri($"/api/blocks/data/{type}?props={encoded}", UriKind.Relative);
    }

    private static JsonElement Coverage(JsonElement answer, long eventId)
    {
        Assert.True(answer.GetProperty("signedIn").GetBoolean());
        return Assert.Single(answer.GetProperty("events").EnumerateArray(), entry => entry.GetProperty("id").GetInt64() == eventId);
    }

    private async Task RunJobAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventRosterJob>().RunAsync(cancellationToken);
    }

    private static (string Key, int Count, string Outcome)[] TransferLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{EventsModule.ModuleKey}:erasure.transfers", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

    private async Task<List<Notification>> MailsAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == type && row.Vid == vid && row.DataJson.Contains(SlugStem))
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task ForgetMailsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .Where(row => People.Contains(row.Vid) && row.DataJson.Contains(SlugStem))
            .ExecuteDeleteAsync(cancellationToken);
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

    private static async Task<List<JsonElement>> ListAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        [.. (await OkAsync(response, cancellationToken)).EnumerateArray()];

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

    /// <summary>
    /// An event of the class with a roster starting at <paramref name="starts"/>, for six hours: the tower open three hours, the approach two,
    /// and the controller available on the tower's first two hours alone — so the proposal gives them the tower twice and leaves the rest
    /// uncovered.
    /// </summary>
    private async Task<long> ProposableAsync(string name, DateTime starts, CancellationToken cancellationToken)
    {
        var id = await EventRowAsync(name, starts, starts.AddHours(6), PublishStatus.Published, proposed: false, cancellationToken);
        await PositionRowAsync(id, Tower, starts, starts.AddHours(3), cancellationToken);
        await PositionRowAsync(id, Approach, starts, starts.AddHours(2), cancellationToken);
        await AvailabilityRowAsync(id, ControllerVid, starts, starts.AddHours(2), cancellationToken);
        return id;
    }

    private async Task<long> EventRowAsync(string name, DateTime starts, DateTime ends, PublishStatus status, bool proposed, CancellationToken cancellationToken)
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
            RosterProposedAt = proposed ? DateTime.UtcNow.AddDays(-5) : null,
            RosterPublishedAt = proposed ? DateTime.UtcNow.AddDays(-4) : null,
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

    private async Task AvailabilityRowAsync(long eventId, int vid, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var given = DateTime.UtcNow.AddDays(-5);
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

    /// <summary>A shift written by the installation, already told.</summary>
    private async Task<long> ShiftRowAsync(long eventId, long positionId, string fir, int vid, DateTime from, DateTime to, CancellationToken cancellationToken)
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
            Origin = ShiftOrigin.Staff,
            NotifiedAt = DateTime.UtcNow.AddDays(-4),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcShifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken);
        return shift.Id;
    }

    /// <summary>A request waiting, written by the installation, with the two notes.</summary>
    private async Task<long> TransferRowAsync(long eventId, long shiftId, string fir, int vid, int? toVid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var transfer = new EventAtcTransfer
        {
            EventId = eventId,
            ShiftId = shiftId,
            ControllerVid = vid,
            ToVid = toVid,
            Fir = fir,
            Note = "evt-test-e12 their note",
            DecisionNote = "evt-test-e12 the staff's note",
            Status = TransferStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.AtcTransfers.Add(transfer);
        await database.SaveChangesAsync(cancellationToken);
        return transfer.Id;
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

    private async Task<List<EventAtcTransfer>> TransfersAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(transfer => transfer.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<string, long>> PositionIdsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().AtcPositions.IgnoreQueryFilters().AsNoTracking()
            .Where(position => position.EventId == eventId)
            .ToDictionaryAsync(position => position.Callsign, position => position.Id, cancellationToken);
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

    private async Task SeedPersonAsync(int vid, string? position, int? rating, string? email, bool superadmin, CancellationToken cancellationToken)
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
        user.LastName = "Publication";
        user.Email = email;
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

    /// <summary>No archive of sessions: the proposal leaves the experience out, as the division does today.</summary>
    private sealed class NoArchive : IAtcActivitySource
    {
        public Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<AtcActivity?>(null);

        public Task<AtcActivity?> SessionsOfAsync(int vid, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<AtcActivity?>(null);
    }
}
