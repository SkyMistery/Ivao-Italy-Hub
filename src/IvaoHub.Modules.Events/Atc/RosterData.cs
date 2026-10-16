using IvaoHub.Core.Atc;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registers;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>A controller's availability as the roster reads it: their windows, in their order, and when they gave it.</summary>
public sealed record RosterAvailability(int Vid, IReadOnlyList<AtcWindowDto> Windows, DateTime Since);

/// <summary>
/// What the roster of an event is made of (design M4 §4.3, E11b), read once for the proposal (<see cref="RosterProposal"/>) and for the
/// staff's roster with its warnings (<see cref="StaffRoster"/>), so the two read the same rules off the same data: the positions with their
/// kind, their minimum and their preferred rating from the core; the availabilities with their windows; the ATC ratings the hub has; the
/// penalties from the shifts of the other events; and the experience from the archive of sessions, when the division has one. One query
/// per kind of row, never one per row — but the archive, which answers one controller at a time (E10b).
/// </summary>
public sealed class RosterData(
    EventsDbContext database,
    EventsPeople people,
    IAtcPositionDirectory directory,
    IAtcActivitySource archive,
    RatingVocabulary vocabulary)
{
    /// <summary>The numbers of the roster of an event: its length of a shift, or the setting's, and the settings' others.</summary>
    public static RosterRules Rules(Event row, EventsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(settings);

        return new RosterRules(row.ShiftMinutes ?? settings.ShiftMinutes, settings.MinimumAtcRating, settings.MaxConsecutiveShifts, settings.BreakShifts);
    }

    /// <summary>The positions as the proposer reads them: the kind the directory gives each callsign, its minimum, its preferred rating.</summary>
    public async Task<IReadOnlyList<RosterPosition>> PositionsAsync(IReadOnlyList<EventAtcPosition> positions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var callsigns = positions.Select(position => position.Callsign).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var known = await directory.FindAsync(callsigns, cancellationToken);
        var minima = await directory.MinimaAsync(callsigns, cancellationToken);

        return
        [
            .. positions.Select(position =>
            {
                var type = known.GetValueOrDefault(position.Callsign)?.Type;
                return new RosterPosition(
                    position.Id,
                    position.Callsign,
                    type,
                    position.FromUtc,
                    position.ToUtc,
                    minima.GetValueOrDefault(position.Callsign) ?? AtcPositionMinimum.None,
                    vocabulary.PreferredFor(type));
            }),
        ];
    }

    /// <summary>The availabilities of an event with their windows, read past the members' filter, the first given first.</summary>
    public async Task<IReadOnlyList<RosterAvailability>> AvailabilitiesAsync(long eventId, CancellationToken cancellationToken)
    {
        var availabilities = await CrudSource.BackOffice<EventAtcAvailability>(database).AsNoTracking()
            .Where(availability => availability.EventId == eventId)
            .OrderBy(availability => availability.CreatedAt)
            .ThenBy(availability => availability.Id)
            .ToListAsync(cancellationToken);
        var windows = (await CrudSource.BackOffice<EventAtcWindow>(database).AsNoTracking()
                .Where(window => window.EventId == eventId)
                .ToListAsync(cancellationToken))
            .ToLookup(window => window.AvailabilityId);

        return
        [
            .. availabilities.Select(availability => new RosterAvailability(
                availability.ControllerVid,
                [.. windows[availability.Id].OrderBy(window => window.FromUtc).Select(window => new AtcWindowDto(window.FromUtc, window.ToUtc))],
                availability.CreatedAt)),
        ];
    }

    /// <summary>The ATC ratings the hub has for these people, by VID: what the last sign in brought.</summary>
    public Task<IReadOnlyDictionary<int, int>> RatingsAsync(IEnumerable<int> vids, CancellationToken cancellationToken) =>
        people.AtcRatingsAsync(vids, cancellationToken);

    /// <summary>
    /// The penalty of each of these people (§4.5), from their shifts of the other events, weighed by <c>noShowWeight</c> (E13b): attended and
    /// confirmed no-shows count; excused, proposed and unknown ones do not (note 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 3).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, double>> PenaltiesAsync(
        IReadOnlyCollection<int> vids,
        long eventId,
        double weight,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vids);

        var counted = await ControllerRegister.CountsAsync(database, vids, exceptEventId: eventId, cancellationToken);

        return vids.Distinct().ToDictionary(
            vid => vid,
            vid => counted.TryGetValue(vid, out var counts) ? RosterProposer.Penalty(counts.NoShows, counts.Attended, weight) : 0);
    }

    /// <summary>
    /// The experience of these people (§4.3): the connections each opened in the last <paramref name="months"/>, by callsign and by the
    /// kind the core's directory gives the callsign. <b>Null when the archive did not answer for one of them</b> — no archive, or one that
    /// cannot be read —: then the criterion is left out for everybody (reading 3 of the note).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, RosterExperience>?> ExperienceAsync(
        IReadOnlyCollection<int> vids,
        int months,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vids);

        var sessions = new Dictionary<int, IReadOnlyList<AtcPresence>>();
        foreach (var vid in vids.Distinct())
        {
            if (await archive.SessionsOfAsync(vid, now.AddMonths(-months), now, cancellationToken) is not { } activity)
            {
                return null;
            }

            sessions[vid] = activity.Online;
        }

        var types = await directory.FindAsync(
            [.. sessions.Values.SelectMany(own => own.Select(presence => presence.Callsign)).Distinct(StringComparer.OrdinalIgnoreCase)],
            cancellationToken);

        return sessions.ToDictionary(
            entry => entry.Key,
            entry => new RosterExperience(
                entry.Value.GroupBy(presence => presence.Callsign, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
                entry.Value.Select(presence => types.GetValueOrDefault(presence.Callsign)?.Type)
                    .OfType<string>()
                    .GroupBy(type => type, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase)));
    }
}
