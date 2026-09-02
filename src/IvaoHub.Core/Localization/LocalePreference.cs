using IvaoHub.Core.Division;

namespace IvaoHub.Core.Localization;

/// <summary>
/// Which language a member sees. One rule, in one place, so that the login, the language switcher
/// of F6 and anything else that has to pick a language all answer the same way.
/// </summary>
public static class LocalePreference
{
    /// <summary>
    /// English. Not a division's choice: it is the language of IVAO and of this project, so it is
    /// what a member falls back to when the division does not speak theirs.
    /// </summary>
    public const string Fallback = "en";

    /// <summary>
    /// The language a member starts with: the one they chose on IVAO if the division speaks it,
    /// English otherwise. A member who has picked a language here keeps it; this is only ever
    /// consulted when there is nothing to keep.
    /// </summary>
    /// <param name="languageId">The language IVAO has for the member, if any.</param>
    /// <param name="options">The division, which decides which languages exist at all.</param>
    public static string Resolve(string? languageId, DivisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (Spoken(options, languageId) is { } wanted)
        {
            return wanted;
        }

        // A division that does not list English at all still has to answer with something it can
        // actually render, and that is its own default.
        return Spoken(options, Fallback) ?? options.DefaultLocale;
    }

    /// <summary>
    /// The language as the division spells it, or null when the division does not speak it.
    /// <c>en-GB</c> counts as <c>en</c>: IVAO and browsers both hand out regional tags.
    /// </summary>
    public static string? Spoken(DivisionOptions options, string? language)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalised = language.Trim().Split('-')[0];

        return options.Locales.FirstOrDefault(
            locale => string.Equals(locale, normalised, StringComparison.OrdinalIgnoreCase));
    }
}
