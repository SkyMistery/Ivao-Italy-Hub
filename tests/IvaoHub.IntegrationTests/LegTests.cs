using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The legs of a tour (M2, T7a), through the real host: measured and numbered by the server, deleted and renumbered
/// without touching a leg a report points at, retired and restored with a reason, a rotation retired whole, a leg with
/// reports changed only with a reason; and a ready tour that keeps its shape, with the estimated times of its
/// reference aircraft.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class LegTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13); T7 takes 7xx.
    private const int CoordinatorVid = 780071;
    private const int AdvisorVid = 780072;

    private const string TestType = "XT7A";

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _created = [];

    public async ValueTask InitializeAsync()
    {
        // The fixtures of IVAO, so fetching the runways of a leg's airports never leaves the machine.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await SeedUserAsync(AdvisorVid, "IT-FOA1", token);
        await FoTestAirports.SeedAsync(_factory.Services, token);
        await SeedAircraftTypeAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A tour deleted takes its legs with it.
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            database.Tours.RemoveRange(await database.Tours.IgnoreQueryFilters()
                .Where(tour => _created.Contains(tour.Id))
                .ToListAsync(token));
            database.AircraftProfiles.RemoveRange(await database.AircraftProfiles.IgnoreQueryFilters()
                .Where(profile => profile.IcaoType == TestType)
                .ToListAsync(token));
            await database.SaveChangesAsync(token);

            await scope.ServiceProvider.GetRequiredService<HubDbContext>().IvaoAircraftTypes
                .Where(type => type.IcaoCode == TestType)
                .ExecuteDeleteAsync(token);
        }

        await FoTestAirports.RemoveAsync(_factory.Services, token);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task LegsAreMeasuredNumberedRemovedAndRestoredByTheRulesOfReports()
    {
        var token = TestContext.Current.CancellationToken;
        var reported = new ReportedLegs();
        await using var withReports = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ITourReports>(reported)));
        using var advisor = await SignedInAsync(withReports, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Sequential, token);
        var legs = LegsUri(Id(tour));

        // Three legs at the end, measured by the server: Rome to Milan is 253.9 NM.
        var grid = await OkAsync(await advisor.PostAsJsonAsync(legs, Leg("xfa1", "XFA2"), token), token);
        grid = await OkAsync(await advisor.PostAsJsonAsync(legs, Leg("XFA2", "XFA3"), token), token);
        grid = await OkAsync(await advisor.PostAsJsonAsync(legs, Leg("XFA3", "XFA1"), token), token);

        var rows = Rows(grid);
        Assert.Equal([1, 2, 3], rows.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Equal(253.9m, rows[0].GetProperty("distanceNm").GetDecimal());
        Assert.Equal("XFA1", rows[0].GetProperty("departureIcao").GetString());
        Assert.Equal("XA1", rows[0].GetProperty("departureIata").GetString());

        // Without a reference aircraft there are no estimates.
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("estimatedMinutes").ValueKind);
        Assert.Equal(JsonValueKind.Null, grid.GetProperty("totalEstimatedMinutes").ValueKind);

        // An airport the hub does not know is refused on its cell.
        using (var unknown = await advisor.PostAsJsonAsync(legs, Leg("XFA1", "XFZ9"), token))
        {
            await AssertRefusedAsync(unknown, "arrivalIcao", "flightops:errors.airportUnknown", token);
        }

        // "Duplicate" after the first: the new leg is number 2, and the ones after it move down.
        var first = Id(rows[0]);
        grid = await OkAsync(await advisor.PostAsJsonAsync($"{legs}?after={first}", Leg("XFA1", "XFA2"), token), token);
        rows = Rows(grid);
        Assert.Equal(["XFA1", "XFA1", "XFA2", "XFA3"], rows.Select(row => row.GetProperty("departureIcao").GetString()));
        Assert.Equal([1, 2, 3, 4], rows.Select(row => row.GetProperty("number").GetInt32()));

        // A report points at the third leg (Milan to London), by its identifier.
        var withReport = Id(rows[2]);
        reported.Legs.Add(withReport);

        // Changed only with a reason, which the row keeps for the audit.
        using (var noReason = await advisor.PutAsJsonAsync($"{legs}/{withReport}", Leg("XFA2", "XFA3") with { FlightNumbers = ["az 200", "AZ 202", "AZ 200"] }, token))
        {
            await AssertRefusedAsync(noReason, "changeReason", "flightops:errors.changeReasonRequired", token);
        }

        grid = await OkAsync(
            await advisor.PutAsJsonAsync(
                $"{legs}/{withReport}",
                Leg("XFA2", "XFA3") with { FlightNumbers = ["az 200", "AZ 202", "AZ 200"], ChangeReason = "fo-test real flight number" },
                token),
            token);
        // Upper case, each once, in order: two suggestions of a route flown twice a day.
        Assert.Equal(
            ["AZ 200", "AZ 202"],
            Row(grid, withReport).GetProperty("flightNumbers").EnumerateArray().Select(number => number.GetString()));
        Assert.True(Row(grid, withReport).GetProperty("hasReports").GetBoolean());

        // The first leg has no report: deleted, and every leg after it renumbered — the reported one keeps its identity.
        var removal = await advisor.GetFromJsonAsync<JsonElement>($"{legs}/{first}/removal", token);
        Assert.Equal("Delete", removal.GetProperty("outcome").GetString());

        grid = await OkAsync(await advisor.PostAsJsonAsync($"{legs}/{first}/remove", new { reason = (string?)null }, token), token);
        rows = Rows(grid);
        Assert.Equal(3, rows.Count);
        Assert.Equal([1, 2, 3], rows.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Equal(2, Row(grid, withReport).GetProperty("number").GetInt32());

        // The reported leg is retired, never deleted, and only with a reason.
        removal = await advisor.GetFromJsonAsync<JsonElement>($"{legs}/{withReport}/removal", token);
        Assert.Equal("Retire", removal.GetProperty("outcome").GetString());
        Assert.Equal([2], removal.GetProperty("numbers").EnumerateArray().Select(number => number.GetInt32()));

        using (var noReason = await advisor.PostAsJsonAsync($"{legs}/{withReport}/remove", new { reason = " " }, token))
        {
            await AssertRefusedAsync(noReason, "reason", "flightops:errors.retireReasonRequired", token);
        }

        var totalBefore = grid.GetProperty("totalNm").GetDecimal();
        grid = await OkAsync(await advisor.PostAsJsonAsync($"{legs}/{withReport}/remove", new { reason = "fo-test airport closed" }, token), token);
        Assert.Equal(3, Rows(grid).Count);
        Assert.Equal("fo-test airport closed", Row(grid, withReport).GetProperty("retiredReason").GetString());
        Assert.True(grid.GetProperty("totalNm").GetDecimal() < totalBefore);

        // Back in the tour, with a reason, while the tour is not closed.
        using (var noReason = await advisor.PostAsJsonAsync($"{legs}/{withReport}/restore", new { reason = "" }, token))
        {
            await AssertRefusedAsync(noReason, "reason", "flightops:errors.restoreReasonRequired", token);
        }

        grid = await OkAsync(await advisor.PostAsJsonAsync($"{legs}/{withReport}/restore", new { reason = "fo-test reopened" }, token), token);
        Assert.Equal(JsonValueKind.Null, Row(grid, withReport).GetProperty("retiredAt").ValueKind);
        Assert.Equal(totalBefore, grid.GetProperty("totalNm").GetDecimal());

        // A rotation is never retired one leg at a time. The rule is the server's whatever the kind, so the rotation is
        // written straight into the tables here; composing a hub tour through its screens is T7b's test (TourShapeTests).
        var others = Rows(grid).Select(Id).Where(id => id != withReport).ToArray();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            var hub = new TourHub { TourId = Id(tour), Icao = Rome, OwnerDepartment = Department.FOD, OwnerDepartmentMask = DepartmentMask.Of(Department.FOD) };
            database.Hubs.Add(hub);
            await database.SaveChangesAsync(token);
            var rotation = new Rotation { TourId = Id(tour), HubId = hub.Id, Size = 2, OwnerDepartment = Department.FOD, OwnerDepartmentMask = DepartmentMask.Of(Department.FOD) };
            database.Rotations.Add(rotation);
            await database.SaveChangesAsync(token);

            var rotated = new[] { withReport, others[0] };
            await database.Legs.Where(leg => rotated.Contains(leg.Id))
                .ExecuteUpdateAsync(update => update.SetProperty(leg => leg.RotationId, rotation.Id), token);
        }

        removal = await advisor.GetFromJsonAsync<JsonElement>($"{legs}/{withReport}/removal", token);
        Assert.Equal("RetireRotation", removal.GetProperty("outcome").GetString());
        Assert.Equal(2, removal.GetProperty("numbers").GetArrayLength());

        grid = await OkAsync(await advisor.PostAsJsonAsync($"{legs}/{withReport}/remove", new { reason = "fo-test rotation replaced" }, token), token);
        Assert.Equal(2, Rows(grid).Count(row => row.GetProperty("retiredAt").ValueKind != JsonValueKind.Null));
    }

    [Fact]
    public async Task AReadyTourKeepsItsShapeAndShowsTheEstimatedTimes()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);

        // The reference aircraft flies at 450 kt; the division's numbers are the defaults, 5 % and 15 minutes (T18).
        using (var profile = await coordinator.PostAsJsonAsync(
            AircraftEndpoints.ProfilesPattern,
            new AircraftProfileWriteDto(Department.FOD, TestType, 450, "fo-test", default),
            token))
        {
            Assert.Equal(HttpStatusCode.Created, profile.StatusCode);
        }

        var tour = await CreatedTourAsync(coordinator, TourKind.SequentialChosenStart, token, TestType);
        var legs = LegsUri(Id(tour));

        // No legs, no ready: the problem is said before anybody presses.
        var problems = await coordinator.GetFromJsonAsync<JsonElement>($"{TourEndpoints.Pattern}/{Id(tour)}/ready-problems", token);
        Assert.Contains("flightops:errors.noLegs", Keys(problems, "legs"));

        await OkAsync(await coordinator.PostAsJsonAsync(legs, Leg("XFA1", "XFA2"), token), token);
        var grid = await OkAsync(await coordinator.PostAsJsonAsync(legs, Leg("XFA2", "XFA3"), token), token);

        // 253.9 NM at 450 kt: 60 × 253.9 × 1.05 / 450 + 15 = 50.5 → 51 minutes.
        Assert.Equal(51, Rows(grid)[0].GetProperty("estimatedMinutes").GetInt32());
        Assert.Equal(
            Rows(grid).Sum(row => row.GetProperty("estimatedMinutes").GetInt32()),
            grid.GetProperty("totalEstimatedMinutes").GetInt32());

        // A start anywhere asks for a ring.
        using (var notARing = await coordinator.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Ready" }, token))
        {
            await AssertRefusedAsync(notARing, "legs", "flightops:errors.notARing", token);
        }

        grid = await OkAsync(await coordinator.PostAsJsonAsync(legs, Leg("XFA3", "XFA1"), token), token);
        await OkAsync(await coordinator.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Ready" }, token), token);

        // Ready, it stays a ring: a leg that breaks it is refused, and so is removing one.
        var last = Rows(grid)[2];
        using (var broken = await coordinator.PutAsJsonAsync($"{legs}/{Id(last)}", Leg("XFA3", "XFA2") with { RowVersion = last.GetProperty("rowVersion").GetDateTime() }, token))
        {
            await AssertRefusedAsync(broken, "legs", "flightops:errors.notARing", token);
        }

        using (var removed = await coordinator.PostAsJsonAsync($"{legs}/{Id(last)}/remove", new { reason = (string?)null }, token))
        {
            await AssertRefusedAsync(removed, "legs", "flightops:errors.notARing", token);
        }

        // A release of its own outside the tour's period is refused on the leg's number.
        using (var early = await coordinator.PutAsJsonAsync(
            $"{legs}/{Id(last)}",
            Leg("XFA3", "XFA1") with { ReleaseAt = DateTime.UtcNow.AddYears(-1) },
            token))
        {
            await AssertRefusedAsync(early, "legs.3", "flightops:errors.legReleaseOutsidePeriod", token);
        }

        // A stale version is a conflict, not a silent overwrite.
        using (var stale = await coordinator.PutAsJsonAsync(
            $"{legs}/{Id(last)}",
            Leg("XFA3", "XFA1") with { FlightNumbers = ["X1"], RowVersion = last.GetProperty("rowVersion").GetDateTime().AddSeconds(-5) },
            token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        // An Open tour has no legs to write.
        var open = await CreatedTourAsync(coordinator, TourKind.Open, token);
        using (var refused = await coordinator.PostAsJsonAsync(LegsUri(Id(open)), Leg("XFA1", "XFA2"), token))
        {
            await AssertRefusedAsync(refused, "tourId", "flightops:errors.kindHasNoLegs", token);
        }
    }

    [Fact]
    public async Task AnImportIsPreviewedWithoutWritingAndAppliedByTheRulesOfReports()
    {
        var token = TestContext.Current.CancellationToken;
        var reported = new ReportedLegs();
        await using var withReports = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ITourReports>(reported)));
        using var advisor = await SignedInAsync(withReports, AdvisorVid, token);

        var tour = await CreatedTourAsync(advisor, TourKind.Sequential, token);
        var legs = LegsUri(Id(tour));
        await AddLegAsync(advisor, Id(tour), token, Rome, Milan);
        await AddLegAsync(advisor, Id(tour), token, Milan, London);
        await AddLegAsync(advisor, Id(tour), token, London, Rome);
        await AddLegAsync(advisor, Id(tour), token, Rome, London);
        var before = Rows(await advisor.GetFromJsonAsync<JsonElement>(legs, token));

        // Milan to London has a report; Rome to London has none.
        reported.Legs.Add(Id(before[1]));

        // Only the row that changes names an aircraft type: an empty column is "no types", which the other legs have.
        object FileRow(string from, string to, string? flight = null) => new
        {
            departureIcao = from,
            arrivalIcao = to,
            flightNumbers = flight is null ? Array.Empty<string>() : [flight],
            aircraftTypes = flight is null ? Array.Empty<string>() : [TestType],
        };

        var rows = new[] { FileRow(Rome, Milan, "az 100"), FileRow(London, Rome), FileRow(Milan, Rome) };

        // An airport the hub does not know is refused on its row and cell.
        using (var unknown = await advisor.PostAsJsonAsync(
            $"{legs}/import/preview",
            new { rows = new[] { FileRow(Rome, Milan), FileRow(Rome, "XFZ9") }, mode = "Replace" },
            token))
        {
            await AssertRefusedAsync(unknown, "rows[1].arrivalIcao", "flightops:errors.airportUnknown", token);
        }

        // The preview says what "replace" does, and writes nothing.
        var preview = await OkAsync(
            await advisor.PostAsJsonAsync($"{legs}/import/preview", new { rows, mode = "Replace" }, token),
            token);
        Assert.Equal(
            ["Changed", "Retired", "Unchanged", "Added", "Deleted"],
            preview.GetProperty("lines").EnumerateArray().Select(line => line.GetProperty("outcome").GetString()));
        Assert.True(preview.GetProperty("reasonRequired").GetBoolean());
        Assert.Equal(
            before.Select(Id),
            Rows(await advisor.GetFromJsonAsync<JsonElement>(legs, token)).Select(Id));

        var fingerprint = preview.GetProperty("fingerprint").GetString();

        // Retiring owes a reason.
        using (var noReason = await advisor.PostAsJsonAsync($"{legs}/import", new { rows, mode = "Replace", fingerprint }, token))
        {
            await AssertRefusedAsync(noReason, "reason", "flightops:errors.importReasonRequired", token);
        }

        // Legs that moved since the preview are not the ones anybody looked at.
        using (var stale = await advisor.PostAsJsonAsync(
            $"{legs}/import",
            new { rows, mode = "Replace", reason = "fo-test new season", fingerprint = "0000000000000000" },
            token))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }

        var grid = await OkAsync(
            await advisor.PostAsJsonAsync($"{legs}/import", new { rows, mode = "Replace", reason = "fo-test new season", fingerprint }, token),
            token);
        var after = Rows(grid);

        // The one without a report is gone, the one with a report retired in its place; every number the file's order.
        Assert.DoesNotContain(after, row => Id(row) == Id(before[3]));
        Assert.Equal([Id(before[0]), Id(before[1]), Id(before[2])], after.Take(3).Select(Id));
        Assert.Equal([1, 2, 3, 4], after.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Equal("AZ 100", after[0].GetProperty("flightNumbers")[0].GetString());
        Assert.Equal("fo-test new season", after[1].GetProperty("retiredReason").GetString());
        Assert.Equal(Milan, after[3].GetProperty("departureIcao").GetString());
        Assert.Equal(253.9m, after[3].GetProperty("distanceNm").GetDecimal());

        // The same file again changes nothing, and owes nothing.
        preview = await OkAsync(
            await advisor.PostAsJsonAsync($"{legs}/import/preview", new { rows, mode = "Replace" }, token),
            token);
        Assert.All(
            preview.GetProperty("lines").EnumerateArray(),
            line => Assert.Contains(line.GetProperty("outcome").GetString(), new[] { "Unchanged", "Kept" }));
        Assert.False(preview.GetProperty("reasonRequired").GetBoolean());

        // A hub tour's legs belong to rotations a file cannot name.
        var hub = await CreatedTourAsync(advisor, TourKind.Hub, token);
        using (var refused = await advisor.PostAsJsonAsync($"{LegsUri(Id(hub))}/import/preview", new { rows, mode = "Merge" }, token))
        {
            await AssertRefusedAsync(refused, "tourId", "flightops:errors.importNotOnHub", token);
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>The reports arrive with T11: here a leg "has" them because the test says so.</summary>
    private sealed class ReportedLegs : ITourReports
    {
        public HashSet<long> Legs { get; } = [];

        public Task<bool> AnyAsync(long tourId, CancellationToken cancellationToken = default) => Task.FromResult(Legs.Count > 0);

        public Task<IReadOnlySet<long>> LegsWithReportsAsync(long tourId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<long>>(new HashSet<long>(Legs));
    }

    private async Task<JsonElement> CreatedTourAsync(HttpClient client, TourKind kind, CancellationToken cancellationToken, string? reference = null)
    {
        var now = DateTime.UtcNow;
        var slug = $"fo-test-legs-{Guid.NewGuid():N}"[..28];
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
            ReferenceAircraftIcao: reference,
            RequiredNm: null,
            AllowedAircraft: null,
            AwardId: null,
            RowVersion: default);

        using var response = await client.PostAsJsonAsync(TourEndpoints.Pattern, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _created.Add(Id(body));
        return body;
    }

    private static Core.Localization.Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static List<JsonElement> Rows(JsonElement grid) => [.. grid.GetProperty("legs").EnumerateArray()];

    private static JsonElement Row(JsonElement grid, long id) => Rows(grid).Single(row => Id(row) == id);

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
        Assert.Contains(key, problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(error => error.GetString()));
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

    private async Task SeedAircraftTypeAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (!await database.IvaoAircraftTypes.AnyAsync(type => type.IcaoCode == TestType, cancellationToken))
        {
            database.IvaoAircraftTypes.Add(new IvaoAircraftType
            {
                IcaoCode = TestType,
                Model = "fo-test",
                SyncedAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow,
            });
            await database.SaveChangesAsync(cancellationToken);
        }
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
        user.LastName = "Legs";
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
