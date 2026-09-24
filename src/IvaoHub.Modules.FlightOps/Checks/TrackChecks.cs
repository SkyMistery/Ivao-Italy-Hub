using System.Globalization;
using System.Text.Json.Nodes;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;

namespace IvaoHub.Modules.FlightOps.Checks;

/// <summary>What one line of a check on the tracks says: fine, a failure, or that this part could not be judged.</summary>
internal enum LineState
{
    Fine,
    Failed,
    Unknown,
}

/// <summary>
/// The checks on the tracks (design M2 §6.4, T18) read the points the tracker gave at the send (T13a), one flight at a time,
/// with the same take-off and landing the send took (<see cref="TrackedFlight"/>): the first point in the air, and the first
/// on the ground after the last one in the air — so a touch and go on the way is not the landing. The thresholds they use were
/// measured on the corpus of real flights (note 2026-09-24-i-controlli-sulle-tracce).
/// </summary>
internal static class TrackLines
{
    /// <summary>At or below this ground speed the aircraft stands still (the validator of today's tours uses the same).</summary>
    public const int StillKt = 2;

    /// <summary>
    /// A hole longer than this between two points is a disconnection: the tracker samples every 15 seconds or so, 40 at most
    /// on the corpus, so a minute is never the sampling.
    /// </summary>
    public static readonly TimeSpan DisconnectionGap = TimeSpan.FromSeconds(60);

    private const double MetresPerNm = 1852;

    /// <summary>
    /// The lines of every flight with a track; a flight without one says so. Failed when a line fails, unavailable when no
    /// line could judge anything, passed otherwise.
    /// </summary>
    public static CheckVerdict PerFlight(
        FlightCheckContext context,
        Func<CheckedFlight, IReadOnlyList<IvaoTrackPointDto>, IEnumerable<(LineState State, EvidenceLine Line)>> judge)
    {
        var lines = new List<(LineState State, EvidenceLine Line)>();
        foreach (var flight in context.Flights)
        {
            lines.AddRange(Points(flight) is { } points
                ? judge(flight, points).Select(line => (line.State, PlanLines.Numbered(line.Line, flight, context)))
                : [(LineState.Unknown, PlanLines.Numbered(EvidenceLine.Of("noTrack"), flight, context))]);
        }

        var evidence = lines.Select(line => line.Line).ToArray();
        return lines.Any(line => line.State == LineState.Failed) ? CheckVerdict.Failed(evidence)
            : lines.All(line => line.State == LineState.Unknown) ? CheckVerdict.Unavailable(evidence)
            : CheckVerdict.Passed(evidence);
    }

    /// <summary>The points of a flight in time order, or null without a track worth reading.</summary>
    public static IReadOnlyList<IvaoTrackPointDto>? Points(CheckedFlight flight) =>
        flight.Track is { Count: > 1 } track ? [.. track.OrderBy(point => point.At)] : null;

