using System.Text.RegularExpressions;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Notifications;

/// <summary>
/// The words of a mail. They are not rows and not <c>.resx</c>: they are the same
/// <c>locales/{lang}/*.json</c> files the single page application reads, in the namespace
/// <c>mail</c>, resolved by <see cref="LocaleCatalog"/> in the language of the recipient (design M1
/// section 5.2).
/// <para>Placeholders are written <c>{{name}}</c>, exactly as i18next writes them everywhere else
/// in this project, so that a translator opening <c>mail.json</c> sees the same thing they see in
/// <c>common.json</c>. A name the data does not have is left as it stands: a mail that says
/// <c>{{department}}</c> is a bug somebody reports, while an empty gap is a bug nobody notices.</para>
/// </summary>
public static partial class MailTemplate
{
    /// <summary>Renders the subject and the body of one type into one mail.</summary>
    public static OutgoingMail Render(
        LocaleCatalog catalog,
        string locale,
        string type,
        string address,
        IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(data);

        return new OutgoingMail(
            address,
            Fill(catalog.Resolve(locale, NotificationTypes.SubjectKey(type)), data),
            Fill(catalog.Resolve(locale, NotificationTypes.BodyKey(type)), data));
    }

    private static string Fill(string text, IReadOnlyDictionary<string, string> data) =>
        Placeholder().Replace(text, match =>
            data.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

    [GeneratedRegex(@"\{\{\s*([\w.]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
