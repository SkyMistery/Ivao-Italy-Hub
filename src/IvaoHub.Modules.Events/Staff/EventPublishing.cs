using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// What an event needs to be published (design M4 §2.2, E3b), field by field with the core's <see cref="Refusals"/>: its title
/// and summary in every language of the division; its dates in order — seen, bookings open, starts, ends —, with the bookings
/// opening on an event with slots; an airport for an event with slots; the page of whoever organises an event of others.
/// <para>Asked by «Publish», and by every write of an event already published, so that it stays what it was published as — as a
/// ready tour stays ready: the release of a published event lets out the row as it is, because it already passed these checks
/// (note 2026-09-29-la-vita-di-un-evento §2), and nothing refuses it at that hour.</para>
/// </summary>
public sealed class EventPublishing(EventsDbContext database, IOptions<DivisionOptions> division)
{
    /// <summary>The field the airports of an event are refused under: they are rows of their own, the tab of the page.</summary>
    public const string AirportsField = "airports";

    public const string SlotsNeedAirportsKey = "events:errors.slotsNeedAirports";

    public async Task<Refusals> ProblemsAsync(Event row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var problems = new Refusals();
        var locales = division.Value.Locales;

        // Read by everybody the event is for, in their language.
        if (!row.Title.HasAll(locales))
        {
            problems.Missing("title", row.Title.MissingLocales(locales));
        }

        if (!row.Summary.HasAll(locales))
        {
            problems.Missing("summary", row.Summary.MissingLocales(locales));
        }

        // Seen ≤ bookings open ≤ starts < ends: an empty «seen from» is the publication, and the bookings open on an event with
        // slots, or nobody ever books one.
        if (row.VisibleFromUtc is { } seen && seen > row.StartsAtUtc)
        {
            problems.Add("visibleFromUtc", "events:errors.seenAfterItStarts");
        }

        if (row.BookingOpensAtUtc is { } opens)
        {
            if (row.VisibleFromUtc is { } seenFrom && opens < seenFrom)
            {
                problems.Add("bookingOpensAtUtc", "events:errors.bookingBeforeItIsSeen");
            }

            if (opens > row.StartsAtUtc)
            {
                problems.Add("bookingOpensAtUtc", "events:errors.bookingAfterItStarts");
            }
        }
        else if (HasSlots(row))
        {
            problems.Add("bookingOpensAtUtc", "events:errors.bookingOpensRequired");
        }

        if (row.EndsAtUtc <= row.StartsAtUtc)
        {
            problems.Add("endsAtUtc", "events:errors.endsBeforeItStarts");
        }

        if (await LacksAirportsAsync(row, leaving: null, cancellationToken))
        {
            problems.Add(AirportsField, SlotsNeedAirportsKey);
        }

        // An event of the network or of another division sends to their page.
        if (row.Organizer != EventOrganizer.Division && string.IsNullOrWhiteSpace(row.ExternalUrl))
        {
            problems.Add("externalUrl", "events:errors.externalUrlRequired");
        }

        return problems;
    }

    /// <summary>
    /// Whether an event with slots has no airport left for them — without <paramref name="leaving"/>, the airport about to be
    /// deleted, when there is one. A slot is always at an airport of its event (§1.5).
    /// </summary>
    public async Task<bool> LacksAirportsAsync(Event row, long? leaving, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!HasSlots(row))
        {
            return false;
        }

        var airports = database.Airports.Where(airport => airport.EventId == row.Id);
        if (leaving is { } id)
        {
            airports = airports.Where(airport => airport.Id != id);
        }

        return !await airports.AnyAsync(cancellationToken);
    }

    private static bool HasSlots(Event row) => row.PublicSlots || row.PrivateSlots;
}
