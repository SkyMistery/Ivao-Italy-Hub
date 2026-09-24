using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Core.Weather;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using IvaoHub.Modules.FlightOps.Weather;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>
/// The engine of the automatic checks (design M2 §6.1–§6.3): after a report is sent or sent again, every check the rules it
/// froze name — or its errors do — runs on what the report was, and writes what it found in <c>fo_check_results</c>. The
/// errors of a failed check become <b>suggested</b> on the report; nobody decides but the validator, who confirms them or
/// not, and the report keeps which they did (§6.3).
/// <para>A check that throws is <c>Unavailable</c>, never failed. The two the agent runs (§6.6) are not run here.</para>
/// </summary>
public sealed class FlightChecks(
    FlightOpsDbContext database,
    IEnumerable<IFlightCheck> checks,
    ModuleSettingsStore settingsStore,
    IAirportDirectory airports,
    IRunwayDirectory runways,
    IClock clock,
    ILogger<FlightChecks> logger)
{
    /// <summary>The evidence as its column holds it: the web's names.</summary>
    public static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, IFlightCheck> _checks = checks.ToDictionary(check => check.Key, StringComparer.Ordinal);

    /// <summary>
    /// Runs the checks on a report waiting for a validator and saves what they found with its suggestions. The report is
    /// tracked with its flights and its errors. Returns false when somebody changed it meanwhile: the job tries again.
    /// </summary>
    public async Task<bool> RunAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var now = clock.UtcNow;
        var context = await ContextAsync(pirep, cancellationToken);
        var rules = PirepSubmission.Snapshot(pirep);
        var found = Evaluate(Wanted(rules), context, rules);

        var previous = await database.CheckResults
            .Where(result => result.PirepId == pirep.Id && result.RanBy == CheckRanBy.Server)
            .ToListAsync(cancellationToken);
        database.CheckResults.RemoveRange(previous);
        database.CheckResults.AddRange(found.Select(entry => new CheckResult
        {
            PirepId = pirep.Id,
            CheckKey = entry.Key,
            Outcome = entry.Value.Outcome,
            EvidenceJson = JsonSerializer.Serialize(entry.Value.Evidence, ColumnJson),
            RanBy = CheckRanBy.Server,
            RanAt = now,
        }));

        var agents = await database.CheckResults.AsNoTracking()
            .Where(result => result.PirepId == pirep.Id && result.RanBy == CheckRanBy.Agent && result.Outcome == CheckOutcome.Failed)
            .Select(result => result.CheckKey)
            .ToListAsync(cancellationToken);
        Suggest(pirep, rules, [.. found.Where(entry => entry.Value.Outcome == CheckOutcome.Failed).Select(entry => entry.Key), .. agents]);
        pirep.ChecksRanAt = now;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Taken or changed meanwhile: the job runs the checks again on what it is now.
            database.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>
    /// The run right after a send (§6.1): never a refused send — whatever goes wrong, the report stays sent and the job runs
    /// the checks later.
    /// </summary>
    public async Task TryRunAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(pirep, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The checks did not run on report {Pirep} at its send; the job will run them", pirep.Id);
        }
    }

    /// <summary>
    /// The checks the server runs on a report, with the parameters of the rule that names each: a check named only by an error
    /// runs with the values it starts with. The agent's are left to the agent.
    /// </summary>
    public IReadOnlyDictionary<string, JsonObject> Wanted(IReadOnlyList<SnapshotRuleDto> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var wanted = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var rule in rules.Where(rule => rule.CheckKey is { } key && _checks.ContainsKey(key)))
        {
            wanted.TryAdd(rule.CheckKey!, rule.Parameters);
        }

        foreach (var key in rules.SelectMany(rule => rule.Errors).Select(error => error.CheckKey).OfType<string>().Where(_checks.ContainsKey))
        {
            wanted.TryAdd(key, CheckCatalog.Read(key, new JsonObject(), amending: false).Parameters);
        }

        return wanted;
    }

    /// <summary>
    /// What each check found, a failure to run being <c>Unavailable</c>. The rules are passed so the log can say which report
    /// it was without the report.
    /// </summary>
    public IReadOnlyDictionary<string, CheckVerdict> Evaluate(
        IReadOnlyDictionary<string, JsonObject> wanted,
        FlightCheckContext context,
        IReadOnlyList<SnapshotRuleDto> rules)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(context);

        var found = new Dictionary<string, CheckVerdict>(StringComparer.Ordinal);
        foreach (var (key, parameters) in wanted)
        {
            try
            {
                found[key] = _checks[key].Evaluate(context, parameters);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A check that breaks says nothing about the flight (§6.2): never a failure, and the log says which.
                logger.LogWarning(exception, "Check {Check} could not run on report {Pirep} ({Rules} rules)", key, context.PirepId, rules.Count);
                found[key] = CheckVerdict.Unavailable(EvidenceLine.Of("checkBroke"));
            }
        }

        return found;
    }

    /// <summary>
    /// The errors of the failed checks as suggestions on the report: added unconfirmed where the report has none, the flag set
    /// or cleared on the rows it has, and an unconfirmed suggestion no check makes any more dropped.
    /// </summary>
    public static void Suggest(Pirep pirep, IReadOnlyList<SnapshotRuleDto> rules, IReadOnlyCollection<string> failed)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(failed);

        var suggested = rules
            .SelectMany(rule => rule.Errors)
            .Where(error => error.CheckKey is { } key && failed.Contains(key))
            .DistinctBy(error => error.Id)
            .ToList();
        var ids = suggested.Select(error => error.Id).ToHashSet();

        foreach (var row in pirep.Errors)
        {
            row.SuggestedByCheck = ids.Contains(row.ErrorId);
        }

        pirep.Errors.RemoveAll(row => !row.Confirmed && !row.SuggestedByCheck);
        pirep.Errors.AddRange(suggested
            .Where(error => pirep.Errors.All(row => row.ErrorId != error.Id))
            .Select(error => new PirepError { ErrorId = error.Id, Category = error.Category, SuggestedByCheck = true }));
    }

    /// <summary>The results of a report, each runner's, as its columns hold them.</summary>
    public static IReadOnlyList<EvidenceLine> Evidence(CheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Deserialize<List<EvidenceLine>>(result.EvidenceJson, ColumnJson) ?? [];
    }

    /// <summary>What the checks read of a report (§6.2), gathered once: nothing a check reads comes from anywhere else.</summary>
    public async Task<FlightCheckContext> ContextAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == pirep.TourId, cancellationToken);
        var leg = PirepSubmission.LegSnapshot(pirep);

        var tourIds = new[] { tour.Id, tour.ParentTourId ?? 0 };
        var callsigns = await database.CallsignRules.AsNoTracking()
            .Where(rule => tourIds.Contains(rule.TourId))
            .ToListAsync(cancellationToken);
        IReadOnlyList<IReadOnlyList<CallsignRule>> levels =
        [
            [.. callsigns.Where(rule => pirep.LegId is not null && rule.LegId == pirep.LegId)],
            [.. callsigns.Where(rule => rule.TourId == tour.Id && rule.LegId == null)],
            [.. callsigns.Where(rule => tour.ParentTourId is { } parent && rule.TourId == parent && rule.LegId == null)],
        ];

        var routes = await database.Pireps.AsNoTracking()
            .Where(report => report.TourId == pirep.TourId && report.Vid == pirep.Vid && report.Id != pirep.Id)
            .Select(report => new { report.Status, report.DepartureIcao, report.ArrivalIcao })
            .ToListAsync(cancellationToken);

        var flightIds = pirep.Flights.Select(flight => flight.Id).ToList();
        var tracks = await database.PirepTracks.AsNoTracking()
            .Where(track => flightIds.Contains(track.PirepFlightId))
            .ToDictionaryAsync(track => track.PirepFlightId, cancellationToken);

        // The places and the weather the checks on the tracks read (T18): the runways were fetched at the send, the METARs
        // kept by the weather job or at the send (T16).
        var icaos = WeatherArchive.Airports(pirep).Select(airport => airport.Icao).ToList();
        var places = await airports.FindAsync(icaos, cancellationToken);
        var ends = new Dictionary<string, IReadOnlyList<IvaoRunway>>(StringComparer.Ordinal);
        foreach (var icao in icaos)
        {
            ends[icao] = await runways.GetAsync(icao, cancellationToken);
        }

        var (from, to) = WeatherArchive.Window(pirep);
        var metars = await database.WeatherBulletins.AsNoTracking()
            .Where(bulletin => icaos.Contains(bulletin.Icao) && bulletin.Kind == WeatherReportKind.Metar
                && bulletin.IssuedAt >= from && bulletin.IssuedAt <= to)
            .Select(bulletin => new WeatherReport(bulletin.Icao, bulletin.Kind, bulletin.IssuedAt, bulletin.Raw, bulletin.Source))
            .ToListAsync(cancellationToken);

        return new FlightCheckContext(
            pirep.Id,
            tour.Kind,
            leg,
            pirep.IsDiversion,
            [
                .. pirep.Flights.OrderBy(flight => flight.Seq).Select(flight => Checked(
                    flight,
                    tracks.TryGetValue(flight.Id, out var track) ? TrackCodec.Decode(track) : null)),
            ],
            levels,
            await AllowedAsync(leg.Aircraft, cancellationToken),
            [.. routes.Where(route => Pirep.Counts(route.Status)).Select(route => (route.DepartureIcao, route.ArrivalIcao))],
            settings)
        {
            DiversionIcao = pirep.DiversionIcao,
            Airports = places,
            Runways = ends,
            Metars = metars,
            Exemptions = PirepSubmission.Atc(pirep).Exemptions,
        };
    }

    /// <summary>A flight as the checks read it: its plans with the reader of the core's client, the one at take-off as the send chose it.</summary>
    public static CheckedFlight Checked(PirepFlight flight, IReadOnlyList<IvaoTrackPointDto>? track)
    {
        ArgumentNullException.ThrowIfNull(flight);

        using var document = JsonDocument.Parse(flight.FlightPlansJson);
        var plans = IvaoTrackerReader.ReadFlightPlans(document.RootElement);

        return new CheckedFlight(
            flight.Seq,
            flight.Callsign,
            flight.Aircraft,
            flight.DepartureIcao,
            flight.ArrivalIcao,
            flight.TakeoffAt,
            flight.LandingAt,
            plans,
            plans.FirstOrDefault(plan => plan.Revision == flight.PlanAtTakeoffRevision) ?? plans.LastOrDefault(),
            track);
    }

    /// <summary>The types the leg admits, groups expanded; null when it admits any (design M2 §2.7).</summary>
    private async Task<IReadOnlySet<string>?> AllowedAsync(AllowedAircraft allowed, CancellationToken cancellationToken)
    {
        if (allowed.Types.Count == 0 && allowed.GroupIds.Count == 0)
        {
            return null;
        }

        var groupIds = allowed.GroupIds.ToArray();
        var grouped = await database.AircraftGroups.AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .ToListAsync(cancellationToken);

        return allowed.Types.Concat(grouped.SelectMany(group => group.IcaoTypes)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
