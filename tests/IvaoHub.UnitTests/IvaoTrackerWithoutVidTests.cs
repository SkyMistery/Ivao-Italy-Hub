using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The tracker asked without a VID (M4, E10a): what happened at an airport in a window, read page after page. The
/// fixtures are one evening at Rome Fiumicino, 28 September 2026 from 16:00 to 17:59:59 UTC, recorded from the live API
/// with <c>tools/record-ivao-fixtures.mjs --sessions-at</c> and the people taken out (the nine members are VIDs 761020–761028):
/// four departures, six arrivals and the tower — one pilot connected twice, and one flight filed from LIPZ to Rome and then
/// from Rome to LICR, which is what tells how the tracker matches an airport.
/// </summary>
public sealed class IvaoTrackerWithoutVidTests
{
    private static readonly DateTime From = new(2026, 9, 28, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 28, 17, 59, 59, DateTimeKind.Utc);

    /// <summary>The flight whose plan went LIPZ to Rome in its first revision and Rome to LICR in its second.</summary>
    private const long Refiled = 1000007;

    [Fact]
    public void AnAirportsEveningSaysWhoFlewAndWhoControlledWithThePeopleTakenOut()
    {
        var (sessions, pages) = IvaoTrackerReader.ReadSessions(IvaoFixtures.Read("tracker-airport-LIRF.json"));

        Assert.Equal(10, sessions.Count);
        Assert.Equal(1, pages);
        Assert.Equal(9, sessions.Count(session => session.ConnectionType == IvaoConnectionType.Pilot));

        var tower = Assert.Single(sessions, session => session.ConnectionType == IvaoConnectionType.Atc);
        Assert.Equal("LIRF_TWR", tower.Callsign);
        Assert.False(tower.HasFlightPlan);

        // Nine members as the VIDs the events' tests own, one of them twice; no member object, and no pilot's own callsign.
        Assert.All(sessions, session => Assert.InRange(session.Vid, 761020, 761029));
        Assert.Equal(9, sessions.Select(session => session.Vid).Distinct().Count());
        Assert.All(sessions, session => Assert.DoesNotContain("\"user\"", session.RawJson, StringComparison.Ordinal));
        Assert.All(
            sessions.Where(session => session.ConnectionType == IvaoConnectionType.Pilot),
            session => Assert.StartsWith("TST", session.Callsign, StringComparison.Ordinal));
    }

    [Fact]
    public void AnAirportAloneIsFoundInAnyRevisionAndTwoAirportsInOneSameRevision()
    {
        Assert.Equal([1000010, 1000009, Refiled, 1000001], Found(new IvaoSessionQuery(null, From, To, "LIRF")));
        Assert.Equal([1000008, Refiled, 1000005, 1000004, 1000003, 1000002], Found(new IvaoSessionQuery(null, From, To, ArrivalIcao: "LIRF")));

        // Measured on this very flight: each revision answers for its own pair, and a pair taken from two revisions for none.
        Assert.Equal([Refiled], Found(new IvaoSessionQuery(null, From, To, "LIRF", "LICR")));
        Assert.Equal([Refiled, 1000004, 1000002], Found(new IvaoSessionQuery(null, From, To, "LIPZ", "LIRF")));
        Assert.Empty(Found(new IvaoSessionQuery(null, From, To, "LIPZ", "LICR")));
        Assert.Empty(Found(new IvaoSessionQuery(null, From, To, "LIRF", "LIRF")));

        // The row keeps the first revision's airports: found for its departure from Rome, it says it left LIPZ.
        var refiled = IvaoTrackerReader.ReadSession(Rows().Single(row => Id(row) == Refiled));
        Assert.NotNull(refiled);
        Assert.Equal("LIPZ", refiled.DepartureIcao);
        Assert.Equal("LIRF", refiled.ArrivalIcao);
    }

    [Fact]
    public void TheWindowHoldsTheSessionsThatStartedInItAndBothEndsCount()
    {
        var started = new DateTime(2026, 9, 28, 16, 46, 10, DateTimeKind.Utc);

        Assert.Equal([Refiled], Found(new IvaoSessionQuery(null, started, started, ArrivalIcao: "LIRF")));
        Assert.DoesNotContain(Refiled, Found(new IvaoSessionQuery(null, started.AddSeconds(1), To, ArrivalIcao: "LIRF")));
        Assert.DoesNotContain(Refiled, Found(new IvaoSessionQuery(null, From, started.AddSeconds(-1), ArrivalIcao: "LIRF")));
    }

