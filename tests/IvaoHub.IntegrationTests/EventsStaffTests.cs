using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The event in the staff's back office (M4, E3a), through the real host with the division file of this repository: the events
/// department creates and changes an event and its airports, cancels one with its note and deletes one nobody took part in;
/// whoever collaborates — the ATC operations, the flight operations, the membership, with the nine grants of the design on the
/// events department — reads the events and neither writes nor deletes them, and holds the permission and not the department
/// (E2b); the list has the views of the state; the presets of the kinds reach whoever writes events, and their rows are the
/// kinds an event chooses from (E4).
/// <para>⚠️ Everybody here is seeded **without an address**, as in <see cref="EventsSkeletonTests"/>: the tests of the contacts
/// assert who of the events, the ATC operations, the flight operations and the membership receives a message. What each holds
/// comes from the grants of their position, which the division file gives.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsStaffTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the events module owns in the shared database (CONTRIBUTING.md); E3a has 761006–761018 and 761060–761069.
    private const int CoordinatorVid = 761006;
    private const int AdvisorVid = 761007;
    private const int AtcOperationsVid = 761008;
    private const int FlightOperationsVid = 761009;
    private const int MembershipVid = 761010;

    /// <summary>Two airports of no country the network has: nobody else's airport is touched.</summary>
    private const string First = "XEA1";
    private const string Second = "XEA2";

    private const string SlugStem = "evt-test-e3a";

    /// <summary>A description of one section with one block of text, in both languages.</summary>
    private const string Description = """
        {"schemaVersion":1,"sections":[{"id":"s","blocks":[{"id":"b","type":"text",
         "props":{"markdown":{"it":"Testo italiano","en":"English text"}}}]}]}
        """;

    private static readonly string[] Locales = ["it", "en"];

    private static readonly Uri SettingsUri = new(
        ModuleSettingsEndpoints.Pattern.Replace("{key}", EventsModule.ModuleKey, StringComparison.Ordinal),
        UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        // The recorded answers of the network: a host started without them asks the network for a token while the reference
        // data are empty (E10b's warning in HANDOFF-M4.md), and no test calls the network.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(AdvisorVid, "IT-EA2", token);
        await SeedUserAsync(AtcOperationsVid, "IT-AOA1", token);
        await SeedUserAsync(FlightOperationsVid, "IT-FOAC", token);
        await SeedUserAsync(MembershipVid, "IT-MA1", token);
        await SeedAirportsAsync(token);
        await ForgetEventsAsync(token);
    }

    /// <summary>
    /// What the class seeded is taken back — its events with their airports, its airports, the positions of its VIDs and the
    /// settings —, so that no class after it finds a staff it did not seed. The members themselves stay, as every class leaves its
    /// own.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        int[] vids = [CoordinatorVid, AdvisorVid, AtcOperationsVid, FlightOperationsVid, MembershipVid];

        await ForgetEventsAsync(CancellationToken.None);
        await ForgetSettingsAsync(CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.UserStaffPositions.Where(position => vids.Contains(position.Vid)).ExecuteDeleteAsync();
            await database.IvaoAirports.Where(airport => airport.Icao == First || airport.Icao == Second).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheEventsDepartmentCreatesAndChangesAnEvent()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(AdvisorVid, token);

        // An advisor of the department writes events, as the design gives them (§6.2).
        var created = await CreatedAsync(advisor, EventEndpoints.Pattern, Payload("create") with { PublicSlots = true }, token);
        Assert.Equal(nameof(Department.ED), created.GetProperty("ownerDepartment").GetString());
        Assert.Equal(nameof(EventStateKind.Draft), created.GetProperty("state").GetString());
        Assert.True(created.GetProperty("publicSlots").GetBoolean());
        Assert.False(created.GetProperty("privateSlots").GetBoolean());

        // Changed, and read back as it was written.
        var id = Id(created);
        var changed = Payload("create") with
        {
            Title = Text("evt-test-e3a renamed"),
            ExternalUrl = "https://example.org/the-event",
            Organizer = EventOrganizer.OtherDivision,
            Visibility = Visibility.Members,
            RowVersion = created.GetProperty("rowVersion").GetDateTime(),
        };
        var updated = await OkAsync(await advisor.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", changed, token), token);
        Assert.Equal("evt-test-e3a renamed", updated.GetProperty("title").GetProperty("en").GetString());
        Assert.Equal(nameof(EventOrganizer.OtherDivision), updated.GetProperty("organizer").GetString());
        Assert.Equal(nameof(Visibility.Members), updated.GetProperty("visibility").GetString());

        // A version read before the change is answered 409, not written over the change.
        using (var stale = await advisor.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", changed, token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        // The description is a document of blocks, kept as it is sent; its envelope is checked, on its field.
        var described = await OkAsync(await advisor.PutAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}",
            changed with { Body = JsonNode.Parse(Description), RowVersion = updated.GetProperty("rowVersion").GetDateTime() },
            token), token);
        Assert.Equal(
            "text",
            described.GetProperty("body").GetProperty("sections")[0].GetProperty("blocks")[0].GetProperty("type").GetString());

        using (var notADocument = await advisor.PutAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}",
            changed with { Body = JsonNode.Parse("[]"), RowVersion = described.GetProperty("rowVersion").GetDateTime() },
            token))
        {
            Assert.Contains("errors.body.notAnObject", (await RefusalsAsync(notADocument, token))["body"]);
        }

        // The list shows it with the division's word for its kind.
        var page = await OkAsync(await advisor.GetAsync($"{EventEndpoints.Pattern}?q={Slug("create")}", token), token);
        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("rfo", row.GetProperty("kind").GetString());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("kindLabel").GetProperty("en").GetString()));

        // Every refusal on its field.
        using (var refused = await advisor.PostAsJsonAsync(
            EventEndpoints.Pattern,
            Payload("refused") with
            {
                Kind = "evt-test-no-such-kind",
                ExternalUrl = "javascript:alert(1)",
                Visibility = Visibility.Department,
                EndsAtUtc = Payload("refused").StartsAtUtc!.Value.AddHours(-1),
            },
            token))
        {
            var errors = await RefusalsAsync(refused, token);
            Assert.Contains("errors.url.absolute", errors["externalUrl"]);
            Assert.Contains("events:errors.visibilityChoice", errors["visibility"]);
            Assert.Contains("events:errors.endsBeforeItStarts", errors["endsAtUtc"]);
        }

        // What needs other rows: a kind the calendar does not have, an address another event has.
        using (var unknownKind = await advisor.PostAsJsonAsync(EventEndpoints.Pattern, Payload("refused") with { Kind = "evt-test-no-such-kind" }, token))
        {
            Assert.Contains("errors.calendar.kindUnknown", (await RefusalsAsync(unknownKind, token))["kind"]);
        }

        using (var taken = await advisor.PostAsJsonAsync(EventEndpoints.Pattern, Payload("create"), token))
        {
            Assert.Contains("events:errors.slugTaken", (await RefusalsAsync(taken, token))["slug"]);
        }
    }

    [Fact]
    public async Task AnEventHasItsAirportsWithTheirCapacityOnceEach()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        var id = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("airports"), token));

        // In the event's care, whatever the payload; the ICAO in upper case, as the core writes it.
        var first = await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, "xea1", 1) with { MaxMovementsPerHour = 30 }, token);
        Assert.Equal(First, first.GetProperty("icao").GetString());
        Assert.Equal(nameof(Department.ED), first.GetProperty("ownerDepartment").GetString());
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, Second, 2) with { MaxArrivalsPerHour = 20, MaxDeparturesPerHour = 20 }, token);

        // An airport the hub does not know, one already in the event, a capacity said both ways: each on its field.
        using (var unknown = await coordinator.PostAsJsonAsync(EventAirportEndpoints.Pattern, Airport(id, "XEZ9", 3), token))
        {
            Assert.Contains("events:errors.airportUnknown", (await RefusalsAsync(unknown, token))["icao"]);
        }

        using (var twice = await coordinator.PostAsJsonAsync(EventAirportEndpoints.Pattern, Airport(id, First, 3), token))
        {
            Assert.Contains("events:errors.airportTwice", (await RefusalsAsync(twice, token))["icao"]);
        }

        using (var both = await coordinator.PostAsJsonAsync(
            EventAirportEndpoints.Pattern,
            Airport(id, First, 1) with { MaxMovementsPerHour = 30, MaxArrivalsPerHour = 20 },
            token))
        {
            Assert.Contains("events:errors.capacityEitherOr", (await RefusalsAsync(both, token))["maxMovementsPerHour"]);
        }

        using (var nowhere = await coordinator.PostAsJsonAsync(EventAirportEndpoints.Pattern, Airport(long.MaxValue, First, 1), token))
        {
            Assert.Contains("events:errors.eventUnknown", (await RefusalsAsync(nowhere, token))["eventId"]);
        }

        // The tab of the event lists them in their order.
        var listed = await OkAsync(await coordinator.GetAsync($"{EventAirportEndpoints.Pattern}?filter[eventId]={id}", token), token);
        Assert.Equal([First, Second], listed.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("icao").GetString()));

        // An event about the whole division has no airports of its own: it does not become one while it has some, nor takes one.
        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        using (var whole = await coordinator.PutAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}",
            Payload("airports") with { WholeDivision = true, RowVersion = stored.GetProperty("rowVersion").GetDateTime() },
            token))
        {
            Assert.Contains("events:errors.wholeDivisionHasAirports", (await RefusalsAsync(whole, token))["wholeDivision"]);
        }

        var onlineDay = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("whole") with { Kind = "online-day", WholeDivision = true }, token));
        using (var refused = await coordinator.PostAsJsonAsync(EventAirportEndpoints.Pattern, Airport(onlineDay, First, 1), token))
        {
            Assert.Contains("events:errors.wholeDivisionHasNoAirports", (await RefusalsAsync(refused, token))["eventId"]);
        }
    }

    [Fact]
    public async Task WhoeverCollaboratesReadsTheEventsAndNeitherWritesNorDeletesThem()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        var created = await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("collaborates"), token);
        var id = Id(created);
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, First, 1), token);
        var rewritten = Payload("collaborates") with { Title = Text("evt-test-e3a not theirs"), RowVersion = created.GetProperty("rowVersion").GetDateTime() };

        foreach (var (vid, department, part) in new[]
        {
            (AtcOperationsVid, Department.AOD, EventsPermissions.AtcEdit),
            (FlightOperationsVid, Department.FOD, EventsPermissions.RoutesEdit),
            (MembershipVid, Department.MD, EventsPermissions.ReportsEdit),
        })
        {
            using var collaborator = await SignedInAsync(vid, token);

            // Their part of the events, held on the events department — the permission, and not the department (E2b): their own
            // department is the only one their session says.
            var me = await collaborator.GetFromJsonAsync<JsonElement>("/api/me", token);
            var held = me.GetProperty("permissions").EnumerateArray()
                .Where(permission => permission.GetProperty("department").GetString() == nameof(Department.ED))
                .Select(permission => permission.GetProperty("name").GetString())
                .ToList();
            Assert.Contains(EventsPermissions.View, held);
            Assert.Contains(part, held);
            Assert.DoesNotContain(EventsPermissions.Edit, held);
            Assert.DoesNotContain(EventsPermissions.Delete, held);
            Assert.Equal(
                [department.ToString()],
                me.GetProperty("user").GetProperty("departments").EnumerateArray().Select(entry => entry.GetString()));

            // They read every event, its airports included, from the section of the back office.
            var page = await OkAsync(await collaborator.GetAsync($"{EventEndpoints.Pattern}?q={Slug("collaborates")}", token), token);
            Assert.Equal(id, Id(Assert.Single(page.GetProperty("items").EnumerateArray())));
            await OkAsync(await collaborator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
            var airports = await OkAsync(await collaborator.GetAsync($"{EventAirportEndpoints.Pattern}?filter[eventId]={id}", token), token);
            Assert.Single(airports.GetProperty("items").EnumerateArray());

            // Nor the text of the event, nor its airports, nor cancelling it, nor deleting it: none of theirs (§6.3).
            AssertForbidden(await collaborator.PutAsJsonAsync($"{EventEndpoints.Pattern}/{id}", rewritten, token));
            AssertForbidden(await collaborator.PostAsJsonAsync(EventAirportEndpoints.Pattern, Airport(id, Second, 2), token));
            AssertForbidden(await collaborator.PostAsJsonAsync(
                $"{EventEndpoints.Pattern}/{id}/cancel",
                new EventCancelRequest(Text("evt-test-e3a not theirs"), default),
                token));
            AssertForbidden(await collaborator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token));
            AssertForbidden(await collaborator.GetAsync(EventEndpoints.KindPresetsPattern, token));
        }

        // An advisor of the events department writes the event and does not delete it either: only its coordinator and assistant do.
        using var advisor = await SignedInAsync(AdvisorVid, token);
        AssertForbidden(await advisor.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token));

        // Nothing of it was written.
        var stored = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token), token);
        Assert.Equal(Slug("collaborates"), stored.GetProperty("title").GetProperty("en").GetString());
        Assert.Equal(JsonValueKind.Null, stored.GetProperty("cancelledAt").ValueKind);
    }

    [Fact]
    public async Task TheCoordinatorDeletesADraftNobodyTookPartInWithItsAirports()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        var id = Id(await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("deleted"), token));
        await CreatedAsync(coordinator, EventAirportEndpoints.Pattern, Airport(id, First, 1), token);

        using (var deleted = await coordinator.DeleteAsync($"{EventEndpoints.Pattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using (var gone = await coordinator.GetAsync($"{EventEndpoints.Pattern}/{id}", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Airports.AnyAsync(airport => airport.EventId == id, token));
    }

    [Fact]
    public async Task CancellingAnEventSaysWhenWhoAndWhyInEveryLanguageOnce()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(AdvisorVid, token);

        var created = await CreatedAsync(advisor, EventEndpoints.Pattern, Payload("cancelled"), token);
        var id = Id(created);
        var cancel = $"{EventEndpoints.Pattern}/{id}/cancel";

        // The note is read on the page of the event by everybody: in every language of the division.
        using (var halfNote = await advisor.PostAsJsonAsync(cancel, new EventCancelRequest(Text("weather", "en"), default), token))
        {
            Assert.Contains("errors.localized.missing", (await RefusalsAsync(halfNote, token))["note"]);
        }

        // A version read before a change is not cancelled over the change.
        var stored = await OkAsync(await advisor.PutAsJsonAsync(
            $"{EventEndpoints.Pattern}/{id}",
            Payload("cancelled") with { Title = Text("evt-test-e3a changed first"), RowVersion = created.GetProperty("rowVersion").GetDateTime() },
            token), token);

        using (var stale = await advisor.PostAsJsonAsync(cancel, new EventCancelRequest(Text("weather"), created.GetProperty("rowVersion").GetDateTime()), token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        var cancelled = await OkAsync(await advisor.PostAsJsonAsync(
            cancel,
            new EventCancelRequest(Text("weather"), stored.GetProperty("rowVersion").GetDateTime()),
            token), token);
        Assert.Equal(nameof(EventStateKind.Cancelled), cancelled.GetProperty("state").GetString());
        Assert.NotEqual(JsonValueKind.Null, cancelled.GetProperty("cancelledAt").ValueKind);
        Assert.Equal("weather", cancelled.GetProperty("cancellationNote").GetProperty("it").GetString());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Events.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(candidate => candidate.Id == id, token);
            Assert.Equal(AdvisorVid, row.CancelledBy);
        }

        // Once.
        using (var again = await advisor.PostAsJsonAsync(cancel, new EventCancelRequest(Text("weather"), default), token))
        {
            Assert.Contains("events:errors.alreadyCancelled", (await RefusalsAsync(again, token))["id"]);
        }

        // And an event that is over happened: it is not cancelled any more.
        var past = Id(await CreatedAsync(
            advisor,
            EventEndpoints.Pattern,
            Payload("over") with { StartsAtUtc = DateTime.UtcNow.AddDays(-2), EndsAtUtc = DateTime.UtcNow.AddDays(-2).AddHours(4) },
            token));
        using (var over = await advisor.PostAsJsonAsync($"{EventEndpoints.Pattern}/{past}/cancel", new EventCancelRequest(Text("weather"), default), token))
        {
            Assert.Contains("events:errors.eventOver", (await RefusalsAsync(over, token))["id"]);
        }
    }

    [Fact]
    public async Task TheListHasAViewForEachStateOfAnEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        // Publishing is E3b's verb: the published ones are written as it will leave them.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            database.Events.AddRange(
                Stored("view-draft", PublishStatus.Draft, now.AddDays(10)),
                Stored("view-upcoming", PublishStatus.Published, now.AddDays(10)),
                Stored("view-in-progress", PublishStatus.Published, now.AddHours(-1)),
                Stored("view-ended", PublishStatus.Published, now.AddDays(-3)),
                Stored("view-cancelled", PublishStatus.Published, now.AddDays(10), cancelledAt: now));
            await database.SaveChangesAsync(token);
        }

        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        foreach (var (view, expected) in new[]
        {
            (EventViews.Drafts, "view-draft"),
            (EventViews.Upcoming, "view-upcoming"),
            (EventViews.InProgress, "view-in-progress"),
            (EventViews.Ended, "view-ended"),
            (EventViews.Cancelled, "view-cancelled"),
        })
        {
            var page = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}?q={SlugStem}-view&filter[view]={view}", token), token);
            Assert.Equal([Slug(expected)], page.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("slug").GetString()));
        }

        // Without a view, every one of them; a view that does not exist is a mistake of the caller.
        var all = await OkAsync(await coordinator.GetAsync($"{EventEndpoints.Pattern}?q={SlugStem}-view", token), token);
        Assert.Equal(5, all.GetProperty("total").GetInt32());

        using var unknown = await coordinator.GetAsync($"{EventEndpoints.Pattern}?filter[view]=past", token);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task ThePresetsOfTheKindsReachWhoeverWritesEvents()
    {
        var token = TestContext.Current.CancellationToken;

        try
        {
            using var coordinator = await SignedInAsync(CoordinatorVid, token);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                (await coordinator.GetFromJsonAsync<JsonElement>(SettingsUri, token)).GetRawText())!;
            settings["kindPresets"] = JsonSerializer.SerializeToElement(new[]
            {
                new { kind = "rfo", publicSlots = true, privateSlots = true, hasRoster = true, wholeDivision = false, inPerson = false },
            });

            using (var saved = await coordinator.PutAsJsonAsync(SettingsUri, settings, token))
            {
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            }

            // An advisor of the department does not manage the settings, and writes events: the form of an event reads the presets
            // here, and sets the switches from them when the kind changes.
            using var advisor = await SignedInAsync(AdvisorVid, token);
            using (var settingsRead = await advisor.GetAsync(SettingsUri, token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, settingsRead.StatusCode);
            }

            var presets = await OkAsync(await advisor.GetAsync(EventEndpoints.KindPresetsPattern, token), token);
            var preset = Assert.Single(presets.EnumerateArray());
            Assert.Equal("rfo", preset.GetProperty("kind").GetString());
            Assert.True(preset.GetProperty("publicSlots").GetBoolean());
            Assert.True(preset.GetProperty("privateSlots").GetBoolean());
            Assert.False(preset.GetProperty("wholeDivision").GetBoolean());

            // An event of that kind keeps the switches it is saved with: preset, never imposed.
            var created = await CreatedAsync(advisor, EventEndpoints.Pattern, Payload("preset") with { PublicSlots = true, PrivateSlots = false }, token);
            Assert.True(created.GetProperty("publicSlots").GetBoolean());
            Assert.False(created.GetProperty("privateSlots").GetBoolean());
        }
        finally
        {
            await ForgetSettingsAsync(token);
        }
    }

    [Fact]
    public async Task AnEventChoosesAKindOfTheEventsAndAnyKindWhileTheSettingsListNone()
    {
        // The kinds of the events are the ones with a row in the presets, and every kind of the calendar while there is none (E4,
        // note 2026-10-06-i-tipi-che-un-evento-sceglie, decided by the maintainer on #223).
        var token = TestContext.Current.CancellationToken;
        await ForgetSettingsAsync(token);

        try
        {
            using var coordinator = await SignedInAsync(CoordinatorVid, token);

            // No row at all, as on a new installation: a kind of the calendar that is no event's is still taken.
            var meeting = await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("kinds-meeting") with { Kind = "meeting" }, token);

            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                (await coordinator.GetFromJsonAsync<JsonElement>(SettingsUri, token)).GetRawText())!;
            settings["kindPresets"] = JsonSerializer.SerializeToElement(new[]
            {
                new { kind = "rfo", publicSlots = false, privateSlots = false, hasRoster = false, wholeDivision = false, inPerson = false },
            });

            using (var saved = await coordinator.PutAsJsonAsync(SettingsUri, settings, token))
            {
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            }

            // Listed: a new event of a kind without a row is refused on its field, one with a row — every switch off — is taken.
            using (var refused = await coordinator.PostAsJsonAsync(EventEndpoints.Pattern, Payload("kinds-exam") with { Kind = "exam" }, token))
            {
                Assert.Equal(["events:errors.kindNotOfEvents"], (await RefusalsAsync(refused, token))["kind"]);
            }

            await CreatedAsync(coordinator, EventEndpoints.Pattern, Payload("kinds-listed"), token);

            // The event written before keeps its kind when it is saved again, and does not change to another kind without a row.
            var kept = await OkAsync(await coordinator.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{Id(meeting)}",
                Payload("kinds-meeting") with
                {
                    Kind = "meeting",
                    Title = Text("evt-test-e4 kept"),
                    RowVersion = meeting.GetProperty("rowVersion").GetDateTime(),
                },
                token), token);
            Assert.Equal("meeting", kept.GetProperty("kind").GetString());

            using (var changed = await coordinator.PutAsJsonAsync(
                $"{EventEndpoints.Pattern}/{Id(meeting)}",
                Payload("kinds-meeting") with { Kind = "exam", RowVersion = kept.GetProperty("rowVersion").GetDateTime() },
                token))
            {
                Assert.Equal(["events:errors.kindNotOfEvents"], (await RefusalsAsync(changed, token))["kind"]);
            }

            // A kind the calendar does not have is still refused as one.
            using (var unknown = await coordinator.PostAsJsonAsync(
                EventEndpoints.Pattern,
                Payload("kinds-unknown") with { Kind = "evt-test-no-such-kind" },
                token))
            {
                Assert.Equal(["errors.calendar.kindUnknown"], (await RefusalsAsync(unknown, token))["kind"]);
            }
        }
        finally
        {
            await ForgetSettingsAsync(token);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    /// <summary>A new RFO a month ahead, called after its address, in both languages of the division.</summary>
    private static EventWriteDto Payload(string name)
    {
        var starts = DateTime.UtcNow.Date.AddDays(30).AddHours(17);

        return new EventWriteDto(
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
    }

    private static EventAirportWriteDto Airport(long eventId, string icao, int ordinal) =>
        new(eventId, icao, ordinal, MaxMovementsPerHour: null, MaxArrivalsPerHour: null, MaxDeparturesPerHour: null, RowVersion: default);

    /// <summary>An event as the database holds it, starting at <paramref name="starts"/> and lasting four hours.</summary>
    private static Event Stored(string name, PublishStatus status, DateTime starts, DateTime? cancelledAt = null) => new()
    {
        Slug = Slug(name),
        Kind = "rfo",
        Title = Text(Slug(name)),
        Summary = Text(Slug(name)),
        Status = status,
        PublishedAt = status == PublishStatus.Published ? starts.AddDays(-30) : null,
        StartsAtUtc = starts,
        EndsAtUtc = starts.AddHours(4),
        CancelledAt = cancelledAt,
        CancelledBy = cancelledAt is null ? null : CoordinatorVid,
        CancellationNote = cancelledAt is null ? null : Text("evt-test-e3a cancelled"),
    };

    private static Localized<string> Text(string text) => Text(text, Locales);

    private static Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

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

    /// <summary>A member of the staff with one position, and never an address.</summary>
    private async Task SeedUserAsync(int vid, string position, CancellationToken cancellationToken)
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
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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

    /// <summary>The two airports of the class, in the core's snapshot, where an airport of an event is checked.</summary>
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

    /// <summary>
    /// The events of this class, with their airports and — since E3b, the published ones of <see cref="TheListHasAViewForEachStateOfAnEvent"/>
    /// project themselves — their calendar entries and lines in the search, whatever a run stopped halfway left behind.
    /// </summary>
    private Task ForgetEventsAsync(CancellationToken cancellationToken) =>
        EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

    private async Task ForgetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().DivisionSettings
            .Where(setting => setting.Key == ModuleSettingsStore.SettingsKey(EventsModule.ModuleKey))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
