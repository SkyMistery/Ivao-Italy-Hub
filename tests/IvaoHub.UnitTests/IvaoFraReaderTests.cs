using System.Net;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The FRAs of IVAO as the hub reads them (M4, E10c, note <c>2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione</c>):
/// the rows of a position and never a member's, whichever client reads them, and the pages of the real client read whole or
/// not at all. The fixture is IVAO's answer for Italy on 30 September 2026, left with the FRAs of the bench's airports and
/// FIRs (<c>tests/fixtures/ivao/fras-IT.json</c>).
/// </summary>
public sealed class IvaoFraReaderTests
{
    private static IReadOnlyList<IvaoFraDto> Recorded() =>
        IvaoFraReader.Read(IvaoFixtures.Read("fras-IT.json").EnumerateArray());

    [Fact]
    public void TheRecordedFrasAreReadWithTheirPositionsDaysAndHours()
    {
        var fras = Recorded();

        Assert.Equal(94, fras.Count);
        Assert.Equal(fras.Count, fras.Select(fra => fra.Id).Distinct().Count());

        // Bari's tower: AS2 by day and ADC by night, every day of the week, the night past midnight.
        var tower = fras.Where(fra => fra.Callsign == "LIBD_TWR").OrderBy(fra => fra.Id).ToList();
        Assert.Equal(
            [
                new IvaoFraDto(785, "LIBD_TWR", 3, AllWeek, new TimeOnly(8, 0), new TimeOnly(23, 0), null, true, tower[0].RawJson),
                new IvaoFraDto(39880, "LIBD_TWR", 5, AllWeek, new TimeOnly(23, 0), new TimeOnly(8, 0), null, true, tower[1].RawJson),
            ],
            tower);

        // A sector's callsign comes from the sector IVAO expands with it; the whole day is 00:00 to 00:00.
        var sector = Assert.Single(fras, fra => fra.Callsign == "LIRR_NE_CTR");
        Assert.Equal((6, new TimeOnly(0, 0), new TimeOnly(0, 0)), (sector.MinimumRating, sector.StartsAt, sector.EndsAt));

        // An approach of Malpensa asks ADC until noon on Saturday and Sunday.
        var weekend = Assert.Single(fras, fra => fra.Id == 35658);
        Assert.Equal("LIMC_ANE_APP", weekend.Callsign);
        Assert.Equal(IvaoFraReader.DayBit(DayOfWeek.Saturday) | IvaoFraReader.DayBit(DayOfWeek.Sunday), weekend.Days);

        // The row is kept as IVAO sent it, and nobody is in it: the recording asked for the rows of a position only.
        Assert.Contains("LIBD_TWR", tower[0].RawJson, StringComparison.Ordinal);
        Assert.All(
            IvaoFixtures.Read("fras-IT.json").EnumerateArray(),
            row => Assert.Equal(JsonValueKind.Null, row.GetProperty("userId").ValueKind));
    }

    [Fact]
    public void AMembersRowAndARowThatDoesNotReadAreLeftOut()
    {
        var fras = IvaoFraReader.Read(Rows(
            Row(1, "LIRF_TWR"),
            Row(2, "LIRF_GND", userId: "761040"), // a member's exception, however IVAO spells the field
            Row(3, "LIRF_DEL", userIdAgain: "761041"),
            Row(4, "LIRF_APP", blacklist: "true"),
            Row(5, null), // no position IVAO expanded, so no callsign
            Row(6, "LIRF_W_GND", minimum: "null"),
            Row(7, "LIRF_E_TWR", start: "\"8 o'clock\""),
            Row(8, "LIRR_NE_CTR", sector: true),
            Row(1, "LIRF_TWR"),
            Row(9, "LIRF_S_TWR", userId: "0"))); // nobody, as a number

        // The tower, the sector, and the row whose member is nobody; the one met twice, once.
        Assert.Equal([1, 8, 9], fras.Select(fra => fra.Id));
        Assert.Equal("LIRR_NE_CTR", fras[1].Callsign);
    }

