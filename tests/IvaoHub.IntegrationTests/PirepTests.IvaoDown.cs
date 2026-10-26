using System.Net;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A report sent while IVAO does not answer (issue #324): the tracker finds the flight, and then the real IVAO client fails —
/// the flight plan and the track of the session, or the runways of the airports once the report is saved. Either is the
/// module's own answer, a refusal or a report in the queue, and never a 500: before #324 the client threw on those calls,
/// and a report already saved answered 500 to a pilot who could not send it again.
/// </summary>
public sealed partial class PirepTests
{
    [Fact]
    public async Task AFlightIvaoCannotGiveTheTrackOfIsRefusedAsTrackerUnavailable()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var flown = _flights.Add(PilotVid, "XAA410", Rome, Milan, DateTime.UtcNow.AddDays(-1));

        await using var host = IvaoDownHost(trackerToo: true);
        using var pilot = await SignedInAsync(host, PilotVid, token);

        await RefusedAsync(pilot, Reports(tourId), Payload(legs[0], flown), "sessionIds", "flightops:errors.trackerUnavailable", token);
    }

    [Fact]
    public async Task AReportSentWhileIvaoCannotGiveTheRunwaysIsQueued()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var flown = _flights.Add(PilotVid, "XAA420", Rome, Milan, DateTime.UtcNow.AddDays(-1));

        await using var host = IvaoDownHost(trackerToo: false);
        using var pilot = await SignedInAsync(host, PilotVid, token);

        var sent = await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token);
        Assert.Equal("Queued", sent.GetProperty("status").GetString());

        await using var scope = _host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
        Assert.True(await Everything<Pirep>(database).AnyAsync(report => report.Id == Id(sent), token));
    }

    /// <summary>
    /// The host of these tests with an IVAO that is down behind the real client: the tracker's search still answers from
    /// the shelf, so the send gets as far as the calls the test is about.
    /// </summary>
    private WebApplicationFactory<Program> IvaoDownHost(bool trackerToo) =>
        _host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IIvaoApiClient>(provider => new IvaoDown(
                new TrackerDouble(provider.GetRequiredService<FixtureIvaoApiClient>(), _flights),
                DownClient(),
                trackerToo))));

    /// <summary>The real IVAO client in front of an IVAO that gives its token and nothing else.</summary>
    private static IvaoApiClient DownClient()
    {
        var down = new Unreachable();
        var cache = new MemoryCache(new MemoryCacheOptions());

        return new IvaoApiClient(
            new HttpClient(down) { BaseAddress = new Uri("https://api.example.org") },
            new IvaoApiTokenProvider(
                new HttpClient(down) { BaseAddress = new Uri("https://api.example.org") },
                Options.Create(new IvaoOAuthOptions { ClientId = "test", ClientSecret = "test" }),
                cache,
                NullLogger<IvaoApiTokenProvider>.Instance),
            cache,
            IvaoNetworkPictureStore.None,
            NullLogger<IvaoApiClient>.Instance);
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri!.AbsolutePath == "/v2/oauth/token"
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ \"access_token\": \"token\", \"expires_in\": 3600 }", Encoding.UTF8, "application/json"),
                })
                : throw new HttpRequestException("No such host is known.");
    }

    /// <summary>The search from the shelf; the runways, the METARs and, when asked, the flight's plan and track from IVAO down.</summary>
    private sealed class IvaoDown(TrackerDouble tracker, IvaoApiClient down, bool trackerToo) : IIvaoApiClient
    {
        public Task<IReadOnlyList<IvaoTrackerSessionDto>?> SearchSessionsAsync(IvaoSessionQuery query, CancellationToken cancellationToken = default) =>
            tracker.SearchSessionsAsync(query, cancellationToken);

        public Task<IReadOnlyList<IvaoFlightPlanDto>?> GetFlightPlansAsync(long sessionId, CancellationToken cancellationToken = default) =>
            trackerToo ? down.GetFlightPlansAsync(sessionId, cancellationToken) : tracker.GetFlightPlansAsync(sessionId, cancellationToken);

        public Task<IReadOnlyList<IvaoTrackPointDto>?> GetTracksAsync(long sessionId, CancellationToken cancellationToken = default) =>
            trackerToo ? down.GetTracksAsync(sessionId, cancellationToken) : tracker.GetTracksAsync(sessionId, cancellationToken);

        public Task<IReadOnlyList<IvaoRunway>?> GetRunwaysAsync(string icao, CancellationToken cancellationToken = default) =>
            down.GetRunwaysAsync(icao, cancellationToken);

        public Task<IvaoMetarDto?> GetMetarAsync(string icao, CancellationToken cancellationToken = default) =>
            down.GetMetarAsync(icao, cancellationToken);

        public Task<IReadOnlyList<IvaoCenterDto>> GetCentersAsync(string countryId, CancellationToken cancellationToken = default) =>
            tracker.GetCentersAsync(countryId, cancellationToken);

        public Task<IReadOnlyList<IvaoAirportDto>> GetAirportsAsync(string? countryId, bool includeRunways = true, CancellationToken cancellationToken = default) =>
            tracker.GetAirportsAsync(countryId, includeRunways, cancellationToken);

        public Task<IReadOnlyList<IvaoAircraftType>> GetAircraftTypesAsync(CancellationToken cancellationToken = default) =>
            tracker.GetAircraftTypesAsync(cancellationToken);

        public Task<(IReadOnlyList<IvaoAircraftEquipment> Equipments, IReadOnlyList<IvaoTransponderType> Transponders)> GetFlightPlanVocabulariesAsync(
            CancellationToken cancellationToken = default) =>
            tracker.GetFlightPlanVocabulariesAsync(cancellationToken);

        public Task<JsonElement?> GetMeAsync(string accessToken, CancellationToken cancellationToken = default) =>
            tracker.GetMeAsync(accessToken, cancellationToken);

        public Task<IvaoNetworkStatus> GetNetworkStatusAsync(IvaoAirspace airspace, CancellationToken cancellationToken = default) =>
            tracker.GetNetworkStatusAsync(airspace, cancellationToken);
    }
}
