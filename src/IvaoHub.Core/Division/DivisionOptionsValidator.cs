using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Division;

/// <summary>
/// Refuses to start with a division file that would produce a broken site (design M0 section 2.1).
/// Every message names the file and the field, so that a division which forks can fix it alone.
/// </summary>
public sealed partial class DivisionOptionsValidator : IValidateOptions<DivisionOptions>
{
    private readonly IReadOnlyCollection<string> _knownModuleKeys;

    /// <param name="knownModuleKeys">
    /// Keys the module registry knows about. Unknown keys are a warning, not a failure, so that a
    /// division can keep a key for a module it has not merged yet.
    /// </param>
    public DivisionOptionsValidator(IReadOnlyCollection<string>? knownModuleKeys = null) =>
        _knownModuleKeys = knownModuleKeys ?? [];

    /// <summary>Module keys that were configured but are unknown to the registry.</summary>
    public IReadOnlyList<string> UnknownModuleKeys { get; private set; } = [];

    public ValidateOptionsResult Validate(string? name, DivisionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!DivisionCode().IsMatch(options.Code))
        {
            failures.Add("division.json: 'code' must be 2 or 3 upper case letters, for example \"IT\".");
        }

        if (!DivisionCode().IsMatch(options.CountryId))
        {
            failures.Add("division.json: 'countryId' must be a 2 or 3 letter ISO country code.");
        }

        if (string.IsNullOrWhiteSpace(options.Domain))
        {
            failures.Add("division.json: 'domain' is required.");
        }

        if (options.Locales.Length == 0)
        {
            failures.Add("division.json: 'locales' must list at least one language.");
        }
        else if (options.Locales.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Locales.Length)
        {
            failures.Add("division.json: 'locales' contains the same language twice.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultLocale))
        {
            failures.Add("division.json: 'defaultLocale' is required.");
        }
        else if (options.Locales.Length > 0 && !options.Locales.Contains(options.DefaultLocale, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"division.json: 'defaultLocale' ({options.DefaultLocale}) is not one of 'locales'.");
        }

        foreach (var locale in options.Locales)
        {
            if (!options.Name.ContainsKey(locale))
            {
                failures.Add($"division.json: 'name' has no entry for the language '{locale}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.Timezone))
        {
            failures.Add("division.json: 'timezone' is required.");
        }
        else if (!IsKnownTimeZone(options.Timezone))
        {
            failures.Add($"division.json: 'timezone' ({options.Timezone}) is not a time zone this machine knows.");
        }

        UnknownModuleKeys = [.. options.Modules.Keys.Where(key => !_knownModuleKeys.Contains(key))];

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsKnownTimeZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[A-Z]{2,3}$")]
    private static partial Regex DivisionCode();
}
