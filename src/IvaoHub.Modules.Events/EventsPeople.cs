using IvaoHub.Core.Data;
using IvaoHub.Core.People;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events;

/// <summary>A person as the staff's pages of the events name them: the VID that always is, and the name the hub has — none when it has none.</summary>
public sealed record EventMemberDto(int Vid, string? Name);

/// <summary>
/// The people of the events as the hub knows them (plan §16.13: whoever signed in): their names by VID, read by the core's one reader
/// of them, as the training's pages read theirs. A name is never copied onto a row of the module (design M4 §1.13); a person
/// whose data was erased is a negative VID with no name, and the page says so with the core's word (<c>personName</c>).
/// </summary>
public sealed class EventsPeople(HubDbContext hub)
{
    /// <summary>
    /// The names the hub has for these people, by VID; whoever it does not know is left out, and so is a VID below one (the core's
    /// <see cref="MemberNames"/>, note <c>2026-10-11-un-solo-lettore-dei-nomi</c>).
    /// </summary>
    public Task<IReadOnlyDictionary<int, string>> NamesAsync(IEnumerable<int> vids, CancellationToken cancellationToken) =>
        MemberNames.ReadAsync(hub, vids, cancellationToken);

    /// <summary>
    /// The ATC ratings the hub has for these people, by VID — the network's numbers, as the last sign in brought them (E11a): what the
    /// staff of the ATC read next to a controller's availability. Whoever has none, or is not known, is left out.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, int>> AtcRatingsAsync(IEnumerable<int> vids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vids);

        var wanted = vids.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return (await hub.Users.AsNoTracking()
                .Where(user => wanted.Contains(user.Vid) && user.RatingAtc != null)
                .Select(user => new { user.Vid, user.RatingAtc })
                .ToListAsync(cancellationToken))
            .ToDictionary(user => user.Vid, user => user.RatingAtc!.Value);
    }

    /// <summary>The ATC rating the hub has for one person — the signed in member's, for their own availability (E11a).</summary>
    public Task<int?> AtcRatingAsync(int vid, CancellationToken cancellationToken) =>
        hub.Users.AsNoTracking().Where(user => user.Vid == vid).Select(user => user.RatingAtc).FirstOrDefaultAsync(cancellationToken);

    /// <summary>A person as a page names them: the VID, and the name the hub has.</summary>
    public static EventMemberDto Member(int vid, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new EventMemberDto(vid, names.GetValueOrDefault(vid));
    }
}