    [Fact]
    public void TheKindOfConnectionAndTheMemberNarrowTheSearch()
    {
        Assert.Equal([1000006], Found(new IvaoSessionQuery(null, From, To, ConnectionType: IvaoConnectionType.Atc)));
        Assert.Equal(9, Found(new IvaoSessionQuery(null, From, To, ConnectionType: IvaoConnectionType.Pilot)).Length);

        // A controller files no plan, so a search for an airport never finds one.
        Assert.Empty(Found(new IvaoSessionQuery(null, From, To, "LIRF", ConnectionType: IvaoConnectionType.Atc)));

        // The member who connected twice.
        Assert.Equal([1000005, 1000003], Found(new IvaoSessionQuery(761025, From, To)));
    }

    [Fact]
    public async Task ThePagesAreReadInTurnUntilTheLastOne()
    {
        var asked = new List<int>();

        var (sessions, total) = Assert.NotNull(
            await IvaoTrackerReader.ReadPagesAsync(Departures(), Serve(RecordedPages(), asked), TestContext.Current.CancellationToken));

        Assert.Equal([1000010, 1000009, Refiled, 1000001], sessions.Select(session => session.Id));
        Assert.Equal(4, total);

        // Two pages of two: the page past the last one is never asked for.
        Assert.Equal([1, 2], asked);
    }

    [Fact]
    public async Task AWindowWithNothingInItIsAnEmptyAnswerNotAFailure()
    {
        var nothing = IvaoFixtures.Read("tracker-pages-LIRF.json").GetProperty("nothing");
        var asked = new List<int>();

        // What IVAO answers for a window with no session: no rows, and no pages at all.
        Assert.Equal(0, nothing.GetProperty("pages").GetInt32());

        var (sessions, total) = Assert.NotNull(
            await IvaoTrackerReader.ReadPagesAsync(Departures(), Serve([nothing], asked), TestContext.Current.CancellationToken));

        Assert.Empty(sessions);
        Assert.Equal(0, total);
        Assert.Equal([1], asked);
    }

    [Fact]
    public async Task APageWithNothingOnItEndsTheReadingWhateverTheCountSays()
    {
        // The page IVAO answers past the last one: no rows, while its count still says two pages.
        var past = RecordedPages()[^1];
        Assert.Equal(2, past.GetProperty("pages").GetInt32());
        Assert.Empty(past.GetProperty("items").EnumerateArray());

        var asked = new List<int>();
        var (sessions, _) = Assert.NotNull(await IvaoTrackerReader.ReadPagesAsync(
            Departures(),
            (number, _) =>
            {
                asked.Add(number);
                return Task.FromResult<JsonElement?>(past);
            },
            TestContext.Current.CancellationToken));

        Assert.Empty(sessions);
        Assert.Equal([1], asked);
    }

    [Fact]
    public async Task APageThatCannotBeHadHalfWayFailsTheWholeAnswer()
    {
        var pages = RecordedPages();
        var asked = new List<int>();

        var read = await IvaoTrackerReader.ReadPagesAsync(
            Departures(),
            (number, _) =>
            {
                asked.Add(number);
                return Task.FromResult<JsonElement?>(number == 1 ? pages[0] : null);
            },
            TestContext.Current.CancellationToken);

        // Half a list would read as "not there": the first page's two sessions do not come back alone.
        Assert.Null(read);
        Assert.Equal([1, 2], asked);
    }

    [Fact]
    public async Task TheLimitStopsThePagesAndCutsTheAnswer()
    {
        var asked = new List<int>();

        var (three, total) = Assert.NotNull(await IvaoTrackerReader.ReadPagesAsync(
            Departures() with { Limit = 3 },
            Serve(RecordedPages(), asked),
            TestContext.Current.CancellationToken));

        Assert.Equal([1000010, 1000009, Refiled], three.Select(session => session.Id));
        Assert.Equal(4, total);
        Assert.Equal([1, 2], asked);

        // A limit met on the first page asks for no other: the page holding the last session, the one that costs IVAO
        // ten seconds, is never read.
        asked.Clear();
        var (two, _) = Assert.NotNull(await IvaoTrackerReader.ReadPagesAsync(
            Departures() with { Limit = 2 },
            Serve(RecordedPages(), asked),
            TestContext.Current.CancellationToken));

        Assert.Equal([1000010, 1000009], two.Select(session => session.Id));
        Assert.Equal([1], asked);
    }