    /// <summary>The first point in the air, or -1.</summary>
    public static int Takeoff(IReadOnlyList<IvaoTrackPointDto> points)
    {
        for (var index = 0; index < points.Count; index++)
        {
            if (!points[index].OnGround)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The first point on the ground after the last one in the air, or -1 when the session ended in the air.</summary>
    public static int Landing(IReadOnlyList<IvaoTrackPointDto> points)
    {
        var lastAirborne = -1;
        for (var index = 0; index < points.Count; index++)
        {
            if (!points[index].OnGround)
            {
                lastAirborne = index;
            }
        }

        return lastAirborne >= 0 && lastAirborne + 1 < points.Count ? lastAirborne + 1 : -1;
    }

    /// <summary>The points between the take-off and the landing, in the air.</summary>
    public static IEnumerable<IvaoTrackPointDto> Airborne(IReadOnlyList<IvaoTrackPointDto> points) => points.Where(point => !point.OnGround);

    public static GeoPoint At(IvaoTrackPointDto point) => new(point.Latitude, point.Longitude);

    public static double Metres(GeoPoint from, GeoPoint to) => GreatCircle.DistanceNm(from, to) * MetresPerNm;

    /// <summary>The initial true bearing from one point to another, in degrees.</summary>
    public static double Bearing(GeoPoint from, GeoPoint to)
    {
        var lat1 = double.DegreesToRadians(from.Latitude);
        var lat2 = double.DegreesToRadians(to.Latitude);
        var deltaLon = double.DegreesToRadians(to.Longitude - from.Longitude);
        var y = Math.Sin(deltaLon) * Math.Cos(lat2);
        var x = (Math.Cos(lat1) * Math.Sin(lat2)) - (Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon));
        return (double.RadiansToDegrees(Math.Atan2(y, x)) + 360) % 360;
    }

    /// <summary>How far apart two headings are, 0 to 180 degrees.</summary>
    public static double Apart(double first, double second) => Math.Abs(((first - second + 540) % 360) - 180);

    /// <summary>A parameter of the check, or its starting value in the catalogue when the rule does not say.</summary>
    public static int Parameter(JsonObject parameters, string key, string name) =>
        OpenCatalog.Number(parameters, name)
        ?? CheckCatalog.Fields(key).First(field => field.Name == name).Default
        ?? throw new InvalidOperationException($"{key}.{name} has no starting value.");

    public static string Time(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Whole(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    public static string Minutes(TimeSpan span) => span.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture);
}

/// <summary>
/// No disconnection in the air longer than <c>maxSingleDisconnectMinutes</c>, nor more than <c>maxTotalDisconnectMinutes</c>
/// together (design M2 §6.4). A hole in the track is a disconnection when it is longer than a minute and either end of it is
/// in the air; one on the ground is shown and does not count. A session that ends in the air is a disconnection for ever.
/// <para>A flight is one session (§3.4): the old system's «disconnected 2921 minutes» on 880159 came from gluing together
/// sessions of different days, which a report here cannot do.</para>
/// </summary>
public sealed class DisconnectionsCheck : IFlightCheck
{
    public string Key => CheckCatalog.Disconnections;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var maxSingle = TrackLines.Parameter(parameters, Key, "maxSingleDisconnectMinutes");
        var maxTotal = TrackLines.Parameter(parameters, Key, "maxTotalDisconnectMinutes");

        return TrackLines.PerFlight(context, (_, points) =>
        {
            var lines = new List<(LineState, EvidenceLine)>();
            var longest = TimeSpan.Zero;
            var total = TimeSpan.Zero;

            for (var index = 1; index < points.Count; index++)
            {
                var before = points[index - 1];
                var after = points[index];
                var hole = after.At - before.At;
                if (hole <= TrackLines.DisconnectionGap)
                {
                    continue;
                }

                if (before.OnGround && after.OnGround)
                {
                    lines.Add((LineState.Fine, EvidenceLine.Of("disconnectionOnGround",
                        ("at", TrackLines.Time(before.At)), ("minutes", TrackLines.Minutes(hole)))));
                    continue;
                }

                longest = hole > longest ? hole : longest;
                total += hole;
                lines.Add((LineState.Fine, EvidenceLine.Of("disconnectionInFlight",
                    ("at", TrackLines.Time(before.At)),
                    ("minutes", TrackLines.Minutes(hole)),
                    ("altitude", before.AltitudeFeet.ToString(CultureInfo.InvariantCulture)))));
            }

            if (TrackLines.Takeoff(points) >= 0 && TrackLines.Landing(points) < 0)
            {
                var last = points[^1];
                lines.Add((LineState.Failed, EvidenceLine.Of("sessionEndedInFlight",
                    ("at", TrackLines.Time(last.At)), ("altitude", last.AltitudeFeet.ToString(CultureInfo.InvariantCulture)))));
            }

            if (total == TimeSpan.Zero)
            {
                lines.Add((LineState.Fine, EvidenceLine.Of("disconnectionsNone")));
                return lines;
            }

            var tooLong = longest.TotalMinutes > maxSingle || total.TotalMinutes > maxTotal;
            lines.Add((tooLong ? LineState.Failed : LineState.Fine, EvidenceLine.Of(tooLong ? "disconnectionsTooLong" : "disconnectionsWithin",
                ("longest", TrackLines.Minutes(longest)),
                ("total", TrackLines.Minutes(total)),
                ("maxSingle", maxSingle.ToString(CultureInfo.InvariantCulture)),
                ("maxTotal", maxTotal.ToString(CultureInfo.InvariantCulture)))));
            return lines;
        });
    }
}

/// <summary>
/// Standing still at the gate before the pushback and after the arrival (design M2 §6.4): from the first point to the first
/// that moves — faster than 2 kt, or in the air — and from the last that moves to the last point. On the corpus two minutes
/// (the starting value) keep 877596 (2.3 minutes before) and catch 877464 (0.8), as the controllers did; today's three would
/// have caught both.
/// </summary>
public sealed class ParkingCheck : IFlightCheck
{
    public string Key => CheckCatalog.Parking;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var before = TrackLines.Parameter(parameters, Key, "minParkingMinutesBefore");
        var after = TrackLines.Parameter(parameters, Key, "minParkingMinutesAfter");

