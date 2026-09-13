using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// "What waits for me" on the staff dashboard (D3, note 2026-09-13-le-dashboard-a-tutto-schermo §3.5):
/// one block, four answers, each given for whoever asks. The rows are read past the query filter —
/// a draft is invisible to it — so what keeps one person's work off another's dashboard is the
/// narrowing by permission in <see cref="MyWorkProvider"/>, and that is what these tests hold down.
/// <para>⚠️ The database is shared with every other class, so nothing here counts rows: each test
/// looks for the row it wrote, by its address, and for its absence where it must not be. The rows
/// that answer oldest first are written with a date long past, so a queue other tests left behind
/// cannot push them out of the window.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MyWorkTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own: the positions are added to whatever a VID already holds, and a VID shared
    // with another class would carry that class's director into this one.
    private const int DirectorVid = 770001;
    private const int EventsCoordinatorVid = 770002;
    private const int FlightOpsAssistantVid = 770003;

    private static readonly DateTime LongAgo = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task APageWaitingForApprovalIsShownOnlyToWhoeverMayApproveIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (director, coordinator, _) = await PeopleAsync(token);

        var id = await SeedContentAsync(ContentKind.Page, Department.ED, content =>
        {
            content.Status = PublishStatus.Ready;
            content.ReadyAt = LongAgo;
            content.ReadyBy = EventsCoordinatorVid;
        }, token);

        Assert.Contains(Url(id), await UrlsAsync(director, MyWorkProvider.Approvals, token));

        // Whoever marked it ready does not approve it: pages go through somebody else.
        Assert.DoesNotContain(Url(id), await UrlsAsync(coordinator, MyWorkProvider.Approvals, token));
    }

    [Fact]
    public async Task AContactMessageIsShownToItsDepartmentAndNotToAnother()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, coordinator, flightOps) = await PeopleAsync(token);

        long id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var message = new ContactMessage
            {
                OwnerDepartment = Department.ED,
                Subject = $"Una domanda {Guid.NewGuid():N}",
                Body = "Quando è il prossimo evento?",
            };

            database.ContactMessages.Add(message);
            await database.SaveChangesAsync(token);
            id = message.Id;
        }

        var address = $"/staff/ed/contacts/{id}";
        Assert.Contains(address, await UrlsAsync(coordinator, MyWorkProvider.Contacts, token));
        Assert.DoesNotContain(address, await UrlsAsync(flightOps, MyWorkProvider.Contacts, token));
    }

    [Fact]
    public async Task ADocumentDueForReviewIsShownToWhoeverMayEditIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, coordinator, flightOps) = await PeopleAsync(token);

        var id = await SeedContentAsync(ContentKind.Document, Department.ED, content =>
        {
            content.Status = PublishStatus.Published;
            content.PublishedAt = LongAgo;
            content.ReviewOn = LongAgo;
        }, token);

        Assert.Contains(Url(id), await UrlsAsync(coordinator, MyWorkProvider.Reviews, token));
        Assert.DoesNotContain(Url(id), await UrlsAsync(flightOps, MyWorkProvider.Reviews, token));
    }

    [Fact]
    public async Task ADraftIsShownToWhoeverWroteItAndToNobodyElse()
    {
        var token = TestContext.Current.CancellationToken;
        var (director, coordinator, _) = await PeopleAsync(token);

        // Written through the API, so the author is the one the interceptor records and not one this
        // test claims.
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ContentEndpoints.Pattern, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                kind = nameof(ContentKind.News),
                slug = $"w{Guid.NewGuid():N}"[..14],
                ownerDepartment = nameof(Department.ED),
                visibility = nameof(Visibility.Public),
                isTemplate = false,
                title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Bozza", ["en"] = "Draft" },
                summary = (Dictionary<string, string>?)null,
                seo = (Dictionary<string, object>?)null,
                body = JsonNode.Parse("""{ "schemaVersion": 1, "sections": [] }"""),
                schemaVersion = 1,
                rowVersion = "0001-01-01T00:00:00",
            }),
        };
        request.Headers.Add("X-Requested-With", "hub");

        using var created = await coordinator.SendAsync(request, token);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(token));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        Assert.Contains(Url(id), await UrlsAsync(coordinator, MyWorkProvider.Drafts, token));

        // The director may read every draft of the division, and still this one is not theirs.
        Assert.DoesNotContain(Url(id), await UrlsAsync(director, MyWorkProvider.Drafts, token));
    }

    [Fact]
    public async Task AVisitorIsAnsweredWithNothing()
    {
        var token = TestContext.Current.CancellationToken;
        using var anonymous = _factory.CreateApiClient();

        foreach (var what in MyWorkProvider.Kinds)
        {
            Assert.Empty(await UrlsAsync(anonymous, what, token));
        }
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static string Url(long id) => $"/staff/content/{id}";

    private static async Task<List<string>> UrlsAsync(HttpClient client, string what, CancellationToken cancellationToken)
    {
        var props = new JsonObject { ["what"] = what, ["limit"] = DataBlockScope.MaxItems };
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var answer = await client.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/blocks/data/{CoreBlocks.MyWork}?props={encoded}", UriKind.Relative),
            cancellationToken);

        return [.. answer.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("url").GetString()!)];
    }

    private async Task<(HttpClient Director, HttpClient Coordinator, HttpClient FlightOps)> PeopleAsync(
        CancellationToken cancellationToken)
    {
        await SeedUserAsync(DirectorVid, "IT-DIR", cancellationToken);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", cancellationToken);
        await SeedUserAsync(FlightOpsAssistantVid, "IT-FOA1", cancellationToken);

        var director = _factory.CreateApiClient();
        await _factory.SignInAsync(director, DirectorVid, cancellationToken);

        var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, cancellationToken);

        var flightOps = _factory.CreateApiClient();
        await _factory.SignInAsync(flightOps, FlightOpsAssistantVid, cancellationToken);

        return (director, coordinator, flightOps);
    }

    private async Task<long> SeedContentAsync(
        ContentKind kind,
        Department department,
        Action<ContentEntry> shape,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var content = new ContentEntry
        {
            Kind = kind,
            Slug = $"w{Guid.NewGuid():N}"[..14],
            OwnerDepartment = department,
            Visibility = Visibility.Staff,
            Status = PublishStatus.Draft,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Da fare"),
                new KeyValuePair<string, string>("en", "To do"),
            ]),
            BodyJson = """{ "schemaVersion": 1, "sections": [] }""",
        };

        shape(content);
        database.Contents.Add(content);
        await database.SaveChangesAsync(cancellationToken);

        return content.Id;
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
