using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The shape of a tour (M2, T7b), through the real host: a hub tour composed from nothing — two hubs, their rotations
/// and a connection — and marked ready, the "done when" of T7; a container and its subtours, with the dates they take
/// from it; callsign constraints and the template that copies them; and the rows of a tour that take its care before
/// their permission is asked (note 2026-09-21-la-forma-dei-tour).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TourShapeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13); T7b takes 73–75.
    private const int CoordinatorVid = 780073;
    private const int AdvisorVid = 780074;
    private const int EventsVid = 780075;

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _created = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await SeedUserAsync(AdvisorVid, "IT-FOA1", token);
        await SeedUserAsync(EventsVid, "IT-EC", token);
        await FoTestAirports.SeedAsync(_factory.Services, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // Subtours first: a container goes after them. A tour deleted takes its legs, hubs, rotations and constraints.
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var tours = await database.Tours.IgnoreQueryFilters().Where(tour => _created.Contains(tour.Id)).ToListAsync(token);
            database.Tours.RemoveRange(tours.Where(tour => tour.IsSubtour));
            await database.SaveChangesAsync(token);
            database.Tours.RemoveRange(tours.Where(tour => !tour.IsSubtour));
            await database.SaveChangesAsync(token);

            await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
                .Where(grant => grant.Vid == EventsVid)
                .ExecuteDeleteAsync(token);
        }

        await FoTestAirports.RemoveAsync(_factory.Services, token);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AHubTourIsComposedFromNothingAndMarkedReady()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Hub, token);
        var tourId = Id(tour);
        var legs = LegsUri(tourId);

        // Two hubs, each an airport the hub knows, once.
        var rome = await CreatedAsync(advisor, ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, "xfa1", 1, default), token);
        var milan = await CreatedAsync(advisor, ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, Milan, 2, default), token);
        Assert.Equal(Rome, rome.GetProperty("icao").GetString());

        using (var twice = await advisor.PostAsJsonAsync(ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, Rome, 3, default), token))
        {
            await AssertRefusedAsync(twice, "icao", "flightops:errors.hubTaken", token);
        }

        using (var unknown = await advisor.PostAsJsonAsync(ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, "XFZ9", 3, default), token))
        {
            await AssertRefusedAsync(unknown, "icao", "flightops:errors.airportUnknown", token);
        }

        // A rotation of two legs out of each hub; a size the design does not know is refused.
        var fromRome = await CreatedAsync(advisor, ShapeEndpoints.RotationsPattern, new RotationWriteDto(tourId, Id(rome), 1, 2, default), token);
        var fromMilan = await CreatedAsync(advisor, ShapeEndpoints.RotationsPattern, new RotationWriteDto(tourId, Id(milan), 1, 2, default), token);

        using (var three = await advisor.PostAsJsonAsync(ShapeEndpoints.RotationsPattern, new RotationWriteDto(tourId, Id(rome), 2, 3, default), token))
        {
            await AssertRefusedAsync(three, "size", "flightops:errors.rotationSizeChoice", token);
        }

        // The legs, each saying its rotation; the connection between the hubs is a leg of its own.
        await OkAsync(await advisor.PostAsJsonAsync(legs, InRotation(Rome, London, Id(fromRome)), token), token);

        // Half a rotation is not ready, and the list of problems says which one.
        var problems = await ReadyProblemsAsync(advisor, tourId, token);
        Assert.Contains("flightops:errors.rotationSize", Keys(problems, $"rotations.{Rome}.1"));
        Assert.Contains("flightops:errors.rotationSize", Keys(problems, $"rotations.{Milan}.1"));

        await OkAsync(await advisor.PostAsJsonAsync(legs, InRotation(London, Rome, Id(fromRome)), token), token);
        await OkAsync(await advisor.PostAsJsonAsync(legs, Leg(Rome, Milan) with { Kind = LegKind.HubConnection }, token), token);
        await OkAsync(await advisor.PostAsJsonAsync(legs, InRotation(Milan, London, Id(fromMilan)), token), token);
        var grid = await OkAsync(await advisor.PostAsJsonAsync(legs, InRotation(London, Milan, Id(fromMilan)), token), token);

        // The place of each leg in its rotation is the server's, from the order of the tour.
        Assert.Equal(
            [1, 2, null, 1, 2],
            grid.GetProperty("legs").EnumerateArray().Select(row => row.GetProperty("seqInRotation").ValueKind == JsonValueKind.Null
                ? (int?)null
                : row.GetProperty("seqInRotation").GetInt32()));

        // The rotations' list says the hub and how many legs each has.
        var page = await OkAsync(await advisor.GetAsync($"{ShapeEndpoints.RotationsPattern}?filter[tourId]={tourId}", token), token);
        var listed = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([Rome, Milan], listed.Select(row => row.GetProperty("hubIcao").GetString()).Order(StringComparer.Ordinal));
        Assert.All(listed, row => Assert.Equal(2, row.GetProperty("legs").GetInt32()));

        // Ready.
        await OkAsync(await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{tourId}/status", new TourStatusRequest(TourStatusAction.Ready), token), token);

        // Now it keeps its shape: a third hub with nothing to fly is refused, so is a rotation with legs or a hub with
        // rotations deleted.
        using (var third = await advisor.PostAsJsonAsync(ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, London, 3, default), token))
        {
            await AssertRefusedAsync(third, $"hubs.{London}", "flightops:errors.hubWithoutRotations", token);
        }

        using (var rotationWithLegs = await advisor.DeleteAsync($"{ShapeEndpoints.RotationsPattern}/{Id(fromRome)}", token))
        {
            await AssertRefusedAsync(rotationWithLegs, "id", "flightops:errors.rotationHasLegs", token);
        }

        using (var hubWithRotations = await advisor.DeleteAsync($"{ShapeEndpoints.HubsPattern}/{Id(rome)}", token))
        {
            await AssertRefusedAsync(hubWithRotations, "id", "flightops:errors.hubHasRotations", token);
        }
    }

    [Fact]
    public async Task HubsRotationsAndConnectionsBelongToAHubTour()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Sequential, token);
        var tourId = Id(tour);

        using (var hub = await advisor.PostAsJsonAsync(ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, Rome, 1, default), token))
        {
            await AssertRefusedAsync(hub, "tourId", "flightops:errors.kindHasNoHubs", token);
        }

        using (var connection = await advisor.PostAsJsonAsync(LegsUri(tourId), Leg(Rome, Milan) with { Kind = LegKind.HubConnection }, token))
        {
            await AssertRefusedAsync(connection, "kind", "flightops:errors.legHubOnly", token);
        }

        using (var missing = await advisor.PostAsJsonAsync(ShapeEndpoints.HubsPattern, new HubWriteDto(long.MaxValue, Rome, 1, default), token))
        {
            await AssertRefusedAsync(missing, "tourId", "flightops:errors.tourUnknown", token);
        }
    }

    [Fact]
    public async Task TheRowsOfATourAreInItsCareBeforeTheirPermissionIsAsked()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Hub, token);
        var tourId = Id(tour);

        // The events department takes care of this tour too, and one of its staff is granted the tours of that care.
        var both = DepartmentMask.Of([Department.FOD, Department.ED]);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>().Tours.IgnoreQueryFilters()
                .Where(row => row.Id == tourId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.OwnerDepartmentMask, both), token);
            await GrantAsync(scope, EventsVid, Department.ED, token);
        }

        // Asked on the base department alone, this would be refused: the hub is the tour's.
        using var events = await SignedInAsync(_factory, EventsVid, token);
        var hub = await CreatedAsync(events, ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, Rome, 1, default), token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            Assert.Equal(both, (await database.Hubs.IgnoreQueryFilters().SingleAsync(row => row.Id == Id(hub), token)).OwnerDepartmentMask);

            // And when the tour's care narrows, the rows follow at its next save.
            await database.Tours.IgnoreQueryFilters()
                .Where(row => row.Id == tourId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.OwnerDepartmentMask, DepartmentMask.Of(Department.FOD)), token);
        }

        var current = await OkAsync(await advisor.GetAsync($"{TourEndpoints.Pattern}/{tourId}", token), token);
        await OkAsync(await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{tourId}", Payload(current), token), token);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            Assert.Equal(
                DepartmentMask.Of(Department.FOD),
                (await database.Hubs.IgnoreQueryFilters().SingleAsync(row => row.Id == Id(hub), token)).OwnerDepartmentMask);
        }

        // Out of the tour's care now, the events staff member no longer reaches its hubs.
        using var refused = await events.PostAsJsonAsync(ShapeEndpoints.HubsPattern, new HubWriteDto(tourId, Milan, 2, default), token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task AContainerHasSubtoursThatTakeItsDatesUntilTheyHaveTheirOwn()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var container = await CreatedTourAsync(advisor, TourKind.Container, token);
        var containerId = Id(container);

        // A subtour without dates takes the container's; one with a release of its own keeps it and takes the close.
        var first = await CreatedTourAsync(advisor, TourKind.Sequential, token, parent: containerId);
        var ownRelease = DateTime.Parse(container.GetProperty("releaseAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal).AddDays(10);
        var second = await CreatedTourAsync(advisor, TourKind.Free, token, parent: containerId, release: ownRelease);

        // Read back as stored: the answer to a create carries the clock's seventh digit, the column keeps six.
        container = await OkAsync(await advisor.GetAsync($"{TourEndpoints.Pattern}/{containerId}", token), token);
        Assert.True(first.GetProperty("releaseFromParent").GetBoolean());
        Assert.True(first.GetProperty("closeFromParent").GetBoolean());
        Assert.Equal(container.GetProperty("releaseAt").GetString(), first.GetProperty("releaseAt").GetString());
        Assert.False(second.GetProperty("releaseFromParent").GetBoolean());
        Assert.True(second.GetProperty("closeFromParent").GetBoolean());
        Assert.Equal(container.GetProperty("closeAt").GetString(), second.GetProperty("closeAt").GetString());

        // A subtour is one level down, has no award, and hangs off a container.
        using (var nested = await advisor.PostAsJsonAsync(TourEndpoints.Pattern, TourPayload(TourKind.Container, parent: containerId), token))
        {
            await AssertRefusedAsync(nested, "kind", "flightops:errors.subtourNotContainer", token);
        }

        using (var underASequence = await advisor.PostAsJsonAsync(TourEndpoints.Pattern, TourPayload(TourKind.Free, parent: Id(first)), token))
        {
            await AssertRefusedAsync(underASequence, "parentTourId", "flightops:errors.parentUnknown", token);
        }

        // The list of tours shows the container, not its subtours; the container's tab asks for them.
        var list = await OkAsync(await advisor.GetAsync($"{TourEndpoints.Pattern}?q={container.GetProperty("slug").GetString()}", token), token);
        Assert.Equal([containerId], list.GetProperty("items").EnumerateArray().Select(Id));
        var subtours = await OkAsync(await advisor.GetAsync($"{TourEndpoints.Pattern}?filter[parent]={containerId}", token), token);
        Assert.Equal([Id(first), Id(second)], subtours.GetProperty("items").EnumerateArray().Select(Id).Order());

        // Ready once it asks for no more subtours than it has.
        var problems = await ReadyProblemsAsync(advisor, containerId, token);
        Assert.Contains("errors.required", Keys(problems, "requiredSubtours"));

        var payload = Payload(container) with { RequiredSubtours = 3 };
        container = await OkAsync(await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{containerId}", payload, token), token);
        Assert.Contains("flightops:errors.requiredSubtoursTooMany", Keys(await ReadyProblemsAsync(advisor, containerId, token), "requiredSubtours"));

        container = await OkAsync(await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{containerId}", Payload(container) with { RequiredSubtours = 2 }, token), token);
        container = await OkAsync(await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{containerId}/status", new TourStatusRequest(TourStatusAction.Ready), token), token);

        // The container's close moves, and the subtours that take it follow.
        var later = DateTime.Parse(container.GetProperty("closeAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal).AddDays(30);
        container = await OkAsync(
            await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{containerId}", Payload(container) with { CloseAt = later }, token),
            token);
        var followed = await OkAsync(await advisor.GetAsync($"{TourEndpoints.Pattern}/{Id(first)}", token), token);
        Assert.Equal(container.GetProperty("closeAt").GetString(), followed.GetProperty("closeAt").GetString());

        // A date of its own past the container's is a problem of the subtour.
        var beyond = later.AddDays(1);
        var outside = await OkAsync(
            await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{Id(first)}", Payload(followed) with { CloseAt = beyond }, token),
            token);
        Assert.False(outside.GetProperty("closeFromParent").GetBoolean());
        Assert.Contains("flightops:errors.subtourOutsideParent", Keys(await ReadyProblemsAsync(advisor, Id(first), token), "closeAt"));

        // A container with subtours keeps its kind and is not deleted before them.
        using (var kind = await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{containerId}", Payload(container) with { Kind = TourKind.Free }, token))
        {
            // Ready and not yet released: the kind is still free to change, the subtours are what holds it.
            await AssertRefusedAsync(kind, "kind", "flightops:errors.containerHasSubtours", token);
        }

        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using (var deleted = await coordinator.DeleteAsync($"{TourEndpoints.Pattern}/{containerId}", token))
        {
            await AssertRefusedAsync(deleted, "id", "flightops:errors.containerHasSubtours", token);
        }
    }

    [Fact]
    public async Task CallsignConstraintsNameAnAirlineOrDenyACallsignAndTravelWithATemplate()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Sequential, token);
        var tourId = Id(tour);
        await AddLegAsync(advisor, tourId, token);
        var legId = Id((await OkAsync(await advisor.GetAsync(LegsUri(tourId), token), token)).GetProperty("legs")[0]);

        await CreatedAsync(advisor, ShapeEndpoints.CallsignRulesPattern, Rule(tourId, null, CallsignMode.Allow, CallsignMatch.Airline, "ity"), token);
        await CreatedAsync(advisor, ShapeEndpoints.CallsignRulesPattern, Rule(tourId, legId, CallsignMode.Deny, CallsignMatch.Exact, "IHX870"), token);

        // Only a deny names a whole callsign; an airline is three letters; a leg is one of the tour's.
        using (var exactAllow = await advisor.PostAsJsonAsync(ShapeEndpoints.CallsignRulesPattern, Rule(tourId, null, CallsignMode.Allow, CallsignMatch.Exact, "ITY123"), token))
        {
            await AssertRefusedAsync(exactAllow, "match", "flightops:errors.exactOnlyDenies", token);
        }

        using (var longAirline = await advisor.PostAsJsonAsync(ShapeEndpoints.CallsignRulesPattern, Rule(tourId, null, CallsignMode.Allow, CallsignMatch.Airline, "ITY1"), token))
        {
            await AssertRefusedAsync(longAirline, "value", "flightops:errors.airlineCode", token);
        }

        using (var otherLeg = await advisor.PostAsJsonAsync(ShapeEndpoints.CallsignRulesPattern, Rule(tourId, long.MaxValue, CallsignMode.Deny, CallsignMatch.Airline, "AZA"), token))
        {
            await AssertRefusedAsync(otherLeg, "legId", "flightops:errors.legUnknown", token);
        }

        var page = await OkAsync(await advisor.GetAsync($"{ShapeEndpoints.CallsignRulesPattern}?filter[tourId]={tourId}", token), token);
        var rows = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["ITY", "IHX870"], rows.Select(row => row.GetProperty("value").GetString()));
        Assert.Equal(1, rows[1].GetProperty("legNumber").GetInt32());

        // A template takes the tour's constraint, not the leg's: it has no legs.
        using var saved = await coordinator.PostAsJsonAsync(
            $"{TourEndpoints.Pattern}/{tourId}/save-as-template",
            new TourSaveAsTemplateRequest(Text("fo-test-shape template", Locales)),
            token);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var template = await saved.Content.ReadFromJsonAsync<JsonElement>(token);
        _created.Add(Id(template));

        var copied = await OkAsync(await coordinator.GetAsync($"{ShapeEndpoints.CallsignRulesPattern}?filter[tourId]={Id(template)}", token), token);
        Assert.Equal(["ITY"], copied.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("value").GetString()));

        // And a template's constraints are changed with the template's own permission, which an advisor has not.
        using var byAdvisor = await advisor.PostAsJsonAsync(
            ShapeEndpoints.CallsignRulesPattern,
            Rule(Id(template), null, CallsignMode.Deny, CallsignMatch.Airline, "AZA"),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, byAdvisor.StatusCode);
    }

    private static CallsignRuleWriteDto Rule(long tourId, long? legId, CallsignMode mode, CallsignMatch match, string value) =>
        new(tourId, legId, mode, match, value, default);

    private static LegWriteDto InRotation(string departure, string arrival, long rotationId) =>
        Leg(departure, arrival) with { RotationId = rotationId };

    private async Task<JsonElement> CreatedTourAsync(
        HttpClient client,
        TourKind kind,
        CancellationToken cancellationToken,
        long? parent = null,
        DateTime? release = null)
    {
        using var response = await client.PostAsJsonAsync(TourEndpoints.Pattern, TourPayload(kind, parent, release), cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _created.Add(Id(body));
        return body;
    }

    /// <summary>A new tour; a subtour takes the container's dates unless it is handed one.</summary>
    private static TourWriteDto TourPayload(TourKind kind, long? parent = null, DateTime? release = null)
    {
        var now = DateTime.UtcNow;
        var slug = $"fo-test-shape-{Guid.NewGuid():N}"[..28];

        return new TourWriteDto(
            OwnerDepartment: Department.FOD,
            IsTemplate: false,
            Slug: slug,
            Kind: kind,
            Title: Text(slug, Locales),
            Summary: Text(slug, Locales),
            Briefing: null,
            CoverMediaId: null,
            BannerMediaId: null,
            ShowPreview: false,
            ReleaseAt: parent is null ? now.AddDays(5) : release,
            CloseAt: parent is null ? now.AddDays(60) : null,
            ReportWindowDays: null,
            Progression: TourProgression.FlyAhead,
            HubRotationOrder: null,
            RequiresProcedures: false,
            DailyLegLimit: 5,
            MinPilotRating: null,
            ReferenceAircraftIcao: null,
            RequiredNm: null,
            AllowedAircraft: null,
            AwardId: null,
            RowVersion: default,
            ParentTourId: parent);
    }

    /// <summary>The tour as its editor would send it back: a subtour's inherited date stays empty.</summary>
    private static TourWriteDto Payload(JsonElement tour)
    {
        DateTime? Date(string name, string inherited) =>
            tour.GetProperty(name).ValueKind == JsonValueKind.Null || (tour.TryGetProperty(inherited, out var from) && from.GetBoolean())
                ? null
                : DateTime.Parse(tour.GetProperty(name).GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        return new TourWriteDto(
            OwnerDepartment: Enum.Parse<Department>(tour.GetProperty("ownerDepartment").GetString()!),
            IsTemplate: false,
            Slug: tour.GetProperty("slug").GetString(),
            Kind: Enum.Parse<TourKind>(tour.GetProperty("kind").GetString()!),
            Title: Text(tour.GetProperty("slug").GetString()!, Locales),
            Summary: Text(tour.GetProperty("slug").GetString()!, Locales),
            Briefing: null,
            CoverMediaId: null,
            BannerMediaId: null,
            ShowPreview: false,
            ReleaseAt: Date("releaseAt", "releaseFromParent"),
            CloseAt: Date("closeAt", "closeFromParent"),
            ReportWindowDays: tour.GetProperty("reportWindowDays").GetInt32(),
            Progression: TourProgression.FlyAhead,
            HubRotationOrder: null,
            RequiresProcedures: false,
            DailyLegLimit: 5,
            MinPilotRating: null,
            ReferenceAircraftIcao: null,
            RequiredNm: null,
            AllowedAircraft: null,
            AwardId: null,
            RowVersion: tour.GetProperty("rowVersion").GetDateTime(),
            ParentTourId: tour.GetProperty("parentTourId").ValueKind == JsonValueKind.Null ? null : tour.GetProperty("parentTourId").GetInt64(),
            RequiredSubtours: tour.GetProperty("requiredSubtours").ValueKind == JsonValueKind.Null ? null : tour.GetProperty("requiredSubtours").GetInt32());
    }

    private static async Task<JsonElement> ReadyProblemsAsync(HttpClient client, long tourId, CancellationToken cancellationToken) =>
        await OkAsync(await client.GetAsync($"{TourEndpoints.Pattern}/{tourId}/ready-problems", cancellationToken), cancellationToken);

    private static async Task<JsonElement> CreatedAsync<T>(HttpClient client, string uri, T payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static Core.Localization.Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static IEnumerable<string?> Keys(JsonElement problems, string field) =>
        problems.GetProperty("errors").TryGetProperty(field, out var keys) ? keys.EnumerateArray().Select(key => key.GetString()) : [];

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

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");
        var problem = JsonDocument.Parse(text).RootElement;
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var keys), text);
        Assert.Contains(key, keys.EnumerateArray().Select(error => error.GetString()));
    }

    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> factory, int vid, CancellationToken cancellationToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(
            new Uri(string.Create(CultureInfo.InvariantCulture, $"{TestSignInStartupFilter.Path}?vid={vid}"), UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>Tours of this department, to a member of it who holds no tours of their own.</summary>
    private static async Task GrantAsync(AsyncServiceScope scope, int vid, Department department, CancellationToken cancellationToken)
    {
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        foreach (var permission in new[] { TourPermissions.View, TourPermissions.Edit })
        {
            database.UserGrants.Add(new UserGrant
            {
                Vid = vid,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = department,
                Effect = GrantEffect.Grant,
                Reason = "fo-test",
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

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
        user.LastName = "Shape";
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
}