        return TrackLines.PerFlight(context, (_, points) =>
        {
            static bool Moving(IvaoTrackPointDto point) => !point.OnGround || point.GroundSpeedKnots > TrackLines.StillKt;

            var lines = new List<(LineState, EvidenceLine)>();

            var firstMove = points.ToList().FindIndex(Moving);
            var still = (firstMove < 0 ? points[^1].At : points[firstMove].At) - points[0].At;
            var shortBefore = still.TotalMinutes < before;
            lines.Add((shortBefore ? LineState.Failed : LineState.Fine, EvidenceLine.Of(shortBefore ? "parkedBeforeShort" : "parkedBefore",
                ("minutes", TrackLines.Minutes(still)), ("min", before.ToString(CultureInfo.InvariantCulture)))));

            if (TrackLines.Landing(points) < 0)
            {
                lines.Add((LineState.Unknown, EvidenceLine.Of("parkedAfterUnknown")));
                return lines;
            }

            var lastMove = points.ToList().FindLastIndex(Moving);
            var stillAfter = points[^1].At - points[Math.Max(lastMove, 0)].At;
            var shortAfter = stillAfter.TotalMinutes < after;
            lines.Add((shortAfter ? LineState.Failed : LineState.Fine, EvidenceLine.Of(shortAfter ? "parkedAfterShort" : "parkedAfter",
                ("minutes", TrackLines.Minutes(stillAfter)), ("min", after.ToString(CultureInfo.InvariantCulture)))));
            return lines;
        });
    }
}

/// <summary>
/// No more than 250 kt below FL100 (design M2 §6.4). The tracker gives the ground speed, not the indicated one: the check
/// estimates it as the Python validator does without wind — the true airspeed taken as the ground speed, reduced by the
/// density of the standard atmosphere at that altitude — and says so. A point beyond 250 kt plus <c>toleranceKt</c> fails;
/// the thousand feet just below FL100 never do (the aircraft accelerating as it passes the level), and are counted.
/// <para>An exemption <c>FreeSpeed</c> softens it (note 2026-09-23-gli-atc-contattati): the check passes and names it — unless
/// the archive saw that position not online during the flight, when it fails as without (Carmine, 24 September 2026).</para>
/// </summary>
public sealed class Speed250Check : IFlightCheck
{
    public const int LimitKt = 250;

    public const int Fl100Feet = 10_000;

    /// <summary>The band just below FL100 where going over the limit is shown and never fails.</summary>
    public const int BandFeet = 1_000;

    public string Key => CheckCatalog.Speed250;

