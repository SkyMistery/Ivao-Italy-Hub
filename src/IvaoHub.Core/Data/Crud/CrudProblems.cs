using System.Text.Json;
using FluentValidation.Results;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Http;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// What the API answers when a write is refused. The machine readable part carries i18n keys —
/// <c>errors.localized.missing</c> — because only the browser knows which language it is showing;
/// the human readable title is resolved here from the very same language files, so that a caller
/// which is not our own client still gets a sentence and no sentence is ever written in the code
/// (design M0 sections 3.9 and 7.5).
/// </summary>
public static class CrudProblems
{
    public const string ValidationTitleKey = "errors.validation.title";
    public const string ForbiddenTitleKey = "errors.forbidden.title";
    public const string ConflictTitleKey = "errors.conflict.title";
    public const string NotFoundTitleKey = "errors.notFound.title";

    /// <summary>
    /// The languages that are missing, per field, next to the plain list of keys. A form can then
    /// say "Italian is missing" instead of "invalid", which is the whole reason the validator
    /// carries its state (design M0 section 3.1).
    /// </summary>
    public const string LocalizedExtension = "localized";

    /// <summary>400 with one i18n key per field, in the shape the form generator reads.</summary>
    public static IResult Validation(ValidationResult result, LocaleCatalog catalog, string locale)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = result.Errors
            .GroupBy(failure => FieldName(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        var missingLocales = result.Errors
            .Where(failure => failure.CustomState is LocalizedMissing)
            .GroupBy(failure => FieldName(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(failure => ((LocalizedMissing)failure.CustomState).Locales)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.Ordinal);

        return Validation(errors, missingLocales, catalog, locale);
    }

    /// <summary>
    /// The same answer, from a caller that is not a FluentValidation validator. Publication is the
    /// first: its rules are about the row as a whole and about a tree of blocks, not about the
    /// fields of a payload, but a refusal has to reach the form in exactly one shape.
    /// </summary>
    public static IResult Validation(
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]> missingLocales,
        LocaleCatalog catalog,
        string locale)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(missingLocales);
        ArgumentNullException.ThrowIfNull(catalog);

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (missingLocales.Count > 0)
        {
            extensions[LocalizedExtension] = missingLocales;
        }

        return Results.ValidationProblem(
            errors.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            title: catalog.Resolve(locale, ValidationTitleKey),
            extensions: extensions);
    }

    /// <summary>A field name as the API spells it, so the client matches it to its own form.</summary>
    public static string FieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? string.Empty : JsonNamingPolicy.CamelCase.ConvertName(propertyName);
}
