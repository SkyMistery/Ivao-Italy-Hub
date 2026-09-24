using System.Text.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Threads;
using IvaoHub.Modules.FlightOps.Tours;
using IvaoHub.Modules.FlightOps.Weather;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>A tour a pilot reads or reports on, with its container when it is a subtour.</summary>
public sealed record PilotTour(Tour Tour, Tour? Parent)
{
    /// <summary>A new report is taken while the tour is open, or closing for flights that took off by the close (§1.2.1).</summary>
    public bool TakesReports(DateTime now) =>
        Tour.Kind != TourKind.Container && TourState.Of(Tour, now) is TourStateKind.Open or TourStateKind.Closing;
}

/// <summary>
/// A pilot's report, from the choice of the flight to the row in the queue (design M2 §3.2): the sessions of the tracker
/// the pilot may choose from, the checks that <b>block</b> the send (point 5) — each refused under the field it is about —,
/// and the report written with what it is judged against frozen (§5.4). Sending again after «to modify» runs the same
/// checks without the report being corrected, and freezes nothing again; withdrawing lets its sessions go.
/// <para>Everything the checks decide is in pure functions — <see cref="TourRules"/>, <see cref="OpenRules"/>,
/// <see cref="DailyLimits"/>, <see cref="CallsignRules"/> — and this class only gathers what they need.</para>
/// </summary>
public sealed class PirepSubmission(
    FlightOpsDbContext database,
    HubDbContext hub,
    IIvaoApiClient ivao,
    IAirportDirectory airports,
    IRunwayDirectory runways,
    IAircraftTypeDirectory aircraftTypes,
    PilotProgress progress,
    AtcProposer atc,
    EffectiveRules effectiveRules,
    ModuleSettingsStore settingsStore,
    WeatherArchive weatherArchive,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>The snapshots as the columns hold them: the web's names, enums by name, and a translated text as the map of its languages.</summary>
    private static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory(), new JsonStringEnumConverter() },
    };

    /// <summary>
    /// The tour, if a pilot may see it: ready, not hidden, released or previewed, and its container too when it is a subtour.
    /// A hidden tour is gone for the pilots who started it as well (design M2 §1.2.2).
    /// </summary>
    public async Task<PilotTour?> TourAsync(long tourId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tour = await database.Tours.AsNoTracking().FirstOrDefaultAsync(row => row.Id == tourId, cancellationToken);
        if (tour is null || !TourState.IsPublic(tour, now))
        {
            return null;
        }

        if (tour.ParentTourId is not { } parentId)
        {
            return new PilotTour(tour, null);
        }

        var parent = await database.Tours.AsNoTracking().FirstOrDefaultAsync(row => row.Id == parentId, cancellationToken);
        return parent is not null && TourState.IsPublic(parent, now) ? new PilotTour(tour, parent) : null;
    }

    /// <summary>
    /// The sessions the pilot may report on this tour (§3.2 point 2): theirs, in the report window and not before the
    /// release, not after the close, not claimed by another report — between the leg's airports when there is a leg, or
    /// between the ones asked for (the flight after a diversion). Null when the tracker could not be asked.
    /// </summary>
    public async Task<IReadOnlyList<TrackerSessionDto>?> SessionsAsync(
        PilotTour pilot,
        Leg? leg,
        string? departure,
        string? arrival,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pilot);

        var now = clock.UtcNow;
        var tour = pilot.Tour;
        var from = new[] { now.AddDays(-tour.ReportWindowDays), tour.ReleaseAt ?? DateTime.MinValue, leg?.ReleaseAt ?? DateTime.MinValue }.Max();

        var found = await ivao.SearchSessionsAsync(
            new IvaoSessionQuery(currentUser.Vid, from, now, departure ?? leg?.DepartureIcao, arrival ?? leg?.ArrivalIcao),
            cancellationToken);
        if (found is null)
        {
            return null;
        }

        var ids = found.Select(session => (long?)session.Id).ToArray();
        var claimed = await database.PirepFlights.AsNoTracking()
            .Where(flight => ids.Contains(flight.ClaimedSessionId))
            .Select(flight => flight.ClaimedSessionId!.Value)
            .ToListAsync(cancellationToken);

        return
        [
            .. found
                .Where(session => session.HasFlightPlan && !claimed.Contains(session.Id) && session.StartedAt <= tour.CloseAt)
                .OrderByDescending(session => session.StartedAt)
                .Select(session => new TrackerSessionDto(
                    session.Id,
                    session.Callsign,
                    session.StartedAt,
                    session.EndedAt,
                    session.DepartureIcao,
                    session.ArrivalIcao,
                    session.AircraftIcao)),
        ];
    }

    /// <summary>
    /// Sends a report, or sends again one the validator returned (<paramref name="correcting"/>, tracked, with its flights).
    /// Returns the report written, or the refusals field by field.
    /// </summary>
    public async Task<(Pirep? Pirep, IReadOnlyDictionary<string, string[]>? Problems)> SendAsync(
        PilotTour pilot,
        PirepWriteDto payload,
        Pirep? correcting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        ArgumentNullException.ThrowIfNull(payload);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var vid = currentUser.Vid;
        var tour = pilot.Tour;
        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        var correctingId = correcting?.Id ?? 0;

        if (correcting is null && !pilot.TakesReports(now))
        {
            return (null, problems.Add("tour", "flightops:errors.reportTourClosed").Errors);
        }

        var legs = await database.Legs.AsNoTracking().Where(row => row.TourId == tour.Id).ToListAsync(cancellationToken);
        Leg? leg = null;

        if (tour.Kind == TourKind.Open)
        {
            if (payload.LegId is not null && correcting is null)
            {
                problems.Add("legId", "flightops:errors.reportNoLegOnOpen");
            }
        }
        else
        {
            // A correction reports the same leg: the leg it froze is the one it is judged on.
            var legId = correcting is null ? payload.LegId : correcting.LegId;
            leg = legs.FirstOrDefault(row => row.Id == legId);
            if (leg is null || leg.IsRetired)
            {
                problems.Add("legId", "flightops:errors.reportLegGone");
            }
        }

        var mine = await database.Pireps.AsNoTracking()
            .Where(report => report.TourId == tour.Id && report.Vid == vid && report.Id != correctingId)
            .ToListAsync(cancellationToken);

        foreach (var key in await PilotRefusalsAsync(pilot, mine, cancellationToken))
        {
            problems.Add("tour", key);
        }

        var ids = payload.SessionIds ?? [];
        if (ids.Count != (payload.IsDiversion ? 2 : 1) || ids.Distinct().Count() != ids.Count)
        {
            problems.Add("sessionIds", "flightops:errors.reportSessionCount");
        }

        var diversion = payload.IsDiversion ? Upper(payload.DiversionIcao) : null;
        if (payload.IsDiversion && diversion is not { Length: 4 })
        {
            problems.Add("diversionIcao", "errors.required");
        }

        if (payload.IsDiversion && payload.DiversionReason is null)
        {
            problems.Add("diversionReason", "errors.required");
        }

        var contacts = payload.AtcContacts ?? [];
        var exemptions = payload.Exemptions ?? [];
        foreach (var (field, key) in AtcProposal.Problems(contacts, exemptions))
        {
            problems.Add(field, key);
        }

        if (!problems.IsEmpty)
        {
            return (null, problems.Errors);
        }

        // The flights, read from the tracker: theirs, in the window, not claimed by another report.
        var from = now.AddDays(-tour.ReportWindowDays);
        var flights = await FlightsAsync(ids, vid, from, now, correctingId, checkClaims: true, problems, cancellationToken);
        if (flights is null)
        {
            return (null, problems.Errors);
        }

        var first = flights[0];
        var takeoff = first.TakeoffAt!.Value;

        var departure = leg?.DepartureIcao ?? first.DepartureIcao;
        var arrival = leg?.ArrivalIcao ?? (payload.IsDiversion ? flights[^1].ArrivalIcao : first.ArrivalIcao);

        if (first.DepartureIcao != departure)
        {
            problems.Add("sessionIds", "flightops:errors.reportFlightDeparture");
        }

        if (!payload.IsDiversion && first.ArrivalIcao != arrival)
        {
            problems.Add("sessionIds", "flightops:errors.reportFlightArrival");
        }

        if (payload.IsDiversion)
        {
            var then = flights[1];
            if (diversion == arrival)
            {
                problems.Add("diversionIcao", "flightops:errors.reportDiversionSameAirport");
            }

            // The second flight starts where the first ended, after it, and brings the aircraft where the leg ends.
            if (then.DepartureIcao != diversion || then.TakeoffAt <= takeoff)
            {
                problems.Add("sessionIds", "flightops:errors.reportDiversionContinuity");
            }

            if (then.ArrivalIcao != arrival)
            {
                problems.Add("sessionIds", "flightops:errors.reportFlightArrival");
            }
        }

        // The window, the release and the close, always in UTC (Carmine, 15 September 2026).
        if (takeoff < from)
        {
            problems.Add("sessionIds", "flightops:errors.reportOutsideWindow");
        }

        if (takeoff > tour.CloseAt)
        {
            problems.Add("sessionIds", "flightops:errors.reportAfterClose");
        }

        if (takeoff < tour.ReleaseAt)
        {
            problems.Add("sessionIds", "flightops:errors.reportBeforeRelease");
        }

        await CheckCallsignsAndAircraftAsync(pilot, leg, flights, problems, cancellationToken);
        CheckProcedures(tour, first.FlightRules, payload, problems);

        var distance = leg?.DistanceNm ?? 0m;

        if (leg is not null)
        {
            var hubs = await database.Hubs.AsNoTracking().Where(row => row.TourId == tour.Id).ToListAsync(cancellationToken);
            var rotations = await database.Rotations.AsNoTracking().Where(row => row.TourId == tour.Id).ToListAsync(cancellationToken);

            if (TourRules.Refusal(tour, legs, hubs, rotations, mine, leg.Id, takeoff, settings.RejectGraceHours) is { } refused)
            {
                problems.Add("legId", refused);
            }
        }
        else
        {
            var (open, route) = await OpenFlightAsync(departure, arrival, first, takeoff, cancellationToken);
            distance = route;
            var constraints = await database.TourConstraints.AsNoTracking()
                .Where(row => row.TourId == tour.Id)
                .ToListAsync(cancellationToken);

            if (open is null)
            {
                problems.Add("sessionIds", "flightops:errors.airportUnknown");
            }
            else
            {
                foreach (var key in OpenRules.Problems(open, constraints, mine))
                {
                    problems.Add("sessionIds", key);
                }
            }
        }

        var day = takeoff.Date;
        var counted = await database.Pireps.AsNoTracking()
            .Where(report => report.Vid == vid
                && report.Id != correctingId
                && report.Status != PirepStatus.Rejected
                && report.Status != PirepStatus.Withdrawn
                && report.TakeoffAt >= day
                && report.TakeoffAt < day.AddDays(1))
            .Select(report => new { report.TourId, report.TakeoffAt })
            .ToListAsync(cancellationToken);

        if (DailyLimits.Refusal(
                takeoff,
                tour.Id,
                tour.DailyLegLimit,
                settings.DailyLegLimit,
                [.. counted.Select(report => (report.TourId, report.TakeoffAt))]) is { } limited)
        {
            problems.Add("sessionIds", limited);
        }

        if (!problems.IsEmpty)
        {
            return (null, problems.Errors);
        }

        // The proposal worked out again here, and not taken from the browser: what the report says was proposed is what the
        // archive had (§3.3).
        var (activity, proposed) = await atc.ProposeAsync(flights, diversion, cancellationToken);
        var (flownFrom, flownTo) = AtcProposer.Interval(flights);

        var pirep = correcting ?? new Pirep
        {
            TourId = tour.Id,
            ScopeTourId = tour.ParentTourId ?? tour.Id,
            LegId = leg?.Id,
            Vid = vid,
            SubmittedAt = now,
            OwnerDepartment = tour.OwnerDepartment,
            OwnerDepartmentMask = tour.OwnerDepartmentMask,
        };

        var before = correcting?.Status;

        if (correcting is null)
        {
            // Frozen once, at the first send (§5.4): a correction is judged on what the first send was.
            pirep.RulesSnapshotJson = await RulesSnapshotAsync(tour, cancellationToken);
            pirep.LegSnapshotJson = JsonSerializer.Serialize(
                new SnapshotLegDto(
                    leg?.Id,
                    leg?.Number,
                    departure,
                    arrival,
                    distance,
                    leg?.Callsigns ?? [],
                    EffectiveAircraft(pilot, leg)),
                ColumnJson);
            database.Pireps.Add(pirep);
        }
        else
        {
            database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = payload.RowVersion;
            database.PirepFlights.RemoveRange(pirep.Flights);
            pirep.Flights = [];
            pirep.ResubmittedAt = now;
        }

        pirep.Status = PirepStatus.Queued;
        pirep.QueuedAt = now;

        // Back in the queue for anybody (§3.1): whoever sent it back no longer holds it.
        pirep.AssignedToVid = null;
        pirep.LeaseUntil = null;
        pirep.DepartureIcao = departure;
        pirep.ArrivalIcao = arrival;
        pirep.DistanceNm = distance;
        pirep.TakeoffAt = takeoff;
        pirep.FlightRules = first.FlightRules;
        pirep.Sid = Upper(payload.Sid);
        pirep.Star = Upper(payload.Star);
        pirep.Approach = Upper(payload.Approach);
        pirep.IsDiversion = payload.IsDiversion;
        pirep.DiversionIcao = diversion;
        pirep.DiversionReason = payload.IsDiversion ? payload.DiversionReason : null;
        pirep.DiversionNote = payload.IsDiversion ? Trimmed(payload.DiversionNote) : null;
        pirep.PilotRemarks = Trimmed(payload.PilotRemarks);
        pirep.AtcArchiveAvailable = activity is not null;
        pirep.AtcContactsJson = JsonSerializer.Serialize(AtcProposal.Merge(proposed, contacts), ColumnJson);
        pirep.AtcExemptionsJson = JsonSerializer.Serialize(
            exemptions.Select(exemption =>
            {
                var callsign = AtcProposal.Normalize(exemption.Callsign);
                return new AtcExemptionDto(
                    callsign,
                    exemption.Kind,
                    Trimmed(exemption.Note),
                    AtcProposal.StatusOf(callsign, flownFrom, flownTo, activity),
                    ExemptionCatalog.Softens(exemption.Kind));
            }),
            ColumnJson);

        pirep.Flights.AddRange(flights.Select((flight, index) => new PirepFlight
        {
            Seq = index + 1,
            TrackerSessionId = flight.Session.Id,
            ClaimedSessionId = flight.Session.Id,
            Callsign = CallsignRules.Normalize(flight.Session.Callsign),
            Aircraft = flight.Aircraft,
            DepartureIcao = flight.DepartureIcao,
            ArrivalIcao = flight.ArrivalIcao,
            TakeoffAt = flight.TakeoffAt!.Value,
            LandingAt = flight.LandingAt,
            FlightPlansJson = "[" + string.Join(',', flight.Plans.Select(plan => plan.RawJson)) + "]",
            PlanAtTakeoffRevision = flight.PlanAtTakeoff?.Revision,
            Track = TrackCodec.Encode(flight.Track, now),
        }));

        pirep.Events.Add(new PirepEvent
        {
            FromStatus = before,
            ToStatus = PirepStatus.Queued,
            ByVid = vid,
            At = now,
            Note = correcting is null ? "flightops:events.submitted" : "flightops:events.resubmitted",
        });

        if (correcting is null)
        {
            await EnrolAsync(pilot, vid, now, cancellationToken);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException && IsDuplicate(exception))
        {
            // Two sends of the same session at once: the unique claim decides, and the second is told so.
            database.ChangeTracker.Clear();
            return (null, new Refusals().Add("sessionIds", "flightops:errors.reportSessionClaimed").Errors);
        }

        // The runways of the airports a report touches, for the checks that read them (T1, T18).
        await runways.EnsureAsync([.. new[] { departure, arrival, diversion }.OfType<string>().Distinct()], cancellationToken);

        // The weather of the flight's airports the job did not keep (design M2 §1.13 point 2, T16): never a refused send.
        await weatherArchive.FillFlightAsync(pirep, now, cancellationToken);

        return (pirep, null);
    }

    /// <summary>
    /// The controllers to propose while the pilot fills the form in (§3.3): the pilot's own sessions of the report window,
    /// read as flights, and the archive's positions along them. Null, with the refusal written, when the flights cannot be
    /// read; a proposal that is not <c>Available</c> when there is no archive. Nothing is claimed or written.
    /// </summary>
    public async Task<(AtcProposalDto? Proposal, IReadOnlyDictionary<string, string[]>? Problems)> ProposeAsync(
        PilotTour pilot,
        IReadOnlyList<long> sessionIds,
        string? diversionIcao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        ArgumentNullException.ThrowIfNull(sessionIds);

        var problems = new Refusals();
        if (sessionIds.Count is 0 or > PirepValidation.MaxFlights || sessionIds.Distinct().Count() != sessionIds.Count)
        {
            return (null, problems.Add("sessionIds", "flightops:errors.reportSessionCount").Errors);
        }

        var now = clock.UtcNow;
        var flights = await FlightsAsync(
            sessionIds,
            currentUser.Vid,
            now.AddDays(-pilot.Tour.ReportWindowDays),
            now,
            correctingId: 0,
            checkClaims: false,
            problems,
            cancellationToken);
        if (flights is null)
        {
            return (null, problems.Errors);
        }

        var (activity, proposed) = await atc.ProposeAsync(flights, Upper(diversionIcao), cancellationToken);

        return (
            new AtcProposalDto(
                activity is not null,
                [.. proposed.Select(presence => new AtcContactDto(presence.Callsign, presence.Frequency, AtcContactOrigin.Proposed))],
                atc.Attribution),
            null);
    }

    /// <summary>A report still in the queue, taken back by its pilot (§3.1): the leg is flyable and its sessions free again.</summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> WithdrawAsync(Pirep pirep, DateTime rowVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        if (pirep.Status != PirepStatus.Queued)
        {
            return new Refusals().Add("status", "flightops:errors.reportNotWithdrawable").Errors;
        }

        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = rowVersion;
        Withdraw(pirep, currentUser.Vid, clock.UtcNow, "flightops:events.withdrawn");
        await database.SaveChangesAsync(cancellationToken);

        return null;
    }

    /// <summary>A report withdrawn, by its pilot or by the module (§3.1): its sessions let go, and the step written down.</summary>
    public void Withdraw(Pirep pirep, int byVid, DateTime at, string note)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        foreach (var flight in pirep.Flights)
        {
            flight.ClaimedSessionId = null;
        }

        database.PirepEvents.Add(new PirepEvent
        {
            PirepId = pirep.Id,
            FromStatus = pirep.Status,
            ToStatus = PirepStatus.Withdrawn,
            ByVid = byVid,
            At = at,
            Note = note,
        });
        pirep.Status = PirepStatus.Withdrawn;
    }

    /// <summary>
    /// Where the signed in pilot is in the tour: the colour of every leg, what may be reported now, the goal of an Open
    /// tour, their reports, and why they may send nothing when they may not.
    /// </summary>
    public async Task<MyTourDto> MineAsync(PilotTour pilot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pilot);

        var now = clock.UtcNow;
        var tour = pilot.Tour;
        var vid = currentUser.Vid;
        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);

        var mine = await database.Pireps.AsNoTracking()
            .Include(report => report.Flights)
            .Include(report => report.Errors)
            .Where(report => report.TourId == tour.Id && report.Vid == vid)
            .OrderByDescending(report => report.SubmittedAt)
            .ToListAsync(cancellationToken);

        var standing = await progress.OfAsync(tour, vid, mine, cancellationToken);
        var threads = await PirepDisputes.ThreadsAsync(hub, [.. mine.Where(report => report.DisputeStatus is not null).Select(report => report.Id)], cancellationToken);

        var blocked = !pilot.TakesReports(now)
            ? "flightops:errors.reportTourClosed"
            : (await PilotRefusalsAsync(pilot, mine, cancellationToken)).FirstOrDefault();

        return new MyTourDto(
            tour.Id,
            [.. standing.Progress.Legs.Select(entry => new MyLegDto(entry.Key, entry.Value))],
            [.. standing.Progress.Flyable.Order()],
            standing.Progress.Next,
            standing.Finished,
            blocked,
            standing.Goal,
            [.. mine.Select(report => ToDto(
                report,
                PirepDisputes.DisputableUntil(report, settings.DisputeWindowDays, now),
                threads.TryGetValue(report.Id, out var thread) ? thread : null))]);
    }

    /// <summary>
    /// The report as its pilot reads it. <paramref name="disputableUntil"/> and <paramref name="threadId"/> are the dispute's
    /// (T14b), which need the settings and the core's threads: the pages that show them pass them, the answers of a step leave them out.
    /// </summary>
    public static PirepDto ToDto(Pirep pirep, DateTime? disputableUntil = null, long? threadId = null)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        return new PirepDto(
            pirep.Id,
            pirep.TourId,
            pirep.LegId,
            pirep.Status,
            pirep.IsDisputed,
            pirep.SubmittedAt,
            pirep.ResubmittedAt,
            pirep.DepartureIcao,
            pirep.ArrivalIcao,
            pirep.DistanceNm,
            pirep.TakeoffAt,
            pirep.FlightRules,
            pirep.Sid,
            pirep.Star,
            pirep.Approach,
            pirep.IsDiversion,
            pirep.DiversionIcao,
            pirep.DiversionReason,
            pirep.DiversionNote,
            pirep.PilotRemarks,
            JsonSerializer.Deserialize<List<AtcContactDto>>(pirep.AtcContactsJson, ColumnJson) ?? [],
            JsonSerializer.Deserialize<List<AtcExemptionDto>>(pirep.AtcExemptionsJson, ColumnJson) ?? [],
            pirep.AtcArchiveAvailable,
            [
                .. pirep.Flights.OrderBy(flight => flight.Seq).Select(flight => new PirepFlightDto(
                    flight.Seq,
                    flight.TrackerSessionId,
                    flight.Callsign,
                    flight.Aircraft,
                    flight.DepartureIcao,
                    flight.ArrivalIcao,
                    flight.TakeoffAt,
                    flight.LandingAt,
                    pirep.FlightRules)),
            ],
            pirep.DecidedAt,
            pirep.NoteToPilot,
            ViolatedRules(pirep),
            pirep.RowVersion,
            pirep.DisputeStatus,
            disputableUntil,
            threadId);
    }

    /// <summary>
    /// The rules a decision said were broken (§3.5): those of the frozen rules that carry an error the decision confirmed. The
    /// report's errors have to be loaded; a report without any has none.
    /// </summary>
    public static IReadOnlyList<Review.ViolatedRuleDto> ViolatedRules(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var confirmed = pirep.Errors.Where(error => error.Confirmed).Select(error => error.ErrorId).ToHashSet();
        return confirmed.Count == 0
            ? []
            : [
                .. Snapshot(pirep)
                    .Where(rule => rule.Errors.Any(error => confirmed.Contains(error.Id)))
                    .Select(rule => new Review.ViolatedRuleDto(rule.Code, rule.Title)),
            ];
    }

    /// <summary>The rules the report froze at its first send (§5.4).</summary>
    public static IReadOnlyList<SnapshotRuleDto> Snapshot(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        return JsonSerializer.Deserialize<List<SnapshotRuleDto>>(pirep.RulesSnapshotJson, ColumnJson) ?? [];
    }

    /// <summary>The leg the report froze at its first send (§3.2 point 6).</summary>
    public static SnapshotLegDto LegSnapshot(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        return JsonSerializer.Deserialize<SnapshotLegDto>(pirep.LegSnapshotJson, ColumnJson)
            ?? new SnapshotLegDto(pirep.LegId, null, pirep.DepartureIcao, pirep.ArrivalIcao, pirep.DistanceNm, [], AllowedAircraft.All);
    }

    /// <summary>The controllers and the exemptions of the report, as the columns hold them (§3.3).</summary>
    public static (IReadOnlyList<AtcContactDto> Contacts, IReadOnlyList<AtcExemptionDto> Exemptions) Atc(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        return (
            JsonSerializer.Deserialize<List<AtcContactDto>>(pirep.AtcContactsJson, ColumnJson) ?? [],
            JsonSerializer.Deserialize<List<AtcExemptionDto>>(pirep.AtcExemptionsJson, ColumnJson) ?? []);
    }

    /// <summary>
    /// What keeps a pilot from sending anything on the tour, whatever the flight (§3.2 point 5): a ban, a rating below the
    /// tour's, a report of theirs waiting to be corrected (§3.1).
    /// </summary>
    private async Task<IReadOnlyList<string>> PilotRefusalsAsync(PilotTour pilot, IReadOnlyList<Pirep> mine, CancellationToken cancellationToken)
    {
        var refusals = new List<string>();
        var now = clock.UtcNow;
        var vid = currentUser.Vid;

        var bans = await database.Bans.AsNoTracking().Where(ban => ban.Vid == vid).ToListAsync(cancellationToken);
        if (bans.Any(ban => ban.Holds(pilot.Tour.Id, pilot.Tour.ParentTourId, now)))
        {
            refusals.Add("flightops:errors.reportBanned");
        }

        if (pilot.Tour.MinPilotRating is { } least)
        {
            // From the IVAO profile read at the last login: a pilot just promoted signs in again (§3.2).
            var rating = await hub.Users.AsNoTracking()
                .Where(user => user.Vid == vid)
                .Select(user => user.RatingPilot)
                .FirstOrDefaultAsync(cancellationToken);

            if (rating is null || rating < least)
            {
                refusals.Add("flightops:errors.reportRatingTooLow");
            }
        }

        if (mine.Any(report => report.Status == PirepStatus.ToModify))
        {
            refusals.Add("flightops:errors.reportToModifyPending");
        }

        return refusals;
    }

    /// <summary>The sessions read into flights, in the order sent; null, with the refusal written, when one cannot be.</summary>
    private async Task<IReadOnlyList<TrackedFlight>?> FlightsAsync(
        IReadOnlyList<long> ids,
        int vid,
        DateTime from,
        DateTime to,
        long correctingId,
        bool checkClaims,
        Refusals problems,
        CancellationToken cancellationToken)
    {
        var found = await ivao.SearchSessionsAsync(new IvaoSessionQuery(vid, from, to), cancellationToken);
        if (found is null)
        {
            problems.Add("sessionIds", "flightops:errors.trackerUnavailable");
            return null;
        }

        var wanted = ids.Select(id => (long?)id).ToArray();
        var claimed = checkClaims && await database.PirepFlights.AsNoTracking()
            .Where(flight => wanted.Contains(flight.ClaimedSessionId) && flight.PirepId != correctingId)
            .AnyAsync(cancellationToken);
        if (claimed)
        {
            problems.Add("sessionIds", "flightops:errors.reportSessionClaimed");
            return null;
        }

        var flights = new List<TrackedFlight>();
        foreach (var id in ids)
        {
            // Only a session the tracker lists for this pilot in the window: an identifier alone proves nothing.
            var session = found.FirstOrDefault(row => row.Id == id);
            if (session is null)
            {
                problems.Add("sessionIds", "flightops:errors.reportSessionUnknown");
                return null;
            }

            var plans = await ivao.GetFlightPlansAsync(id, cancellationToken);
            var track = await ivao.GetTracksAsync(id, cancellationToken);
            if (plans is null || track is null)
            {
                problems.Add("sessionIds", "flightops:errors.trackerUnavailable");
                return null;
            }

            var flight = TrackedFlight.Read(session, plans, track);
            if (flight.TakeoffAt is null)
            {
                problems.Add("sessionIds", "flightops:errors.reportNeverTookOff");
                return null;
            }

            flights.Add(flight);
        }

        return flights;
    }

    /// <summary>
    /// The callsign and the aircraft of every flight (answer 5): the callsign by the allows of the nearest level and every
    /// deny (§1.6), the aircraft by the leg's list, else the tour's, else the container's (§2.7), groups expanded.
    /// </summary>
    private async Task CheckCallsignsAndAircraftAsync(
        PilotTour pilot,
        Leg? leg,
        IReadOnlyList<TrackedFlight> flights,
        Refusals problems,
        CancellationToken cancellationToken)
    {
        var tourIds = new[] { pilot.Tour.Id, pilot.Parent?.Id ?? 0 };
        var rules = await database.CallsignRules.AsNoTracking()
            .Where(rule => tourIds.Contains(rule.TourId))
            .ToListAsync(cancellationToken);

        IReadOnlyList<IReadOnlyList<CallsignRule>> levels =
        [
            [.. rules.Where(rule => leg is not null && rule.LegId == leg.Id)],
            [.. rules.Where(rule => rule.TourId == pilot.Tour.Id && rule.LegId == null)],
            [.. rules.Where(rule => pilot.Parent is not null && rule.TourId == pilot.Parent.Id && rule.LegId == null)],
        ];

        foreach (var flight in flights)
        {
            switch (CallsignRules.Judge(flight.Session.Callsign, levels))
            {
                case CallsignVerdict.Denied:
                    problems.Add("sessionIds", "flightops:errors.reportCallsignDenied");
                    break;
                case CallsignVerdict.NotAllowed:
                    problems.Add("sessionIds", "flightops:errors.reportCallsignNotAllowed");
                    break;
                default:
                    break;
            }
        }

        var allowed = EffectiveAircraft(pilot, leg);
        if (allowed.Types.Count == 0 && allowed.GroupIds.Count == 0)
        {
            return;
        }

        var groupIds = allowed.GroupIds.ToArray();
        var grouped = await database.AircraftGroups.AsNoTracking()
            .Where(group => groupIds.Contains(group.Id))
            .ToListAsync(cancellationToken);
        var types = allowed.Types
            .Concat(grouped.SelectMany(group => group.IcaoTypes))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (flights.Any(flight => flight.Aircraft is not { } aircraft || !types.Contains(aircraft)))
        {
            problems.Add("sessionIds", "flightops:errors.reportAircraftNotAllowed");
        }
    }

    /// <summary>
    /// SID, STAR and approach when the tour asks for them (answer 4), by the flight rules of the plan at take-off: <c>I</c>
    /// all three, <c>Y</c> (IFR then VFR) the SID only, <c>Z</c> (VFR then IFR) STAR and approach only, <c>V</c> none.
    /// </summary>
    private static void CheckProcedures(Tour tour, string flightRules, PirepWriteDto payload, Refusals problems)
    {
        if (!tour.RequiresProcedures)
        {
            return;
        }

        var departing = flightRules is "I" or "Y";
        var arriving = flightRules is "I" or "Z";

        if (departing && string.IsNullOrWhiteSpace(payload.Sid))
        {
            problems.Add("sid", "flightops:errors.reportProcedureRequired");
        }

        if (arriving && string.IsNullOrWhiteSpace(payload.Star))
        {
            problems.Add("star", "flightops:errors.reportProcedureRequired");
        }

        if (arriving && string.IsNullOrWhiteSpace(payload.Approach))
        {
            problems.Add("approach", "flightops:errors.reportProcedureRequired");
        }
    }

    /// <summary>The flight of an Open tour with the facts its filters read, and the great circle of its route.</summary>
    private async Task<(OpenFlight? Flight, decimal DistanceNm)> OpenFlightAsync(
        string departure,
        string arrival,
        TrackedFlight first,
        DateTime takeoff,
        CancellationToken cancellationToken)
    {
        var known = await airports.FindAsync([departure, arrival], cancellationToken);
        if (!known.TryGetValue(departure, out var from) || !known.TryGetValue(arrival, out var to)
            || from.Latitude is not { } fromLatitude || from.Longitude is not { } fromLongitude
            || to.Latitude is not { } toLatitude || to.Longitude is not { } toLongitude)
        {
            return (null, 0m);
        }

        var distance = GreatCircle.DistanceNmRounded(new GeoPoint(fromLatitude, fromLongitude), new GeoPoint(toLatitude, toLongitude));

        await runways.EnsureAsync([arrival], cancellationToken);
        var longest = (await runways.GetAsync(arrival, cancellationToken)).Max(runway => runway.LengthMetres);
        var wake = first.Aircraft is { } aircraft
            ? (await aircraftTypes.WakeCategoriesAsync([aircraft], cancellationToken)).GetValueOrDefault(aircraft)
            : null;

        return (
            new OpenFlight(
                departure,
                arrival,
                from.CountryId,
                to.CountryId,
                fromLongitude,
                toLongitude,
                distance,
                takeoff,
                wake,
                first.FlightRules,
                longest,
                to.ElevationFeet),
            distance);
    }

    /// <summary>The rules in force on the tour, with their parameters composed and their errors (§5.4), as JSON.</summary>
    private async Task<string> RulesSnapshotAsync(Tour tour, CancellationToken cancellationToken)
    {
        var rules = await effectiveRules.ForTourAsync(tour, cancellationToken);
        var errorIds = rules.SelectMany(rule => rule.ErrorIds).Distinct().ToArray();
        var errors = await database.Errors.AsNoTracking()
            .Where(error => errorIds.Contains(error.Id))
            .ToDictionaryAsync(error => error.Id, cancellationToken);

        return JsonSerializer.Serialize(
            rules.Select(rule => new SnapshotRuleDto(
                rule.Rule.Id,
                rule.Rule.AmendsRuleId,
                rule.Rule.Code,
                rule.Rule.Title,
                rule.Rule.Text,
                rule.CheckKey,
                rule.Parameters,
                [
                    .. rule.ErrorIds
                        .Where(errors.ContainsKey)
                        .Select(id => new SnapshotErrorDto(id, errors[id].Name, errors[id].Category, errors[id].YearlyMax)),
                ])),
            ColumnJson);
    }

    /// <summary>The first report enrols the pilot in the tour, and in its container when it is a subtour (§1.9, §2.7).</summary>
    private async Task EnrolAsync(PilotTour pilot, int vid, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var tourId in new[] { pilot.Tour.Id, pilot.Parent?.Id }.OfType<long>())
        {
            var enrolled = await database.Enrolments.AnyAsync(row => row.TourId == tourId && row.Vid == vid, cancellationToken)
                || database.Enrolments.Local.Any(row => row.TourId == tourId && row.Vid == vid);
            if (!enrolled)
            {
                database.Enrolments.Add(new Enrolment { TourId = tourId, Vid = vid, StartedAt = now });
            }
        }
    }

    /// <summary>The aircraft a leg admits: its own, else its tour's, else the container's (design M2 §2.7).</summary>
    private static AllowedAircraft EffectiveAircraft(PilotTour pilot, Leg? leg)
    {
        static bool Any(AllowedAircraft allowed) => allowed.Types.Count > 0 || allowed.GroupIds.Count > 0;

        if (leg is not null && Any(leg.Aircraft))
        {
            return leg.Aircraft;
        }

        return Any(pilot.Tour.AllowedAircraft) ? pilot.Tour.AllowedAircraft : pilot.Parent?.AllowedAircraft ?? AllowedAircraft.All;
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static string? Upper(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToUpperInvariant();

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Refusals as the form reads them: one or more i18n keys per field.</summary>
    private sealed class Refusals
    {
        private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

        public bool IsEmpty => _errors.Count == 0;

        public IReadOnlyDictionary<string, string[]> Errors =>
            _errors.ToDictionary(entry => entry.Key, entry => entry.Value.Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        public Refusals Add(string field, string key)
        {
            if (!_errors.TryGetValue(field, out var keys))
            {
                _errors[field] = keys = [];
            }

            keys.Add(key);
            return this;
        }
    }
}
