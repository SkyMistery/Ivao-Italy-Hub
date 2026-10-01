using System.Linq.Expressions;
using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>The state of an event as it is seen (design M4 §2.1). Never stored: read off its dates, its status and its cancellation.</summary>
public enum EventStateKind
{
    /// <summary>Not published: the staff's only.</summary>
    Draft,

    /// <summary>Published, and not seen yet: before <see cref="Event.VisibleFromUtc"/> (§2.2).</summary>
    Scheduled,

    /// <summary>Seen, before the bookings open.</summary>
    Announced,

    /// <summary>From the opening of the bookings to the start; each slot closes at its own off block time (§3.3).</summary>
    BookingOpen,

    /// <summary>Between its start and its end, and still booked slot by slot.</summary>
    InProgress,

    /// <summary>After its end: the staff's, and each member's own rows (§2.4).</summary>
    Ended,

    /// <summary>Cancelled, whatever its dates say: seen with its note until its end (§2.3).</summary>
    Cancelled,
}

/// <summary>
/// The one place the state of an event is read off its dates (design M4 §2.1, note 2026-09-29-la-vita-di-un-evento), as
/// <c>TourState</c> is for a tour: the list, the page, the verbs and the projections all ask here, and no job changes a state.
/// <para>Every instant is the first moment of what it opens: an event seen from 18:00 is seen at 18:00, one that ends at 22:00
/// has ended at 22:00. A cancelled event is cancelled before anything else, a draft is a draft whatever its dates.</para>
/// </summary>
public static class EventState
{
    public static EventStateKind Of(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.CancelledAt is not null)
        {
            return EventStateKind.Cancelled;
        }

        if (row.Status != PublishStatus.Published)
        {
            return EventStateKind.Draft;
        }

        if (now >= row.EndsAtUtc)
        {
            return EventStateKind.Ended;
        }

        if (now >= row.StartsAtUtc)
        {
            return EventStateKind.InProgress;
        }

        if (row.VisibleFromUtc is { } visible && now < visible)
        {
            return EventStateKind.Scheduled;
        }

        return row.BookingOpensAtUtc is { } opens && now >= opens ? EventStateKind.BookingOpen : EventStateKind.Announced;
    }
}

/// <summary>
/// The views of the staff's list of events (design M4 §7.2), as <c>filter[view]</c> names them: the drafts, the upcoming ones —
/// published and not started, seen or not —, the ones in progress, the ended ones and the cancelled ones. Left out, the list holds
/// every event. Each view is the states <see cref="EventState.Of"/> gives, written once more in what SQL can ask, next to it; a
/// test holds the two to the same answer.
/// </summary>
public static class EventViews
{
    /// <summary>The name of the filter, in <c>filter[view]</c>.</summary>
    public const string Filter = "view";

    public const string Drafts = "drafts";

    public const string Upcoming = "upcoming";

    public const string InProgress = "inProgress";

    public const string Ended = "ended";

    public const string Cancelled = "cancelled";

    /// <summary>Every view, in the order the list offers them.</summary>
    public static readonly IReadOnlyList<string> All = [Drafts, Upcoming, InProgress, Ended, Cancelled];

    /// <summary>The view a state is listed under.</summary>
    public static string Of(EventStateKind state) => state switch
    {
        EventStateKind.Draft => Drafts,
        EventStateKind.Scheduled or EventStateKind.Announced or EventStateKind.BookingOpen => Upcoming,
        EventStateKind.InProgress => InProgress,
        EventStateKind.Ended => Ended,
        EventStateKind.Cancelled => Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "An event has no such state."),
    };

    /// <summary>The events of one view at <paramref name="now"/>, as SQL asks it; null for a view that does not exist, which the list answers with 400.</summary>
    public static Expression<Func<Event, bool>>? Where(string view, DateTime now) => view switch
    {
        Drafts => row => row.CancelledAt == null && row.Status != PublishStatus.Published,
        Upcoming => row => row.CancelledAt == null && row.Status == PublishStatus.Published && now < row.StartsAtUtc,
        InProgress => row => row.CancelledAt == null && row.Status == PublishStatus.Published
            && row.StartsAtUtc <= now && now < row.EndsAtUtc,
        Ended => row => row.CancelledAt == null && row.Status == PublishStatus.Published && row.EndsAtUtc <= now,
        Cancelled => row => row.CancelledAt != null,
        _ => null,
    };
}
