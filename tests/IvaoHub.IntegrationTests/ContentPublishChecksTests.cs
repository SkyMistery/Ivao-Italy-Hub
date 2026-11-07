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
/// What publication checks in a body without reading a schema (design of the editor, phase 3, section 5.9): where the
/// links of a page lead, and the picture a section asks for. VID 785101; the rows are the web team's, under a shelf of
/// this class, and are found by their own slugs because the database is shared with every other class.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContentPublishChecksTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 785101;
    private const string Link = "body.sections[0].blocks[0].props.href";

    private HubWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;
        _shelf = await TestShelf.SeedAsync(_factory, "test-shelf-publish-checks", token);
        await SeedSuperadminAsync(token);

        _client = _factory.CreateApiClient();
        await _factory.SignInAsync(_client, SuperadminVid, token);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ALinkThatLeadsNowhereStopsThePageAndTheEditorIsToldBeforeTheClick()
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(Visibility.Public, ButtonTo("/nothing-is-here"), token);

        Assert.Equal([ContentPublishService.LinkNowhereKey], await ProblemsAsync(page.Id, Link, token));

        using var refused = await PublishAsync(page.Id, token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var errors = (await refused.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");
        Assert.Equal(ContentPublishService.LinkNowhereKey, errors.GetProperty(Link)[0].GetString());

        // And it stayed a draft: a draft may say anything, and saving this one was never refused.
        Assert.False(await IsOnlineAsync(page.Id, token));
    }

    [Fact]
    public async Task APageThatHasNotComeOutIsNowhereUntilItHas()
    {
        var token = TestContext.Current.CancellationToken;
        var later = await WriteAsync(Visibility.Public, Text("Later."), token);
        var page = await WriteAsync(Visibility.Public, ButtonTo(later.Url), token);

        Assert.Equal([ContentPublishService.LinkNowhereKey], await ProblemsAsync(page.Id, Link, token));

        await PublishedAsync(later.Id, token);

        Assert.Empty(await ProblemsAsync(page.Id, Link, token));
        await PublishedAsync(page.Id, token);
    }

    [Fact]
    public async Task ALinkMayNotLeadToSomewhereItsReadersCannotGo()
    {
        var token = TestContext.Current.CancellationToken;
        var forTheStaff = await WriteAsync(Visibility.Staff, Text("Staff."), token);
        await PublishedAsync(forTheStaff.Id, token);

        var forEverybody = await WriteAsync(Visibility.Public, ButtonTo(forTheStaff.Url), token);
        Assert.Equal([ContentPublishService.LinkLessVisibleKey], await ProblemsAsync(forEverybody.Id, Link, token));

        // The other way round is the ordinary case.
        var alsoForTheStaff = await WriteAsync(Visibility.Staff, ButtonTo(forTheStaff.Url), token);
        Assert.Empty(await ProblemsAsync(alsoForTheStaff.Id, Link, token));

        // The back office is a screen with readers of its own.
        var toTheBackOffice = await WriteAsync(Visibility.Public, ButtonTo("/staff/content"), token);
        Assert.Equal([ContentPublishService.LinkLessVisibleKey], await ProblemsAsync(toTheBackOffice.Id, Link, token));
    }

    [Fact]
    public async Task ALinkInsideAPieceOfMarkdownIsALink()
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(
            Visibility.Public,
            Text("[Here](/nothing-is-here-either), and ![a picture](/nothing-is-here-either.png)."),
            token);

        const string InItalian = "body.sections[0].blocks[0].props.markdown.it";
        Assert.Equal([ContentPublishService.LinkNowhereKey], await ProblemsAsync(page.Id, InItalian, token));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/calendar")]
    [InlineData("/contact?subject=1#form")]
    [InlineData("/tours")]
    [InlineData("/tours/whatever-the-module-says")]
    [InlineData("/me")]
    [InlineData("https://example.org/nobody-knows")]
    [InlineData("//example.org/nobody-knows")]
    [InlineData("#a-part-of-this-page")]
    [InlineData("mailto:somebody@example.org")]
    public async Task WhatTheApplicationAnswersForAndWhatLeavesTheSiteAreNotAsked(string address)
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(Visibility.Public, ButtonTo(address), token);

        Assert.Empty(await ProblemsAsync(page.Id, Link, token));
    }

    [Fact]
    public async Task APageMayLeadToItselfOnTheDayItComesOut()
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(Visibility.Public, Text("Empty for a moment."), token);

        await SetBodyAsync(page.Id, ButtonTo(page.Url), token);

        Assert.Empty(await ProblemsAsync(page.Id, Link, token));
    }

    [Fact]
    public async Task ANewsItemADocumentAndAnAddressLeftBehindAllArrive()
    {
        var token = TestContext.Current.CancellationToken;

        var news = await WriteAsync(Visibility.Public, Text("News."), token, ContentKind.News);
        Assert.Equal(
            [ContentPublishService.LinkNowhereKey],
            await ProblemsAsync((await WriteAsync(Visibility.Public, ButtonTo($"/news/{news.Slug}"), token)).Id, Link, token));

        await PublishedAsync(news.Id, token);
        Assert.Empty(await ProblemsAsync((await WriteAsync(Visibility.Public, ButtonTo($"/news/{news.Slug}"), token)).Id, Link, token));

        // Nothing sits under a news item.
        Assert.Equal(
            [ContentPublishService.LinkNowhereKey],
            await ProblemsAsync((await WriteAsync(Visibility.Public, ButtonTo($"/news/{news.Slug}/more"), token)).Id, Link, token));

        var moved = await WriteAsync(Visibility.Public, Text("Moved."), token);
        await PublishedAsync(moved.Id, token);
        var oldAddress = $"left-{Guid.NewGuid():N}"[..20];
        await LeaveBehindAsync(moved.Id, oldAddress, token);

        Assert.Empty(await ProblemsAsync((await WriteAsync(Visibility.Public, ButtonTo($"/{oldAddress}"), token)).Id, Link, token));
    }

    [Fact]
    public async Task ASectionThatAsksForAPictureAndNamesNoneStopsThePage()
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(
            Visibility.Public,
            """{ "schemaVersion": 1, "sections": [ { "id": "s_main", "background": "image", "blocks": [] } ] }""",
            token);

        Assert.Equal(
            [ContentPublishService.MediaToChooseKey],
            await ProblemsAsync(page.Id, "body.sections[0].mediaId", token));
    }

    [Theory]
    [InlineData("""{ "width": "full" }""", "image", "mediaId")]
    [InlineData("""{ "images": [ { "mediaId": 999999996 }, { } ] }""", "gallery", "images[1].mediaId")]
    [InlineData("""{ "items": [ { "name": "Partner" } ] }""", "logoGrid", "items[0].mediaId")]
    public async Task APictureABlockCannotDoWithoutStopsThePageAndAnOptionalOneDoesNot(string props, string type, string field)
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(Visibility.Public, OneBlock(type, props), token);

        Assert.Equal(
            [ContentPublishService.MediaToChooseKey],
            await ProblemsAsync(page.Id, $"body.sections[0].blocks[0].props.{field}", token));

        using var refused = await PublishAsync(page.Id, token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var hero = await WriteAsync(Visibility.Public, OneBlock("hero", """{ "title": { "it": "Titolo", "en": "Title" } }"""), token);
        Assert.Empty(await ProblemsAsync(hero.Id, "body.sections[0].blocks[0].props.mediaId", token));
    }

    /// <summary>
    /// What Carmine's decision does to a page that went out before it, with a picture missing: the public keeps reading
    /// the version, and only what would become the next one stops.
    /// </summary>
    [Fact]
    public async Task APageAlreadyOnlineWithAPictureMissingStaysOnlineAndOnlyItsNextVersionStops()
    {
        var token = TestContext.Current.CancellationToken;
        var page = await WriteAsync(Visibility.Public, OneBlock("image", """{ "width": "full" }"""), token);
        await PublishWithoutTheChecksAsync(page.Id, token);

        using var anonymous = _factory.CreateApiClient();
        var publicAddress = new Uri(
            $"{ContentEndpoints.Pattern}/public/page?path={Uri.EscapeDataString(page.Url.TrimStart('/'))}",
            UriKind.Relative);

        var read = await anonymous.GetFromJsonAsync<JsonElement>(publicAddress, token);
        Assert.Equal(page.Id, read.GetProperty("page").GetProperty("id").GetInt64());

        using var refused = await PublishAsync(page.Id, token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // And the refusal took nothing away from what was there.
        Assert.True(await IsOnlineAsync(page.Id, token));
        var stillRead = await anonymous.GetFromJsonAsync<JsonElement>(publicAddress, token);
        Assert.Equal(page.Id, stillRead.GetProperty("page").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task EveryKindOfProblemIsSaidAtOnce()
    {
        var token = TestContext.Current.CancellationToken;

        // English is missing, and the link leads nowhere: the list is the whole of what stands in the way.
        var page = await WriteAsync(
            Visibility.Public,
            """
            { "schemaVersion": 1, "sections": [ { "id": "s_main", "layout": "stacked", "blocks": [
              { "id": "b_one", "type": "cta", "version": 1,
                "props": { "label": { "it": "Vai", "en": "" }, "href": "/nothing-is-here" } } ] } ] }
            """,
            token);

        var problems = await _client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{page.Id}/publish-problems", token);
        var errors = problems.GetProperty("errors");

        Assert.Equal("errors.localized.missing", errors.GetProperty("body.sections[0].blocks[0].props.label")[0].GetString());
        Assert.Equal(ContentPublishService.LinkNowhereKey, errors.GetProperty(Link)[0].GetString());
    }

    /// <summary>The home leads to <c>/start</c>, a file the seed reads after it: seeding has to get the two out all the same.</summary>
    [Fact]
    public async Task TheSeededHomeComesOutThoughThePageItLeadsToIsSeededAfterIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var home = await database.Contents.IgnoreQueryFilters()
            .FirstAsync(row => row.Kind == ContentKind.Page && row.Slug == "home" && !row.IsTemplate, token);

        Assert.Contains("/start", home.BodyJson, StringComparison.Ordinal);
        Assert.NotNull(home.PublishedVersionId);
    }

    private static string OneBlock(string type, string props) => $$"""
        { "schemaVersion": 1, "sections": [ { "id": "s_main", "layout": "stacked", "blocks": [
          { "id": "b_one", "type": "{{type}}", "version": 1, "props": {{props}} } ] } ] }
        """;

    private async Task PublishWithoutTheChecksAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var row = await database.Contents.IgnoreQueryFilters().FirstAsync(content => content.Id == id, cancellationToken);
        await PublishedByHand.PublishAsync(database, row, cancellationToken);
    }

    private static string ButtonTo(string address) => $$"""
        { "schemaVersion": 1, "sections": [ { "id": "s_main", "layout": "stacked", "blocks": [
          { "id": "b_one", "type": "cta", "version": 1,
            "props": { "label": { "it": "Vai", "en": "Go" }, "href": {{JsonSerializer.Serialize(address)}} } } ] } ] }
        """;

    private static string Text(string markdown) => $$"""
        { "schemaVersion": 1, "sections": [ { "id": "s_main", "layout": "stacked", "blocks": [
          { "id": "b_one", "type": "text", "version": 1,
            "props": { "markdown": { "it": {{JsonSerializer.Serialize(markdown)}}, "en": "Text." } } } ] } ] }
        """;

    private sealed record Written(long Id, string Slug, string Url);

    private async Task<Written> WriteAsync(
        Visibility visibility,
        string body,
        CancellationToken cancellationToken,
        ContentKind kind = ContentKind.Page)
    {
        var slug = $"chk-{Guid.NewGuid():N}"[..20];
        using var created = await SendAsync(
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            new
            {
                kind = kind.ToString(),
                slug,
                ownerDepartment = nameof(Department.WD),
                visibility = visibility.ToString(),
                isTemplate = false,
                title = new Dictionary<string, string> { ["it"] = "Controllo", ["en"] = "Check" },
                summary = (Dictionary<string, string>?)null,
                seo = (Dictionary<string, object>?)null,
                body = JsonNode.Parse(body),
                schemaVersion = 1,
                rowVersion = "0001-01-01T00:00:00",
                parentId = kind == ContentKind.Page ? _shelf : (long?)null,
            },
            cancellationToken);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(cancellationToken));

        var row = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var url = kind == ContentKind.Page ? $"/{row.GetProperty("path").GetString()}" : $"/news/{slug}";
        return new Written(row.GetProperty("id").GetInt64(), slug, url);
    }

    /// <summary>The keys of what stands in the way at one path, empty when it has nothing to say about that path.</summary>
    private async Task<string[]> ProblemsAsync(long id, string path, CancellationToken cancellationToken)
    {
        var problems = await _client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}/publish-problems", cancellationToken);

        return problems.GetProperty("errors").TryGetProperty(path, out var keys)
            ? [.. keys.EnumerateArray().Select(key => key.GetString()!)]
            : [];
    }

    private Task<HttpResponseMessage> PublishAsync(long id, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, $"{ContentEndpoints.Pattern}/{id}/publish", new { changelog = (string?)null }, cancellationToken);

    private async Task PublishedAsync(long id, CancellationToken cancellationToken)
    {
        using var published = await PublishAsync(id, cancellationToken);
        Assert.True(published.StatusCode == HttpStatusCode.OK, await published.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<bool> IsOnlineAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        return await database.Contents.IgnoreQueryFilters().Where(row => row.Id == id).Select(row => row.PublishedVersionId).FirstAsync(cancellationToken) is not null;
    }

    private async Task SetBodyAsync(long id, string body, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var row = await database.Contents.IgnoreQueryFilters().FirstAsync(content => content.Id == id, cancellationToken);
        row.BodyJson = JsonNode.Parse(body)!.ToJsonString();
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task LeaveBehindAsync(long id, string oldAddress, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var row = await database.Contents.IgnoreQueryFilters().FirstAsync(content => content.Id == id, cancellationToken);
        row.PreviousPathsJson = JsonSerializer.Serialize(new[] { oldAddress });
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Content = JsonContent.Create(payload, payload.GetType());
        request.Headers.Add("X-Requested-With", "hub");
        return await _client.SendAsync(request, cancellationToken);
    }

    private async Task SeedSuperadminAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == SuperadminVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = SuperadminVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsSuperadmin = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
