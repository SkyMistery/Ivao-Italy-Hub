namespace IvaoHub.Modules.Training;

/// <summary>
/// Refusals as a form reads them: one or more i18n keys per field, the shape of the <c>ProblemDetails</c> of the generated forms
/// (design M0 §7.5). For the verbs of the module that are not a form over a row — a request is a set of checks across the
/// trainee's history (design M3 §2.2) — and whose every refusal still lands under the field it is about.
/// </summary>
internal sealed class Refusals
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsEmpty => _errors.Count == 0;

    public IReadOnlyDictionary<string, string[]> Errors =>
        _errors.ToDictionary(entry => entry.Key, entry => entry.Value.Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

    public Refusals Add(string field, string key)
    {
        if (!_errors.TryGetValue(field, out var keys))
        {
            _errors[field] = keys = [];
        }

        keys.Add(key);
        return this;
    }
}
