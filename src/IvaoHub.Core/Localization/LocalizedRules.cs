using FluentValidation;
using IvaoHub.Core.Division;

namespace IvaoHub.Core.Localization;

/// <summary>
/// The languages a localized field has to carry, in one place. A draft may well be incomplete, so
/// the rule "every language" belongs to the publication service and to the write DTOs that ask for
/// it, never to the entity (design M0 section 3.1).
/// </summary>
public static class LocalizedRules
{
    /// <summary>The i18n key the single page application resolves; the API never sends prose.</summary>
    public const string MissingMessageKey = "errors.localized.missing";

    /// <summary>
    /// The field has a non empty value in every language of the division. What is missing travels
    /// in the validation state, so the problem details can name the languages instead of saying
    /// "invalid".
    /// </summary>
    public static IRuleBuilderOptions<T, Localized<TValue>> Required<T, TValue>(
        this IRuleBuilder<T, Localized<TValue>> rule,
        DivisionOptions division)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(division);

        return rule
            .Must(value => value is not null && value.HasAll(division.Locales))
            .WithMessage(MissingMessageKey)
            .WithState((_, value) => new LocalizedMissing(
                value is null ? division.Locales : [.. value.MissingLocales(division.Locales)]));
    }
}

/// <summary>The languages that are missing from a localized field, attached to the failure.</summary>
public sealed record LocalizedMissing(IReadOnlyList<string> Locales);
