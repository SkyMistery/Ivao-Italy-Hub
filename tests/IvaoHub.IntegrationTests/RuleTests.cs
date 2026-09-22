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
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Rules and errors (M2, T9), through the real host: the "done when" of T9 — the flight operations department writes a
/// general rule with the parameters of the disconnections, a tour amends it, and the block shows the public catalogue —;
/// the amendment that follows its rule for what it does not change; the copy of a tour's rules and the template's carrying
/// parameters and errors; and what is refused.
/// <para>General rules and errors are the division's, so other rows may be in the shared database: every assertion looks
/// at this class's own codes and identifiers, and every row goes back at the end.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class RuleTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13); T9 takes 78–79.
    private const int CoordinatorVid = 780078;
    private const int AdvisorVid = 780079;

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
        await SeedUserAsync(AdvisorVid, "IT-FOA1", token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A tour deleted takes its rules, amendments included; then the general rules may go, and the errors with their links.
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await database.Tours.IgnoreQueryFilters().Where(tour => _tours.Contains(tour.Id)).ExecuteDeleteAsync(token);
            await database.Rules.Where(rule => _rules.Contains(rule.Id)).ExecuteDeleteAsync(token);
            await database.Errors.Where(error => _errors.Contains(error.Id)).ExecuteDeleteAsync(token);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AGeneralRuleIsAmendedByATourAndThePublicErrorsAreShown()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        // Two errors: one the public reads, one only the validators.
        var disconnected = await CreatedErrorAsync(advisor, "fo-test disconnected", ErrorCategory.Warning, yearlyMax: 3, isPublic: true, token);
        var hidden = await CreatedErrorAsync(advisor, "fo-test hidden", ErrorCategory.Dangerous, yearlyMax: null, isPublic: false, token);

        // A warning has a maximum, and only a warning.
        using (var noMax = await advisor.PostAsJsonAsync(RuleEndpoints.ErrorsPattern, Error("fo-test", ErrorCategory.Warning, null, false), token))
        {
            await AssertRefusedAsync(noMax, "yearlyMax", "errors.required", token);
        }

        using (var infoMax = await advisor.PostAsJsonAsync(RuleEndpoints.ErrorsPattern, Error("fo-test", ErrorCategory.Info, 2, false), token))
        {
            await AssertRefusedAsync(infoMax, "yearlyMax", "flightops:errors.yearlyMaxOnlyWarning", token);
        }

        // The general rule of the disconnections: only the total written, the single one the check's starting value.
        var code = Code("G");
        var general = await CreatedRuleAsync(
            coordinator,
            Rule(null, code, CheckCatalog.Disconnections, """{"maxTotalDisconnectMinutes":25}""", [disconnected, hidden]),
            token);
        Assert.Equal("""{"maxSingleDisconnectMinutes":15,"maxTotalDisconnectMinutes":25}""", general.GetProperty("parameters").GetRawText());

        // A parameter out of bounds is refused on its field; a code is once among the general rules.
        using (var wrong = await coordinator.PostAsJsonAsync(
            RuleEndpoints.RulesPattern,
            Rule(null, Code("G"), CheckCatalog.Disconnections, """{"maxSingleDisconnectMinutes":-5}""", []),
            token))
        {
            await AssertRefusedAsync(wrong, "parameters.maxSingleDisconnectMinutes", "errors.number.range", token);
        }

        using (var taken = await coordinator.PostAsJsonAsync(RuleEndpoints.RulesPattern, Rule(null, code, null, "{}", []), token))
        {
            await AssertRefusedAsync(taken, "code", "flightops:errors.ruleCodeTaken", token);
        }

        // The list of the general rules shows its values, with their units, and its two errors.
        var generals = await OkAsync(await advisor.GetAsync($"{RuleEndpoints.RulesPattern}?pageSize=100&q={code}", token), token);
        var listed = Assert.Single(generals.GetProperty("items").EnumerateArray(), row => row.GetProperty("code").GetString() == code);
        Assert.Equal(["15 min", "Σ 25 min"], listed.GetProperty("values").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(2, listed.GetProperty("errors").GetInt32());

        // A tour amends it, with the single disconnection only; its check is the general rule's, whatever it sends.
        var tour = await CreatedTourAsync(advisor, token);
        var amendment = await CreatedRuleAsync(
            advisor,
            Rule(Id(tour), Code("A"), CheckCatalog.Vmc, """{"maxSingleDisconnectMinutes":10}""", [], amends: Id(general)),
            token);
        Assert.Equal(CheckCatalog.Disconnections, amendment.GetProperty("checkKey").GetString());
        Assert.Equal("""{"maxSingleDisconnectMinutes":10}""", amendment.GetProperty("parameters").GetRawText());

        // Once per tour.
        using (var twice = await advisor.PostAsJsonAsync(
            RuleEndpoints.RulesPattern,
            Rule(Id(tour), Code("A"), null, "{}", [], amends: Id(general)),
            token))
        {
            await AssertRefusedAsync(twice, "amendsRuleId", "flightops:errors.alreadyAmended", token);
        }

        // In force on the tour: the amendment in the general rule's place, with the total of the general rule.
        var effective = Mine(await EffectiveAsync(advisor, Id(tour), token), code, amendment);
        Assert.Equal("""{"maxSingleDisconnectMinutes":10,"maxTotalDisconnectMinutes":25}""", effective.GetProperty("parameters").GetRawText());
        Assert.Equal(code, effective.GetProperty("amendsCode").GetString());
        Assert.Equal([Id(disconnected), Id(hidden)], effective.GetProperty("errorIds").EnumerateArray().Select(id => id.GetInt64()).Order());

        // The general rule changes its total, and the tour follows it for what it did not amend (Carmine, 22 September 2026).
        await OkAsync(await coordinator.PutAsJsonAsync(
            $"{RuleEndpoints.RulesPattern}/{Id(general)}",
            Rule(null, code, CheckCatalog.Disconnections, """{"maxSingleDisconnectMinutes":15,"maxTotalDisconnectMinutes":30}""", [disconnected, hidden])
                with { RowVersion = general.GetProperty("rowVersion").GetDateTime() },
            token), token);
        effective = Mine(await EffectiveAsync(advisor, Id(tour), token), code, amendment);
        Assert.Equal("""{"maxSingleDisconnectMinutes":10,"maxTotalDisconnectMinutes":30}""", effective.GetProperty("parameters").GetRawText());

        // A general rule some tour amends is not deleted.
        using (var delete = await coordinator.DeleteAsync($"{RuleEndpoints.RulesPattern}/{Id(general)}", token))
        {
            await AssertRefusedAsync(delete, "id", "flightops:errors.ruleHasAmendments", token);
        }

        // The block shows the public error, with the general rule it belongs to, to anybody; never the hidden one.
        using var anonymous = _factory.CreateClient();
        var catalogue = await OkAsync(await anonymous.GetAsync($"/api/blocks/data/{ErrorCatalogProvider.BlockType}", token), token);
        var items = catalogue.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetInt64() == Id(hidden));
        var shown = Assert.Single(items, item => item.GetProperty("id").GetInt64() == Id(disconnected));
        Assert.Equal("Warning", shown.GetProperty("category").GetString());
        Assert.Equal(3, shown.GetProperty("yearlyMax").GetInt32());
        Assert.Equal([code], shown.GetProperty("rules").EnumerateArray().Select(rule => rule.GetProperty("code").GetString()));

        // The error list says how many rules in force name each error.
        var errors = await OkAsync(await advisor.GetAsync($"{RuleEndpoints.ErrorsPattern}?pageSize=100", token), token);
        Assert.Equal(1, Assert.Single(errors.GetProperty("items").EnumerateArray(), row => Id(row) == Id(disconnected)).GetProperty("rules").GetInt32());
    }

    [Fact]
    public async Task TheCopyAndTheTemplateCarryParametersAndErrors()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(_factory, CoordinatorVid, token);
        using var advisor = await SignedInAsync(_factory, AdvisorVid, token);

        var error = await CreatedErrorAsync(advisor, "fo-test landing", ErrorCategory.Info, yearlyMax: null, isPublic: false, token);
        var general = await CreatedRuleAsync(advisor, Rule(null, Code("G"), CheckCatalog.Speed250, "{}", []), token);
        Assert.Equal("""{"toleranceKt":10}""", general.GetProperty("parameters").GetRawText());

        // The source: an amendment, and a rule of its own with a check and an error.
        var source = await CreatedTourAsync(advisor, token);
        var ownCode = Code("S");
        var amendCode = Code("S");
        await CreatedRuleAsync(advisor, Rule(Id(source), amendCode, null, """{"toleranceKt":20}""", [], amends: Id(general)), token);
        await CreatedRuleAsync(advisor, Rule(Id(source), ownCode, CheckCatalog.LandingAtArrival, """{"radiusNm":3}""", [error]), token);

        // The target already amends the same general rule: its own stays, and the copy says which it left out.
        var target = await CreatedTourAsync(advisor, token);
        var kept = await CreatedRuleAsync(advisor, Rule(Id(target), Code("T"), null, "{}", [], amends: Id(general)), token);

        var copy = await OkAsync(await advisor.PostAsJsonAsync(
            $"{TourEndpoints.Pattern}/{Id(target)}/copy-rules",
            new CopyRulesRequest(Id(source)),
            token), token);
        Assert.Equal(1, copy.GetProperty("copied").GetInt32());
        Assert.Equal([amendCode], copy.GetProperty("skipped").EnumerateArray().Select(skipped => skipped.GetString()));

        var rules = await TourRulesAsync(advisor, Id(target), token);
        Assert.Equal(
            new[] { kept.GetProperty("code").GetString()!, ownCode }.Order(StringComparer.Ordinal),
            rules.Select(rule => rule.GetProperty("code").GetString()!).Order(StringComparer.Ordinal));
        var copied = rules.Single(rule => rule.GetProperty("code").GetString() == ownCode);
        Assert.Equal(["3 NM"], copied.GetProperty("values").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(1, copied.GetProperty("errors").GetInt32());

        // A copy onto itself, or from a tour that does not exist, is refused.
        using (var itself = await advisor.PostAsJsonAsync($"{TourEndpoints.Pattern}/{Id(target)}/copy-rules", new CopyRulesRequest(Id(target)), token))
        {
            await AssertRefusedAsync(itself, "sourceTourId", "flightops:errors.tourUnknown", token);
        }

        // A template of the source carries both its rules, parameters and errors (§1.10).
        using var saved = await coordinator.PostAsJsonAsync(
            $"{TourEndpoints.Pattern}/{Id(source)}/save-as-template",
            new TourSaveAsTemplateRequest(Text("fo-test rules template", Locales)),
            token);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var template = await saved.Content.ReadFromJsonAsync<JsonElement>(token);
        _tours.Add(Id(template));

        var carried = await TourRulesAsync(coordinator, Id(template), token);
        Assert.Equal(
            new[] { amendCode, ownCode }.Order(StringComparer.Ordinal),
            carried.Select(rule => rule.GetProperty("code").GetString()!).Order(StringComparer.Ordinal));
        var amendment = carried.Single(rule => rule.GetProperty("code").GetString() == amendCode);
        Assert.Equal(Id(general), amendment.GetProperty("amendsRuleId").GetInt64());
        Assert.Equal(["± 20 kt"], amendment.GetProperty("values").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(1, carried.Single(rule => rule.GetProperty("code").GetString() == ownCode).GetProperty("errors").GetInt32());

        // A template's rules are changed with the template's own permission: the advisor has not got it (§7.2).
        var templateRule = carried.Single(rule => rule.GetProperty("code").GetString() == ownCode);
        using (var denied = await advisor.PutAsJsonAsync(
            $"{RuleEndpoints.RulesPattern}/{Id(templateRule)}",
            Rule(Id(template), ownCode, CheckCatalog.LandingAtArrival, """{"radiusNm":4}""", [error]) with
            {
                RowVersion = templateRule.GetProperty("rowVersion").GetDateTime(),
            },
            token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        // A general rule amends nothing.
        using (var generalAmends = await advisor.PostAsJsonAsync(
            RuleEndpoints.RulesPattern,
            Rule(null, Code("G"), null, "{}", [], amends: Id(general)),
            token))
        {
            await AssertRefusedAsync(generalAmends, "amendsRuleId", "flightops:errors.generalAmendsNothing", token);
        }
    }

    private static string Code(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    private static TourRuleWriteDto Rule(
        long? tourId,
        string code,
        string? check,
        string parameters,
        IReadOnlyList<JsonElement> errors,
        long? amends = null) => new(
        tourId,
        code,
        Text(code, Locales),
        Text($"The rule {code}.", Locales),
        amends,
        check,
        JsonNode.Parse(parameters),
        [.. errors.Select(Id)],
        Sort: 0,
        Retired: false,
        RowVersion: default);

    private static TourErrorWriteDto Error(string name, ErrorCategory category, int? yearlyMax, bool isPublic) => new(
        Text(name, Locales),
        Text($"{name} described.", Locales),
        null,
        category,
        yearlyMax,
        null,
        isPublic,
        Retired: false,
        RowVersion: default);

    private async Task<JsonElement> CreatedErrorAsync(
        HttpClient client,
        string name,
        ErrorCategory category,
        int? yearlyMax,
        bool isPublic,
        CancellationToken cancellationToken)
    {
        var error = await CreatedAsync(client, RuleEndpoints.ErrorsPattern, Error(name, category, yearlyMax, isPublic), cancellationToken);
        _errors.Add(Id(error));
        return error;
    }

    private async Task<JsonElement> CreatedRuleAsync(HttpClient client, TourRuleWriteDto payload, CancellationToken cancellationToken)
    {
        var rule = await CreatedAsync(client, RuleEndpoints.RulesPattern, payload, cancellationToken);
        _rules.Add(Id(rule));
        return rule;
    }

    private static async Task<List<JsonElement>> EffectiveAsync(HttpClient client, long tourId, CancellationToken cancellationToken) =>
        [.. (await OkAsync(await client.GetAsync($"{TourEndpoints.Pattern}/{tourId}/effective-rules", cancellationToken), cancellationToken))
            .EnumerateArray()];

    /// <summary>The rule in force that is this test's: the amendment standing in for the general rule of that code.</summary>
    private static JsonElement Mine(List<JsonElement> effective, string generalCode, JsonElement amendment)
    {
        Assert.DoesNotContain(effective, rule => rule.GetProperty("code").GetString() == generalCode);
        return Assert.Single(effective, rule => Id(rule) == Id(amendment));
    }

    private static async Task<List<JsonElement>> TourRulesAsync(HttpClient client, long tourId, CancellationToken cancellationToken) =>
        [.. (await OkAsync(await client.GetAsync($"{RuleEndpoints.RulesPattern}?filter[tour]={tourId}&pageSize=100", cancellationToken), cancellationToken))
            .GetProperty("items").EnumerateArray()];

    private async Task<JsonElement> CreatedTourAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var slug = $"fo-test-rules-{Guid.NewGuid():N}"[..28];
        var payload = new TourWriteDto(
            OwnerDepartment: Department.FOD,
            IsTemplate: false,
            Slug: slug,
            Kind: TourKind.Free,
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
        _tours.Add(Id(body));
        return body;
    }

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
        user.LastName = "Rules";
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
