using System.Net;
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
/// What the public finds of a row comes from its published version, while the row goes on being
/// written (note 2026-10-21-il-pubblico-legge-la-versione): the search index and the live lists of
/// news and documents, each asked after an edit that nobody has published yet. The public page itself
/// is asked in <c>ContentEndToEndTests.PublicReadsOnlyPublishedVersion</c>.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class PublishedVersionReadTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own: the integration tests share one database.
    private const int EventsCoordinatorVid = 770001;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task SearchFindsWhatWasPublishedAndNotTheDraftAfterIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await CoordinatorAsync(token);

        var (published, draft) = (Word(), Word());
        var id = await PublishedNewsAsync(coordinator, title: published, text: published, token);
        await EditAsync(coordinator, id, title: draft, text: draft, token);

        using var anonymous = _factory.CreateApiClient();

        var found = await SearchAsync(anonymous, published, token);
        var hit = Assert.Single(found.GetProperty("results").GetProperty("items").EnumerateArray());
        Assert.Equal($"content:{id}", hit.GetProperty("sourceId").GetString());
        Assert.Equal(published, hit.GetProperty("title").GetString());

        Assert.Equal(0, (await SearchAsync(anonymous, draft, token)).GetProperty("results").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task ALiveListShowsThePublishedTitle()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await CoordinatorAsync(token);

        var (published, draft) = (Word(), Word());
        var id = await PublishedNewsAsync(coordinator, title: published, text: published, token);
        await EditAsync(coordinator, id, title: draft, text: draft, token);

        using var anonymous = _factory.CreateApiClient();
        var list = await anonymous.GetFromJsonAsync<JsonElement>(
            BlockDataUri(CoreBlocks.NewsList, new JsonObject { ["department"] = nameof(Department.ED), ["limit"] = 50 }),
            token);

        var item = list.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("id").GetInt64() == id);
        Assert.Equal(published, item.GetProperty("title").GetProperty("it").GetString());
    }

    [Fact]
    public async Task TheReleaseThatChangesTheRuleWritesTheIndexAgainFromTheVersions()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await CoordinatorAsync(token);

        var (published, before) = (Word(), Word());
        var id = await PublishedNewsAsync(coordinator, title: published, text: published, token);

        // The line as a release before this one left it: the draft's words, and the key not yet written.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.SearchIndex
                .Where(line => line.SourceModule == ProjectionSource.Core && line.SourceId == $"content:{id}")
                .ExecuteUpdateAsync(line => line.SetProperty(row => row.Text, before), token);
            await database.DivisionSettings
                .Where(setting => setting.Key == ContentSeeder.SearchFromVersionsKey)
                .ExecuteDeleteAsync(token);

            await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(token);
        }

        using var anonymous = _factory.CreateApiClient();
        Assert.Equal(0, (await SearchAsync(anonymous, before, token)).GetProperty("results").GetProperty("total").GetInt32());
        Assert.Equal(1, (await SearchAsync(anonymous, published, token)).GetProperty("results").GetProperty("total").GetInt32());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            Assert.True(await database.DivisionSettings.AnyAsync(setting => setting.Key == ContentSeeder.SearchFromVersionsKey, token));
        }
    }

    /// <summary>A word of its own, letters and digits, long enough for the FULLTEXT index.</summary>
    private static string Word() => $"w{Guid.NewGuid():N}"[..14];

    private async Task<HttpClient> CoordinatorAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", cancellationToken);

        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, cancellationToken);
        return client;
    }

    /// <summary>A news item of the events department, published by its coordinator: news needs nobody's leave.</summary>
    private static async Task<long> PublishedNewsAsync(HttpClient client, string title, string text, CancellationToken cancellationToken)
    {
        var slug = $"n{Guid.NewGuid():N}"[..14];
        using var created = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, Payload(slug, title, text, null), cancellationToken);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(cancellationToken));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();

        using var published = await SendAsync(client, HttpMethod.Post, $"{ContentEndpoints.Pattern}/{id}/publish", new { changelog = (string?)null }, cancellationToken);
        Assert.True(published.StatusCode == HttpStatusCode.OK, await published.Content.ReadAsStringAsync(cancellationToken));

        return id;
    }

    /// <summary>The draft written again, and not published.</summary>
    private static async Task EditAsync(HttpClient client, long id, string title, string text, CancellationToken cancellationToken)
    {
        var current = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", cancellationToken);
        using var edited = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(current.GetProperty("slug").GetString()!, title, text, current.GetProperty("rowVersion").GetString()),
            cancellationToken);
        Assert.True(edited.StatusCode == HttpStatusCode.OK, await edited.Content.ReadAsStringAsync(cancellationToken));
    }

    private static Task<JsonElement> SearchAsync(HttpClient client, string word, CancellationToken cancellationToken) =>
        client.GetFromJsonAsync<JsonElement>($"{SearchEndpoints.Pattern}?q={word}&locale=it", cancellationToken);

    private static object Payload(string slug, string title, string text, string? rowVersion) => new
    {
        kind = nameof(ContentKind.News),
        slug,
        ownerDepartment = nameof(Department.ED),
        visibility = nameof(Visibility.Public),
        isTemplate = false,
        title = new Dictionary<string, string> { ["it"] = title, ["en"] = title },
        summary = (Dictionary<string, string>?)null,
        seo = (Dictionary<string, object>?)null,
        body = JsonNode.Parse($$"""
            { "schemaVersion": 1, "sections": [
              { "id": "s_main", "layout": "stacked", "blocks": [
                { "id": "b_text", "type": "heading", "version": 1,
                  "props": { "level": 2, "text": { "it": "{{text}}", "en": "{{text}}" } } } ] } ] }
            """),
        schemaVersion = 1,
        rowVersion = rowVersion ?? "0001-01-01T00:00:00",
    };

    /// <summary>A live block, asked the way the browser asks: the properties base64url encoded.</summary>
    private static Uri BlockDataUri(string type, JsonNode props)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Uri($"/api/blocks/data/{type}?props={encoded}", UriKind.Relative);
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
