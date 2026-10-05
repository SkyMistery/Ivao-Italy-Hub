using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The life of an event (M4, E3b), through the real host with the division file of this repository and a clock the test moves:
/// «Publish» refuses what an event still needs, field by field, and a published event stays one that could be published; a
/// published event is in the calendar and in the search from when it is seen to its end, with <see cref="EventReleaseJob"/>
/// projecting it again at both moments — the same run twice, or after one lost, does the same —; a cancelled event has no entry,
/// and one for the members no line in the search; its files are in use until a week after its end.
/// <para>⚠️ The coordinator is seeded **without an address**, as in <see cref="EventsStaffTests"/>. ⚠️ No signed-in request is made
/// while the clock is moved, out of caution: how a member's session reads a clock moved by days is not what these tests prove.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsLifeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // E3b's in the range the events module owns in the shared database (CONTRIBUTING.md): 761011–761017 and 761061–761068.
    private const int CoordinatorVid = 761011;

    /// <summary>An airport of no country the network has: nobody else's airport is touched.</summary>
    private const string Airport = "XEB1";

    private const string SlugStem = "evt-test-e3b";

    /// <summary>A word no other row of the search has, in the summary of the events found.</summary>
    private const string SearchWord = "Quetzalcoatlus";

    /// <summary>Files of the library nobody else names: two banners and a picture of the description.</summary>
    private const long BannerId = 761_011_001;
    private const long PictureId = 761_011_002;
    private const long OtherBannerId = 761_011_003;

    /// <summary>A description of one section with one picture of the library.</summary>
    private const string DescriptionWithPicture = """
        {"schemaVersion":1,"sections":[{"id":"s","blocks":[{"id":"p","type":"image",
         "props":{"mediaId":761011002,"alt":{"it":"Foto","en":"Picture"}}}]}]}
        """;

    private static readonly string[] Locales = ["it", "en"];

    private readonly MovableClock _clock = new();
    private HubWebApplicationFactory _base = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The recorded answers of the network (E10b's warning in HANDOFF-M4.md), and the clock of the host, which the test moves:
        // the projections read it (ProjectionContext.Clock), and so do the job and the verbs.
        _base = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        _factory = _base.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IClock>(_clock)));
        var token = TestContext.Current.CancellationToken;

        // The host runs the job by itself every quarter of an hour, and a run of its own would move the start the test's next run
        // counts from (TourTests): paused, the only runs are the test's.
        var scheduler = await _factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(token);
        await scheduler.PauseJob(new JobKey(EventReleaseJob.JobName), token);

        await SeedCoordinatorAsync(token);
        await SeedAirportAsync(token);
        await ForgetAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => position.Vid == CoordinatorVid).ExecuteDeleteAsync();
            await database.IvaoAirports.Where(airport => airport.Icao == Airport).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
        await _base.DisposeAsync();
    }

    [Fact]
    public async Task PublishingRefusesWhatTheEventStillNeedsFieldByField()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var starts = Starts(days: 30);

        // A draft may be all of this: English only, slots without an airport nor the opening of their bookings, the event of
        // another division without its page, seen only after it starts.
        var draft = await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("refused", starts) with
        {
            Title = Text("evt-test-e3b refused", "en"),
            Summary = Text("evt-test-e3b refused", "en"),
            PublicSlots = true,
            Organizer = EventOrganizer.OtherDivision,
            VisibleFromUtc = starts.AddHours(1),
        }, token);

        var refused = await RefusedAsync(await PublishAsync(coordinator, draft, token), token);
        Assert.Equal(["errors.localized.missing"], refused.Errors["title"]);
        Assert.Equal(["it"], refused.Missing["title"]);
        Assert.Equal(["it"], refused.Missing["summary"]);
        Assert.Equal(["events:errors.seenAfterItStarts"], refused.Errors["visibleFromUtc"]);
        Assert.Equal(["events:errors.bookingOpensRequired"], refused.Errors["bookingOpensAtUtc"]);
        Assert.Equal(["events:errors.slotsNeedAirports"], refused.Errors["airports"]);
        Assert.Equal(["events:errors.externalUrlRequired"], refused.Errors["externalUrl"]);

        // The bookings open between when it is seen and its start: not before the one, not after the other.
        var early = await PutAsync(coordinator, draft, Payload("refused", starts) with
        {
            PublicSlots = true,
            VisibleFromUtc = starts.AddDays(-5),
            BookingOpensAtUtc = starts.AddDays(-6),
        }, token);
        Assert.Equal(
            ["events:errors.bookingBeforeItIsSeen"],
            (await RefusedAsync(await PublishAsync(coordinator, early, token), token)).Errors["bookingOpensAtUtc"]);

        var late = await PutAsync(coordinator, early, Payload("refused", starts) with { PublicSlots = true, BookingOpensAtUtc = starts.AddHours(1) }, token);
        Assert.Equal(
            ["events:errors.bookingAfterItStarts"],
            (await RefusedAsync(await PublishAsync(coordinator, late, token), token)).Errors["bookingOpensAtUtc"]);
        Assert.Equal(nameof(EventStateKind.Draft), late.GetProperty("state").GetString());

        // With all of it, it is published: seen in twenty days, so not seen yet.
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, AirportPayload(Id(draft)), token);
        var complete = await PutAsync(coordinator, late, Payload("refused", starts) with
        {
            PublicSlots = true,
            Organizer = EventOrganizer.OtherDivision,
            ExternalUrl = "https://example.org/evt-test-e3b",
            VisibleFromUtc = starts.AddDays(-10),
            BookingOpensAtUtc = starts.AddDays(-5),
        }, token);
        var published = await OkAsync(await PublishAsync(coordinator, complete, token), token);
        Assert.Equal(nameof(PublishStatus.Published), published.GetProperty("status").GetString());
        Assert.Equal(nameof(EventStateKind.Scheduled), published.GetProperty("state").GetString());
        Assert.NotEqual(JsonValueKind.Null, published.GetProperty("publishedAt").ValueKind);
    }

    [Fact]
    public async Task APublishedEventStaysOneThatCouldBePublished()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var starts = Starts(days: 30);
        var slots = Payload("stays", starts) with { PublicSlots = true, BookingOpensAtUtc = starts.AddDays(-5) };

        var draft = await CreatedAsync(coordinator, EventEndpoints.Pattern, slots, token);
        var airport = await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, AirportPayload(Id(draft)), token);

        // A version read before a change is not published over the change.
        var changed = await PutAsync(coordinator, draft, slots with { BookingOpensAtUtc = starts.AddDays(-4) }, token);
        using (var stale = await PublishAsync(coordinator, Id(draft), RowVersion(draft), token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        var published = await OkAsync(await PublishAsync(coordinator, changed, token), token);

        // Once.
        Assert.Equal(
            ["events:errors.alreadyPublished"],
            (await RefusedAsync(await PublishAsync(coordinator, published, token), token)).Errors["id"]);

        // A change it could not be published with is refused with the refusals of «Publish»: half a title, no opening of the
        // bookings; and its last airport stays while it has slots.
        using (var halfTitle = await coordinator.PutAsJsonAsync(
            $"{EventEndpoints.Pattern}/{Id(published)}",
            slots with { Title = Text("evt-test-e3b stays", "en"), BookingOpensAtUtc = null, RowVersion = RowVersion(published) },
            token))
        {
            var errors = (await RefusedAsync(halfTitle, token)).Errors;
            Assert.Equal(["errors.localized.missing"], errors["title"]);
            Assert.Equal(["events:errors.bookingOpensRequired"], errors["bookingOpensAtUtc"]);
        }

        using (var lastAirport = await coordinator.DeleteAsync($"{EventAirportEndpoints.Pattern}/{Id(airport)}", token))
        {
            Assert.Equal(["events:errors.slotsNeedAirports"], (await RefusedAsync(lastAirport, token)).Errors["id"]);
        }

        // One it could be published with is written, and it stays published.
        var renamed = await PutAsync(coordinator, published, slots with { Title = Text("evt-test-e3b renamed") }, token);
        Assert.Equal(nameof(PublishStatus.Published), renamed.GetProperty("status").GetString());
        Assert.Equal("evt-test-e3b renamed", renamed.GetProperty("title").GetProperty("it").GetString());

        // A cancelled draft is not published: it stays cancelled.
        var cancelled = await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("cancelled-draft", starts), token);
        await OkAsync(await coordinator.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{Id(cancelled)}/cancel",
            new EventCancelRequest(Text("evt-test-e3b weather"), default),
            token), token);
        Assert.Equal(
            ["events:errors.alreadyCancelled"],
            (await RefusedAsync(await PublishAsync(coordinator, Id(cancelled), default, token), token)).Errors["id"]);
    }

    [Fact]
    public async Task AnEventPublishedWithItsReleaseInAnHourEntersTheCalendarAtItAndLeavesItAtItsEnd()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var now = _clock.UtcNow;
        var starts = Starts(days: 3);

        var published = await PublishedAsync(coordinator, Payload("release", starts) with
        {
            Summary = Text($"evt-test-e3b {SearchWord}"),
            VisibleFromUtc = now.AddHours(1),
        }, token);
        Assert.Equal(nameof(EventStateKind.Scheduled), published.GetProperty("state").GetString());
        var id = Id(published);
        var version = await StoredRowVersionAsync(id, token);

        // Published, its release in an hour: in nobody's calendar, found by nobody.
        var before = await ProjectedAsync(id, token);
        Assert.Empty(before.Calendar);
        Assert.Empty(before.Search);
        Assert.False(await FoundAsync(Slug("release"), token));

        // The release passes and nobody writes the event: the job's next run brings it in, for everybody.
        _clock.Offset = TimeSpan.FromMinutes(61);
        await RunJobAsync(token);

        var seen = await ProjectedAsync(id, token);
        var entry = Assert.Single(seen.Calendar);
        Assert.Equal("rfo", entry.Kind);
        Assert.Equal(starts, entry.StartsAtUtc);
        Assert.Equal(starts.AddHours(5), entry.EndsAtUtc);
        Assert.Equal(Visibility.Public, entry.Visibility);
        Assert.Equal(Department.ED, entry.OwnerDepartment);
        Assert.Equal($"/events/{Slug("release")}", entry.Url);
        Assert.Equal(Locales.Length, seen.Search.Count);
        Assert.All(seen.Search, row => Assert.Equal((Event.SearchKind, Visibility.Public), (row.Kind, row.Visibility)));
        Assert.True(await FoundAsync(Slug("release"), token));

        // Its end passes: out of the calendar and of the search.
        _clock.Offset = starts.AddHours(5) - now;
        await RunJobAsync(token);

        var ended = await ProjectedAsync(id, token);
        Assert.Empty(ended.Calendar);
        Assert.Empty(ended.Search);
        Assert.False(await FoundAsync(Slug("release"), token));

        // And the job wrote nothing of the event itself.
        Assert.Equal(version, await StoredRowVersionAsync(id, token));
    }

    [Fact]
    public async Task TheJobDoesTheSameRunTwiceAndAfterRunsLost()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var now = _clock.UtcNow;
        var starts = Starts(days: 3);

        var first = Id(await PublishedAsync(coordinator, Payload("first", starts) with { VisibleFromUtc = now.AddHours(1) }, token));
        var second = Id(await PublishedAsync(coordinator, Payload("second", starts) with { VisibleFromUtc = now.AddHours(2) }, token));

        // A run after the first release: the first in, the second not yet.
        _clock.Offset = TimeSpan.FromMinutes(65);
        await RunJobAsync(token);
        var entry = Assert.Single((await ProjectedAsync(first, token)).Calendar);
        Assert.Empty((await ProjectedAsync(second, token)).Calendar);

        // Twice: the same rows the same way — the same entry, not a second one.
        await RunJobAsync(token);
        Assert.Equal(entry.Id, Assert.Single((await ProjectedAsync(first, token)).Calendar).Id);
        Assert.Empty((await ProjectedAsync(second, token)).Calendar);

        // The run after the second release is lost — the hub asleep —, and the one after it fails: the next run that succeeds
        // counts from the last one that did, and makes them up.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            hub.JobsLog.Add(new JobLogEntry
            {
                Job = EventReleaseJob.JobName,
                StartedAt = now.AddMinutes(150),
                FinishedAt = now.AddMinutes(150),
                Status = "failed",
                Message = "evt-test-e3b a run that failed",
            });
            await hub.SaveChangesAsync(token);
        }

        _clock.Offset = TimeSpan.FromMinutes(185);
        await RunJobAsync(token);
        Assert.Single((await ProjectedAsync(second, token)).Calendar);
        Assert.Equal(entry.Id, Assert.Single((await ProjectedAsync(first, token)).Calendar).Id);

        // After both ends, one run takes both out.
        _clock.Offset = starts.AddHours(6) - now;
        await RunJobAsync(token);
        foreach (var id in new[] { first, second })
        {
            var ended = await ProjectedAsync(id, token);
            Assert.Empty(ended.Calendar);
            Assert.Empty(ended.Search);
        }
    }

    [Fact]
    public async Task ACancelledEventLeavesTheCalendarAndAnEventOfTheMembersIsNotInTheSearch()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var now = _clock.UtcNow;
        var starts = Starts(days: 3);

        // Without a release, it is seen once it is published: projected by the publication itself.
        var published = await PublishedAsync(coordinator, Payload("cancelled", starts), token);
        var id = Id(published);
        var seen = await ProjectedAsync(id, token);
        Assert.Single(seen.Calendar);
        Assert.Equal(Locales.Length, seen.Search.Count);

        // Cancelled: no entry. Its page stays with the note until its end (§2.3), and the search finds it there.
        await OkAsync(await coordinator.PostAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}/cancel",
            new EventCancelRequest(Text("evt-test-e3b weather"), RowVersion(published)),
            token), token);
        var cancelled = await ProjectedAsync(id, token);
        Assert.Empty(cancelled.Calendar);
        Assert.Equal(Locales.Length, cancelled.Search.Count);

        // An event for the members: in their calendar, and never in the search, which holds the events everybody reads (§8.1).
        var members = Id(await PublishedAsync(coordinator, Payload("members", starts) with { Visibility = Visibility.Members }, token));
        var theirs = await ProjectedAsync(members, token);
        Assert.Equal(Visibility.Members, Assert.Single(theirs.Calendar).Visibility);
        Assert.Empty(theirs.Search);

        // At the end the cancelled one leaves the search too.
        _clock.Offset = starts.AddHours(5) - now;
        await RunJobAsync(token);
        Assert.Empty((await ProjectedAsync(id, token)).Search);
        Assert.Empty((await ProjectedAsync(members, token)).Calendar);
    }

    [Fact]
    public async Task TheFilesOfAnEventAreInUseUntilAWeekAfterItsEnd()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var starts = Starts(days: 30);
        var ends = starts.AddHours(5);

        // A draft's files are in use too: an event being prepared needs its banner as much as a published one.
        var draft = await CreatedAsync(
            coordinator,
            EventEndpoints.Pattern,
            Payload("files", starts) with { BannerMediaId = BannerId, Body = JsonNode.Parse(DescriptionWithPicture) },
            token);
        var uses = await MediaUsesAsync(Id(draft), token);
        Assert.Equal([BannerId, PictureId], uses.Keys.Order());
        Assert.All(uses.Values, until => Assert.Equal(ends + Event.MediaKeptAfterEnd, until));

        // The end moves, and the uses with it; a banner changed is no longer in use; published, the same.
        var moved = await PutAsync(
            coordinator,
            draft,
            Payload("files", starts) with { EndsAtUtc = ends.AddDays(1), BannerMediaId = OtherBannerId },
            token);
        await OkAsync(await PublishAsync(coordinator, moved, token), token);
        uses = await MediaUsesAsync(Id(draft), token);
        Assert.Equal([PictureId, OtherBannerId], uses.Keys.Order());
        Assert.All(uses.Values, until => Assert.Equal(ends.AddDays(1) + Event.MediaKeptAfterEnd, until));
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>An instant some days ahead, on the hour: what the database stores is what was sent.</summary>
    private DateTime Starts(int days) => _clock.UtcNow.Date.AddDays(days).AddHours(17);

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

    private static EventAirportWriteDto AirportPayload(long eventId) =>
        new(eventId, Airport, 1, MaxMovementsPerHour: 30, MaxArrivalsPerHour: null, MaxDeparturesPerHour: null, RowVersion: default);

    private static Localized<string> Text(string text) => Text(text, Locales);

    private static Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static DateTime RowVersion(JsonElement row) => row.GetProperty("rowVersion").GetDateTime();

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, JsonElement row, CancellationToken cancellationToken) =>
        PublishAsync(client, Id(row), RowVersion(row), cancellationToken);

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, long id, DateTime rowVersion, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{EventEndpoints.Pattern}/{id}/publish", new EventPublishRequest(rowVersion), cancellationToken);

    /// <summary>Created and published, as the event's page does it.</summary>
    private static async Task<JsonElement> PublishedAsync(HttpClient client, EventWriteDto payload, CancellationToken cancellationToken)
    {
        var created = await CreatedAsync(client, EventEndpoints.Pattern, payload, cancellationToken);
        return await OkAsync(await PublishAsync(client, created, cancellationToken), cancellationToken);
    }

    /// <summary>The event written with this payload, over the version <paramref name="current"/> holds.</summary>
    private static async Task<JsonElement> PutAsync(HttpClient client, JsonElement current, EventWriteDto payload, CancellationToken cancellationToken) =>
        await OkAsync(
            await client.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{Id(current)}",
                payload with { RowVersion = RowVersion(current) },
                cancellationToken),
            cancellationToken);

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
                $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    /// <summary>The keys of a 400 field by field, and the languages missing in each translated field refused.</summary>
    private static async Task<(Dictionary<string, string[]> Errors, Dictionary<string, string[]> Missing)> RefusedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

            var problem = JsonDocument.Parse(text).RootElement;
            return (Fields(problem, "errors"), Fields(problem, "localized"));
        }

        static Dictionary<string, string[]> Fields(JsonElement problem, string name) =>
            problem.TryGetProperty(name, out var fields)
                ? fields.EnumerateObject().ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray())
                : [];
    }

    /// <summary>One run of the job, through the host, as Quartz would start it; a run that failed fails the test with its message.</summary>
    private async Task RunJobAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventReleaseJob>().RunAsync(cancellationToken);

        var last = await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog.AsNoTracking()
            .Where(run => run.Job == EventReleaseJob.JobName)
            .OrderByDescending(run => run.Id)
            .FirstAsync(cancellationToken);
        Assert.True(last.Status == "succeeded", last.Message);
    }

    /// <summary>The lines of the event in the search (one per language) and its calendar entries, whoever may read them.</summary>
    private async Task<(List<SearchIndexEntry> Search, List<CalendarEntry> Calendar)> ProjectedAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var source = string.Create(CultureInfo.InvariantCulture, $"event:{id}");

        var search = await hub.SearchIndex.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.SourceModule == EventsModule.ModuleKey && row.SourceId == source)
            .ToListAsync(cancellationToken);
        var calendar = await hub.CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.SourceModule == EventsModule.ModuleKey && row.SourceId == source)
            .ToListAsync(cancellationToken);

        return (search, calendar);
    }

    /// <summary>The files the event keeps in use, and until when.</summary>
    private async Task<Dictionary<long, DateTime?>> MediaUsesAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var source = string.Create(CultureInfo.InvariantCulture, $"event:{id}");

        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().MediaUses.AsNoTracking()
            .Where(row => row.SourceModule == EventsModule.ModuleKey && row.SourceId == source)
            .ToDictionaryAsync(row => row.MediaId, row => row.UsedUntil, cancellationToken);
    }

    /// <summary>Whether a visitor's search finds the event at this address, by the word of its summary.</summary>
    private async Task<bool> FoundAsync(string slug, CancellationToken cancellationToken)
    {
        using var visitor = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var answer = await visitor.GetFromJsonAsync<JsonElement>($"{SearchEndpoints.Pattern}?q={SearchWord}&locale=en", cancellationToken);

        return answer.GetProperty("results").GetProperty("items").EnumerateArray()
            .Any(hit => hit.GetProperty("url").GetString() == $"/events/{slug}");
    }

    private async Task<DateTime> StoredRowVersionAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => row.RowVersion)
            .SingleAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(
            new Uri(string.Create(CultureInfo.InvariantCulture, $"{TestSignInStartupFilter.Path}?vid={vid}"), UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>The coordinator of the events department, with the one position and never an address.</summary>
    private async Task SeedCoordinatorAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == CoordinatorVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = CoordinatorVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "Events";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(row => row.Vid == CoordinatorVid && row.Position == "IT-EC", cancellationToken))
        {
            var parsed = StaffRoleMap.Parse("IT-EC", "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = CoordinatorVid,
                Position = "IT-EC",
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The airport of the class, in the core's snapshot, where an airport of an event is checked.</summary>
    private async Task SeedAirportAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == Airport, cancellationToken))
        {
            database.IvaoAirports.Add(new IvaoAirport
            {
                Icao = Airport,
                Name = $"evt-test {Airport}",
                CountryId = "XX",
                Latitude = 41.8,
                Longitude = 12.24,
                SyncedAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow,
            });
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>The events of the class with what they projected, and the runs of the job: each test counts its runs from none.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().JobsLog
            .Where(run => run.Job == EventReleaseJob.JobName)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>The clock of the host, which a test moves forward: the real time, plus how far it was moved.</summary>
    private sealed class MovableClock : IClock
    {
        public TimeSpan Offset { get; set; }

        public DateTime UtcNow => DateTime.UtcNow + Offset;
    }
}
