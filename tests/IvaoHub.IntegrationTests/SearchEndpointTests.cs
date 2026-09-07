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
    /// <para>A word of its own per test, and not one shared by the class: the whole assembly writes
    /// into one database, so a row another test seeded would be counted here. It is exactly what
    /// happened the first time — locally the order of the two tests hid it, on CI it did not.</para>
    /// </summary>
    private const string Needle = "zurigo";

    /// <summary>The word of the empty query test, so that its own row is never counted above.</summary>
    private const string OtherNeedle = "amburgo";

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
        await SeedLinkAsync(Department.ED, Visibility.Public, $"{OtherNeedle} qualcosa", token);

        using var client = _factory.CreateApiClient();
        var page = await client.GetFromJsonAsync<JsonElement>($"{SearchEndpoints.Pattern}?q=", token);

        Assert.Equal(0, page.GetProperty("results").GetProperty("total").GetInt32());
        Assert.Empty(page.GetProperty("results").GetProperty("items").EnumerateArray());
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
        Assert.Equal(1, italian.GetProperty("results").GetProperty("total").GetInt32());

        var english = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=bilingual&locale=en",
            token);
        Assert.Equal(1, english.GetProperty("results").GetProperty("total").GetInt32());

        // And not in the other one: a row of the Italian index does not carry the English words.
        var wrongLanguage = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=bilingual&locale=it",
            token);
        Assert.Equal(0, wrongLanguage.GetProperty("results").GetProperty("total").GetInt32());
    }

    // ---- the three questions M0 left open (design M1 section 7) --------------------------------

    [Fact]
    public async Task SearchOrdersByRelevanceThenRecency()
    {
        // Relevance first, and among equals the most recently changed. Both halves are asserted,
        // because either one alone passes on data that happens to be in the right order anyway.
        var token = TestContext.Current.CancellationToken;
        var needle = $"gothenburg{Guid.NewGuid():N}"[..20];

        // Three rows, seeded in the wrong order on purpose. The first two say the word once each,
        // so they score the same and only their age separates them; the third says it three times
        // in the title and the description, so it scores higher whatever its age.
        await SeedLinkAsync(Department.ED, Visibility.Public, $"{needle} older", token);
        await Task.Delay(1100, token);
        await SeedLinkAsync(Department.ED, Visibility.Public, $"{needle} newer", token);
        await Task.Delay(1100, token);
        await SeedLinkAsync(
            Department.ED,
            Visibility.Public,
            $"{needle} {needle} {needle} strongest",
            token);

        using var client = _factory.CreateApiClient();
        var titles = await TitlesAsync(client, needle, token);

        // The strongest match first, however old it is; then the two equals, newest first.
        Assert.Equal(3, titles.Count);
        Assert.Contains("strongest", titles[0], StringComparison.Ordinal);
        Assert.Contains("newer", titles[1], StringComparison.Ordinal);
        Assert.Contains("older", titles[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchReturnsSnippetPerLocale()
    {
        // The extract comes back as **text**, in the language searched, around the first place the
        // word turns up. Which terms to mark is the browser's business; the server does not return
        // markup and this asserts that it does not.
        var token = TestContext.Current.CancellationToken;
        var needle = $"helsinki{Guid.NewGuid():N}"[..18];

        await SeedLinkAsync(
            Department.ED,
            Visibility.Public,
            italian: $"Titolo italiano",
            english: $"An English title",
            // ⚠️ The opening is **long on purpose**, longer than a snippet: with a short text the
            // whole thing comes back whatever the extract does, and the first version of this test
            // passed with the search term ignored entirely. The word has to sit far enough in that
            // only an extract cut around it can contain it.
            description: (
                Italian: $"{Filler("Una premessa lunga")} e poi la parola {needle} in mezzo al testo.",
                English: $"{Filler("A long opening")} and then the word {needle} in the middle of the text."),
            cancellationToken: token);

        using var client = _factory.CreateApiClient();

        var italian = await FirstHitAsync(client, needle, "it", token);
        var english = await FirstHitAsync(client, needle, "en", token);

        var italianSnippet = italian.GetProperty("snippet").GetString()!;
        var englishSnippet = english.GetProperty("snippet").GetString()!;

        // Cut around the word and not from the top: the opening is longer than a whole snippet, so
        // an extract that started at the beginning could not contain the word at all.
        Assert.True(
            italianSnippet.Length <= SearchSnippet.MaxLength + 4,
            $"the snippet is {italianSnippet.Length} characters, which is not an extract");
        Assert.StartsWith("…", italianSnippet, StringComparison.Ordinal);

        // Each row of the index is one language, so each snippet is cut out of that language's text.
        Assert.Contains(needle, italianSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parola", italianSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("the word", italianSnippet, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(needle, englishSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("the word", englishSnippet, StringComparison.OrdinalIgnoreCase);

        // Text and never markup: the client marks the terms, because only the client knows the
        // language on screen — and a server that returned HTML would be one nobody could escape.
        Assert.DoesNotContain("<", italianSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("&lt;", italianSnippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchTellsWhenEveryTermIsTooShort()
    {
        // The one of the three the code cannot fix: InnoDB does not index words shorter than three
        // letters, and on a shared MariaDB `innodb_ft_min_token_size` is not ours to change. So the
        // answer says so, with a key the browser turns into a sentence, instead of an empty page
        // that reads as an opinion about the question.
        var token = TestContext.Current.CancellationToken;

        using var client = _factory.CreateApiClient();

        var tooShort = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=an%20il&locale=it",
            token);

        Assert.Equal(SearchEndpoints.TermsTooShort, tooShort.GetProperty("notice").GetString());
        Assert.Equal(0, tooShort.GetProperty("results").GetProperty("total").GetInt32());

        // ⚠️ And one long word next to two short ones is **not** this case: MariaDB ignores the
        // short ones and answers on the rest, which is a real answer and must not be explained away.
        var mixed = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q=il%20{Needle}&locale=it",
            token);

        Assert.True(
            mixed.GetProperty("notice").ValueKind == JsonValueKind.Null,
            "a query with one word long enough to be indexed is a real query");
    }

    // ---- helpers -------------------------------------------------------------------------------

    /// <summary>Enough words to push what follows past the length of a snippet.</summary>
    private static string Filler(string opening) =>
        string.Join(' ', Enumerable.Repeat(opening, 12));

    private static async Task<List<string>> TitlesAsync(
        HttpClient client,
        string query,
        CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q={query}&locale=it",
            cancellationToken);

        return [.. page.GetProperty("results").GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)];
    }

    private static async Task<JsonElement> FirstHitAsync(
        HttpClient client,
        string query,
        string locale,
        CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q={query}&locale={locale}",
            cancellationToken);

        return page.GetProperty("results").GetProperty("items").EnumerateArray().First();
    }

    private static async Task<int> CountAsync(HttpClient client, string query, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{SearchEndpoints.Pattern}?q={query}&locale=it",
            cancellationToken);

        // ⚠️ One level deeper since G10: a search answers with the page **and** the one thing it
        // sometimes has to say about the query itself. The envelope inside is the same
        // `PagedResult` every list of the hub uses.
        return page.GetProperty("results").GetProperty("total").GetInt32();
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
        CancellationToken cancellationToken,
        (string Italian, string English)? description = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.Links.Add(new Link
        {
            OwnerDepartment = department,
            Visibility = visibility,
            Title = italian.L(english),
            Description = description is { } text ? text.Italian.L(text.English) : null,
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
