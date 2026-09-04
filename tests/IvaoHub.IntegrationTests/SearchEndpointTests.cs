using System.Net.Http.Json;
using System.Text.Json;
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
/// <c>GET /api/search</c> over the FULLTEXT index of <c>cms_search_index</c>, on a real MariaDB.
/// <para>It is a different mechanism from the <c>?q=</c> of a back office list, and the difference
/// is the point: that one is a <c>LIKE</c> over the columns of one table, for a coordinator looking
/// through their own rows; this reads the projection the save changes interceptor rewrites for
/// every publishable row of every module (design M0 section 3.6).</para>
/// <para>What is really being proved here is that nothing in the endpoint decides who sees what:
/// the index rows declare an owner and a visibility, so the global query filter narrows them like
/// it narrows anything else.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SearchEndpointTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int MemberVid = 630001;
    private const int EventsCoordinatorVid = 630002;
    private const int FlightOpsCoordinatorVid = 630003;

    /// <summary>
    /// Long enough for InnoDB to index it: a FULLTEXT index ignores words shorter than
    /// <c>innodb_ft_min_token_size</c>, which is three by default, and a test that used a short word
    /// would fail for a reason that has nothing to do with the code.
    /// </summary>
    private const string Needle = "zurigo";

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task SearchRespectsVisibility()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(MemberVid, cancellationToken: token);
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        await SeedUserAsync(FlightOpsCoordinatorVid, position: "IT-FOC", cancellationToken: token);

        await SeedLinkAsync(Department.ED, Visibility.Public, $"{Needle} publico", token);
        await SeedLinkAsync(Department.ED, Visibility.Members, $"{Needle} membri", token);
        await SeedLinkAsync(Department.ED, Visibility.Staff, $"{Needle} staff", token);
        await SeedLinkAsync(Department.ED, Visibility.Department, $"{Needle} eventi", token);

        // An anonymous visitor: only what is published to everybody.
        using var anonymous = _factory.CreateApiClient();
        Assert.Equal(1, await CountAsync(anonymous, Needle, token));

        // A member who has signed in and holds no staff position: one more.
        using var member = _factory.CreateApiClient();
        await _factory.SignInAsync(member, MemberVid, token);
        Assert.Equal(2, await CountAsync(member, Needle, token));

        // Staff of another department: the public one, the members one, the staff one -- and not
        // the one events keeps to itself.
        using var flightOps = _factory.CreateApiClient();
        await _factory.SignInAsync(flightOps, FlightOpsCoordinatorVid, token);
        Assert.Equal(3, await CountAsync(flightOps, Needle, token));

        // The department that owns them: all four.
        using var events = _factory.CreateApiClient();
        await _factory.SignInAsync(events, EventsCoordinatorVid, token);
        Assert.Equal(4, await CountAsync(events, Needle, token));
    }

    [Fact]
    public async Task AnEmptyQueryIsAnEmptyPageAndNotTheWholeSite()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedLinkAsync(Department.ED, Visibility.Public, $"{Needle} qualcosa", token);

        using var client = _factory.CreateApiClient();
        var page = await client.GetFromJsonAsync<JsonElement>($"{SearchEndpoints.Pattern}?q=", token);

        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ARowIsIndexedOncePerLanguageAndFoundInTheOneAsked()
    {
        var token = TestContext.Current.CancellationToken;

        // Two different words for the same row, one per language of the division: the index holds
        // a row for each, which is what makes a FULLTEXT index work without a column per language.
        await SeedLinkAsync(
            Department.ED,
            Visibility.Public,
            italian: "linguaggio bilingue",
            english: "bilingual wording",
            cancellationToken: token);

        using var client = _factory.CreateApiClient();

        var italian = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=bilingue&locale=it",
            token);
        Assert.Equal(1, italian.GetProperty("total").GetInt32());

        var english = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=bilingual&locale=en",
            token);
        Assert.Equal(1, english.GetProperty("total").GetInt32());

        // And not in the other one: a row of the Italian index does not carry the English words.
        var wrongLanguage = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=bilingual&locale=it",
            token);
        Assert.Equal(0, wrongLanguage.GetProperty("total").GetInt32());
    }

    private static async Task<int> CountAsync(HttpClient client, string query, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q={query}&locale=it",
            cancellationToken);

        return page.GetProperty("total").GetInt32();
    }

    private Task SeedLinkAsync(
        Department department,
        Visibility visibility,
        string title,
        CancellationToken cancellationToken) =>
        SeedLinkAsync(department, visibility, title, title, cancellationToken);

    private async Task SeedLinkAsync(
        Department department,
        Visibility visibility,
        string italian,
        string english,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.Links.Add(new Link
        {
            OwnerDepartment = department,
            Visibility = visibility,
            Title = italian.L(english),
            Url = $"https://example.org/{Guid.NewGuid():N}",
            IsActive = true,
        });

        // The projection into cms_search_index is written by the save changes interceptor, inside
        // this very transaction. Nothing here writes an index row by hand.
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUserAsync(
        int vid,
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
}