    [Fact]
    public async Task ThePagesGoOnPastTwoHundredWhenTheCallerSaysSo()
    {
        // Three full pages and a half, the rows cut from the recorded ones: the reading is what is tested here, not the rows.
        var pages = Enumerable.Range(1, 4).Select(number => Page(number, rows: number < 4 ? 100 : 50, total: 350, pages: 4)).ToArray();
        var asked = new List<int>();

        var (all, _) = Assert.NotNull(await IvaoTrackerReader.ReadPagesAsync(
            Departures() with { Limit = 1000 },
            Serve(pages, asked),
            TestContext.Current.CancellationToken));

        Assert.Equal(350, all.Count);
        Assert.Equal(350, all.Select(session => session.Id).Distinct().Count());
        Assert.Equal([1, 2, 3, 4], asked);

        // Without a limit of its own a search reads two hundred, as the tours always have.
        asked.Clear();
        var (some, total) = Assert.NotNull(
            await IvaoTrackerReader.ReadPagesAsync(Departures(), Serve(pages, asked), TestContext.Current.CancellationToken));

        Assert.Equal(IvaoSessionQuery.DefaultLimit, some.Count);
        Assert.Equal(350, total);
        Assert.Equal([1, 2], asked);
    }

    [Fact]
    public async Task TheSameSessionOnTwoPagesIsCountedOnce()
    {
        // A new session arrived between the two pages and pushed the rest down by one: the second page starts with the
        // first page's last row.
        var pages = RecordedPages();
        var second = JsonNode.Parse(pages[1].GetRawText())!.AsObject();
        second["items"]!.AsArray().Insert(0, JsonNode.Parse(pages[0].GetProperty("items")[1].GetRawText()));
        var asked = new List<int>();

        var (sessions, _) = Assert.NotNull(await IvaoTrackerReader.ReadPagesAsync(
            Departures(),
            Serve([pages[0], JsonSerializer.SerializeToElement(second)], asked),
            TestContext.Current.CancellationToken));

        Assert.Equal([1000010, 1000009, Refiled, 1000001], sessions.Select(session => session.Id));
    }

    [Fact]
    public void ASearchWithoutAMemberAsksForTheAirportsAndTheKindOfConnection()
    {
        var text = new IvaoSessionQuery(null, From, To, "lirf", ConnectionType: IvaoConnectionType.Atc).ToQueryString();

        Assert.DoesNotContain("userId", text, StringComparison.Ordinal);
        Assert.Contains("departureId=LIRF", text, StringComparison.Ordinal);
        Assert.Contains("connectionType=ATC", text, StringComparison.Ordinal);
        Assert.Contains("2026-09-28T16%3A00%3A00", text, StringComparison.Ordinal);

        // The tracker's own words, in capitals: anything else is refused with a 400.
        Assert.Equal(
            ["PILOT", "ATC", "OBS", "FOLME"],
            Enum.GetValues<IvaoConnectionType>().Select(type =>
                new IvaoSessionQuery(null, From, To, ConnectionType: type).ToQueryString().Split("connectionType=")[1]));
    }

    [Fact]
    public void APageIsAFullOneOrTheLimitAndTheLimitIsAtLeastOne()
    {
        Assert.Equal(100, Departures().PerPage);
        Assert.Equal(30, (Departures() with { Limit = 30 }).PerPage);
        Assert.Equal(100, (Departures() with { Limit = 1000 }).PerPage);

        Assert.Throws<ArgumentOutOfRangeException>(() => Departures() with { Limit = 0 });
    }

