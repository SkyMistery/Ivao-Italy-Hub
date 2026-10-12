using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// «Duplicate» (M4, E8b, design §2.3-bis; note 2026-10-11-duplica-e-la-cancellazione-negli-eventi §1), through the real host with the
/// division file of this repository: a copy is a draft with every time moved by the same difference, the event's settings, its banner,
/// its airports with their capacity, its routes and, when asked, its public slots with their rotations — and nothing of the members;
/// its private slots are generated with it when its public slots are there, and left to «Generate» when a new table is still to come;
/// any event is duplicated, an ended and cancelled one too, and a banner the library no longer has stays behind. Whoever duplicates
/// holds <c>Events.Edit</c> on the new event — never with a grant on one event alone — and the area of every row the copy writes.
/// <para>⚠️ The coordinator is seeded **without an address**, as in <see cref="EventsStaffTests"/>: the tests of the contacts assert who
/// of the events receives a message. The collaborator holds only the grants a test writes, and none after the class.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsDuplicateTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The coordinator of E6b's class (EventsBookingPagesTests), reused (the events module's range is all handed out, CONTRIBUTING.md);
    // the collaborator is one of the two VIDs the merged phases left unused, the person EventsErasureTests erases — no grant of this
    // class outlives it.
    private const int CoordinatorVid = 761063;
    private const int CollaboratorVid = 761098;

    /// <summary>Two airports of the events of this class, and one away from them: of no country the network has.</summary>
    private const string First = "XEJ1";
    private const string Second = "XEJ2";
    private const string Away = "XEJ3";

    /// <summary>Two aircraft types the core knows here.</summary>
    private const string TypeA = "XEJA";
    private const string TypeB = "XEJB";

    private const string SlugStem = "evt-test-e8b-dup";

    /// <summary>The name of the files of the library this class writes, so that it takes them back.</summary>
    private const string MediaStem = "evt-test-e8b";

    private const string Header = "callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg";

    /// <summary>A description the copy carries as it is written.</summary>
    private const string Body = """{"schemaVersion":1,"sections":[],"evtTest":"e8b"}""";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly string[] Airports = [First, Second, Away];

    private static readonly int[] People = [CoordinatorVid, CollaboratorVid];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(CollaboratorVid, position: null, token);
        await SeedReferenceAsync(token);
        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows, the collaborator's grants, its files, airports and types.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => People.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.IvaoAirports.Where(airport => Airports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.IvaoAircraftTypes.Where(type => type.IcaoCode == TypeA || type.IcaoCode == TypeB).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ACopyMovesEveryTimeByTheSameDifferenceIsADraftAndCarriesNothingOfTheMembers()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);
        var seen = Minute(DateTime.UtcNow.AddDays(-2));

        // An RFO of three hours at two airports with their capacity, seen since two days and booked since yesterday.
        var id = await EventAsync(
            coordinator,
            "source",
            publicSlots: true,
            privateSlots: true,
            day.AddHours(17),
            day.AddHours(20),
            token,
            [Capacity(First, arrivals: 2, departures: 2), Capacity(Second, movements: 4)],
            visibleFrom: seen,
            opens: seen.AddDays(1));

        // Its routes, written by whoever writes them; a rotation of two legs between its airports and a flight from away.
        await RouteAsync(coordinator, id, First, Second, "XEJ1 DCT XEJ2", remarks: "evt-test-e8b expect holding", token);
        await RouteAsync(coordinator, id, Away, First, "XEJ3 DCT XEJ1", remarks: null, token);
        await OkAsync(
            await LoadAsync(
                coordinator,
                id,
                Table(
                    Line("XEJ101", $"{TypeA}/{TypeB}", First, day.AddHours(17).AddMinutes(30), Second, day.AddHours(18).AddMinutes(10), stand: "B1", rotation: "R1", leg: 1),
                    Line("XEJ102", TypeA, Second, day.AddHours(18).AddMinutes(30), First, day.AddHours(19).AddMinutes(10), stand: null, rotation: "R1", leg: 2),
                    Line("XEJ103", TypeB, Away, day.AddHours(18), First, day.AddHours(18).AddMinutes(50), stand: null, rotation: null, leg: null)),
                token),
            token);
        await OkAsync(await GenerateAsync(coordinator, id, token), token);

        // Its banner and its description, as the library and the editor left them; then published, and booked by a pilot: a public slot,
        // and a private arrival with its linked departure.
        var banner = await MediaAsync(deleted: false, token);
        await WriteEventAsync(id, row => (row.BannerMediaId, row.BodyJson) = (banner, Body), token);
        await PublishAsync(coordinator, id, token);
        await BookAsync(id, token);

        // Thirty days later, two hours and a quarter earlier in the day.
        var newStart = day.AddDays(30).AddHours(15).AddMinutes(-15);
        var difference = newStart - day.AddHours(17);
        var (copied, notGenerated) = await CopiedAsync(await DuplicateAsync(coordinator, id, new EventDuplicateRequest(newStart, $"  {Slug("copy").ToUpperInvariant()}  ", PublicSlots: true), token), token);
        var copyId = copied.GetProperty("id").GetInt64();
        Assert.Equal((nameof(EventStateKind.Draft), Slug("copy"), (string?)null), (copied.GetProperty("state").GetString(), copied.GetProperty("slug").GetString(), notGenerated));

        var (source, copy) = (await EventRowAsync(id, token), await EventRowAsync(copyId, token));

        // Its settings, its words, its banner, in the same care; a draft, never published, not cancelled.
        Assert.Equal(
            (source.Kind, source.PublicSlots, source.PrivateSlots, source.WholeDivision, source.Organizer, source.Visibility),
            (copy.Kind, copy.PublicSlots, copy.PrivateSlots, copy.WholeDivision, copy.Organizer, copy.Visibility));
        Assert.Equal((source.Title.Get("en"), source.Summary.Get("it"), Body), (copy.Title.Get("en"), copy.Summary.Get("it"), copy.BodyJson));
        Assert.Equal(((long?)banner, source.OwnerDepartment, source.OwnerDepartmentMask), (copy.BannerMediaId, copy.OwnerDepartment, copy.OwnerDepartmentMask));
        Assert.Equal((PublishStatus.Draft, (DateTime?)null, (DateTime?)null, CoordinatorVid), (copy.Status, copy.PublishedAt, copy.CancelledAt, copy.CreatedBy));

        // Every time, by the same difference.
        Assert.Equal(
            (newStart, source.EndsAtUtc + difference, source.VisibleFromUtc + difference, source.BookingOpensAtUtc + difference),
            (copy.StartsAtUtc, copy.EndsAtUtc, copy.VisibleFromUtc, copy.BookingOpensAtUtc));

        // Its airports with their capacity, and its routes, as they were.
        Assert.Equal(await AirportsAsync(id, token), await AirportsAsync(copyId, token));
        Assert.Equal(await RoutesAsync(id, token), await RoutesAsync(copyId, token));

        // Its public slots with their rotations, at the new times; its private slots generated around them, the source's moved.
        Assert.Equal(
            (await SlotsAsync(id, SlotKind.Public, token)).Select(slot => Flight(slot, difference)),
            (await SlotsAsync(copyId, SlotKind.Public, token)).Select(slot => Flight(slot, TimeSpan.Zero)));
        var privateSlots = await SlotsAsync(copyId, SlotKind.Private, token);
        Assert.NotEmpty(privateSlots);
        Assert.All(privateSlots, slot => Assert.True(slot.Generated));
        Assert.Equal(Places(await SlotsAsync(id, SlotKind.Private, token), difference), Places(privateSlots, TimeSpan.Zero));

        // Nothing of the members; the source keeps its own.
        Assert.Empty(await BookingsAsync(copyId, token));
        Assert.Equal(3, (await BookingsAsync(id, token)).Count);

        // A draft is in no calendar, and keeps its banner for as long as it is one.
        await using var read = _factory.Services.CreateAsyncScope();
        var hub = read.ServiceProvider.GetRequiredService<HubDbContext>();
        var sourceId = $"event:{copyId}";
        Assert.False(await hub.CalendarEntries.IgnoreQueryFilters().AnyAsync(row => row.SourceModule == EventsModule.ModuleKey && row.SourceId == sourceId, token));
        var use = Assert.Single(await hub.MediaUses.Where(row => row.SourceModule == EventsModule.ModuleKey && row.SourceId == sourceId).ToListAsync(token));
        Assert.Equal((banner, (DateTime?)null), (use.MediaId, use.UsedUntil));
    }

    [Fact]
    public async Task WithoutItsPublicSlotsACopyLeavesThePrivateOnesToGenerateAndOneThatTakesNoneHasThemAtOnce()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);

        // An RFO copied without its public slots: the new table comes first, so no private slot either — «Generate» makes them.
        var rfo = await EventAsync(coordinator, "rfo", publicSlots: true, privateSlots: true, day.AddHours(17), day.AddHours(20), token, [Capacity(First, arrivals: 2, departures: 2)]);
        await OkAsync(await LoadAsync(coordinator, rfo, Table(Line("XEJ201", TypeA, First, day.AddHours(17).AddMinutes(30), Away, day.AddHours(18).AddMinutes(30), stand: null, rotation: null, leg: null)), token), token);
        await OkAsync(await GenerateAsync(coordinator, rfo, token), token);

        var (rfoCopy, rfoNotGenerated) = await CopiedAsync(await DuplicateAsync(coordinator, rfo, new EventDuplicateRequest(day.AddDays(7).AddHours(17), Slug("rfo-copy"), PublicSlots: false), token), token);
        var copy = rfoCopy.GetProperty("id").GetInt64();
        Assert.Null(rfoNotGenerated);
        Assert.Empty(await SlotsAsync(copy, kind: null, token));
        Assert.Equal(await AirportsAsync(rfo, token), await AirportsAsync(copy, token));
        Assert.Equal(12, (await OkAsync(await GenerateAsync(coordinator, copy, token), token)).GetProperty("generated").GetInt32());

        // An event that takes no public slot — an MSE, as a division presets it —, last month's, published, cancelled and over, its
        // banner deleted since: copied onto a new date, a draft with its private slots at once, and no banner.
        var monthAgo = Day(days: -30);
        var mse = await EventAsync(coordinator, "mse", publicSlots: false, privateSlots: true, monthAgo.AddHours(17), monthAgo.AddHours(20), token, [Capacity(First, arrivals: 2)]);
        await OkAsync(await GenerateAsync(coordinator, mse, token), token);
        var gone = await MediaAsync(deleted: true, token);
        await WriteEventAsync(
            mse,
            row => (row.Status, row.PublishedAt, row.CancelledAt, row.CancelledBy, row.CancellationNote, row.BannerMediaId) =
                (PublishStatus.Published, monthAgo.AddDays(-10), monthAgo.AddDays(-1), CoordinatorVid, Text("evt-test-e8b weather"), gone),
            token);

        var (again, mseNotGenerated) = await CopiedAsync(await DuplicateAsync(coordinator, mse, new EventDuplicateRequest(day.AddHours(17), Slug("mse-copy"), PublicSlots: false), token), token);
        var mseCopy = await EventRowAsync(again.GetProperty("id").GetInt64(), token);
        Assert.Null(mseNotGenerated);
        Assert.Equal((PublishStatus.Draft, (DateTime?)null, (int?)null, (long?)null), (mseCopy.Status, mseCopy.CancelledAt, mseCopy.CancelledBy, mseCopy.BannerMediaId));
        Assert.Equal(nameof(EventStateKind.Draft), again.GetProperty("state").GetString());
        Assert.Equal(Places(await SlotsAsync(mse, SlotKind.Private, token), day - monthAgo), Places(await SlotsAsync(mseCopy.Id, SlotKind.Private, token), TimeSpan.Zero));

        // One whose airports say no capacity is copied all the same, without private slots: the answer says why (the review of #249,
        // point 3), and «Generate» on it says the same.
        var none = await EventAsync(coordinator, "none", publicSlots: false, privateSlots: true, day.AddHours(17), day.AddHours(20), token, [Capacity(First)]);
        var (noneCopied, noneNotGenerated) = await CopiedAsync(await DuplicateAsync(coordinator, none, new EventDuplicateRequest(day.AddDays(1).AddHours(17), Slug("none-copy"), PublicSlots: false), token), token);
        var noneCopy = noneCopied.GetProperty("id").GetInt64();
        Assert.Equal("events:errors.noCapacity", noneNotGenerated);
        Assert.Empty(await SlotsAsync(noneCopy, kind: null, token));
        Assert.Equal(["events:errors.noCapacity"], (await RefusedAsync(await GenerateAsync(coordinator, noneCopy, token), token))[PrivateSlotGeneration.Field]);
    }

    [Fact]
    public async Task OnlyWhoeverWritesTheEventAndTheRowsItsCopyCarriesDuplicatesIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var day = Day(days: 20);
        var newStart = day.AddDays(7).AddHours(17);

        // An event with a route and a public slot, and a bare one: no route, no slot.
        var full = await EventAsync(coordinator, "full", publicSlots: true, privateSlots: false, day.AddHours(17), day.AddHours(20), token, [Capacity(First)]);
        await RouteAsync(coordinator, full, First, Second, "XEJ1 DCT XEJ2", remarks: null, token);
        await OkAsync(await LoadAsync(coordinator, full, Table(Line("XEJ301", TypeA, First, day.AddHours(17).AddMinutes(30), Away, day.AddHours(18).AddMinutes(30), stand: null, rotation: null, leg: null)), token), token);
        var bare = await EventAsync(coordinator, "bare", publicSlots: false, privateSlots: false, day.AddHours(17), day.AddHours(20), token, [Capacity(First)]);

        // A member writes no event; a visitor is told to sign in.
        using (var member = await SignedInAsync(CollaboratorVid, token))
        {
            Forbidden(await DuplicateAsync(member, full, new EventDuplicateRequest(newStart, Slug("member"), PublicSlots: true), token));
        }

        using (var anonymous = _factory.CreateApiClient())
        {
            // As the browser sends it: without the header, the guard of the session's writes answers first.
            anonymous.DefaultRequestHeaders.Add("X-Requested-With", "hub");
            using var nobody = await DuplicateAsync(anonymous, full, new EventDuplicateRequest(newStart, Slug("visitor"), PublicSlots: true), token);
            Assert.Equal(HttpStatusCode.Unauthorized, nobody.StatusCode);
        }

        // Events.Edit on that event alone: a new event is never the grant's (E10i).
        await GrantAsync(EventsPermissions.Edit, Event.ScopeOf(full), token);
        using (var oneEvent = await SignedInAsync(CollaboratorVid, token))
        {
            Forbidden(await DuplicateAsync(oneEvent, full, new EventDuplicateRequest(newStart, Slug("one-event"), PublicSlots: false), token));
        }

        // Events.Edit on the events department and nothing else: not an event whose routes the copy would write, nor its slots; a bare one.
        await GrantAsync(EventsPermissions.Edit, scope: null, token);
        using (var department = await SignedInAsync(CollaboratorVid, token))
        {
            Forbidden(await DuplicateAsync(department, full, new EventDuplicateRequest(newStart, Slug("no-routes"), PublicSlots: false), token));
            await CreatedAsync(await DuplicateAsync(department, bare, new EventDuplicateRequest(newStart, Slug("bare-copy"), PublicSlots: false), token), token);
        }

        await GrantAsync(EventsPermissions.RoutesEdit, scope: null, token, keep: true);
        using (var routes = await SignedInAsync(CollaboratorVid, token))
        {
            Forbidden(await DuplicateAsync(routes, full, new EventDuplicateRequest(newStart, Slug("no-slots"), PublicSlots: true), token));
            await CreatedAsync(await DuplicateAsync(routes, full, new EventDuplicateRequest(newStart, Slug("routes-copy"), PublicSlots: false), token), token);
        }

        Assert.False(await EventExistsAsync(Slug("member"), token) || await EventExistsAsync(Slug("one-event"), token)
            || await EventExistsAsync(Slug("no-routes"), token) || await EventExistsAsync(Slug("no-slots"), token));

        // The address: free, of the right shape, never the member's own page; a start; an event that exists.
        Assert.Equal(["events:errors.slugTaken"], (await RefusedAsync(await DuplicateAsync(coordinator, full, new EventDuplicateRequest(newStart, Slug("full"), PublicSlots: true), token), token))["slug"]);
        Assert.Equal(["events:errors.slugReserved"], (await RefusedAsync(await DuplicateAsync(coordinator, full, new EventDuplicateRequest(newStart, "mine", PublicSlots: true), token), token))["slug"]);
        Assert.Equal(["errors.slug.invalid"], (await RefusedAsync(await DuplicateAsync(coordinator, full, new EventDuplicateRequest(newStart, "not an address", PublicSlots: true), token), token))["slug"]);
        Assert.Equal(["errors.required"], (await RefusedAsync(await DuplicateAsync(coordinator, full, new EventDuplicateRequest(null, Slug("no-start"), PublicSlots: true), token), token))["startsAtUtc"]);
        // A start at the edge of the calendar, whose end would fall past it: refused on its field, not a 500 (the review of #249, point 4).
        var edge = new DateTime(9999, 12, 31, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(["events:errors.duplicateOutOfCalendar"], (await RefusedAsync(await DuplicateAsync(coordinator, full, new EventDuplicateRequest(edge, Slug("edge"), PublicSlots: true), token), token))["startsAtUtc"]);
        Assert.False(await EventExistsAsync(Slug("edge"), token));
        using (var missing = await DuplicateAsync(coordinator, 0, new EventDuplicateRequest(newStart, Slug("missing"), PublicSlots: true), token))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>Midnight of a day some days from today, in UTC: the slots are at hours of it.</summary>
    private static DateTime Day(int days) => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(days), DateTimeKind.Utc);

    /// <summary>An instant to the minute.</summary>
    private static DateTime Minute(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, DateTimeKind.Utc);

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    /// <summary>An airport of an event with its capacity: movements, or arrivals and departures, or none.</summary>
    private static (string Icao, int? Movements, int? Arrivals, int? Departures) Capacity(string icao, int? movements = null, int? arrivals = null, int? departures = null) =>
        (icao, movements, arrivals, departures);

    /// <summary>A table as a spreadsheet copies it: the header, then a row per line, separated by tabs.</summary>
    private static string Table(params string[] lines) => string.Join("\r\n", [Header, .. lines]) + "\r\n";

    private static string Line(string callsign, string types, string departure, DateTime offBlock, string arrival, DateTime onBlock, string? stand, string? rotation, int? leg) =>
        string.Join('\t', callsign, string.Empty, types, departure, Moment(offBlock), arrival, Moment(onBlock), stand ?? string.Empty, rotation ?? string.Empty, leg?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private static string Moment(DateTime at) => at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A public slot as a line, its times moved back by <paramref name="difference"/>: the flight it is, wherever it was copied.</summary>
    private static string Flight(EventSlot slot, TimeSpan difference) => string.Create(
        CultureInfo.InvariantCulture,
        $"{slot.Callsign} {slot.FlightNumber} {string.Join('/', slot.AircraftTypes)} {slot.EventAirportIcao} {slot.IsArrival} {slot.DepartureIcao} {slot.OffBlockUtc + difference:O} {slot.ArrivalIcao} {slot.OnBlockUtc + difference:O} {slot.Stand} {slot.RotationCode} {slot.RotationLeg} {slot.Generated}");

    /// <summary>Where private slots are — the airport, the direction and the time there, moved by <paramref name="difference"/> —, in order.</summary>
    private static List<string> Places(IEnumerable<EventSlot> slots, TimeSpan difference) =>
    [
        .. slots
            .Select(slot => string.Create(CultureInfo.InvariantCulture, $"{slot.EventAirportIcao} {(slot.IsArrival ? "arrival" : "departure")} {(slot.IsArrival ? slot.OnBlockUtc : slot.OffBlockUtc) + difference:O}"))
            .Order(StringComparer.Ordinal),
    ];

    private static EventWriteDto Payload(string slug, DateTime starts, DateTime ends, bool publicSlots, bool privateSlots, DateTime? visibleFrom, DateTime? opens) => new(
        Kind: "rfo",
        PublicSlots: publicSlots,
        PrivateSlots: privateSlots,
        WholeDivision: false,
        Organizer: EventOrganizer.Division,
        ExternalUrl: null,
        Title: Text(slug),
        Slug: slug,
        Summary: Text($"{slug} summary"),
        Body: null,
        BannerMediaId: null,
        VisibleFromUtc: visibleFrom,
        BookingOpensAtUtc: opens ?? DateTime.UtcNow.AddDays(-1),
        StartsAtUtc: starts,
        EndsAtUtc: ends,
        Visibility: Visibility.Public,
        RowVersion: default);

    /// <summary>An event of the class with its airports and their capacity, as the staff write it from its page: a draft.</summary>
    private static async Task<long> EventAsync(
        HttpClient coordinator,
        string name,
        bool publicSlots,
        bool privateSlots,
        DateTime starts,
        DateTime ends,
        CancellationToken cancellationToken,
        IReadOnlyList<(string Icao, int? Movements, int? Arrivals, int? Departures)> airports,
        DateTime? visibleFrom = null,
        DateTime? opens = null)
    {
        var id = (await CreatedAsync(
                await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, Payload(Slug(name), starts, ends, publicSlots, privateSlots, visibleFrom, opens), cancellationToken),
                cancellationToken))
            .GetProperty("id").GetInt64();

        var ordinal = 1;
        foreach (var (icao, movements, arrivals, departures) in airports)
        {
            await CreatedAsync(
                await coordinator.PostAsJsonAsync(
                    EventAirportEndpoints.Pattern,
                    new EventAirportWriteDto(id, icao, ordinal++, movements, arrivals, departures, RowVersion: default),
                    cancellationToken),
                cancellationToken);
        }

        return id;
    }

    private static async Task RouteAsync(HttpClient coordinator, long eventId, string departure, string arrival, string route, string? remarks, CancellationToken cancellationToken) =>
        await CreatedAsync(
            await coordinator.PostAsJsonAsync(
                EventRouteEndpoints.Pattern,
                new EventRouteWriteDto(eventId, departure, arrival, route, remarks is null ? null : Text(remarks), RowVersion: default),
                cancellationToken),
            cancellationToken);

    private static async Task PublishAsync(HttpClient coordinator, long id, CancellationToken cancellationToken) =>
        await OkAsync(await coordinator.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), cancellationToken), cancellationToken);

    private static Task<HttpResponseMessage> LoadAsync(HttpClient client, long id, string table, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/slots/load", new SlotLoadRequest(table, SlotLoadMode.Add), cancellationToken);

    private static Task<HttpResponseMessage> GenerateAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        client.PostAsync(new Uri($"{EventEndpoints.Pattern}/{id}/slots/generate", UriKind.Relative), null, cancellationToken);

    private static Task<HttpResponseMessage> DuplicateAsync(HttpClient client, long id, EventDuplicateRequest request, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/duplicate", request, cancellationToken);

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

    /// <summary>A copy made: the new event, and why its private slots were not generated when the generator refused them.</summary>
    private static async Task<(JsonElement Event, string? PrivateSlotsNotGenerated)> CopiedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var answer = await CreatedAsync(response, cancellationToken);
        var why = answer.GetProperty("privateSlotsNotGenerated");
        return (answer.GetProperty("event"), why.ValueKind == JsonValueKind.Null ? null : why.GetString());
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

    private static void Forbidden(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>The keys of a 400, field by field.</summary>
    private static async Task<Dictionary<string, string[]>> RefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject()
                .ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray());
        }
    }

    /// <summary>A row of the event changed as the installation would: what no form of the module writes in this test.</summary>
    private async Task WriteEventAsync(long id, Action<Event> write, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var row = await database.Events.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == id, cancellationToken);
        write(row);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A pilot's bookings of the event, written by the installation: the first public slot to land, and the first private arrival
    /// with the first private departure from its airport at least an hour after it, linked.
    /// </summary>
    private async Task BookAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var row = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == eventId, cancellationToken);
        var slots = await database.Slots.AsNoTracking().Where(slot => slot.EventId == eventId).ToListAsync(cancellationToken);

        var landing = slots.Where(slot => slot is { Kind: SlotKind.Private, IsArrival: true }).OrderBy(slot => slot.OnBlockUtc).First();
        var leaving = slots
            .Where(slot => slot is { Kind: SlotKind.Private, IsArrival: false } && slot.EventAirportIcao == landing.EventAirportIcao && slot.OffBlockUtc >= landing.OnBlockUtc!.Value.AddHours(1))
            .OrderBy(slot => slot.OffBlockUtc)
            .First();
        var publicSlot = slots.Where(slot => slot.Kind == SlotKind.Public).OrderBy(slot => slot.Id).First();

        EventBooking Booking(EventSlot slot) => new()
        {
            EventId = eventId,
            SlotId = slot.Id,
            BookerVid = CollaboratorVid,
            AircraftIcao = TypeA,
            CreatedAt = DateTime.UtcNow,
            OwnerDepartment = row.OwnerDepartment,
            OwnerDepartmentMask = row.OwnerDepartmentMask,
        };

        var departure = Booking(leaving);
        (departure.Callsign, departure.OtherIcao, departure.OtherTimeUtc) = ("XEJ902", Away, leaving.OffBlockUtc!.Value.AddHours(1));
        database.Bookings.AddRange(Booking(publicSlot), departure);
        await database.SaveChangesAsync(cancellationToken);

        var arrival = Booking(landing);
        (arrival.Callsign, arrival.OtherIcao, arrival.OtherTimeUtc, arrival.PairedBookingId) = ("XEJ901", Away, landing.OnBlockUtc!.Value.AddHours(-1), departure.Id);
        database.Bookings.Add(arrival);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A file of the library of the events department, there or deleted since, as the library would keep it; no file on disk.</summary>
    private async Task<long> MediaAsync(bool deleted, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var media = new MediaAsset
        {
            OwnerDepartment = Department.ED,
            Visibility = Visibility.Public,
            FileName = $"{MediaStem}-banner.png",
            StoredName = $"{MediaStem}-{Guid.NewGuid():N}.png",
            ContentType = "image/png",
            ByteSize = 1,
            DeletedAt = deleted ? DateTime.UtcNow.AddDays(-1) : null,
            HasFile = !deleted,
        };

        hub.Media.Add(media);
        await hub.SaveChangesAsync(cancellationToken);
        return media.Id;
    }

    private async Task<Event> EventRowAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<bool> EventExistsAsync(string slug, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
            .AnyAsync(row => row.Slug == slug, cancellationToken);
    }

    /// <summary>The airports of an event with their capacity, in their order, as lines.</summary>
    private async Task<List<string>> AirportsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return
        [
            .. (await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Airports.AsNoTracking()
                    .Where(airport => airport.EventId == eventId)
                    .OrderBy(airport => airport.Ordinal)
                    .ToListAsync(cancellationToken))
                .Select(airport => $"{airport.Icao} {airport.Ordinal} {airport.MaxMovementsPerHour} {airport.MaxArrivalsPerHour} {airport.MaxDeparturesPerHour}"),
        ];
    }

    /// <summary>The routes of an event in the order they were written, as lines.</summary>
    private async Task<List<string>> RoutesAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return
        [
            .. (await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Routes.AsNoTracking()
                    .Where(route => route.EventId == eventId)
                    .OrderBy(route => route.Id)
                    .ToListAsync(cancellationToken))
                .Select(route => $"{route.DepartureIcao} {route.ArrivalIcao} {route.Route} {route.Remarks?.Get("en")} {route.Remarks?.Get("it")}"),
        ];
    }

    /// <summary>The slots of an event of one kind, or of both, by their id.</summary>
    private async Task<List<EventSlot>> SlotsAsync(long eventId, SlotKind? kind, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Slots.AsNoTracking()
            .Where(slot => slot.EventId == eventId && (kind == null || slot.Kind == kind))
            .OrderBy(slot => slot.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The bookings of an event as stored, read past every filter.</summary>
    private async Task<List<EventBooking>> BookingsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(booking => booking.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A permission of the events granted to the collaborator on the events department — on one event alone when a scope is given —,
    /// written straight to the table as the permissions screen or a screen of the module would. The collaborator's other grants go
    /// first, unless <paramref name="keep"/>.
    /// </summary>
    private async Task GrantAsync(string permission, string? scope, CancellationToken cancellationToken, bool keep = false)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        if (!keep)
        {
            await database.UserGrants.Where(grant => grant.Vid == CollaboratorVid).ExecuteDeleteAsync(cancellationToken);
        }

        database.UserGrants.Add(new UserGrant
        {
            Vid = CollaboratorVid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.ED,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = "evt-test-e8b",
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in now: the effective permissions are computed at login, and a grant written signs its holder out.</summary>
    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member with one position of the staff — or none, a member of the division and nothing else —, never with an address.</summary>
    private async Task SeedUserAsync(int vid, string? position, CancellationToken cancellationToken)
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
        user.LastName = "Duplicate";
        user.Email = null;
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The airports and the aircraft types of the class, in the core's snapshots, where a slot's and a route's are checked.</summary>
    private async Task SeedReferenceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var icao in Airports)
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport
                {
                    Icao = icao,
                    Name = $"evt-test {icao}",
                    CountryId = "XX",
                    Latitude = 41.8,
                    Longitude = 12.24,
                    SyncedAt = clock.UtcNow,
                });
            }
        }

        foreach (var code in new[] { TypeA, TypeB })
        {
            if (!await database.IvaoAircraftTypes.AnyAsync(type => type.IcaoCode == code, cancellationToken))
            {
                database.IvaoAircraftTypes.Add(new IvaoAircraftType { IcaoCode = code, Model = "evt-test", Manufacturer = "evt-test", SyncedAt = clock.UtcNow });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows and what they projected, the collaborator's grants and the class's files.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await hub.UserGrants.Where(grant => grant.Vid == CollaboratorVid).ExecuteDeleteAsync(cancellationToken);
        await hub.Media.IgnoreQueryFilters().Where(media => media.FileName.StartsWith(MediaStem)).ExecuteDeleteAsync(cancellationToken);
    }
}
