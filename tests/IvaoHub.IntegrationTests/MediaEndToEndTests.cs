using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    private static byte[] Png(int width, int height, int padding = 0)
    {
        var bytes = new byte[33 + padding];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);

        return bytes;
    }
}
