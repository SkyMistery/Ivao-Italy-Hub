using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules the metadata of a file has to satisfy. Messages are i18n keys, never sentences
/// (design M0 sections 3.9 and 7.5).
/// </summary>
public sealed class MediaWriteDtoValidator : AbstractValidator<MediaWriteDto>
{
    /// <summary>Longest category the column holds.</summary>
    public const int MaxCategoryLength = 64;

    public MediaWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        // The alternative text is the one thing a file cannot be published without: it is what a
        // reader using a screen reader is given instead of the picture, in their own language.
        RuleFor(media => media.Alt).Required(division.Value);

        RuleFor(media => media.Category)
            .MaximumLength(MaxCategoryLength).WithMessage("errors.text.tooLong");
    }
}
