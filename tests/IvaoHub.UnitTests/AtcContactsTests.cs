using System.Text.Json;
using IvaoHub.Core.Airspace;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The controllers contacted and the exemptions (design M2 §3.3, T12): the proposal on a recorded flight with an archive
/// built around it, what the report keeps of what the pilot declared, the status of an exemption, and the refusals.
/// </summary>
public sealed class AtcContactsTests
{
    private static readonly DateTime Division = new(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime World = new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);

    // ─── The proposal ─────────────────────────────────────────────────────────

    /// <summary>
    /// The recorded LIRQ → LXGB flight (session 62747397): the tower of the departure while it was on the ground, the centres
    /// open when the track was in their region, the tower of the arrival when it landed — and nothing else: not a position
    /// of another airport, not a centre that opened after the flight had left its region, not the departure's ground an
    /// hour after take-off.
    /// </summary>
    [Fact]
    public async Task ARecordedFlightIsProposedThePositionsItMetWhenItMetThem()
    {
        var flight = RecordedFlight();
        var takeoff = flight.TakeoffAt!.Value;
        var landing = flight.LandingAt!.Value;
        var inItaly = flight.Track.Last(point => !point.OnGround && Regions.Of(point.Longitude) == "LIMM").At;

        var archive = new ArchiveDouble(
            new AtcPresence("LIRQ_TWR", "119.155", takeoff.AddHours(-1), takeoff.AddMinutes(5)),
            new AtcPresence("LIRQ_GND", "121.905", takeoff.AddHours(1), takeoff.AddHours(2)),
            new AtcPresence("LIMM_CTR", "134.205", takeoff.AddMinutes(-30), inItaly),
            new AtcPresence("LFMM_CTR", "127.030", landing, null),
            new AtcPresence("LECB_CTR", "132.705", flight.Session.StartedAt, flight.Session.EndedAt),
            new AtcPresence("LXGB_TWR", "122.805", landing.AddMinutes(-10), null),
            new AtcPresence("LIRF_TWR", "118.705", flight.Session.StartedAt, flight.Session.EndedAt));

        var (activity, proposed) = await new AtcProposer(archive, new Regions()).ProposeAsync([flight], null, TestContext.Current.CancellationToken);

        Assert.NotNull(activity);
        Assert.Equal(["LIRQ_TWR", "LIMM_CTR", "LECB_CTR", "LXGB_TWR"], proposed.Select(presence => presence.Callsign));
        Assert.Equal("119.155", proposed[0].Frequency);
    }

    /// <summary>A division without an archive: nothing is proposed, and the answer says so rather than «nobody was online».</summary>
    [Fact]
    public async Task WithoutAnArchiveNothingIsProposedAndTheAnswerIsNotAvailable()
    {
        var proposer = new AtcProposer(new UnavailableAtcActivitySource(), new Regions());

        var (activity, proposed) = await proposer.ProposeAsync([RecordedFlight()], null, TestContext.Current.CancellationToken);

        Assert.Null(activity);
        Assert.Empty(proposed);
        Assert.Equal(Regions.Credit, proposer.Attribution);
    }

    /// <summary>A diversion: the first flight's positions are the diversion airport's, not the planned arrival's.</summary>
    [Fact]
    public void TheFirstFlightOfADiversionLandedAtTheDiversionAirport()
    {
        var start = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        var first = new FlightPassage("LIRF", "LIRN", start, start.AddHours(2), start.AddMinutes(15), start.AddMinutes(100), []);
        var second = new FlightPassage("LIRN", "LICC", start.AddHours(4), start.AddHours(5), start.AddHours(4).AddMinutes(10), start.AddMinutes(290), []);
        var activity = Activity(
            new AtcPresence("LIRN_APP", null, start.AddMinutes(80), start.AddHours(6)),
            new AtcPresence("LICC_TWR", null, start.AddMinutes(270), null),
            new AtcPresence("LICC_APP", null, start.AddMinutes(60), start.AddMinutes(120)));

        var proposed = AtcProposal.Propose([first, second], activity);

        Assert.Equal(["LIRN_APP", "LICC_TWR"], proposed.Select(presence => presence.Callsign));
    }

    // ─── What the report keeps ────────────────────────────────────────────────

    /// <summary>
    /// Every proposed position stays on the report — kept, or removed by the pilot (Carmine, 23 September 2026) — and the
    /// pilot's own come after, normalised; a «proposed» written by the browser proves nothing.
    /// </summary>
    [Fact]
    public void TheReportKeepsTheProposedTheRemovedAndTheAdded()
    {
        var at = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        AtcPresence[] proposed =
        [
            new("LIRF_TWR", "118.705", at, null),
            new("LIRR_CTR", "125.500", at, null),
        ];

        var merged = AtcProposal.Merge(
            proposed,
            [new(" lirf_twr ", null), new("LIMM_CTR", "134,205"), new("limm_ctr", null)]);

        Assert.Equal(
            [
                new AtcContactDto("LIRF_TWR", "118.705", AtcContactOrigin.Proposed),
                new AtcContactDto("LIRR_CTR", "125.500", AtcContactOrigin.Removed),
                new AtcContactDto("LIMM_CTR", "134.205", AtcContactOrigin.Added),
            ],
            merged);
    }

