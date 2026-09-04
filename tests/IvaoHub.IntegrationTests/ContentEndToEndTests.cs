using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Editorial content over the wire: a page born from a template, edited, published and read by a
/// visitor, against a real MariaDB with the real cookie, the real policies and the real interceptor
/// (design M0 section 8).
/// <para>The last test is the acceptance of M0 itself, run rather than described: a frozen data
/// block keeps saying what it said on the day it was published, and switching it back to live and
/// republishing is what makes it move again.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContentEndToEndTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 620001;
    private const int EventsCoordinatorVid = 620002;
    private const int WebAdvisorVid = 620003;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheSystemTemplatesAreSeededOnceAndStayOutOfTheOrdinaryList()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        // Booting the host a second time against the same database must not seed them again: the
        // key in hub_division_settings is what makes a release able to add a template without
        // undoing the one the staff has since edited.
        await using (var second = new HubWebApplicationFactory(mariaDb.ConnectionString))
        {
            using var warmUp = second.CreateApiClient();
            using var health = await warmUp.GetAsync(new Uri("/health", UriKind.Relative), token);
            health.EnsureSuccessStatusCode();
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var templates = await database.Contents
                .IgnoreQueryFilters()
                .Where(row => row.IsTemplate && row.Slug == "section-page")
                .ToListAsync(token);

            Assert.Single(templates);
            Assert.Equal(Department.WD, templates[0].OwnerDepartment);
        }

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var ordinary = await client.GetFromJsonAsync<JsonElement>(ContentEndpoints.Pattern, token);
        Assert.All(
            ordinary.GetProperty("items").EnumerateArray(),
            item => Assert.False(item.GetProperty("isTemplate").GetBoolean()));

        var asked = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}?filter[isTemplate]=true",
            token);

        Assert.NotEmpty(asked.GetProperty("items").EnumerateArray());
        Assert.All(
            asked.GetProperty("items").EnumerateArray(),
            item => Assert.True(item.GetProperty("isTemplate").GetBoolean()));
    }

    [Fact]
    public async Task NewFromTemplateDeepCopies()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var (templateId, templateBody) = await TemplateAsync("section-page", token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var slug = $"copy-{Guid.NewGuid():N}"[..20];
        var page = await FromTemplateAsync(client, templateId, Department.ED, slug, token);

        Assert.Equal(templateId, page.GetProperty("templateId").GetInt64());
        Assert.Equal(nameof(PublishStatus.Draft), page.GetProperty("status").GetString());
        Assert.False(page.GetProperty("isTemplate").GetBoolean());

        var walker = new BlockDocumentWalker(["it", "en"]);
        var copied = page.GetProperty("body").Deserialize<JsonNode>()!;

        var templateIds = Identifiers(walker, templateBody);
        var copiedIds = Identifiers(walker, copied);

        // A copy that shared an identifier with its template would not be a copy: two rows would
        // answer to one name, and the editor would be editing whichever it found first.
        Assert.Equal(templateIds.Count, copiedIds.Count);
        Assert.Empty(templateIds.Intersect(copiedIds, StringComparer.Ordinal));

        // The keys only a template may carry are left behind, or the page would be able to lift
        // its own restrictions -- and the envelope validator would refuse the next save.
        foreach (var section in walker.EnumerateSections(copied))
        {
            Assert.Null(section.Node["locked"]);
            Assert.Null(section.Node["required"]);
            Assert.Null(section.Node["allowedBlocks"]);
        }

        // What the template does fix travels with the copy: the links section is derived, and its
        // block arrives already asking to be captured at publication (design M0 section 5.6).
        var derived = walker.EnumerateBlocks(copied).Single(block => block.Type == CoreBlocks.LinkList);
        Assert.Equal("frozen", derived.Node["renderMode"]?.GetValue<string>());
    }

    [Fact]
    public async Task TemplateEditRequiresManageTemplates()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebAdvisorVid, position: "IT-WMA1", cancellationToken: token);
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var (templateId, _) = await TemplateAsync("about", token);

        using var advisor = _factory.CreateApiClient();
        await _factory.SignInAsync(advisor, WebAdvisorVid, token);

        // An advisor of the web team holds Content.Edit on WD, so an ordinary page of that
        // department is theirs to change...
        var ordinarySlug = $"advisor-{Guid.NewGuid():N}"[..20];
        using var ordinary = await SendAsync(
            advisor,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(Department.WD, ordinarySlug),
            token);

        Assert.Equal(HttpStatusCode.Created, ordinary.StatusCode);

        // ...and the very same permission is not enough for a template. That is the extra write
        // policy of the CRUD engine, and this is its first real use (design M0 section 5.7).
        var template = await advisor.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/{templateId}",
            token);

        using var refused = await SendAsync(
            advisor,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{templateId}",
            Payload(
                Department.WD,
                template.GetProperty("slug").GetString()!,
                isTemplate: true,
                body: template.GetProperty("body").Deserialize<JsonNode>(),
                rowVersion: template.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // A super administrator holds everything, so the same call goes through: what was refused
        // was the permission and not the payload.
        using var superadmin = _factory.CreateApiClient();
        await _factory.SignInAsync(superadmin, SuperadminVid, token);

        var reloaded = await superadmin.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/{templateId}",
            token);

        using var allowed = await SendAsync(
            superadmin,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{templateId}",
            Payload(
                Department.WD,
                reloaded.GetProperty("slug").GetString()!,
                isTemplate: true,
                body: reloaded.GetProperty("body").Deserialize<JsonNode>(),
                rowVersion: reloaded.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task EnvelopeValidationRejectsUnknownBlockAndDepth()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s1", "blocks": [ { "id": "b1", "type": "mystery", "props": {} } ],
            "sections": [ { "id": "s2", "sections": [ { "id": "s3",
              "sections": [ { "id": "s4" } ] } ] } ] } ]
        }
        """);

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(Department.ED, $"bad-{Guid.NewGuid():N}"[..20], body: body),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");

        // The failure is filed under the path of the thing that is wrong, so the editor can put the
        // message next to the block rather than at the top of the screen.
        Assert.Equal(
            "errors.body.blockTypeUnknown",
            errors.GetProperty("body.sections[0].blocks[0]")[0].GetString());

        Assert.Equal(
            "errors.body.tooDeep",
            errors.GetProperty("body.sections[0].sections[0].sections[0].sections[0]")[0].GetString());
    }

    [Fact]
    public async Task PublishRejectsMissingLocales()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var body = Body(italianOnly: true);
        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(Department.ED, $"half-{Guid.NewGuid():N}"[..20], body: body, english: null),
            token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        // A draft is allowed to be half written. Showing it to the public is not.
        using var refused = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{id}/publish",
            new { changelog = (string?)null },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
        var errors = problem.GetProperty("errors");
        var missing = problem.GetProperty(CrudProblems.LocalizedExtension);

        Assert.Equal("errors.localized.missing", errors.GetProperty("title")[0].GetString());
        Assert.Equal(["en"], Strings(missing.GetProperty("title")));

        // And the paths inside the body, one per translated value that is not written everywhere.
        Assert.Equal(
            "errors.localized.missing",
            errors.GetProperty("body.sections[0].blocks[0].props.text")[0].GetString());
        Assert.Equal(["en"], Strings(missing.GetProperty("body.sections[0].blocks[0].props.text")));
    }

    [Fact]
    public async Task PublicReadsOnlyPublishedVersion()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var slug = $"draft-{Guid.NewGuid():N}"[..20];
        var id = await CreateAsync(client, Department.ED, slug, Body(), Visibility.Public, token);

        using var anonymous = _factory.CreateApiClient();

        // A draft is nobody's business but the staff's.
        using var beforePublishing = await anonymous.GetAsync(PublicUri(slug), token);
        Assert.Equal(HttpStatusCode.NotFound, beforePublishing.StatusCode);

        await PublishAsync(client, id, token);

        var published = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);
        Assert.Equal(1, published.GetProperty("version").GetInt32());
        Assert.Equal("Prima stesura", Heading(published, "it"));

        // Editing the draft changes nothing out there: the public site reads the version, and the
        // version was written when somebody published it.
        var loaded = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", token);
        using var edited = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(
                Department.ED,
                slug,
                visibility: Visibility.Public,
                body: Body(heading: "Seconda stesura"),
                rowVersion: loaded.GetProperty("rowVersion").GetString()),
            token);

        edited.EnsureSuccessStatusCode();

        var stale = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);
        Assert.Equal("Prima stesura", Heading(stale, "it"));

        await PublishAsync(client, id, token);

        var fresh = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);
        Assert.Equal(2, fresh.GetProperty("version").GetInt32());
        Assert.Equal("Seconda stesura", Heading(fresh, "it"));
    }

    [Fact]
    public async Task ContentPublishFreezesDataBlocks()
    {
        // The acceptance of M0, run rather than described (implementation plan section D, F7).
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var category = $"software-{Guid.NewGuid():N}"[..16];
        await SeedLinkAsync(Department.ED, $"https://example.org/{category}/one", category, sort: 0, token);
        await SeedLinkAsync(Department.ED, $"https://example.org/{category}/two", category, sort: 1, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var slug = $"frozen-{Guid.NewGuid():N}"[..20];
        var id = await CreateAsync(client, Department.ED, slug, Body(category: category, mode: "frozen"), Visibility.Public, token);
        await PublishAsync(client, id, token);

        using var anonymous = _factory.CreateApiClient();

        var captured = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);
        Assert.Equal(2, FrozenItems(captured).GetArrayLength());

        // A third link in the same category. The page was captured, so it does not move.
        await SeedLinkAsync(Department.ED, $"https://example.org/{category}/three", category, sort: 2, token);

        var unchanged = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);
        Assert.Equal(2, FrozenItems(unchanged).GetArrayLength());

        // Switch the block to live and republish: now there is nothing captured at all, and the
        // browser will ask the provider itself.
        var loaded = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", token);
        using var edited = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(
                Department.ED,
                slug,
                visibility: Visibility.Public,
                body: Body(category: category, mode: "live"),
                rowVersion: loaded.GetProperty("rowVersion").GetString()),
            token);

        edited.EnsureSuccessStatusCode();
        await PublishAsync(client, id, token);

        var live = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);
        var block = LinkListBlock(live);
        Assert.Equal(JsonValueKind.Null, block.GetProperty("frozen").ValueKind);

        // And what the browser would ask for is the whole list, the third link included.
        var props = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($$"""{"category":"{{category}}","limit":10}"""))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var resolved = await anonymous.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/blocks/data/{CoreBlocks.LinkList}?props={props}", UriKind.Relative),
            token);

        Assert.Equal(3, resolved.GetProperty("items").GetArrayLength());
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static Uri PublicUri(string slug) =>
        new($"{ContentEndpoints.Pattern}/public/{ContentKind.Page}/{slug}", UriKind.Relative);

    private static string? Heading(JsonElement content, string locale) =>
        content.GetProperty("body").GetProperty("sections")[0]
            .GetProperty("blocks")[0].GetProperty("props").GetProperty("text")
            .GetProperty(locale).GetString();

    private static JsonElement LinkListBlock(JsonElement content) =>
        content.GetProperty("body").GetProperty("sections")[0].GetProperty("blocks")[1];

    private static JsonElement FrozenItems(JsonElement content) =>
        LinkListBlock(content).GetProperty("frozen").GetProperty("items");

    private static string?[] Strings(JsonElement array) =>
        [.. array.EnumerateArray().Select(value => value.GetString())];

    private static List<string> Identifiers(BlockDocumentWalker walker, JsonNode body) =>
    [
        .. walker.EnumerateSections(body).Select(node => node.Id!),
        .. walker.EnumerateBlocks(body).Select(node => node.Id!),
    ];

    /// <summary>
    /// A hero heading and a link list, which is the smallest body that shows both kinds of block.
    /// </summary>
    private static JsonNode Body(
        string heading = "Prima stesura",
        bool italianOnly = false,
        string? category = null,
        string mode = "live")
    {
        var text = italianOnly
            ? """{ "it": "Prima stesura" }"""
            : $$"""{ "it": "{{heading}}", "en": "First draft" }""";

        return JsonNode.Parse($$"""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "id": "s_hero",
              "key": "hero",
              "layout": "stacked",
              "blocks": [
                { "id": "b_heading", "type": "heading", "version": 1,
                  "props": { "level": 1, "text": {{text}} } },
                { "id": "b_links", "type": "linkList", "version": 1, "renderMode": "{{mode}}",
                  "props": { "category": {{(category is null ? "null" : $"\"{category}\"")}}, "limit": 10 } }
              ]
            }
          ]
        }
        """)!;
    }

    private static object Payload(
        Department department,
        string slug,
        string? italian = "Pagina di prova",
        string? english = "Test page",
        bool isTemplate = false,
        Visibility visibility = Visibility.Staff,
        JsonNode? body = null,
        string? rowVersion = null)
    {
        var title = new Dictionary<string, string>(StringComparer.Ordinal);
        if (italian is not null)
        {
            title["it"] = italian;
        }

        if (english is not null)
        {
            title["en"] = english;
        }

        return new
        {
            kind = nameof(ContentKind.Page),
            slug,
            ownerDepartment = department.ToString(),
            visibility = visibility.ToString(),
            isTemplate,
            title,
            summary = (Dictionary<string, string>?)null,
            seo = (Dictionary<string, object>?)null,
            body = body ?? Body(),
            schemaVersion = 1,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };
    }

    private static async Task<long> CreateAsync(
        HttpClient client,
        Department department,
        string slug,
        JsonNode body,
        Visibility visibility,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(department, slug, visibility: visibility, body: body),
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

    private static async Task<JsonElement> FromTemplateAsync(
        HttpClient client,
        long templateId,
        Department department,
        string slug,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/from-template/{templateId}",
            new { ownerDepartment = department.ToString(), slug },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task<(long Id, JsonNode Body)> TemplateAsync(string slug, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var template = await database.Contents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(row => row.IsTemplate && row.Slug == slug, cancellationToken);

        return (template.Id, JsonNode.Parse(template.BodyJson)!);
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

    private async Task SeedLinkAsync(
        Department department,
        string url,
        string category,
        int sort,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.Links.Add(new Link
        {
            OwnerDepartment = department,
            Visibility = Visibility.Public,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", $"Link {sort}"),
                new KeyValuePair<string, string>("en", $"Link {sort}"),
            ]),
            Url = url,
            Category = category,
            Sort = sort,
            IsActive = true,
        });

        await database.SaveChangesAsync(cancellationToken);
    }
}
