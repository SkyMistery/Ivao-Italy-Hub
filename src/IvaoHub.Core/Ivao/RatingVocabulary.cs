namespace IvaoHub.Core.Ivao;

/// <summary>The two ladders a member climbs on IVAO, which keeps them apart: <c>hub_users.rating_atc</c> and <c>rating_pilot</c>.</summary>
public enum RatingKind
{
    Atc,
    Pilot,
}

/// <summary>
/// One rating of a ladder, as the hub knows it (M3, A1, decision note of 25 September 2026).
/// </summary>
/// <param name="Kind">Which ladder.</param>
/// <param name="Number">IVAO's own number, the one a profile carries and <c>hub_users</c> keeps.</param>
/// <param name="ShortName">What the staff says out loud — ADC, PP — and what <c>RatingBadge</c> draws.</param>
/// <param name="HasPracticalTraining">Whether a division trains a member for it in practice, with a trainer.</param>
/// <param name="PositionType">
/// The kind of ATC position it is trained on, as IVAO spells it in <c>position</c> (<c>TWR</c>, <c>APP</c>, <c>CTR</c>);
/// null for a rating nobody trains for, and for every pilot's.
/// </param>
public sealed record Rating(RatingKind Kind, int Number, string ShortName, bool HasPracticalTraining, string? PositionType)
{
    /// <summary>The key of its name in the language files of the core: <c>ratings.Atc.ADC</c>.</summary>
    public string NameKey => $"ratings.{Kind}.{ShortName}";
}

/// <summary>
/// The ratings and the rules that go with them, as questions a module asks instead of writing numbers of its own: which
/// rating comes next and whether it is trained, whether one rating is at least another, and — through
/// <see cref="Rating.PositionType"/> — on which positions a rating is trained (design M3 §1.7, §8 n.4).
/// <para>Built from data: the core registers IVAO's (<see cref="IvaoRatings"/>), and a module's tests build one of their
/// own with the same class, so the module is proved on ratings that are not IVAO's (design M3 §10). The order of a ladder
/// is the order of the list it is given, lowest first — never the numbers, which merely happen to agree today.</para>
/// </summary>
public sealed class RatingVocabulary
{
    private readonly Dictionary<RatingKind, IReadOnlyList<Rating>> _ladders;

    public RatingVocabulary(IEnumerable<Rating> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        _ladders = ratings
            .GroupBy(rating => rating.Kind)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Rating>)[.. group]);

        foreach (var (kind, ladder) in _ladders)
        {
            // A number or a name written twice would make "which one is it?" a question with two answers.
            if (ladder.DistinctBy(rating => rating.Number).Count() != ladder.Count
                || ladder.DistinctBy(rating => rating.ShortName, StringComparer.OrdinalIgnoreCase).Count() != ladder.Count)
            {
                throw new ArgumentException($"A rating of the {kind} ladder is written twice.", nameof(ratings));
            }
        }
    }

    /// <summary>The ratings of one ladder, lowest first.</summary>
    public IReadOnlyList<Rating> Ladder(RatingKind kind) => _ladders.GetValueOrDefault(kind) ?? [];

    /// <summary>The rating a number stands for; null for no number, and for one this vocabulary does not know.</summary>
    public Rating? Find(RatingKind kind, int? number) =>
        number is null ? null : Ladder(kind).FirstOrDefault(rating => rating.Number == number);

    /// <summary>
    /// The rating a member with <paramref name="current"/> is trained for next: the one just above it, if it has a practical
    /// training, and otherwise nothing — an AS3 is offered ADC, an ACC nothing at all (design M3 §2.2). Null as well for a
    /// rating this vocabulary does not know: a member is never offered a training on a guess.
    /// </summary>
    public Rating? NextTraining(RatingKind kind, int? current)
    {
        var ladder = Ladder(kind);
        var index = IndexOf(ladder, current);
        if (index < 0 || index + 1 >= ladder.Count)
        {
            return null;
        }

        var next = ladder[index + 1];
        return next.HasPracticalTraining ? next : null;
    }

    /// <summary>
    /// Whether <paramref name="number"/> stands at least as high as <paramref name="required"/> on the ladder: whether a
    /// trainer may train for it (design M3 §2.4). A rating this vocabulary does not know is at least nothing.
    /// </summary>
    public bool IsAtLeast(RatingKind kind, int? number, int required)
    {
        var ladder = Ladder(kind);
        var index = IndexOf(ladder, number);
        var least = IndexOf(ladder, required);

        return index >= 0 && least >= 0 && index >= least;
    }

    private static int IndexOf(IReadOnlyList<Rating> ladder, int? number)
    {
        for (var index = 0; index < ladder.Count; index++)
        {
            if (ladder[index].Number == number)
            {
                return index;
            }
        }

        return -1;
    }
}

/// <summary>
/// IVAO's ratings. The numbers, the short names and the order are the ones the API spells, read on 25 September 2026 from
/// the ratings of the members connected (<c>/v2/tracker/now/atc</c>, <c>/now/pilots</c> and recent sessions): IVAO has no
/// endpoint that lists them. The ratings a division trains in practice — ADC, APC, ACC; PP, SPP, CP — and the positions it
/// trains them on are how IVAO's training works today, confirmed by the staff of a training department; no position IVAO
/// publishes carries a rating (<c>/v2/ATCPositions/all</c> and <c>/v2/subcenters/all</c>, the same day).
/// <para>This is IVAO knowledge, so it lives in the IVAO perimeter of the core (plan §4.2) and a module never repeats it:
/// the names are keys of the core's language files (<see cref="Rating.NameKey"/>), IVAO's own English names.</para>
/// </summary>
public static class IvaoRatings
{
    public static RatingVocabulary Vocabulary { get; } = new(
    [
        new(RatingKind.Atc, 2, "AS1", false, null),
        new(RatingKind.Atc, 3, "AS2", false, null),
        new(RatingKind.Atc, 4, "AS3", false, null),
        new(RatingKind.Atc, 5, "ADC", true, "TWR"),
        new(RatingKind.Atc, 6, "APC", true, "APP"),
        new(RatingKind.Atc, 7, "ACC", true, "CTR"),
        new(RatingKind.Atc, 8, "SEC", false, null),
        new(RatingKind.Atc, 9, "SAI", false, null),
        new(RatingKind.Atc, 10, "CAI", false, null),

        new(RatingKind.Pilot, 2, "FS1", false, null),
        new(RatingKind.Pilot, 3, "FS2", false, null),
        new(RatingKind.Pilot, 4, "FS3", false, null),
        new(RatingKind.Pilot, 5, "PP", true, null),
        new(RatingKind.Pilot, 6, "SPP", true, null),
        new(RatingKind.Pilot, 7, "CP", true, null),
        new(RatingKind.Pilot, 8, "ATP", false, null),
        new(RatingKind.Pilot, 9, "SFI", false, null),
        new(RatingKind.Pilot, 10, "CFI", false, null),
    ]);
}
