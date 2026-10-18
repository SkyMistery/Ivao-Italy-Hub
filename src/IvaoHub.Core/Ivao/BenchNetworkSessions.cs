using System.Collections.Concurrent;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Which people of the end to end bench were given sessions on the network, and from when (note
/// 2026-10-18-il-banco-fa-girare-un-job). A person of the bench has no recorded session of their own, so the checks after an
/// event, a report of support and a rule of award used to find nothing of them. <c>PUT /e2e/sessions/{vid}</c> gives one the
/// sessions of <see cref="FixtureIvaoApiClient.BenchTemplate"/>, moved so that the first of them starts at the moment asked, and
/// <see cref="FixtureIvaoApiClient"/> answers them for that VID besides its files.
/// <para>Registered only on the bench, beside its sign in: anywhere else the fixture client has none and reads its files as it
/// always did. Kept in the process and never written to a file: a bench that stops forgets them, and no spec leaves one
/// behind for the next.</para>
/// </summary>
public sealed class BenchNetworkSessions
{
    private readonly ConcurrentDictionary<int, DateTime> _given = new();

    /// <summary>Gives the person the template's sessions, the first starting at <paramref name="atUtc"/>; a second call moves them.</summary>
    public void Give(int vid, DateTime atUtc) =>
        _given[vid] = atUtc.Kind == DateTimeKind.Local ? atUtc.ToUniversalTime() : DateTime.SpecifyKind(atUtc, DateTimeKind.Utc);

    /// <summary>Takes the person's sessions back; false when they had none.</summary>
    public bool Take(int vid) => _given.TryRemove(vid, out _);

    /// <summary>When the person's first session starts, UTC; null when they were given none.</summary>
    public DateTime? Of(int vid) => _given.TryGetValue(vid, out var at) ? at : null;
}
