using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The review of a page (G19, note 2026-09-13-contenuti-centralizzati §3.2): a coordinator marks a
/// page ready and may not publish it; nobody writes it while it waits; the director reads what
/// changed, corrects the address, publishes it with its menu entry; the author is told; a page sent
/// back is a draft again with the note; and a news item of the same coordinator is published without
/// anybody's leave, because the division approves only pages.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PageReviewTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int DirectorVid = 680001;
    private const int EventsCoordinatorVid = 680002;

    private HubWebApplicationFactory _factory = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        _shelf = await TestShelf.SeedAsync(_factory, "test-shelf-review", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ACoordinatorMarksAPageReadyAndTheDirectorPublishesItCorrected()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, director) = await PeopleAsync(token);

        var slug = Stem();
        var page = await CreateAsync(coordinator, ContentKind.Page, slug, Body("Primo"), token);

        // Not published by whoever wrote it: pages go through approval in this division.
        using var direct = await SendAsync(coordinator, HttpMethod.Post, $"{ContentEndpoints.Pattern}/{page.Id}/publish", new { changelog = (string?)null }, token);
        Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);

        // Ready, with a proposed menu entry and a word for whoever reads it.
        using var ready = await ReviewAsync(coordinator, page.Id, new
        {
            action = "Ready",
            note = "Pronta per la pubblicazione",
            menu = new { parentId = (long?)null, label = new Dictionary<string, string> { ["it"] = "Eventi speciali", ["en"] = "Special events" } },
        }, token);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Ready", (await ready.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("status").GetString());

        // While it waits, nobody writes it — the refusal is on the row, not a courtesy of the screen.
        var current = await coordinator.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{page.Id}", token);
        using var written = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{page.Id}",
            Payload(ContentKind.Page, slug, Body("Cambiata di nascosto"), current.GetProperty("rowVersion").GetString()),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, written.StatusCode);
        Assert.Equal("errors.content.review.inReview", await FirstErrorAsync(written, "status", token));

        // The approver is told, and reads a first publication with its menu entry.
        Assert.NotEmpty(await NotificationsAsync(NotificationTypes.ContentReadyForApproval, page.Id, DirectorVid, token));

        var summary = await director.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{page.Id}/review", token);
        Assert.True(summary.GetProperty("firstPublication").GetBoolean());
        Assert.Equal("Special events", summary.GetProperty("menu").GetProperty("label").GetProperty("en").GetString());
        Assert.Equal("Pronta per la pubblicazione", summary.GetProperty("note").GetString());

        // Approved with the address corrected: published as it was marked ready, at the new address.
        var corrected = $"{slug}-ok";
        using var approved = await ReviewAsync(director, page.Id, new
        {
            action = "Approve",
            changelog = "Prima edizione",
            slug = corrected,
            parentId = _shelf,
            menu = new { parentId = (long?)null, label = new Dictionary<string, string> { ["it"] = "Eventi speciali", ["en"] = "Special events" } },
        }, token);
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(token));

        using var anonymous = _factory.CreateApiClient();
        var read = await anonymous.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/public/page?path={Uri.EscapeDataString($"test-shelf-review/{corrected}")}",
            token);
        Assert.Equal("Primo", read.GetProperty("page").GetProperty("body").GetProperty("sections")[0]
            .GetProperty("blocks")[0].GetProperty("props").GetProperty("text").GetProperty("it").GetString());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var version = await database.ContentVersions.AsNoTracking().SingleAsync(row => row.ContentId == page.Id, token);
            Assert.Equal(DirectorVid, version.ApprovedBy);

            Assert.True(await database.MenuItems.IgnoreQueryFilters().AnyAsync(
                item => item.Path == $"/test-shelf-review/{corrected}",
                token));
        }

        // And whoever marked it ready hears that it is online.
        Assert.NotEmpty(await NotificationsAsync(NotificationTypes.ContentApproved, page.Id, EventsCoordinatorVid, token));
    }

    [Fact]
    public async Task APageSentBackIsADraftAgainWithTheNoteAndAWithdrawnOneToo()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, director) = await PeopleAsync(token);

        var page = await CreateAsync(coordinator, ContentKind.Page, Stem(), Body("Da rivedere"), token);

        using var ready = await ReviewAsync(coordinator, page.Id, new { action = "Ready" }, token);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        // The author cannot send back, and the approver can.
        using var notTheirs = await ReviewAsync(coordinator, page.Id, new { action = "SendBack", note = "No" }, token);
        Assert.Equal(HttpStatusCode.Forbidden, notTheirs.StatusCode);

        using var sentBack = await ReviewAsync(director, page.Id, new { action = "SendBack", note = "Manca la sezione degli orari" }, token);
        Assert.Equal(HttpStatusCode.OK, sentBack.StatusCode);

        var row = await sentBack.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("Draft", row.GetProperty("status").GetString());
        Assert.NotEmpty(await NotificationsAsync(NotificationTypes.ContentSentBack, page.Id, EventsCoordinatorVid, token));

        // Ready again, and withdrawn by the author: a draft, and nobody is told anything.
        using var again = await ReviewAsync(coordinator, page.Id, new { action = "Ready" }, token);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        using var withdrawn = await ReviewAsync(coordinator, page.Id, new { action = "Withdraw" }, token);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("Draft", (await withdrawn.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheSummarySaysWhichSectionChangedSinceWhatIsOnline()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, director) = await PeopleAsync(token);

        var slug = Stem();
        var page = await CreateAsync(coordinator, ContentKind.Page, slug, Body("Prima"), token);

        using (var first = await ReviewAsync(coordinator, page.Id, new { action = "Ready" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var approved = await ReviewAsync(director, page.Id, new { action = "Approve" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        }

        // The hero is written again, the other section is not.
        var current = await coordinator.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{page.Id}", token);
        using var edited = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{page.Id}",
            Payload(ContentKind.Page, slug, Body("Seconda"), current.GetProperty("rowVersion").GetString()),
            token);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        using (var second = await ReviewAsync(coordinator, page.Id, new { action = "Ready" }, token))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        var summary = await director.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{page.Id}/review", token);
        Assert.False(summary.GetProperty("firstPublication").GetBoolean());

        var changes = summary.GetProperty("sections").EnumerateArray()
            .ToDictionary(section => section.GetProperty("key").GetString()!, section => section.GetProperty("change").GetString());
        Assert.Equal("Changed", changes["hero"]);
        Assert.Equal("Unchanged", changes["closing"]);
    }

    [Fact]
    public async Task ANewsItemIsPublishedByItsDepartmentWithoutAnybodysLeave()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, _) = await PeopleAsync(token);

        var news = await CreateAsync(coordinator, ContentKind.News, Stem(), Body("Notizia"), token);

        using var published = await SendAsync(coordinator, HttpMethod.Post, $"{ContentEndpoints.Pattern}/{news.Id}/publish", new { changelog = (string?)null }, token);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        // And a review of it is not a thing: the division reviews only pages.
        using var ready = await ReviewAsync(coordinator, news.Id, new { action = "Ready" }, token);
        Assert.Equal(HttpStatusCode.BadRequest, ready.StatusCode);
    }

    private async Task<(HttpClient Coordinator, HttpClient Director)> PeopleAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(DirectorVid, "IT-DIR", "director@example.org", cancellationToken);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", "events@example.org", cancellationToken);

        var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, cancellationToken);

        var director = _factory.CreateApiClient();
        await _factory.SignInAsync(director, DirectorVid, cancellationToken);

        return (coordinator, director);
    }

    private static string Stem() => $"r{Guid.NewGuid():N}"[..12];

    private async Task<IReadOnlyList<Notification>> NotificationsAsync(string type, long id, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var rows = await database.Notifications
            .AsNoTracking()
            .Where(row => row.Type == type && row.Vid == vid)
            .ToListAsync(cancellationToken);

        return [.. rows.Where(row => row.DataJson.Contains($"/staff/content/{id}\"", StringComparison.Ordinal))];
    }

    private async Task<(long Id, string? RowVersion)> CreateAsync(
        HttpClient client,
        ContentKind kind,
        string slug,
        JsonNode body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload(kind, slug, body, null), cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (created.GetProperty("id").GetInt64(), created.GetProperty("rowVersion").GetString());
    }

    private object Payload(ContentKind kind, string slug, JsonNode body, string? rowVersion) => new
    {
        kind = kind.ToString(),
        slug,
        ownerDepartment = nameof(Department.ED),
        visibility = nameof(Visibility.Public),
        isTemplate = false,
        title = new Dictionary<string, string> { ["it"] = "Pagina in revisione", ["en"] = "Page under review" },
        summary = (Dictionary<string, string>?)null,
        seo = (Dictionary<string, object>?)null,
        body,
        schemaVersion = 1,
        parentId = kind == ContentKind.Page ? _shelf : (long?)null,
        rowVersion = rowVersion ?? "0001-01-01T00:00:00",
    };

    private static JsonNode Body(string heading) => JsonNode.Parse($$"""
        { "schemaVersion": 1, "sections": [
          { "id": "s_hero", "key": "hero", "layout": "stacked", "blocks": [
            { "id": "b_heading", "type": "heading", "version": 1,
              "props": { "level": 1, "text": { "it": "{{heading}}", "en": "{{heading}}" } } } ] },
          { "id": "s_closing", "key": "closing", "layout": "stacked", "blocks": [
            { "id": "b_closing", "type": "heading", "version": 1,
              "props": { "level": 2, "text": { "it": "Fine", "en": "End" } } } ] } ] }
        """)!;

    private static Task<HttpResponseMessage> ReviewAsync(HttpClient client, long id, object request, CancellationToken cancellationToken) =>
        SendAsync(client, HttpMethod.Post, $"{ContentEndpoints.Pattern}/{id}/review", request, cancellationToken);

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return problem.GetProperty("errors").GetProperty(field).EnumerateArray().First().GetString();
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

    private async Task SeedUserAsync(int vid, string position, string email, CancellationToken cancellationToken)
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
        user.Email = email;
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
