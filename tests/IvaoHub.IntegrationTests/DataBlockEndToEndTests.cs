using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The six data blocks of the core, against a real database and the real container (design M1
/// section 1.2, group Data).
/// <para>What is asserted here is the half a schema cannot: that the registry and the container
/// agree on which types have a provider, that <c>networkStats</c> is never captured however a body
/// asks, and that a provider answers a visitor and a member of the staff differently on the very
/// same page — which is the whole reason a provider reads through the query filter instead of
/// filtering by hand.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class DataBlockEndToEndTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 640001;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task EveryDataBlockTypeHasAProvider()
    {
        // The two halves of a data block are declared in two places on purpose — a descriptor in
        // the registry, a service in the container — and nothing but this compares them. A block
        // with no provider is a page that answers 404 to one of its own blocks.
        await using var scope = _factory.Services.CreateAsyncScope();

        var registry = scope.ServiceProvider.GetRequiredService<BlockRegistry>();
        var providers = scope.ServiceProvider.GetRequiredService<DataBlockProviders>();

        var data = registry.All.Where(descriptor => descriptor.Kind == BlockKind.Data).ToList();

        // Six of the core plus the one M0 built, and the count is written out so that a block lost
        // in a merge is something CI says out loud rather than a gallery that is quietly shorter.
        Assert.Equal(7, data.Count);

        foreach (var descriptor in data)
        {
            Assert.True(
                providers.For(descriptor) is not null,
                $"The block '{descriptor.Type}' is declared as data and nothing answers for it.");
        }

        // And the other way round, which is the half that rots silently: a provider registered for
        // a key no descriptor names is a service nobody will ever call.
        foreach (var provider in scope.ServiceProvider.GetServices<IDataBlockProvider>())
        {
            Assert.True(
                registry.Find(provider.Key) is { Kind: BlockKind.Data },
                $"A provider answers for '{provider.Key}', which no data block declares.");
        }
    }

    [Fact]
    public async Task PublishFreezesNewsListButNotNetworkStats()
    {
        // The acceptance of the phase, run rather than described. One page, two data blocks, both
        // asking to be captured: one of them is allowed to be and the other never is, and the rule
        // is in the type (`AlwaysLive`) rather than in a check somebody has to remember.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var category = $"news-{Guid.NewGuid():N}"[..14];
        await SeedNewsAsync(Department.ED, category, "one", Visibility.Public, token);
        await SeedNewsAsync(Department.ED, category, "two", Visibility.Public, token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var slug = $"figures-{Guid.NewGuid():N}"[..20];
        var id = await CreateAsync(client, slug, Body(category, mode: "frozen"), token);
        await PublishAsync(client, id, token);

        using var anonymous = _factory.CreateApiClient();
        var published = await anonymous.GetFromJsonAsync<JsonElement>(PublicUri(slug), token);

        var news = BlockOf(published, CoreBlocks.NewsList);
        Assert.Equal(2, news.GetProperty("frozen").GetProperty("items").GetArrayLength());

        // Same page, same `renderMode`, and nothing captured: an expired picture of who is online
        // passed off as the present one is the one thing a capture must never be (plan 9.3).
        var network = BlockOf(published, CoreBlocks.NetworkStats);
        Assert.Equal(JsonValueKind.Null, network.GetProperty("frozen").ValueKind);
    }

    [Fact]
    public async Task NetworkStatsIsNeverFrozenOnPublish()
    {
        // The same rule asked of the service that owns it, with no HTTP in the way: whatever a body
        // says, a block the registry marks always live comes back with nothing captured.
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var blocks = scope.ServiceProvider.GetRequiredService<BlockRegistry>();

        Assert.True(blocks.Find(CoreBlocks.NetworkStats)!.AlwaysLive);
        Assert.False(blocks.Find(CoreBlocks.NewsList)!.AlwaysLive);

        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<ContentPublishService>();

        var slug = $"always-{Guid.NewGuid():N}"[..20];
        var content = new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = slug,
            OwnerDepartment = Department.WD,
            Visibility = Visibility.Public,
            Status = PublishStatus.Draft,
            Title = Both("Sempre in diretta", "Always live"),
            BodyJson = Body(category: null, mode: "frozen").ToJsonString(),
        };

        database.Contents.Add(content);
        await database.SaveChangesAsync(token);

        Assert.Null(await publish.PublishAsync(content, changelog: null, token));

        var version = await publish.PublishedVersionAsync(content, token);
        var body = JsonNode.Parse(version!.BodyJson)!;
        var walker = new BlockDocumentWalker(["it", "en"]);

        foreach (var block in walker.EnumerateBlocks(body))
        {
            if (block.Type == CoreBlocks.NetworkStats)
            {
                Assert.Null(block.Node["frozen"]);
            }
        }
    }

    [Fact]
    public async Task DataBlockRespectsVisibility()
    {
        // One page, two readers. The provider does not know who is asking and does not filter by
        // hand: it reads the table, and the global query filter is what makes the two answers
        // different (design M0 section 3.5). Filtering by hand is how a staff row ends up on a
        // public page, and this is the test that would still be green if somebody wrote it.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var category = $"mixed-{Guid.NewGuid():N}"[..14];
        await SeedNewsAsync(Department.ED, category, "open", Visibility.Public, token);
        await SeedNewsAsync(Department.ED, category, "internal", Visibility.Staff, token);

        using var anonymous = _factory.CreateApiClient();
        var visitor = await anonymous.GetFromJsonAsync<JsonElement>(NewsListUri(category), token);
        var visible = Titles(visitor);

        Assert.Single(visible);
        Assert.Contains("open", visible[0], StringComparison.Ordinal);

        using var staff = _factory.CreateApiClient();
        await _factory.SignInAsync(staff, SuperadminVid, token);

        var member = await staff.GetFromJsonAsync<JsonElement>(NewsListUri(category), token);
        Assert.Equal(2, member.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task StatsMetricsAreAClosedSet()
    {
        // A module that wants a figure of its own registers a block of its own; there is no register
        // of metrics and this is what says so. A name nobody declares is left out rather than
        // refused, so a body written by a newer release does not turn into an error page here.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        using var client = _factory.CreateApiClient();

        var asked = StatsProvider.Metrics.Append("secretBudget").ToList();
        var props = new JsonObject
        {
            ["metrics"] = new JsonArray([.. asked.Select(metric => new JsonObject { ["metric"] = metric })]),
        };

        var answer = await client.GetFromJsonAsync<JsonElement>(BlockDataUri(CoreBlocks.Stats, props), token);
        var answered = answer.GetProperty("metrics")
            .EnumerateArray()
            .Select(metric => metric.GetProperty("metric").GetString())
            .ToList();

        Assert.Equal(StatsProvider.Metrics, answered!);
        Assert.All(
            answer.GetProperty("metrics").EnumerateArray(),
            metric => Assert.True(metric.GetProperty("value").GetInt32() >= 0));

        // The member that was seeded above is one the hub knows, so the figure is not a constant
        // zero that would pass whatever the query said.
        var members = answer.GetProperty("metrics").EnumerateArray()
            .First(metric => metric.GetProperty("metric").GetString() == StatsProvider.KnownMembers);

        Assert.True(members.GetProperty("value").GetInt32() >= 1);
    }

    [Fact]
    public async Task NetworkStatsCountsOnlyWhatTheSnapshotCallsOurs()
    {
        // "In the area" is a rule and not a list in the code: it is the centres and the airports of
        // the snapshot, which is what makes a division that forks get its own answer for free. The
        // fixture is written against the other two fixtures — four controllers of which three work
        // a station the snapshot knows, four flights of which two touch an airport it knows.
        var token = TestContext.Current.CancellationToken;

        await using var factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        await using (var seeding = factory.Services.CreateAsyncScope())
        {
            await SeedSnapshotAsync(seeding.ServiceProvider, token);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider
            .GetServices<IDataBlockProvider>()
            .First(candidate => candidate.Key == CoreBlocks.NetworkStats);

        var props = new JsonObject
        {
            ["figures"] = new JsonArray(
            [
                new JsonObject { ["figure"] = NetworkStatsProvider.DivisionAtc },
                new JsonObject { ["figure"] = NetworkStatsProvider.DivisionPilots },
                new JsonObject { ["figure"] = NetworkStatsProvider.NetworkAtc },
            ]),
            ["showPositions"] = true,
        };

        var answer = await provider.ResolveAsync(props, DataBlockContext.Reader, token);

        Assert.Equal(3, Figure(answer, NetworkStatsProvider.DivisionAtc));
        Assert.Equal(2, Figure(answer, NetworkStatsProvider.DivisionPilots));
        Assert.Equal(210, Figure(answer, NetworkStatsProvider.NetworkAtc));

        var positions = (answer["positions"] as JsonArray)!
            .Select(position => position!["callsign"]!.GetValue<string>())
            .ToList();

        Assert.Equal(["LIMC_APP", "LIRF_TWR", "LIRR_CTR"], positions);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static int Figure(JsonNode answer, string figure) =>
        (answer["figures"] as JsonArray)!
            .First(entry => entry!["figure"]!.GetValue<string>() == figure)!["value"]!
            .GetValue<int>();

    private static string[] Titles(JsonElement answer) =>
        [.. answer.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("url").GetString()!)];

    private static Uri PublicUri(string slug) =>
        new($"{ContentEndpoints.Pattern}/public/{ContentKind.Page}/{slug}", UriKind.Relative);

    private static Uri NewsListUri(string category) =>
        BlockDataUri(CoreBlocks.NewsList, new JsonObject { ["category"] = category, ["limit"] = 10 });

    /// <summary>
    /// A live block, asked the way the browser asks: the properties base64url encoded, because a
    /// query string reads a plain base64 <c>+</c> as a space.
    /// </summary>
    private static Uri BlockDataUri(string type, JsonNode props)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(props.ToJsonString()))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return new Uri($"/api/blocks/data/{type}?props={encoded}", UriKind.Relative);
    }

    private static JsonElement BlockOf(JsonElement content, string type) =>
        content.GetProperty("body").GetProperty("sections")[0].GetProperty("blocks")
            .EnumerateArray()
            .First(block => block.GetProperty("type").GetString() == type);

    private static Localized<string> Both(string italian, string english) =>
        new(
        [
            new KeyValuePair<string, string>("it", italian),
            new KeyValuePair<string, string>("en", english),
        ]);

    /// <summary>A heading, a news list and the network: the smallest page that shows the rule.</summary>
    private static JsonNode Body(string? category, string mode) =>
        JsonNode.Parse($$"""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "id": "s_main",
              "layout": "stacked",
              "blocks": [
                { "id": "b_heading", "type": "heading", "version": 1,
                  "props": { "level": 1, "text": { "it": "Numeri", "en": "Figures" } } },
                { "id": "b_news", "type": "newsList", "version": 1, "renderMode": "{{mode}}",
                  "props": { "category": {{(category is null ? "null" : $"\"{category}\"")}}, "limit": 10 } },
                { "id": "b_network", "type": "networkStats", "version": 1, "renderMode": "{{mode}}",
                  "props": { "figures": [ { "figure": "divisionAtc" } ], "showPositions": false } }
              ]
            }
          ]
        }
        """)!;

    private static async Task<long> CreateAsync(
        HttpClient client,
        string slug,
        JsonNode body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ContentEndpoints.Pattern, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                kind = nameof(ContentKind.Page),
                slug,
                ownerDepartment = nameof(Department.ED),
                visibility = nameof(Visibility.Public),
                isTemplate = false,
                title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Numeri", ["en"] = "Figures" },
                summary = (Dictionary<string, string>?)null,
                seo = (Dictionary<string, object>?)null,
                body,
                schemaVersion = 1,
                rowVersion = "0001-01-01T00:00:00",
            }),
        };

        request.Headers.Add("X-Requested-With", "hub");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task PublishAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{ContentEndpoints.Pattern}/{id}/publish", UriKind.Relative))
        {
            Content = JsonContent.Create(new { changelog = (string?)null }),
        };

        request.Headers.Add("X-Requested-With", "hub");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task SeedUserAsync(int vid, bool isSuperadmin, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (await database.Users.AnyAsync(row => row.Vid == vid, cancellationToken))
        {
            return;
        }

        database.Users.Add(new HubUser
        {
            Vid = vid,
            FirstName = "Test",
            LastName = "User",
            IsSuperadmin = isSuperadmin,
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNewsAsync(
        Department department,
        string category,
        string slug,
        Visibility visibility,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.Contents.Add(new ContentEntry
        {
            Kind = ContentKind.News,
            Slug = $"{category}-{slug}",
            OwnerDepartment = department,
            Visibility = visibility,

            // Published straight away: what this test is about is the visibility filter, and the
            // editorial state is the other half of the same filter, already proven elsewhere.
            Status = PublishStatus.Published,
            PublishedAt = clock.UtcNow,
            Title = Both($"Notizia {slug}", $"News {slug}"),
            BodyJson = """{ "schemaVersion": 1, "sections": [] }""",
            Category = category,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The reference data a division would have taken from the network, written straight in: this
    /// test is about what the hub does with a snapshot, not about how it comes by one.
    /// </summary>
    private static async Task SeedSnapshotAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<HubDbContext>();
        var clock = services.GetRequiredService<IClock>();

        foreach (var (id, name) in new[] { ("LIRR", "Roma"), ("LIMM", "Milano") })
        {
            if (!await database.IvaoCenters.AnyAsync(center => center.Id == id, cancellationToken))
            {
                database.IvaoCenters.Add(new IvaoCenter
                {
                    Id = id,
                    Name = name,
                    CountryId = "IT",
                    SyncedAt = clock.UtcNow,
                });
            }
        }

        foreach (var icao in new[] { "LIRF", "LIMC" })
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport
                {
                    Icao = icao,
                    Name = icao,
                    CountryId = "IT",
                    SyncedAt = clock.UtcNow,
                });
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        // The directory caches for six hours and the synchronisation is what normally clears it.
        services.GetRequiredService<IFirDirectory>().Invalidate();
    }
}
