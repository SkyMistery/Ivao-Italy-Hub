using FluentValidation;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a message has to satisfy before it is written. Messages are i18n keys, never
/// sentences: the browser resolves them into the language it is showing (design M0 section 7.5).
/// <para>There is no rule about who the sender is, because the sender is the session. The form is
/// behind <c>SignedIn</c> precisely so that this validator has nothing to say about spam.</para>
/// </summary>
public sealed class ContactSubmitDtoValidator : AbstractValidator<ContactSubmitDto>
{
    /// <summary>Longest subject the column holds.</summary>
    public const int MaxSubjectLength = 200;

    /// <summary>Longest message. Generous, but not "paste a logfile in here".</summary>
    public const int MaxBodyLength = 5000;

    public ContactSubmitDtoValidator()
    {
        RuleFor(message => message.Subject)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxSubjectLength).WithMessage("errors.text.tooLong");

        RuleFor(message => message.Body)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxBodyLength).WithMessage("errors.text.tooLong");
    }
}
