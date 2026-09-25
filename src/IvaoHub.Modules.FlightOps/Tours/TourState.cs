using System.Linq.Expressions;
using IvaoHub.Core.Division;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>The state of a tour as it is seen (design M2 §1.2.1). Never stored: computed from the dates.</summary>
public enum TourStateKind
{
    Template,
    Draft,
    Upcoming,
    Open,

    /// <summary>Looks closed, but still accepts reports of flights that took off by the close.</summary>
    Closing,
    Closed,
}

/// <summary>
/// The one place the state of a tour is read off its dates (design M2 §1.2.1): the list, the page, the form,
/// the save and the projections all ask here. No job publishes or closes a tour.
/// <para>What SQL has to ask — the tours that still need a daily limit of their own — is an expression written
/// here too, next to the rule it translates.</para>
/// </summary>
public static class TourState
{
    /// <summary>The refusal of every write to an archived tour and its rows (design M2 §10, note 2026-09-25-la-conservazione-dei-tour).</summary>
    public const string PurgedKey = "flightops:errors.tourPurged";

    public static TourStateKind Of(Tour tour, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(tour);

        if (tour.IsTemplate)
        {
            return TourStateKind.Template;
        }

        if (tour.Status != PublishStatus.Published || tour.ReleaseAt is not { } release || tour.CloseAt is not { } close)
        {
            return TourStateKind.Draft;
        }

        if (now < release)
        {
            return TourStateKind.Upcoming;
        }

        if (now <= close)
        {
            return TourStateKind.Open;
        }

        return now <= close.AddDays(tour.ReportWindowDays) ? TourStateKind.Closing : TourStateKind.Closed;
    }

    /// <summary>
    /// Whether anybody outside the staff sees the tour now: ready, not hidden, not archived, and released or shown as a
    /// preview. From this moment its kind no longer changes (Carmine, 15 September 2026).
    /// </summary>
    public static bool IsPublic(Tour tour, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(tour);

        return !tour.IsHidden
            && tour.PurgedAt is null
            && Of(tour, now) is not (TourStateKind.Template or TourStateKind.Draft)
            && (tour.ShowPreview || tour.ReleaseAt <= now);
    }

    /// <summary>
    /// The tours that forbid switching the division's daily limit off (design M2 §3.7): not a template, without a
    /// limit of their own, and ready and not yet past their report window, or a draft whose dates are still ahead.
    /// </summary>
    public static Expression<Func<Tour, bool>> NeedsOwnDailyLimit(DateTime now) =>
        tour => !tour.IsTemplate
            && tour.DailyLegLimit == null
            && (tour.Status == PublishStatus.Published
                ? tour.CloseAt != null && tour.CloseAt.Value.AddDays(tour.ReportWindowDays) >= now
                : tour.CloseAt == null || tour.CloseAt >= now);
}
