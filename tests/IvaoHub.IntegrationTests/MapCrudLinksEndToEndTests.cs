using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// The CRUD engine over the wire, on the guinea pig entity of M0 (design M0 section 8). Nothing
/// here is mocked away: a real MariaDB, the real application cookie, the real policies and the real
/// save changes interceptor. What it proves is that a resource of the back office costs a
/// configuration object — because if any of it had needed hand written code, these tests would be
/// exercising that code instead.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MapCrudLinksEndToEndTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 610001;
    private const int EventsCoordinatorVid = 610002;
    private const int FlightOpsAdvisorVid = 610003;
    private const int MemberVid = 610004;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AnonymousIsChallengedAndAMemberIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(MemberVid, cancellationToken: token);

        using var anonymous = _factory.CreateApiClient();
        using var anonymousResponse = await anonymous.GetAsync(new Uri(LinksEndpoints.Pattern, UriKind.Relative), token);

        // Not signed in is 401 and not 403: the browser has somewhere to go from there.
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var member = _factory.CreateApiClient();
        await _factory.SignInAsync(member, MemberVid, token);
        using var memberResponse = await member.GetAsync(new Uri(LinksEndpoints.Pattern, UriKind.Relative), token);

        // Signed in but holding no permission at all: the policy on the endpoint says no.
        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);
    }

    [Fact]
    public async Task TheListIsNarrowedToTheDepartmentsOfTheUser()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        await SeedLinkAsync(Department.ED, "https://events.example.org/one", token: token);
        await SeedLinkAsync(Department.FOD, "https://flightops.example.org/one", token: token);

        using var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);
        var mine = await ListAsync(coordinator, string.Empty, token);

        // The coordinator of one department never sees the rows of another, filter or no filter.
        Assert.All(
            mine.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("ED", item.GetProperty("ownerDepartment").GetString()));
        Assert.NotEmpty(mine.GetProperty("items").EnumerateArray());

        using var superadmin = _factory.CreateApiClient();
        await _factory.SignInAsync(superadmin, SuperadminVid, token);
        var everything = await ListAsync(superadmin, string.Empty, token);

        var departments = everything.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("ownerDepartment").GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ED", departments);
        Assert.Contains("FOD", departments);
    }

    [Fact]
    public async Task TheListPagesSortsFiltersAndSearchesInTheLanguageOfTheReader()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(SuperadminVid, isSuperadmin: true, cancellationToken: token);

        var category = $"paging-{Guid.NewGuid():N}";
        for (var index = 0; index < 5; index++)
        {
            await SeedLinkAsync(
                Department.ED,
                $"https://example.org/{category}/{index}",
                category: category,
                sort: index,
                title: new Localized<string>(
                [
                    new KeyValuePair<string, string>("it", $"Invito numero {index}"),
                    new KeyValuePair<string, string>("en", $"Invitation number {index}"),
                ]),
                token: token);
        }

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        var firstPage = await ListAsync(client, $"?filter[category]={category}&pageSize=2&sort=Sort&dir=asc", token);
        Assert.Equal(5, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(0, firstPage.GetProperty("items")[0].GetProperty("sort").GetInt32());

        var lastPage = await ListAsync(client, $"?filter[category]={category}&pageSize=2&sort=Sort&dir=desc", token);
        Assert.Equal(4, lastPage.GetProperty("items")[0].GetProperty("sort").GetInt32());

        // The default locale of the division is Italian, so the Italian title is what is searched.
        var italian = await ListAsync(client, $"?filter[category]={category}&q=Invito numero 3", token);
        Assert.Equal(1, italian.GetProperty("total").GetInt32());

        // The English one is in the very same column and is not what this reader is searching.
        var english = await ListAsync(client, $"?filter[category]={category}&q=Invitation number 3", token);
        Assert.Equal(0, english.GetProperty("total").GetInt32());

        // Filtering and sorting are allow lists, not a query language.
        using var unknownFilter = await client.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}?filter[createdBy]=1", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, unknownFilter.StatusCode);

        using var unknownSort = await client.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}?sort=CreatedBy", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, unknownSort.StatusCode);
    }

    [Fact]
    public async Task ACreateWithoutEveryLanguageIsAValidationProblemNamingTheLanguage()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        using var response = await PostAsync(
            client,
            Payload(Department.ED, "https://events.example.org/incomplete", italian: "Solo italiano", english: null),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        var errors = problem.GetProperty("errors");

        // The message is an i18n key, never a sentence: the browser knows which language it draws.
        Assert.Equal(
            LocalizedRules.MissingMessageKey,
            errors.GetProperty("title")[0].GetString());

        // And what is missing travels with it, so a form can say "English", not "invalid".
        var missing = problem.GetProperty(CrudProblems.LocalizedExtension).GetProperty("title")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        Assert.Equal(["en"], missing);
    }

    [Fact]
    public async Task AnAddressThatIsNotAWebAddressIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        using var response = await PostAsync(client, Payload(Department.ED, "javascript:alert(1)"), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("errors.url.absolute", problem.GetProperty("errors").GetProperty("url")[0].GetString());
    }

    [Fact]
    public async Task ACoordinatorCreatesReadsUpdatesAndDeletesInTheirOwnDepartment()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var url = $"https://events.example.org/{Guid.NewGuid():N}";
        using var created = await PostAsync(client, Payload(Department.ED, url), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var detail = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        var id = detail.GetProperty("id").GetInt64();

        // The audit columns were filled by the interceptor, not by the payload.
        Assert.Equal(EventsCoordinatorVid, detail.GetProperty("createdBy").GetInt32());
        Assert.Equal(EventsCoordinatorVid, detail.GetProperty("updatedBy").GetInt32());

        var read = await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}/{id}", token);
        Assert.Equal(url, read.GetProperty("url").GetString());

        var rowVersion = read.GetProperty("rowVersion").GetString()!;

        using var updated = await PutAsync(
            client,
            id,
            Payload(Department.ED, url, italian: "Invito aggiornato", english: "Updated invitation", rowVersion: rowVersion),
            token);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var afterUpdate = await updated.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal("Invito aggiornato", afterUpdate.GetProperty("title").GetProperty("it").GetString());

        using var deleted = await DeleteAsync(client, id, token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var gone = await client.GetAsync(new Uri($"{LinksEndpoints.Pattern}/{id}", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task AStaleVersionIsAConflictAndNotASilentOverwrite()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var url = $"https://events.example.org/{Guid.NewGuid():N}";
        using var created = await PostAsync(client, Payload(Department.ED, url), token);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        var loaded = await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}/{id}", token);
        var staleVersion = loaded.GetProperty("rowVersion").GetString()!;

        using var first = await PutAsync(
            client,
            id,
            Payload(Department.ED, url, italian: "Prima modifica", english: "First edit", rowVersion: staleVersion),
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Somebody else saved in the meantime; this form is holding the version from before.
        using var second = await PutAsync(
            client,
            id,
            Payload(Department.ED, url, italian: "Seconda modifica", english: "Second edit", rowVersion: staleVersion),
            token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var stored = await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}/{id}", token);
        Assert.Equal("Prima modifica", stored.GetProperty("title").GetProperty("it").GetString());
    }

    [Fact]
    public async Task AnotherDepartmentIsRefusedOnEveryVerb()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(FlightOpsAdvisorVid, position: "IT-FOA1", cancellationToken: token);

        var foreignId = await SeedLinkAsync(Department.FOD, $"https://flightops.example.org/{Guid.NewGuid():N}", token: token);

        using var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, EventsCoordinatorVid, token);

        using var read = await coordinator.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{foreignId}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        using var write = await PutAsync(
            coordinator,
            foreignId,
            Payload(Department.FOD, "https://flightops.example.org/hijacked"),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        using var remove = await DeleteAsync(coordinator, foreignId, token);
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);

        // Creating one over there is refused too: the department travels in the payload, and the
        // payload is not what decides.
        using var created = await PostAsync(coordinator, Payload(Department.FOD, "https://flightops.example.org/new"), token);
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);

        // The advisor of that department may edit its rows: an advisor edits, and does not publish.
        using var advisor = _factory.CreateApiClient();
        await _factory.SignInAsync(advisor, FlightOpsAdvisorVid, token);

        var loaded = await advisor.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}/{foreignId}", token);
        using var allowed = await PutAsync(
            advisor,
            foreignId,
            Payload(
                Department.FOD,
                loaded.GetProperty("url").GetString()!,
                italian: "Modificato dall'advisor",
                english: "Edited by the advisor",
                rowVersion: loaded.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task MovingARowToAnotherDepartmentNeedsThePermissionOnBothSides()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var url = $"https://events.example.org/{Guid.NewGuid():N}";
        using var created = await PostAsync(client, Payload(Department.ED, url), token);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        var loaded = await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}/{id}", token);

        // Handing a row to a department one is not part of it would be a way of taking rows away
        // from a department one row at a time.
        using var moved = await PutAsync(
            client,
            id,
            Payload(Department.FOD, url, rowVersion: loaded.GetProperty("rowVersion").GetString()),
            token);

        Assert.Equal(HttpStatusCode.Forbidden, moved.StatusCode);
    }

    [Fact]
    public async Task AWriteThroughTheEngineIsProjectedIntoTheSearchIndex()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, EventsCoordinatorVid, token);

        var url = $"https://events.example.org/{Guid.NewGuid():N}";
        using var created = await PostAsync(client, Payload(Department.ED, url), token);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        // The engine never writes a projection: the interceptor does, in the same transaction.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var rows = await database.SearchIndex
                .Where(entry => entry.SourceId == $"link:{id}")
                .ToListAsync(token);

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.Locale == "it");
            Assert.Contains(rows, row => row.Locale == "en");
        }

        using var deleted = await DeleteAsync(client, id, token);
        deleted.EnsureSuccessStatusCode();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            Assert.Empty(await database.SearchIndex.Where(entry => entry.SourceId == $"link:{id}").ToListAsync(token));
        }
    }

    // ---- helpers -----------------------------------------------------------------------------

    private async Task<JsonElement> ListAsync(HttpClient client, string query, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>($"{LinksEndpoints.Pattern}{query}", cancellationToken);

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        object payload,
        CancellationToken cancellationToken) =>
        SendAsync(client, HttpMethod.Post, LinksEndpoints.Pattern, payload, cancellationToken);

    private static Task<HttpResponseMessage> PutAsync(
        HttpClient client,
        long id,
        object payload,
        CancellationToken cancellationToken) =>
        SendAsync(client, HttpMethod.Put, $"{LinksEndpoints.Pattern}/{id}", payload, cancellationToken);

    private static Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        long id,
        CancellationToken cancellationToken) =>
        SendAsync(client, HttpMethod.Delete, $"{LinksEndpoints.Pattern}/{id}", payload: null, cancellationToken);

    /// <summary>
    /// Every state changing call carries the header the cross site guard demands, exactly as the
    /// generated client does: a test that skipped it would be testing a request no browser makes.
    /// </summary>
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

    private static object Payload(
        Department department,
        string url,
        string? italian = "Invito Discord",
        string? english = "Discord invitation",
        int sort = 0,
        string? category = null,
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
            ownerDepartment = department.ToString(),
            visibility = nameof(Visibility.Public),
            title,
            url,
            description = (Dictionary<string, string>?)null,
            category,
            sort,
            isActive = true,
            // A payload with no version means "the row as it is now"; the tests that care about
            // the conflict send the one they loaded.
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };
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

    /// <summary>
    /// A row put there by the installation itself rather than by a person: no HTTP context, so no
    /// identity, which is how a job or a seed writes. The write guard leaves those alone.
    /// </summary>
    private async Task<long> SeedLinkAsync(
        Department department,
        string url,
        string? category = null,
        int sort = 0,
        Localized<string>? title = null,
        CancellationToken token = default)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var link = new Link
        {
            OwnerDepartment = department,
            Visibility = Visibility.Public,
            Title = title ?? new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Invito Discord"),
                new KeyValuePair<string, string>("en", "Discord invitation"),
            ]),
            Url = url,
            Category = category,
            Sort = sort,
        };

        database.Links.Add(link);
        await database.SaveChangesAsync(token);

        return link.Id;
    }
}
