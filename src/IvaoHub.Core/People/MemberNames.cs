using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.People;

/// <summary>
/// The names the hub has for people, by VID: the one reader of them (note 2026-10-11-un-solo-lettore-dei-nomi). The staff
/// is whoever has signed in at least once (plan §16.13), so a name is read from the core's members, never copied onto a
/// row of a module; a page then names a person with the core's <c>personName</c>, which asks <c>isErased</c> first.
/// <para>What it answers is what the contact threads, the tours, the training and the events each answered on their own
/// before it (E10m): first and last name joined by a space and trimmed, kept even when that leaves nothing — whether an
/// empty name reads as none is the page's to say —; whoever the hub does not know left out; and no name for a VID that
/// is not a member's, zero or negative, which is what somebody whose data was erased is (a pseudonym, tied to nobody).</para>
/// </summary>
public static class MemberNames
{
    /// <summary>The names the hub has for these people, by VID; whoever it does not know is left out.</summary>
    public static async Task<IReadOnlyDictionary<int, string>> ReadAsync(
        HubDbContext hub,
        IEnumerable<int> vids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(vids);

        // Never the pseudonym of somebody whose data was erased: their user went with the erasure, and a number below one
        // is nobody's.
        var wanted = vids.Where(vid => vid > 0).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        // Joined and trimmed here, not in the query: the database's TRIM takes only spaces, and a name from the network
        // may end in another blank.
        return (await hub.Users.AsNoTracking()
                .Where(user => wanted.Contains(user.Vid))
                .Select(user => new { user.Vid, user.FirstName, user.LastName })
                .ToListAsync(cancellationToken))
            .ToDictionary(user => user.Vid, user => $"{user.FirstName} {user.LastName}".Trim());
    }

    /// <summary>The same, for the columns that may name nobody: a missing VID is left out like an unknown one.</summary>
    public static Task<IReadOnlyDictionary<int, string>> ReadAsync(
        HubDbContext hub,
        IEnumerable<int?> vids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vids);

        return ReadAsync(hub, vids.OfType<int>(), cancellationToken);
    }
}
