using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// The availabilities of an event as the staff of the ATC read them (design M4 §4.2, §6.3, E11a): <b>per position</b> — the controllers
/// with a window that meets its hours —, because an availability says no FIR and the staff of a FIR read only their positions (E2's form,
/// point 7). One query for the windows, one for the names, one for the ratings, never one per row.
/// </summary>
public sealed class StaffAvailability(
    EventsDbContext database,
    EventsPeople people,
    IAtcPositionDirectory directory,
    RatingVocabulary vocabulary)
{
    /// <summary>A page of positions with the names the core knows and how many controllers are available in some of their hours.</summary>
    public async Task<IReadOnlyList<EventAtcPositionDto>> PositionsAsync(
        IReadOnlyList<EventAtcPosition> positions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var named = await directory.FindAsync([.. positions.Select(position => position.Callsign).Distinct()], cancellationToken);
        var windows = await WindowsAsync(positions.Select(position => position.EventId), cancellationToken);

        return
        [
            .. positions.Select(position => AtcPositionMapper.Row(
                position,
                named.GetValueOrDefault(position.Callsign)?.Name,
                windows
                    .Where(window => window.EventId == position.EventId && AtcRules.Meet(window.FromUtc, window.ToUtc, position.FromUtc, position.ToUtc))
                    .Select(window => window.AvailabilityId)
                    .Distinct()
                    .Count())),
        ];
    }

    /// <summary>
    /// The controllers with a window that meets the hours of <paramref name="position"/>, the first given first: named, with their rating,
    /// their windows in the event — all of them, so the staff see the evening whole — and whether they cover the position end to end.
    /// </summary>
    public async Task<IReadOnlyList<AtcCandidateDto>> CandidatesAsync(EventAtcPosition position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);

        var windows = (await WindowsAsync([position.EventId], cancellationToken)).ToLookup(window => window.AvailabilityId);
        var meeting = windows
            .Where(own => own.Any(window => AtcRules.Meet(window.FromUtc, window.ToUtc, position.FromUtc, position.ToUtc)))
            .Select(own => own.Key)
            .ToList();

        var availabilities = await CrudSource.BackOffice<EventAtcAvailability>(database).AsNoTracking()
            .Where(availability => meeting.Contains(availability.Id))
            .OrderBy(availability => availability.CreatedAt)
            .ThenBy(availability => availability.Id)
            .ToListAsync(cancellationToken);

        var vids = availabilities.Select(availability => availability.ControllerVid).ToList();
        var names = await people.NamesAsync(vids, cancellationToken);
        var ratings = await people.AtcRatingsAsync(vids, cancellationToken);

        return
        [
            .. availabilities.Select(availability =>
            {
                var own = windows[availability.Id].OrderBy(window => window.FromUtc).Select(window => new AtcWindowDto(window.FromUtc, window.ToUtc)).ToList();

                return new AtcCandidateDto(
                    availability.Id,
                    EventsPeople.Member(availability.ControllerVid, names),
                    ratings.TryGetValue(availability.ControllerVid, out var rating) ? vocabulary.Find(RatingKind.Atc, rating)?.ShortName : null,
                    AtcRules.Cover(own, position.FromUtc, position.ToUtc),
                    [.. own.Select(window => AtcPositionEndpoints.Written(window.FromUtc, window.ToUtc))],
                    availability.Note,
                    availability.CreatedAt);
            }),
        ];
    }

    /// <summary>The windows of these events, read past the members' filter: the staff read every controller's.</summary>
    private Task<List<EventAtcWindow>> WindowsAsync(IEnumerable<long> eventIds, CancellationToken cancellationToken)
    {
        var ids = eventIds.Distinct().ToList();

        return CrudSource.BackOffice<EventAtcWindow>(database).AsNoTracking()
            .Where(window => ids.Contains(window.EventId))
            .ToListAsync(cancellationToken);
    }
}
