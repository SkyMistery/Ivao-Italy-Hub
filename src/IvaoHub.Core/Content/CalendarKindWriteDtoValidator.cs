using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a kind has to satisfy before it is written. Messages are i18n keys, never sentences
/// (design M0 sections 3.9 and 7.5).
/// </summary>
public sealed partial class CalendarKindWriteDtoValidator : AbstractValidator<CalendarKindWriteDto>
{
    /// <summary>Longest key the column holds; the same width as <c>cms_calendar_entries.kind</c>.</summary>
    public const int MaxKeyLength = 32;

    /// <summary>
    /// The colours a chip may take, which is the palette of the badge the design system ships. It
    /// is a closed set for the reason every closed set of this hub is one: a value nobody refused
    /// would be drawn as no colour at all, and the row would have said something that never
    /// appeared.
    /// <para>⚠️ The other half is <c>CALENDAR_KIND_COLOURS</c> in <c>web/src/shared/ui/calendar.ts</c>.
    /// The two agree by hand — a colour is a value inside a design system the contract cannot carry
    /// — and the integration test that posts a colour the server does not know is what keeps them
    /// agreeing, exactly as it does for the backgrounds of a section.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Colours =
        ["blue", "green", "orange", "purple", "indigo", "pink", "red", "yellow", "gray"];

    public CalendarKindWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        // What a reader sees, so it exists in every language of the division; which ones are
        // missing travels with the failure.
        RuleFor(kind => kind.Label).Required(division.Value);

        // The same shape as a slug, and for the same reason: a key ends up in a query string and in
        // rows already written, so it has to be typeable on any alphabet's keyboard.
        RuleFor(kind => kind.Key)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxKeyLength).WithMessage("errors.text.tooLong")
            .Must(key => key is null || KeyPattern().IsMatch(key)).WithMessage("errors.slug.invalid");

        RuleFor(kind => kind.Colour)
            .Must(colour => colour is not null && Colours.Contains(colour, StringComparer.Ordinal))
            .WithMessage("errors.calendar.colourUnknown");

        RuleFor(kind => kind.Sort)
            .GreaterThanOrEqualTo(0).WithMessage("errors.number.min");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}
