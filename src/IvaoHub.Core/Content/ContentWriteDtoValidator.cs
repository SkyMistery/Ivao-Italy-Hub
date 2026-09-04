using System.Text.RegularExpressions;
using FluentValidation;

namespace IvaoHub.Core.Content;

/// <summary>
/// What a content row has to satisfy before it is written. Messages are i18n keys, never sentences.
/// <para>Notice what is <b>not</b> here: "a title in every language of the division". A draft is
/// allowed to be half written — that is what a draft is for — and the rule that every language must
/// be present belongs to publication, which is the moment somebody is about to show the page to the
/// public (design M0 sections 3.1 and 5.5).</para>
/// </summary>
public sealed partial class ContentWriteDtoValidator : AbstractValidator<ContentWriteDto>
{
    /// <summary>Longest slug the unique index holds.</summary>
    public const int MaxSlugLength = 160;

    public ContentWriteDtoValidator(BlockDocumentWalker walker, BlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(blocks);

        // A row still has to be findable in a list, so it needs a name in at least one language.
        RuleFor(content => content.Title)
            .Must(title => title.Values.Any(text => !string.IsNullOrWhiteSpace(text)))
            .WithMessage("errors.required");

        RuleFor(content => content.Slug)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxSlugLength).WithMessage("errors.text.tooLong")
            .Must(slug => slug is null || SlugPattern().IsMatch(slug)).WithMessage("errors.slug.invalid");

        RuleFor(content => content.SchemaVersion)
            .Equal(BlockDocumentWalker.SupportedSchemaVersion)
            .WithMessage("errors.body.schemaVersion");

        // The envelope, and only the envelope. Each failure is filed under the path of the thing
        // that is wrong -- `body.sections[0].blocks[2]` -- so the editor can put the message next
        // to the block rather than at the top of the screen.
        RuleFor(content => content.Body).Custom((body, context) =>
        {
            var payload = context.InstanceToValidate;
            var validation = walker.ValidateEnvelope(body, blocks.Types, payload.IsTemplate);

            foreach (var error in validation.Errors)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure(
                    error.Path == "$" ? nameof(ContentWriteDto.Body) : $"{nameof(ContentWriteDto.Body)}.{error.Path}",
                    error.Key));
            }
        });
    }

    /// <summary>
    /// What may appear in an address. Lower case, digits and single dashes: the slug is part of a
    /// URL that outlives the page, and a fork writing in another alphabet still gets one it can
    /// type. The editor proposes one from the title; this is what it is held to.
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
