using IvaoHub.Core.Content;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Settings;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>
/// The warnings of a date (design M3 §2.5), as queries and pure functions, so that the database and a test ask the same thing:
/// on the days the date touches in the division's time zone (<see cref="DivisionDays.Touched"/>), the other trainings with their
/// session then, whoever trains them; the entries of the calendar of the kinds the division checks (<c>conflictKinds</c>) that
/// touch those days — read from the core's calendar as it is, the trainings' own sessions left out because the first already
/// counts them —; and what the division's policy makes of what was found.
/// </summary>
public static class DateConflicts
{
    /// <summary>The policy refuses a date with a warning (<see cref="ConflictPolicy.Block"/>).</summary>
    public const string Blocked = "training:errors.dateBlocked";

    /// <summary>The policy wants a date with a warning confirmed (<see cref="ConflictPolicy.Warn"/>), and it was not.</summary>
    public const string NotConfirmed = "training:errors.dateNotConfirmed";

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
