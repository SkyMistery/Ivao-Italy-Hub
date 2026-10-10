using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// A private arrival and its linked departure (design M4 §1.6, §3.4, E7): booked together by one pilot, so that the gate manager gives
/// them one gate — the arrival names the departure (<see cref="EventBooking.PairedBookingId"/>), and the two are read as a pair while
/// both are there.
/// <para><b>When one of the two goes</b> — withdrawn by its pilot, taken away by the staff — <b>the link dissolves and the other
/// stays</b>, an ordinary private booking: the recommendation of the note 2026-10-10-il-ritiro-di-un-volo-collegato, asked of the
/// maintainer with issue #245. Each booking is already whole on its own — its slot, its compatibility, its off block —, and the arrival
/// closes before its departure does: a rule that took both away could not hold after the arrival's off block. A pair is born together
/// and never made again later.</para>
/// </summary>
internal static class BookingPairs
{
    /// <summary>
    /// The pairs among these bookings, each way: a booking's id to the other's, for an arrival and its departure that are both there.
    /// </summary>
    public static IReadOnlyDictionary<long, long> Of(IEnumerable<EventBooking> bookings)
    {
        ArgumentNullException.ThrowIfNull(bookings);

        var present = bookings.ToList();
        var ids = present.Select(booking => booking.Id).ToHashSet();
        var pairs = new Dictionary<long, long>();

        foreach (var arrival in present)
        {
            if (arrival.PairedBookingId is { } departure && ids.Contains(departure))
            {
                pairs[arrival.Id] = departure;
                pairs[departure] = arrival.Id;
            }
        }

        return pairs;
    }

    /// <summary>
    /// What a booking about to be deleted leaves of its pair: the arrival that names it — when it is a linked departure — names it no
    /// more, in the same save as the delete. An arrival going needs nothing: the link is its own column, and goes with it.
    /// </summary>
    public static async Task LetGoAsync(EventsDbContext database, EventBooking leaving, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(leaving);

        var arrivals = await CrudSource.BackOffice<EventBooking>(database)
            .Where(booking => booking.PairedBookingId == leaving.Id)
            .ToListAsync(cancellationToken);

        foreach (var arrival in arrivals)
        {
            arrival.PairedBookingId = null;
        }
    }
}
