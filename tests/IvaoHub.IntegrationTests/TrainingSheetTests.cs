using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Reference;
using IvaoHub.Modules.Training.Sheets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The evaluation sheet of the training (M3, A5; design M3 §1.4), through the real host: whoever manages the sheets composes
/// the sheet of a rating in every language of the division and reads it back in its order; a missing language and a rating
/// with no practical training are refused on their fields; nobody else writes it; and an item a report marks is switched off,
/// never deleted.
/// <para>⚠️ Everybody holds what they hold by a grant to their VID and has no address (<c>CONTRIBUTING.md</c>, "Tests"). The
/// coordinator and the assistant of the training department hold <c>Training.ManageSheets</c> and <c>Training.Edit</c> together
/// (design M3 §3.2): the first is what the screen and the engine ask, the second what the write guard asks of every row of the
/// staff (§3.1), as it asks <c>Tours.Edit</c> of whoever holds <c>Tours.ManageAircraft</c>.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class TrainingSheetTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the training module owns in the shared database (CONTRIBUTING.md); 790001–790013 are A1's, A3's and A4's.
    private const int ManagerVid = 790014;
    private const int EditorVid = 790015;
    private const int SheetsOnlyVid = 790016;

    private const string GrantReason = "trn-test";

    private static readonly Uri Items = new(SheetItemEndpoints.Pattern, UriKind.Relative);

    private readonly List<long> _created = [];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        foreach (var vid in new[] { ManagerVid, EditorVid, SheetsOnlyVid })
        {
            await SeedUserAsync(vid, token);
        }

        await GrantAsync(ManagerVid, TrainingPermissions.ManageSheets, token);
        await GrantAsync(ManagerVid, TrainingPermissions.Edit, token);
        await GrantAsync(EditorVid, TrainingPermissions.Edit, token);
        await GrantAsync(SheetsOnlyVid, TrainingPermissions.ManageSheets, token);
    }

    public async ValueTask DisposeAsync()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
            await database.SheetItems.Where(item => _created.Contains(item.Id)).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task WhoManagesTheSheetsComposesTheSheetOfARatingInEveryLanguageAndReadsItBackInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var rating = TrainedRating(RatingKind.Atc);
        var stem = Stem();

        using var manager = await SignedInAsync(_factory, ManagerVid, token);

        // Written out of order on purpose: the sheet reads by its order, not by when an item was written.
        var theory = await CreatedAsync(manager, Payload(rating, SheetSection.Theory, $"{stem} theory", sort: 2), token);
        var last = await CreatedAsync(manager, Payload(rating, SheetSection.Practice, $"{stem} second practice", sort: 3), token);
        var first = await CreatedAsync(manager, Payload(rating, SheetSection.Practice, $"{stem} first practice", sort: 1), token);

        // The sheet of that rating, narrowed as the screen narrows it, with the words of every language kept as written.
        var sheet = await manager.GetFromJsonAsync<JsonElement>(
            $"{SheetItemEndpoints.Pattern}?filter[kind]={rating.Kind}&filter[rating]={rating.Number}&pageSize=100",
            token);
        var ours = sheet.GetProperty("items").EnumerateArray()
            .Where(item => Title(item).Contains(stem, StringComparison.Ordinal))
            .ToList();

        Assert.Equal([Id(first), Id(theory), Id(last)], ours.Select(Id));
        Assert.All(ours, item =>
        {
            Assert.Equal(rating.Kind.ToString(), item.GetProperty("kind").GetString());
            Assert.Equal(rating.Number, item.GetProperty("rating").GetInt32());
            Assert.Equal(rating.ShortName, item.GetProperty("ratingShortName").GetString());
            Assert.True(item.GetProperty("isActive").GetBoolean());
        });
        Assert.Equal(
            [nameof(SheetSection.Practice), nameof(SheetSection.Theory), nameof(SheetSection.Practice)],
            ours.Select(item => item.GetProperty("section").GetString()));

        var read = await manager.GetFromJsonAsync<JsonElement>($"{SheetItemEndpoints.Pattern}/{Id(theory)}", token);
        foreach (var locale in Locales())
        {
            Assert.Equal($"{stem} theory ({locale})", read.GetProperty("title").GetProperty(locale).GetString());
        }

        // The other ladder's sheet does not hold them, and the row is the base department's, written by whoever wrote it.
        var other = TrainedRating(RatingKind.Pilot);
        var pilots = await manager.GetFromJsonAsync<JsonElement>(
            $"{SheetItemEndpoints.Pattern}?filter[kind]={other.Kind}&filter[rating]={other.Number}&pageSize=100",
            token);
        Assert.DoesNotContain(pilots.GetProperty("items").EnumerateArray(), item => Title(item).Contains(stem, StringComparison.Ordinal));

        var firstId = Id(first);
        var firstKey = firstId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        await using var scope = _factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<TrainingDbContext>().SheetItems.AsNoTracking()
            .SingleAsync(item => item.Id == firstId, token);
        Assert.Equal(Department.TD, stored.OwnerDepartment);
        Assert.Equal(ManagerVid, stored.CreatedBy);

        // Every write of an item is in the audit log, under its table.
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking()
            .AnyAsync(entry => entry.Entity == "trn_sheet_items" && entry.EntityId == firstKey && entry.Vid == ManagerVid, token));
    }

    [Fact]
    public async Task AMissingLanguageAndARatingWithNoPracticalTrainingAreRefusedOnTheirFields()
    {
        var token = TestContext.Current.CancellationToken;
        var rating = TrainedRating(RatingKind.Atc);
        var stem = Stem();

        using var manager = await SignedInAsync(_factory, ManagerVid, token);

        // A title in one language only, when the division speaks more.
        var oneLanguage = Payload(rating, SheetSection.Practice, stem, sort: 1) with
        {
            Title = new Dictionary<string, string> { [Locales()[0]] = stem },
        };
        using (var refused = await manager.PostAsJsonAsync(Items, oneLanguage, token))
        {
            await AssertRefusedAsync(refused, "title", "errors.localized.missing", token);
        }

        // A rating the core knows and gives no practical training.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var untrained = scope.ServiceProvider.GetRequiredService<RatingVocabulary>().Ladder(RatingKind.Atc)
                .First(candidate => !candidate.HasPracticalTraining);

            using var refused = await manager.PostAsJsonAsync(Items, Payload(untrained, SheetSection.Practice, stem, sort: 1), token);
            await AssertRefusedAsync(refused, "rating", "training:errors.ratingNotTrained", token);
        }

        var all = await manager.GetFromJsonAsync<JsonElement>($"{SheetItemEndpoints.Pattern}?pageSize=100&q={stem}", token);
        Assert.Empty(all.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task OnlyWhoManagesTheSheetsReadsAndWritesThem()
    {
        var token = TestContext.Current.CancellationToken;
        var payload = Payload(TrainedRating(RatingKind.Pilot), SheetSection.Theory, Stem(), sort: 1);

        // Editing the trainings is not managing the sheet: neither the list nor a new item.
        using (var editor = await SignedInAsync(_factory, EditorVid, token))
        {
            using var list = await editor.GetAsync(Items, token);
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

            using var write = await editor.PostAsJsonAsync(Items, payload, token);
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

            Assert.DoesNotContain("/staff/training/sheets", await StaffEntriesOfAsync(editor, token));
        }

        // The sheets alone open the screen and pass the engine, but the write guard asks Training.Edit of every row of the
        // staff (design M3 §3.1): the coordinator and the assistant hold both.
        using (var sheetsOnly = await SignedInAsync(_factory, SheetsOnlyVid, token))
        {
            using var list = await sheetsOnly.GetAsync(Items, token);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            using var write = await sheetsOnly.PostAsJsonAsync(Items, payload, token);
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        }

        using (var manager = await SignedInAsync(_factory, ManagerVid, token))
        {
            Assert.Contains("/staff/training/sheets", await StaffEntriesOfAsync(manager, token));
        }

        using var anonymous = _factory.CreateApiClient();
        using var refused = await anonymous.GetAsync(Items, token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task AnItemAReportMarksIsSwitchedOffAndNeverDeleted()
    {
        var token = TestContext.Current.CancellationToken;
        var stem = Stem();

        // The reports arrive with A9: here an item "has" them because the answer says so.
        await using var withReports = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<ISheetItemReports, EveryItemHasReports>()));
        using var manager = await SignedInAsync(withReports, ManagerVid, token);

        var payload = Payload(TrainedRating(RatingKind.Atc), SheetSection.Practice, stem, sort: 1);
        var item = await CreatedAsync(manager, payload, token);

        using (var refused = await manager.DeleteAsync(new Uri($"{SheetItemEndpoints.Pattern}/{Id(item)}", UriKind.Relative), token))
        {
            await AssertRefusedAsync(refused, "id", "training:errors.sheetItemUsed", token);
        }

        // Switched off instead, and still there.
        using (var off = await manager.PutAsJsonAsync(
            new Uri($"{SheetItemEndpoints.Pattern}/{Id(item)}", UriKind.Relative),
            payload with { IsActive = false, RowVersion = item.GetProperty("rowVersion").GetDateTime() },
            token))
        {
            Assert.True(off.StatusCode == HttpStatusCode.OK, await off.Content.ReadAsStringAsync(token));
        }

        var read = await manager.GetFromJsonAsync<JsonElement>($"{SheetItemEndpoints.Pattern}/{Id(item)}", token);
        Assert.False(read.GetProperty("isActive").GetBoolean());

        // With the answer of today — no report marks anything yet — the same item is deleted.
        using var plain = await SignedInAsync(_factory, ManagerVid, token);
        using (var deleted = await plain.DeleteAsync(new Uri($"{SheetItemEndpoints.Pattern}/{Id(item)}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using var gone = await plain.GetAsync(new Uri($"{SheetItemEndpoints.Pattern}/{Id(item)}", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    /// <summary>What a client sends: a title in every language of the division, each saying which one it is.</summary>
    private sealed record ItemPayload(
        string Kind,
        int Rating,
        string Section,
        Dictionary<string, string> Title,
        int Sort,
        bool IsActive,
        DateTime RowVersion);

    private sealed class EveryItemHasReports : ISheetItemReports
    {
        public Task<bool> AnyAsync(long itemId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private ItemPayload Payload(Rating rating, SheetSection section, string title, int sort) => new(
        rating.Kind.ToString(),
        rating.Number,
        section.ToString(),
        Locales().ToDictionary(locale => locale, locale => $"{title} ({locale})"),
        sort,
        IsActive: true,
        RowVersion: default);

    /// <summary>A rating of that ladder the core's vocabulary gives a practical training, as the form offers it.</summary>
    private Rating TrainedRating(RatingKind kind)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TrainingReference>().Ratings.First(rating => rating.Kind == kind);
    }

    private IReadOnlyList<string> Locales() => _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value.Locales;

    /// <summary>The title of a row in the first language of the division, where the stem of this run is.</summary>
    private string Title(JsonElement item) => item.GetProperty("title").GetProperty(Locales()[0]).GetString() ?? string.Empty;

    private static string Stem() => $"trn-test-{Guid.NewGuid():N}"[..20];

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private async Task<JsonElement> CreatedAsync(HttpClient client, ItemPayload payload, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(Items, payload, cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _created.Add(Id(body));
        return body;
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        var problem = JsonDocument.Parse(text).RootElement;
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var keys), text);
        Assert.Contains(key, keys.EnumerateArray().Select(error => error.GetString()));
    }

    /// <summary>The addresses of the back office entries of the training a member is offered.</summary>
    private static async Task<IEnumerable<string>> StaffEntriesOfAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken);

        return me.GetProperty("navigation").GetProperty("staff").EnumerateArray()
            .Where(entry => entry.TryGetProperty("module", out var module) && module.GetString() == TrainingModule.ModuleKey)
            .Select(entry => entry.GetProperty("path").GetString()!)
            .ToList();
    }

    private static async Task<HttpClient> SignedInAsync(WebApplicationFactory<Program> factory, int vid, CancellationToken cancellationToken)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");

        using var response = await client.PostAsync(new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>A member of the staff with no position and never an address: what they hold, they hold by a grant.</summary>
    private async Task SeedUserAsync(int vid, CancellationToken cancellationToken)
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
        user.LastName = "Sheet";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A permission on the training department, to one VID, once.</summary>
    private async Task GrantAsync(int vid, string permission, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (await database.UserGrants.AnyAsync(grant => grant.Vid == vid && grant.Value == permission && grant.Reason == GrantReason, cancellationToken))
        {
            return;
        }

        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.TD,
            Effect = GrantEffect.Grant,
            Reason = GrantReason,
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
