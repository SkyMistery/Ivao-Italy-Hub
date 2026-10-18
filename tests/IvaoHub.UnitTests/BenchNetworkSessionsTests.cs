using System.Text.Json;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The sessions a person of the end to end bench is given (M4, E10w; note <c>decisions/2026-10-18-il-banco-fa-girare-un-job.md</c>):
/// the fixture client answers the template's, moved onto the moment asked, for that VID only, through the tracker's own rule — and,
/// with nothing given, exactly what it answered before.
/// </summary>
public sealed class BenchNetworkSessionsTests
{
    private const int Person = 999004;
    private const int Other = 999002;

    /// <summary>A moment of the bench's day, with seconds: the move keeps them.</summary>
    private static readonly DateTime At = new(2026, 10, 18, 14, 3, 20, DateTimeKind.Utc);

    /// <summary>In the template the flight leaves 50 minutes and 8 seconds after the tower opens, and each lasts its own time.</summary>
    private static readonly TimeSpan FlightAfter = new(0, 50, 8);

    private static readonly TimeSpan TowerFor = TimeSpan.FromSeconds(2454);
    private static readonly TimeSpan FlightFor = TimeSpan.FromSeconds(4312);

    [Fact]
    public async Task APersonGivenSessionsHasTheTemplatesMovedOntoTheMomentAsked()
    {
        var bench = new BenchNetworkSessions();
        bench.Give(Person, At);
        var client = Client(bench);
        var cancellation = TestContext.Current.CancellationToken;

        var found = await client.SearchSessionsAsync(new IvaoSessionQuery(Person, At.AddHours(-1), At.AddDays(1)), cancellation);

        Assert.NotNull(found);
        Assert.Equal(
            [
                (999004002L, Person, "TST001", IvaoConnectionType.Pilot, At + FlightAfter, At + FlightAfter + FlightFor, "LIRF", "LIMC"),
                (999004001L, Person, "LIRF_TWR", IvaoConnectionType.Atc, At, At + TowerFor, (string?)null, (string?)null),
            ],
            found.Select(session => (
                session.Id,
                session.Vid,
                session.Callsign,
                session.ConnectionType!.Value,
                session.StartedAt,
                session.EndedAt,
                session.DepartureIcao,
                session.ArrivalIcao)));

        // Every instant of the row is moved, the revisions' airports and the rest are the template's.
        using var raw = JsonDocument.Parse(found[0].RawJson);
        Assert.Equal("2026-10-18T16:05:20.000Z", raw.RootElement.GetProperty("completedAt").GetString());
        Assert.Equal(["LIMC", "LIMC"], raw.RootElement.GetProperty("flightPlans").EnumerateArray().Select(plan => plan.GetProperty("arrivalId").GetString()));
    }

    [Fact]
    public async Task TheTrackersRuleStillPicksAmongThem()
    {
        var bench = new BenchNetworkSessions();
        bench.Give(Person, At);
        var client = Client(bench);
        var cancellation = TestContext.Current.CancellationToken;

        // A controller's shift: the tower only.
        var atc = await client.SearchSessionsAsync(new IvaoSessionQuery(Person, At.AddHours(-12), At.AddHours(1), ConnectionType: IvaoConnectionType.Atc), cancellation);
        Assert.Equal([999004001L], atc!.Select(session => session.Id));

        // A flight between its two airports, on its day.
        var flight = await client.SearchSessionsAsync(new IvaoSessionQuery(Person, At.Date, At.Date.AddDays(1), "LIRF", "LIMC", IvaoConnectionType.Pilot), cancellation);
        Assert.Equal([999004002L], flight!.Select(session => session.Id));

        // A window that ends before the first starts holds none: the day before has nothing.
        Assert.Empty((await client.SearchSessionsAsync(new IvaoSessionQuery(Person, At.AddDays(-1), At.AddSeconds(-1)), cancellation))!);
    }

    [Fact]
    public async Task OnlyThePersonGivenThemHasThemAndTakenBackNobodyDoes()
    {
        var bench = new BenchNetworkSessions();
        bench.Give(Person, At);
        var client = Client(bench);
        var cancellation = TestContext.Current.CancellationToken;
        var day = (From: At.AddHours(-1), To: At.AddDays(1));

        Assert.Empty((await client.SearchSessionsAsync(new IvaoSessionQuery(Other, day.From, day.To), cancellation))!);

        // A second person, and a second call that moves the first's: each has their own numbers, on their own moment.
        bench.Give(Other, At.AddHours(2));
        bench.Give(Person, At.AddHours(1));
        Assert.Equal(
            [(999002001L, At.AddHours(2))],
            (await client.SearchSessionsAsync(new IvaoSessionQuery(Other, day.From, day.To, ConnectionType: IvaoConnectionType.Atc), cancellation))!
                .Select(session => (session.Id, session.StartedAt)));
        Assert.Equal(
            [(999004001L, At.AddHours(1))],
            (await client.SearchSessionsAsync(new IvaoSessionQuery(Person, day.From, day.To, ConnectionType: IvaoConnectionType.Atc), cancellation))!
                .Select(session => (session.Id, session.StartedAt)));

        Assert.True(bench.Take(Person));
        Assert.False(bench.Take(Person));
        Assert.Empty((await client.SearchSessionsAsync(new IvaoSessionQuery(Person, day.From, day.To), cancellation))!);
    }

    [Fact]
    public async Task WithoutTheBenchTheFixtureClientAnswersWhatItAlwaysDid()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var june = new IvaoSessionQuery(
            780001,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var alone = await Client(bench: null).SearchSessionsAsync(june, cancellation);
        var benched = await Client(new BenchNetworkSessions()).SearchSessionsAsync(june, cancellation);

        Assert.NotEmpty(alone!);
        Assert.Equal(alone!.Select(session => session.Id), benched!.Select(session => session.Id));

        // Nobody of the bench, nothing of the template, whatever the window.
        Assert.Empty((await Client(new BenchNetworkSessions()).SearchSessionsAsync(new IvaoSessionQuery(Person, At.AddYears(-30), At.AddYears(1)), cancellation))!);
    }

    [Fact]
    public void TheTemplateKeepsNoKeyOfAPerson()
    {
        // The template is the stand-in evening's tower and one of its flights (tracker-airport-LIRF.json): no VID, its own numbers,
        // the stand-in day.
        var path = Path.Combine(HubPaths.Resolve(AppContext.BaseDirectory).Root, FixtureIvaoApiClient.Directory, FixtureIvaoApiClient.BenchTemplate);
        using var template = JsonDocument.Parse(File.ReadAllText(path));
        var rows = template.RootElement.EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(0, row.GetProperty("userId").GetInt32());
            Assert.True(row.GetProperty("id").GetInt64() < 1000);
            Assert.StartsWith("2001-01-01T", row.GetProperty("createdAt").GetString(), StringComparison.Ordinal);
        });
    }

    private static FixtureIvaoApiClient Client(BenchNetworkSessions? bench) =>
        new(HubPaths.Resolve(AppContext.BaseDirectory), new Development(), NullLogger<FixtureIvaoApiClient>.Instance, bench);

    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "IvaoHub.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