    /// <summary>
    /// «Online» when the archive lists it during the flights; «not online» only where the archive is complete for the whole
    /// interval — the division's positions from its start, the world's from later —; «unverifiable» otherwise.
    /// </summary>
    [Fact]
    public void AnExemptionIsOnlineNotOnlineOrUnverifiable()
    {
        var from = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        var to = from.AddHours(2);
        var activity = Activity(new AtcPresence("LIRR_CTR", null, from.AddHours(-3), from.AddMinutes(1)));

        Assert.Equal(ExemptionStatus.Online, AtcProposal.StatusOf("lirr_ctr", from, to, activity));
        Assert.Equal(ExemptionStatus.NotOnline, AtcProposal.StatusOf("LIMM_CTR", from, to, activity));
        Assert.Equal(ExemptionStatus.NotOnline, AtcProposal.StatusOf("LFMM_CTR", from, to, activity));
        Assert.Equal(ExemptionStatus.Unverifiable, AtcProposal.StatusOf("LIRR_CTR", from, to, null));

        // Before the archive kept the rest of the world, a foreign position that is not listed may simply not be kept.
        var august = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ExemptionStatus.Unverifiable, AtcProposal.StatusOf("LFMM_CTR", august, august.AddHours(2), activity));
        Assert.Equal(ExemptionStatus.NotOnline, AtcProposal.StatusOf("LIMM_CTR", august, august.AddHours(2), activity));
    }

    /// <summary>The perimeter Carmine chose on 23 September 2026: each kind softens its own check, and two soften none.</summary>
    [Fact]
    public void EachKindOfExemptionSoftensOnlyItsOwnChecks()
    {
        Assert.Equal([CheckCatalog.Speed250], ExemptionCatalog.Softens(ExemptionKind.FreeSpeed));
        Assert.Equal([CheckCatalog.SemicircularLevels], ExemptionCatalog.Softens(ExemptionKind.LevelChange));
        Assert.Empty(ExemptionCatalog.Softens(ExemptionKind.DirectRouting));
        Assert.Empty(ExemptionCatalog.Softens(ExemptionKind.Other));
    }

    /// <summary>What the pilot writes is refused under its field when it cannot be a callsign, a frequency or an exemption.</summary>
    [Fact]
    public void WhatThePilotDeclaresIsRefusedWhereItIsWrong()
    {
        Assert.Empty(AtcProposal.Problems(
            [new("LIRF_TWR", "118.705"), new("LIRR_N_CTR", null)],
            [new("lirf_twr", ExemptionKind.FreeSpeed, null), new("LIRR_N_CTR", ExemptionKind.Other, "Vectors for spacing")]));

        Assert.Contains(("atcContacts", "flightops:errors.atcCallsign"), AtcProposal.Problems([new("Rome tower", null)], []));
        Assert.Contains(("atcContacts", "flightops:errors.atcFrequency"), AtcProposal.Problems([new("LIRF_TWR", "1187")], []));
        Assert.Contains(
            ("exemptions", "flightops:errors.exemptionNotContacted"),
            AtcProposal.Problems([new("LIRF_TWR", null)], [new("LIRR_CTR", ExemptionKind.DirectRouting, null)]));
        Assert.Contains(
            ("exemptions", "flightops:errors.exemptionNoteRequired"),
            AtcProposal.Problems([new("LIRF_TWR", null)], [new("LIRF_TWR", ExemptionKind.Other, " ")]));
    }

    // ─── Builders ─────────────────────────────────────────────────────────────

    private static AtcActivity Activity(params AtcPresence[] online) => new(online, Division, World, ["LI"]);

    private static TrackedFlight RecordedFlight()
    {
        const long id = 62747397;
        var session = IvaoTrackerReader.ReadSessions(Fixture("tracker-sessions-780001.json")).Sessions.Single(row => row.Id == id);
        var plans = IvaoTrackerReader.ReadFlightPlans(Fixture($"tracker-flightplans-{id}.json"));
        var track = IvaoTrackerReader.ReadTracks(Fixture($"tracker-tracks-{id}.json"));
        return TrackedFlight.Read(session, plans, track);
    }

    private static JsonElement Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory!.FullName, FixtureIvaoApiClient.Directory, name);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    /// <summary>An archive that answers with what the test gives it, with the division complete and the world from 28 August.</summary>
    private sealed class ArchiveDouble(params AtcPresence[] online) : IAtcActivitySource
    {
        public Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<AtcActivity?>(Activity([.. online.Where(presence => presence.Overlaps(fromUtc, toUtc))]));
    }

    /// <summary>
    /// Three regions cut by longitude along the route from Florence to Gibraltar — rough, and enough: which point is in which
    /// polygon is <see cref="FirLocator"/>'s business and has its own tests.
    /// </summary>
    private sealed class Regions : IFirLocator
    {
        public const string Credit = "Test outlines";

        public string Attribution => Credit;

        public static string Of(double longitude) => longitude > 7 ? "LIMM" : longitude > 3 ? "LFMM" : "LECB";

        public Task<IReadOnlyList<string>> LocateAsync(double latitude, double longitude, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([Of(longitude)]);

        public Task<IReadOnlyList<string>> LocateAlongAsync(
            IReadOnlyList<(double Latitude, double Longitude)> points,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([.. points.Select(point => Of(point.Longitude)).Distinct()]);

        public Task<IReadOnlySet<string>> KnownAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>>(ids.ToHashSet());

        public void Invalidate()
        {
        }
    }
}
