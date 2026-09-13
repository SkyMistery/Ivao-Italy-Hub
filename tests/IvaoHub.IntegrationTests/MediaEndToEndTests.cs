using System.Buffers.Binary;
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
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The media library over the wire (design M1 section 2). A real MariaDB, the real cookie, the real
/// policies, the real save changes interceptor — and a real directory on disk, because half of what
/// this resource does is not in the database at all.
/// <para>What the five tests are about is what a media library gets wrong when nobody looks: a file
/// that is not what it claims, two files overwriting each other, a staff-only picture reachable by
/// its address, and a page breaking because somebody tidied up.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MediaEndToEndTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int WebCoordinatorVid = 620001;
    private const int EventsCoordinatorVid = 620002;

    private HubWebApplicationFactory _factory = null!;
    private string _mediaRoot = null!;

    public ValueTask InitializeAsync()
    {
        // A directory of this run's own: the files are the point, and a test that shared them with
        // the developer's own installation would be a test that passes on a dirty disk.
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"ivaohub-media-{Guid.NewGuid():N}");
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, mediaDirectory: _mediaRoot);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();

        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MediaUploadRejectsTypeAndSize()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var client = await SignedInAsync(WebCoordinatorVid, token);

        // A file whose bytes are a web page. The name and the declared content type both say PNG,
        // which is exactly the point: the browser's word is not what decides.
        using var disguised = await UploadAsync(
            client,
            Department.WD,
            Encoding.ASCII.GetBytes("<!doctype html><script>alert(1)</script>"),
            "logo.png",
            "image/png",
            token);

        Assert.Equal(HttpStatusCode.BadRequest, disguised.StatusCode);
        Assert.Equal("errors.media.typeNotAllowed", await FirstErrorAsync(disguised, "file", token));

        // Bigger than the configured limit, which the test host sets to something small on purpose:
        // a limit only ever proven by uploading eight megabytes is a limit nobody proves.
        using var oversized = await UploadAsync(
            client,
            Department.WD,
            Png(4000, 3000, padding: HubWebApplicationFactory.TestMediaMaxBytes),
            "huge.png",
            "image/png",
            token);

        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal("errors.media.tooLarge", await FirstErrorAsync(oversized, "file", token));

        // And nothing of either attempt is on disk: the row is written after the bytes, so a
        // refusal leaves neither.
        Assert.Empty(FilesOnDisk());
    }

    [Fact]
    public async Task MediaStoredNameIsOpaque()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", token);

        using var web = await SignedInAsync(WebCoordinatorVid, token);
        using var events = await SignedInAsync(EventsCoordinatorVid, token);

        var first = await UploadedAsync(web, Department.WD, Png(10, 10), "logo.png", token);
        var second = await UploadedAsync(events, Department.ED, Png(20, 20), "logo.png", token);

        var stored = await StoredNamesAsync([first, second], token);

        // Two departments uploading "logo.png" get two files. If the name on disk were derived from
        // the uploaded one, the second would have overwritten the first and the web team's logo
        // would silently have become the events one.
        Assert.Equal(2, stored.Distinct(StringComparer.Ordinal).Count());
        Assert.All(stored, name => Assert.DoesNotContain("logo", name, StringComparison.OrdinalIgnoreCase));

        // And nothing anybody typed became a path: year, month, thirty two hex characters, suffix.
        Assert.All(stored, name => Assert.Matches(@"^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\.png$", name));

        Assert.Equal(2, FilesOnDisk().Length);
    }

    [Fact]
    public async Task TheSameBytesUploadedTwiceAreOneFileInADepartmentAndTwoAcrossDepartments()
    {
        // Carmine, 11 September 2026: "what happens if I upload two identical images?" Two rows
        // and two files, was the answer; decided on the 12th (decision note): the same bytes in the
        // same library answer the row that exists, 200 and not 201, and the second file goes.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", token);

        using var web = await SignedInAsync(WebCoordinatorVid, token);
        using var events = await SignedInAsync(EventsCoordinatorVid, token);

        var bytes = Png(30, 30, padding: 7);

        using var first = await UploadAsync(web, Department.WD, bytes, "logo.png", "image/png", token);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        // The name the browser gave it is decoration: the bytes are what is compared.
        using var again = await UploadAsync(web, Department.WD, bytes, "logo-copy.png", "image/png", token);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(firstId, (await again.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64());
        Assert.Single(FilesOnDisk());

        // Another department is another library: its own row, its own file, its own alt text.
        using var elsewhere = await UploadAsync(events, Department.ED, bytes, "logo.png", "image/png", token);
        Assert.Equal(HttpStatusCode.Created, elsewhere.StatusCode);
        Assert.Equal(2, FilesOnDisk().Length);

        // A different picture is a different file, however it is called.
        using var other = await UploadAsync(web, Department.WD, Png(31, 30, padding: 7), "logo.png", "image/png", token);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        Assert.Equal(3, FilesOnDisk().Length);
    }

    [Fact]
    public async Task APublicFileIsReadByEveryDepartmentAndChangedOnlyByItsOwn()
    {
        // Note 2026-09-13-contenuti-centralizzati, 3.4: the logo the web team uploaded is picked on a
        // page of Events without being uploaded twice, and Events cannot change it or throw it away.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", token);

        using var web = await SignedInAsync(WebCoordinatorVid, token);
        using var events = await SignedInAsync(EventsCoordinatorVid, token);

        // Sizes of this test's own, so the deduplication of another test never answers for these.
        var shared = await UploadedAsync(web, Department.WD, Png(47, 23, padding: 11), "shared.png", token);
        var kept = await UploadedAsync(web, Department.WD, Png(23, 47, padding: 11), "kept.png", token);
        await MakePublicAsync(web, shared, token);

        var listed = await events.GetFromJsonAsync<JsonElement>(
            $"{MediaEndpoints.Pattern}?filter[ownerDepartment]={nameof(Department.WD)}&pageSize=100",
            token);
        var ids = listed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64()).ToList();

        Assert.Contains(shared, ids);
        Assert.DoesNotContain(kept, ids);

        using var read = await events.GetAsync(new Uri($"{MediaEndpoints.Pattern}/{kept}", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        // Read, and only read.
        await Assert.ThrowsAsync<HttpRequestException>(() => MakePublicAsync(events, shared, token));

        using var removal = new HttpRequestMessage(HttpMethod.Delete, new Uri($"{MediaEndpoints.Pattern}/{shared}", UriKind.Relative));
        removal.Headers.Add("X-Requested-With", "hub");
        using var removed = await events.SendAsync(removal, token);
        Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
    }

    [Fact]
    public async Task ANameThatIsAPathDoesNotBecomeOne()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var client = await SignedInAsync(WebCoordinatorVid, token);

        var id = await UploadedAsync(client, Department.WD, Png(10, 10), "../../../etc/passwd.png", token);
        var stored = (await StoredNamesAsync([id], token))[0];

        Assert.DoesNotContain("..", stored, StringComparison.Ordinal);
        Assert.All(FilesOnDisk(), file => Assert.StartsWith(_mediaRoot, file, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MediaServedBehindVisibilityFilter()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var web = await SignedInAsync(WebCoordinatorVid, token);
        var id = await UploadedAsync(web, Department.WD, Png(10, 10), "internal.png", token);

        var url = await UrlAsync(web, id, token);

        // Uploaded as staff, which is what a file arrives as until somebody says otherwise.
        using var anonymous = _factory.CreateApiClient();
        using var refused = await anonymous.GetAsync(new Uri(url, UriKind.Relative), token);

        // 404 and not 403: a 403 would confirm that a file exists at that identifier, which is
        // exactly what the address of a staff-only file must not do.
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var forStaff = await web.GetAsync(new Uri(url, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, forStaff.StatusCode);
        Assert.Equal("image/png", forStaff.Content.Headers.ContentType?.MediaType);

        // A file only the staff may see must not be kept by a shared cache in front of the hub.
        Assert.Contains("private", CacheControlOf(forStaff), StringComparison.Ordinal);

        await MakePublicAsync(web, id, token);

        using var forAnybody = await anonymous.GetAsync(new Uri(url, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, forAnybody.StatusCode);
        Assert.Contains("immutable", CacheControlOf(forAnybody), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MediaDeleteKeepsFileWhileAVersionUsesIt()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var client = await SignedInAsync(WebCoordinatorVid, token);

        var shown = await UploadedAsync(client, Department.WD, Png(10, 10), "hero.png", token);
        var unused = await UploadedAsync(client, Department.WD, Png(11, 11), "spare.png", token);

        await PublishPageShowingAsync(shown, token);

        var shownPath = await PathOnDiskAsync(shown, token);
        var unusedPath = await PathOnDiskAsync(unused, token);

        using var deletedShown = await DeleteAsync(client, shown, token);
        Assert.Equal(HttpStatusCode.NoContent, deletedShown.StatusCode);

        using var deletedUnused = await DeleteAsync(client, unused, token);
        Assert.Equal(HttpStatusCode.NoContent, deletedUnused.StatusCode);

        // The one a published page shows keeps its bytes: removing them would break a page that has
        // already been printed and read.
        Assert.True(File.Exists(shownPath));

        // The one nothing shows does not: a library that never lets go of anything is a disk that
        // fills up.
        Assert.False(File.Exists(unusedPath));

        // Both leave the library, and the row of each survives the deletion, because an address a
        // browser has cached names the identifier.
        //
        // Named rather than counted: every integration test writes into the same database, so
        // "the list is empty" would be a promise about the other tests rather than about this one.
        var listed = await client.GetFromJsonAsync<JsonElement>($"{MediaEndpoints.Pattern}?pageSize=100", token);
        var stillListed = listed.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToArray();

        Assert.DoesNotContain(shown, stillListed);
        Assert.DoesNotContain(unused, stillListed);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var rows = await database.Media.IgnoreQueryFilters()
            .Where(media => media.Id == shown || media.Id == unused)
            .ToListAsync(token);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.NotNull(row.DeletedAt));
        Assert.True(rows.Single(row => row.Id == shown).HasFile);
        Assert.False(rows.Single(row => row.Id == unused).HasFile);
    }

    [Fact]
    public async Task PublishRefusesAPageShowingAPictureItsReadersMayNotSee()
    {
        // ⚠️ The defect the demo of M1 found, and the whole round it comes from. A file arrives in
        // the library visible to the staff -- it becomes public because somebody says so, never by
        // arriving -- so an image uploaded and dropped straight into a page was staff-only, the
        // page went out with it, and a visitor got a broken picture. Nothing had refused and
        // nothing had warned: the address of a file a reader may not see answers 404 by design,
        // which is right, and the page had no business being published carrying it.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var client = await SignedInAsync(WebCoordinatorVid, token);

        var picture = await UploadedAsync(client, Department.WD, Png(10, 10), "hero.png", token);
        var slug = $"hidden-{Guid.NewGuid():N}"[..24];
        var id = await CreatePublicPageAsync(client, slug, Body(picture, 0), token);

        using var refused = await PublishAsync(client, id, token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // Read once: a refusal is one body, and the second read of an answer already read is empty.
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        var errors = problem.GetProperty("errors");

        // Named where the editor can act on it, exactly like a translation that is not written:
        // the path of the property, so the screen can say which block to open.
        Assert.Equal(
            "errors.content.mediaNotVisible",
            errors.GetProperty("body.sections[0].blocks[0].props.mediaId")[0].GetString());

        // A picture that is not there at all is the same kind of hole, and the gallery of the same
        // body carries one: identifier 999999 belongs to nobody. The path counts through the list,
        // because "one of the pictures in this gallery" is not something an editor can act on.
        Assert.Equal(
            "errors.content.mediaMissing",
            errors.GetProperty("body.sections[0].blocks[1].props.mediaIds[1]")[0].GetString());

        // The row stayed a draft: a refusal that half published would be worse than the defect.
        using var stillDraft = await client.GetAsync(
            new Uri($"{ContentEndpoints.Pattern}/{id}", UriKind.Relative),
            token);
        var draft = await stillDraft.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(nameof(PublishStatus.Draft), draft.GetProperty("status").GetString());

        // And the same, for the picture a row names in a column rather than in its body: the file
        // a document is. It is the shape the demo actually reported -- "a published document with
        // a picture does not show the picture" -- and it is the same rule, which is why it is the
        // same check and the same message.
        var attachment = await UploadedAsync(client, Department.WD, Png(12, 12), "sheet.png", token);
        var document = await CreatePublicPageAsync(
            client,
            $"doc-{Guid.NewGuid():N}"[..24],
            EmptyBody,
            token,
            ContentKind.Document,
            attachment);

        using var refusedDocument = await PublishAsync(client, document, token);
        Assert.Equal(HttpStatusCode.BadRequest, refusedDocument.StatusCode);

        var documentProblem = await refusedDocument.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(
            "errors.content.mediaNotVisible",
            documentProblem.GetProperty("errors").GetProperty("fileMediaId")[0].GetString());

        // Say so in the library, and a page showing that picture publishes.
        await MakePublicAsync(client, picture, token);
        var repaired = await CreatePublicPageAsync(client, $"ok-{Guid.NewGuid():N}"[..24], BodyShowing(picture), token);

        using var published = await PublishAsync(client, repaired, token);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        // And now the reader really is served the file, which is the thing the demo was looking at.
        using var anonymous = _factory.CreateApiClient();
        using var served = await anonymous.GetAsync(
            new Uri(await UrlAsync(client, picture, token), UriKind.Relative),
            token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
    }

    [Fact]
    public async Task MediaUsageQueryFindsPagesByMediaId()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var client = await SignedInAsync(WebCoordinatorVid, token);

        var inABlock = await UploadedAsync(client, Department.WD, Png(10, 10), "single.png", token);
        var inAGallery = await UploadedAsync(client, Department.WD, Png(11, 11), "gallery.png", token);
        var nowhere = await UploadedAsync(client, Department.WD, Png(12, 12), "orphan.png", token);

        var slug = $"usage-{Guid.NewGuid():N}"[..24];
        var contentId = await SeedPageAsync(slug, Body(inABlock, inAGallery), token);

        // Asked the way the screen asks it: a filter of the content list, resolved by the one helper
        // that knows how to look inside a JSON body. There is no endpoint for it.
        var single = await UsageAsync(client, inABlock, token);
        Assert.Equal([contentId], single);

        // A gallery holds a list of identifiers rather than one, and both shapes are found: the
        // recursive path walks the tree without this side knowing what a block looks like.
        Assert.Equal([contentId], await UsageAsync(client, inAGallery, token));

        Assert.Empty(await UsageAsync(client, nowhere, token));
    }

    // ---- helpers -----------------------------------------------------------------------------

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>The upload, shaped exactly as a browser sends it.</summary>
    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Department department,
        byte[] bytes,
        string fileName,
        string declaredContentType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(MediaEndpoints.Pattern, UriKind.Relative));
        var form = new MultipartFormDataContent();

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(declaredContentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(department.ToString()), "ownerDepartment");

        request.Content = form;
        request.Headers.Add("X-Requested-With", "hub");

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<long> UploadedAsync(
        HttpClient client,
        Department department,
        byte[] bytes,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var response = await UploadAsync(client, department, bytes, fileName, "image/png", cancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return created.GetProperty("id").GetInt64();
    }

    private static async Task<string> UrlAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        var media = await client.GetFromJsonAsync<JsonElement>($"{MediaEndpoints.Pattern}/{id}", cancellationToken);
        return media.GetProperty("url").GetString()!;
    }

    private static async Task MakePublicAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        var media = await client.GetFromJsonAsync<JsonElement>($"{MediaEndpoints.Pattern}/{id}", cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri($"{MediaEndpoints.Pattern}/{id}", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                ownerDepartment = media.GetProperty("ownerDepartment").GetString(),
                visibility = nameof(Visibility.Public),
                alt = new Dictionary<string, string> { ["it"] = "Logo", ["en"] = "Logo" },
                title = (Dictionary<string, string>?)null,
                category = (string?)null,
                rowVersion = media.GetProperty("rowVersion").GetString(),
            }),
        };

        request.Headers.Add("X-Requested-With", "hub");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"{MediaEndpoints.Pattern}/{id}", UriKind.Relative));

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<long[]> UsageAsync(HttpClient client, long mediaId, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}?filter[{ContentEndpoints.UsesMediaFilter}]={mediaId}",
            cancellationToken);

        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];
    }

    /// <summary>The header as it was written, not as a parser chose to give it back.</summary>
    private static string CacheControlOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Cache-Control", out var values)
            ? string.Join(", ", values)
            : string.Empty;

    private static async Task<string> FirstErrorAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return problem.GetProperty("errors").GetProperty(field)[0].GetString()!;
    }

    private async Task<string[]> StoredNamesAsync(long[] ids, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        return [.. await database.Media.IgnoreQueryFilters()
            .Where(media => ids.Contains(media.Id))
            .OrderBy(media => media.Id)
            .Select(media => media.StoredName)
            .ToListAsync(cancellationToken)];
    }

    private async Task<string> PathOnDiskAsync(long id, CancellationToken cancellationToken)
    {
        var stored = (await StoredNamesAsync([id], cancellationToken))[0];
        return Path.Combine(_mediaRoot, stored.Replace('/', Path.DirectorySeparatorChar));
    }

    private string[] FilesOnDisk() =>
        Directory.Exists(_mediaRoot)
            ? Directory.GetFiles(_mediaRoot, "*", SearchOption.AllDirectories)
            : [];

    /// <summary>A public page or document, written over the wire the way the back office writes one.</summary>
    private static async Task<long> CreatePublicPageAsync(
        HttpClient client,
        string slug,
        string body,
        CancellationToken cancellationToken,
        ContentKind kind = ContentKind.Page,
        long? fileMediaId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(ContentEndpoints.Pattern, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                kind = kind.ToString(),
                slug,
                ownerDepartment = nameof(Department.WD),
                visibility = nameof(Visibility.Public),
                isTemplate = false,
                title = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["it"] = "Pagina con un'immagine",
                    ["en"] = "A page with a picture",
                },
                summary = (Dictionary<string, string>?)null,
                seo = (Dictionary<string, object>?)null,
                body = JsonNode.Parse(body),
                schemaVersion = 1,
                fileMediaId,
                rowVersion = "0001-01-01T00:00:00",
            }),
        };

        request.Headers.Add("X-Requested-With", "hub");

        using var response = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task<HttpResponseMessage> PublishAsync(
        HttpClient client,
        long id,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{ContentEndpoints.Pattern}/{id}/publish", UriKind.Relative))
        {
            Content = JsonContent.Create(new { changelog = (string?)null }),
        };

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>A document whose content is the file it carries has nothing in its body.</summary>
    private const string EmptyBody = """
        { "schemaVersion": 1, "sections": [] }
        """;

    /// <summary>A body showing one picture and nothing else, for the half of the test that works.</summary>
    private static string BodyShowing(long single) =>
        $$"""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "id": "s_one",
              "layout": "stacked",
              "blocks": [
                { "id": "b_one", "type": "hero", "version": 1, "props": { "mediaId": {{single}} } }
              ]
            }
          ]
        }
        """;

    /// <summary>A page body that shows one media on its own and another inside a gallery.</summary>
    private static string Body(long single, long inAList) =>
        $$"""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "id": "s_one",
              "layout": "stacked",
              "blocks": [
                { "id": "b_one", "type": "hero", "version": 1, "props": { "mediaId": {{single}} } },
                { "id": "b_two", "type": "gallery", "version": 1, "props": { "mediaIds": [{{inAList}}, 999999] } }
              ]
            }
          ]
        }
        """;

    /// <summary>
    /// A draft and a published version of it, written the way the installation writes: no HTTP
    /// context, so no identity, which is what a seed is.
    /// </summary>
    private async Task<long> SeedPageAsync(string slug, string body, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var page = new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = slug,
            OwnerDepartment = Department.WD,
            Visibility = Visibility.Public,
            Status = PublishStatus.Published,
            PublishedAt = clock.UtcNow,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Pagina con immagini"),
                new KeyValuePair<string, string>("en", "Page with pictures"),
            ]),
            BodyJson = body,
        };

        database.Contents.Add(page);
        await database.SaveChangesAsync(cancellationToken);

        return page.Id;
    }

    /// <summary>A published version whose stored body shows the media, which is what retention reads.</summary>
    private async Task PublishPageShowingAsync(long mediaId, CancellationToken cancellationToken)
    {
        var slug = $"shown-{Guid.NewGuid():N}"[..24];
        var contentId = await SeedPageAsync(slug, Body(mediaId, 0), cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.ContentVersions.Add(new ContentVersion
        {
            ContentId = contentId,
            Version = 1,
            Title = new Localized<string>([new KeyValuePair<string, string>("en", "Page with pictures")]),
            BodyJson = Body(mediaId, 0),
            PublishedAt = clock.UtcNow,
        });

        await database.SaveChangesAsync(cancellationToken);
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

        if (!await database.UserStaffPositions.AnyAsync(
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

    /// <summary>A PNG of the given size, optionally padded so that it is over a size limit.</summary>
    /// <summary>
    /// A PNG header the parser reads, followed by sixteen random bytes: two calls never make the
    /// same file. ⚠️ Since the same bytes in the same library answer the row that exists (decision
    /// note of 12 September 2026), and every test of this class writes into one database while
    /// each keeps a directory of its own, a fixed 10×10 picture uploaded by two tests would be one
    /// row whose file the first test has already deleted. A test that wants the same file twice
    /// keeps the array.
    /// </summary>
    private static byte[] Png(int width, int height, int padding = 0)
    {
        var bytes = new byte[33 + 16 + padding];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        Guid.NewGuid().ToByteArray().CopyTo(bytes, 33);

        return bytes;
    }
}
