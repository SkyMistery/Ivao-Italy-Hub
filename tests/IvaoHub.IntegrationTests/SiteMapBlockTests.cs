using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The map of the site (design <c>13-design-struttura-del-sito.md</c> §5.1): the block answers <b>whoever is reading</b> — the
/// pages online and the screens they may open —, is never captured, and no answer of it is kept by a shared cache, not even a
/// visitor's. VIDs 787001–787002; the pages are the web team's, under a shelf of this class, and the
/// map is asked from that shelf, because the database is shared with every other class.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SiteMapBlockTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 787001;
    private const int MemberVid = 787002;
    private const string ShelfSlug = "test-shelf-site-map";

    private HubWebApplicationFactory _factory = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;
        _shelf = await TestShelf.SeedAsync(_factory, ShelfSlug, token);

        await SeedUserAsync(SuperadminVid, isSuperadmin: true, token);
        await SeedUserAsync(MemberVid, isSuperadmin: false, token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task EachReaderIsAnsweredWithWhatTheyMayOpen()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var open = await WriteAsync(superadmin, Visibility.Public, _shelf, publish: true, token);
        var under = await WriteAsync(superadmin, Visibility.Public, open.Id, publish: true, token);
        var members = await WriteAsync(superadmin, Visibility.Members, _shelf, publish: true, token);
        var staff = await WriteAsync(superadmin, Visibility.Staff, _shelf, publish: true, token);
        var draft = await WriteAsync(superadmin, Visibility.Public, _shelf, publish: false, token);

        var root = new JsonObject { ["href"] = $"/{ShelfSlug}" };

        using var visitor = _factory.CreateApiClient();
        using var anonymous = await visitor.GetAsync(BlockDataUri(root), token);
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        var seen = await PathsAsync(anonymous, token);

        Assert.Contains(open.Path, seen);
        Assert.Contains(under.Path, seen);
        Assert.DoesNotContain(members.Path, seen);
        Assert.DoesNotContain(staff.Path, seen);
        Assert.DoesNotContain(draft.Path, seen);

        // A map of one section has no screens: a screen has no pages under it (answer 4 of the maintainer).
        Assert.All(seen, path => Assert.StartsWith($"/{ShelfSlug}/", path, StringComparison.Ordinal));

        // Not even a visitor's map is kept by a shared cache: every reader asks the same address, and a cache keyed without
        // the cookies would answer a member with it (the maintainer on #555, finding 3).
        Assert.DoesNotContain("s-maxage", anonymous.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.True(anonymous.Headers.CacheControl?.NoStore, "The anonymous answer of the map is not no-store.");

        using var member = await SignedInAsync(MemberVid, token);
        using var signedIn = await member.GetAsync(BlockDataUri(root), token);
        var theirs = await PathsAsync(signedIn, token);

        Assert.Contains(members.Path, theirs);
        Assert.DoesNotContain(staff.Path, theirs);
        Assert.DoesNotContain("s-maxage", signedIn.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        using var everything = await superadmin.GetAsync(BlockDataUri(root), token);
        Assert.Contains(staff.Path, await PathsAsync(everything, token));
    }

    [Fact]
    public async Task TheWholeSiteCarriesTheScreensAndAPageUnderTheTitleOnline()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var page = await WriteAsync(superadmin, Visibility.Public, _shelf, publish: true, token);

        // A new title written into the draft, waiting: the map keeps saying what was published.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var row = await database.Contents.IgnoreQueryFilters().SingleAsync(content => content.Id == page.Id, token);
            row.Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Bozza nuova"),
                new KeyValuePair<string, string>("en", "New draft"),
            ]);
            await database.SaveChangesAsync(token);
        }

        using var visitor = _factory.CreateApiClient();
        var answer = await visitor.GetFromJsonAsync<JsonElement>(BlockDataUri(new JsonObject()), token);
        var items = answer.GetProperty("items").EnumerateArray().ToList();

        var line = Assert.Single(items, item => item.GetProperty("path").GetString() == page.Path);
        Assert.Equal("Address", line.GetProperty("title").GetProperty("en").GetString());

        // The core's screens, each named by its key; `/contact` too, which is declared for everybody (note
        // 2026-11-22-la-mappa-del-sito §4).
        foreach (var screen in SiteDestinations.CoreScreens)
        {
            var drawn = Assert.Single(items, item => item.GetProperty("path").GetString() == screen.Path);
            Assert.Equal(screen.TitleKey, drawn.GetProperty("titleKey").GetString());
        }
    }

    [Fact]
    public async Task AnAddressOfElsewhereIsNoRoot()
    {
        var token = TestContext.Current.CancellationToken;
        using var visitor = _factory.CreateApiClient();

        var answer = await visitor.GetFromJsonAsync<JsonElement>(
            BlockDataUri(new JsonObject { ["href"] = "https://elsewhere.example/about" }),
            token);

        Assert.Empty(answer.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task PublicationNeverCapturesIt()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var blocks = scope.ServiceProvider.GetRequiredService<BlockRegistry>();
        var providers = scope.ServiceProvider.GetRequiredService<DataBlockProviders>();

        var descriptor = blocks.Find(CoreBlocks.SiteMap);
        Assert.NotNull(descriptor);
        Assert.True(descriptor.AlwaysLive);
        Assert.Equal(Visibility.Public, descriptor.Audience);

        // An answer that depends on the reader is never shared (the maintainer on #555, finding 3).
        Assert.Null(descriptor.SharedFor);

        // And asked for a capture all the same, it answers nothing rather than the map of whoever pressed publish.
        var provider = providers.For(descriptor)!;
        var captured = await provider.ResolveAsync(
            new JsonObject(),
            DataBlockContext.Publishing(Visibility.Public, Department.WD),
            TestContext.Current.CancellationToken);

        Assert.Empty(captured["items"]!.AsArray());
    }

    private sealed record Written(long Id, string Path);

    private static Uri BlockDataUri(JsonNode props)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Uri($"/api/blocks/data/{CoreBlocks.SiteMap}?props={encoded}", UriKind.Relative);
    }

    private static async Task<List<string>> PathsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return [.. answer.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("path").GetString()!)];
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task<Written> WriteAsync(
        HttpClient client,
        Visibility visibility,
        long parentId,
        bool publish,
        CancellationToken cancellationToken)
    {
        var slug = $"map-{Guid.NewGuid():N}"[..20];
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
                parentId,
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

        return new Written(id, $"/{row.GetProperty("path").GetString()}");
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

    private async Task SeedUserAsync(int vid, bool isSuperadmin, CancellationToken cancellationToken)
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
        user.IsStaff = false;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
