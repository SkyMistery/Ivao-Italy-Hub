using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Tours;
using IvaoHub.Modules.FlightOps.Weather;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Agent;

/// <summary>What sending results came to: done, refused field by field, not the reader's to decide, or no longer waiting.</summary>
public enum AgentWriteResult
{
    Done,
    Refused,
    Forbidden,
    NotWaiting,
}

/// <summary>
/// The agent's side of the reports (design M2 §6.6, T19b): the queue it works, a report as it reads it, and the results it sends.
/// The token opens the endpoints; what the agent may do on a report is the one handler's answer on the row, exactly as for its
/// member on the page — <c>Tours.Validate</c> on the tour, and never on their own report (§7.3). An agent's failed check suggests
/// the errors it is linked to, as the server's do (§6.1).
/// </summary>
public sealed partial class AgentDesk(
    FlightOpsDbContext database,
    PirepReview reviews,
    FlightChecks checks,
    ModuleSettingsStore settingsStore,
    IAirportDirectory airports,
    IRunwayDirectory runways,
    IAtcActivitySource archive,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>At most this many reports in the queue, the ones waiting longest: a backlog is worked off in turns.</summary>
    public const int MaxQueue = 200;

    public AgentContractDto Contract() => new(
        AgentContract.Current,
        AgentContract.Accepted,
        [.. CheckCatalog.Keys.Where(key => !checks.ServerKeys.Contains(key))],
        [.. CheckCatalog.Keys.Where(checks.ServerKeys.Contains)],
        AgentContract.MaxResults,
        AgentContract.MaxEvidenceCharacters);

    /// <summary>
    /// The reports waiting — queued, or in somebody's review — that the member may decide, the ones waiting longest first; with
    /// <paramref name="pending"/>, only those with an agent's check and no result from an agent since they were queued.
    /// </summary>
    public async Task<IReadOnlyList<AgentQueueItemDto>> QueueAsync(bool pending, CancellationToken cancellationToken)
    {
        var waiting = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .Where(report => report.Status == PirepStatus.Queued || report.Status == PirepStatus.InReview)
            .OrderBy(report => report.QueuedAt)
            .ThenBy(report => report.Id)
            .Take(MaxQueue)
            .ToListAsync(cancellationToken);

        var mine = new List<Pirep>(waiting.Count);
        foreach (var report in waiting)
        {
            if (await reviews.MayValidateAsync(report))
            {
                mine.Add(report);
            }
        }

        var ids = mine.Select(report => report.Id).ToList();
        var tourIds = mine.Select(report => report.TourId).Distinct().ToList();
        var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);
        var lastRun = await database.CheckResults.AsNoTracking()
            .Where(result => ids.Contains(result.PirepId) && result.RanBy == CheckRanBy.Agent)
            .GroupBy(result => result.PirepId)
            .Select(group => new { PirepId = group.Key, RanAt = group.Max(result => result.RanAt) })
            .ToDictionaryAsync(row => row.PirepId, row => row.RanAt, cancellationToken);

        var items = new List<AgentQueueItemDto>(mine.Count);
        foreach (var report in mine)
        {
            var wanted = checks.WantedOfTheAgent(PirepSubmission.Snapshot(report)).Keys.ToList();
            DateTime? ranAt = lastRun.TryGetValue(report.Id, out var at) && at >= report.QueuedAt ? at : null;
            if (pending && (wanted.Count == 0 || ranAt is not null))
            {
                continue;
            }

            var leg = PirepSubmission.LegSnapshot(report);
            items.Add(new AgentQueueItemDto(
                report.Id,
                report.TourId,
                titles.GetValueOrDefault(report.TourId) ?? Localized<string>.Empty,
                leg.Number,
                report.DepartureIcao,
                report.ArrivalIcao,
                report.Vid,
                report.TakeoffAt,
                report.QueuedAt,
                report.Status.ToString(),
                wanted,
                ranAt));
        }

        return items;
    }

    /// <summary>A report with its flights and errors, when it exists and is not withdrawn; tracked for a write.</summary>
    public Task<Pirep?> FindAsync(long id, bool tracked, CancellationToken cancellationToken) => reviews.FindAsync(id, tracked, cancellationToken);

    /// <summary>Whether the member may decide this report: the one handler, on the row.</summary>
    public Task<bool> MayValidateAsync(Pirep pirep) => reviews.MayValidateAsync(pirep);

    /// <summary>The report as the agent reads it.</summary>
    public async Task<AgentPirepDto> ReadAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == pirep.TourId, cancellationToken);
        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        var rules = PirepSubmission.Snapshot(pirep);
        var leg = PirepSubmission.LegSnapshot(pirep);

        var flightIds = pirep.Flights.Select(flight => flight.Id).ToList();
        var tracks = await database.PirepTracks.AsNoTracking()
            .Where(track => flightIds.Contains(track.PirepFlightId))
            .ToDictionaryAsync(track => track.PirepFlightId, cancellationToken);

        var icaos = WeatherArchive.Airports(pirep).Select(airport => airport.Icao).ToList();
        var places = await airports.FindAsync(icaos, cancellationToken);
        var located = new List<AgentAirportDto>();
        foreach (var icao in icaos.Where(places.ContainsKey))
        {
            var place = places[icao];
            var ends = await runways.GetAsync(icao, cancellationToken);
            located.Add(new AgentAirportDto(
                place.Icao,
                place.Name,
                place.CountryId,
                place.Latitude,
                place.Longitude,
                place.ElevationFeet,
                [.. ends.Select(end => new AgentRunwayDto(end.Designator, end.LengthMetres, end.Bearing, end.Latitude, end.Longitude, end.ElevationFeet))]));
        }

        return new AgentPirepDto(
            pirep.Id,
            pirep.TourId,
            tour.Title,
            pirep.Status.ToString(),
            pirep.Vid,
            pirep.FlightRules,
            pirep.Sid,
            pirep.Star,
            pirep.Approach,
            new AgentLegDto(leg.Number, leg.DepartureIcao, leg.ArrivalIcao, leg.Callsigns),
            pirep.IsDiversion,
            pirep.DiversionIcao,
            [
                .. pirep.Flights.OrderBy(flight => flight.Seq).Select(flight => Flight(
                    flight,
                    tracks.TryGetValue(flight.Id, out var track) ? TrackCodec.Decode(track) : null)),
            ],
            [.. checks.WantedOfTheAgent(rules).Select(entry => new AgentCheckWantedDto(entry.Key, entry.Value))],
            new AgentSettingsDto(settings.NorthSouthLevelCountries),
            located,
            await AtcAsync(pirep, cancellationToken),
            await ResultsAsync(pirep.Id, cancellationToken));
    }

    /// <summary>
    /// Saves the results of one run: each replaces the agent's result for the same check (note of 15 September §3.2), and the
    /// errors of every failed check — the server's and the agents' — are suggested again. Only on a report still waiting
    /// (Carmine, 24 September 2026): a decision already taken is not suggested at afterwards; a reopening puts it back.
    /// </summary>
    public async Task<(AgentWriteResult Result, IReadOnlyDictionary<string, string[]>? Problems, AgentChecksWrittenDto? Written)> WriteAsync(
        Pirep pirep,
        AgentChecksWriteDto body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        ArgumentNullException.ThrowIfNull(body);

        if (!await reviews.MayValidateAsync(pirep))
        {
            return (AgentWriteResult.Forbidden, null, null);
        }

        if (pirep.Status is not (PirepStatus.Queued or PirepStatus.InReview))
        {
            return (AgentWriteResult.NotWaiting, null, null);
        }

        var (results, problems) = Read(body, checks.ServerKeys);
        if (problems.Count > 0)
        {
            return (AgentWriteResult.Refused, problems, null);
        }

        var now = clock.UtcNow;
        var sent = results.Select(result => result.Key).ToHashSet(StringComparer.Ordinal);
        var replaced = await database.CheckResults
            .Where(result => result.PirepId == pirep.Id && result.RanBy == CheckRanBy.Agent && sent.Contains(result.CheckKey))
            .ToListAsync(cancellationToken);
        database.CheckResults.RemoveRange(replaced);

        long? tokenId = long.TryParse(Principal?.FindFirstValue(HubClaims.PersonalToken), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        database.CheckResults.AddRange(results.Select(result => new CheckResult
        {
            PirepId = pirep.Id,
            CheckKey = result.Key,
            Outcome = result.Outcome,
            EvidenceJson = JsonSerializer.Serialize(result.Evidence.Select(line => new EvidenceLine(null, null, line)), FlightChecks.ColumnJson),
            RanBy = CheckRanBy.Agent,
            RanAt = now,
            ByVid = currentUser.Vid,
            TokenId = tokenId,
            AgentVersion = body.AgentVersion!.Trim(),
        }));

        // Every failed check suggests, whoever ran it: the rows kept, and the ones just sent.
        var failed = await database.CheckResults.AsNoTracking()
            .Where(result => result.PirepId == pirep.Id
                && result.Outcome == CheckOutcome.Failed
                && !(result.RanBy == CheckRanBy.Agent && sent.Contains(result.CheckKey)))
            .Select(result => result.CheckKey)
            .ToListAsync(cancellationToken);
        FlightChecks.Suggest(pirep, PirepSubmission.Snapshot(pirep), [.. failed, .. results.Where(result => result.Outcome == CheckOutcome.Failed).Select(result => result.Key)]);

        await database.SaveChangesAsync(cancellationToken);

        return (
            AgentWriteResult.Done,
            null,
            new AgentChecksWrittenDto(
                await ResultsAsync(pirep.Id, cancellationToken),
                [.. pirep.Errors.Where(error => error.SuggestedByCheck).Select(error => error.ErrorId).Order()]));
    }

    /// <summary>
    /// The results of a request, or why not, field by field as the API spells them. A key the server runs is refused: one runner
    /// a check (Carmine, 24 September 2026). A key the catalogue does not know is kept and suggests nothing — the contract does
    /// not know the checks (note of 15 September §3.2).
    /// </summary>
    public static (IReadOnlyList<(string Key, CheckOutcome Outcome, IReadOnlyList<string> Evidence)> Results, Dictionary<string, string[]> Problems) Read(
        AgentChecksWriteDto body,
        IReadOnlyCollection<string> serverKeys)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(serverKeys);

        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var version = body.AgentVersion?.Trim();
        if (string.IsNullOrEmpty(version) || version.Length > AgentContract.MaxVersionLength)
        {
            problems["agentVersion"] = ["flightops:errors.agentVersion"];
        }

        var sent = body.Results ?? [];
        if (sent.Count is 0 or > AgentContract.MaxResults)
        {
            problems["results"] = ["flightops:errors.agentResults"];
            return ([], problems);
        }

        var results = new List<(string, CheckOutcome, IReadOnlyList<string>)>(sent.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sent.Count; index++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"results[{index}]");
            var result = sent[index];
            var key = result?.CheckKey?.Trim() ?? string.Empty;

            if (key.Length == 0 || key.Length > AgentContract.MaxKeyLength || !KeyShape().IsMatch(key))
            {
                problems[$"{at}.checkKey"] = ["flightops:errors.agentCheckKey"];
            }
            else if (serverKeys.Contains(key))
            {
                problems[$"{at}.checkKey"] = ["flightops:errors.agentCheckKeyServer"];
            }
            else if (!seen.Add(key))
            {
                problems[$"{at}.checkKey"] = ["flightops:errors.agentCheckKeyTwice"];
            }

            // By name only: Enum.TryParse would also take "1" and "Passed, Failed".
            var outcome = CheckOutcome.Unavailable;
            if (result?.Outcome is not { } named || !Enum.GetNames<CheckOutcome>().Contains(named, StringComparer.Ordinal) || !Enum.TryParse(named, out outcome))
            {
                problems[$"{at}.outcome"] = ["flightops:errors.agentOutcome"];
            }

            var lines = (result?.Evidence ?? []).Select(line => line?.Trim() ?? string.Empty).Where(line => line.Length > 0).ToList();
            if (lines.Count > AgentContract.MaxEvidenceLines || lines.Sum(line => line.Length) > AgentContract.MaxEvidenceCharacters)
            {
                problems[$"{at}.evidence"] = ["flightops:errors.agentEvidence"];
            }

            results.Add((key, outcome, lines));
        }

        return (results, problems);
    }

    /// <summary>What agents sent on a report, one per check.</summary>
    private async Task<IReadOnlyList<AgentResultDto>> ResultsAsync(long pirepId, CancellationToken cancellationToken)
    {
        var rows = await database.CheckResults.AsNoTracking()
            .Where(result => result.PirepId == pirepId && result.RanBy == CheckRanBy.Agent)
            .OrderBy(result => result.CheckKey)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new AgentResultDto(
                row.CheckKey,
                row.Outcome.ToString(),
                [.. FlightChecks.Evidence(row).Select(line => line.Text).OfType<string>()],
                row.RanAt,
                row.ByVid,
                row.AgentVersion)),
        ];
    }

    /// <summary>The archive for the flight's interval, and what the pilot declared.</summary>
    private async Task<AgentAtcDto> AtcAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        var (from, to) = WeatherArchive.Window(pirep);
        var activity = await archive.OnlineAsync(from, to, cancellationToken);
        var (contacts, exemptions) = PirepSubmission.Atc(pirep);

        return new AgentAtcDto(
            activity is not null,
            from,
            to,
            [.. (activity?.Online ?? []).Select(presence => new AgentAtcPresenceDto(presence.Callsign, presence.Frequency, presence.StartedAt, presence.EndedAt))],
            activity?.DivisionSince,
            activity?.WorldSince,
            activity?.DivisionPrefixes ?? [],
            [.. contacts.Select(contact => new AgentAtcContactDto(contact.Callsign, contact.Frequency, contact.Origin.ToString()))],
            [.. exemptions.Select(exemption => new AgentExemptionDto(exemption.Callsign, exemption.Kind.ToString(), exemption.Note, exemption.Status.ToString(), exemption.Softens))]);
    }

    private static AgentFlightDto Flight(PirepFlight flight, IReadOnlyList<IvaoTrackPointDto>? track)
    {
        var read = FlightChecks.Checked(flight, track);
        return new AgentFlightDto(
            flight.Seq,
            flight.Callsign,
            flight.Aircraft,
            flight.DepartureIcao,
            flight.ArrivalIcao,
            flight.TakeoffAt,
            flight.LandingAt,
            flight.PlanAtTakeoffRevision,
            [
                .. read.Plans.Select(plan => new AgentPlanDto(
                    plan.Revision,
                    plan.FiledAt,
                    plan.DepartureIcao,
                    plan.ArrivalIcao,
                    plan.AlternateIcao,
                    plan.SecondAlternateIcao,
                    plan.AircraftIcao,
                    plan.WakeTurbulence,
                    plan.Equipment,
                    plan.Transponder,
                    plan.FlightRules,
                    plan.FlightType,
                    plan.Level,
                    plan.Speed,
                    plan.Route,
                    plan.Remarks,
                    plan.DepartureTime is { } departure ? (int)departure.TotalMinutes : null,
                    plan.EstimatedEnroute is { } enroute ? (int)enroute.TotalMinutes : null)),
            ],
            track?.Select(point => new AgentTrackPointDto(
                point.At,
                point.Latitude,
                point.Longitude,
                point.AltitudeFeet,
                point.GroundSpeedKnots,
                point.Heading,
                point.OnGround)).ToList());
    }

    private ClaimsPrincipal? Principal => http.HttpContext?.User;

    /// <summary>A key as the catalogue writes them: camel case, letters and digits.</summary>
    [GeneratedRegex("^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyShape();
}