    [Theory]
    [InlineData("\"08:00\"", "\"17:00\"", 8, 17)] // as IVAO's documentation shows them
    [InlineData("\"23:00:00\"", "\"24:00:00\"", 23, 0)] // the end of the day is the midnight it runs up to
    [InlineData("\"00:00:00\"", "\"24:00\"", 0, 0)]
    public void TimesAreReadInTheirShapes(string start, string end, int startsAt, int endsAt)
    {
        var fra = Assert.Single(IvaoFraReader.Read(Rows(Row(1, "LIRF_TWR", start: start, end: end))));

        Assert.Equal(new TimeOnly(startsAt, 0), fra.StartsAt);
        Assert.Equal(new TimeOnly(endsAt, 0), fra.EndsAt);
    }

    [Theory]
    [InlineData("\"2026-09-12\"", "2026-09-12")] // as IVAO answered on 30 September 2026
    [InlineData("\"2026-09-12T00:00:00.000Z\"", "2026-09-12")] // as its documentation types it
    [InlineData("null", null)]
    [InlineData("\"the twelfth\"", null)]
    public void AnFraOfOneDateHasItsDate(string date, string? expected)
    {
        var fra = Assert.Single(IvaoFraReader.Read(Rows(Row(1, "LIRF_TWR", date: date))));

        Assert.Equal(expected is null ? null : DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), fra.OnDate);
    }

    [Fact]
    public void AnFraSwitchedOffIsReadAsSwitchedOff()
    {
        // The snapshot keeps it, as IVAO lists it; the directory is what leaves it out.
        var fra = Assert.Single(IvaoFraReader.Read(Rows(Row(1, "LIRF_TWR", active: false))));

        Assert.False(fra.IsActive);
    }

    [Fact]
    public void APageSaysHowManyThereAre()
    {
        using var page = JsonDocument.Parse($"{{ \"totalItems\": 1, \"perPage\": 100, \"page\": 1, \"pages\": 4, \"items\": [{Row(1, "LIRF_TWR")}] }}");
        var read = IvaoFraReader.ReadPage(page.RootElement);

        Assert.NotNull(read);
        Assert.Equal(4, read.Value.Pages);
        Assert.Single(read.Value.Rows);

        // What is not a page says nothing, least of all that there is no FRA.
        using var array = JsonDocument.Parse("[]");
        using var empty = JsonDocument.Parse("{}");
        Assert.Null(IvaoFraReader.ReadPage(array.RootElement));
        Assert.Null(IvaoFraReader.ReadPage(empty.RootElement));
    }

    [Fact]
    public async Task TheRealClientReadsEveryPageOfTheCountryAndOnlyThePositionsRows()
    {
        var recorded = IvaoFixtures.Read("fras-IT.json").EnumerateArray().ToArray();
        var ivao = new Pages([Page(recorded[..60], 1, 2), Page(recorded[60..], 2, 2)]);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var fras = await Client(ivao, cache).GetFrasAsync("IT", TestContext.Current.CancellationToken);

        Assert.NotNull(fras);
        Assert.Equal(94, fras.Count);
        Assert.Equal(2, ivao.Asked.Count);
        Assert.All(ivao.Asked, query =>
        {
            Assert.StartsWith("/v2/fras?countryId=IT&members=false&expand=true&perPage=100&page=", query, StringComparison.Ordinal);
        });
        Assert.EndsWith("page=2", ivao.Asked[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task APageThatFailsIsNoAnswerAtAll()
    {
        // Half of the FRAs would prune the other half from the snapshot: the client does not answer, and the night keeps what
        // it has.
        var recorded = IvaoFixtures.Read("fras-IT.json").EnumerateArray().ToArray();
        var ivao = new Pages([Page(recorded[..60], 1, 2), null]);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        Assert.Null(await Client(ivao, cache).GetFrasAsync("IT", TestContext.Current.CancellationToken));
        Assert.Equal(2, ivao.Asked.Count);
    }

    [Fact]
    public async Task ADivisionWithNoFraIsAnsweredNoneAndNotNothing()
    {
        // The reviewer's point 2 on #204: a division can lift all of its FRAs, and IVAO's empty page says so — an empty list,
        // which clears the snapshot, where a failure is null and keeps it. So does an answer that is not a page at all.
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var none = new Pages(["{ \"totalItems\": 0, \"perPage\": 100, \"page\": 1, \"pages\": 0, \"items\": [] }"]);

        var fras = await Client(none, cache).GetFrasAsync("IT", TestContext.Current.CancellationToken);

        Assert.NotNull(fras);
        Assert.Empty(fras);
        Assert.Single(none.Asked);

        using var otherCache = new MemoryCache(new MemoryCacheOptions());
        Assert.Null(await Client(new Pages(["[]"]), otherCache).GetFrasAsync("IT", TestContext.Current.CancellationToken));
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static readonly int AllWeek = Enum.GetValues<DayOfWeek>().Sum(IvaoFraReader.DayBit);

    private static JsonElement[] Rows(params string[] rows)
    {
        using var document = JsonDocument.Parse($"[{string.Join(',', rows)}]");
        return [.. document.RootElement.EnumerateArray().Select(row => row.Clone())];
    }

    /// <summary>A row shaped like IVAO's, with what a test changes in it: every value as the JSON it is written in.</summary>
    private static string Row(
        long id,
        string? callsign,
        string minimum = "5",
        string start = "\"08:00:00\"",
        string end = "\"23:00:00\"",
        string date = "null",
        bool active = true,
        bool sector = false,
        string userId = "null",
        string userIdAgain = "null",
        string blacklist = "false")
    {
        var position = callsign is null
            ? "\"atcPosition\": null, \"subcenter\": null"
            : sector
                ? $"\"atcPosition\": null, \"subcenter\": {{ \"centerId\": \"LIRR\", \"composePosition\": \"{callsign}\", \"position\": \"CTR\" }}"
                : $"\"atcPosition\": {{ \"airportId\": \"LIRF\", \"composePosition\": \"{callsign}\", \"position\": \"TWR\" }}, \"subcenter\": null";

        return $$"""
            {
              "id": {{id}}, "userId": {{userId}}, "user_id": {{userIdAgain}}, "minAtc": {{minimum}},
              "startTime": {{start}}, "endTime": {{end}}, "date": {{date}}, "active": {{(active ? "true" : "false")}},
              "dayMon": true, "dayTue": true, "dayWed": true, "dayThu": true, "dayFri": true, "daySat": true, "daySun": true,
              "isBlacklist": {{blacklist}}, {{position}}
            }
            """;
    }

    private static string Page(IEnumerable<JsonElement> rows, int page, int pages) =>
        $"{{ \"totalItems\": 94, \"perPage\": 100, \"page\": {page}, \"pages\": {pages}, \"items\": [{string.Join(',', rows.Select(row => row.GetRawText()))}] }}";

    private static IvaoApiClient Client(Pages ivao, MemoryCache cache)
    {
        var options = Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" });

        return new IvaoApiClient(
            new HttpClient(ivao) { BaseAddress = new Uri("https://api.example.org") },
            new IvaoApiTokenProvider(
                new HttpClient(ivao) { BaseAddress = new Uri("https://api.example.org") },
                options,
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            NullLogger<IvaoApiClient>.Instance);
    }

    /// <summary>An IVAO that gives a token and then the pages it is given, in order; a null page is a server error.</summary>
    private sealed class Pages(IReadOnlyList<string?> pages) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/v2/oauth/token")
            {
                return Task.FromResult(Json("{ \"access_token\": \"token\", \"expires_in\": 3600 }"));
            }

            Asked.Add(request.RequestUri.PathAndQuery);
            return Task.FromResult(pages[Asked.Count - 1] is { } page
                ? Json(page)
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
