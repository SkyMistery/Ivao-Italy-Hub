using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace IvaoHub.Core.Localization;

/// <summary>
/// A field translated into the languages of the division: one JSON column on the row, never a
/// separate translations table (plan section 16.1). Keys are language codes ("it", "en") and are
/// kept sorted so that two equal values always serialise to the same JSON.
/// </summary>
public sealed record Localized<T> : IReadOnlyDictionary<string, T>
{
    private readonly ImmutableSortedDictionary<string, T> _values;

    public Localized(IEnumerable<KeyValuePair<string, T>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToImmutableSortedDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static Localized<T> Empty { get; } = new([]);

    /// <summary>The value for that language, or <c>null</c> when the language is missing.</summary>
    public T? Get(string locale) => _values.TryGetValue(locale, out var value) ? value : default;

    /// <summary>Requested language, then the fallback language, then whatever is available.</summary>
    public T? Resolve(string locale, string fallback)
    {
        if (_values.TryGetValue(locale, out var value))
        {
            return value;
        }

        if (_values.TryGetValue(fallback, out var fallbackValue))
        {
            return fallbackValue;
        }

        return _values.Count > 0 ? _values.Values.First() : default;
    }

    public Localized<T> With(string locale, T value) => new(_values.SetItem(locale, value));

    /// <summary>Used by the "every language before publishing" rule (design M0 section 3.1).</summary>
    public bool HasAll(IEnumerable<string> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);
        return locales.All(locale => _values.TryGetValue(locale, out var value) && !IsBlank(value));
    }

    /// <summary>The languages that are missing or empty, in the order they were asked for.</summary>
    public IReadOnlyList<string> MissingLocales(IEnumerable<string> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);
        return [.. locales.Where(locale => !_values.TryGetValue(locale, out var value) || IsBlank(value))];
    }

    private static bool IsBlank(T? value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        _ => false,
    };

    /// <summary>
    /// Compared through the dictionary of the other side, so that the keys are matched the way this
    /// type stores them: case insensitively. Set arithmetic over the pairs would compare the keys
    /// ordinally instead, and make <c>{"EN": "x"}</c> differ from <c>{"en": "x"}</c> even though
    /// both resolve to the same value.
    /// </summary>
    public bool Equals(Localized<T>? other) =>
        other is not null
        && _values.Count == other._values.Count
        && _values.All(pair =>
            other._values.TryGetValue(pair.Key, out var value)
            && EqualityComparer<T>.Default.Equals(pair.Value, value));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var (key, value) in _values)
        {
            hash.Add(key, StringComparer.OrdinalIgnoreCase);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    public T this[string key] => _values[key];
    public IEnumerable<string> Keys => _values.Keys;
    public IEnumerable<T> Values => _values.Values;
    public int Count => _values.Count;
    public bool ContainsKey(string key) => _values.ContainsKey(key);
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value) => _values.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<string, T>> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class LocalizedExtensions
{
    /// <summary>Shorthand for seeds and tests only, never for production code paths.</summary>
    public static Localized<string> L(this string italian, string english) =>
        new([new KeyValuePair<string, string>("it", italian), new KeyValuePair<string, string>("en", english)]);

    public static Localized<T> ToLocalized<T>(this IEnumerable<KeyValuePair<string, T>> values) => new(values);
}
