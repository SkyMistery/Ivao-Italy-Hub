using FluentValidation;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Awards;

/// <summary>The rules an award has to satisfy. Messages are i18n keys, never sentences.</summary>
public sealed class AwardWriteDtoValidator : AbstractValidator<AwardWriteDto>
{
    public AwardWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        // Whoever assigns it reads it in their language, whichever department wrote it.
        RuleFor(award => award.Name).Required(division.Value);
    }
}

/// <summary>The rules an assignment has to satisfy on its own; what needs other rows is checked before saving.</summary>
public sealed class AwardAssignmentWriteDtoValidator : AbstractValidator<AwardAssignmentWriteDto>
{
    /// <summary>The width of <c>hub_award_assignments.reason</c>.</summary>
    public const int MaxReasonLength = 512;

    public AwardAssignmentWriteDtoValidator()
    {
        RuleFor(assignment => assignment.AwardId)
            .GreaterThan(0).WithMessage("errors.required");

        RuleFor(assignment => assignment.Vid)
            .GreaterThan(0).WithMessage("errors.required");

        // Why, always: the register is read by people who were not there when it was decided.
        RuleFor(assignment => assignment.Reason)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxReasonLength).WithMessage("errors.text.tooLong");
    }
}

/// <summary>A signal is dismissed or put back by hand; handled only by an assignment.</summary>
public sealed class AwardSignalWriteDtoValidator : AbstractValidator<AwardSignalWriteDto>
{
    public AwardSignalWriteDtoValidator()
    {
        RuleFor(signal => signal.Status)
            .Must(status => status is AwardSignalStatus.Pending or AwardSignalStatus.Dismissed)
            .WithMessage("errors.awards.handledByAssignment");
    }
}
