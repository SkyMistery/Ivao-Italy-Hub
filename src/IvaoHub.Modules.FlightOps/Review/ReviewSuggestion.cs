using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;

namespace IvaoHub.Modules.FlightOps.Review;

/// <summary>
/// What the system proposes for a report (design M2 §4.3), a pure function: a <c>Dangerous</c> error marked rejects from its
/// first occurrence; a <c>Warning</c> that takes the pilot's calendar year over its <c>yearly_max</c> rejects; anything else is
/// accepted. The validator decides — against it only with a reason (<c>threshold_overridden</c>).
/// <para>The count of the year is the error confirmed on the pilot's <b>decided</b> reports — accepted and rejected, on any
/// tour, by UTC year of the take-off — plus this one (Carmine, 23 September 2026, note 2026-09-23-la-validazione §2.5).</para>
/// </summary>
public static class ReviewSuggestion
{
    public const string Dangerous = "dangerous";

    public const string OverYearlyMax = "overYearlyMax";

    /// <param name="marked">The errors being marked on this report, as the rules it froze describe them.</param>
    /// <param name="countsInYear">Per error, how many times the pilot already has it in the year of this flight, this report excluded.</param>
    public static SuggestionDto Of(IEnumerable<SnapshotErrorDto> marked, IReadOnlyDictionary<long, int> countsInYear)
    {
        ArgumentNullException.ThrowIfNull(marked);
        ArgumentNullException.ThrowIfNull(countsInYear);

        var reasons = new List<SuggestionReasonDto>();
        foreach (var error in marked.DistinctBy(error => error.Id))
        {
            if (error.Category == ErrorCategory.Dangerous)
            {
                reasons.Add(new SuggestionReasonDto(error.Id, Dangerous, null, null));
            }
            else if (error.Category == ErrorCategory.Warning
                && error.YearlyMax is { } max
                && countsInYear.GetValueOrDefault(error.Id) + 1 > max)
            {
                reasons.Add(new SuggestionReasonDto(error.Id, OverYearlyMax, countsInYear.GetValueOrDefault(error.Id) + 1, max));
            }
        }

        return new SuggestionDto(reasons.Count > 0 ? PirepStatus.Rejected : PirepStatus.Accepted, reasons);
    }

    /// <summary>
    /// Whether a decision goes against the suggestion: an acceptance where a rejection was proposed, or the other way round.
    /// «To modify» is neither, and is never compared.
    /// </summary>
    public static bool Overrides(PirepStatus outcome, SuggestionDto suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        return outcome is PirepStatus.Accepted or PirepStatus.Rejected && outcome != suggestion.Outcome;
    }
}
