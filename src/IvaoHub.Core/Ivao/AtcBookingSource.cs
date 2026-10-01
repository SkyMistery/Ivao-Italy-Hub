using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// "Who has booked a position of the network in this window?" — the question a module asks of the network's ATC bookings
/// (M4, E15a; design M4 §9.1, §17.2 n.3): the roster of an event reads them beside its shifts, to see who has also booked on
/// the network and which position somebody else has taken. Read when asked, never kept and never sampled (note
/// 2026-09-28-i-job-quando-passenger-spegne-l-hub §8). How the network lists its bookings — by day of UTC, one across midnight
/// on both days, a position matched by the start of its callsign — is this folder's to know, not the module's.
/// </summary>
public interface IAtcBookingSource
{
    /// <summary>
    /// The widest window the source asks for, in days of UTC: one call each. An event lasts hours, and a whole day of the
    /// division touches two days of UTC; a window of more than a week is a mistake upstream, not seven calls to make.
    /// </summary>
    const int MaxDays = 7;

    /// <summary>
    /// The bookings that overlap the window from <paramref name="fromUtc"/> to <paramref name="toUtc"/> — of every position, or
    /// of the one position <paramref name="callsign"/> names, in any case —, in the order they start; none for an empty window.
    /// Both instants are UTC, whatever their <see cref="DateTime.Kind"/> says.
    /// <para><see langword="null"/> when the network could not be asked, which a page shows as «not available» and never as
    /// «nobody booked»; and for a window of more than <see cref="MaxDays"/> days, which the source does not ask.</para>
    /// </summary>
    Task<IReadOnlyList<AtcBookingDto>?> BookedAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? callsign = null,
        CancellationToken cancellationToken = default);
}

/// <summary>What a booking is for, as the network says it.</summary>
public enum AtcBookingKind
{
    /// <summary>An ordinary booking: somebody controlling the position.</summary>
    Controlling,

    /// <summary>A training session on the position.</summary>
    Training,

    /// <summary>An exam on the position.</summary>
    Exam,
}

/// <summary>
/// One booking of a position on the network, as little of it as a page needs. Who booked it is a VID and nothing else: a page
/// names a member of the hub the way it names anybody, and somebody who never signed in by their number.
/// </summary>
/// <param name="Callsign">Upper case, for example <c>LIRF_TWR</c> or <c>LIRR_NE_CTR</c>.</param>
/// <param name="StartsAt">UTC.</param>
/// <param name="EndsAt">UTC.</param>
/// <param name="Vid">Who booked it.</param>
/// <param name="Kind">An ordinary booking, a training or an exam.</param>
public sealed record AtcBookingDto(string Callsign, DateTime StartsAt, DateTime EndsAt, int Vid, AtcBookingKind Kind);

internal sealed class AtcBookingSource(IIvaoApiClient ivao, ILogger<AtcBookingSource> logger) : IAtcBookingSource
{
    public async Task<IReadOnlyList<AtcBookingDto>?> BookedAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? callsign = null,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            return [];
        }

        // IVAO lists its bookings by day of UTC, and a booking that overlaps the window touches one of the days from the
        // window's first instant to its last.
        var first = DateOnly.FromDateTime(fromUtc);
        var last = DateOnly.FromDateTime(toUtc.AddTicks(-1));
        var days = last.DayNumber - first.DayNumber + 1;
        if (days > IAtcBookingSource.MaxDays)
        {
            logger.LogWarning(
                "A window of {Days} days was asked of the network's bookings; the source asks at most {MaxDays}.",
                days,
                IAtcBookingSource.MaxDays);
            return null;
        }

        var wanted = string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim().ToUpperInvariant();
        var bookings = new List<AtcBookingDto>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            // A day that could not be read is the whole answer: half a list would let the staff conclude that a booking is
            // not there.
            if (await ivao.GetDailyAtcBookingsAsync(day, wanted, cancellationToken) is not { } listed)
            {
                return null;
            }

            bookings.AddRange(listed);
        }

        // One across midnight comes once from each of its days. IVAO matches a position by the start of the callsign, so the
        // one asked for is kept only where it is the whole callsign.
        return [.. bookings
            .Where(booking => booking.StartsAt < toUtc && booking.EndsAt > fromUtc)
            .Where(booking => wanted is null || booking.Callsign == wanted)
            .Distinct()
            .OrderBy(booking => booking.StartsAt)
            .ThenBy(booking => booking.Callsign, StringComparer.Ordinal)
            .ThenBy(booking => booking.EndsAt)
            .ThenBy(booking => booking.Vid)];
    }
}
