using System.Text.Json;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The sessions of the network under a neutral name (M4, E10t): what a module asks, passed whole to the one client's search
/// (E10a), and what the client answers, cut down to what a module reads. The source is taken from the services the core
/// registers, in front of a client that records the question and answers however the test says — or of the fixture client,
/// with the recorded evening at Rome Fiumicino.
/// </summary>
public sealed class NetworkSessionSourceTests
{
    private static readonly DateTime From = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2001, 1, 1, 17, 59, 59, DateTimeKind.Utc);

    [Theory]
    [InlineData(761070, "LIRF", null, NetworkConnection.Pilot, IvaoConnectionType.Pilot, 1000)]
    [InlineData(null, null, "LIRF", NetworkConnection.Pilot, IvaoConnectionType.Pilot, 450)]
    [InlineData(761071, null, null, NetworkConnection.Atc, IvaoConnectionType.Atc, 1)]
    [InlineData(null, "LIPZ", "LIRF", null, null, NetworkSessionQuery.DefaultLimit)]
    public async Task TheQuestionReachesTheClientWhole(
        int? vid,
        string? departure,
        string? arrival,
        NetworkConnection? connection,
        IvaoConnectionType? type,
        int limit)
    {
        var client = new Client(_ => []);
        using var hub = Hub(client);
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<INetworkSessionSource>();

        var query = new NetworkSessionQuery(vid, From, To, departure, arrival, connection);
        if (limit != NetworkSessionQuery.DefaultLimit)
        {
            query = query with { Limit = limit };
        }

        Assert.Empty((await source.SessionsAsync(query, TestContext.Current.CancellationToken))!);

        var asked = Assert.Single(client.Asked);
        Assert.Equal(new IvaoSessionQuery(vid, From, To, departure, arrival, type) { Limit = limit }, asked);
    }

    [Fact]
    public async Task ASessionComesWithItsNumberItsMemberItsCallsignAndItsTwoEnds()
    {
        var started = new DateTime(2001, 1, 1, 16, 20, 0, DateTimeKind.Utc);
        var client = new Client(_ => [Session(1000042, 761072, "AZA123", started, TimeSpan.FromMinutes(95))]);
        using var hub = Hub(client);
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<INetworkSessionSource>();

        var sessions = await source.SessionsAsync(new NetworkSessionQuery(null, From, To, "LIRF"), TestContext.Current.CancellationToken);

        Assert.Equal(
            new NetworkSession(1000042, 761072, "AZA123", started, started.AddMinutes(95)),
            Assert.Single(sessions!));
    }

    [Fact]
    public async Task TheAnswerIsNewestFirstEachOnceAndNoLongerThanTheLimit()
    {
        var at = new DateTime(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);
        var client = new Client(_ =>
        [
            Session(1, 761073, "AAA1", at.AddMinutes(10), TimeSpan.FromHours(1)),
            Session(3, 761074, "AAA3", at.AddMinutes(30), TimeSpan.FromHours(1)),
            Session(3, 761074, "AAA3", at.AddMinutes(30), TimeSpan.FromHours(1)),
            Session(2, 761075, "AAA2", at.AddMinutes(20), TimeSpan.FromHours(1)),
        ]);
        using var hub = Hub(client);
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<INetworkSessionSource>();
        var token = TestContext.Current.CancellationToken;

        var all = await source.SessionsAsync(new NetworkSessionQuery(null, From, To, "LIRF"), token);
        var two = await source.SessionsAsync(new NetworkSessionQuery(null, From, To, "LIRF") { Limit = 2 }, token);

        Assert.Equal([3L, 2L, 1L], all!.Select(session => session.Id));
        Assert.Equal([3L, 2L], two!.Select(session => session.Id));
    }

    [Fact]
    public async Task ANetworkThatCannotBeAskedIsNotAnEmptyList()
    {
        using var hub = Hub(new Client(_ => null));
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<INetworkSessionSource>();

        Assert.Null(await source.SessionsAsync(new NetworkSessionQuery(761076, From, To), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ALimitOutsideTheCoresCeilingIsRefused()
    {
        var query = new NetworkSessionQuery(null, From, To, "LIRF");

        Assert.Equal(IvaoSessionQuery.DefaultLimit, query.Limit);
        Assert.Equal(IvaoSessionQuery.MaxLimit, NetworkSessionQuery.MaxLimit);
        Assert.Equal(NetworkSessionQuery.MaxLimit, (query with { Limit = NetworkSessionQuery.MaxLimit }).Limit);
        Assert.Throws<ArgumentOutOfRangeException>(() => query with { Limit = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => query with { Limit = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => query with { Limit = NetworkSessionQuery.MaxLimit + 1 });
    }

    [Fact]
    public async Task TheFixtureClientAnswersTheSourceAsItAnswersItsOwnSearch()
    {
        var token = TestContext.Current.CancellationToken;
        using var hub = Hub();
        using var scope = hub.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<INetworkSessionSource>();
        var client = Assert.IsType<FixtureIvaoApiClient>(scope.ServiceProvider.GetRequiredService<IIvaoApiClient>());

        var arrivals = await source.SessionsAsync(
            new NetworkSessionQuery(null, From, To, ArrivalIcao: "LIRF", Connection: NetworkConnection.Pilot),
            token);
        var recorded = await client.SearchSessionsAsync(
            new IvaoSessionQuery(null, From, To, ArrivalIcao: "LIRF", ConnectionType: IvaoConnectionType.Pilot),
            token);

        Assert.NotEmpty(arrivals!);
        Assert.Equal(
            recorded!.Select(session => new NetworkSession(session.Id, session.Vid, session.Callsign, session.StartedAt, session.EndedAt)),
            arrivals!);
    }

    private static IvaoTrackerSessionDto Session(long id, int vid, string callsign, DateTime startedAt, TimeSpan duration) =>
        new(id, vid, callsign, startedAt, duration, true, "LIRF", "LICR", "A320", "{}") { ConnectionType = IvaoConnectionType.Pilot };

    /// <summary>The services of the core, with the fixture client unless the test brings its own.</summary>
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

    /// <summary>A client that keeps every search it is asked and answers it however the test says; it knows nothing else.</summary>
    private sealed class Client(Func<IvaoSessionQuery, IReadOnlyList<IvaoTrackerSessionDto>?> answer) : IIvaoApiClient
    {
        public List<IvaoSessionQuery> Asked { get; } = [];

        public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(
            IvaoSessionQuery query,
            CancellationToken cancellationToken = default)
        {
            Asked.Add(query);
            return Task.FromResult(answer(query));
        }

        public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(
            string? countryId,
            bool includeRunways = true,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(string icao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)>
            GetFlightPlanVocabulariesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(long sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(long sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IvaoNetworkStatus> GetNetworkStatusAsync(IvaoAirspace airspace, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "IvaoHub.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
