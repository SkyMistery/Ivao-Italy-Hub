using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a calendar entry has to satisfy before it is written. Messages are i18n keys, never
/// sentences (design M0 sections 3.9 and 7.5).
/// </summary>
public sealed class CalendarWriteDtoValidator : AbstractValidator<CalendarWriteDto>
{
    /// <summary>Longest kind the column holds.</summary>
    public const int MaxKindLength = 32;

    /// <summary>Longest address the column holds.</summary>
    public const int MaxUrlLength = 1024;

    public CalendarWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        // An entry is read on the public calendar, so it exists in every language of the division;
        // which ones are missing travels with the failure.
        RuleFor(entry => entry.Title).Required(division.Value);

        // Free text, because the staff writes it: `meeting`, `deadline`, whatever a department
        // uses. What is checked is that there is one and that it fits the column.
        RuleFor(entry => entry.Kind)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxKindLength).WithMessage("errors.text.tooLong");

        RuleFor(entry => entry.Url)
            .MaximumLength(MaxUrlLength).WithMessage("errors.text.tooLong");

        // An entry that ends before it starts is not a short entry, it is a mistake somebody made
        // in two fields; the calendar would draw it as nothing at all.
        RuleFor(entry => entry.EndsAtUtc)
            .Must((entry, ends) => ends is null || ends >= entry.StartsAtUtc)
            .WithMessage("errors.calendar.endsBeforeStart");
    }
}