    /// <summary>The indicated airspeed a ground speed amounts to at an altitude, in still air of the standard atmosphere.</summary>
    public static double IndicatedKt(int groundSpeedKt, int altitudeFeet)
    {
        var density = Math.Pow(1 - (6.8756e-6 * Math.Max(0, altitudeFeet)), 4.2559);
        return groundSpeedKt * Math.Sqrt(density);
    }

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var limit = LimitKt + TrackLines.Parameter(parameters, Key, "toleranceKt");
        var verdict = TrackLines.PerFlight(context, (_, points) =>
        {
            var below = TrackLines.Airborne(points)
                .Where(point => point.AltitudeFeet < Fl100Feet)
                .Select(point => (Point: point, Indicated: IndicatedKt(point.GroundSpeedKnots, point.AltitudeFeet)))
                .ToList();
            var over = below.Where(point => point.Indicated > limit).ToList();
            var failing = over.Where(point => point.Point.AltitudeFeet < Fl100Feet - BandFeet).ToList();
            var lines = new List<(LineState, EvidenceLine)>();

            if (failing.Count > 0)
            {
                var worst = failing.MaxBy(point => point.Indicated);
                lines.Add((LineState.Failed, EvidenceLine.Of("speed250Exceeded",
                    ("ias", TrackLines.Whole(worst.Indicated)),
                    ("gs", worst.Point.GroundSpeedKnots.ToString(CultureInfo.InvariantCulture)),
                    ("altitude", worst.Point.AltitudeFeet.ToString(CultureInfo.InvariantCulture)),
                    ("at", TrackLines.Time(worst.Point.At)),
                    ("count", failing.Count.ToString(CultureInfo.InvariantCulture)),
                    ("limit", limit.ToString(CultureInfo.InvariantCulture)))));
            }
            else
            {
                var fastest = below.Where(point => point.Point.AltitudeFeet < Fl100Feet - BandFeet).Select(point => point.Indicated).DefaultIfEmpty(0).Max();
                lines.Add((LineState.Fine, EvidenceLine.Of("speed250Kept",
                    ("ias", TrackLines.Whole(fastest)), ("limit", limit.ToString(CultureInfo.InvariantCulture)))));
            }

            if (over.Count > failing.Count)
            {
                lines.Add((LineState.Fine, EvidenceLine.Of("speed250NearFl100", ("count", (over.Count - failing.Count).ToString(CultureInfo.InvariantCulture)))));
            }

            return lines;
        });

        if (verdict.Outcome != CheckOutcome.Failed)
        {
            return verdict;
        }

        var estimated = verdict.Evidence.Append(EvidenceLine.Of("speedEstimated")).ToArray();
        var exemptions = context.Exemptions.Where(exemption => exemption.Softens.Contains(Key, StringComparer.Ordinal)).ToList();
        if (exemptions.FirstOrDefault(exemption => exemption.Status != ExemptionStatus.NotOnline) is { } softening)
        {
            return CheckVerdict.Passed([.. estimated, EvidenceLine.Of("speed250Exempted", ("callsign", softening.Callsign))]);
        }

        return CheckVerdict.Failed([
            .. estimated,
            .. exemptions.Select(exemption => EvidenceLine.Of("speed250ExemptionNotOnline", ("callsign", exemption.Callsign))),
        ]);
    }
}

/// <summary>
/// The speed reported agrees with the speed between the positions (design M2 §6.4): a simulation rate above one moves the
/// aircraft faster than it says. Over every five minutes in the air, the median of the ratio between the two — the median,
/// because the first point after a reconnection jumps (880159: 1264 kt for one sample). Faster than <c>tolerancePercent</c>
/// fails. On the corpus the honest flights stay within 2 %, and 877196 flew four times faster for a while.
/// <para>A slower rate is not looked at: it gains nothing.</para>
/// </summary>
public sealed class SimRateCheck : IFlightCheck
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    /// <summary>Below this ground speed the ratio is noise.</summary>
    public const int MinimumKt = 50;

    /// <summary>Fewer pairs than this in a window and it says nothing.</summary>
    public const int MinimumPairs = 5;

    public string Key => CheckCatalog.SimRate;

    /// <summary>The worst median ratio of a five-minute window, and when it began; null when no window could be measured.</summary>
    public static (double Ratio, DateTime At)? Worst(IReadOnlyList<IvaoTrackPointDto> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var pairs = new List<(DateTime At, double Ratio)>();
        for (var index = 1; index < points.Count; index++)
        {
            var before = points[index - 1];
            var after = points[index];
            var seconds = (after.At - before.At).TotalSeconds;
            var reported = (before.GroundSpeedKnots + after.GroundSpeedKnots) / 2.0;
            if (before.OnGround || after.OnGround || seconds <= 0 || reported < MinimumKt)
            {
                continue;
            }

            var moved = GreatCircle.DistanceNm(TrackLines.At(before), TrackLines.At(after)) / seconds * 3600;
            pairs.Add((after.At, moved / reported));
        }

        (double Ratio, DateTime At)? worst = null;
        for (var start = 0; start < pairs.Count; start++)
        {
            var window = pairs.Skip(start).TakeWhile(pair => pair.At < pairs[start].At + Window).Select(pair => pair.Ratio).Order().ToList();
            if (window.Count >= MinimumPairs && window[window.Count / 2] is var median && (worst is null || median > worst.Value.Ratio))
            {
                worst = (median, pairs[start].At);
            }
        }

        return worst;
    }

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var limit = 1 + (TrackLines.Parameter(parameters, Key, "tolerancePercent") / 100.0);
        return TrackLines.PerFlight(context, (_, points) =>
        {
            if (Worst(points) is not { } worst)
            {
                return [(LineState.Unknown, EvidenceLine.Of("simRateNotMeasured"))];
            }

            var ratio = worst.Ratio.ToString("0.00", CultureInfo.InvariantCulture);
            return worst.Ratio > limit
                ? [(LineState.Failed, EvidenceLine.Of("simRateFaster", ("ratio", ratio), ("at", TrackLines.Time(worst.At)),
                    ("limit", limit.ToString("0.00", CultureInfo.InvariantCulture))))]
                : [(LineState.Fine, EvidenceLine.Of("simRateKept", ("ratio", ratio)))];
        });
    }
}

