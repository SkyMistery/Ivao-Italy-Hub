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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The criteria of G20, run (note 2026-09-13-contenuti-centralizzati §3.3–3.4): a document filed in
/// two collections appears on the two pages that list them, whatever department the pages belong
/// to; a file a published page shows is archived rather than deleted; and a file replaced on the
/// same row is what the published page shows, at a new address that may still be cached for a year.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CollectionsAndMediaTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own: the suite shares one database (see NewsDocumentsAndCategoriesTests).
    private const int WebCoordinatorVid = 750001;

    /// <summary>An eight by eight PNG. A byte after its end makes a second file of the same picture.</summary>
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC";

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ADocumentInTwoCollectionsAppearsOnThePagesThatListThem()
    {
        var token = TestContext.Current.CancellationToken;
        using var web = await WebAsync(token);

        var guides = Key("guides");
        var procedures = Key("procedures");
        var guidesId = await CreateCollectionAsync(web, Department.AOD, guides, token);
        await CreateCollectionAsync(web, Department.AOD, procedures, token);

        var document = await CreateAsync(web, ContentKind.Document, Department.AOD, Key("doc"), Body(), token, [guides, procedures]);
        await PublishAsync(web, document, token);

        // A page of Training listing the guides of ATC, and a page of ATC listing its procedures.
        var training = await CreateAsync(web, ContentKind.Page, Department.TD, Key("training"), ListOf(Department.AOD, guides), token);
        var atc = await CreateAsync(web, ContentKind.Page, Department.AOD, Key("atc"), ListOf(Department.AOD, procedures), token);
        await PublishAsync(web, training, token);
        await PublishAsync(web, atc, token);

        var appears = await web.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{document}/appears-in", token);
        var pages = appears.EnumerateArray().Select(page => page.GetProperty("id").GetInt64()).ToArray();
        Assert.Contains(training, pages);
        Assert.Contains(atc, pages);

        var uses = await web.GetFromJsonAsync<JsonElement>($"{CategoriesEndpoints.Pattern}/{guidesId}/uses", token);
        Assert.Equal([training], uses.EnumerateArray().Select(page => page.GetProperty("id").GetInt64()).ToArray());

        // And the list itself holds the document, under either collection.
        using var anonymous = _factory.CreateApiClient();
        var listed = await anonymous.GetFromJsonAsync<JsonElement>(BlockDataUri(Department.AOD, procedures), token);
        var item = Assert.Single(listed.GetProperty("items").EnumerateArray());
        Assert.Equal(document, item.GetProperty("id").GetInt64());
        Assert.Equal([guides, procedures], item.GetProperty("collections").EnumerateArray().Select(key => key.GetString()).ToArray());
    }

    [Fact]
    public async Task AFileAPublishedPageShowsIsArchivedNotDeleted()
    {
        var token = TestContext.Current.CancellationToken;
        using var web = await WebAsync(token);

        var logo = await UploadAsync(web, Department.WD, [.. Convert.FromBase64String(Png), 9], token);
        var page = await CreateAsync(web, ContentKind.Page, Department.TD, Key("logo-page"), WithImage(logo), token);
        await PublishAsync(web, page, token);

        using var refused = await SendAsync(web, HttpMethod.Delete, $"{MediaEndpoints.Pattern}/{logo}", null, token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("errors.media.inUse", problem.GetProperty("errors").GetProperty("id")[0].GetString());

        using var archived = await SendAsync(web, HttpMethod.Post, $"{MediaEndpoints.Pattern}/{logo}/archive", null, token);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        // Out of the library and of the picker, in the archive, and still served.
        Assert.DoesNotContain(logo, await IdsAsync(web, $"{MediaEndpoints.Pattern}?filter[ownerDepartment]=WD&pageSize=100", token));
        Assert.Contains(logo, await IdsAsync(web, $"{MediaEndpoints.Pattern}?filter[ownerDepartment]=WD&filter[archived]=true&pageSize=100", token));

        using var anonymous = _factory.CreateApiClient();
        using var served = await anonymous.GetAsync(new Uri($"/media/{logo}/file", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        // A file nobody shows is still deleted.
        var unused = await UploadAsync(web, Department.WD, [.. Convert.FromBase64String(Png), 1], token);
        using var deleted = await SendAsync(web, HttpMethod.Delete, $"{MediaEndpoints.Pattern}/{unused}", null, token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task AReplacedFileIsWhatThePublishedPageShowsAtANewAddress()
    {
        var token = TestContext.Current.CancellationToken;
        using var web = await WebAsync(token);

        var logo = await UploadAsync(web, Department.WD, [.. Convert.FromBase64String(Png), 2], token);
        var slug = Key("replaced");
        var page = await CreateAsync(web, ContentKind.Page, Department.TD, slug, WithImage(logo), token);
        await PublishAsync(web, page, token);

        using var anonymous = _factory.CreateApiClient();
        var before = await PublicFingerprintAsync(anonymous, slug, logo, token);

        using var replaced = await UploadAsync(web, $"{MediaEndpoints.Pattern}/{logo}/file", null, [.. Convert.FromBase64String(Png), 3], token);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var detail = await replaced.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(logo, detail.GetProperty("id").GetInt64());

        // Not published again: the page names the same identifier, and the answer carries the new file.
        var after = await PublicFingerprintAsync(anonymous, slug, logo, token);
        Assert.NotEqual(before, after);
        Assert.Contains($"/media/{logo}/{after}/", detail.GetProperty("url").GetString(), StringComparison.Ordinal);

        using var current = await anonymous.GetAsync(new Uri($"/media/{logo}/{after}/file", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Contains("immutable", current.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        using var stale = await anonymous.GetAsync(new Uri($"/media/{logo}/{before}/file", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.True(stale.Headers.CacheControl?.NoCache);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<HttpClient> WebAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", cancellationToken);
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, WebCoordinatorVid, cancellationToken);
        return client;
    }

    private static string Key(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static JsonNode Body(string blocks = """{ "id": "b_heading", "type": "heading", "version": 1, "props": { "level": 1, "text": { "it": "Titolo", "en": "Heading" } } }""") =>
        JsonNode.Parse($$"""{ "schemaVersion": 1, "sections": [ { "id": "s_body", "layout": "stacked", "blocks": [ {{blocks}} ] } ] }""")!;

    private static JsonNode ListOf(Department department, string collection) => Body(
        $$"""{ "id": "b_list", "type": "documentList", "version": 1, "renderMode": "live", "props": { "department": "{{department}}", "category": "{{collection}}", "limit": 10 } }""");

    private static JsonNode WithImage(long mediaId) => Body(
        $$"""{ "id": "b_image", "type": "image", "version": 1, "props": { "mediaId": {{mediaId}}, "alt": { "it": "Logo", "en": "Logo" } } }""");

    private static Uri BlockDataUri(Department department, string collection)
    {
        var props = $$"""{"department":"{{department}}","category":"{{collection}}","limit":10}""";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return new Uri($"/api/blocks/data/{CoreBlocks.DocumentList}?props={encoded}", UriKind.Relative);
    }

    private static async Task<string> PublicFingerprintAsync(HttpClient client, string slug, long mediaId, CancellationToken cancellationToken)
    {
        var answer = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/public/page?path={slug}", cancellationToken);
        return answer.GetProperty("page").GetProperty("media").GetProperty(mediaId.ToString(System.Globalization.CultureInfo.InvariantCulture)).GetString()!;
    }

    private static async Task<long[]> IdsAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(path, cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];
    }

    private static async Task<long> CreateCollectionAsync(HttpClient client, Department department, string key, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, CategoriesEndpoints.Pattern, new
        {
            kind = nameof(ContentKind.Document),
            ownerDepartment = department.ToString(),
            key,
            label = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Raccolta", ["en"] = "Collection" },
            sort = 0,
            isActive = true,
            rowVersion = "0001-01-01T00:00:00",
        }, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task<long> CreateAsync(
        HttpClient client,
        ContentKind kind,
        Department department,
        string slug,
        JsonNode body,
        CancellationToken cancellationToken,
        string[]? collections = null)
    {
        using var response = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, new
        {
            kind = kind.ToString(),
            slug,
            ownerDepartment = department.ToString(),
            visibility = nameof(Visibility.Public),
            isTemplate = false,
            title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Prova", ["en"] = "Test" },
            body,
            schemaVersion = 1,
            collections = collections ?? [],
            pinned = false,
            sort = 0,
            rowVersion = "0001-01-01T00:00:00",
        }, cancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{ContentEndpoints.Pattern}/{id}/publish", new { changelog = (string?)null }, cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static async Task<long> UploadAsync(HttpClient client, Department department, byte[] bytes, CancellationToken cancellationToken)
    {
        using var response = await UploadAsync(client, MediaEndpoints.Pattern, department, bytes, cancellationToken);
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancellationToken));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string path,
        Department? department,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "logo.png");

        if (department is { } owner)
        {
            form.Add(new StringContent(owner.ToString()), "ownerDepartment");
            form.Add(new StringContent(nameof(Visibility.Public)), "visibility");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = form };
        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
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
        user.LastName = "Web";
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