    [Fact]
    public async Task TheFixtureClientAnswersAnAirportTheWayTheTrackerDoes()
    {
        var client = new FixtureIvaoApiClient(
            HubPaths.Resolve(AppContext.BaseDirectory),
            new Development(),
            NullLogger<FixtureIvaoApiClient>.Instance);
        var cancellation = TestContext.Current.CancellationToken;

        var arrivals = await client.SearchSessionsAsync(new IvaoSessionQuery(null, From, To, ArrivalIcao: "LIRF"), cancellation);
        Assert.NotNull(arrivals);
        Assert.Equal([1000008, Refiled, 1000005, 1000004, 1000003, 1000002], arrivals.Select(session => session.Id));

        var newest = await client.SearchSessionsAsync(new IvaoSessionQuery(null, From, To, ArrivalIcao: "LIRF") { Limit = 2 }, cancellation);
        Assert.NotNull(newest);
        Assert.Equal([1000008, Refiled], newest.Select(session => session.Id));

        // A pair of airports is answered from either one's recording.
        var pair = await client.SearchSessionsAsync(new IvaoSessionQuery(null, From, To, "LIPZ", "LIRF"), cancellation);
        Assert.NotNull(pair);
        Assert.Equal([Refiled, 1000004, 1000002], pair.Select(session => session.Id));

        // An airport nobody recorded answers nothing rather than failing, and a member's own flights come as they did.
        Assert.Empty((await client.SearchSessionsAsync(new IvaoSessionQuery(null, From, To, "EGLL"), cancellation))!);
        var june = await client.SearchSessionsAsync(
            new IvaoSessionQuery(780001, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), "LPMA"),
            cancellation);
        Assert.NotNull(june);
        Assert.Equal(62748566, Assert.Single(june).Id);
    }

    [Fact]
    public async Task TheClientAsksForTheAirportWithoutAMemberAHundredToAPage()
    {
        using var ivao = new PlayedIvao(RecordedPages(), failingPage: null);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var sessions = await Client(ivao, cache).SearchSessionsAsync(Departures(), TestContext.Current.CancellationToken);

        Assert.NotNull(sessions);
        Assert.Equal([1000010, 1000009, Refiled, 1000001], sessions.Select(session => session.Id));
        Assert.Equal(2, ivao.Asked.Count);
        Assert.All(ivao.Asked, query =>
        {
            Assert.DoesNotContain("userId", query, StringComparison.Ordinal);
            Assert.Contains("departureId=LIRF", query, StringComparison.Ordinal);
            Assert.Contains("&perPage=100", query, StringComparison.Ordinal);
        });
        Assert.EndsWith("&page=2&perPage=100", ivao.Asked[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task APageThatNeverComesIsCouldNotLookAndNotAnException()
    {
        // The second page does not arrive at all — the connection dropped, as when IVAO's gateway gives up on the slow
        // page. The answer is "could not look", the same as a refused page: never an exception, never half a list.
        using var ivao = new PlayedIvao(RecordedPages(), failingPage: 2);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var sessions = await Client(ivao, cache).SearchSessionsAsync(Departures(), TestContext.Current.CancellationToken);

        Assert.Null(sessions);
        Assert.Equal(2, ivao.Asked.Count);
    }

    private static IvaoApiClient Client(PlayedIvao ivao, MemoryCache cache) => new(
        new HttpClient(ivao, disposeHandler: false) { BaseAddress = new Uri("https://api.example.org") },
        new IvaoApiTokenProvider(
            new HttpClient(ivao, disposeHandler: false) { BaseAddress = new Uri("https://api.example.org") },
            Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" }),
            cache,
            NullLogger<IvaoApiTokenProvider>.Instance),
        cache,
        NullLogger<IvaoApiClient>.Instance);

    private static IvaoSessionQuery Departures() => new(null, From, To, "LIRF");

    private static JsonElement[] Rows() => [.. IvaoFixtures.Read("tracker-airport-LIRF.json").EnumerateArray()];

    private static long Id(JsonElement row) => row.GetProperty("id").GetInt64();

    private static long[] Found(IvaoSessionQuery query) => [.. Rows().Where(row => IvaoTrackerReader.Answers(row, query)).Select(Id)];

    /// <summary>The departures of the evening, two to a page, as IVAO paged them, and then the page past the last one.</summary>
    private static JsonElement[] RecordedPages() => [.. IvaoFixtures.Read("tracker-pages-LIRF.json").GetProperty("pages").EnumerateArray()];

    /// <summary>Hands out the pages one by one, writing down which were asked for; past the last, nothing.</summary>
    private static Func<int, CancellationToken, Task<JsonElement?>> Serve(JsonElement[] pages, List<int> asked) =>
        (number, _) =>
        {
            asked.Add(number);
            return Task.FromResult<JsonElement?>(number <= pages.Length ? pages[number - 1] : null);
        };

    /// <summary>A page of the tracker's shape, its rows cut from the first recorded one, each with an identifier of its own.</summary>
    private static JsonElement Page(int number, int rows, int total, int pages)
    {
        var template = Rows()[0].GetRawText();
        var items = new JsonArray();
        for (var index = 0; index < rows; index++)
        {
            var row = JsonNode.Parse(template)!.AsObject();
            row["id"] = 5_000_000 - ((number - 1) * 100) - index;
            items.Add(row);
        }

        return JsonSerializer.SerializeToElement(new JsonObject
        {
            ["items"] = items,
            ["totalItems"] = total,
            ["perPage"] = 100,
            ["page"] = number,
            ["pages"] = pages,
        });
    }

    /// <summary>
    /// IVAO played by the test: a token for the asking, and the recorded pages of the tracker by their number — except one
    /// that never arrives, when the test says so.
    /// </summary>
    private sealed class PlayedIvao(JsonElement[] pages, int? failingPage) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/v2/oauth/token")
            {
                return Task.FromResult(Answer("""{"access_token":"played","token_type":"Bearer","expires_in":1800}"""));
            }

            Asked.Add(uri.Query);
            var number = int.Parse(
                HttpUtility.ParseQueryString(uri.Query)["page"]!,
                System.Globalization.CultureInfo.InvariantCulture);

            return number == failingPage
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("The connection was closed before the page arrived."))
                : Task.FromResult(Answer(pages[number - 1].GetRawText()));
        }

        private static HttpResponseMessage Answer(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "IvaoHub.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