/// <summary>
/// The highest altitude flown stays within the limit of the plan's flight rules (note 2026-09-24-i-controlli-dai-pirep-veri
/// §4): <c>maxFeetI|V|Y|Z</c>, 19 500 ft for VFR and 66 000 for the rest as today's system (Carmine, 24 September 2026).
/// </summary>
public sealed class MaxAltitudeCheck : IFlightCheck
{
    public string Key => CheckCatalog.MaxAltitude;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        return TrackLines.PerFlight(context, (flight, points) =>
        {
            if (TrackLines.Airborne(points).MaxBy(point => point.AltitudeFeet) is not { } highest)
            {
                return [(LineState.Unknown, EvidenceLine.Of("maxAltitudeNotFlown"))];
            }

            var rules = flight.PlanAtTakeoff is { } plan ? PlanLines.Rules(plan) : "I";
            var limit = TrackLines.Parameter(parameters, Key, CheckCatalog.MaxFeetFor(rules));
            var above = highest.AltitudeFeet > limit;
            return [(above ? LineState.Failed : LineState.Fine, EvidenceLine.Of(above ? "maxAltitudeExceeded" : "maxAltitudeKept",
                ("altitude", highest.AltitudeFeet.ToString(CultureInfo.InvariantCulture)),
                ("limit", limit.ToString(CultureInfo.InvariantCulture)),
                ("rules", rules),
                ("at", TrackLines.Time(highest.At))))];
        });
    }
}

/// <summary>
/// The flight landed where it was meant to (design M2 §6.4): the leg's arrival, or the diversion airport for the first flight
/// of a diversion. The touchdown — after the last point in the air, so a touch and go on the way does not count — within
/// <c>radiusNm</c> of the airport's position or of one of its thresholds. A session that ended in the air did not land.
/// </summary>
public sealed class LandingAtArrivalCheck : IFlightCheck
{
    public string Key => CheckCatalog.LandingAtArrival;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var radius = TrackLines.Parameter(parameters, Key, "radiusNm");
        return TrackLines.PerFlight(context, (flight, points) =>
        {
            var airport = context.ExpectedArrival(flight);
            var landing = TrackLines.Landing(points);
            if (landing < 0)
            {
                return [(LineState.Failed, EvidenceLine.Of("noLanding", ("airport", airport)))];
            }

            var places = new List<GeoPoint>();
            if (context.Airports.GetValueOrDefault(airport) is { Latitude: { } latitude, Longitude: { } longitude })
            {
                places.Add(new GeoPoint(latitude, longitude));
            }

            places.AddRange(context.Runways.GetValueOrDefault(airport, [])
                .Where(runway => runway is { Latitude: not null, Longitude: not null })
                .Select(runway => new GeoPoint(runway.Latitude!.Value, runway.Longitude!.Value)));

            if (places.Count == 0)
            {
                return [(LineState.Unknown, EvidenceLine.Of("airportUnknown", ("airport", airport)))];
            }

            var touchdown = TrackLines.At(points[landing]);
            var distance = places.Min(place => GreatCircle.DistanceNm(place, touchdown));
            var away = distance > radius;
            return [(away ? LineState.Failed : LineState.Fine, EvidenceLine.Of(away ? "landedAway" : "landedAt",
                ("airport", airport),
                ("distance", distance.ToString("0.0", CultureInfo.InvariantCulture)),
                ("radius", radius.ToString(CultureInfo.InvariantCulture))))];
        });
    }
}

