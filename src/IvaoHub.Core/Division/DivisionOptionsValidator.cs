using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Division;

/// <summary>
/// Refuses to start with a division file that would produce a broken site (design M0 section 2.1).
/// Every message names the file and the field, so that a division which forks can fix it alone.
/// </summary>
public sealed partial class DivisionOptionsValidator : IValidateOptions<DivisionOptions>
{
    private readonly IReadOnlyCollection<string> _knownModuleKeys;
    private readonly ILogger<DivisionOptionsValidator>? _logger;

    /// <param name="knownModuleKeys">
    /// The keys of every module this build has, from the explicit list. Unknown keys are a warning
    /// and not a failure, so that a division can keep a key for a module it has not merged yet;
    /// nothing is reported when nothing is known, which is the case of a host with no modules.
    /// </param>
    /// <param name="logger">Where an unknown key is reported. A validator has no other way out.</param>
    public DivisionOptionsValidator(
        IReadOnlyCollection<string>? knownModuleKeys = null,
        ILogger<DivisionOptionsValidator>? logger = null)
    {
        _knownModuleKeys = knownModuleKeys ?? [];
        _logger = logger;
    }

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

        foreach (var prefix in options.IcaoPrefixes)
        {
            // Checked because it is otherwise a field nobody ever looks at until the day it is
            // wrong: a lower case or over long prefix would silently match nothing.
            if (!IcaoPrefix().IsMatch(prefix))
            {
                failures.Add(
                    $"division.json: 'icaoPrefixes' contains '{prefix}'; a prefix is 1 to 4 upper "
                    + "case letters, for example \"LI\".");
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

        // Reported, not remembered. This object is a singleton and validation can run more than
        // once, so a property holding the result of the last call is a field that means nothing to
        // whoever reads it and is unsafe for whoever reads it from another thread.
        var unknown = options.Modules.Keys.Where(key => !_knownModuleKeys.Contains(key)).ToArray();
        if (unknown.Length > 0 && _knownModuleKeys.Count > 0)
        {
            _logger?.LogWarning(
                "division.json enables {Count} module(s) the registry does not know: {Keys}.",
                unknown.Length,
                string.Join(", ", unknown));
        }

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

    [GeneratedRegex("^[A-Z]{1,4}$")]
    private static partial Regex IcaoPrefix();
}
