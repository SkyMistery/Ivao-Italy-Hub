using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The public side of the tours (M2, T10), through the real host: what a visitor is given at <c>/tours</c> and at
/// <c>/tours/{slug}</c>, what is refused to everybody there, and the base map the hub serves itself.
/// <para>Anonymous on purpose: every read here is made by a client that never signed in, because that is the reader
/// these two addresses exist for. What a member of staff sees at the same addresses is the same answer — the drafts
/// are in the back office.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PublicTourTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 §13); T10 takes 80–81.
    private const int CoordinatorVid = 780080;

    private static readonly string[] Locales = ["it", "en"];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _tours = [];
    private readonly List<long> _rules = [];
    private readonly List<long> _errors = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await FoTestAirports.SeedAsync(_factory.Services, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await database.Tours.IgnoreQueryFilters().Where(tour => _tours.Contains(tour.Id)).ExecuteDeleteAsync(token);
            await database.Rules.Where(rule => _rules.Contains(rule.Id)).ExecuteDeleteAsync(token);
            await database.Errors.Where(error => _errors.Contains(error.Id)).ExecuteDeleteAsync(token);
        }

        await FoTestAirports.RemoveAsync(_factory.Services, token);
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// The "done when" of T10, as far as a test without a browser reaches: a released tour with two legs is on the
    /// cards and at its own address, with the airports and their coordinates — which is what the map draws —, the
    /// distances, the rules in force with their parameters and the errors the division made public.
    /// </summary>
    [Fact]
    public async Task AReleasedTourIsReadByAnybodyWithItsLegsItsRulesAndItsTotals()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var visitor = Anonymous();

        var tour = await CreatedTourAsync(coordinator, released: true, token);
        var tourId = Id(tour);
        var slug = tour.GetProperty("slug").GetString()!;

        // A leg answers with the whole grid, renumbered: 200, not a row created at an address of its own.
        await OkAsync(await coordinator.PostAsJsonAsync(Legs(tourId), Leg(Rome, Milan), token), token);
        await OkAsync(await coordinator.PostAsJsonAsync(Legs(tourId), Leg(Milan, London), token), token);

        // A rule of the tour with a parameter, one public error and one the department keeps to itself.
        var shown = await CreatedErrorAsync(coordinator, "fo-test t10 public", isPublic: true, token);
        var kept = await CreatedErrorAsync(coordinator, "fo-test t10 internal", isPublic: false, token);
        var code = Code();
        await CreatedRuleAsync(
            coordinator,
            new TourRuleWriteDto(
                tourId,
                code,
                Text(code, Locales),
                Text($"The rule {code}.", Locales),
                AmendsRuleId: null,
                CheckCatalog.LandingAtArrival,
                JsonNode.Parse("""{"radiusNm":3}"""),
                [Id(shown), Id(kept)],
                Sort: 0,
                Retired: false,
                RowVersion: default),
            token);

        await ReadyAsync(coordinator, tourId, token);

        // The cards: this tour, open, with its two legs and the distance of both.
        var cards = await OkAsync(await visitor.GetAsync(PublicCards, token), token);
        var card = Assert.Single(cards.EnumerateArray(), row => Id(row) == tourId);
        Assert.Equal("Open", card.GetProperty("state").GetString());
        Assert.Equal(2, card.GetProperty("legs").GetInt32());
        Assert.True(card.GetProperty("totalNm").GetDecimal() > 600, card.GetProperty("totalNm").GetRawText());

        // The page: the legs in order, with the coordinates the map draws and the codes a pilot reads.
        var page = await OkAsync(await visitor.GetAsync($"{PublicCards}/{slug}", token), token);
        var legs = page.GetProperty("legs").EnumerateArray().ToList();
        Assert.Equal([Rome, Milan], legs.Select(leg => leg.GetProperty("departureIcao").GetString()));
        Assert.Equal([Milan, London], legs.Select(leg => leg.GetProperty("arrivalIcao").GetString()));
        Assert.Equal("XA1", legs[0].GetProperty("departureIata").GetString());
        Assert.Equal(41.8002777778, legs[0].GetProperty("departureLatitude").GetDouble(), 6);
        Assert.Equal(12.2388888889, legs[0].GetProperty("departureLongitude").GetDouble(), 6);
        Assert.All(legs, leg => Assert.True(leg.GetProperty("released").GetBoolean()));
        Assert.Equal(
            legs.Sum(leg => leg.GetProperty("distanceNm").GetDecimal()),
            page.GetProperty("totalNm").GetDecimal());

        // The rules in force, with the value of the parameter written out, and only the public error.
        var rule = Assert.Single(page.GetProperty("rules").EnumerateArray(), row => row.GetProperty("code").GetString() == code);
        Assert.Equal(["3 NM"], rule.GetProperty("values").EnumerateArray().Select(value => value.GetString()));
        var error = Assert.Single(page.GetProperty("errors").EnumerateArray());
        Assert.Equal(Id(shown), Id(error));

        // Hidden: gone from the cards and gone from its address, for everybody (design M2 §1.2.2).
        await StatusAsync(coordinator, tourId, TourStatusAction.Hide, token);

        var afterwards = await OkAsync(await visitor.GetAsync(PublicCards, token), token);
        Assert.DoesNotContain(afterwards.EnumerateArray(), row => Id(row) == tourId);

        using var gone = await visitor.GetAsync($"{PublicCards}/{slug}", token);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    /// <summary>
    /// A tour the public may not see yet: a draft is nobody's business, a ready tour before its release is nobody's
    /// either — unless the flight operations department shows it as a preview (answer 1) — and a template never is.
    /// </summary>
    [Fact]
    public async Task ADraftATemplateAndATourBeforeItsReleaseAreNotPublicUnlessItIsShownAsAPreview()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var visitor = Anonymous();

        // A draft, ready dates and all: not there.
        var draft = await CreatedTourAsync(coordinator, released: false, token);
        await AssertNotPublicAsync(visitor, draft, token);

        // Ready, and its release is still ahead: still not there.
        await OkAsync(await coordinator.PostAsJsonAsync(Legs(Id(draft)), Leg(Rome, Milan), token), token);
        var ready = await ReadyAsync(coordinator, Id(draft), token);
        Assert.Equal("Upcoming", ready.GetProperty("state").GetString());
        await AssertNotPublicAsync(visitor, draft, token);

        // Shown as a preview: there, and said to be upcoming.
        var previewed = await OkAsync(
            await coordinator.PutAsJsonAsync(
                $"{TourEndpoints.Pattern}/{Id(draft)}",
                Payload(draft.GetProperty("slug").GetString()!, released: false, preview: true) with
                {
                    RowVersion = ready.GetProperty("rowVersion").GetDateTime(),
                },
                token),
            token);
        Assert.True(previewed.GetProperty("isPublic").GetBoolean());

        var page = await OkAsync(await visitor.GetAsync($"{PublicCards}/{draft.GetProperty("slug").GetString()}", token), token);
        Assert.Equal("Upcoming", page.GetProperty("state").GetString());
        Assert.Single(page.GetProperty("legs").EnumerateArray());

        // A template has no address at all, and is on nobody's cards.
        var template = await CreatedAsync(
            coordinator,
            $"{TourEndpoints.Pattern}/{Id(draft)}/save-as-template",
            new TourSaveAsTemplateRequest(Text("fo-test t10 template", Locales)),
            token);
        _tours.Add(Id(template));

        var cards = await OkAsync(await visitor.GetAsync(PublicCards, token), token);
        Assert.DoesNotContain(cards.EnumerateArray(), row => Id(row) == Id(template));
    }

    /// <summary>
    /// The block <c>flightops.tourCards</c>: the same cards, live, and its property narrows them. Asked the way a
    /// published page asks — anonymously, through <c>/api/blocks/data/{type}</c> with the properties in the query.
    /// </summary>
    [Fact]
    public async Task TheBlockShowsTheCardsAndTheStatesItIsAskedFor()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var visitor = Anonymous();

        var open = await CreatedTourAsync(coordinator, released: true, token);
        await OkAsync(await coordinator.PostAsJsonAsync(Legs(Id(open)), Leg(Rome, Milan), token), token);
        await ReadyAsync(coordinator, Id(open), token);

        var everything = await BlockAsync(visitor, props: null, token);
        Assert.Contains(everything, row => Id(row) == Id(open));

        // Only the tours still to come: this one is open, so it is not among them.
        var upcoming = await BlockAsync(visitor, """{"states":["Upcoming"]}""", token);
        Assert.DoesNotContain(upcoming, row => Id(row) == Id(open));

        // A state that is not one a card can be in narrows to nothing rather than widening to everything.
        var nonsense = await BlockAsync(visitor, """{"states":["Closed"]}""", token);
        Assert.DoesNotContain(nonsense, row => Id(row) == Id(open));

        // At most one card, and the block answers the tour with the earliest release: the one this test made is the
        // only one it can assert about, so what is asserted is the count.
        var one = await BlockAsync(visitor, """{"limit":1}""", token);
        Assert.Single(one);
    }

    /// <summary>
    /// The base map (note 2026-09-15-la-mappa §5, verification 1): the archive is served with <c>Range</c> and a strong
    /// tag, which is what makes a 179 MB file readable a few kilobytes at a time. Kestrel does it here; Passenger is
    /// the same question asked again on the staging, and it is still open.
    /// <para>The archive belongs to an installation and is not in the repository, so a run without one writes a small
    /// stand-in and takes it away again; a machine that has the real world keeps it.</para>
    /// </summary>
    [Fact]
    public async Task TheBaseMapIsServedInPiecesWithAStrongTagAndNothingElseIs()
    {
        var token = TestContext.Current.CancellationToken;
        using var visitor = Anonymous();

        var archive = Path.Combine(_factory.Paths.Tiles, TileEndpoints.BaseMapName);
        var written = !File.Exists(archive);

        if (written)
        {
            Directory.CreateDirectory(_factory.Paths.Tiles);
            await File.WriteAllBytesAsync(archive, Encoding.ASCII.GetBytes(new string('m', 4096)), token);
        }

        try
        {
            // The first kilobyte, which is how `pmtiles` reads the header before anything else — and the whole point
            // of the endpoint: 188 MB answered in pieces. The archive is never asked for whole, here or in a browser.
            using var request = new HttpRequestMessage(HttpMethod.Get, TileEndpoints.Prefix + "/" + TileEndpoints.BaseMapName);
            request.Headers.Range = new RangeHeaderValue(0, 1023);
            using var piece = await visitor.SendAsync(request, token);
            Assert.Equal(HttpStatusCode.PartialContent, piece.StatusCode);
            Assert.Equal(1024, (await piece.Content.ReadAsByteArrayAsync(token)).Length);
            Assert.Equal("bytes", piece.Headers.AcceptRanges.Single());

            var tag = piece.Headers.ETag;
            Assert.NotNull(tag);
            Assert.False(tag!.IsWeak);

            // The same tag on another piece: a cache that keeps one of them is keeping this archive and no other.
            using var again = new HttpRequestMessage(HttpMethod.Get, TileEndpoints.Prefix + "/" + TileEndpoints.BaseMapName);
            again.Headers.Range = new RangeHeaderValue(1024, 2047);
            using var second = await visitor.SendAsync(again, token);
            Assert.Equal(HttpStatusCode.PartialContent, second.StatusCode);
            Assert.Equal(tag, second.Headers.ETag);
        }
        finally
        {
            if (written)
            {
                File.Delete(archive);
            }
        }

        // One directory, one file, and a name that is a name: nothing else is served from there.
        foreach (var name in new[] { "other.pmtiles", "..%2Fconfig%2Fdivision.json" })
        {
            using var refused = await visitor.GetAsync($"{TileEndpoints.Prefix}/{name}", token);
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }
    }

    private const string PublicCards = $"{TourEndpoints.Pattern}/public";

    private HttpClient Anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        return client;
    }

    private static string Legs(long tourId) => $"/api/flightops/tours/{tourId}/legs";

    private static LegWriteDto Leg(string from, string to) =>
        new(from, to, Callsigns: ["XAA100"], FlightNumbers: null, Aircraft: null, ReleaseAt: null, ChangeReason: null, RowVersion: default);

    private static string Code() => $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

    private async Task AssertNotPublicAsync(HttpClient visitor, JsonElement tour, CancellationToken cancellationToken)
    {
        var cards = await OkAsync(await visitor.GetAsync(PublicCards, cancellationToken), cancellationToken);
        Assert.DoesNotContain(cards.EnumerateArray(), row => Id(row) == Id(tour));

        using var page = await visitor.GetAsync($"{PublicCards}/{tour.GetProperty("slug").GetString()}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
    }

    private static async Task<List<JsonElement>> BlockAsync(HttpClient client, string? props, CancellationToken cancellationToken)
    {
        var query = props is null
            ? string.Empty
            : "?props=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(props)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var answer = await OkAsync(
            await client.GetAsync($"/api/blocks/data/{TourCardsProvider.BlockType}{query}", cancellationToken),
            cancellationToken);

        return [.. answer.GetProperty("items").EnumerateArray()];
    }

    private async Task<JsonElement> ReadyAsync(HttpClient client, long tourId, CancellationToken cancellationToken) =>
        await StatusAsync(client, tourId, TourStatusAction.Ready, cancellationToken);

    private async Task<JsonElement> StatusAsync(
        HttpClient client,
        long tourId,
        TourStatusAction action,
        CancellationToken cancellationToken) =>
        await OkAsync(
            await client.PostAsJsonAsync($"{TourEndpoints.Pattern}/{tourId}/status", new TourStatusRequest(action), cancellationToken),
            cancellationToken);

    private async Task<JsonElement> CreatedErrorAsync(HttpClient client, string name, bool isPublic, CancellationToken cancellationToken)
    {
        var error = await CreatedAsync(
            client,
            RuleEndpoints.ErrorsPattern,
            new TourErrorWriteDto(
                Text(name, Locales),
                Text($"{name} described.", Locales),
                null,
                ErrorCategory.Info,
                null,
                null,
                isPublic,
                Retired: false,
                RowVersion: default),
            cancellationToken);

        _errors.Add(Id(error));
        return error;
    }

    private async Task<JsonElement> CreatedRuleAsync(HttpClient client, TourRuleWriteDto payload, CancellationToken cancellationToken)
    {
        var rule = await CreatedAsync(client, RuleEndpoints.RulesPattern, payload, cancellationToken);
        _rules.Add(Id(rule));
        return rule;
    }

    private async Task<JsonElement> CreatedTourAsync(HttpClient client, bool released, CancellationToken cancellationToken)
    {
        var slug = $"fo-test-public-{Guid.NewGuid():N}"[..28];
        var tour = await CreatedAsync(client, TourEndpoints.Pattern, Payload(slug, released, preview: false), cancellationToken);
        _tours.Add(Id(tour));
        return tour;
    }

    private static TourWriteDto Payload(string slug, bool released, bool preview) => new(
        OwnerDepartment: Department.FOD,
        IsTemplate: false,
        Slug: slug,
        Kind: TourKind.Sequential,
        Title: Text(slug, Locales),
        Summary: Text($"{slug} summary", Locales),
        Briefing: null,
        CoverMediaId: null,
        BannerMediaId: null,
        ShowPreview: preview,
        ReleaseAt: released ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(10),
        CloseAt: DateTime.UtcNow.AddDays(60),
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
        user.LastName = "Public";
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
