using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training;

/// <summary>
/// The people of the trainings as the hub knows them (plan §16.13: whoever signed in): their names by VID, read from the core's
/// members, and how a page or a mail writes one of them. A name is never copied onto a training (design M3 §6).
/// </summary>
public sealed class TrainingPeople(HubDbContext hub)
{
    /// <summary>The names the hub has for these people, by VID; whoever it does not know is left out.</summary>
    public async Task<IReadOnlyDictionary<int, string>> NamesAsync(IEnumerable<int?> vids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vids);

        var wanted = vids.OfType<int>().Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return (await hub.Users.AsNoTracking()
                .Where(user => wanted.Contains(user.Vid))
                .Select(user => new { user.Vid, user.FirstName, user.LastName })
                .ToListAsync(cancellationToken))
            .ToDictionary(user => user.Vid, user => $"{user.FirstName} {user.LastName}".Trim());
    }

    /// <summary>A person as a page names them: the VID that always is, and the name the hub has.</summary>
    public static TrainingMemberDto? Member(int? vid, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return vid is { } known ? new TrainingMemberDto(known, names.GetValueOrDefault(known)) : null;
    }

    /// <summary>A person in a mail: their name and VID, or the VID alone when the hub has no name.</summary>
    public static string Label(int vid, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return names.GetValueOrDefault(vid) is { Length: > 0 } name
            ? string.Create(CultureInfo.InvariantCulture, $"{name} ({vid})")
            : vid.ToString(CultureInfo.InvariantCulture);
    }
}
