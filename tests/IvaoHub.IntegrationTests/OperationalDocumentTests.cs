using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The operational document over the wire (G14, note
/// 2026-09-10-il-documento-operativo-come-va-ivao-aero): the six facts a SOP or a LoA carries
/// beside its body, checked against the division's own airspace when written, and what the public
/// page is told about them once published — including the way on when the document was replaced.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class OperationalDocumentTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 660001;
    private const int AtcCoordinatorVid = 660002;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheAirspaceIsListedWithNamesToWhoeverIsSignedIn()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);
        await SeedSnapshotAsync(token);

        using var client = _factory.CreateApiClient();

        // A visitor has no document to write, and the list is not theirs to read: they are sent
        // to sign in, which is what a challenge is to a browser without the hub's own header.
        using var anonymous = await client.GetAsync(new Uri(AirspaceEndpoints.Pattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Found, anonymous.StatusCode);

        await _factory.SignInAsync(client, SuperadminVid, token);

        var listing = await client.GetFromJsonAsync<JsonElement>(AirspaceEndpoints.Pattern, token);

        var airports = listing.GetProperty("airports").EnumerateArray().ToList();
        var centers = listing.GetProperty("centers").EnumerateArray().ToList();

        Assert.Contains(airports, airport => airport.GetProperty("code").GetString() == "LIRF"
            && airport.GetProperty("name").GetString() == "Roma Fiumicino");
        Assert.Contains(centers, center => center.GetProperty("code").GetString() == "LIRR"
            && center.GetProperty("name").GetString() == "Roma");
    }

    [Fact]
    public async Task ADocumentIsHeldToTheAirspaceAndAPageMayNotCarryItsFields()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);
        await SeedSnapshotAsync(token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        // An airport the division does not have, a FIR it does not have, and a position written
        // the way nobody on the network writes one: three typing mistakes, refused where they sit.
        using var refused = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(
                $"sop-{Guid.NewGuid():N}"[..20],
                icao: "EGLL",
                fir: "EGTT",
                primaryPosition: "lirf tower"),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var errors = (await refused.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");
        Assert.Equal("errors.content.icaoUnknown", errors.GetProperty("icao")[0].GetString());
        Assert.Equal("errors.content.firUnknown", errors.GetProperty("fir")[0].GetString());
        Assert.Equal("errors.content.positionInvalid", errors.GetProperty("primaryPosition")[0].GetString());

        // The same facts on a page are not a mistake of spelling but of kind.
        using var page = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload($"pg-{Guid.NewGuid():N}"[..20], kind: ContentKind.Page, icao: "LIRF", documentType: "Sop"),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);

        var pageErrors = (await page.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");
        Assert.Equal("errors.content.notADocument", pageErrors.GetProperty("icao")[0].GetString());
        Assert.Equal("errors.content.notADocument", pageErrors.GetProperty("documentType")[0].GetString());

        // And the real thing goes through with everything it said, the footer on by default.
        using var created = await SendAsync(
            client,
            HttpMethod.Post,
            ContentEndpoints.Pattern,
            Payload(
                $"sop-{Guid.NewGuid():N}"[..20],
                icao: "LIRF",
                fir: "LIRR",
                primaryPosition: "LIRF_TWR",
                secondaryPosition: "LIRR_CTR",
                effectiveOn: "2026-10-01",
                reviewOn: "2027-10-01"),
            token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var document = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("Sop", document.GetProperty("documentType").GetString());
        Assert.Equal("LIRF", document.GetProperty("icao").GetString());
        Assert.Equal("LIRR", document.GetProperty("fir").GetString());
        Assert.Equal("LIRF_TWR", document.GetProperty("primaryPosition").GetString());
        Assert.StartsWith("2026-10-01", document.GetProperty("effectiveOn").GetString(), StringComparison.Ordinal);
        Assert.True(document.GetProperty("showFooter").GetBoolean());
    }

    [Fact]
    public async Task ThePublicPageSaysWhoPublishedWhichCycleAndWhereToGoNext()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);
        await SeedSnapshotAsync(token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var oldSlug = $"loa-{Guid.NewGuid():N}"[..20];
        var newSlug = $"loa-{Guid.NewGuid():N}"[..20];

        var old = await CreateAsync(client, Payload(oldSlug, documentType: "Loa", icao: "LIRF", fir: "LIRR"), token);
        var successor = await CreateAsync(client, Payload(newSlug, documentType: "Loa", icao: "LIRF", fir: "LIRR"), token);

        // A cycle that is not four digits is refused by publication, on the field of the dialog.
        using var badCycle = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{old.Id}/publish",
            new { changelog = (string?)null, airac = "26-09" },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, badCycle.StatusCode);
        Assert.Equal(
            "errors.content.airacInvalid",
            (await badCycle.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors").GetProperty("airac")[0].GetString());

        await PublishAsync(client, old.Id, "2609", token);
        await PublishAsync(client, successor.Id, "2610", token);

        var read = await client.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Document)}/{oldSlug}",
            token);

        // The footer: the version, the cycle and a name — never the VID.
        Assert.Equal("2609", read.GetProperty("airac").GetString());
        Assert.Equal("Test User", read.GetProperty("publishedByName").GetString());
        Assert.Equal("Loa", read.GetProperty("documentType").GetString());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("supersededBySlug").ValueKind);

        // A successor without a retirement date is half a notice, and refused.
        using var half = await SendAsync(
            client,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{old.Id}",
            Payload(oldSlug, documentType: "Loa", icao: "LIRF", fir: "LIRR", supersededById: successor.Id, rowVersion: old.RowVersion),
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
                documentType: "Loa",
                icao: "LIRF",
                fir: "LIRR",
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
        Assert.Equal("LoA di prova", after.GetProperty("supersededByTitle").GetProperty("it").GetString());
    }

    [Fact]
    public async Task ADocumentPastItsReviewDateIsPointedOutToItsDepartmentOnce()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "aoc@example.org");
        await SeedSnapshotAsync(token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        // One stem for both slugs, so the list can be asked about these two rows and nobody else's:
        // every class of this assembly writes into the same database.
        var stem = $"rv{Guid.NewGuid():N}"[..14];
        var overdue = await CreateAsync(client, Payload($"{stem}-overdue", reviewOn: "2026-01-01"), token);
        var fresh = await CreateAsync(client, Payload($"{stem}-fresh", reviewOn: "2099-01-01"), token);

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

        return [.. all.Where(row => row.DataJson.Contains($"/documents/{id}\"", StringComparison.Ordinal))];
    }

    private static object Payload(
        string slug,
        ContentKind kind = ContentKind.Document,
        string? documentType = "Sop",
        string? icao = null,
        string? fir = null,
        string? primaryPosition = null,
        string? secondaryPosition = null,
        string? effectiveOn = null,
        string? reviewOn = null,
        string? retiredAt = null,
        long? supersededById = null,
        string? rowVersion = null) => new
        {
            kind = kind.ToString(),
            slug,
            ownerDepartment = nameof(Department.AOD),
            visibility = nameof(Visibility.Public),
            isTemplate = false,
            title = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["it"] = documentType == "Loa" ? "LoA di prova" : "SOP di prova",
                ["en"] = documentType == "Loa" ? "Test LoA" : "Test SOP",
            },
            summary = (Dictionary<string, string>?)null,
            seo = (Dictionary<string, object>?)null,
            body = Body(),
            schemaVersion = 1,
            documentType = kind == ContentKind.Document || documentType is not null ? documentType : null,
            icao,
            fir,
            primaryPosition,
            secondaryPosition,
            effectiveOn,
            reviewOn,
            retiredAt,
            supersededById,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };

    private static JsonNode Body() => JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "id": "s_main",
              "layout": "stacked",
              "blocks": [
                { "id": "b_heading", "type": "heading", "version": 1,
                  "props": { "level": 1, "text": { "it": "Procedure", "en": "Procedures" } } }
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

    private static async Task PublishAsync(HttpClient client, long id, string airac, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/{id}/publish",
            new { changelog = "First edition", airac },
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
        request.Content = JsonContent.Create(payload);
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

    /// <summary>
    /// The snapshot a division would have taken from the network, written straight in: one FIR
    /// and one airport are all the airspace this test needs to tell a real code from a made up one.
    /// </summary>
    private async Task SeedSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (!await database.IvaoCenters.AnyAsync(center => center.Id == "LIRR", cancellationToken))
        {
            database.IvaoCenters.Add(new IvaoCenter { Id = "LIRR", Name = "Roma", CountryId = "IT", SyncedAt = clock.UtcNow });
        }

        var airport = await database.IvaoAirports.FirstOrDefaultAsync(row => row.Icao == "LIRF", cancellationToken);
        if (airport is null)
        {
            airport = new IvaoAirport { Icao = "LIRF", CountryId = "IT", SyncedAt = clock.UtcNow };
            database.IvaoAirports.Add(airport);
        }

        // Another test seeds the same airport with its code for a name; the listing test wants the
        // name, so it is set either way.
        airport.Name = "Roma Fiumicino";

        await database.SaveChangesAsync(cancellationToken);
        scope.ServiceProvider.GetRequiredService<IFirDirectory>().Invalidate();
    }
}