/// <summary>
/// The take-off roll started at the threshold (design M2 §6.4). The runway is the one whose heading is nearest the heading at
/// lift-off and whose centre line the roll is on (parallel runways); the roll starts at the last point below 30 kt before
/// lift-off, moved back by the distance it took to reach that speed — the tracker samples every 15 seconds, so the aircraft
/// is rarely caught standing on the runway, and a rolling take-off never is. Beyond the division's
/// <c>thresholdToleranceMeters</c> (150, kept by Carmine on 24 September 2026 after the corpus) the check does not fail: it
/// says «take-off from an intersection», and the validator looks for a published distance from there, which no API gives.
/// </summary>
public sealed class TakeoffFromThresholdCheck : IFlightCheck
{
    /// <summary>The roll is the part of the ground run faster than this.</summary>
    public const int RollKt = 30;

    /// <summary>A runway further than this from the heading at lift-off is not the one used.</summary>
    public const double MaxHeadingApart = 30;

    /// <summary>The roll further than this from a runway's centre line is not on that runway.</summary>
    public const double MaxOffCentreMetres = 150;

    private const double MetresPerSecondPerKnot = 0.514444;

    public string Key => CheckCatalog.TakeoffFromThreshold;

    /// <summary>
    /// How far past the threshold of the runway used the roll started, in metres (never below zero), and which runway; null
    /// when the track does not say.
    /// </summary>
    public static (IvaoRunway Runway, double Metres)? Start(IReadOnlyList<IvaoTrackPointDto> points, IReadOnlyList<IvaoRunway> runways)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(runways);

        var liftOff = TrackLines.Takeoff(points);
        if (liftOff <= 0)
        {
            return null;
        }

        var slow = liftOff - 1;
        while (slow > 0 && points[slow].GroundSpeedKnots >= RollKt)
        {
            slow--;
        }

        if (points[slow].GroundSpeedKnots >= RollKt || !points[slow].OnGround)
        {
            return null;
        }

        var start = TrackLines.At(points[slow]);
        var heading = points[liftOff].Heading;
        var measured = runways
            .Where(runway => runway is { Bearing: not null, Latitude: not null, Longitude: not null })
            .Where(runway => TrackLines.Apart(runway.Bearing!.Value, heading) <= MaxHeadingApart)
            .Select(runway =>
            {
                var threshold = new GeoPoint(runway.Latitude!.Value, runway.Longitude!.Value);
                var distance = TrackLines.Metres(threshold, start);
                var angle = double.DegreesToRadians(TrackLines.Bearing(threshold, start) - runway.Bearing!.Value);
                return (Runway: runway, Along: distance * Math.Cos(angle), Across: Math.Abs(distance * Math.Sin(angle)));
            })
            .Where(runway => runway.Across <= MaxOffCentreMetres)
            .OrderBy(runway => runway.Across)
            .FirstOrDefault();

        if (measured.Runway is null)
        {
            return null;
        }

        // Back to where it stood: v² / 2a, with the acceleration of the next sample.
        var along = measured.Along;
        var speed = points[slow].GroundSpeedKnots * MetresPerSecondPerKnot;
        var next = points[slow + 1];
        var seconds = (next.At - points[slow].At).TotalSeconds;
        var acceleration = seconds > 0 ? ((next.GroundSpeedKnots * MetresPerSecondPerKnot) - speed) / seconds : 0;
        if (acceleration > 0.3)
        {
            along -= speed * speed / (2 * acceleration);
        }

