using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Settings;

namespace IvaoHub.Modules.Training.Requests;

/// <summary>What the rules of a request read about a trainee on one ladder, gathered by whoever asks them.</summary>
/// <param name="Kind">The ladder.</param>
/// <param name="Rating">The trainee's rating on it, by the number the hub keeps; none when the network has said nothing.</param>
/// <param name="Hours">The trainee's hours on it; none when the network has said nothing, which is not zero.</param>
/// <param name="History">Every training of the trainee, on both ladders.</param>
/// <param name="Bans">Every ban of the trainee, lifted and over ones included.</param>
/// <param name="PositionsOffered">How many positions the rating proposed is offered on, the hidden ones left out.</param>
public sealed record PathFacts(
    RatingKind Kind,
    int? Rating,
    decimal? Hours,
    IReadOnlyList<Training> History,
    IReadOnlyList<TraineeBan> Bans,
    int PositionsOffered);

/// <summary>
/// Where a trainee stands on one ladder (design M3 §2.2): the one training the hub proposes, whether it is a mock exam, and the
/// first rule that refuses a request — with what it needs to say so, since a refusal travels as a bare key.
/// </summary>
/// <param name="Kind">The ladder.</param>
/// <param name="Next">The rating proposed; none when nothing comes after the trainee's with a practical training.</param>
/// <param name="IsMockExam">Whether the training proposed would be a mock exam (§2.8).</param>
/// <param name="Refusal">The i18n key of the first rule that refuses, in the design's order; none when the request may be made.</param>
/// <param name="BannedUntil">With a ban: the end of the last one holding; none when one holds until somebody lifts it.</param>
/// <param name="OpenTrainingId">With an open training on the ladder: which one.</param>
/// <param name="WaitUntil">The end of the waiting on the ladder, while it runs.</param>
/// <param name="MinimumHours">The hours the rating proposed needs, when the division set a threshold for it.</param>
public sealed record PathStanding(
    RatingKind Kind,
    Rating? Next,
    bool IsMockExam,
    string? Refusal,
    DateTime? BannedUntil,
    long? OpenTrainingId,
    DateTime? WaitUntil,
    int? MinimumHours);

/// <summary>
/// The rules of a request (design M3 §2.2, §2.8), in pure functions: whoever asks them gathers the trainee's history, bans,
/// rating and hours, and the settings, and they answer — the page, to say beforehand what the trainee may ask for, and the
/// request itself, which asks them again. The vocabulary of the core says which rating comes next (§1.7): no number of a rating
/// is written here, and the tests hold these rules on a vocabulary that is not the network's (§10).
/// <para>The order is the design's — the ban, one open training per ladder, the waiting, the hours —, with the two a request
/// cannot do without in their places: something to ask for, before its hours; a position to train on, last. The mock exam
/// refuses nothing, and the theory comes after all of them, in the request, because a «no» is recorded.</para>
/// </summary>
public static class RequestRules
{
    /// <summary>A ban holds: nothing is asked for, on either ladder (§2.9).</summary>
    public const string Banned = "training:errors.requestBanned";

    /// <summary>A training of the trainee's is still open on the ladder.</summary>
    public const string Open = "training:errors.requestOpen";

    /// <summary>The waiting after the last training on the ladder has not run out.</summary>
    public const string Waiting = "training:errors.requestWaiting";

    /// <summary>Nothing after the trainee's rating has a practical training — or the hub does not know the rating.</summary>
    public const string NothingToAsk = "training:errors.requestNothingToAsk";

    /// <summary>The rating proposed has a threshold of hours, and the network has not told the hub the trainee's.</summary>
    public const string HoursUnknown = "training:errors.requestHoursUnknown";

    /// <summary>The trainee's hours are below the threshold of the rating proposed.</summary>
    public const string HoursTooFew = "training:errors.requestHoursTooFew";

    /// <summary>The rating proposed is trained on a position, and the division offers none.</summary>
    public const string NoPosition = "training:errors.requestNoPosition";

