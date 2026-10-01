using IvaoHub.Core.Division;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// Refuses to start with a <c>division.json → preferredAtcRatings</c> that names a kind of position or a rating IVAO does not
/// have (M4, E10c; the maintainer's answer on #204). It lives in the IVAO perimeter because both lists are IVAO's
/// (<see cref="IvaoAtcPosition.Kinds"/>, <see cref="IvaoRatings"/>); the rest of the file is <see cref="DivisionOptionsValidator"/>'s.
/// Every message names the file and the key, so that a division which forks can fix it alone.
/// </summary>
public sealed class PreferredAtcRatingsValidator : IValidateOptions<DivisionOptions>
{
    public ValidateOptionsResult Validate(string? name, DivisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        var ratings = IvaoRatings.Vocabulary.Ladder(RatingKind.Atc).Select(rating => rating.ShortName).ToArray();

        foreach (var (kind, rating) in options.PreferredAtcRatings)
        {
            // A kind written wrong would put nobody first on the positions it meant, and nothing would say so.
            if (!IvaoAtcPosition.Kinds.Contains(kind.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"division.json: 'preferredAtcRatings' has an entry for '{kind}', which is not a kind of ATC position IVAO "
                    + $"lists. Use one of: {string.Join(", ", IvaoAtcPosition.Kinds)}.");
            }

            if (IvaoRatings.Vocabulary.Named(RatingKind.Atc, rating) is null)
            {
                failures.Add(
                    $"division.json: 'preferredAtcRatings.{kind}' ({rating}) is not an ATC rating. Use one of: "
                    + $"{string.Join(", ", ratings)}, or leave the kind out to put nobody first on it.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
