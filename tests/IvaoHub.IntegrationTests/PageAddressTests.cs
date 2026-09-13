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
/// The address of a page (G18, note 2026-09-13-contenuti-centralizzati §3.7): a page sits under a
/// page, up to three levels; the first segment is not one the application answers for; the top of the
/// site is for whoever holds <c>Content.Approve</c>; and a published page that moves sends a visitor
/// arriving at its old address to the new one, with the pages under it.
/// <para>Every slug carries a stem of the run: the suite shares one database, and an address is the
/// one thing two classes could both want.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PageAddressTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 670001;
    private const int EventsCoordinatorVid = 670002;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ThreeLevelsAreAnAddressAndAFourthIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var stem = Stem();
        var training = await CreateAsync(client, $"{stem}-training", parentId: null, token);
        var guide = await CreateAsync(client, "guide", training.Id, token);
        var start = await CreateAsync(client, "start", guide.Id, token);

        Assert.Equal($"{stem}-training/guide/start", start.Path);
        await PublishAsync(client, start.Id, token);

        using var anonymous = _factory.CreateApiClient();
        var read = await anonymous.GetFromJsonAsync<JsonElement>(PageUri($"{stem}-training/guide/start"), token);
        Assert.Equal(start.Id, read.GetProperty("page").GetProperty("id").GetInt64());
        Assert.Equal($"{stem}-training/guide/start", read.GetProperty("page").GetProperty("path").GetString());

        // A fourth level is refused where it was asked for, and so is a page moved under itself.
        using var fourth = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload("deeper", start.Id), token);
        Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
        Assert.Equal("errors.content.address.tooDeep", await FirstErrorAsync(fourth, "parentId", token));

        // Two pages may share a slug under two parents — `guide` exists under training, and is free
        // at the top — but not under one: the second is refused, and the form is told the free one.
        using var twin = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload("guide", training.Id), token);
        Assert.Equal(HttpStatusCode.BadRequest, twin.StatusCode);
        Assert.Equal("errors.content.address.taken", await FirstErrorAsync(twin, "slug", token));

        var described = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/address?kind=Page&department=WD&slug=guide&parentId={training.Id}",
            token);
        Assert.Equal("Taken", described.GetProperty("state").GetString());
        Assert.Equal("guide-2", described.GetProperty("suggestion").GetString());
        Assert.Equal($"/{stem}-training/guide", described.GetProperty("path").GetString());
    }

    [Fact]
    public async Task AnAddressTheApplicationAnswersForIsNotAPage()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        using var news = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload("news", parentId: null), token);
        Assert.Equal(HttpStatusCode.BadRequest, news.StatusCode);
        Assert.Equal("errors.content.address.reserved", await FirstErrorAsync(news, "slug", token));

        // Under a page it is just a word: `/<page>/news` belongs to nobody else.
        var stem = Stem();
        var parent = await CreateAsync(client, $"{stem}-events", parentId: null, token);
        var child = await CreateAsync(client, "news", parent.Id, token);
        Assert.Equal($"{stem}-events/news", child.Path);
    }

    [Fact]
    public async Task TheTopOfTheSiteIsNotACoordinatorsToWriteIn()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);
        await SeedUserAsync(EventsCoordinatorVid, token, position: "IT-EC");

        using var superadmin = _factory.CreateApiClient();
        await _factory.SignInAsync(superadmin, SuperadminVid, token);
        var shelf = await CreateAsync(superadmin, $"{Stem()}-shelf", parentId: null, token);

        using var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);

        // At the top: refused on the field that chose it.
        using var top = await SendAsync(
            coordinator,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload($"{Stem()}-mine", parentId: null, department: Department.ED),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, top.StatusCode);
        Assert.Equal("errors.content.address.topLevel", await FirstErrorAsync(top, "parentId", token));

        // Under a page of the site, whoever's page it is: created.
        using var under = await SendAsync(
            coordinator,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload("mine", shelf.Id, department: Department.ED),
            token);
        Assert.Equal(HttpStatusCode.Created, under.StatusCode);
    }

    [Fact]
    public async Task APublishedPageThatMovesSendsItsOldAddressesOnWithThePagesUnderIt()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var stem = Stem();
        var training = await CreateAsync(client, $"{stem}-training", parentId: null, token);
        var pilots = await CreateAsync(client, $"{stem}-pilots", parentId: null, token);
        var guide = await CreateAsync(client, "guide", training.Id, token);
        var start = await CreateAsync(client, "start", guide.Id, token);
        await PublishAsync(client, guide.Id, token);
        await PublishAsync(client, start.Id, token);

        // Moved under the pilots, with its child.
        var current = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{guide.Id}", token);
        using var moved = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{guide.Id}",
            Payload("guide", pilots.Id, rowVersion: current.GetProperty("rowVersion").GetString()),
            token);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        using var anonymous = _factory.CreateApiClient();

        var now = await anonymous.GetFromJsonAsync<JsonElement>(PageUri($"{stem}-pilots/guide/start"), token);
        Assert.Equal(start.Id, now.GetProperty("page").GetProperty("id").GetInt64());

        var before = await anonymous.GetFromJsonAsync<JsonElement>(PageUri($"{stem}-training/guide/start"), token);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("page").ValueKind);
        Assert.Equal($"/{stem}-pilots/guide/start", before.GetProperty("movedTo").GetString());

        var parentBefore = await anonymous.GetFromJsonAsync<JsonElement>(PageUri($"{stem}-training/guide"), token);
        Assert.Equal($"/{stem}-pilots/guide", parentBefore.GetProperty("movedTo").GetString());

        // An address nobody ever had is a not found, not a guess.
        using var never = await anonymous.GetAsync(PageUri($"{stem}-nowhere/guide"), token);
        Assert.Equal(HttpStatusCode.NotFound, never.StatusCode);
    }

    private static string Stem() => $"a{Guid.NewGuid():N}"[..12];

    private static Uri PageUri(string path) =>
        new($"{ContentEndpoints.Pattern}/public/page?path={Uri.EscapeDataString(path)}", UriKind.Relative);

    private static async Task<(long Id, string Path)> CreateAsync(
        HttpClient client,
        string slug,
        long? parentId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload(slug, parentId), cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (created.GetProperty("id").GetInt64(), created.GetProperty("path").GetString()!);
    }

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{id}/publish",
            new { changelog = (string?)null },
            cancellationToken);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static object Payload(
        string slug,
        long? parentId,
        Department department = Department.WD,
        string? rowVersion = null) => new
        {
            kind = nameof(ContentKind.Page),
            slug,
            ownerDepartment = department.ToString(),
            visibility = nameof(Visibility.Public),
            isTemplate = false,
            title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Pagina", ["en"] = "Page" },
            summary = (Dictionary<string, string>?)null,
            seo = (Dictionary<string, object>?)null,
            body = JsonNode.Parse("""
                { "schemaVersion": 1, "sections": [ { "id": "s_main", "layout": "stacked", "blocks": [
                  { "id": "b_heading", "type": "heading", "version": 1,
                    "props": { "level": 1, "text": { "it": "Pagina", "en": "Page" } } } ] } ] }
                """),
            schemaVersion = 1,
            parentId,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };

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

    private async Task SeedUserAsync(int vid, CancellationToken cancellationToken, string? position = null)
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
        user.IsSuperadmin = position is null;
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
