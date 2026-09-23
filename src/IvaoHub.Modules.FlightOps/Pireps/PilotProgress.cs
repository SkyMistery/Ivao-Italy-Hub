using System.Text.Json.Nodes;
using IvaoHub.Core.Airspace;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Shape;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>What a measure of progress counts: legs, nautical miles, the goal of an <c>Open</c> tour, subtours.</summary>
public enum ProgressUnit
{
    Legs,
    Miles,
    Goal,
    Subtours,
}

/// <summary>
/// Where a pilot stands in a tour: the colours of its legs (<see cref="TourRules"/>), the goal of an <c>Open</c> tour
/// (<see cref="OpenRules"/>), the subtours done of a <c>Container</c>, and whether it is finished — with one measure, done out
/// of how many, that a card or a list shows whatever the kind.
/// </summary>
public sealed record PilotStanding(TourProgress Progress, OpenProgress? Goal, bool Finished, int Done, int Target, ProgressUnit Unit);

/// <summary>
/// The one answer to «where is this pilot in this tour» (design M2 §2), for the pilot's own page (T11b), the completion and the
/// award signal, the block <c>flightops.myTours</c> and the pilot's page of the staff (T15). It gathers what the pure rules need
/// and adds the one kind they cannot answer alone: a <c>Container</c> is finished when <c>required_subtours</c> of its subtours
/// are (§2.7), which is read off the enrolments of the subtours — each written by the completion of its own.
/// </summary>
public sealed class PilotProgress(
    FlightOpsDbContext database,
    IAirportDirectory airports,
    IFirLocator firs,
    ModuleSettingsStore settingsStore,
    IClock clock)
{
    /// <summary>
    /// The pilot's standing in the tour. <paramref name="reports"/> are their reports on it, every state, when the caller has
    /// them already — the completion passes the one being decided as it is about to be saved; null reads them.
    /// </summary>
    public async Task<PilotStanding> OfAsync(Tour tour, int vid, IReadOnlyList<Pirep>? reports, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tour);

        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        reports ??= await database.Pireps.AsNoTracking()
            .Include(report => report.Flights)
            .Where(report => report.TourId == tour.Id && report.Vid == vid)
            .ToListAsync(cancellationToken);

        var legs = await database.Legs.AsNoTracking().Where(row => row.TourId == tour.Id).ToListAsync(cancellationToken);
        var hubs = await database.Hubs.AsNoTracking().Where(row => row.TourId == tour.Id).ToListAsync(cancellationToken);
        var rotations = await database.Rotations.AsNoTracking().Where(row => row.TourId == tour.Id).ToListAsync(cancellationToken);
        var progress = TourRules.Of(tour, legs, hubs, rotations, reports, clock.UtcNow, settings.RejectGraceHours);

        switch (tour.Kind)
        {
            case TourKind.Open when tour.OpenGoal is { } kind:
            {
                var constraints = await database.TourConstraints.AsNoTracking()
                    .Where(row => row.TourId == tour.Id)
                    .ToListAsync(cancellationToken);
                var parameters = OpenCatalog.Parse(tour.OpenGoalJson);
                var goal = OpenRules.Progress(kind, parameters, constraints, reports, await FactsAsync(kind, parameters, reports, cancellationToken));
                return new PilotStanding(progress, goal, goal.Finished, goal.Done, goal.Target, ProgressUnit.Goal);
            }

            case TourKind.Open:
                return new PilotStanding(progress, null, false, 0, 0, ProgressUnit.Goal);

            case TourKind.Container:
            {
                var done = await SubtoursDoneAsync(tour.Id, vid, cancellationToken);
                var required = tour.RequiredSubtours ?? 0;
                return new PilotStanding(progress, null, required > 0 && done >= required, done, required, ProgressUnit.Subtours);
            }

            case TourKind.Distance:
            {
                var flown = legs.Where(leg => progress.Legs.TryGetValue(leg.Id, out var colour) && colour == LegProgress.Done).Sum(leg => leg.DistanceNm);
                return new PilotStanding(progress, null, progress.Finished, (int)flown, tour.RequiredNm ?? 0, ProgressUnit.Miles);
            }

            default:
            {
                // What finishes a Hub tour is its normal legs: the positioning ones are the way between hubs (TourRules).
                var counted = legs.Where(leg => progress.Legs.ContainsKey(leg.Id) && (tour.Kind != TourKind.Hub || leg.Kind == LegKind.Normal)).ToList();
                var done = counted.Count(leg => progress.Legs[leg.Id] == LegProgress.Done);
                return new PilotStanding(progress, null, progress.Finished, done, counted.Count, ProgressUnit.Legs);
            }
        }
    }

    /// <summary>How many subtours of the container the pilot has completed, counting a completion written in this very save.</summary>
    public async Task<int> SubtoursDoneAsync(long containerId, int vid, CancellationToken cancellationToken)
    {
        var subtours = await database.Tours.AsNoTracking()
            .Where(row => row.ParentTourId == containerId)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken);

        var saved = await database.Enrolments.AsNoTracking()
            .Where(row => row.Vid == vid && subtours.Contains(row.TourId) && row.CompletedAt != null)
            .Select(row => row.TourId)
            .ToListAsync(cancellationToken);

        var pending = database.Enrolments.Local
            .Where(row => row.Vid == vid && subtours.Contains(row.TourId) && row.CompletedAt is not null)
            .Select(row => row.TourId);

        return saved.Union(pending).Count();
    }

    /// <summary>The countries and the regions of the airports the accepted flights touched, for the goals that count them.</summary>
    private async Task<OpenFacts> FactsAsync(OpenGoal goal, JsonObject parameters, IReadOnlyList<Pirep> reports, CancellationToken cancellationToken)
    {
        if (goal is not (OpenGoal.DistinctCountries or OpenGoal.CollectRegions))
        {
            return OpenFacts.None;
        }

        var touched = reports
            .Where(report => report.Status == PirepStatus.Accepted)
            .SelectMany(report => new[] { report.DepartureIcao, report.ArrivalIcao })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var known = await airports.FindAsync(touched, cancellationToken);
        var regions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        if (goal == OpenGoal.CollectRegions && OpenCatalog.Codes(parameters, "firs").Count > 0)
        {
            foreach (var airport in known.Values)
            {
                if (airport.Latitude is { } latitude && airport.Longitude is { } longitude)
                {
                    regions[airport.Icao] = await firs.LocateAsync(latitude, longitude, cancellationToken);
                }
            }
        }

        return new OpenFacts(known.ToDictionary(entry => entry.Key, entry => entry.Value.CountryId, StringComparer.Ordinal), regions);
    }
}
