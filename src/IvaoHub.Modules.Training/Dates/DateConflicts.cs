using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Settings;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// The warnings of a date (design M3 §2.5), as queries and pure functions, so that the database and a test ask the same thing:
/// on the days the date touches in the division's time zone (<see cref="DivisionDays.Touched"/>), the other trainings with their
/// session then, whoever trains them; the entries of the calendar of the kinds the division checks (<c>conflictKinds</c>) that
/// touch those days — read from the core's calendar as whoever writes the date reads it, the trainings' own sessions left out
/// because the first already counts them —; which of those entries a date may keep; and what the division's policy makes of
/// what was found.
/// </summary>
public static class DateConflicts
{
    /// <summary>The policy refuses a date with a warning (<see cref="ConflictPolicy.Block"/>).</summary>
    public const string Blocked = "training:errors.dateBlocked";

    /// <summary>The policy wants a date with a warning confirmed (<see cref="ConflictPolicy.Warn"/>), and it was not.</summary>
    public const string NotConfirmed = "training:errors.dateNotConfirmed";

    /// <summary>What a page for the staff may carry, as the core's ceiling says it: the entries a date may keep.</summary>
    private static readonly IReadOnlyList<Visibility> KeptVisibilities = VisibilityCeiling.For(Visibility.Staff);

    /// <summary>The other trainings with their session on those days: dated, starting between the two moments, not this one.</summary>
    public static IQueryable<Training> Sessions(IQueryable<Training> trainings, long except, DateTime from, DateTime to)
    {
        ArgumentNullException.ThrowIfNull(trainings);

        return trainings.Where(training => training.Id != except
            && training.State == TrainingState.Scheduled
            && training.ScheduledStartUtc >= from
            && training.ScheduledStartUtc < to);
    }

    /// <summary>
    /// The entries of the calendar of these kinds that touch those days: one that starts before the last moment and ends after the
    /// first — or, with no end, starts between the two. The sessions of the trainings are left out, whatever the kinds say.
    /// </summary>
    public static IQueryable<CalendarEntry> Entries(IQueryable<CalendarEntry> entries, IReadOnlyCollection<string> kinds, DateTime from, DateTime to)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(kinds);

        return entries.Where(entry => kinds.Contains(entry.Kind)
            && !(entry.SourceModule == TrainingModule.ModuleKey && entry.SourceId.StartsWith(Training.SourcePrefix))
            && entry.StartsAtUtc < to
            && (entry.EndsAtUtc == null ? entry.StartsAtUtc >= from : entry.EndsAtUtc > from));
    }

    /// <summary>
    /// The entries a date may keep among its warnings (§2.5). A date proposed keeps them (<see cref="TrainingSlot"/>), and whoever
    /// reads the training reads them, not only whoever proposed it — who may read more of the calendar: the direction reads the
    /// entries of every department that only that department reads. So a date keeps what a page for the staff may carry, the
    /// core's ceiling (<see cref="VisibilityCeiling"/>): the entries of everybody, of the members and of the staff, never those of
    /// one department, which stay with whoever was shown them. The other trainings need no ceiling: a warning of theirs says what
    /// the public calendar says of their session.
    /// </summary>
    public static IEnumerable<CalendarEntry> Kept(IEnumerable<CalendarEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Where(entry => KeptVisibilities.Contains(entry.Visibility));
    }

    /// <summary>What was found, as the trainer is warned of it: by time, a session before an entry at the same moment.</summary>
    public static IReadOnlyList<DateWarning> Warnings(IEnumerable<Training> sessions, IEnumerable<CalendarEntry> entries, RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(vocabulary);

        return
        [
            .. sessions
                .Where(training => training.ScheduledStartUtc is not null)
                .Select(training => new DateWarning(
                    DateWarningKind.Training,
                    training.ScheduledStartUtc!.Value,
                    EndsAtUtc: null,
                    training.Id,
                    training.Kind,
                    vocabulary.Find(training.Kind, training.Rating)?.ShortName,
                    training.Position,
                    CalendarKind: null,
                    Title: null,
                    Url: null))
                .Concat(entries.Select(entry => new DateWarning(
                    DateWarningKind.Calendar,
                    entry.StartsAtUtc,
                    entry.EndsAtUtc,
                    TrainingId: null,
                    TrainingKind: null,
                    RatingShortName: null,
                    Position: null,
                    entry.Kind,
                    entry.Title,
                    string.IsNullOrWhiteSpace(entry.Url) ? null : entry.Url)))
                .OrderBy(warning => warning.StartsAtUtc)
                .ThenBy(warning => warning.Kind),
        ];
    }

    /// <summary>
    /// What the policy makes of a date with this many warnings: nothing to say — none found, or none looked for —; refused, when the
    /// policy blocks; to be confirmed, when it warns and whoever wrote the date did not confirm it.
    /// </summary>
    public static string? Refusal(ConflictPolicy policy, int warnings, bool confirmed) => policy switch
    {
        ConflictPolicy.Block when warnings > 0 => Blocked,
        ConflictPolicy.Warn when warnings > 0 && !confirmed => NotConfirmed,
        _ => null,
    };
}
