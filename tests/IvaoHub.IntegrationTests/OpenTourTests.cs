using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The <c>Open</c> tour (M2, T7c), through the real host: composed from nothing — a goal, two filters and a sequence rule
/// — and marked ready, the "done when" of T7c; a wrong parameter refused on its field; constraints only on an <c>Open</c>
/// tour, once per kind, and carried by a template (note 2026-09-22-il-tour-open).
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class OpenTourTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13); T7c takes 76–77.
    private const int CoordinatorVid = 780076;
    private const int AdvisorVid = 780077;

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _created = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await SeedUserAsync(AdvisorVid, "IT-FOA1", token);
        await FoTestAirports.SeedAsync(_factory.Services, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A tour deleted takes its constraints.
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await database.Tours.IgnoreQueryFilters().Where(tour => _created.Contains(tour.Id)).ExecuteDeleteAsync(token);
        }

        await FoTestAirports.RemoveAsync(_factory.Services, token);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AnOpenTourIsComposedFromNothingAndMarkedReady()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Open, token);
        var tourId = Id(tour);

        // No goal yet: not ready, and the list of problems says so.
        Assert.Contains("errors.required", Keys(await ReadyProblemsAsync(advisor, tourId, token), "openGoal"));

        // A goal that names an airport the hub does not know is refused on the field.
        using (var unknown = await advisor.PutAsJsonAsync(
            TourUri(tourId),
            Payload(tour) with { OpenGoal = OpenGoal.CollectList, OpenGoalParameters = Parameters($$"""{"airports":["{{Rome}}","XFZ9"]}""") },
            token))
        {
            await AssertRefusedAsync(unknown, "openGoalParameters.airports", "flightops:errors.airportUnknown", token);
        }

        // Two of three airports, written loosely, and kept as the server writes them.
        tour = await OkAsync(await advisor.PutAsJsonAsync(
            TourUri(tourId),
            Payload(tour) with
            {
                OpenGoal = OpenGoal.CollectList,
                OpenGoalParameters = Parameters($$"""{"airports":[" {{Rome.ToLowerInvariant()}}","{{Milan}}","{{London}}"],"count":2,"nm":5}"""),
            },
            token), token);
        Assert.Equal("CollectList", tour.GetProperty("openGoal").GetString());
        Assert.Equal(
            $$"""{"airports":["{{Rome}}","{{Milan}}","{{London}}"],"count":2}""",
            tour.GetProperty("openGoalParameters").GetRawText());

        // A save of the settings alone keeps the goal.
        tour = await OkAsync(await advisor.PutAsJsonAsync(TourUri(tourId), Payload(tour), token), token);
        Assert.Equal("CollectList", tour.GetProperty("openGoal").GetString());

        // Two filters and a sequence rule; a wrong parameter is refused on its field.
        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.DepartureOrArrivalIn, """{"countries":["xx"]}"""), token);

        using (var inverted = await advisor.PostAsJsonAsync(
            ShapeEndpoints.ConstraintsPattern,
            Constraint(tourId, TourConstraintKind.DistanceBetween, """{"minNm":1500,"maxNm":200}"""),
            token))
        {
            await AssertRefusedAsync(inverted, "parameters.maxNm", "flightops:errors.distanceBounds", token);
        }

        using (var nowhere = await advisor.PostAsJsonAsync(
            ShapeEndpoints.ConstraintsPattern,
            Constraint(tourId, TourConstraintKind.ArrivalIn, """{"countries":["ZZ"]}"""),
            token))
        {
            await AssertRefusedAsync(nowhere, "parameters.countries", "flightops:errors.countryUnknown", token);
        }

        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.DistanceBetween, """{"minNm":200,"maxNm":1500}"""), token);
        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.Chained, "{}"), token);

        // Once per kind; once per airport for the flights at an airport.
        using (var twice = await advisor.PostAsJsonAsync(
            ShapeEndpoints.ConstraintsPattern,
            Constraint(tourId, TourConstraintKind.DistanceBetween, """{"maxNm":900}"""),
            token))
        {
            await AssertRefusedAsync(twice, "kind", "flightops:errors.constraintTaken", token);
        }

        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.MinFlightsAt, $$"""{"airport":"{{Rome}}","count":2}"""), token);
        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.MinFlightsAt, $$"""{"airport":"{{Milan}}","count":1}"""), token);

        using (var sameAirport = await advisor.PostAsJsonAsync(
            ShapeEndpoints.ConstraintsPattern,
            Constraint(tourId, TourConstraintKind.MinFlightsAt, $$"""{"airport":"{{Rome}}","count":3}"""),
            token))
        {
            await AssertRefusedAsync(sameAirport, "parameters.airport", "flightops:errors.constraintTaken", token);
        }

        // The list shows each row's values.
        var page = await OkAsync(await advisor.GetAsync($"{ShapeEndpoints.ConstraintsPattern}?filter[tourId]={tourId}", token), token);
        var rows = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(
            ["DepartureOrArrivalIn", "DistanceBetween", "Chained", "MinFlightsAt", "MinFlightsAt"],
            rows.Select(row => row.GetProperty("kind").GetString()));
        Assert.Equal(["≥ 200 NM", "≤ 1500 NM"], rows[1].GetProperty("values").EnumerateArray().Select(value => value.GetString()));

        // Ready.
        await OkAsync(await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{tourId}/status", new TourStatusRequest(TourStatusAction.Ready), token), token);

        // A ready tour keeps what could be marked ready: east and west together are refused.
        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.Eastbound, "{}"), token);
        using (var west = await advisor.PostAsJsonAsync(ShapeEndpoints.ConstraintsPattern, Constraint(tourId, TourConstraintKind.Westbound, "{}"), token))
        {
            await AssertRefusedAsync(west, "constraints", "flightops:errors.constraintsContradict", token);
        }
    }

    [Fact]
    public async Task ConstraintsBelongToAnOpenTourAndTravelWithATemplate()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        // A tour with legs has none.
        var sequential = await CreatedTourAsync(advisor, TourKind.Sequential, token);
        using (var refused = await advisor.PostAsJsonAsync(
            ShapeEndpoints.ConstraintsPattern,
            Constraint(Id(sequential), TourConstraintKind.Chained, "{}"),
            token))
        {
            await AssertRefusedAsync(refused, "tourId", "flightops:errors.kindHasNoConstraints", token);
        }

        var open = await CreatedTourAsync(advisor, TourKind.Open, token);
        var openId = Id(open);
        open = await OkAsync(await advisor.PutAsJsonAsync(
            TourUri(openId),
            Payload(open) with { OpenGoal = OpenGoal.Distance, OpenGoalParameters = Parameters("""{"nm":21600}""") },
            token), token);
        await CreatedAsync(advisor, ShapeEndpoints.ConstraintsPattern, Constraint(openId, TourConstraintKind.Eastbound, "{}"), token);

        // An Open tour with constraints keeps its kind (note 2026-09-22-il-tour-open §2.4).
        using (var changed = await advisor.PutAsJsonAsync(TourUri(openId), Payload(open) with { Kind = TourKind.Free }, token))
        {
            await AssertRefusedAsync(changed, "kind", "flightops:errors.openHasConstraints", token);
        }

        // A template carries the goal and the constraints.
        using var saved = await coordinator.PostAsJsonAsync(
            $"{TourEndpoints.Pattern}/{openId}/save-as-template",
            new TourSaveAsTemplateRequest(Text("fo-test-open template", Locales)),
            token);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var template = await saved.Content.ReadFromJsonAsync<JsonElement>(token);
        _created.Add(Id(template));

        Assert.Equal("Distance", template.GetProperty("openGoal").GetString());
        var copied = await OkAsync(await coordinator.GetAsync($"{ShapeEndpoints.ConstraintsPattern}?filter[tourId]={Id(template)}", token), token);
        Assert.Equal(["Eastbound"], copied.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("kind").GetString()));

        // Without constraints the kind changes, and the goal goes with it.
        var other = await CreatedTourAsync(advisor, TourKind.Open, token);
        other = await OkAsync(await advisor.PutAsJsonAsync(
            TourUri(Id(other)),
            Payload(other) with { OpenGoal = OpenGoal.FlightCount, OpenGoalParameters = Parameters("""{"count":10}""") },
            token), token);
        other = await OkAsync(await advisor.PutAsJsonAsync(TourUri(Id(other)), Payload(other) with { Kind = TourKind.Free }, token), token);
        Assert.Equal(JsonValueKind.Null, other.GetProperty("openGoal").ValueKind);
        Assert.Equal(JsonValueKind.Null, other.GetProperty("openGoalParameters").ValueKind);
    }

    private static TourConstraintWriteDto Constraint(long tourId, TourConstraintKind kind, string parameters) =>
        new(tourId, kind, Parameters(parameters), default);

    private static JsonObject Parameters(string json) => JsonNode.Parse(json)!.AsObject();

    private static string TourUri(long tourId) => $"{TourEndpoints.Pattern}/{tourId}";

    private async Task<JsonElement> CreatedTourAsync(HttpClient client, TourKind kind, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var slug = $"fo-test-open-{Guid.NewGuid():N}"[..27];
        var payload = new TourWriteDto(
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
            ReleaseAt: now.AddDays(5),
            CloseAt: now.AddDays(60),
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
            RowVersion: default);

        var body = await CreatedAsync(client, TourEndpoints.Pattern, payload, cancellationToken);
        _created.Add(Id(body));
        return body;
    }

    /// <summary>The tour as its settings form would send it back: without the goal, which that form does not hold.</summary>
    private static TourWriteDto Payload(JsonElement tour) => new(
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
        ReleaseAt: DateTime.Parse(tour.GetProperty("releaseAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
        CloseAt: DateTime.Parse(tour.GetProperty("closeAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
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
        RowVersion: tour.GetProperty("rowVersion").GetDateTime());

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
        user.LastName = "Open";
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
