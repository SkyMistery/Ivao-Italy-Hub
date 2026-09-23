namespace IvaoHub.Core.Atc;

/// <summary>
/// "Which positions were online in this interval?" — the one question the hub asks of an archive of ATC sessions (design M2
/// §6.5). The tours module asks it to propose the controllers a pilot contacted (§3.3) and to tell whether an exemption's
/// position was online; nothing it answers decides anything by itself.
/// <para>An archive is an <b>optional</b> integration of the core (note 2026-09-14-dati-condivisi-con-vipi §3.4): a division
/// that has none gets <see langword="null"/>, which a caller shows as «not available» and never as «failed». Which archive
/// it is lives in this folder and nowhere else; an architecture test holds that line.</para>
/// </summary>
public interface IAtcActivitySource
{
    /// <summary>
    /// The positions online at some moment between <paramref name="fromUtc"/> and <paramref name="toUtc"/>, or
    /// <see langword="null"/> when there is no archive or it could not be read.
    /// </summary>
    Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}

/// <summary>One connection of a controller, as the archive keeps it.</summary>
/// <param name="Callsign">Upper case, for example <c>LIRF_TWR</c> or <c>LIRR_N_CTR</c>.</param>
/// <param name="Frequency">In MHz as the network writes it, when the archive knows it.</param>
/// <param name="StartedAt">When the connection opened, UTC.</param>
/// <param name="EndedAt">Null while the connection is still open.</param>
public sealed record AtcPresence(string Callsign, string? Frequency, DateTime StartedAt, DateTime? EndedAt)
{
    /// <summary>
    /// The place a callsign names: what comes before the first underscore — an airport (<c>LIRF</c>) or a region
    /// (<c>LIRR</c>).
    /// </summary>
    public string Station => StationOf(Callsign);

    /// <summary>Whether the connection was open at some moment between the two instants.</summary>
    public bool Overlaps(DateTime fromUtc, DateTime toUtc) => StartedAt <= toUtc && (EndedAt is null || EndedAt >= fromUtc);

    public static string StationOf(string callsign)
    {
        ArgumentNullException.ThrowIfNull(callsign);

        var cut = callsign.IndexOf('_', StringComparison.Ordinal);
        return (cut < 0 ? callsign : callsign[..cut]).Trim().ToUpperInvariant();
    }
}

/// <summary>
/// What the archive had for an interval: the connections, and how far back it is complete — for the division's own
/// positions and for the rest of the world, which an archive may have started keeping later, or keep for less time.
/// <para>That is what separates «the position was not online» from «we cannot know»: a position the archive does not list
/// is offline only where the archive covers the whole interval.</para>
/// </summary>
public sealed record AtcActivity(
    IReadOnlyList<AtcPresence> Online,
    DateTime? DivisionSince,
    DateTime? WorldSince,
    IReadOnlyList<string> DivisionPrefixes)
{
    /// <summary>The connections of one callsign, in the order they started.</summary>
    public IEnumerable<AtcPresence> Of(string callsign) =>
        Online.Where(presence => string.Equals(presence.Callsign, callsign, StringComparison.OrdinalIgnoreCase))
            .OrderBy(presence => presence.StartedAt);

    /// <summary>Whether a position the archive does not list, from <paramref name="fromUtc"/> on, was really offline.</summary>
    public bool Covers(string callsign, DateTime fromUtc)
    {
        ArgumentNullException.ThrowIfNull(callsign);

        var station = AtcPresence.StationOf(callsign);
        var since = DivisionPrefixes.Any(prefix => station.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ? DivisionSince
            : WorldSince;

        return since is { } start && fromUtc >= start;
    }
}

/// <summary>A division with no archive: every question is answered «not available».</summary>
public sealed class UnavailableAtcActivitySource : IAtcActivitySource
{
    public Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<AtcActivity?>(null);
}
