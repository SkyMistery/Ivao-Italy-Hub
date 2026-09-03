using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Localization;

/// <summary>
/// The language files of the division, read by the back end. There is exactly one set of them,
/// under <c>locales/{lang}/*.json</c>, and both the single page application and the server read it:
/// no <c>.resx</c>, no second place where a sentence can live (plan section 16.8).
/// <para>The server needs it wherever it produces something a person reads without the client
/// resolving it: the title of a problem details answer today, the mail templates in M1. What the
/// API sends in the machine readable part stays a key — <c>errors.localized.missing</c> — because
/// the client knows which language it is showing and the server does not.</para>
/// </summary>
public sealed class LocaleCatalog
{
    private readonly Lazy<FrozenDictionary<string, FrozenDictionary<string, string>>> _byLocale;
    private readonly DivisionOptions _division;

    public LocaleCatalog(HubPaths paths, IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(division);

        _division = division.Value;
        _byLocale = new Lazy<FrozenDictionary<string, FrozenDictionary<string, string>>>(
            () => Load(paths.Locales, _division.Locales));
    }

    /// <summary>The languages that actually have a directory on disk.</summary>
    public IReadOnlyCollection<string> Locales => _byLocale.Value.Keys;

    /// <summary>
    /// The text for that key, or <c>null</c> when neither the requested language nor the default
    /// one has it. Keys are the flattened path of the JSON file, exactly as the client writes
    /// them: <c>nav.home</c>, <c>errors.localized.missing</c>.
    /// </summary>
    public string? Get(string locale, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (locale is not null
            && _byLocale.Value.TryGetValue(locale, out var requested)
            && requested.TryGetValue(key, out var text))
        {
            return text;
        }

        return _byLocale.Value.TryGetValue(_division.DefaultLocale, out var fallback)
            && fallback.TryGetValue(key, out var fallbackText)
            ? fallbackText
            : null;
    }

    /// <summary>
    /// The text for that key, falling back to the key itself. A missing key is a mistake the
    /// language check of the build catches, and it must not turn into an empty screen meanwhile.
    /// </summary>
    public string Resolve(string locale, string key) => Get(locale, key) ?? key;

    private static FrozenDictionary<string, FrozenDictionary<string, string>> Load(
        string root,
        IReadOnlyCollection<string> locales)
    {
        var loaded = new Dictionary<string, FrozenDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var locale in locales)
        {
            var directory = Path.Combine(root, locale);
            if (!Directory.Exists(directory))
            {
                // A fork that has not translated itself yet still has to start; the missing
                // language shows up as untranslated keys rather than as a crash on boot.
                continue;
            }

            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(name => name, StringComparer.Ordinal))
            {
                Flatten(JsonNode.Parse(File.ReadAllText(file)), prefix: string.Empty, texts, file);
            }

            loaded[locale] = texts.ToFrozenDictionary(StringComparer.Ordinal);
        }

        return loaded.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Namespaces are a loading detail of the client, not part of a key: the files of one language
    /// flatten into a single map. Two namespaces claiming the same key would make the answer
    /// depend on the order the files were read, so it is refused instead.
    /// </summary>
    private static void Flatten(JsonNode? node, string prefix, Dictionary<string, string> texts, string file)
    {
        switch (node)
        {
            case JsonObject entries:
                foreach (var (key, child) in entries)
                {
                    Flatten(child, prefix.Length == 0 ? key : $"{prefix}.{key}", texts, file);
                }

                break;

            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                if (!texts.TryAdd(prefix, value.GetValue<string>()))
                {
                    throw new InvalidOperationException(
                        $"The translation key '{prefix}' is declared twice for the same language; "
                        + $"the second one is in '{file}'.");
                }

                break;

            default:
                // Arrays and numbers are not translations; the client does not use them either.
                break;
        }
    }
}
