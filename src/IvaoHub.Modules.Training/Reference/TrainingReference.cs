using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Ivao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Modules.Training.Reference;

/// <summary>A rating the division trains for, as a form offers it: its ladder, the number the hub keeps, and its words.</summary>
/// <param name="Kind">The ladder, ATC or pilot.</param>
/// <param name="Number">The number a profile carries and <c>hub_users</c> keeps; a form sends it back, and never shows it.</param>
/// <param name="ShortName">What the staff says out loud, and what <c>RatingBadge</c> draws.</param>
/// <param name="NameKey">The key of its name in the language files of the core.</param>
public sealed record TrainingRatingDto(RatingKind Kind, int Number, string ShortName, string NameKey);

/// <summary>A position of the division a training may take place on, with the rating it is trained for.</summary>
public sealed record TrainingPositionDto(string Callsign, string Name, string RatingShortName);

/// <summary>
/// What the division trains, asked of the core rather than written here (design M3 §1.7, note
/// 2026-09-25-rating-e-postazioni-dal-nucleo): the ratings the vocabulary gives a practical training, and the positions of the
/// division the directory says each of them is trained on. The settings are chosen from them and checked against them (A4).
/// </summary>
public sealed class TrainingReference(RatingVocabulary vocabulary, IAtcPositionDirectory directory)
{
    /// <summary>Every rating with a practical training, ladder by ladder, lowest first.</summary>
    public IReadOnlyList<Rating> Ratings { get; } =
        [.. Enum.GetValues<RatingKind>().SelectMany(vocabulary.Ladder).Where(rating => rating.HasPracticalTraining)];

    /// <summary>
    /// The positions those ratings are trained on, each once, by rating and then by callsign. A rating trained on no
    /// position — a pilot's — has none, and the directory says so without asking the database.
    /// </summary>
    public async Task<IReadOnlyList<TrainingPositionDto>> PositionsAsync(CancellationToken cancellationToken = default)
    {
        var positions = new List<TrainingPositionDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rating in Ratings)
        {
            foreach (var position in await directory.ForRatingAsync(rating, cancellationToken))
            {
                if (seen.Add(position.Callsign))
                {
                    positions.Add(new TrainingPositionDto(position.Callsign, position.Name, rating.ShortName));
                }
            }
        }

        return positions;
    }
}

/// <summary>
/// What the screens of the training choose from: the ratings any signed in member may read, which is public knowledge and
/// what every later form of the module offers (A5, A6), and the positions, which today only the settings offer.
/// </summary>
public static class ReferenceEndpoints
{
    public const string RatingsPattern = "/api/training/ratings";

    public const string PositionsPattern = "/api/training/positions";

    public static IEndpointRouteBuilder MapReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(RatingsPattern, (TrainingReference reference) =>
                Results.Ok(reference.Ratings
                    .Select(rating => new TrainingRatingDto(rating.Kind, rating.Number, rating.ShortName, rating.NameKey))
                    .ToList()))
            .WithTags("Training")
            .WithName("TrainingRatings")
            .RequireAuthorization(HubPolicies.SignedIn)
            .Produces<List<TrainingRatingDto>>();

        app.MapGet(PositionsPattern, async (TrainingReference reference, HttpContext http) =>
                Results.Ok(await reference.PositionsAsync(http.RequestAborted)))
            .WithTags("Training")
            .WithName("TrainingPositions")
            .RequireAuthorization(TrainingPermissions.ManageSettings)
            .Produces<List<TrainingPositionDto>>();

        return app;
    }
}
