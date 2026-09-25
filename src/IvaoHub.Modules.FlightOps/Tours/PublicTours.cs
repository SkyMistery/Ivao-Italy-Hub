using System.Text.Json.Nodes;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Legs;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Shape;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>
/// The tours as the site shows them (design M2 §8.1): the cards of <c>/tours</c> and the page of <c>/tours/{slug}</c>,
/// composed once here and read by the two endpoints and by the block <c>flightops.tourCards</c>.
/// <para>What is public is <see cref="TourState.IsPublic"/> and nothing else: the row's own visibility column is coarse —
/// a column cannot follow the clock — so it says "ready, not hidden, not a template" and the release is asked here. A
/// member of staff reading these two addresses sees exactly what a visitor sees; the draft is in the back office.</para>
/// <para>A subtour is not on <c>/tours</c>: it has a page of its own and its container lists it (note
/// 2026-09-21-la-forma-dei-tour).</para>
/// </summary>
public sealed class PublicTours(
    FlightOpsDbContext database,
    LegBook legs,
    EffectiveRules effective,
    IClock clock)
{
    private static readonly ShapeMapper Shapes = new();

    /// <summary>The states a card may be in: a closed tour is history, and history is not on the front page.</summary>
    public static readonly IReadOnlyList<TourStateKind> CardStates =
        [TourStateKind.Open, TourStateKind.Closing, TourStateKind.Upcoming];

    /// <summary>
    /// The cards of the tours the public sees now, newest release first among those still running and soonest first
    /// among those still to come — which is the same order: by release, descending for what is open and ascending for
    /// what is not yet. Kept simple: by release date, the running ones first.
    /// </summary>
    public async Task<IReadOnlyList<PublicTourCardDto>> CardsAsync(
        IReadOnlyCollection<TourStateKind>? states,
        int? limit,
        CancellationToken cancellationToken)
    {
        // Null is "whatever a card can be"; an empty list is a caller that asked for none of them (`TourCardsProvider`).
        var wanted = states ?? CardStates;
        var now = clock.UtcNow;

        var tours = await Public(database.Tours.AsNoTracking())
            .Where(tour => tour.ParentTourId == null)
            .OrderBy(tour => tour.ReleaseAt)
            .ToListAsync(cancellationToken);

        var shown = tours
            .Where(tour => wanted.Contains(TourState.Of(tour, now)))
            .ToList();

        var counted = await CountLegsAsync([.. shown.Select(tour => tour.Id)], cancellationToken);

        return
        [
            .. shown
                .OrderBy(tour => TourState.Of(tour, now) == TourStateKind.Upcoming ? 1 : 0)
                .ThenBy(tour => tour.ReleaseAt)
                .Take(limit is { } most && most > 0 ? most : int.MaxValue)
                .Select(tour => new PublicTourCardDto(
                    tour.Id,
                    tour.Slug!,
                    tour.Kind,
                    tour.Title,
                    tour.Summary,
                    tour.CoverMediaId,
                    TourState.Of(tour, now),
                    tour.ReleaseAt!.Value,
                    tour.CloseAt!.Value,
                    counted.GetValueOrDefault(tour.Id).Legs,
                    counted.GetValueOrDefault(tour.Id).TotalNm)),
        ];
    }

    /// <summary>One tour by its address, with everything its page shows; null when nobody outside the staff may see it.</summary>
    public async Task<PublicTourDto?> ReadAsync(string slug, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tour = await Public(database.Tours.AsNoTracking())
            .FirstOrDefaultAsync(row => row.Slug == slug, cancellationToken);

        if (tour is null)
        {
            return null;
        }

        // A subtour of a container nobody may see yet is not public either: its page is inside its parent's period.
        var parent = tour.ParentTourId is { } parentId
            ? await Public(database.Tours.AsNoTracking()).FirstOrDefaultAsync(row => row.Id == parentId, cancellationToken)
            : null;

        if (tour.ParentTourId is not null && parent is null)
        {
            return null;
        }

        var rows = await database.Legs.AsNoTracking()
            .Where(leg => leg.TourId == tour.Id && leg.RetiredAt == null)
            .OrderBy(leg => leg.Number)
            .ToListAsync(cancellationToken);

        var grid = await legs.GridAsync(tour, rows, cancellationToken);
        var located = rows.ToDictionary(leg => leg.Id);

        // Two reads and the pairing here: a join across two sets the module's own filters narrow is not a query the
        // provider translates, and a rotation and its hub are a handful of rows either way.
        var hubs = await database.Hubs.AsNoTracking()
            .Where(hub => hub.TourId == tour.Id)
            .ToDictionaryAsync(hub => hub.Id, hub => hub.Icao, cancellationToken);
        var rotations = (await database.Rotations.AsNoTracking()
            .Where(rotation => rotation.TourId == tour.Id)
            .OrderBy(rotation => rotation.Sort)
            .ToListAsync(cancellationToken))
            .Select(rotation => new PublicRotationDto(
                rotation.Id,
                hubs.GetValueOrDefault(rotation.HubId, string.Empty),
                rotation.Sort,
                rotation.Size))
            .ToList();

        var constraints = await database.TourConstraints.AsNoTracking()
            .Where(constraint => constraint.TourId == tour.Id)
            .ToListAsync(cancellationToken);

        var callsigns = await database.CallsignRules.AsNoTracking()
            .Where(rule => rule.TourId == tour.Id)
            .ToListAsync(cancellationToken);

        var rules = await effective.ForTourAsync(tour, cancellationToken);
        var errors = await PublicErrorsAsync(rules, cancellationToken);

        var subtours = tour.Kind == TourKind.Container
            ? await SubtoursAsync(tour.Id, now, cancellationToken)
            : [];

        var groups = await GroupNamesAsync(
            [.. rows.SelectMany(leg => leg.Aircraft.GroupIds).Concat(tour.AllowedAircraft.GroupIds)],
            cancellationToken);

        var numbers = rows.Where(leg => leg.RotationId is not null).ToDictionary(leg => leg.Id, leg => leg.RotationId);

        return new PublicTourDto(
            tour.Id,
            tour.Slug!,
            tour.Kind,
            tour.Title,
            tour.Summary,
            JsonNode.Parse(tour.BriefingJson) ?? new JsonObject(),
            tour.CoverMediaId,
            tour.BannerMediaId,
            TourState.Of(tour, now),
            tour.ReleaseAt!.Value,
            tour.CloseAt!.Value,
            tour.ReportWindowDays,
            tour.Progression,
            tour.HubRotationOrder,
            tour.RequiresProcedures,
            tour.MinPilotRating,
            tour.ReferenceAircraftIcao,
            Aircraft(tour.AllowedAircraft, groups),
            tour.RequiredNm,
            tour.RequiredSubtours,
            tour.OpenGoal,
            tour.OpenGoal is null ? null : OpenCatalog.Parse(tour.OpenGoalJson),
            tour.OpenGoal is { } goal
                ? OpenCatalog.Describe(OpenCatalog.Fields(goal), OpenCatalog.Parse(tour.OpenGoalJson))
                : [],
            [.. constraints.Select(ConstraintMapper.ToList)],
            [.. callsigns.Select(rule => Shapes.ToList(rule, rows.FirstOrDefault(leg => leg.Id == rule.LegId)?.Number))],
            [.. rules.Select(RuleMapper.ToDto)],
            errors,
            rotations,
            [
                .. grid.Legs.Select(leg => new PublicLegDto(
                    leg.Id,
                    leg.Number,
                    leg.Kind,
                    numbers.GetValueOrDefault(leg.Id),
                    leg.DepartureIcao,
                    leg.DepartureIata,
                    located[leg.Id].DepartureLatitude,
                    located[leg.Id].DepartureLongitude,
                    leg.ArrivalIcao,
                    leg.ArrivalIata,
                    located[leg.Id].ArrivalLatitude,
                    located[leg.Id].ArrivalLongitude,
                    leg.DistanceNm,
                    leg.EstimatedMinutes,
                    leg.Callsigns,
                    leg.FlightNumbers,
                    Aircraft(located[leg.Id].Aircraft, groups),
                    leg.ReleaseAt,
                    (leg.ReleaseAt ?? tour.ReleaseAt) <= now)),
            ],
            grid.TotalNm,
            grid.TotalEstimatedMinutes,
            parent is null ? null : new PublicParentDto(parent.Id, parent.Slug!, parent.Title),
            subtours,
            tour.OwnerDepartment);
    }

    /// <summary>Ready, not hidden, not archived, not a template, with an address and both its dates, and released or shown as a preview.</summary>
    private IQueryable<Tour> Public(IQueryable<Tour> tours)
    {
        var now = clock.UtcNow;

        return tours.Where(tour =>
            !tour.IsTemplate
            && !tour.IsHidden
            && tour.PurgedAt == null
            && tour.Status == PublishStatus.Published
            && tour.Slug != null
            && tour.ReleaseAt != null
            && tour.CloseAt != null
            && (tour.ShowPreview || tour.ReleaseAt <= now));
    }

    private async Task<IReadOnlyList<PublicSubtourDto>> SubtoursAsync(long parentId, DateTime now, CancellationToken cancellationToken)
    {
        var subtours = await Public(database.Tours.AsNoTracking())
            .Where(tour => tour.ParentTourId == parentId)
            .OrderBy(tour => tour.ReleaseAt)
            .ThenBy(tour => tour.Id)
            .ToListAsync(cancellationToken);

        var counted = await CountLegsAsync([.. subtours.Select(tour => tour.Id)], cancellationToken);

        return
        [
            .. subtours.Select(tour => new PublicSubtourDto(
                tour.Id,
                tour.Slug!,
                tour.Title,
                tour.Summary,
                TourState.Of(tour, now),
                tour.ReleaseAt!.Value,
                tour.CloseAt!.Value,
                counted.GetValueOrDefault(tour.Id).Legs,
                counted.GetValueOrDefault(tour.Id).TotalNm)),
        ];
    }

    /// <summary>How many legs a tour still flies and how far they go, for the tours of a list: one query, not one each.</summary>
    private async Task<IReadOnlyDictionary<long, (int Legs, decimal TotalNm)>> CountLegsAsync(
        IReadOnlyList<long> tourIds,
        CancellationToken cancellationToken)
    {
        if (tourIds.Count == 0)
        {
            return new Dictionary<long, (int, decimal)>();
        }

        var counted = await database.Legs.AsNoTracking()
            .Where(leg => tourIds.Contains(leg.TourId) && leg.RetiredAt == null)
            .GroupBy(leg => leg.TourId)
            .Select(group => new { TourId = group.Key, Legs = group.Count(), TotalNm = group.Sum(leg => leg.DistanceNm) })
            .ToListAsync(cancellationToken);

        return counted.ToDictionary(row => row.TourId, row => (row.Legs, row.TotalNm));
    }

    /// <summary>
    /// The errors the rules in force can give, of those the division made public (design M2 §5.3). A rule may name an
    /// error the department keeps to itself: the page names what a pilot can be told.
    /// </summary>
    private async Task<IReadOnlyList<PublicErrorDto>> PublicErrorsAsync(
        IReadOnlyList<EffectiveRule> rules,
        CancellationToken cancellationToken)
    {
        var ids = rules.SelectMany(rule => rule.ErrorIds).Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var errors = await database.Errors.AsNoTracking()
            .Where(error => ids.Contains(error.Id) && error.IsPublic && error.RetiredAt == null)
            .ToListAsync(cancellationToken);

        return
        [
            .. errors
                .OrderBy(error => error.Category)
                .ThenBy(error => error.Id)
                .Select(error => new PublicErrorDto(error.Id, error.Name, error.Description, error.Category, error.YearlyMax)),
        ];
    }

    private async Task<IReadOnlyDictionary<long, Localized<string>>> GroupNamesAsync(
        IReadOnlyList<long> groupIds,
        CancellationToken cancellationToken)
    {
        var wanted = groupIds.Distinct().ToArray();

        if (wanted.Length == 0)
        {
            return new Dictionary<long, Localized<string>>();
        }

        var groups = await database.AircraftGroups.AsNoTracking()
            .Where(group => wanted.Contains(group.Id))
            .ToListAsync(cancellationToken);

        return groups.ToDictionary(group => group.Id, group => group.Name);
    }

    private static PublicAircraftDto Aircraft(AllowedAircraft allowed, IReadOnlyDictionary<long, Localized<string>> groups) =>
        new(
            allowed.Types,
            [
                .. allowed.GroupIds
                    .Where(groups.ContainsKey)
                    .Select(id => new PublicAircraftGroupDto(id, groups[id])),
            ]);
}
