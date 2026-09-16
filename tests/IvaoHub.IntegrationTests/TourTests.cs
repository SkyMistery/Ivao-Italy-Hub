using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The tours in the back office (M2, T6), through the real host: a tour born from a template, marked ready with its
/// problems listed field by field, found in search and calendar and gone from both when hidden; a tour with reports
/// hidden and never deleted; what a ready tour may still change; the division's limit that stays on while a tour needs
/// it; and the job that projects a tour again at its release.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TourTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int CoordinatorVid = 780061;
    private const int AdvisorVid = 780062;

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _created = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await SeedUserAsync(AdvisorVid, "IT-FOA1", token);
    }

    public async ValueTask DisposeAsync()
    {
        // The tours this class made go, and their projections with them: through the context, so the interceptor
        // removes what they projected.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            database.Tours.RemoveRange(await database.Tours.IgnoreQueryFilters()
                .Where(tour => _created.Contains(tour.Id))
                .ToListAsync(TestContext.Current.CancellationToken));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ATourFromATemplateIsMarkedReadyAndIsFoundUntilItIsHidden()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // A template: settings and a picture, no address and no dates. Written by who manages templates only.
        var templateBody = Payload(isTemplate: true, slug: null, title: $"fo-test-tour-template-{suffix}") with
        {
            DailyLegLimit = 6,
            RequiresProcedures = true,
            BannerMediaId = 900_001,
        };

        using (var refused = await advisor.PostAsJsonAsync(TourEndpoints.Pattern, templateBody, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var template = await CreatedAsync(await coordinator.PostAsJsonAsync(TourEndpoints.Pattern, templateBody, token), token);
        Assert.Null(template.GetProperty("slug").GetString());
        Assert.Equal("Template", template.GetProperty("state").GetString());

        // A new tour out of it: the settings come, the dates do not.
        var slug = $"fo-test-tour-{suffix}";
        var tour = await CreatedAsync(
            await advisor.PostAsJsonAsync(
                $"{TourEndpoints.Pattern}/from-template/{Id(template)}",
                new { title = Text($"fo-test-tour-{suffix}", "it", "en"), slug },
                token),
            token);

        Assert.Equal(6, tour.GetProperty("dailyLegLimit").GetInt32());
        Assert.True(tour.GetProperty("requiresProcedures").GetBoolean());
        Assert.Equal(900_001, tour.GetProperty("bannerMediaId").GetInt64());
        Assert.Equal(JsonValueKind.Null, tour.GetProperty("releaseAt").ValueKind);
        Assert.Equal("Draft", tour.GetProperty("state").GetString());

        // What stands in the way of "ready", field by field, before anybody presses it; and the same answer after.
        var problems = await advisor.GetFromJsonAsync<JsonElement>($"{TourEndpoints.Pattern}/{Id(tour)}/ready-problems", token);
        Assert.Contains("closeAt", problems.GetProperty("errors").EnumerateObject().Select(field => field.Name));
        Assert.Equal(["it", "en"], problems.GetProperty("localized").GetProperty("summary").EnumerateArray().Select(locale => locale.GetString()));

        using (var notYet = await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Ready" }, token))
        {
            await AssertRefusedAsync(notYet, "releaseAt", "errors.required", token);
        }

        // The dates, the summary in every language: released yesterday, so public as soon as it is ready.
        var now = DateTime.UtcNow;
        var dated = Payload(isTemplate: false, slug, $"fo-test-tour-{suffix}") with
        {
            Summary = Text("fo-test-tour summary", "it", "en"),
            ReleaseAt = now.AddDays(-1),
            CloseAt = now.AddDays(30),
            DailyLegLimit = 6,
            BannerMediaId = 900_001,
            RowVersion = tour.GetProperty("rowVersion").GetDateTime(),
        };

        using (var saved = await advisor.PutAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}", dated, token))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        using (var ready = await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Ready" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            var body = await ready.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal("Open", body.GetProperty("state").GetString());
            Assert.True(body.GetProperty("isPublic").GetBoolean());
        }

        await AssertProjectedAsync(Id(tour), Visibility.Public, calendarEntries: 2, token);

        // Hidden: nobody outside the staff finds it, and the banner is still in use.
        using (var hidden = await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Hide" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        }

        await AssertProjectedAsync(Id(tour), visibility: null, calendarEntries: 0, token);
        Assert.Equal(1, await MediaUsesAsync(Id(tour), token));

        // Released, it does not go back to draft: it is hidden instead.
        using (var draft = await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Draft" }, token))
        {
            await AssertRefusedAsync(draft, "status", "flightops:errors.releasedStaysReady", token);
        }

        // The list keeps templates apart, and computes the state.
        var list = await advisor.GetFromJsonAsync<JsonElement>($"{TourEndpoints.Pattern}?q={slug}", token);
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal("Open", row.GetProperty("state").GetString());
        Assert.True(row.GetProperty("isHidden").GetBoolean());

        // A template becomes a template of this tour, under a name of its own.
        var copy = await CreatedAsync(
            await coordinator.PostAsJsonAsync(
                $"{TourEndpoints.Pattern}/{Id(tour)}/save-as-template",
                new { title = Text($"fo-test-tour-copy-{suffix}", "it", "en") },
                token),
            token);
        Assert.True(copy.GetProperty("isTemplate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, copy.GetProperty("closeAt").ValueKind);

        using (var advisorCopy = await advisor.PostAsJsonAsync(
            $"{TourEndpoints.Pattern}/{Id(tour)}/save-as-template",
            new { title = Text("fo-test-tour-nope", "it", "en") },
            token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, advisorCopy.StatusCode);
        }
    }

    [Fact]
    public async Task ATourWithReportsIsHiddenAndNeverDeleted()
    {
        var token = TestContext.Current.CancellationToken;

        // The reports arrive with T11: here a tour "has" them because the answer says so.
        await using var withReports = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ITourReports, EveryTourHasReports>()));
        using var coordinator = await SignedInAsync(withReports, CoordinatorVid, token);
        using var advisor = await SignedInAsync(withReports, AdvisorVid, token);
        using var plainCoordinator = await SignedInAsync(_factory, CoordinatorVid, token);

        var tour = await CreatedAsync(
            await coordinator.PostAsJsonAsync(TourEndpoints.Pattern, Payload(isTemplate: false, $"fo-test-tour-{Guid.NewGuid():N}"[..28], "fo-test-tour-reports"), token),
            token);

        using (var refused = await coordinator.DeleteAsync(new Uri($"{TourEndpoints.Pattern}/{Id(tour)}", UriKind.Relative), token))
        {
            await AssertRefusedAsync(refused, "id", "flightops:errors.tourHasReports", token);
        }

        using (var hidden = await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Hide" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        }

        // Without reports: an advisor edits but does not delete (§7.2), the coordinator deletes.
        using (var advisorDelete = await SignedInAsync(_factory, AdvisorVid, token))
        using (var forbidden = await advisorDelete.DeleteAsync(new Uri($"{TourEndpoints.Pattern}/{Id(tour)}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using (var deleted = await plainCoordinator.DeleteAsync(new Uri($"{TourEndpoints.Pattern}/{Id(tour)}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }
    }

    [Fact]
    public async Task AReadyTourKeepsItsKindAndItsCloseMovesOnlyTwoWindowsAhead()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);
        var slug = $"fo-test-tour-{Guid.NewGuid():N}"[..28];
        var now = DateTime.UtcNow;

        var payload = Payload(isTemplate: false, slug, "fo-test-tour-kind") with
        {
            Summary = Text("fo-test-tour summary", "it", "en"),
            ReleaseAt = now.AddDays(-2),
            CloseAt = now.AddDays(40),
            ReportWindowDays = 7,
            DailyLegLimit = 5,
            CoverMediaId = 900_002,
        };

        var tour = await CreatedAsync(await advisor.PostAsJsonAsync(TourEndpoints.Pattern, payload, token), token);
        var ready = await OkAsync(await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Ready" }, token), token);

        // Public: the kind no longer changes.
        using (var kind = await advisor.PutAsJsonAsync(
            $"{TourEndpoints.Pattern}/{Id(tour)}",
            payload with { Kind = TourKind.Free, RowVersion = ready.GetProperty("rowVersion").GetDateTime() },
            token))
        {
            await AssertRefusedAsync(kind, "kind", "flightops:errors.kindLocked", token);
        }

        // The close moves, but not closer than two windows from today.
        using (var tooSoon = await advisor.PutAsJsonAsync(
            $"{TourEndpoints.Pattern}/{Id(tour)}",
            payload with { CloseAt = now.AddDays(10), RowVersion = ready.GetProperty("rowVersion").GetDateTime() },
            token))
        {
            await AssertRefusedAsync(tooSoon, "closeAt", "flightops:errors.closeTooSoon", token);
        }

        var extended = now.AddDays(90);
        await OkAsync(
            await advisor.PutAsJsonAsync(
                $"{TourEndpoints.Pattern}/{Id(tour)}",
                payload with { CloseAt = extended, RowVersion = ready.GetProperty("rowVersion").GetDateTime() },
                token),
            token);

        // And the photo stays in the library until a month after the new close.
        await using var scope = _factory.Services.CreateAsyncScope();
        var use = await scope.ServiceProvider.GetRequiredService<HubDbContext>().MediaUses.AsNoTracking()
            .SingleAsync(row => row.SourceModule == FlightOpsModule.ModuleKey && row.SourceId == $"tour:{Id(tour)}", token);
        Assert.Equal(900_002, use.MediaId);
        Assert.Equal(extended + Tour.MediaKeptAfterClose, use.UsedUntil!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TheDivisionsLimitStaysOnWhileATourAheadHasNoneOfItsOwn()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        var slug = $"fo-test-tour-{Guid.NewGuid():N}"[..28];
        var now = DateTime.UtcNow;

        var payload = Payload(isTemplate: false, slug, "fo-test-tour-limit") with
        {
            ReleaseAt = now.AddDays(5),
            CloseAt = now.AddDays(40),
        };
        var tour = await CreatedAsync(await coordinator.PostAsJsonAsync(TourEndpoints.Pattern, payload, token), token);

        var settingsUri = new Uri(ModuleSettingsEndpoints.Pattern.Replace("{key}", FlightOpsModule.ModuleKey, StringComparison.Ordinal), UriKind.Relative);
        var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            (await coordinator.GetFromJsonAsync<JsonElement>(settingsUri, token)).GetRawText())!;
        settings["dailyLegLimit"] = JsonSerializer.SerializeToElement<int?>(null);

        try
        {
            using (var refused = await coordinator.PutAsJsonAsync(settingsUri, settings, token))
            {
                await AssertRefusedAsync(refused, "dailyLegLimit", "flightops:errors.toursNeedDailyLimit", token);
            }

            // The list names the tours in the way, with the same rule.
            var inTheWay = await coordinator.GetFromJsonAsync<JsonElement>(
                $"{TourEndpoints.Pattern}?filter[{TourEndpoints.NeedsOwnDailyLimitFilter}]=true&pageSize=100",
                token);
            Assert.Contains(Id(tour), inTheWay.GetProperty("items").EnumerateArray().Select(Id));

            // Every such tour given a limit of its own, the limit switches off.
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
                await database.Tours.IgnoreQueryFilters()
                    .Where(row => row.DailyLegLimit == null && !row.IsTemplate)
                    .Where(row => row.Slug != null && row.Slug.StartsWith("fo-test-"))
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.DailyLegLimit, 3), token);

                if (await database.Tours.IgnoreQueryFilters().AnyAsync(TourState.NeedsOwnDailyLimit(DateTime.UtcNow), token))
                {
                    // A tour of somebody else's in the shared database: the refusal is still right, and proven above.
                    return;
                }
            }

            using var accepted = await coordinator.PutAsJsonAsync(settingsUri, settings, token);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<HubDbContext>().DivisionSettings
                .Where(row => row.Key == ModuleSettingsStore.SettingsKey(FlightOpsModule.ModuleKey))
                .ExecuteDeleteAsync(token);
        }
    }

    [Fact]
    public async Task TheReleaseJobMakesAReadyTourPublicWithoutWritingIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);
        var slug = $"fo-test-tour-{Guid.NewGuid():N}"[..28];
        var now = DateTime.UtcNow;

        var payload = Payload(isTemplate: false, slug, "fo-test-tour-release") with
        {
            Summary = Text("fo-test-tour summary", "it", "en"),
            ReleaseAt = now.AddDays(3),
            CloseAt = now.AddDays(40),
            DailyLegLimit = 5,
        };
        var tour = await CreatedAsync(await advisor.PostAsJsonAsync(TourEndpoints.Pattern, payload, token), token);
        var ready = await OkAsync(await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(tour)}/status", new { action = "Ready" }, token), token);
        Assert.Equal("Upcoming", ready.GetProperty("state").GetString());

        // Ready but not released, and no preview: the staff finds it, the public does not.
        await AssertProjectedAsync(Id(tour), Visibility.Staff, calendarEntries: 2, token);

        // Time passes: the release is now behind, and nobody saves the tour.
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
        var released = DateTime.UtcNow.AddSeconds(-1);
        await database.Tours.IgnoreQueryFilters()
            .Where(row => row.Id == Id(tour))
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ReleaseAt, released), token);
        var before = await database.Tours.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == Id(tour), token);

        Assert.True(await scope.ServiceProvider.GetRequiredService<TourReleaseJob>().RunAsync(token) >= 1);

        await AssertProjectedAsync(Id(tour), Visibility.Public, calendarEntries: 2, token);

        // The tour itself was not written: same row version, same author of the last change.
        var after = await database.Tours.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == Id(tour), token);
        Assert.Equal(before.RowVersion, after.RowVersion);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private sealed class EveryTourHasReports : ITourReports
    {
        public Task<bool> AnyAsync(long tourId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private static TourWriteDto Payload(bool isTemplate, string? slug, string title) => new(
        OwnerDepartment: Department.FOD,
        IsTemplate: isTemplate,
        Slug: slug,
        Kind: TourKind.Sequential,
        Title: Text(title, Locales),
        Summary: Text(title, "en"),
        Briefing: null,
        CoverMediaId: null,
        BannerMediaId: null,
        ShowPreview: false,
        ReleaseAt: isTemplate ? null : DateTime.UtcNow.AddDays(10),
        CloseAt: isTemplate ? null : DateTime.UtcNow.AddDays(60),
        ReportWindowDays: null,
        Progression: TourProgression.FlyAhead,
        HubRotationOrder: null,
        RequiresProcedures: false,
        DailyLegLimit: null,
        MinPilotRating: null,
        ReferenceAircraftIcao: null,
        AwardId: null,
        RowVersion: default);

    private static Core.Localization.Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private async Task<JsonElement> CreatedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            _created.Add(Id(body));
            return body;
        }
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

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(key, problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(error => error.GetString()));
    }

    /// <summary>The line of the tour in search (one per language) and its calendar entries, all with this visibility.</summary>
    private async Task AssertProjectedAsync(long tourId, Visibility? visibility, int calendarEntries, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var sourceId = string.Create(CultureInfo.InvariantCulture, $"tour:{tourId}");

        var search = await database.SearchIndex.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.SourceModule == FlightOpsModule.ModuleKey && row.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        var calendar = await database.CalendarEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.SourceModule == FlightOpsModule.ModuleKey && row.SourceId == sourceId)
            .ToListAsync(cancellationToken);

        if (visibility is null)
        {
            Assert.Empty(search);
        }
        else
        {
            Assert.Equal(Locales.Length, search.Count);
            Assert.All(search, row => Assert.Equal(visibility, row.Visibility));
            Assert.All(calendar, row => Assert.Equal(visibility, row.Visibility));
        }

        Assert.Equal(calendarEntries, calendar.Count);
    }

    private async Task<int> MediaUsesAsync(long tourId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().MediaUses.AsNoTracking()
            .CountAsync(row => row.SourceModule == FlightOpsModule.ModuleKey && row.SourceId == $"tour:{tourId}", cancellationToken);
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
        user.LastName = "Tours";
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
