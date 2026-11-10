using System.Globalization;
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
/// <c>cms_contents.draft_from_file</c> (<c>decisions/2026-11-04-la-bozza-che-viene-da-un-file.md</c>): a save says that the
/// draft came from a file, a save that says nothing leaves it, publication takes it away, whoever approves reads it, and the
/// audit never hears of it. VIDs 786001–786002.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContentDraftFromFileTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int DirectorVid = 786001;
    private const int EventsCoordinatorVid = 786002;

    private HubWebApplicationFactory _factory = null!;
    private long _shelf;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;
        _shelf = await TestShelf.SeedAsync(_factory, "test-shelf-from-file", token);

        await SeedUserAsync(DirectorVid, "IT-DIR", "director-file@example.org", token);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", "events-file@example.org", token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ASaveThatSaysNothingDoesNotTakeItAway()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);

        var created = await CreateAsync(coordinator, token);
        Assert.False(FromFile(created));

        var imported = await PutAsync(coordinator, created, row => row["draftFromFile"] = true, autosave: false, token);
        Assert.True(FromFile(imported));
        Assert.True(FromFile(await ReadAsync(coordinator, imported, token)));

        // The case that is easy to get wrong: the autosave and a press on «save draft» never name the field.
        var autosaved = await PutAsync(coordinator, imported, row => row["title"]!["it"] = "Scritta dopo", autosave: true, token);
        Assert.True(FromFile(autosaved));

        var pressed = await PutAsync(coordinator, autosaved, row => row["title"]!["it"] = "Scritta ancora", autosave: false, token);
        Assert.True(FromFile(pressed));

        // Undo says it.
        var undone = await PutAsync(coordinator, pressed, row => row["draftFromFile"] = false, autosave: true, token);
        Assert.False(FromFile(undone));
        Assert.False(FromFile(await ReadAsync(coordinator, undone, token)));
    }

    [Fact]
    public async Task TheAuditNeverHearsOfIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);

        var created = await CreateAsync(coordinator, token);

        // A first save, of nothing: the empty translated columns settle from null to empty, which is a change of the row of its own.
        var settled = await PutAsync(coordinator, created, _ => { }, autosave: false, token);
        var before = await AuditAsync(created, token);
        var storedAt = (await ReadAsync(coordinator, settled, token)).GetProperty("updatedAt").GetDateTime();

        // Alone: not a change of the row. No audit row, and no new «updated at».
        var alone = await PutAsync(coordinator, settled, row => row["draftFromFile"] = true, autosave: false, token);
        Assert.True(FromFile(alone));
        Assert.Equal(
            storedAt,
            (await ReadAsync(coordinator, alone, token)).GetProperty("updatedAt").GetDateTime());
        Assert.Equal(before.Count, (await AuditAsync(created, token)).Count);

        // Beside something else, by a press and by the autosave: the audit tells of the something else only.
        var pressed = await PutAsync(
            coordinator,
            alone,
            row =>
            {
                row["draftFromFile"] = false;
                row["title"]!["it"] = "Con una pressione";
            },
            autosave: false,
            token);
        await PutAsync(
            coordinator,
            pressed,
            row =>
            {
                row["draftFromFile"] = true;
                row["title"]!["it"] = "Da sola";
            },
            autosave: true,
            token);

        var rows = await AuditAsync(created, token);
        Assert.Equal(["updated", "autosaved"], rows.Skip(before.Count).Select(row => row.Action));
        foreach (var row in rows)
        {
            Assert.DoesNotContain("DraftFromFile", row.BeforeJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain("DraftFromFile", row.AfterJson ?? string.Empty, StringComparison.Ordinal);
        }

        Assert.Contains("\"Title\"", rows[^1].AfterJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhoeverApprovesReadsItAndPublicationTakesItAway()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(EventsCoordinatorVid, token);
        using var director = await SignedInAsync(DirectorVid, token);

        var created = await CreateAsync(coordinator, token);
        var imported = await PutAsync(coordinator, created, row => row["draftFromFile"] = true, autosave: false, token);

        using var ready = await SendAsync(coordinator, HttpMethod.Post, Row(imported) + "/review", new { action = "Ready" }, false, token);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        // Another person, in another browser: what the line of the version starts from is on the row.
        Assert.True(FromFile(await ReadAsync(director, imported, token)));

        using var approved = await SendAsync(
            director,
            HttpMethod.Post,
            Row(imported) + "/review",
            new { action = "Approve", changelog = "Da un file", parentId = _shelf, slug = imported.GetProperty("slug").GetString() },
            false,
            token);
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(token));

        var published = await ReadAsync(director, imported, token);
        Assert.False(FromFile(published));
        Assert.NotEqual(JsonValueKind.Null, published.GetProperty("publishedAt").ValueKind);

        // And the next draft is one nobody imported, until somebody does.
        var next = await PutAsync(director, published, row => row["title"]!["it"] = "Dopo l'uscita", autosave: false, token);
        Assert.False(FromFile(next));
    }

    private static bool FromFile(JsonElement row) => row.GetProperty("draftFromFile").GetBoolean();

    private static string Row(JsonElement row) => $"{ContentEndpoints.Pattern}/{row.GetProperty("id").GetInt64()}";

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task<JsonElement> CreateAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var slug = $"file-{Guid.NewGuid():N}"[..20];
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            new
            {
                kind = nameof(ContentKind.Page),
                slug,
                ownerDepartment = nameof(Department.ED),
                visibility = nameof(Visibility.Public),
                isTemplate = false,
                title = new Dictionary<string, string> { ["it"] = "Da un file", ["en"] = "From a file" },
                body = JsonNode.Parse("""
                    { "schemaVersion": 1, "sections": [
                      { "id": "s_one", "layout": "stacked", "blocks": [
                        { "id": "b_one", "type": "heading", "version": 1,
                          "props": { "level": 2, "text": { "it": "Uno", "en": "One" } } } ] } ] }
                    """),
                schemaVersion = 1,
                parentId = _shelf,
                collections = Array.Empty<string>(),
                rowVersion = "0001-01-01T00:00:00",
            },
            false,
            cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(cancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// The row back as it was read, with a change on top and without the field unless the change names it: what a client that
    /// says nothing about it sends.
    /// </summary>
    private static async Task<JsonElement> PutAsync(
        HttpClient client,
        JsonElement row,
        Action<JsonObject> change,
        bool autosave,
        CancellationToken cancellationToken)
    {
        var payload = JsonNode.Parse(row.GetRawText())!.AsObject();
        payload.Remove("draftFromFile");

        // The detail reads an empty column as an empty object, and the editor sends it back as null (`toWriteDto`).
        payload["summary"] = null;
        payload["seo"] = null;
        change(payload);

        using var response = await SendAsync(client, HttpMethod.Put, Row(row), payload, autosave, cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancellationToken));
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static Task<JsonElement> ReadAsync(HttpClient client, JsonElement row, CancellationToken cancellationToken) =>
        client.GetFromJsonAsync<JsonElement>(Row(row), cancellationToken);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload,
        bool autosave,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Content = JsonContent.Create(payload, payload.GetType());
        request.Headers.Add("X-Requested-With", "hub");
        if (autosave)
        {
            request.Headers.Add(HubSaveChangesInterceptor.AutosaveHeader, "1");
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task<List<AuditLogEntry>> AuditAsync(JsonElement row, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = row.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);

        return await database.AuditLog.AsNoTracking()
            .Where(entry => entry.Entity == "cms_contents" && entry.EntityId == key)
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
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
