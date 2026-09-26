using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Training;

/// <summary>
/// The mails of the training (design M3 §5.2), one member at a time and in their language: what the training is — the ladder,
/// the rating and the position —, the page it is read on, and what the sentence of the type needs besides. Through the one
/// notification service, which drops whoever switched the type off or has no address.
/// <para>One place for what every mail of the module says of a training, so the request received (A6a) and the staff's
/// decisions (A7) say it alike.</para>
/// </summary>
public sealed class TrainingMail(
    HubDbContext hub,
    RatingVocabulary vocabulary,
    INotificationService notifications,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division)
{
    /// <summary>The trainee's page of their trainings (A6b), where every mail to a trainee points.</summary>
    public const string MinePath = "/training/mine";

    /// <summary>The staff's page of one training (A7), where a mail to the trainer points.</summary>
    public static string StaffPath(long id) => string.Create(CultureInfo.InvariantCulture, $"/staff/training/{id}");

    /// <summary>
    /// Queues a mail of <paramref name="type"/> to <paramref name="vid"/> about <paramref name="training"/>, pointing at
    /// <paramref name="path"/>; <paramref name="fill"/> adds what the type's sentence needs, in the recipient's language.
    /// </summary>
    public async Task SendAsync(
        string type,
        int vid,
        Training training,
        string path,
        Action<IDictionary<string, string>, string>? fill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        var options = division.Value;
        var locale = await hub.Users.AsNoTracking()
            .Where(user => user.Vid == vid)
            .Select(user => user.Locale)
            .FirstOrDefaultAsync(cancellationToken) ?? options.DefaultLocale;

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["training"] = Describe(training, locale),
            ["url"] = $"https://{options.Domain}{path}",
        };
        fill?.Invoke(data, locale);

        await notifications.QueueAsync(new NotificationIntent(type, [NotificationRecipient.Member(vid)], data), cancellationToken);
    }

    /// <summary>A word of the language files, in the recipient's language.</summary>
    public string Word(string locale, string key) => catalog.Resolve(locale, key);

    /// <summary>The training in a line: its ladder, the short name of its rating and its position, as the language says them.</summary>
    private string Describe(Training training, string locale)
    {
        string?[] parts =
        [
            catalog.Resolve(locale, $"training:kinds.{training.Kind}"),
            vocabulary.Find(training.Kind, training.Rating)?.ShortName,
            training.Position,
        ];

        return string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }
}