        return (measured.Runway, Math.Max(0, along));
    }

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tolerance = context.Settings.ThresholdToleranceMeters;
        return TrackLines.PerFlight(context, (flight, points) =>
        {
            var runways = context.Runways.GetValueOrDefault(flight.DepartureIcao, []);
            if (runways.Count == 0)
            {
                return [(LineState.Unknown, EvidenceLine.Of("runwaysUnknown", ("airport", flight.DepartureIcao)))];
            }

            if (Start(points, runways) is not { } start)
            {
                return [(LineState.Unknown, EvidenceLine.Of("rollNotFound", ("airport", flight.DepartureIcao)))];
            }

            // Never a failure (§6.4): beyond the tolerance it tells the validator where to look.
            var intersection = start.Metres > tolerance;
            return [(LineState.Fine, EvidenceLine.Of(intersection ? "takeoffFromIntersection" : "takeoffFromThreshold",
                ("airport", flight.DepartureIcao),
                ("runway", start.Runway.Designator.StartsWith("RW", StringComparison.Ordinal) ? start.Runway.Designator[2..] : start.Runway.Designator),
                ("metres", TrackLines.Whole(start.Metres)),
                ("tolerance", tolerance.ToString(CultureInfo.InvariantCulture))))];
        });
    }
}

/// <summary>
/// VMC at the departure and the arrival, on the VFR parts of a plan only (design M2 §6.4): both ends of a V plan, the arrival
/// of a Y (IFR then VFR), the departure of a Z. Read on the METAR kept for that airport nearest the take-off or the landing,
/// an hour either way at most; without one that end is not available. The ceiling — the lowest broken or overcast layer, or
/// the vertical visibility — below <c>minCloudBaseFeet</c>, or the visibility below <c>minVisibilityMeters</c>, fails.
/// </summary>
public sealed class VmcCheck : IFlightCheck
{
    public static readonly TimeSpan Nearest = TimeSpan.FromHours(1);

    public string Key => CheckCatalog.Vmc;

    public CheckVerdict Evaluate(FlightCheckContext context, JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        var visibility = TrackLines.Parameter(parameters, Key, "minVisibilityMeters");
        var ceiling = TrackLines.Parameter(parameters, Key, "minCloudBaseFeet");

        var lines = new List<(LineState State, EvidenceLine Line)>();
        foreach (var flight in context.Flights)
        {
            var rules = flight.PlanAtTakeoff is { } plan ? PlanLines.Rules(plan) : "I";
            var ends = new List<(string Airport, DateTime At)>();
            if (rules is "V" or "Z")
            {
                ends.Add((flight.DepartureIcao, flight.TakeoffAt));
            }

            if (rules is "V" or "Y" && flight.LandingAt is { } landing)
            {
                ends.Add((context.ExpectedArrival(flight), landing));
            }

            if (ends.Count == 0)
            {
                lines.Add((LineState.Fine, PlanLines.Numbered(EvidenceLine.Of("vmcNotVfr", ("rules", rules)), flight, context)));
                continue;
            }

            foreach (var (airport, at) in ends)
            {
                var metar = context.Metars
                    .Where(report => string.Equals(report.Icao, airport, StringComparison.OrdinalIgnoreCase))
                    .Where(report => (report.IssuedAt - at).Duration() <= Nearest)
                    .MinBy(report => (report.IssuedAt - at).Duration());
                if (metar is null || MetarReading.Read(metar.Raw) is not { } weather)
                {
                    lines.Add((LineState.Unknown, PlanLines.Numbered(
                        EvidenceLine.Of("vmcNoMetar", ("airport", airport), ("at", TrackLines.Time(at))), flight, context)));
                    continue;
                }

                var below = weather.VisibilityMetres < visibility || weather.CeilingFeet < ceiling;
                lines.Add((below ? LineState.Failed : LineState.Fine, PlanLines.Numbered(EvidenceLine.Of(below ? "vmcNotMet" : "vmcMet",
                    ("airport", airport),
                    ("metar", metar.Raw),
                    ("visibility", weather.VisibilityMetres.ToString(CultureInfo.InvariantCulture)),
                    ("ceiling", weather.CeilingFeet?.ToString(CultureInfo.InvariantCulture) ?? "—"),
                    ("minVisibility", visibility.ToString(CultureInfo.InvariantCulture)),
                    ("minCeiling", ceiling.ToString(CultureInfo.InvariantCulture))), flight, context)));
            }
        }

        var evidence = lines.Select(line => line.Line).ToArray();
        return lines.Any(line => line.State == LineState.Failed) ? CheckVerdict.Failed(evidence)
            : lines.All(line => line.State == LineState.Unknown) ? CheckVerdict.Unavailable(evidence)
            : CheckVerdict.Passed(evidence);
    }
}
