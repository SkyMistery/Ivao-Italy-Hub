using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Settings;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>A window of hours, from when to when, as the payloads and the pages of the ATC carry them.</summary>
public sealed record AtcWindowDto(DateTime FromUtc, DateTime ToUtc);

/// <summary>
/// When an event takes availabilities, and what a window is (design M4 §4.1, §4.2, E11a; note 2026-10-12-le-postazioni-e-la-disponibilita):
/// one place for the staff's positions, the member's verbs and the page of the event.
/// </summary>
public static class AtcRules
{
    /// <summary>An event that takes no availability, or no longer does: no roster, cancelled, closed, or no position yet.</summary>
    public const string ClosedKey = "events:errors.availabilityClosed";

    /// <summary>A member whose ATC rating is below the setting's least.</summary>
    public const string RatingTooLowKey = "events:errors.ratingTooLow";

    /// <summary>A window that is not inside the event's.</summary>
    public const string OutsideEventKey = "events:errors.windowOutsideEvent";

    /// <summary>A window that ends before it starts, or when it starts.</summary>
    public const string EndsBeforeItStartsKey = "events:errors.windowEndsBeforeItStarts";

    /// <summary>
    /// When the availabilities of an event close (§4.2): <c>applicationsCloseDays</c> before its start — the moment the roster is
    /// proposed (§4.3). The length of a shift does not move it.
    /// </summary>
    public static DateTime ClosesAt(Event row, EventsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(settings);

        return row.StartsAtUtc.AddDays(-settings.ApplicationsCloseDays);
    }

    /// <summary>
    /// Whether a member may give, change or withdraw their availability now (§4.2): an event with a roster, not cancelled, before the
    /// close, with at least one position — there is nothing to be available for without one. Seen by them is the endpoint's to ask;
    /// their rating is <see cref="IsRatedEnough"/>.
    /// </summary>
    public static bool IsOpen(Event row, EventsSettings settings, bool hasPositions, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.HasRoster && row.CancelledAt is null && hasPositions && now < ClosesAt(row, settings);
    }

    /// <summary>
    /// Whether a member's ATC rating — the network's number, as the hub keeps it — is at least the setting's least (§4.2): the core's vocabulary
    /// answers, so the module names no rating. No least, any member; a rating the vocabulary does not know is at least nothing.
    /// </summary>
    public static bool IsRatedEnough(RatingVocabulary vocabulary, EventsSettings settings, int? ratingAtc)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(settings);

        return settings.MinimumAtcRating is not { } least || vocabulary.IsAtLeast(RatingKind.Atc, ratingAtc, least);
    }

    /// <summary>Whether a member may withdraw now: until the close, cancelled or not — after it the roster was proposed from it.</summary>
    public static bool IsWithdrawable(Event row, EventsSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return now < ClosesAt(row, settings);
    }

    /// <summary>
    /// The windows a member sends, in their order of time (§4.2): at least one and at most <see cref="EventAtcAvailability.MaxWindows"/>,
    /// each ending after it starts, inside the event's window, none meeting another. A refusal lands on the field of its window,
    /// <c>windows[2].fromUtc</c>; the windows come back sorted when there is none.
    /// </summary>
    public static IReadOnlyList<AtcWindowDto> Check(Event row, IReadOnlyList<AtcWindowDto>? windows, Refusals problems)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(problems);

        if (windows is null || windows.Count == 0)
        {
            problems.Add("windows", "errors.required");
            return [];
        }

        if (windows.Count > EventAtcAvailability.MaxWindows)
        {
            problems.Add("windows", "events:errors.tooManyWindows");
            return [];
        }

        for (var index = 0; index < windows.Count; index++)
        {
            var window = windows[index];

            if (window.ToUtc <= window.FromUtc)
            {
                problems.Add($"windows[{index}].toUtc", EndsBeforeItStartsKey);
            }
            else if (!IsInside(row, window.FromUtc, window.ToUtc))
            {
                problems.Add($"windows[{index}].fromUtc", OutsideEventKey);
            }
            else if (windows.Where((other, at) => at != index && other.FromUtc < window.ToUtc && window.FromUtc < other.ToUtc).Any())
            {
                problems.Add($"windows[{index}].fromUtc", "events:errors.windowsOverlap");
            }
        }

        return problems.IsEmpty ? [.. windows.OrderBy(window => window.FromUtc)] : [];
    }

    /// <summary>Whether a stretch of hours is inside the event's window, its two ends included.</summary>
    public static bool IsInside(Event row, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(row);

        return fromUtc >= row.StartsAtUtc && toUtc <= row.EndsAtUtc;
    }

    /// <summary>Whether two stretches of hours meet: one starts before the other ends. Two that only touch do not.</summary>
    public static bool Meet(DateTime fromUtc, DateTime toUtc, DateTime otherFromUtc, DateTime otherToUtc) =>
        fromUtc < otherToUtc && otherFromUtc < toUtc;

    /// <summary>
    /// Whether some windows cover a stretch of hours from end to end (§4.3: «available for the whole shift»), joined where they touch.
    /// </summary>
    public static bool Cover(IEnumerable<AtcWindowDto> windows, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var reached = fromUtc;
        foreach (var window in windows.OrderBy(window => window.FromUtc))
        {
            if (window.FromUtc > reached)
            {
                break;
            }

            if (window.ToUtc > reached)
            {
                reached = window.ToUtc;
            }

            if (reached >= toUtc)
            {
                return true;
            }
        }

        return reached >= toUtc;
    }
}
