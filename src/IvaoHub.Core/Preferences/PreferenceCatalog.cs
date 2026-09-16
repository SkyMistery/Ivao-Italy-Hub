using System.Text.Json;

namespace IvaoHub.Core.Preferences;

/// <summary>
/// One preference a module lets its members keep (M2, T4b).
/// </summary>
/// <param name="Key">
/// The name, always the module's key and a dot first — <c>flightops.reviewQueueOrder</c> — so two
/// modules can never claim the same one.
/// </param>
/// <param name="Accepts">
/// Whether a value is one the module can read back. The core stores nothing it has not been told is
/// valid: a value the module would fail on tomorrow is refused today, with a 400, rather than stored.
/// </param>
public sealed record PreferenceDescriptor(string Key, Func<JsonElement, bool> Accepts)
{
    /// <summary>A preference whose value is one of a closed set of words, the common case.</summary>
    public static PreferenceDescriptor OneOf(string key, params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new PreferenceDescriptor(
            key,
            value => value.ValueKind == JsonValueKind.String && values.Contains(value.GetString(), StringComparer.Ordinal));
    }
}

/// <summary>
/// Every preference this installation knows: the ones the enabled modules declare in
/// <c>IModule.Preferences</c>. The core declares none of its own — its two settings of a member,
/// the language and the notifications, have their own endpoints and stay where they are.
/// <para>Composed once, like <c>PermissionCatalog</c>, and it refuses at start up what would
/// otherwise be a quiet collision: a key declared twice, or a key outside its module's name.</para>
/// </summary>
public sealed class PreferenceCatalog
{
    /// <summary>The width of <c>hub_user_preferences.key</c>.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>
    /// The largest value stored, in bytes of JSON. A preference is a choice, not a document: the order
    /// of a queue is a word, and anything near this size is a sign of something else being stored.
    /// </summary>
    public const int MaxValueBytes = 4096;

    private readonly Dictionary<string, PreferenceDescriptor> _byKey = new(StringComparer.Ordinal);

    /// <param name="declared">Each module's key and what it declares.</param>
    public PreferenceCatalog(IEnumerable<(string Module, IReadOnlyList<PreferenceDescriptor> Preferences)> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        foreach (var (module, preferences) in declared)
        {
            foreach (var preference in preferences)
            {
                if (!preference.Key.StartsWith(module + ".", StringComparison.Ordinal)
                    || preference.Key.Length == module.Length + 1
                    || preference.Key.Length > MaxKeyLength)
                {
                    throw new InvalidOperationException(
                        $"The preference '{preference.Key}' of the module '{module}' has to be named "
                        + $"'{module}.<name>' and fit in {MaxKeyLength} characters.");
                }

                if (!_byKey.TryAdd(preference.Key, preference))
                {
                    throw new InvalidOperationException($"The preference '{preference.Key}' is declared twice.");
                }
            }
        }
    }

    public IReadOnlyCollection<string> Keys => _byKey.Keys;

    public bool TryGet(string key, out PreferenceDescriptor descriptor) =>
        _byKey.TryGetValue(key, out descriptor!);
}
