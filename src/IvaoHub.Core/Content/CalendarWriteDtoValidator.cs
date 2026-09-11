using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a calendar entry has to satisfy before it is written. Messages are i18n keys, never
/// sentences (design M0 sections 3.9 and 7.5).
/// <para>⚠️ It asks the database one question, which no other validator of this hub does: whether
/// the kind is a word of the division's vocabulary. It is here and not in the endpoint because it
/// is a rule about the payload, and a rule the client would otherwise have to be trusted to obey —
/// the select it draws is a convenience, and this is the answer.</para>
/// <para>What it does <b>not</b> check is an entry a module projects: those never come through this
/// DTO, they are written by the projection in the same transaction as the row they mirror. A module
/// that wants its own word puts it in the vocabulary; nothing here can stop it, and nothing here
/// should.</para>
/// </summary>
public sealed class CalendarWriteDtoValidator : AbstractValidator<CalendarWriteDto>
{
    /// <summary>Longest kind the column holds.</summary>
    public const int MaxKindLength = 32;

    /// <summary>Longest address the column holds.</summary>
    public const int MaxUrlLength = 1024;

    public CalendarWriteDtoValidator(IOptions<DivisionOptions> division, HubDbContext database)
    {
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(database);

        // An entry is read on the public calendar, so it exists in every language of the division;
        // which ones are missing travels with the failure.
        RuleFor(entry => entry.Title).Required(division.Value);

        // ⚠️ Not free text any more. Until G13 anybody could type one, and two departments writing
        // `Training` and `training` had two kinds in a calendar that is supposed to be one
        // (decided 7 Sep 2026, note `decisions/2026-09-08-tipi-di-evento-di-divisione.md`). The
        // list is the division's, so this is where a word that is not in it is refused.
        RuleFor(entry => entry.Kind)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxKindLength).WithMessage("errors.text.tooLong")
            .MustAsync(async (kind, cancellationToken) =>
                string.IsNullOrEmpty(kind)
                || await database.CalendarKinds
                    .AsNoTracking()
                    .AnyAsync(row => row.Key == kind && row.IsActive, cancellationToken))
            .WithMessage("errors.calendar.kindUnknown");

        RuleFor(entry => entry.Url)
            .MaximumLength(MaxUrlLength).WithMessage("errors.text.tooLong");

        // An entry that ends before it starts is not a short entry, it is a mistake somebody made
        // in two fields; the calendar would draw it as nothing at all.
        RuleFor(entry => entry.EndsAtUtc)
            .Must((entry, ends) => ends is null || ends >= entry.StartsAtUtc)
            .WithMessage("errors.calendar.endsBeforeStart");
    }
}
