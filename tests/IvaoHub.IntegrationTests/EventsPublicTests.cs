using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Public;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The public side of the events and their routes (M4, E4), through the real host with the division file of this repository: the
/// flight operations write the routes of an event and not the event; a visitor reads the page of a published event with its airports
/// and its routes until its end, and gets 404 after it, a draft and one not seen yet — the staff of the events read it in every
/// state —; an event for the members is hidden from visitors; the block of the list holds the events seen, to come and in progress,
/// the soonest first, and narrows to the kinds it names.
/// <para>⚠️ Everybody here is seeded **without an address**, as in <see cref="EventsStaffTests"/>: the tests of the contacts assert who
/// of the events, the ATC operations and the flight operations receives a message. What each holds comes from the grants of their
/// position, which the division file gives.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsPublicTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // E4's in the range the events module owns in the shared database (CONTRIBUTING.md): 761033–761038 and 761042–761049.
    private const int CoordinatorVid = 761033;
    private const int FlightOperationsVid = 761034;
    private const int MemberVid = 761035;
    private const int AtcOperationsVid = 761036;

    /// <summary>Two airports of no country the network has: nobody else's airport is touched.</summary>
    private const string First = "XEC1";
    private const string Second = "XEC2";

    private const string SlugStem = "evt-test-e4";

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The recorded answers of the network: a host started without them asks the network for a token while the reference data
        // are empty (E10b's warning in HANDOFF-M4.md), and no test calls the network.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(FlightOperationsVid, "IT-FOAC", token);
        await SeedUserAsync(AtcOperationsVid, "IT-AOA1", token);
        await SeedUserAsync(MemberVid, position: null, token);
        await SeedAirportsAsync(token);
        await ForgetEventsAsync(token);
    }

    /// <summary>What the class seeded is taken back — its events with their airports and routes, its airports, the positions of its VIDs.</summary>
    public async ValueTask DisposeAsync()
    {
        int[] vids = [CoordinatorVid, FlightOperationsVid, MemberVid, AtcOperationsVid];

        await ForgetEventsAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => vids.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.IvaoAirports.Where(airport => airport.Icao == First || airport.Icao == Second).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheFlightOperationsWriteTheRoutesOfAnEventAndNotTheEvent()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var created = await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("routes", Starts(days: 30)), token);
        var id = Id(created);

        using var flightOperations = await SignedInAsync(FlightOperationsVid, token);

        // The routes, held on the events department — the permission, not the department (E2b) —, and nothing of the event to write.
        var held = await HeldOnTheEventsDepartmentAsync(flightOperations, token);
        Assert.Contains(EventsPermissions.RoutesView, held);
        Assert.Contains(EventsPermissions.RoutesEdit, held);
        Assert.DoesNotContain(EventsPermissions.Edit, held);

        // A route, in the event's care whatever the payload, its airports in upper case as the core writes them.
        var route = await CreatedAsync(
            flightOperations,
            EventRouteEndpoints.Pattern,
            Route(id, "xec1", Second, "DCT POINT UL1 OTHER DCT", Text("evt-test-e4 by the sea")),
            token);
        Assert.Equal(First, route.GetProperty("departureIcao").GetString());
        Assert.Equal(Second, route.GetProperty("arrivalIcao").GetString());
        Assert.Equal(nameof(Department.ED), route.GetProperty("ownerDepartment").GetString());

        // Changed — the remarks taken out: written in no language, none —, and listed in the tab of the event.
        var changed = await OkAsync(await flightOperations.PutAsJsonAsync(
            $"{EventRouteEndpoints.Pattern}/{Id(route)}",
            Route(id, First, Second, "DCT POINT DCT", Text(string.Empty)) with { RowVersion = RowVersion(route) },
            token), token);
        Assert.Equal("DCT POINT DCT", changed.GetProperty("route").GetString());
        Assert.Equal(JsonValueKind.Null, changed.GetProperty("remarks").ValueKind);

        var listed = await OkAsync(await flightOperations.GetAsync($"{EventRouteEndpoints.Pattern}?filter[eventId]={id}", token), token);
        Assert.Equal([Id(route)], listed.GetProperty("items").EnumerateArray().Select(Id));

        // Every refusal on its field: an airport the hub does not know, no route, remarks in one language of the division only.
        using (var refused = await flightOperations.PostAsJsonAsync(
            EventRouteEndpoints.Pattern,
            Route(id, "XEZ9", Second, string.Empty, Text("evt-test-e4 half", "en")),
            token))
        {
            var errors = await RefusalsAsync(refused, token);
            Assert.Contains("errors.required", errors["route"]);
            Assert.Contains("errors.localized.missing", errors["remarks"]);
        }

        using (var unknown = await flightOperations.PostAsJsonAsync(
            EventRouteEndpoints.Pattern,
            Route(id, "XEZ9", "XEZ8", "DCT", remarks: null),
            token))
        {
            var errors = await RefusalsAsync(unknown, token);
            Assert.Contains("events:errors.airportUnknown", errors["departureIcao"]);
            Assert.Contains("events:errors.airportUnknown", errors["arrivalIcao"]);
        }

        // From an airport to itself, whatever the case it is written in: a route goes somewhere (the review of #223, point 4).
        using (var toItself = await flightOperations.PostAsJsonAsync(
            EventRouteEndpoints.Pattern,
            Route(id, First, "xec1", "DCT", remarks: null),
            token))
        {
            Assert.Contains("events:errors.routeToItself", (await RefusalsAsync(toItself, token))["arrivalIcao"]);
        }

        // Nor the text of the event, nor its airports, nor publishing or cancelling it: none of theirs (§6.2).
        AssertForbidden(await flightOperations.PutAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}",
            Payload("routes", Starts(days: 30)) with { Title = Text("evt-test-e4 not theirs"), RowVersion = RowVersion(created) },
            token));
        AssertForbidden(await flightOperations.PostAsJsonAsync(EventAirportEndpoints.Pattern, Airport(id, First, 1), token));
        AssertForbidden(await flightOperations.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}/publish",
            new EventPublishRequest(RowVersion(created)),
            token));
        AssertForbidden(await flightOperations.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}/cancel",
            new EventCancelRequest(Text("evt-test-e4 not theirs"), default),
            token));

        // The ATC operations read the event and not its routes: another area, another grant.
        using var atcOperations = await SignedInAsync(AtcOperationsVid, token);
        AssertForbidden(await atcOperations.GetAsync($"{EventRouteEndpoints.Pattern}?filter[eventId]={id}", token));
        AssertForbidden(await atcOperations.PostAsJsonAsync(EventRouteEndpoints.Pattern, Route(id, First, Second, "DCT", remarks: null), token));

        // The events department writes them too; the flight operations take theirs out.
        var kept = await CreatedAsync(coordinator, EventRouteEndpoints.Pattern, Route(id, Second, First, "DCT", remarks: null), token);
        using (var deleted = await flightOperations.DeleteAsync($"{EventRouteEndpoints.Pattern}/{Id(route)}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        // Nothing of the event was written by them.
        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        Assert.Equal(Slug("routes"), stored.GetProperty("title").GetProperty("en").GetString());
        Assert.Equal(nameof(PublishStatus.Draft), stored.GetProperty("status").GetString());

        // Deleted by its coordinator, the event takes its routes with it, each leaving its row in the audit as it goes.
        using (var deletedEvent = await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deletedEvent.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<Modules.Events.Data.EventsDbContext>().Routes
            .AnyAsync(row => row.EventId == id, token));
        var keptId = Id(kept).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(entry => entry.Entity == "evt_routes" && entry.EntityId == keptId && entry.Action == "deleted", token));
    }

    [Fact]
    public async Task AVisitorReadsThePageOfAPublishedEventUntilItsEndAndTheStaffAfterIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var visitor = _factory.CreateApiClient();

        // Published with an airport and two routes: the page shows them, with the names the core knows — the routes in the order
        // they were written, here the way out from the second airport first, where an order of the codes would put it second.
        var page = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("page", Starts(days: 10)), token));
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(page, First, 1), token);
        var wayOut = await CreatedAsync(coordinator, EventRouteEndpoints.Pattern, Route(page, Second, First, "DCT OUT DCT", remarks: null), token);
        var wayBack = await CreatedAsync(coordinator, EventRouteEndpoints.Pattern, Route(page, First, Second, "DCT POINT DCT", Text("evt-test-e4 remark")), token);
        await PublishAsync(coordinator, page, token);

        var read = await OkAsync(await visitor.GetAsync(PageUri("page"), token), token);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("unseen").ValueKind);
        Assert.Equal(nameof(EventStateKind.Announced), read.GetProperty("state").GetString());
        Assert.Equal("rfo", read.GetProperty("kind").GetString());
        var airport = Assert.Single(read.GetProperty("airports").EnumerateArray());
        Assert.Equal((First, $"evt-test {First}"), (airport.GetProperty("icao").GetString(), airport.GetProperty("name").GetString()));
        var routes = read.GetProperty("routes").EnumerateArray().ToList();
        Assert.Equal([Id(wayOut), Id(wayBack)], routes.Select(Id));
        Assert.Equal(Second, routes[1].GetProperty("arrival").GetProperty("icao").GetString());
        Assert.Equal($"evt-test {Second}", routes[1].GetProperty("arrival").GetProperty("name").GetString());
        Assert.Equal("evt-test-e4 remark", routes[1].GetProperty("remarks").GetProperty("it").GetString());

        // The tab of the event in the back office lists them in the same order.
        var listed = await OkAsync(await coordinator.GetAsync($"{EventRouteEndpoints.Pattern}?filter[eventId]={page}", token), token);
        Assert.Equal([Id(wayOut), Id(wayBack)], listed.GetProperty("items").EnumerateArray().Select(Id));

        // After its end: not found by a visitor, read by the staff of the events — the events department, and whoever collaborates
        // with Events.View on it —, who are told nobody else sees it, and why.
        var over = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("over", Starts(days: -3)), token));
        await PublishAsync(coordinator, over, token);
        await AssertNotFoundAsync(visitor, PageUri("over"), token);

        var staffRead = await OkAsync(await coordinator.GetAsync(PageUri("over"), token), token);
        Assert.Equal(nameof(EventUnseen.Over), staffRead.GetProperty("unseen").GetString());
        Assert.Equal(nameof(EventStateKind.Ended), staffRead.GetProperty("state").GetString());

        using var flightOperations = await SignedInAsync(FlightOperationsVid, token);
        Assert.Equal(
            nameof(EventUnseen.Over),
            (await OkAsync(await flightOperations.GetAsync(PageUri("over"), token), token)).GetProperty("unseen").GetString());

        // A member who is not of the staff: not found either.
        using var member = await SignedInAsync(MemberVid, token);
        await AssertNotFoundAsync(member, PageUri("over"), token);

        // A draft and one published but not seen yet: the staff's, in the same way.
        await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("draft", Starts(days: 10)), token);
        var scheduled = Id(await CreatedAsync(
            coordinator,
            EventEndpoints.Pattern,
            Payload("scheduled", Starts(days: 10)) with { VisibleFromUtc = Starts(days: 5) },
            token));
        await PublishAsync(coordinator, scheduled, token);

        foreach (var (name, state, unseen) in new[]
        {
            ("draft", EventStateKind.Draft, EventUnseen.Draft),
            ("scheduled", EventStateKind.Scheduled, EventUnseen.NotSeenYet),
        })
        {
            await AssertNotFoundAsync(visitor, PageUri(name), token);
            var preview = await OkAsync(await coordinator.GetAsync(PageUri(name), token), token);
            Assert.Equal(unseen.ToString(), preview.GetProperty("unseen").GetString());
            Assert.Equal(state.ToString(), preview.GetProperty("state").GetString());
        }

        // Cancelled before it is seen: its state says cancelled, and why nobody else sees it is still that it is not seen yet — what
        // the state alone could not tell from a cancelled event that is over.
        await OkAsync(await coordinator.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{scheduled}/cancel",
            new EventCancelRequest(Text("evt-test-e4 early"), default),
            token), token);
        var early = await OkAsync(await coordinator.GetAsync(PageUri("scheduled"), token), token);
        Assert.Equal(
            (nameof(EventStateKind.Cancelled), nameof(EventUnseen.NotSeenYet)),
            (early.GetProperty("state").GetString(), early.GetProperty("unseen").GetString()));

        // A cancelled event stays until its end, with its note (§2.3).
        await OkAsync(await coordinator.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{page}/cancel",
            new EventCancelRequest(Text("evt-test-e4 weather"), default),
            token), token);
        var cancelled = await OkAsync(await visitor.GetAsync(PageUri("page"), token), token);
        Assert.Equal(nameof(EventStateKind.Cancelled), cancelled.GetProperty("state").GetString());
        Assert.Equal("evt-test-e4 weather", cancelled.GetProperty("cancellationNote").GetProperty("en").GetString());

        // An address no event has is not found, by the staff too.
        await AssertNotFoundAsync(coordinator, PageUri("nowhere"), token);
    }

    [Fact]
    public async Task AnEventForTheMembersIsHiddenFromVisitors()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var visitor = _factory.CreateApiClient();
        using var member = await SignedInAsync(MemberVid, token);

        var members = Id(await CreatedAsync(
            coordinator,
            EventEndpoints.Pattern,
            Payload("members", Starts(days: 7)) with { Visibility = Visibility.Members },
            token));
        await PublishAsync(coordinator, members, token);

        // A visitor: neither its page nor its card.
        await AssertNotFoundAsync(visitor, PageUri("members"), token);
        Assert.DoesNotContain(Slug("members"), await ListedAsync(visitor, token));

        // A member signed in: both.
        Assert.Equal(
            JsonValueKind.Null,
            (await OkAsync(await member.GetAsync(PageUri("members"), token), token)).GetProperty("unseen").ValueKind);
        Assert.Contains(Slug("members"), await ListedAsync(member, token));
    }

    [Fact]
    public async Task TheBlockListsTheEventsSeenToComeAndInProgressTheSoonestFirst()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var visitor = _factory.CreateApiClient();
        var now = DateTime.UtcNow;

        // In progress, to come — one with two airports in their order, of another kind —, cancelled before its start; and what the
        // public does not see: over, a draft, one seen only from tomorrow.
        var inProgress = await PublishedAsync(
            coordinator,
            Payload("block-now", now.AddHours(-1)) with { EndsAtUtc = now.AddHours(3) },
            token);
        await PublishedAsync(coordinator, Payload("block-soon", Starts(days: 3)), token);

        var later = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("block-later", Starts(days: 10)) with { Kind = "rfe" }, token));
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(later, Second, 1), token);
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(later, First, 2), token);
        await PublishAsync(coordinator, later, token);

        var cancelled = await PublishedAsync(coordinator, Payload("block-cancelled", Starts(days: 5)), token);
        await OkAsync(await coordinator.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{Id(cancelled)}/cancel",
            new EventCancelRequest(Text("evt-test-e4 weather"), default),
            token), token);

        await PublishedAsync(coordinator, Payload("block-over", Starts(days: -3)), token);
        await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("block-draft", Starts(days: 4)), token);
        await PublishedAsync(coordinator, Payload("block-scheduled", Starts(days: 6)) with { VisibleFromUtc = now.AddDays(1) }, token);

        // Every event seen, the soonest first: what has started, then what is to come, the cancelled one with its state.
        var items = await BlockItemsAsync(visitor, """{"limit":0}""", token);
        Assert.Equal(
            [Slug("block-now"), Slug("block-soon"), Slug("block-cancelled"), Slug("block-later")],
            items.Select(item => item.GetProperty("slug").GetString()));
        Assert.Equal(
            [nameof(EventStateKind.InProgress), nameof(EventStateKind.Announced), nameof(EventStateKind.Cancelled), nameof(EventStateKind.Announced)],
            items.Select(item => item.GetProperty("state").GetString()));
        // Its airports in their order, each with the name the core knows.
        Assert.Equal(
            [(Second, $"evt-test {Second}"), (First, $"evt-test {First}")],
            items[3].GetProperty("airports").EnumerateArray()
                .Select(airport => (airport.GetProperty("icao").GetString(), airport.GetProperty("name").GetString())));
        Assert.Equal(Id(inProgress), Id(items[0]));

        // Of the kinds the block names, and of no other.
        var rfe = await BlockItemsAsync(visitor, """{"kinds":[{"kind":"rfe"}],"limit":0}""", token);
        Assert.Equal([Slug("block-later")], rfe.Select(item => item.GetProperty("slug").GetString()));

        // And at most as many as it asks.
        using var one = await visitor.GetAsync(BlockUri("""{"limit":1}"""), token);
        Assert.Single((await OkAsync(one, token)).GetProperty("items").EnumerateArray());
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>An instant some days from today, on the hour: what the database stores is what was sent.</summary>
    private static DateTime Starts(int days) => DateTime.UtcNow.Date.AddDays(days).AddHours(17);

    /// <summary>An RFO without slots, lasting five hours, called after its address in both languages of the division.</summary>
    private static EventWriteDto Payload(string name, DateTime starts) => new(
        Kind: "rfo",
        PublicSlots: false,
        PrivateSlots: false,
        WholeDivision: false,
        Organizer: EventOrganizer.Division,
        ExternalUrl: null,
        Title: Text(Slug(name)),
        Slug: Slug(name),
        Summary: Text(Slug(name)),
        Body: null,
        BannerMediaId: null,
        VisibleFromUtc: null,
        BookingOpensAtUtc: null,
        StartsAtUtc: starts,
        EndsAtUtc: starts.AddHours(5),
        Visibility: Visibility.Public,
        RowVersion: default);

    private static EventAirportWriteDto Airport(long eventId, string icao, int ordinal) =>
        new(eventId, icao, ordinal, MaxMovementsPerHour: null, MaxArrivalsPerHour: null, MaxDeparturesPerHour: null, RowVersion: default);

    private static EventRouteWriteDto Route(long eventId, string departure, string arrival, string route, Localized<string>? remarks) =>
        new(eventId, departure, arrival, route, remarks, RowVersion: default);

    private static Localized<string> Text(string text) => Text(text, Locales);

    private static Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static DateTime RowVersion(JsonElement row) => row.GetProperty("rowVersion").GetDateTime();

    private static Uri PageUri(string name) => new($"{PublicEventEndpoints.Pattern}/{Slug(name)}", UriKind.Relative);

    private static Uri BlockUri(string props) => new(
        $"/api/blocks/data/{EventListProvider.BlockType}?props="
        + Convert.ToBase64String(Encoding.UTF8.GetBytes(props)).Replace('+', '-').Replace('/', '_').TrimEnd('='),
        UriKind.Relative);

    /// <summary>The cards of this class the block answers with, in their order: the shared database holds other classes' too.</summary>
    private static async Task<List<JsonElement>> BlockItemsAsync(HttpClient client, string props, CancellationToken cancellationToken) =>
        [
            .. (await OkAsync(await client.GetAsync(BlockUri(props), cancellationToken), cancellationToken))
                .GetProperty("items").EnumerateArray()
                .Where(item => item.GetProperty("slug").GetString()!.StartsWith(SlugStem, StringComparison.Ordinal)),
        ];

    /// <summary>The addresses of this class's events the block lists, as many as it gives: other classes' may come sooner.</summary>
    private static async Task<List<string>> ListedAsync(HttpClient client, CancellationToken cancellationToken) =>
        [.. (await BlockItemsAsync(client, """{"limit":0}""", cancellationToken)).Select(item => item.GetProperty("slug").GetString()!)];

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await OkAsync(await client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(default), cancellationToken), cancellationToken);

    /// <summary>Created and published, as the event's page does it.</summary>
    private static async Task<JsonElement> PublishedAsync(HttpClient client, EventWriteDto payload, CancellationToken cancellationToken)
    {
        var created = await CreatedAsync(client, EventEndpoints.Pattern, payload, cancellationToken);
        await PublishAsync(client, Id(created), cancellationToken);
        return created;
    }

    /// <summary>What the session of a member of staff holds on the events department, by name.</summary>
    private static async Task<List<string?>> HeldOnTheEventsDepartmentAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken);
        return
        [
            .. me.GetProperty("permissions").EnumerateArray()
                .Where(permission => permission.GetProperty("department").GetString() == nameof(Department.ED))
                .Select(permission => permission.GetProperty("name").GetString()),
        ];
    }

    private static async Task<JsonElement> CreatedAsync<T>(HttpClient client, string uri, T payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
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

    private static async Task AssertNotFoundAsync(HttpClient client, Uri uri, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{uri}: {response.StatusCode}");
    }

    private static void AssertForbidden(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden,
                $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {response.StatusCode}");
        }
    }

    /// <summary>The keys of a 400, field by field.</summary>
    private static async Task<Dictionary<string, string[]>> RefusalsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        return JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject()
            .ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray());
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member with one position of the staff — or none, a member of the division and nothing else —, and never an address.</summary>
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
        user.LastName = "Events";
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

    /// <summary>The two airports of the class, in the core's snapshot, where the airports of an event and of its routes are checked.</summary>
    private async Task SeedAirportsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var icao in new[] { First, Second })
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

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class, with their airports, their routes and what they projected, whatever a stopped run left.</summary>
    private Task ForgetEventsAsync(CancellationToken cancellationToken) =>
        EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);
}
