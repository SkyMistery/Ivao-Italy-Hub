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
/// The life of a document over the wire (G14): when it comes into force, when it is to be reviewed,
/// when it stopped being in force and what replaced it — and what the public page is told about
/// each. Until 13 September 2026 this was the operational document, with a type, two positions, an
/// ICAO, a FIR and an AIRAC cycle besides; that half left with vIPI (note
/// 2026-09-13-staccarsi-da-vipi), and a document of the hub is now a document of any kind.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class DocumentLifeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 660001;
    private const int SpecialOpsCoordinatorVid = 660002;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheDatesOfADocumentBelongToADocumentAndNothingOfAControllerIsLeft()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        // A page with a review date would be a page the reminder job writes to: refused where it sits.
        using var page = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload($"pg-{Guid.NewGuid():N}"[..20], kind: ContentKind.Page, reviewOn: "2027-10-01"),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        var pageErrors = (await page.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");
        Assert.Equal("errors.content.notADocument", pageErrors.GetProperty("reviewOn")[0].GetString());

        // A document goes through with its dates and the footer on by default. A client that still
        // sends the fields of the operational document is not refused -- an unknown member of the
        // JSON is ignored, as everywhere -- and nothing of them comes back.
        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(
                $"doc-{Guid.NewGuid():N}"[..20],
                effectiveOn: "2026-10-01",
                reviewOn: "2027-10-01",
                retired: new { documentType = "Sop", icao = "LIRF", fir = "LIRR", primaryPosition = "LIRF_TWR" }),
            token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var document = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.StartsWith("2026-10-01", document.GetProperty("effectiveOn").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("2027-10-01", document.GetProperty("reviewOn").GetString(), StringComparison.Ordinal);
        Assert.True(document.GetProperty("showFooter").GetBoolean());

        foreach (var gone in new[] { "documentType", "icao", "fir", "primaryPosition", "secondaryPosition" })
        {
            Assert.False(document.TryGetProperty(gone, out _), $"the detail still carries '{gone}'");
        }
    }

    [Fact]
    public async Task ThePublicPageSaysWhoPublishedAndWhereToGoNext()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var oldSlug = $"reg-{Guid.NewGuid():N}"[..20];
        var newSlug = $"reg-{Guid.NewGuid():N}"[..20];

        var old = await CreateAsync(client, Payload(oldSlug), token);
        var successor = await CreateAsync(client, Payload(newSlug), token);

        await PublishAsync(client, old.Id, token);
        await PublishAsync(client, successor.Id, token);

        var read = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Document)}/{oldSlug}",
            token);

        // The footer: the version and a name -- never the VID, and no cycle any more.
        Assert.Equal("Test User", read.GetProperty("publishedByName").GetString());
        Assert.False(read.TryGetProperty("airac", out _));
        Assert.Equal(JsonValueKind.Null, read.GetProperty("supersededBySlug").ValueKind);

        // A successor without a retirement date is half a notice, and refused.
        using var half = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{old.Id}",
            Payload(oldSlug, supersededById: successor.Id, rowVersion: old.RowVersion),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
        Assert.Equal(
            "errors.content.successorWithoutRetirement",
            (await half.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors").GetProperty("retiredAt")[0].GetString());

        // Retired with a successor: the row stays published, and the reader is sent on.
        var reloaded = await client.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{old.Id}", token);

        using var retired = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{old.Id}",
            Payload(
                oldSlug,
                supersededById: successor.Id,
                retiredAt: "2026-11-01T00:00:00",
                rowVersion: reloaded.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Document)}/{oldSlug}",
            token);

        Assert.StartsWith("2026-11-01", after.GetProperty("retiredAt").GetString(), StringComparison.Ordinal);
        Assert.Equal(newSlug, after.GetProperty("supersededBySlug").GetString());
        Assert.Equal("Regolamento di prova", after.GetProperty("supersededByTitle").GetProperty("it").GetString());
    }

    [Fact]
    public async Task ADocumentPastItsReviewDateIsPointedOutToItsDepartmentOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);
        // ⚠️ Special operations, and not ATC: `ContactsAndNotificationsTests` asserts the exact set
        // of people who hear about a message to the ATC department, and every class of this
        // assembly writes into the same database — a second ATC coordinator here was a red CI there.
        await SeedUserAsync(SpecialOpsCoordinatorVid, token, position: "IT-SOC", email: "soc@example.org");

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        // One stem for both slugs, so the list can be asked about these two rows and nobody else's.
        var stem = $"rv{Guid.NewGuid():N}"[..14];
        var overdue = await CreateAsync(
            client,
            Payload($"{stem}-overdue", department: Department.SOD, reviewOn: "2026-01-01"),
            token);
        var fresh = await CreateAsync(
            client,
            Payload($"{stem}-fresh", department: Department.SOD, reviewOn: "2099-01-01"),
            token);

        // The job, run as the scheduler would run it: nobody signed in, the whole table in view.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var told = await scope.ServiceProvider.GetRequiredService<DocumentReviewJob>().RunAsync(token);
            Assert.True(told >= 1);
        }

        // One row per member of the department's staff who can be reached — and other classes of
        // this assembly seed staff of the same department, so what is counted is "some, and then
        // no more", never "exactly one".
        var reminded = (await RemindersForAsync(overdue.Id, token)).Count;
        Assert.True(reminded >= 1);
        Assert.Empty(await RemindersForAsync(fresh.Id, token));

        // Told once: the row remembers, and the next night says nothing about it.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var row = await database.Contents.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == overdue.Id, token);
            Assert.NotNull(row.ReviewNotifiedAt);

            await scope.ServiceProvider.GetRequiredService<DocumentReviewJob>().RunAsync(token);
        }

        Assert.Equal(reminded, (await RemindersForAsync(overdue.Id, token)).Count);

        // And the list can be asked the same question, for the screen of the department.
        var due = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}?filter[{ContentEndpoints.ReviewDueFilter}]=true&filter[kind]=Document&q={stem}",
            token);
        var ids = due.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64()).ToList();
        Assert.Contains(overdue.Id, ids);
        Assert.DoesNotContain(fresh.Id, ids);
    }

    /// <summary>The reminders queued about one document, found by the address the mail points at.</summary>
    private async Task<IReadOnlyList<Notification>> RemindersForAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var all = await database.Notifications
            .AsNoTracking()
            .Where(row => row.Type == NotificationTypes.DocumentReviewDue)
            .ToListAsync(cancellationToken);

        return [.. all.Where(row => row.DataJson.Contains($"/staff/content/{id}\"", StringComparison.Ordinal))];
    }

    /// <summary>
    /// A write of a content row. <paramref name="retired"/> merges in members the API no longer
    /// has, as an old client would still send them.
    /// </summary>
    private static JsonObject Payload(
        string slug,
        ContentKind kind = ContentKind.Document,
        Department department = Department.AOD,
        string? effectiveOn = null,
        string? reviewOn = null,
        string? retiredAt = null,
        long? supersededById = null,
        string? rowVersion = null,
        object? retired = null)
    {
        var payload = JsonSerializer.SerializeToNode(new
        {
            kind = kind.ToString(),
            slug,
            ownerDepartment = department.ToString(),
            visibility = nameof(Visibility.Public),
            isTemplate = false,
            title = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["it"] = "Regolamento di prova",
                ["en"] = "Test regulation",
            },
            summary = (Dictionary<string, string>?)null,
            seo = (Dictionary<string, object>?)null,
            body = Body(),
            schemaVersion = 1,
            effectiveOn,
            reviewOn,
            retiredAt,
            supersededById,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        })!.AsObject();

        if (retired is not null)
        {
            foreach (var (name, value) in JsonSerializer.SerializeToNode(retired)!.AsObject().ToList())
            {
                payload[name] = value?.DeepClone();
            }
        }

        return payload;
    }

    private static JsonNode Body() => JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "id": "s_main",
              "layout": "stacked",
              "blocks": [
                { "id": "b_heading", "type": "heading", "version": 1,
                  "props": { "level": 1, "text": { "it": "Regole", "en": "Rules" } } }
              ]
            }
          ]
        }
        """)!;

    private static async Task<(long Id, string? RowVersion)> CreateAsync(
        HttpClient client,
        object payload,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, ContentEndpoints.Pattern, payload, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (created.GetProperty("id").GetInt64(), created.GetProperty("rowVersion").GetString());
    }

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{id}/publish",
            new { changelog = "First edition" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private async Task SeedUserAsync(
        int vid,
        CancellationToken cancellationToken,
        string? position = null,
        string? email = null)
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
        // Nobody with a position is the superadmin: the one who publishes here is, the staff member
        // who is told about the review is not.
        user.IsSuperadmin = position is null;
        user.IsStaff = position is not null;
        user.Email = email;
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
