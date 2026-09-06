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
/// The acceptance of G5, run rather than described: news and documents are two <c>kind</c>s of one
/// table, the vocabulary they are filed under is rows a coordinator writes, and a template belongs
/// to a department while being readable by every one of them (design M1 sections 3 and 9.4).
/// <para>Everything here goes over the wire against a real MariaDB, with the real cookie, the real
/// policies and the real interceptor, because the questions being asked — "may this coordinator see
/// that?", "does the visitor get the draft?" — are questions about the whole stack.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class NewsDocumentsAndCategoriesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // ⚠️ A range of its own. The suite shares one database across the whole collection, so two
    // classes on the same VID are one row: this class first took 630001-630003, which
    // `SearchEndpointTests` already owns, and giving 630003 a web team position turned that suite's
    // flight operations coordinator into somebody who reaches every department — one extra row in a
    // count, three classes away, and nothing to do with the code under test.
    private const int EventsCoordinatorVid = 650001;
    private const int WebCoordinatorVid = 650002;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    // ---- the templates every department reads (design M1 section 9.4) --------------------------

    [Fact]
    public async Task TemplatesAreReadableByAnyStaff()
    {
        // The seeded templates belong to the web team. Before G5 a coordinator of any other
        // department was answered with an empty list and a 403, so "new from a template" simply did
        // not exist for eight departments out of nine.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        var templateId = await TemplateAsync("section-page", token);

        using var events = _factory.CreateApiClient();
        await _factory.SignInAsync(events, EventsCoordinatorVid, token);

        var listed = await events.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}?filter[isTemplate]=true",
            token);

        var owners = listed.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("ownerDepartment").GetString())
            .ToArray();

        Assert.NotEmpty(owners);
        Assert.Contains(nameof(Department.WD), owners);

        // The body too, and not only the row: without it `templateRules` falls back to "no rules"
        // and the sections a template locked stop looking locked in the editor.
        var read = await events.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/{templateId}",
            token);

        Assert.Equal(nameof(Department.WD), read.GetProperty("ownerDepartment").GetString());
        Assert.True(read.GetProperty("isTemplate").GetBoolean());

        // And what the reading is for: a page in *their own* department, made from somebody else's
        // template. That is exactly what `CreateFromTemplateAsync` always asked for and could never
        // be given.
        using var created = await SendAsync(
            events,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/from-template/{templateId}",
            new { ownerDepartment = nameof(Department.ED), slug = Slug("from-wd") },
            token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var page = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(nameof(Department.ED), page.GetProperty("ownerDepartment").GetString());
        Assert.False(page.GetProperty("isTemplate").GetBoolean());
    }

    [Fact]
    public async Task TemplatesAreWritableOnlyByTheirDepartment()
    {
        // The other half, and the half that must not have moved: reading a template shared for
        // reading is not a licence to change it.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);

        var templateId = await TemplateAsync("about", token);

        using var events = _factory.CreateApiClient();
        await _factory.SignInAsync(events, EventsCoordinatorVid, token);

        var template = await events.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/{templateId}",
            token);

        using var refused = await SendAsync(
            events,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{templateId}",
            TemplatePayload(template),
            token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // Deleting it is refused for the same reason, and by the same check.
        using var undeletable = await SendAsync(
            events,
            HttpMethod.Delete,
            $"{ContentEndpoints.Pattern}/{templateId}",
            payload: null,
            token);

        Assert.Equal(HttpStatusCode.Forbidden, undeletable.StatusCode);

        // The web team's own coordinator holds ManageTemplates on WD, so the very same call is
        // accepted: what was refused was the department, not the payload.
        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        var reloaded = await web.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/{templateId}",
            token);

        using var allowed = await SendAsync(
            web,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{templateId}",
            TemplatePayload(reloaded),
            token);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // ---- news and documents on the public site --------------------------------------------------

    [Fact]
    public async Task PublicNewsShowsOnlyPublishedAndVisible()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, EventsCoordinatorVid, token);

        var draftSlug = Slug("draft-news");
        await CreateAsync(staff, ContentKind.News, Department.ED, draftSlug, Visibility.Public, token);

        var publicSlug = Slug("open-news");
        var publicId = await CreateAsync(staff, ContentKind.News, Department.ED, publicSlug, Visibility.Public, token);

        var staffOnlySlug = Slug("staff-news");
        var staffOnlyId = await CreateAsync(staff, ContentKind.News, Department.ED, staffOnlySlug, Visibility.Staff, token);

        await PublishAsync(staff, publicId, token);
        await PublishAsync(staff, staffOnlyId, token);

        using var anonymous = _factory.CreateApiClient();

        // Written but never published: nobody's business but the staff's.
        using var draft = await anonymous.GetAsync(PublicUri(ContentKind.News, draftSlug), token);
        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);

        // Published, and meant for everybody.
        using var open = await anonymous.GetAsync(PublicUri(ContentKind.News, publicSlug), token);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        // Published, and meant for the staff. The query filter is what says so, not this endpoint.
        using var restricted = await anonymous.GetAsync(PublicUri(ContentKind.News, staffOnlySlug), token);
        Assert.Equal(HttpStatusCode.NotFound, restricted.StatusCode);

        // And the same three rows through the block a `/news` page is drawn with: one item, the
        // published public one, because the provider reads through the very same filter.
        var listed = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.NewsList, new JsonObject { ["department"] = nameof(Department.ED) }),
            token);

        var slugs = Urls(listed);
        Assert.Contains($"/news/{publicSlug}", slugs);
        Assert.DoesNotContain($"/news/{draftSlug}", slugs);
        Assert.DoesNotContain($"/news/{staffOnlySlug}", slugs);
    }

    [Fact]
    public async Task PinnedNewsComeFirst()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, EventsCoordinatorVid, token);

        // The pinned one is published *first*, so "newest first" alone would put it last: the only
        // thing that can bring it to the top is the pin.
        var pinned = Slug("pinned-news");
        var pinnedId = await CreateAsync(
            staff, ContentKind.News, Department.ED, pinned, Visibility.Public, token, pinned: true);
        await PublishAsync(staff, pinnedId, token);

        var newer = Slug("newer-news");
        var newerId = await CreateAsync(staff, ContentKind.News, Department.ED, newer, Visibility.Public, token);
        await PublishAsync(staff, newerId, token);

        using var anonymous = _factory.CreateApiClient();

        var withPin = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.NewsList, new JsonObject { ["department"] = nameof(Department.ED) }),
            token);

        Assert.Equal($"/news/{pinned}", Urls(withPin)[0]);

        // And a block that says not to pin reads by date alone, which is the newer one on top.
        var withoutPin = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(
                CoreBlocks.NewsList,
                new JsonObject { ["department"] = nameof(Department.ED), ["pinnedFirst"] = false }),
            token);

        Assert.Equal($"/news/{newer}", Urls(withoutPin)[0]);
    }

    [Fact]
    public async Task DocumentWithFileOffersDownload()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        var fileId = await SeedMediaAsync(Department.ED, token);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, EventsCoordinatorVid, token);

        var withFile = Slug("with-file");
        var withFileId = await CreateAsync(
            staff, ContentKind.Document, Department.ED, withFile, Visibility.Public, token, fileMediaId: fileId);
        await PublishAsync(staff, withFileId, token);

        var withoutFile = Slug("no-file");
        var withoutFileId = await CreateAsync(
            staff, ContentKind.Document, Department.ED, withoutFile, Visibility.Public, token);
        await PublishAsync(staff, withoutFileId, token);

        using var anonymous = _factory.CreateApiClient();

        // The identifier reaches the visitor, which is what turns the page into a card with a
        // download rather than something to read (design M1 section 3.3).
        var card = await anonymous.GetFromJsonAsync<JsonElement>(
            PublicUri(ContentKind.Document, withFile),
            token);

        Assert.Equal(fileId, card.GetProperty("fileMediaId").GetInt64());

        var page = await anonymous.GetFromJsonAsync<JsonElement>(
            PublicUri(ContentKind.Document, withoutFile),
            token);

        Assert.Equal(JsonValueKind.Null, page.GetProperty("fileMediaId").ValueKind);

        // And the file itself is downloadable at the address the card points at.
        using var download = await anonymous.GetAsync(new Uri($"/media/{fileId}/seed.png", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
    }

    // ---- the vocabulary -------------------------------------------------------------------------

    [Fact]
    public async Task CategoriesAreScopedToDepartment()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);

        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        var webKey = Slug("web-shelf");
        var webCategoryId = await CreateCategoryAsync(web, Department.WD, ContentKind.News, webKey, token);

        using var events = _factory.CreateApiClient();
        await _factory.SignInAsync(events, EventsCoordinatorVid, token);

        var eventsKey = Slug("events-shelf");
        await CreateCategoryAsync(events, Department.ED, ContentKind.News, eventsKey, token);

        // A shelf is not shared for reading the way a template is: it is an ordinary departmental
        // resource, and the list is narrowed to the departments of whoever asks.
        var listed = await events.GetFromJsonAsync<JsonElement>(CategoriesEndpoints.Pattern, token);
        var keys = listed.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("key").GetString())
            .ToArray();

        Assert.Contains(eventsKey, keys);
        Assert.DoesNotContain(webKey, keys);

        // And writing one of another department is refused by the single handler, as any other row.
        var target = await web.GetFromJsonAsync<JsonElement>(
            $"{CategoriesEndpoints.Pattern}/{webCategoryId}",
            token);

        using var refused = await SendAsync(
            events,
            HttpMethod.Put,
            $"{CategoriesEndpoints.Pattern}/{webCategoryId}",
            CategoryPayload(Department.WD, ContentKind.News, webKey, target.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task CategoryDeletionLeavesTheContentKey()
    {
        // ⚠️ The rule of design M1 section 3.4, and the reason there is no foreign key: a vocabulary
        // may change under rows that are already published, and when it does the row keeps the key
        // it was filed under and the list shows it as it is.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, EventsCoordinatorVid, token);

        var shelf = Slug("shelf");
        var categoryId = await CreateCategoryAsync(staff, Department.ED, ContentKind.News, shelf, token);

        var slug = Slug("filed-news");
        var newsId = await CreateAsync(
            staff, ContentKind.News, Department.ED, slug, Visibility.Public, token, category: shelf);
        await PublishAsync(staff, newsId, token);

        using var anonymous = _factory.CreateApiClient();
        var props = new JsonObject { ["department"] = nameof(Department.ED) };

        // While the shelf exists the list can name it, which is what the public filter offers and
        // what the document list groups by.
        var before = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.NewsList, props),
            token);

        Assert.Contains(shelf, Vocabulary(before));

        using var deleted = await SendAsync(
            staff,
            HttpMethod.Delete,
            $"{CategoriesEndpoints.Pattern}/{categoryId}",
            payload: null,
            token);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // The row is untouched: nothing cascaded, nothing was rewritten.
        var kept = await staff.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{newsId}", token);
        Assert.Equal(shelf, kept.GetProperty("category").GetString());

        // And the list still carries the row, now with a key the vocabulary no longer explains —
        // which is what the client draws as the key itself.
        var after = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.NewsList, props),
            token);

        var filed = after.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("url").GetString() == $"/news/{slug}");

        Assert.Equal(shelf, filed.GetProperty("category").GetString());
        Assert.DoesNotContain(shelf, Vocabulary(after));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static Uri PublicUri(ContentKind kind, string slug) =>
        new($"{ContentEndpoints.Pattern}/public/{kind}/{slug}", UriKind.Relative);

    /// <summary>A block asked the way the browser asks: properties base64url in the query string.</summary>
    private static Uri BlockDataUri(string type, JsonNode props)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Uri($"/api/blocks/data/{type}?props={encoded}", UriKind.Relative);
    }

    private static string[] Urls(JsonElement answer) =>
        [.. answer.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("url").GetString()!)];

    private static string[] Vocabulary(JsonElement answer) =>
        [.. answer.GetProperty("categories").EnumerateArray().Select(item => item.GetProperty("key").GetString()!)];

    /// <summary>Short, unique and a legal slug, so two runs against one database never collide.</summary>
    private static string Slug(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 40)];

    /// <summary>A heading and nothing else: this suite is about the row, not about what is in it.</summary>
    private static JsonNode Body() => JsonNode.Parse("""
    {
      "schemaVersion": 1,
      "sections": [
        {
          "id": "s_body",
          "layout": "stacked",
          "blocks": [
            { "id": "b_heading", "type": "heading", "version": 1,
              "props": { "level": 1, "text": { "it": "Titolo", "en": "Heading" } } }
          ]
        }
      ]
    }
    """)!;

    private static object Payload(
        ContentKind kind,
        Department department,
        string slug,
        Visibility visibility,
        string? category = null,
        long? fileMediaId = null,
        bool pinned = false,
        string? rowVersion = null) => new
        {
            kind = kind.ToString(),
            slug,
            ownerDepartment = department.ToString(),
            visibility = visibility.ToString(),
            isTemplate = false,
            title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Prova", ["en"] = "Test" },
            summary = (Dictionary<string, string>?)null,
            seo = (Dictionary<string, object>?)null,
            body = Body(),
            schemaVersion = 1,
            category,
            coverMediaId = (long?)null,
            pinned,
            sort = 0,
            fileMediaId,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };

    /// <summary>The template read back, sent again unchanged. Only the permission is under test.</summary>
    private static object TemplatePayload(JsonElement template) => new
    {
        kind = template.GetProperty("kind").GetString(),
        slug = template.GetProperty("slug").GetString(),
        ownerDepartment = template.GetProperty("ownerDepartment").GetString(),
        visibility = template.GetProperty("visibility").GetString(),
        isTemplate = true,
        title = template.GetProperty("title").Deserialize<Dictionary<string, string>>(),
        summary = (Dictionary<string, string>?)null,
        seo = (Dictionary<string, object>?)null,
        body = template.GetProperty("body").Deserialize<JsonNode>(),
        schemaVersion = template.GetProperty("schemaVersion").GetInt32(),
        category = (string?)null,
        coverMediaId = (long?)null,
        pinned = false,
        sort = 0,
        fileMediaId = (long?)null,
        rowVersion = template.GetProperty("rowVersion").GetString(),
    };

    private static object CategoryPayload(
        Department department,
        ContentKind kind,
        string key,
        string? rowVersion = null) => new
        {
            kind = kind.ToString(),
            ownerDepartment = department.ToString(),
            key,
            label = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["it"] = "Scaffale",
                ["en"] = "Shelf",
            },
            sort = 0,
            isActive = true,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };

    private static async Task<long> CreateAsync(
        HttpClient client,
        ContentKind kind,
        Department department,
        string slug,
        Visibility visibility,
        CancellationToken cancellationToken,
        string? category = null,
        long? fileMediaId = null,
        bool pinned = false)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(kind, department, slug, visibility, category, fileMediaId, pinned),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task<long> CreateCategoryAsync(
        HttpClient client,
        Department department,
        ContentKind kind,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            CategoriesEndpoints.Pattern,
            CategoryPayload(department, kind, key),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{id}/publish",
            new { changelog = (string?)null },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<long> TemplateAsync(string slug, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var template = await database.Contents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(row => row.IsTemplate && row.Slug == slug, cancellationToken);

        return template.Id;
    }

    /// <summary>
    /// A file in the library, written the way the upload writes it. The bytes are a real eight by
    /// eight PNG so that serving it answers with a picture rather than an empty body.
    /// </summary>
    private async Task<long> SeedMediaAsync(Department department, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<MediaStorage>();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC");

        // What the file is, read from its own bytes and never from what accompanied them — the same
        // question the upload asks (HANDOFF section 3).
        var format = MediaFormats.Detect(bytes)!;

        using var content = new MemoryStream(bytes);
        var stored = await storage.SaveAsync(content, format, clock.UtcNow, cancellationToken);

        var media = new MediaAsset
        {
            OwnerDepartment = department,
            Visibility = Visibility.Public,
            FileName = "seed.png",
            StoredName = stored,
            ContentType = format.ContentType,
            ByteSize = bytes.Length,
            Alt = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Prova"),
                new KeyValuePair<string, string>("en", "Test"),
            ]),
        };

        database.Media.Add(media);
        await database.SaveChangesAsync(cancellationToken);

        return media.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedUserAsync(
        int vid,
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
        user.IsStaff = position is not null;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
                row => row.Vid == vid && row.Position == position,
                cancellationToken))
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
