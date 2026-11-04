using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The settings of the content, `/api/content/settings` (<c>decisions/2026-11-04-la-pagina-dello-stile.md</c>): the page of
/// the division's style, written by whoever cures the templates and read, **with their own visibility**, by whoever
/// downloads the instructions for Claude. VIDs 784001–784004; the pages are written under a shelf of their own.
/// <para>⚠️ The key is one row of the whole division, so every test sets the value it then asks about.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContentSettingsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 784001;
    private const int EventsCoordinatorVid = 784002;
    private const int MemberVid = 784003;
    private const int WebCoordinatorVid = 784004;

    private HubWebApplicationFactory _factory = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _shelf = await TestShelf.SeedAsync(_factory, "test-shelf-content-settings", TestContext.Current.CancellationToken);

        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(MemberVid, cancellationToken: token);
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task APageForTheWholeStaffIsChosenAndReadByWhoeverDownloads()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var (id, path) = await PublishedPageAsync(superadmin, Visibility.Staff, token);

        using var saved = await PutAsync(superadmin, id, token);
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(token));

        // A coordinator of another department, who downloads the instructions of a page of theirs.
        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);
        var read = await coordinator.GetFromJsonAsync<JsonElement>(ContentSettingsEndpoints.Pattern, token);

        Assert.Equal(id, read.GetProperty("stylePageId").GetInt64());
        Assert.Equal(path, read.GetProperty("stylePagePath").GetString());
    }

    [Fact]
    public async Task APageOfOneDepartmentIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var (id, _) = await PublishedPageAsync(superadmin, Visibility.Department, token);

        using var refused = await PutAsync(superadmin, id, token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Contains(
            ContentSettingsEndpoints.StylePageTooNarrowKey,
            problem.GetProperty("errors").GetProperty("stylePageId").EnumerateArray().Select(each => each.GetString()));
    }

    [Fact]
    public async Task APageNeverPublishedIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var id = await DraftPageAsync(superadmin, Visibility.Staff, token);

        using var refused = await PutAsync(superadmin, id, token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Contains(
            ContentSettingsEndpoints.StylePageNotFoundKey,
            problem.GetProperty("errors").GetProperty("stylePageId").EnumerateArray().Select(each => each.GetString()));
    }

    [Fact]
    public async Task APageNarrowedAfterwardsIsNotGivenToWhoeverCannotReadIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var (id, _) = await PublishedPageAsync(superadmin, Visibility.Staff, token);

        using var saved = await PutAsync(superadmin, id, token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // Narrowed to the web team after it was chosen, behind the setting's back.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var row = await database.Contents.IgnoreQueryFilters().SingleAsync(each => each.Id == id, token);
            row.Visibility = Visibility.Department;
            await database.SaveChangesAsync(token);
        }

        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);
        var read = await coordinator.GetFromJsonAsync<JsonElement>(ContentSettingsEndpoints.Pattern, token);

        // The choice stays; the address is not there for somebody who would not be able to open it.
        Assert.Equal(id, read.GetProperty("stylePageId").GetInt64());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("stylePagePath").ValueKind);
    }

    [Fact]
    public async Task NoneTakesTheChoiceAway()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var (id, _) = await PublishedPageAsync(superadmin, Visibility.Staff, token);

        using (var saved = await PutAsync(superadmin, id, token))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        using (var cleared = await PutAsync(superadmin, null, token))
        {
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        }

        var read = await superadmin.GetFromJsonAsync<JsonElement>(ContentSettingsEndpoints.Pattern, token);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("stylePageId").ValueKind);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("stylePagePath").ValueKind);
    }

    [Fact]
    public async Task OnlyWhoeverReachesEveryDepartmentChangesTheStyle()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var (id, _) = await PublishedPageAsync(superadmin, Visibility.Staff, token);

        // A coordinator cures the templates of their department, and still does not decide for the whole division
        // (Carmine, 4 November 2026: «Solo chi ha tutti»).
        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);
        using (var refused = await PutAsync(coordinator, id, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The web coordinator reaches every department, and does.
        using var web = await SignedInAsync(WebCoordinatorVid, token);
        using (var saved = await PutAsync(web, id, token))
        {
            Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(token));
        }
    }

    [Fact]
    public async Task AMemberWithoutTheStaffsPermissionsNeitherReadsNorWrites()
    {
        var token = TestContext.Current.CancellationToken;

        using var member = await SignedInAsync(MemberVid, token);
        using (var read = await member.GetAsync(new Uri(ContentSettingsEndpoints.Pattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        }

        using (var written = await PutAsync(member, null, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, written.StatusCode);
        }
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, long? id, CancellationToken cancellationToken) =>
        await SendAsync(client, HttpMethod.Put, ContentSettingsEndpoints.Pattern, new { stylePageId = id }, cancellationToken);

    /// <summary>A page of the web team under this class's shelf, published by the superadmin.</summary>
    private async Task<(long Id, string Path)> PublishedPageAsync(
        HttpClient client,
        Visibility visibility,
        CancellationToken cancellationToken)
    {
        var id = await DraftPageAsync(client, visibility, cancellationToken);

        using var published = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{id}/publish",
            new { changelog = (string?)null },
            cancellationToken);
        Assert.True(published.StatusCode == HttpStatusCode.OK, await published.Content.ReadAsStringAsync(cancellationToken));

        var row = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", cancellationToken);
        return (id, $"/{row.GetProperty("path").GetString()}");
    }

    private async Task<long> DraftPageAsync(HttpClient client, Visibility visibility, CancellationToken cancellationToken)
    {
        var slug = $"style-{Guid.NewGuid():N}"[..20];
        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            new
            {
                kind = nameof(ContentKind.Page),
                slug,
                ownerDepartment = nameof(Department.WD),
                visibility = visibility.ToString(),
                isTemplate = false,
                title = new Dictionary<string, string> { ["it"] = "Stile", ["en"] = "Style" },
                summary = (Dictionary<string, string>?)null,
                seo = (Dictionary<string, object>?)null,
                body = JsonNode.Parse("""
                    { "schemaVersion": 1, "sections": [
                      { "id": "s_main", "layout": "stacked", "blocks": [
                        { "id": "b_text", "type": "text", "version": 1,
                          "props": { "markdown": { "it": "Frasi brevi.", "en": "Short sentences." } } } ] } ] }
                    """),
                schemaVersion = 1,
                rowVersion = "0001-01-01T00:00:00",
                parentId = _shelf,
            },
            cancellationToken);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(cancellationToken));
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Content = JsonContent.Create(payload, payload.GetType());
        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedUserAsync(
        int vid,
        bool isSuperadmin = false,
        string? position = null,
        CancellationToken cancellationToken = default)
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
        user.LastName = "User";
        user.IsSuperadmin = isSuperadmin;
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
