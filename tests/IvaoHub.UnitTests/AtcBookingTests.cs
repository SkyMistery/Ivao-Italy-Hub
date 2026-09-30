using System.Net;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The network's ATC bookings (M4, E15a), read against one real day: <c>atc-bookings-day.json</c>, recorded with the real token on
/// 30 September 2026 (<c>tools/record-ivao-fixtures.mjs --bookings day 761070 &lt;day&gt; …</c>) — the bookings of the bench's
/// positions that started that day, with an exam and one across midnight, the people replaced by VIDs 761070–761079 and the day
/// moved onto 1 January 2001, so that nothing in the file finds a booking again through IVAO's API. Nothing here calls IVAO: the
/// real client is asked through an IVAO that answers however the test says.
/// </summary>
public sealed class AtcBookingTests
{
    /// <summary>The day the tool moves a recorded day onto: IVAO has no booking before 2023.</summary>
    private static readonly DateOnly Recorded = new(2001, 1, 1);

    /// <summary>A day the fixture was not recorded on, for the bench that shows it on any day.</summary>
    private static readonly DateOnly AnyDay = new(2031, 3, 14);

    // ---- the reader ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheRecordedDayIsReadWithItsPositionsItsSectorsAndItsPeople()
    {
        var bookings = IvaoAtcBookingReader.ReadBookings(IvaoFixtures.Read("atc-bookings-day.json"));

        Assert.NotNull(bookings);
        Assert.Equal(10, bookings.Count);
        Assert.Equal(Enumerable.Range(761070, 10), bookings.Select(booking => booking.Vid).Order());

        // An airport's position and a sector are the same thing to the hub: a callsign, and two times in UTC.
        var tower = Assert.Single(bookings, booking => booking.Callsign == "LIRF_TWR");
        Assert.Equal(At(Recorded, 18), tower.StartsAt);
        Assert.Equal(At(Recorded, 20), tower.EndsAt);
        Assert.Equal(DateTimeKind.Utc, tower.StartsAt.Kind);
        Assert.Equal(AtcBookingKind.Controlling, tower.Kind);

        Assert.Equal(At(Recorded, 22), Assert.Single(bookings, booking => booking.Callsign == "LIRR_NE_CTR").EndsAt);
        Assert.Equal(5, bookings.Count(booking => booking.Callsign.EndsWith("_CTR", StringComparison.Ordinal)));
        Assert.Equal(AtcBookingKind.Exam, Assert.Single(bookings, booking => booking.Callsign == "EDDF_APP").Kind);
        Assert.Equal(At(Recorded.AddDays(1), 1), Assert.Single(bookings, booking => booking.Callsign == "SBGR_TWR").EndsAt);
    }

