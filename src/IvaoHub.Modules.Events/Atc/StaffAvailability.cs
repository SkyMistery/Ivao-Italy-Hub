using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    RatingVocabulary vocabulary,
    IAuthorizationService authorization,
    IHttpContextAccessor http)
{
    /// <summary>
    /// A page of positions with the names the core knows, how many controllers are available in some of their hours, and whether the reader
    /// may write the ATC on each — the one handler asked on the position, its care, scope and FIR (E11b: the shift's form offers those).
    /// </summary>
    public async Task<IReadOnlyList<EventAtcPositionDto>> PositionsAsync(
        IReadOnlyList<EventAtcPosition> positions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var named = await directory.FindAsync([.. positions.Select(position => position.Callsign).Distinct()], cancellationToken);
        var windows = await WindowsAsync(positions.Select(position => position.EventId), cancellationToken);
        var reader = http.HttpContext?.User;

        var rows = new List<EventAtcPositionDto>();
        foreach (var position in positions)
        {
            rows.Add(AtcPositionMapper.Row(
                position,
                named.GetValueOrDefault(position.Callsign)?.Name,
                windows
                    .Where(window => window.EventId == position.EventId && AtcRules.Meet(window.FromUtc, window.ToUtc, position.FromUtc, position.ToUtc))
                    .Select(window => window.AvailabilityId)
                    .Distinct()
                    .Count(),
                reader is not null && (await authorization.AuthorizeAsync(reader, position, EventsPermissions.AtcEdit)).Succeeded));
        }

        return rows;
    }

    /// <summary>
    /// The controllers with a window that meets the hours of <paramref name="position"/>, the first given first: named, with their rating,
    /// from when to when they are available <b>inside the position</b> — the first of their windows there clipped to its opening, the last
    /// clipped to its closing, as instants the list draws (Carmine's answer (B) on #256) —, how many of their windows meet it, and whether
    /// they cover it end to end; a gap between two windows shows only as «not the whole time».
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
                var inside = windows[availability.Id]
                    .Where(window => AtcRules.Meet(window.FromUtc, window.ToUtc, position.FromUtc, position.ToUtc))
                    .Select(window => new AtcWindowDto(window.FromUtc, window.ToUtc))
                    .ToList();

                return new AtcCandidateDto(
                    availability.Id,
                    EventsPeople.Member(availability.ControllerVid, names),
                    ratings.TryGetValue(availability.ControllerVid, out var rating) ? vocabulary.Find(RatingKind.Atc, rating)?.ShortName : null,
                    AtcRules.Cover(inside, position.FromUtc, position.ToUtc),
                    Max(inside.Min(window => window.FromUtc), position.FromUtc),
                    Min(inside.Max(window => window.ToUtc), position.ToUtc),
                    inside.Count,
                    availability.Note,
                    availability.CreatedAt);
            }),
        ];
    }

    private static DateTime Max(DateTime one, DateTime other) => one > other ? one : other;

    private static DateTime Min(DateTime one, DateTime other) => one < other ? one : other;

    /// <summary>The windows of these events, read past the members' filter: the staff read every controller's.</summary>
    private Task<List<EventAtcWindow>> WindowsAsync(IEnumerable<long> eventIds, CancellationToken cancellationToken)
    {
        var ids = eventIds.Distinct().ToList();

        return CrudSource.BackOffice<EventAtcWindow>(database).AsNoTracking()
            .Where(window => ids.Contains(window.EventId))
            .ToListAsync(cancellationToken);
    }
}
