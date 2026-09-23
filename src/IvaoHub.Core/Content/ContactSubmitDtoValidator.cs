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

        // The kinds a member opens from here; a module's own kinds, a dispute first, are opened by the module.
        RuleFor(message => message.Kind)
            .Must(kind => kind is null || ContactKinds.Submittable.Contains(kind, StringComparer.Ordinal))
            .WithMessage("errors.contacts.kindUnknown");

        RuleFor(message => message.References)
            .Must(references => references is null || references.Count <= ContactThreads.MaxReferences)
            .WithMessage("errors.contacts.tooManyReferences");

        // A clarification is about something; "explain this" with nothing to point at is a general message.
        RuleFor(message => message.References)
            .Must(references => references is { Count: > 0 })
            .When(message => message.Kind == ContactKinds.Clarification)
            .WithMessage("errors.contacts.referenceRequired");

        RuleForEach(message => message.References)
            .Must(reference => reference is not null
                && !string.IsNullOrWhiteSpace(reference.SourceModule)
                && !string.IsNullOrWhiteSpace(reference.SourceId)
                && reference.SourceModule.Length <= 32
                && reference.SourceId.Length <= 64)
            .WithMessage("errors.contacts.referenceUnknown")
            .OverridePropertyName("references");
    }
}

/// <summary>An answer has to say something, and not a logfile's worth of it.</summary>
public sealed class ContactReplyWriteDtoValidator : AbstractValidator<ContactReplyWriteDto>
{
    public ContactReplyWriteDtoValidator()
    {
        RuleFor(reply => reply.Body)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(ContactSubmitDtoValidator.MaxBodyLength).WithMessage("errors.text.tooLong");
    }
}
