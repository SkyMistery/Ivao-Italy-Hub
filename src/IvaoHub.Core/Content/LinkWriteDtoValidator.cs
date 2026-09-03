using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a link has to satisfy before it is written. Messages are i18n keys, never sentences:
/// the browser resolves them into the language it is showing (design M0 sections 3.9 and 7.5).
/// </summary>
public sealed class LinkWriteDtoValidator : AbstractValidator<LinkWriteDto>
{
    /// <summary>Longest URL the column holds.</summary>
    public const int MaxUrlLength = 1024;

    /// <summary>Longest category the column holds.</summary>
    public const int MaxCategoryLength = 64;

    public LinkWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        // A link is shown to everybody who reads the site, so it has to exist in every language of
        // the division; which ones are missing travels with the failure.
        RuleFor(link => link.Title).Required(division.Value);

        RuleFor(link => link.Url)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxUrlLength).WithMessage("errors.text.tooLong")
            .Must(BeAnAbsoluteWebAddress).WithMessage("errors.url.absolute");

        RuleFor(link => link.Category)
            .MaximumLength(MaxCategoryLength).WithMessage("errors.text.tooLong");

        RuleFor(link => link.Sort)
            .GreaterThanOrEqualTo(0).WithMessage("errors.number.min");
    }

    /// <summary>
    /// A link leaves the site, so it needs a scheme a browser will follow. Anything else — a
    /// relative path, a <c>javascript:</c> URL — is refused here rather than sanitised later.
    /// </summary>
    private static bool BeAnAbsoluteWebAddress(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
}
