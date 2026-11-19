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
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The preview of a tour (design <c>11-design-pagine-dei-tour</c> §5; the endpoint decided by the master on 18 November
/// 2026): the page of the site drawn from the row the back office holds, a draft included, with the same answer the site
/// reads. Read with <c>Tours.View</c> through the one handler: refused without the permission, and refused on a tour
/// another department curates; a tour with no address or no dates has no page yet, and the refusal names the field.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TourPreviewTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int CoordinatorVid = 780063;
    private const int EventsVid = 780064;
    private const int MemberVid = 780065;

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _created = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await SeedUserAsync(EventsVid, "IT-EC", token);
        await SeedUserAsync(MemberVid, position: null, token);
        await FoTestAirports.SeedAsync(_factory.Services, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            database.Tours.RemoveRange(await database.Tours.IgnoreQueryFilters()
                .Where(tour => _created.Contains(tour.Id))
                .ToListAsync(token));
            await database.SaveChangesAsync(token);

            await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
                .Where(grant => grant.Vid == EventsVid)
                .ExecuteDeleteAsync(token);
        }

        await FoTestAirports.RemoveAsync(_factory.Services, token);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ADraftIsPreviewedAsTheSiteWouldDrawItWhileTheSiteDoesNotHaveIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var slug = Slug();
        var tour = await CreatedAsync(coordinator, Payload(slug), token);
        await FoTestAirports.AddLegAsync(coordinator, Id(tour), token);

        var page = await OkAsync(await coordinator.GetAsync(PreviewUri(Id(tour)), token), token);

        // The site's own answer, composed by the same reader: the address, the state the draft is in, the leg with the
        // numbers of the server.
        Assert.Equal(slug, page.GetProperty("slug").GetString());
        Assert.Equal("Draft", page.GetProperty("state").GetString());
        var leg = Assert.Single(page.GetProperty("legs").EnumerateArray());
        Assert.True(leg.GetProperty("distanceNm").GetDecimal() > 0);
        Assert.True(page.GetProperty("totalNm").GetDecimal() > 0);

        // The site itself does not have a draft.
        using var site = await coordinator.GetAsync($"{TourEndpoints.Pattern}/public/{slug}", token);
        Assert.Equal(HttpStatusCode.NotFound, site.StatusCode);
    }

    [Fact]
    public async Task ADraftWithoutItsAddressOrItsDatesHasNoPageYet()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        // A tour has its address from its first save; a template never has one, nor dates (design M2 §1.10).
        var template = await CreatedAsync(
            coordinator,
            Payload(slug: null) with { IsTemplate = true, ReleaseAt = null, CloseAt = null },
            token);
        await AssertRefusedAsync(await coordinator.GetAsync(PreviewUri(Id(template)), token), "slug", token);

        // A tour made from it has an address and no dates yet: the dates are not copied (§1.10).
        var slug = Slug();
        using var made = await coordinator.PostAsJsonAsync(
            $"{TourEndpoints.Pattern}/from-template/{Id(template)}",
            new { title = Text(slug, Locales), slug },
            token);
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        var undated = await made.Content.ReadFromJsonAsync<JsonElement>(token);
        _created.Add(Id(undated));
        Assert.Equal(JsonValueKind.Null, undated.GetProperty("releaseAt").ValueKind);

        await AssertRefusedAsync(await coordinator.GetAsync(PreviewUri(Id(undated)), token), "releaseAt", token);
    }

    [Fact]
    public async Task AMemberWithoutTheToursViewIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var member = await SignedInAsync(MemberVid, token);
        var tour = await CreatedAsync(coordinator, Payload(Slug()), token);

        using var refused = await member.GetAsync(PreviewUri(Id(tour)), token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task ATourAnotherDepartmentCuratesIsRefusedUntilItsCareReachesTheReader()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var tour = await CreatedAsync(coordinator, Payload(Slug()), token);

        // The events staff hold the tours' view on their own department only: the tour is the flight operations'.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await GrantViewAsync(scope, EventsVid, Department.ED, token);
        }

        using var events = await SignedInAsync(EventsVid, token);
        using (var refused = await events.GetAsync(PreviewUri(Id(tour)), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.DoesNotContain("\"slug\"", await refused.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        }

        // Curated by both departments, the same reader sees it: the refusal was the department, not the permission.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>().Tours.IgnoreQueryFilters()
                .Where(row => row.Id == Id(tour))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        row => row.OwnerDepartmentMask,
                        DepartmentMask.Of(Department.FOD) | DepartmentMask.Of(Department.ED)),
                    token);
        }

        await OkAsync(await events.GetAsync(PreviewUri(Id(tour)), token), token);
    }

    private static string PreviewUri(long id) => string.Create(CultureInfo.InvariantCulture, $"{TourEndpoints.Pattern}/{id}/preview");

    private static string Slug() => $"fo-test-preview-{Guid.NewGuid():N}"[..30];

    private static TourWriteDto Payload(string? slug)
    {
        var now = DateTime.UtcNow;
        var title = slug ?? $"fo-test-preview-{Guid.NewGuid():N}"[..30];

        return new TourWriteDto(
            OwnerDepartment: Department.FOD,
            IsTemplate: false,
            Slug: slug,
            Kind: TourKind.Sequential,
            Title: Text(title, Locales),
            Summary: Text(title, Locales),
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
            RowVersion: default,
            ParentTourId: null);
    }

    private static Core.Localization.Localized<string> Text(string text, params string[] locales) =>
        new(locales.ToDictionary(locale => locale, _ => text));

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private async Task<JsonElement> CreatedAsync(HttpClient client, TourWriteDto payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(TourEndpoints.Pattern, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _created.Add(Id(body));
        return body;
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

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
        }
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

    private static async Task GrantViewAsync(AsyncServiceScope scope, int vid, Department department, CancellationToken cancellationToken)
    {
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = TourPermissions.View,
            Department = department,
            Effect = GrantEffect.Grant,
            Reason = "fo-test",
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A member of the hub; with a position, a member of staff in the department it names.</summary>
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
        user.LastName = "Preview";
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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