    [Fact]
    public void TheRecordedDayKeepsNothingOfThePeopleButTheirNumberNorAnythingThatFindsThemAgain()
    {
        foreach (var row in IvaoFixtures.Read("atc-bookings-day.json").EnumerateArray())
        {
            // IVAO sends the member with their names, division and rating; the tool keeps the number that stands for them.
            Assert.Equal(["id"], row.GetProperty("user").EnumerateObject().Select(property => property.Name));

            // The booking's own number, the moment it was made and its real day would each find it, and its member, through
            // IVAO's API (reviewer's finding on #207): none of them is left.
            Assert.False(row.TryGetProperty("id", out _));
            Assert.False(row.TryGetProperty("createdAt", out _));
            Assert.StartsWith("2001-01-01T", row.GetProperty("startDate").GetString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ARowThatIsNotABookingIsLeftOutAndTheRestIsReadInUpperCase()
    {
        using var answer = JsonDocument.Parse("""
            [
              { "startDate": "2001-01-01T19:00:00.000Z", "endDate": "2001-01-01T21:00:00.000Z", "user": { "id": 761070 } },
              { "atcPosition": "LIRF_TWR", "endDate": "2001-01-01T21:00:00.000Z", "user": { "id": 761070 } },
              { "atcPosition": "LIRF_TWR", "startDate": "2001-01-01T19:00:00.000Z", "endDate": "later",
                "user": { "id": 761070 } },
              { "atcPosition": "LIRF_TWR", "startDate": "2001-01-01T19:00:00.000Z", "endDate": "2001-01-01T21:00:00.000Z" },
              { "atcPosition": "LIRF_TWR", "startDate": "2001-01-01T19:00:00.000Z", "endDate": "2001-01-01T21:00:00.000Z",
                "user": { "id": "761070" } },
              "not even an object",
              {
                "atcPosition": null, "subcenter": " lirr_ne_ctr ", "training": "training",
                "startDate": "2001-01-01T19:00:00.000Z", "endDate": "2001-01-01T21:00:00.000Z",
                "user": { "id": 761071, "firstName": "Some", "lastName": "Body" }
              }
            ]
            """);

        var booking = Assert.Single(IvaoAtcBookingReader.ReadBookings(answer.RootElement)!);

        Assert.Equal(new AtcBookingDto("LIRR_NE_CTR", At(Recorded, 19), At(Recorded, 21), 761071, AtcBookingKind.Training), booking);
    }

    [Fact]
    public void AnAnswerThatIsNotAListOfBookingsIsNotADayWithoutBookings()
    {
        // What IVAO answers to a date it cannot read — with a 400, but the reader must not need the status to tell.
        using var refusal = JsonDocument.Parse("""{ "message": "Validation failed (invalid date format)", "statusCode": 400 }""");
        using var empty = JsonDocument.Parse("[]");

        // What the paged list of the bookings (/v2/atc/bookings) answers: a shape of another endpoint, never of the day.
        using var paged = JsonDocument.Parse("""
            { "items": [ { "atcPosition": "LIRF_TWR", "startDate": "2001-01-01T18:00:00.000Z", "endDate": "2001-01-01T20:00:00.000Z",
              "user": { "id": 761079 } } ], "totalItems": 1, "perPage": 100, "page": 1, "pages": 1 }
            """);

        Assert.Null(IvaoAtcBookingReader.ReadBookings(refusal.RootElement));
        Assert.Empty(IvaoAtcBookingReader.ReadBookings(empty.RootElement)!);
        Assert.Null(IvaoAtcBookingReader.ReadBookings(paged.RootElement));
    }

    [Theory]
    [InlineData("exam", AtcBookingKind.Exam)]
    [InlineData("EXAM", AtcBookingKind.Exam)]
    [InlineData("training", AtcBookingKind.Training)]
    [InlineData(null, AtcBookingKind.Controlling)]
    [InlineData("", AtcBookingKind.Controlling)]
    public void WhatABookingIsForIsReadFromItsTraining(string? training, AtcBookingKind kind)
    {
        var value = training is null ? "null" : $"\"{training}\"";
        using var answer = JsonDocument.Parse($$"""
            { "atcPosition": "LIRF_TWR", "training": {{value}}, "startDate": "2001-01-01T18:00:00.000Z",
              "endDate": "2001-01-01T20:00:00.000Z", "user": { "id": 761079 } }
            """);

        Assert.Equal(kind, IvaoAtcBookingReader.ReadBooking(answer.RootElement)!.Kind);
    }

    // ---- the real client, against an IVAO that answers however the test says ------------------------------------------------

    [Fact]
    public async Task TheClientAsksForTheDayAndThePositionWithTheApplicationsToken()
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var ivao = new Ivao(_ => Json(IvaoFixtures.Read("atc-bookings-day.json").GetRawText()));

        var bookings = await Client(ivao, cache).GetDailyAtcBookingsAsync(Recorded, " lirf_twr ", token);

        var asked = Assert.Single(ivao.Asked);
        Assert.Equal("/v2/atc/bookings/daily?date=2001-01-01&position=LIRF_TWR", asked.PathAndQuery);
        Assert.Equal("Bearer application-token", asked.Authorization);

        // The client hands on what IVAO answered: which of it is the position asked for is the source's to say.
        Assert.Equal(10, bookings!.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.BadGateway, "<html>maintenance</html>")]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"You are not authenticated to access this resource","statusCode":401}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"message":"Validation failed (invalid date format)","statusCode":400}""")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    [InlineData(HttpStatusCode.OK, """{"message":"an object where a list was"}""")]
    public async Task AnIvaoThatFailsAnswersNotAvailable(HttpStatusCode status, string body)
    {
        // «Not available», and never an empty list: «nobody booked» would be the page answering a question it could not ask.
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var bookings = await Client(new Ivao(_ => Json(body, status)), cache).GetDailyAtcBookingsAsync(Recorded, cancellationToken: token);

        Assert.Null(bookings);
    }

    [Fact]
    public async Task AnIvaoThatCannotBeReachedAnswersNotAvailableAndThrowsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var bookings = await Client(new Ivao(_ => throw new HttpRequestException("IVAO is down")), cache)
            .GetDailyAtcBookingsAsync(Recorded, cancellationToken: token);

        Assert.Null(bookings);
    }

    [Fact]
    public async Task WithoutTheApplicationsTokenIvaoIsNotAskedAndTheAnswerIsNotAvailable()
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var ivao = new Ivao(_ => Json("[]"), refusesTheToken: true);

        Assert.Null(await Client(ivao, cache).GetDailyAtcBookingsAsync(Recorded, cancellationToken: token));
        Assert.Empty(ivao.Asked);
    }

    // ---- the fixture client: the recorded day, on whichever day the bench asks ----------------------------------------------

    [Fact]
    public async Task TheBenchListsTheRecordedDayOnAnyDayTheWayIvaoListsADay()
    {
        var token = TestContext.Current.CancellationToken;
        using var hub = Hub();
        using var scope = hub.CreateScope();
        var fixtures = Assert.IsType<FixtureIvaoApiClient>(scope.ServiceProvider.GetRequiredService<IIvaoApiClient>());

        var bookings = await fixtures.GetDailyAtcBookingsAsync(AnyDay, cancellationToken: token);

        // The ten, moved onto the day with their times of day…
        var tower = Assert.Single(bookings!, booking => booking.Callsign == "LIRF_TWR");
        Assert.Equal((At(AnyDay, 18), At(AnyDay, 20), 761079), (tower.StartsAt, tower.EndsAt, tower.Vid));

        // …and the one across midnight twice, as IVAO lists it: the day's own, and the one of the night before, still open when
        // the day began.
        Assert.Equal(11, bookings!.Count);
        Assert.Equal(
            [At(AnyDay.AddDays(-1), 23), At(AnyDay, 23)],
            bookings.Where(booking => booking.Callsign == "SBGR_TWR").Select(booking => booking.StartsAt).Order());
    }

    [Fact]
    public async Task TheBenchMatchesAPositionTheWayIvaoDoesByTheStartOfTheCallsign()
    {
        var token = TestContext.Current.CancellationToken;
        using var hub = Hub();
        using var scope = hub.CreateScope();
        var fixtures = scope.ServiceProvider.GetRequiredService<IIvaoApiClient>();

        var rome = await fixtures.GetDailyAtcBookingsAsync(AnyDay, "lirr", token);
        var tower = await fixtures.GetDailyAtcBookingsAsync(AnyDay, "LIRF_TWR", token);
        var kind = await fixtures.GetDailyAtcBookingsAsync(AnyDay, "TWR", token);

        Assert.Equal(["LIRR_NC_CTR", "LIRR_NE_CTR", "LIRR_SU_CTR"], rome!.Select(booking => booking.Callsign).Order());
        Assert.Equal("LIRF_TWR", Assert.Single(tower!).Callsign);
        Assert.Empty(kind!); // the end of a callsign is not a position
    }

    // ---- the source: what a module asks -------------------------------------------------------------------------------------

    [Fact]
    public async Task AWindowGetsTheBookingsThatOverlapItInTheOrderTheyStart()
    {
        var token = TestContext.Current.CancellationToken;
        using var hub = Hub();
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IAtcBookingSource>();

        var evening = await source.BookedAsync(At(AnyDay, 18, 30), At(AnyDay, 21), cancellationToken: token);

        // All of the evening's but the one that opens at 23:00, and the one of the night before, closed at 01:00.
        Assert.Equal(9, evening!.Count);
        Assert.DoesNotContain(evening, booking => booking.Callsign == "SBGR_TWR");
        Assert.Equal(evening.OrderBy(booking => booking.StartsAt).ThenBy(booking => booking.Callsign, StringComparer.Ordinal), evening);
        Assert.Equal("LIRF_TWR", evening[0].Callsign);
    }

    [Fact]
    public async Task ABookingAcrossMidnightComesOnceThoughIvaoListsItOnBothDays()
    {
        var token = TestContext.Current.CancellationToken;
        using var hub = Hub();
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IAtcBookingSource>();

        // From 22:00 to 02:00: two days of IVAO. Rome's north-east sector closes at 22:00, when the window opens, and is not in it.
        var night = await source.BookedAsync(At(AnyDay, 22), At(AnyDay.AddDays(1), 2), cancellationToken: token);

        Assert.Equal(
            new AtcBookingDto("SBGR_TWR", At(AnyDay, 23), At(AnyDay.AddDays(1), 1), 761077, AtcBookingKind.Controlling),
            Assert.Single(night!));
    }

    [Fact]
    public async Task APositionIsItsWholeCallsignInAnyCase()
    {
        var token = TestContext.Current.CancellationToken;
        using var hub = Hub();
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IAtcBookingSource>();

        var sector = await source.BookedAsync(At(AnyDay, 18), At(AnyDay, 22), " lirr_ne_ctr ", token);
        var start = await source.BookedAsync(At(AnyDay, 18), At(AnyDay, 22), "LIRR", token);

        Assert.Equal(761075, Assert.Single(sector!).Vid);

        // For IVAO «LIRR» is the start of three callsigns; for a module it is the callsign of no position.
        Assert.Empty(start!);
    }

    [Fact]
    public async Task ADayThatCannotBeReadMakesTheWholeAnswerNotAvailable()
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var ivao = new Ivao(request => request.RequestUri!.Query.Contains("2001-01-02", StringComparison.Ordinal)
            ? Json("<html>maintenance</html>", HttpStatusCode.BadGateway)
            : Json(IvaoFixtures.Read("atc-bookings-day.json").GetRawText()));
        using var hub = Hub(Client(ivao, cache));
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IAtcBookingSource>();

        // The evening is one day, and it answers; the night is two, and half a night would say that a booking is not there.
        Assert.Equal(9, (await source.BookedAsync(At(Recorded, 18, 30), At(Recorded, 21), cancellationToken: token))!.Count);
        Assert.Null(await source.BookedAsync(At(Recorded, 22), At(Recorded.AddDays(1), 2), cancellationToken: token));
    }

    [Fact]
    public async Task AWeekIsAskedDayByDayAndAWiderOrEmptyWindowIsNotAskedAtAll()
    {
        var token = TestContext.Current.CancellationToken;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var ivao = new Ivao(_ => Json("[]"));
        using var hub = Hub(Client(ivao, cache));
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IAtcBookingSource>();
        var monday = At(new DateOnly(2031, 3, 10), 0);

        Assert.Empty((await source.BookedAsync(monday, monday.AddDays(IAtcBookingSource.MaxDays), cancellationToken: token))!);
        Assert.Equal(
            Enumerable.Range(10, IAtcBookingSource.MaxDays).Select(day => $"?date=2031-03-{day}"),
            ivao.Asked.Select(asked => asked.Query));

        Assert.Null(await source.BookedAsync(monday, monday.AddDays(IAtcBookingSource.MaxDays).AddMinutes(1), cancellationToken: token));
        Assert.Empty((await source.BookedAsync(monday, monday, cancellationToken: token))!);
        Assert.Empty((await source.BookedAsync(monday.AddHours(1), monday, cancellationToken: token))!);
        Assert.Equal(IAtcBookingSource.MaxDays, ivao.Asked.Count);
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------------

    private static DateTime At(DateOnly day, int hour, int minute = 0) =>
        day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static IvaoApiClient Client(Ivao ivao, MemoryCache cache) =>
        new(
            new HttpClient(ivao) { BaseAddress = new Uri("https://api.example.org") },
            new IvaoApiTokenProvider(
                new HttpClient(ivao) { BaseAddress = new Uri("https://api.example.org") },
                Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" }),
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            NullLogger<IvaoApiClient>.Instance);

    /// <summary>
    /// The core's own wiring on a development machine with <c>Ivao:UseFixtures</c> — the source is internal, as the directories
    /// are —, or with the client the test gives it.
    /// </summary>
    private static ServiceProvider Hub(IIvaoApiClient? client = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [IvaoServiceCollectionExtensions.UseFixturesKey] = "true" })
            .Build());
        services.AddSingleton<IHostEnvironment>(new Development());
        services.AddSingleton(HubPaths.Resolve(AppContext.BaseDirectory));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddIvaoIntegration();

        if (client is not null)
        {
            services.AddSingleton(client);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>What one request to the bookings looked like: where it went, and with which token.</summary>
    private sealed record Request(string PathAndQuery, string Query, string? Authorization);

    /// <summary>An IVAO that hands out the application's token and answers the bookings however the test says.</summary>
    private sealed class Ivao(Func<HttpRequestMessage, HttpResponseMessage> bookings, bool refusesTheToken = false)
        : HttpMessageHandler
    {
        public List<Request> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/v2/oauth/token")
            {
                return Task.FromResult(refusesTheToken
                    ? Json("""{"error":"invalid_client"}""", HttpStatusCode.Unauthorized)
                    : Json("""{"access_token":"application-token","expires_in":1800,"token_type":"Bearer"}"""));
            }

            Asked.Add(new Request(uri.PathAndQuery, uri.Query, request.Headers.Authorization?.ToString()));
            return Task.FromResult(bookings(request));
        }
    }

    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "IvaoHub.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
