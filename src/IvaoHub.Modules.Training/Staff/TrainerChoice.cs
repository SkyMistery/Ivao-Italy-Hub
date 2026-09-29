using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// A member as the choice of a trainer reads them: whether they are of the staff of the training — a position of the direction or
/// of the module's department, as the hub last saw it at their sign in —, and their ratings as the network gave them then.
/// </summary>
public sealed record TrainerFacts(int Vid, bool IsTrainingStaff, int? RatingAtc, int? RatingPilot)
{
    /// <summary>Somebody the hub does not know: never signed in, so on no roster (plan §16.13).</summary>
    public static TrainerFacts Unknown(int vid) => new(vid, IsTrainingStaff: false, RatingAtc: null, RatingPilot: null);

    /// <summary>Their rating on one ladder; none when the network had said nothing.</summary>
    public int? RatingOf(RatingKind kind) => kind == RatingKind.Pilot ? RatingPilot : RatingAtc;
}

/// <summary>
/// Who may train a training (design M3 §2.4, R.1, d2, d4), the rule the list of candidates and the assignment both ask: never the
/// trainee; somebody of the staff of the training, for either ladder — who trains what the staff knows, and the hub does not
/// record —; with a rating on the training's ladder at least the one trained, in the order of the core's vocabulary, so that who
/// stands above every trained rating always may, with no rule of this module saying so.
/// </summary>
public static class TrainerChoice
{
    /// <summary>The trainee of the training: nobody trains their own.</summary>
    public const string IsTrainee = "training:errors.trainerIsTrainee";

    /// <summary>Not of the staff of the training, or unknown to the hub.</summary>
    public const string NotStaff = "training:errors.trainerNotStaff";

    /// <summary>A rating below the one trained, or none the hub knows.</summary>
    public const string RatingTooLow = "training:errors.trainerRatingTooLow";

    /// <summary>Why <paramref name="candidate"/> may not train this training, the first reason first; null when they may.</summary>
    public static string? Refusal(Training training, TrainerFacts candidate, RatingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(training);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(vocabulary);

        if (candidate.Vid == training.TraineeVid)
        {
            return IsTrainee;
        }

        if (!candidate.IsTrainingStaff)
        {
            return NotStaff;
        }

        return vocabulary.IsAtLeast(training.Kind, candidate.RatingOf(training.Kind), training.Rating) ? null : RatingTooLow;
    }
}
