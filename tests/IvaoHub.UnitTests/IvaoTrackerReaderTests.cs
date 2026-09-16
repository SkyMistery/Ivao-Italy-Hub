using System.Text.Json;
using IvaoHub.Core.Ivao;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The tracker read against real flights. The fixtures are three legs of one member's trip, recorded
/// from the live API with <c>tools/record-ivao-fixtures.mjs</c> and anonymised to VID 780001: a
/// parser proved against invented JSON proves only that the invention was parsed.
/// </summary>
public sealed class IvaoTrackerReaderTests
{
    private const long Session = 62748566; // LPMA to LPBJ, 123 minutes

    [Fact]
    public void ASessionCarriesWhereItWentWithoutASecondCall()
    {
        var (sessions, pages) = IvaoTrackerReader.ReadSessions(Fixture("tracker-sessions-780001.json"));

        Assert.Equal(3, sessions.Count);
        Assert.Equal(1, pages); // a bare array is one page

        var session = Assert.Single(sessions, row => row.Id == Session);
        Assert.Equal(780001, session.Vid);
        Assert.True(session.HasFlightPlan);
        Assert.Equal("LPMA", session.DepartureIcao);
        Assert.Equal("LPBJ", session.ArrivalIcao);
        Assert.Equal("E55P", session.AircraftIcao);

        // The declared length is the authority, not the difference between two timestamps.
        Assert.Equal(TimeSpan.FromSeconds(7363), session.Duration);
        Assert.Equal(session.StartedAt + session.Duration, session.EndedAt);
    }

    [Fact]
    public void APagedAnswerSaysHowManyPagesThereAre()
    {
        using var document = JsonDocument.Parse(
            """{"items":[],"totalItems":119,"perPage":50,"page":1,"pages":3}""");

        var (sessions, pages) = IvaoTrackerReader.ReadSessions(document.RootElement);

        Assert.Empty(sessions);
        Assert.Equal(3, pages);
    }

    [Fact]
    public void EveryRevisionOfThePlanComesBackInOrder()
    {
        var plans = IvaoTrackerReader.ReadFlightPlans(Fixture($"tracker-flightplans-{Session}.json"));

        Assert.Equal(3, plans.Count);
        Assert.Equal([1, 2, 3], plans.Select(plan => plan.Revision));
        Assert.True(plans[0].FiledAt <= plans[^1].FiledAt);

        var last = plans[^1];
        Assert.Equal("LPMA", last.DepartureIcao);
        Assert.Equal("LPBJ", last.ArrivalIcao);
        Assert.Equal("I", last.FlightRules);
        Assert.Equal("E55P", last.AircraftIcao);

        // IVAO expands the letters into objects; a flight plan is written with letters — and with
        // the digits some of them carry (J1 is CPDLC over VDL, measured on this very flight).
        Assert.Equal("SBDFGJ1RUWXY", last.Equipment);
        Assert.NotEmpty(last.Transponder);

        // The raw payload travels with the revision: the module stores all of them as they came.
        Assert.Contains("\"revision\"", last.RawJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTrackIsInTimeOrderAndSaysWhatTheAircraftWasDoing()
    {
        var points = IvaoTrackerReader.ReadTracks(Fixture($"tracker-tracks-{Session}.json"));

        Assert.True(points.Count > 500);
        Assert.Equal(points.OrderBy(point => point.At), points);
        Assert.Contains(points, point => point.OnGround);
        Assert.Contains(points, point => !point.OnGround);
        Assert.Contains(points, point => point.GroundSpeedKnots > 100);
        Assert.All(points, point => Assert.InRange(point.Latitude, -90, 90));
        Assert.All(points, point => Assert.InRange(point.Longitude, -180, 180));

        // Measured on the real flights: about fifteen seconds between points, never a minute.
        var gaps = points.Zip(points.Skip(1), (a, b) => (b.At - a.At).TotalSeconds).ToArray();
        Assert.True(gaps.Max() < 60, $"the longest gap was {gaps.Max():F0} s");
        Assert.InRange(gaps.Order().ElementAt(gaps.Length / 2), 1, 30);
    }

    [Fact]
    public void RowsThatAreNotSessionsAreSkippedRatherThanThrownOver()
    {
        using var document = JsonDocument.Parse("""[{"id":1},{"userId":2},"nonsense",{"id":3,"userId":4}]""");

        var (sessions, _) = IvaoTrackerReader.ReadSessions(document.RootElement);

        var session = Assert.Single(sessions);
        Assert.Equal(3, session.Id);
        Assert.False(session.HasFlightPlan);
        Assert.Null(session.DepartureIcao);
    }

    [Fact]
    public void AQueryAsksForOneMemberInOneWindow()
    {
        var query = new IvaoSessionQuery(
            780001,
            new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 8, 6, 0, 0, DateTimeKind.Utc),
            DepartureIcao: "lirf");

        var text = query.ToQueryString();

        Assert.Contains("userId=780001", text, StringComparison.Ordinal);
        Assert.Contains("departureId=LIRF", text, StringComparison.Ordinal);
        Assert.DoesNotContain("arrivalId", text, StringComparison.Ordinal);
        Assert.Contains("2026-09-01T06%3A00%3A00", text, StringComparison.Ordinal);
    }

    private static JsonElement Fixture(string name)
    {
        var path = Path.Combine(RepositoryRoot(), FixtureIvaoApiClient.Directory, name);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
