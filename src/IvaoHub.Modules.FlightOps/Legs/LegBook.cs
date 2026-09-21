using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.FlightOps.Aircraft;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// What every write of a leg does besides the endpoint's own checks (design M2 §1.4, §8.4): the airports frozen and
/// measured, the aircraft checked, the numbers kept without holes, the runways of the airports fetched; and what the
/// editor reads back — every leg of the tour with its IATA codes, its estimated time and whether a report points at it.
/// </summary>
public sealed class LegBook(
    FlightOpsDbContext database,
    IAirportDirectory airports,
    IRunwayDirectory runways,
    AllowedAircraftCheck allowedAircraft,
    ITourReports reports,
    ModuleSettingsStore settings,
    ILogger<LegBook> logger)
{
    /// <summary>The legs of a tour, tracked, in their order.</summary>
    public async Task<List<Leg>> LegsAsync(long tourId, CancellationToken cancellationToken) =>
        await database.Legs
            .Where(leg => leg.TourId == tourId)
            .OrderBy(leg => leg.Number)
            .ThenBy(leg => leg.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The payload on the leg: the airports looked up and their coordinates frozen, the distance measured, the aircraft
    /// normalised and checked, the department the tour's. Returns the refusals, filed under the payload's fields.
    /// A leg a report points at is changed only with a reason (§1.4), which the audit keeps.
    /// </summary>
    internal async Task<Dictionary<string, string[]>?> ApplyAsync(
        Tour tour,
        Leg leg,
        LegWriteDto payload,
        bool hasReports,
        CancellationToken cancellationToken)
    {
        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var departure = LegValidation.Normalize(payload.DepartureIcao);
        var arrival = LegValidation.Normalize(payload.ArrivalIcao);

        var found = await airports.FindAsync([departure, arrival], cancellationToken);
        var from = Located(found, departure);
        var to = Located(found, arrival);

        if (from is null)
        {
            problems["departureIcao"] = ["flightops:errors.airportUnknown"];
        }

        if (to is null)
        {
            problems["arrivalIcao"] = ["flightops:errors.airportUnknown"];
        }

        var aircraft = AllowedAircraftCheck.Normalize(payload.Aircraft);
        if (await allowedAircraft.ProblemAsync(aircraft, cancellationToken) is { } aircraftProblem)
        {
            problems["aircraft"] = [aircraftProblem];
        }

        var reason = string.IsNullOrWhiteSpace(payload.ChangeReason) ? null : payload.ChangeReason.Trim();
        if (hasReports && reason is null)
        {
            problems["changeReason"] = ["flightops:errors.changeReasonRequired"];
        }

        // Rotations and connections are a hub tour's (design M2 §1.3), and a rotation is one of this tour's.
        if (tour.Kind != TourKind.Hub && (payload.Kind != LegKind.Normal || payload.RotationId is not null))
        {
            problems[payload.RotationId is null ? "kind" : "rotationId"] = ["flightops:errors.legHubOnly"];
        }
        else if (payload.RotationId is { } rotationId
            && !await database.Rotations.AnyAsync(rotation => rotation.Id == rotationId && rotation.TourId == tour.Id, cancellationToken))
        {
            problems["rotationId"] = ["flightops:errors.rotationUnknown"];
        }

        if (problems.Count > 0 || from is null || to is null)
        {
            return problems;
        }

        leg.TourId = tour.Id;
        leg.OwnerDepartment = tour.OwnerDepartment;
        leg.OwnerDepartmentMask = tour.OwnerDepartmentMask;
        leg.DepartureIcao = departure;
        leg.ArrivalIcao = arrival;
        leg.DepartureLatitude = from.Value.Latitude;
        leg.DepartureLongitude = from.Value.Longitude;
        leg.ArrivalLatitude = to.Value.Latitude;
        leg.ArrivalLongitude = to.Value.Longitude;
        leg.DistanceNm = GreatCircle.DistanceNmRounded(from.Value, to.Value);
        leg.RealCallsign = Clean(payload.RealCallsign)?.ToUpperInvariant();
        leg.FlightNumber = Clean(payload.FlightNumber)?.ToUpperInvariant();
        leg.Aircraft = aircraft;
        leg.ReleaseAt = payload.ReleaseAt;
        leg.ChangeReason = reason;
        leg.Kind = payload.Kind;
        leg.RotationId = payload.RotationId;

        return null;
    }

    /// <summary>
    /// The place of every leg in its rotation, from 1, in the order the legs have in the tour: kept by the server like
    /// the numbers, so moving a leg never leaves a rotation with two second legs. A leg in no rotation has none.
    /// </summary>
    public static void SequenceRotations(IReadOnlyList<Leg> legs)
    {
        ArgumentNullException.ThrowIfNull(legs);

        foreach (var leg in legs.Where(leg => leg.RotationId is null && leg.SeqInRotation is not null))
        {
            leg.SeqInRotation = null;
        }

        foreach (var rotation in legs.Where(leg => leg.RotationId is not null).GroupBy(leg => leg.RotationId))
        {
            var seq = 1;
            foreach (var leg in rotation.OrderBy(leg => leg.Number))
            {
                if (leg.SeqInRotation != seq)
                {
                    leg.SeqInRotation = seq;
                }

                seq++;
            }
        }
    }

    /// <summary>
    /// Numbers from 1 without holes, in the order the legs already have: after a leg is inserted, deleted or moved,
    /// the whole tour is numbered again. A report points at a leg by its identifier, so none is touched (§1.4.1).
    /// </summary>
    public static void Renumber(IReadOnlyList<Leg> legs)
    {
        ArgumentNullException.ThrowIfNull(legs);

        var number = 1;
        foreach (var leg in legs.OrderBy(leg => leg.Number).ThenBy(leg => leg.Id == 0 ? long.MaxValue : leg.Id))
        {
            if (leg.Number != number)
            {
                leg.Number = number;
            }

            number++;
        }
    }

    /// <summary>
    /// The runways of the airports the legs use, fetched once (design M2 §1.12): the checks of the tracks will need the
    /// thresholds. Never a reason to refuse a leg — a network that is down leaves the airport without runways, and the
    /// checks that need them say "not available".
    /// </summary>
    public async Task FetchRunwaysAsync(IEnumerable<Leg> legs, CancellationToken cancellationToken)
    {
        try
        {
            await runways.EnsureAsync([.. legs.SelectMany(leg => new[] { leg.DepartureIcao, leg.ArrivalIcao })], cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The runways of the airports of a leg could not be fetched now.");
        }
    }

    /// <summary>What the editor shows: every leg, retired ones included, and the totals of those still flown.</summary>
    public async Task<TourLegsDto> GridAsync(Tour tour, IReadOnlyList<Leg> legs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(legs);

        var known = await airports.FindAsync([.. legs.SelectMany(leg => new[] { leg.DepartureIcao, leg.ArrivalIcao })], cancellationToken);
        var withReports = await reports.LegsWithReportsAsync(tour.Id, cancellationToken);
        var estimate = await EstimatorAsync(tour, cancellationToken);

        var rows = legs
            .OrderBy(leg => leg.Number)
            .Select(leg => new LegDto(
                leg.Id,
                leg.TourId,
                leg.Number,
                leg.Kind,
                leg.RotationId,
                leg.SeqInRotation,
                leg.DepartureIcao,
                known.GetValueOrDefault(leg.DepartureIcao)?.Iata,
                leg.ArrivalIcao,
                known.GetValueOrDefault(leg.ArrivalIcao)?.Iata,
                leg.DistanceNm,
                estimate?.Invoke(leg.DistanceNm),
                leg.RealCallsign,
                leg.FlightNumber,
                leg.Aircraft,
                leg.ReleaseAt,
                leg.RetiredAt,
                leg.RetiredReason,
                withReports.Contains(leg.Id),
                leg.UpdatedAt,
                leg.RowVersion))
            .ToList();

        var flown = rows.Where(row => row.RetiredAt is null).ToList();

        return new TourLegsDto(
            tour.Id,
            rows,
            flown.Sum(row => row.DistanceNm),
            estimate is null ? null : flown.Sum(row => row.EstimatedMinutes ?? 0));
    }

    /// <summary>
    /// The estimated time of a distance for this tour (design M2 §1.5), or null when the tour names no reference
    /// aircraft or the aircraft has no profile: without a speed, no estimates.
    /// </summary>
    private async Task<Func<decimal, int?>?> EstimatorAsync(Tour tour, CancellationToken cancellationToken)
    {
        if (tour.ReferenceAircraftIcao is not { } type)
        {
            return null;
        }

        var speed = await database.Set<AircraftProfile>().AsNoTracking()
            .Where(profile => profile.IcaoType == type)
            .Select(profile => (int?)profile.CruiseTasKt)
            .FirstOrDefaultAsync(cancellationToken);

        if (speed is not { } tas || tas <= 0)
        {
            return null;
        }

        var values = await settings.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);

        return distance => EstimatedTime.Minutes(distance, tas, values.DurationFactor, values.DurationFixedMinutes);
    }

    private static GeoPoint? Located(IReadOnlyDictionary<string, AirportDto> found, string icao) =>
        found.GetValueOrDefault(icao) is { Latitude: { } latitude, Longitude: { } longitude }
            ? new GeoPoint(latitude, longitude)
            : null;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
