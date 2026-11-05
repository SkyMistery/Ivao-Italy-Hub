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
/// `GET /api/content/addresses` (<c>decisions/2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere.md</c>): the addresses of
/// the site whoever writes may read, of **every** department — what the site would show them, and nothing of the back office.
/// VIDs 785001–785003; the rows are the web team's, under a shelf of this class, and are found by their own slugs because the
/// database is shared with every other class.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContentSiteAddressesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 785001;
    private const int EventsCoordinatorVid = 785002;
    private const int MemberVid = 785003;

    private HubWebApplicationFactory _factory = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;
        _shelf = await TestShelf.SeedAsync(_factory, "test-shelf-site-addresses", token);

        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(MemberVid, cancellationToken: token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AStafferOfOneDepartmentReadsWhatTheSiteShowsThemOfEveryOther()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        // Of the web team, which the events coordinator is not a member of.
        var publicPage = await WriteAsync(superadmin, ContentKind.Page, Visibility.Public, publish: true, token);
        var staffPage = await WriteAsync(superadmin, ContentKind.Page, Visibility.Staff, publish: true, token);
        var teamPage = await WriteAsync(superadmin, ContentKind.Page, Visibility.Department, publish: true, token);
        var draftPage = await WriteAsync(superadmin, ContentKind.Page, Visibility.Public, publish: false, token);
        var news = await WriteAsync(superadmin, ContentKind.News, Visibility.Public, publish: true, token);

        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);
        var addresses = await coordinator.GetFromJsonAsync<JsonElement>(ContentEndpoints.Pattern + "/addresses", token);
        var byId = addresses.EnumerateArray().ToDictionary(address => address.GetProperty("id").GetInt64());

        Assert.True(byId.ContainsKey(publicPage.Id));
        Assert.True(byId.ContainsKey(staffPage.Id));
        Assert.Equal(publicPage.Url, byId[publicPage.Id].GetProperty("url").GetString());
        Assert.Equal(nameof(Visibility.Staff), byId[staffPage.Id].GetProperty("visibility").GetString());
        Assert.Equal(nameof(Department.WD), byId[staffPage.Id].GetProperty("ownerDepartment").GetString());

        // A page of the web team only, a draft: not what the site shows them.
        Assert.False(byId.ContainsKey(teamPage.Id));
        Assert.False(byId.ContainsKey(draftPage.Id));

        // A news item is an address too, at its own place on the site.
        Assert.Equal($"/news/{news.Slug}", byId[news.Id].GetProperty("url").GetString());
        Assert.Equal(nameof(ContentKind.News), byId[news.Id].GetProperty("kind").GetString());

        // No template is an address.
        var templates = await TemplateIdsAsync(token);
        Assert.Empty(byId.Keys.Intersect(templates));
    }

    [Fact]
    public async Task TheWebTeamsOwnPageIsThereForWhoeverReachesEveryDepartment()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var teamPage = await WriteAsync(superadmin, ContentKind.Page, Visibility.Department, publish: true, token);

        var addresses = await superadmin.GetFromJsonAsync<JsonElement>(ContentEndpoints.Pattern + "/addresses", token);

        Assert.Contains(addresses.EnumerateArray(), address => address.GetProperty("id").GetInt64() == teamPage.Id);
    }

    [Fact]
    public async Task AMemberWithoutContentEditIsNotAnswered()
    {
        var token = TestContext.Current.CancellationToken;
        using var member = await SignedInAsync(MemberVid, token);

        using var refused = await member.GetAsync(new Uri(ContentEndpoints.Pattern + "/addresses", UriKind.Relative), token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    private sealed record Written(long Id, string Slug, string Url);

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task<Written> WriteAsync(
        HttpClient client,
        ContentKind kind,
        Visibility visibility,
        bool publish,
        CancellationToken cancellationToken)
    {
        var slug = $"addr-{Guid.NewGuid():N}"[..20];
        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            new
            {
                kind = kind.ToString(),
                slug,
                ownerDepartment = nameof(Department.WD),
                visibility = visibility.ToString(),
                isTemplate = false,
                title = new Dictionary<string, string> { ["it"] = "Indirizzo", ["en"] = "Address" },
                summary = (Dictionary<string, string>?)null,
                seo = (Dictionary<string, object>?)null,
                body = JsonNode.Parse("""
                    { "schemaVersion": 1, "sections": [
                      { "id": "s_main", "layout": "stacked", "blocks": [
                        { "id": "b_text", "type": "text", "version": 1,
                          "props": { "markdown": { "it": "Testo.", "en": "Text." } } } ] } ] }
                    """),
                schemaVersion = 1,
                rowVersion = "0001-01-01T00:00:00",
                parentId = kind == ContentKind.Page ? _shelf : (long?)null,
            },
            cancellationToken);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(cancellationToken));
        var row = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var id = row.GetProperty("id").GetInt64();

        if (publish)
        {
            using var published = await SendAsync(
                client,
                HttpMethod.Post,
                $"{ContentEndpoints.Pattern}/{id}/publish",
                new { changelog = (string?)null },
                cancellationToken);
            Assert.True(published.StatusCode == HttpStatusCode.OK, await published.Content.ReadAsStringAsync(cancellationToken));
        }

        var url = kind == ContentKind.Page ? $"/{row.GetProperty("path").GetString()}" : $"/news/{slug}";
        return new Written(id, slug, url);
    }

    private async Task<HashSet<long>> TemplateIdsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        return [.. await database.Contents.IgnoreQueryFilters().Where(row => row.IsTemplate).Select(row => row.Id).ToListAsync(cancellationToken)];
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
