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
/// <para>One place for what every mail of the module says of a training, so the request received (A6a), the staff's
/// decisions (A7) and the dates (A8) say it alike. A moment is written in UTC, and the sentence around it says so, as the pages
/// do (A6b). A person a mail names besides its recipient — the trainer to the trainee, the trainee to the trainer — carries their VID
/// beside their name (<see cref="Name"/>), so that erasing their data takes the mail too (A12b).</para>
/// </summary>
public sealed class TrainingMail(
    HubDbContext hub,
    RatingVocabulary vocabulary,
    INotificationService notifications,
    LocaleCatalog catalog,
    TrainingPeople people,
    IOptions<DivisionOptions> division)
{
    /// <summary>The trainee's page of their trainings (A6b), where a mail to a trainee about a request points.</summary>
    public const string MinePath = "/training/mine";

    /// <summary>The trainee's page of one training (design M3 §4.1), where a mail to a trainee about its dates points.</summary>
    public static string MinePathOf(long id) => string.Create(CultureInfo.InvariantCulture, $"{MinePath}/{id}");

    /// <summary>The staff's page of one training (A7), where a mail to the trainer points.</summary>
    public static string StaffPath(long id) => string.Create(CultureInfo.InvariantCulture, $"/staff/training/{id}");

    /// <summary>A moment of a session as a mail writes it, the day and the time in UTC; the sentence around it says UTC.</summary>
    public static string Moment(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A date proposed as a mail writes it: its day and hours, and the day of its end too when it is another.</summary>
    public static string Span(DateTime startsAtUtc, DateTime endsAtUtc) =>
        startsAtUtc.Date == endsAtUtc.Date
            ? string.Create(CultureInfo.InvariantCulture, $"{Moment(startsAtUtc)}–{endsAtUtc:HH:mm}")
            : $"{Moment(startsAtUtc)} – {Moment(endsAtUtc)}";

    /// <summary>
    /// Queues a mail of <paramref name="type"/> to <paramref name="vid"/> about <paramref name="training"/>, pointing at
    /// <paramref name="path"/>; <paramref name="fill"/> adds what the type's sentence needs, in the recipient's language.
    /// </summary>
    public Task SendAsync(
        string type,
        int vid,
        Training training,
        string path,
        Action<IDictionary<string, string>, string>? fill,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        return SendAsync(
            type,
            vid,
            path,
            (data, locale) =>
            {
                data["training"] = Describe(training, locale);
                fill?.Invoke(data, locale);
            },
            cancellationToken);
    }

    /// <summary>
    /// Queues a mail of <paramref name="type"/> to <paramref name="vid"/> that is about no one training — a ban (A10a) —, pointing at
    /// <paramref name="path"/>; <paramref name="fill"/> adds what the type's sentence needs, in the recipient's language.
    /// </summary>
    public async Task SendAsync(
        string type,
        int vid,
        string path,
        Action<IDictionary<string, string>, string>? fill,
        CancellationToken cancellationToken)
    {
        var options = division.Value;
        var locale = await hub.Users.AsNoTracking()
            .Where(user => user.Vid == vid)
            .Select(user => user.Locale)
            .FirstOrDefaultAsync(cancellationToken) ?? options.DefaultLocale;

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["url"] = $"https://{options.Domain}{path}",
        };
        fill?.Invoke(data, locale);

        await notifications.QueueAsync(new NotificationIntent(type, [NotificationRecipient.Member(vid)], data), cancellationToken);
    }

    /// <summary>
    /// A mail about the session in hand to both its people (§5.2: the date fixed, the reminder): the trainee, pointing at their page
    /// of the training, and the trainer, pointing at the staff's — each in their language, with who trains whom and when. Nothing
    /// for a training with no date.
    /// </summary>
    public async Task SessionAsync(string type, Training training, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(training);

        if (training.ScheduledStartUtc is not { } start)
        {
            return;
        }

        var names = await people.NamesAsync([training.TraineeVid, training.TrainerVid], cancellationToken);
        var trainee = TrainingPeople.Label(training.TraineeVid, names);
        var trainer = training.TrainerVid is { } vid ? TrainingPeople.Label(vid, names) : string.Empty;

        await SendAsync(type, training.TraineeVid, training, MinePathOf(training.Id), (data, locale) => Fill(data, locale, "training:mail.training.sessionTrainee"), cancellationToken);

        if (training.TrainerVid is { } trainerVid)
        {
            await SendAsync(type, trainerVid, training, StaffPath(training.Id), (data, locale) => Fill(data, locale, "training:mail.training.sessionTrainer"), cancellationToken);
        }

        void Fill(IDictionary<string, string> data, string locale, string next)
        {
            Name(data, "trainee", training.TraineeVid, trainee);
            if (training.TrainerVid is { } named)
            {
                Name(data, "trainer", named, trainer);
            }
            else
            {
                data["trainer"] = trainer;
            }

            data["session"] = Moment(start);
            data["next"] = Word(locale, next);
        }
    }

    /// <summary>
    /// A person a mail names, the way the core finds a mail about them when their data is erased (note
    /// <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c> §7; A12b): the words under <paramref name="key"/> — their name and
    /// VID, as the sentence says them — and the VID alone under <c>{key}Vid</c>, a name the erasure reads as a person's, as the mails of
    /// the threads carry their sender's. Without it, a mail to somebody else that names them would keep their name after the erasure.
    /// </summary>
    public static void Name(IDictionary<string, string> data, string key, int vid, string words)
    {
        ArgumentNullException.ThrowIfNull(data);

        data[key] = words;
        data[key + "Vid"] = vid.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A word of the language files, in the recipient's language.</summary>
    public string Word(string locale, string key) => catalog.Resolve(locale, key);

    /// <summary>
    /// A sentence of the language files with one value of its own in it, <c>{{name}}</c> as every template writes it: the template
    /// of a mail is filled once, so a sentence that goes into it comes filled.
    /// </summary>
    public string Word(string locale, string key, string name, string value) =>
        Word(locale, key).Replace("{{" + name + "}}", value, StringComparison.Ordinal);

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
