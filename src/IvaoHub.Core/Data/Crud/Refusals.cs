using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// The refusals of a write gathered field by field, in the shape <see cref="CrudProblems"/> answers and the generated
/// forms read (design M0 sections 3.9 and 7.5): one or more i18n keys per field, each once, and next to a translated
/// field the languages that are missing. For the verbs a validator does not cover — checks across other rows, a
/// pilot's report, a trainee's request — whose every refusal still lands under the field it is about. Written once
/// for every module (note 2026-09-27-i-rifiuti-di-un-form-nel-nucleo).
/// </summary>
public sealed class Refusals
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _missingLocales = new(StringComparer.Ordinal);

    /// <summary>Nothing refused: the write may go on.</summary>
    public bool IsEmpty => _errors.Count == 0;

    /// <summary>The i18n keys of every field, each once, in the order they were refused.</summary>
    public IReadOnlyDictionary<string, string[]> Errors => Snapshot(_errors, StringComparer.Ordinal);

    /// <summary>The languages missing in every field refused with <see cref="Missing"/>; empty when there is none.</summary>
    public IReadOnlyDictionary<string, string[]> MissingLocales => Snapshot(_missingLocales, StringComparer.OrdinalIgnoreCase);

    /// <summary>Refuses the field with this i18n key; a key it already carries is not repeated.</summary>
    public Refusals Add(string field, string key)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(key);

        Entry(_errors, field).Add(key);
        return this;
    }

    /// <summary>
    /// A translated field not written in every language of the division: refused with the key the validator uses,
    /// <see cref="LocalizedRules.MissingMessageKey"/>, and the languages next to it, so the form can say "Italian is
    /// missing" instead of "invalid" (design M0 section 3.1).
    /// </summary>
    public Refusals Missing(string field, IEnumerable<string> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);

        Add(field, LocalizedRules.MissingMessageKey);
        Entry(_missingLocales, field).AddRange(locales);
        return this;
    }

    private static List<string> Entry(Dictionary<string, List<string>> source, string field)
    {
        if (!source.TryGetValue(field, out var values))
        {
            source[field] = values = [];
        }

        return values;
    }

    private static Dictionary<string, string[]> Snapshot(Dictionary<string, List<string>> source, StringComparer values) =>
        source.ToDictionary(entry => entry.Key, entry => entry.Value.Distinct(values).ToArray(), StringComparer.Ordinal);
}