    /// <summary>Where the trainee stands on one ladder at that moment.</summary>
    public static PathStanding Standing(PathFacts facts, TrainingSettings settings, RatingVocabulary vocabulary, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(vocabulary);

        var next = vocabulary.NextTraining(facts.Kind, facts.Rating);
        var bans = facts.Bans.Where(ban => ban.Holds(now)).ToList();
        var open = facts.History
            .Where(training => training.Kind == facts.Kind && Training.IsOpen(training.State))
            .OrderByDescending(training => training.Id)
            .FirstOrDefault();
        var waitUntil = WaitUntil(facts.History, facts.Kind, settings) is { } end && end > now ? end : (DateTime?)null;
        var minimum = next is null ? null : MinimumHours(settings, next);

        string? refusal = null;
        if (bans.Count > 0)
        {
            refusal = Banned;
        }
        else if (open is not null)
        {
            refusal = Open;
        }
        else if (waitUntil is not null)
        {
            refusal = Waiting;
        }
        else if (next is null)
        {
            refusal = NothingToAsk;
        }
        else if (minimum is not null && facts.Hours is null)
        {
            refusal = HoursUnknown;
        }
        else if (facts.Hours < minimum)
        {
            refusal = HoursTooFew;
        }
        else if (next.PositionType is not null && facts.PositionsOffered == 0)
        {
            refusal = NoPosition;
        }

        return new PathStanding(
            facts.Kind,
            next,
            next is not null && IsMockExam(facts.History, next),
            refusal,
            bans.Count == 0 || bans.Any(ban => ban.EndsAt is null) ? null : bans.Max(ban => ban.EndsAt),
            open?.Id,
            waitUntil,
            minimum);
    }

    /// <summary>
    /// The end of the waiting on a ladder (§2.2 point 3), from the last training closed on it: <c>cooldownDays</c> after a
    /// completed one whose trainer did not waive it, <c>noShowCooldownDays</c> after a no-show, and nothing after a refusal, a
    /// cancellation or a closure. None when the ladder has no closed training.
    /// </summary>
    public static DateTime? WaitUntil(IEnumerable<Training> history, RatingKind kind, TrainingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(settings);

        var last = history
            .Where(training => training.Kind == kind && EndedAt(training) is not null)
            .OrderByDescending(EndedAt)
            .ThenByDescending(training => training.Id)
            .FirstOrDefault();

        return last?.State switch
        {
            TrainingState.Completed when !last.CooldownWaived => EndedAt(last)!.Value.AddDays(settings.CooldownDays),
            TrainingState.NoShow => EndedAt(last)!.Value.AddDays(settings.NoShowCooldownDays),
            _ => null,
        };
    }

    /// <summary>
    /// Whether the next training on that rating is a mock exam (§2.8): the trainee's last completed training on the same ladder
    /// and rating had «ready for the mock exam» and was not a mock exam already. Decided by the hub, never by the trainee, and it
    /// follows the trainee whoever their trainer is.
    /// </summary>
    public static bool IsMockExam(IEnumerable<Training> history, Rating rating)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(rating);

        var last = history
            .Where(training => training.Kind == rating.Kind && training.Rating == rating.Number && training.State == TrainingState.Completed)
            .OrderByDescending(EndedAt)
            .ThenByDescending(training => training.Id)
            .FirstOrDefault();

        return last is { ReadyForMockExam: true, IsMockExam: false };
    }

    /// <summary>The hours the division asks on the ladder for that rating (§1.6); none without a threshold.</summary>
    public static int? MinimumHours(TrainingSettings settings, Rating rating)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(rating);

        return settings.MinimumHours
            .FirstOrDefault(threshold => threshold.Kind == rating.Kind && threshold.Rating == rating.Number)?.Hours;
    }

    /// <summary>
    /// When a training ended: the report of a completed one, the decision of a refused one, the closure of the others. None for
    /// one still open. A moment the row does not carry, which only a row written by hand would lack, is its last change.
    /// </summary>
    public static DateTime? EndedAt(Training training)
    {
        ArgumentNullException.ThrowIfNull(training);

        return training.State switch
        {
            TrainingState.Completed => training.CompletedAt ?? training.UpdatedAt,
            TrainingState.Rejected => training.DecidedAt ?? training.UpdatedAt,
            TrainingState.Cancelled or TrainingState.Closed or TrainingState.NoShow => training.ClosedAt ?? training.UpdatedAt,
            _ => null,
        };
    }
}
