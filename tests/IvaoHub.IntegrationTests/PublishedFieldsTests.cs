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
/// The fields of a row around its body, and where it stands, once it is online (note
/// 2026-10-21-i-campi-fuori-dalla-versione, decided by Carmine on #339): summary, SEO, collections, cover,
/// file and footer are read from the published version, and an edit of the row changes none of them until
/// somebody publishes; visibility, slug and parent of a row that is online are changed only by whoever may
/// publish it — and approve it, for a page — and new readers go through the checks of publication.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PublishedFieldsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own: the integration tests share one database.
    private const int CoordinatorVid = 770201;
    private const int AdvisorVid = 770202;
    private const int DirectorVid = 770203;

    private HubWebApplicationFactory _factory = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _shelf = await TestShelf.SeedAsync(_factory, "test-shelf-fields", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheTextAroundANewsItemsBodyIsReadFromThePublishedVersion()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, _, _) = await PeopleAsync(token);
        var (firstCover, secondCover) = (await MediaAsync(Visibility.Public, token), await MediaAsync(Visibility.Public, token));
        var (first, second) = (Key(), Key());

        var slug = Slug();
        var id = await CreateAsync(coordinator, Fields(ContentKind.News, slug, "Primo", first, firstCover, fileMediaId: null, showFooter: true), token);
        await PublishAsync(coordinator, id, token);
        await EditAsync(coordinator, id, Fields(ContentKind.News, slug, "Secondo", second, secondCover, fileMediaId: null, showFooter: false), token);

        using var anonymous = _factory.CreateApiClient();

        // Summary, SEO, collections, cover and footer: each one as it was published.
        var read = await PublicAsync(anonymous, ContentKind.News, slug, token);
        Assert.Equal("Primo", read.GetProperty("summary").GetProperty("it").GetString());
        Assert.Equal("Primo", read.GetProperty("seo").GetProperty("it").GetProperty("title").GetString());
        Assert.Equal([first], read.GetProperty("collections").EnumerateArray().Select(key => key.GetString()));
        Assert.Equal(firstCover, read.GetProperty("coverMediaId").GetInt64());
        Assert.True(read.GetProperty("showFooter").GetBoolean());

        // The live list: filed where it was published, with the published summary and cover.
        var listed = await NewsListAsync(anonymous, first, token);
        var item = Assert.Single(listed, row => row.GetProperty("id").GetInt64() == id);
        Assert.Equal("Primo", item.GetProperty("summary").GetProperty("it").GetString());
        Assert.Equal(firstCover, item.GetProperty("coverMediaId").GetInt64());
        Assert.DoesNotContain(await NewsListAsync(anonymous, second, token), row => row.GetProperty("id").GetInt64() == id);

        // Published again, the row's values are what the public reads.
        await PublishAsync(coordinator, id, token);

        read = await PublicAsync(anonymous, ContentKind.News, slug, token);
        Assert.Equal("Secondo", read.GetProperty("summary").GetProperty("it").GetString());
        Assert.Equal("Secondo", read.GetProperty("seo").GetProperty("it").GetProperty("title").GetString());
        Assert.Equal([second], read.GetProperty("collections").EnumerateArray().Select(key => key.GetString()));
        Assert.Equal(secondCover, read.GetProperty("coverMediaId").GetInt64());
        Assert.False(read.GetProperty("showFooter").GetBoolean());
        Assert.Contains(await NewsListAsync(anonymous, second, token), row => row.GetProperty("id").GetInt64() == id);
    }

    [Fact]
    public async Task TheFileOfADocumentIsReadFromThePublishedVersion()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, _, _) = await PeopleAsync(token);
        var (firstFile, secondFile) = (await MediaAsync(Visibility.Public, token), await MediaAsync(Visibility.Public, token));
        var collection = Key();

        var slug = Slug();
        var id = await CreateAsync(coordinator, Fields(ContentKind.Document, slug, "Regola", collection, coverMediaId: null, firstFile, showFooter: true), token);
        await PublishAsync(coordinator, id, token);
        await EditAsync(coordinator, id, Fields(ContentKind.Document, slug, "Regola", collection, coverMediaId: null, secondFile, showFooter: true), token);

        using var anonymous = _factory.CreateApiClient();
        Assert.Equal(firstFile, (await PublicAsync(anonymous, ContentKind.Document, slug, token)).GetProperty("fileMediaId").GetInt64());

        var list = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.DocumentList, new JsonObject { ["department"] = nameof(Department.ED), ["category"] = collection, ["limit"] = 10 }),
            token);
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(firstFile, item.GetProperty("fileMediaId").GetInt64());

        await PublishAsync(coordinator, id, token);
        Assert.Equal(secondFile, (await PublicAsync(anonymous, ContentKind.Document, slug, token)).GetProperty("fileMediaId").GetInt64());
    }

    [Fact]
    public async Task WhereARowOnlineStandsIsChangedByWhoeverMayPublishIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, advisor, _) = await PeopleAsync(token);

        var slug = Slug();
        var fields = Fields(ContentKind.News, slug, "Testo", Key(), coverMediaId: null, fileMediaId: null, showFooter: true);
        var id = await CreateAsync(coordinator, fields, token);

        // A draft is placed by whoever writes it: publication is the step that asks.
        await EditAsync(advisor, id, fields with { Visibility = Visibility.Members }, token);
        await EditAsync(advisor, id, fields, token);

        await PublishAsync(coordinator, id, token);

        // Online: the text is the advisor's to write, who reads it and where it is are not.
        await EditAsync(advisor, id, fields with { Summary = "Corretto" }, token);

        using (var visibility = await TryEditAsync(advisor, id, fields with { Summary = "Corretto", Visibility = Visibility.Members }, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, visibility.StatusCode);
            Assert.Equal("errors.content.position.needsPublish", await FirstErrorAsync(visibility, "visibility", token));
        }

        using (var address = await TryEditAsync(advisor, id, fields with { Summary = "Corretto", Slug = Slug() }, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, address.StatusCode);
            Assert.Equal("errors.content.position.needsPublish", await FirstErrorAsync(address, "slug", token));
        }

        // Whoever may publish it moves it.
        await EditAsync(coordinator, id, fields with { Summary = "Corretto", Visibility = Visibility.Members }, token);
    }

    [Fact]
    public async Task APageOnlineIsMovedByWhoeverMayApproveIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, _, director) = await PeopleAsync(token);

        var slug = Slug();
        var fields = Fields(ContentKind.Page, slug, "Pagina", collection: null, coverMediaId: null, fileMediaId: null, showFooter: true) with
        {
            ParentId = _shelf,
        };
        var id = await CreateAsync(coordinator, fields, token);
        await PublishAsync(director, id, token);

        // The coordinator may publish the department's news, not put a page online: that is approval.
        using (var moved = await TryEditAsync(coordinator, id, fields with { Slug = Slug() }, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, moved.StatusCode);
            Assert.Equal("errors.content.position.needsApproval", await FirstErrorAsync(moved, "slug", token));
        }

        await EditAsync(director, id, fields with { Slug = Slug() }, token);
    }

    [Fact]
    public async Task NewReadersGoThroughTheChecksOfPublication()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, _, _) = await PeopleAsync(token);

        // A cover only the staff may be served, on a news item for the staff: published.
        var staffCover = await MediaAsync(Visibility.Staff, token);
        var pictured = Fields(ContentKind.News, Slug(), "Con immagine", Key(), staffCover, fileMediaId: null, showFooter: true) with
        {
            Visibility = Visibility.Staff,
        };
        var withPicture = await CreateAsync(coordinator, pictured, token);
        await PublishAsync(coordinator, withPicture, token);

        // Given to everybody, the picture of what is online would be a hole: refused.
        using (var wider = await TryEditAsync(coordinator, withPicture, pictured with { Visibility = Visibility.Public }, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wider.StatusCode);
            Assert.Equal("errors.content.position.picturesHidden", await FirstErrorAsync(wider, "visibility", token));
        }

        // A news item for the members that captured a list of news, published.
        var captured = Fields(ContentKind.News, Slug(), "Con una cattura", Key(), coverMediaId: null, fileMediaId: null, showFooter: true) with
        {
            Visibility = Visibility.Members,
            Body = CapturingBody(),
        };
        var withCapture = await CreateAsync(coordinator, captured, token);
        await PublishAsync(coordinator, withCapture, token);

        // More readers than it was captured for: refused until it is published again.
        using (var wider = await TryEditAsync(coordinator, withCapture, captured with { Visibility = Visibility.Public }, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wider.StatusCode);
            Assert.Equal("errors.content.position.capturesNarrower", await FirstErrorAsync(wider, "visibility", token));
        }

        // Fewer: what was captured for the members may be read by the staff.
        await EditAsync(coordinator, withCapture, captured with { Visibility = Visibility.Staff }, token);

        // A news item for the staff that carries what waits for them, published (note 2026-10-28-il-pubblico-di-un-blocco).
        var queued = Fields(ContentKind.News, Slug(), "Con una coda", Key(), coverMediaId: null, fileMediaId: null, showFooter: true) with
        {
            Visibility = Visibility.Staff,
            Body = QueueBody(),
        };
        var withQueue = await CreateAsync(coordinator, queued, token);
        await PublishAsync(coordinator, withQueue, token);

        // Taken out of the draft and given to everybody: what is online still carries it, so refused until published again.
        using (var wider = await TryEditAsync(coordinator, withQueue, queued with { Visibility = Visibility.Public, Body = Body() }, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wider.StatusCode);
            Assert.Equal("errors.content.position.blockAudience", await FirstErrorAsync(wider, "visibility", token));
        }
    }

    /// <summary>What the tests write of a row, as the form sends it.</summary>
    private sealed record RowFields(
        ContentKind Kind,
        string Slug,
        Visibility Visibility,
        string? Summary,
        string? Collection,
        long? CoverMediaId,
        long? FileMediaId,
        bool ShowFooter,
        JsonNode Body,
        long? ParentId = null);

    private static RowFields Fields(
        ContentKind kind,
        string slug,
        string text,
        string? collection,
        long? coverMediaId,
        long? fileMediaId,
        bool showFooter) =>
        new(kind, slug, Visibility.Public, text, collection, coverMediaId, fileMediaId, showFooter, Body());

    private static object Payload(RowFields fields, string? rowVersion) => new
    {
        kind = fields.Kind.ToString(),
        slug = fields.Slug,
        ownerDepartment = nameof(Department.ED),
        visibility = fields.Visibility.ToString(),
        isTemplate = false,
        title = new Dictionary<string, string> { ["it"] = "Campi pubblicati", ["en"] = "Published fields" },
        summary = fields.Summary is null ? null : new Dictionary<string, string> { ["it"] = fields.Summary, ["en"] = fields.Summary },
        seo = fields.Summary is null
            ? null
            : new Dictionary<string, object>
            {
                ["it"] = new { title = fields.Summary },
                ["en"] = new { title = fields.Summary },
            },
        body = fields.Body.DeepClone(),
        schemaVersion = 1,
        collections = fields.Collection is null ? Array.Empty<string>() : [fields.Collection],
        coverMediaId = fields.CoverMediaId,
        pinned = false,
        sort = 0,
        fileMediaId = fields.FileMediaId,
        showFooter = fields.ShowFooter,
        parentId = fields.ParentId,
        rowVersion = rowVersion ?? "0001-01-01T00:00:00",
    };

    private static JsonNode Body() => JsonNode.Parse("""
        { "schemaVersion": 1, "sections": [
          { "id": "s_main", "layout": "stacked", "blocks": [
            { "id": "b_heading", "type": "heading", "version": 1,
              "props": { "level": 2, "text": { "it": "Titolo", "en": "Heading" } } } ] } ] }
        """)!;

    /// <summary>A body with a list of the department's news captured at publication.</summary>
    private static JsonNode CapturingBody() => JsonNode.Parse("""
        { "schemaVersion": 1, "sections": [
          { "id": "s_main", "layout": "stacked", "blocks": [
            { "id": "b_news", "type": "newsList", "version": 1, "renderMode": "frozen",
              "props": { "department": "ED", "limit": 3 } } ] } ] }
        """)!;

    /// <summary>A body with what waits for the staff, a block for the staff only.</summary>
    private static JsonNode QueueBody() => JsonNode.Parse("""
        { "schemaVersion": 1, "sections": [
          { "id": "s_main", "layout": "stacked", "blocks": [
            { "id": "b_work", "type": "myWork", "version": 1, "props": {} } ] } ] }
        """)!;

    private static string Slug() => $"f{Guid.NewGuid():N}"[..14];

    private static string Key() => $"k{Guid.NewGuid():N}"[..12];

    private async Task<(HttpClient Coordinator, HttpClient Advisor, HttpClient Director)> PeopleAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(CoordinatorVid, "IT-EC", cancellationToken);
        await SeedUserAsync(AdvisorVid, "IT-EA1", cancellationToken);
        await SeedUserAsync(DirectorVid, "IT-DIR", cancellationToken);

        var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, CoordinatorVid, cancellationToken);

        var advisor = _factory.CreateApiClient();
        await _factory.SignInAsync(advisor, AdvisorVid, cancellationToken);

        var director = _factory.CreateApiClient();
        await _factory.SignInAsync(director, DirectorVid, cancellationToken);

        return (coordinator, advisor, director);
    }

    private static async Task<long> CreateAsync(HttpClient client, RowFields fields, CancellationToken cancellationToken)
    {
        using var created = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload(fields, null), cancellationToken);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(cancellationToken));
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var published = await SendAsync(client, HttpMethod.Post, $"{ContentEndpoints.Pattern}/{id}/publish", new { changelog = (string?)null }, cancellationToken);
        Assert.True(published.StatusCode == HttpStatusCode.OK, await published.Content.ReadAsStringAsync(cancellationToken));
    }

    private static async Task EditAsync(HttpClient client, long id, RowFields fields, CancellationToken cancellationToken)
    {
        using var edited = await TryEditAsync(client, id, fields, cancellationToken);
        Assert.True(edited.StatusCode == HttpStatusCode.OK, await edited.Content.ReadAsStringAsync(cancellationToken));
    }

    private static async Task<HttpResponseMessage> TryEditAsync(HttpClient client, long id, RowFields fields, CancellationToken cancellationToken)
    {
        var current = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", cancellationToken);
        return await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(fields, current.GetProperty("rowVersion").GetString()),
            cancellationToken);
    }

    private static Task<JsonElement> PublicAsync(HttpClient client, ContentKind kind, string slug, CancellationToken cancellationToken) =>
        client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/public/{kind}/{slug}", cancellationToken);

    private static async Task<IReadOnlyList<JsonElement>> NewsListAsync(HttpClient client, string collection, CancellationToken cancellationToken)
    {
        var list = await client.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.NewsList, new JsonObject { ["department"] = nameof(Department.ED), ["category"] = collection, ["limit"] = 10 }),
            cancellationToken);
        return [.. list.GetProperty("items").EnumerateArray()];
    }

    /// <summary>A live block, asked the way the browser asks: the properties base64url encoded.</summary>
    private static Uri BlockDataUri(string type, JsonNode props)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Uri($"/api/blocks/data/{type}?props={encoded}", UriKind.Relative);
    }

    /// <summary>
    /// A row of the library, written straight in: what is under test is which identifier the public is
    /// given, and publication only asks whether the file is there and who may be served it.
    /// </summary>
    private async Task<long> MediaAsync(Visibility visibility, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var media = new MediaAsset
        {
            OwnerDepartment = Department.ED,
            Visibility = visibility,
            FileName = "seed.png",
            StoredName = $"{Guid.NewGuid():N}.png",
            ContentType = "image/png",
            ByteSize = 1,
            Alt = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Prova"),
                new KeyValuePair<string, string>("en", "Test"),
            ]),
            CreatedAt = clock.UtcNow,
        };

        database.Media.Add(media);
        await database.SaveChangesAsync(cancellationToken);
        return media.Id;
    }

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.True(
            problem.GetProperty("errors").TryGetProperty(field, out var errors),
            problem.GetProperty("errors").ToString());
        return errors.EnumerateArray().First().GetString();
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
        user.LastName = "User";
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
