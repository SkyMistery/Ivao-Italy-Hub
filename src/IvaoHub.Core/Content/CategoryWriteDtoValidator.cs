using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a category has to satisfy before it is written. Messages are i18n keys, never
/// sentences (design M0 sections 3.9 and 7.5).
/// </summary>
public sealed partial class CategoryWriteDtoValidator : AbstractValidator<CategoryWriteDto>
{
    /// <summary>Longest key the column holds; the same width as <c>cms_contents.category</c>.</summary>
    public const int MaxKeyLength = 64;

    public CategoryWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        // The label is what a visitor reads on the public site, so it exists in every language of
        // the division; which ones are missing travels with the failure.
        RuleFor(category => category.Label).Required(division.Value);

        // The same shape as a slug, and for the same reason: a key ends up in a query string and
        // in a body already published, so it has to be typeable in any alphabet's keyboard.
        RuleFor(category => category.Key)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxKeyLength).WithMessage("errors.text.tooLong")
            .Must(key => key is null || KeyPattern().IsMatch(key)).WithMessage("errors.slug.invalid");

        RuleFor(category => category.Sort)
            .GreaterThanOrEqualTo(0).WithMessage("errors.number.min");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}
