using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// What a content row has to satisfy before it is written. Messages are i18n keys, never sentences.
/// <para>Notice what is <b>not</b> here: "a title in every language of the division". A draft is
/// allowed to be half written — that is what a draft is for — and the rule that every language must
/// be present belongs to publication, which is the moment somebody is about to show the page to the
/// public (design M0 sections 3.1 and 5.5).</para>
/// <para>The operational fields of a document (G14) are the exception that proves it: a draft may
/// leave them empty, but what it does write is checked against the division's own airspace right
/// away, because a wrong ICAO is a typing mistake and not a translation still to come.</para>
/// </summary>
public sealed partial class ContentWriteDtoValidator : AbstractValidator<ContentWriteDto>
{
    /// <summary>Longest slug the unique index holds.</summary>
    public const int MaxSlugLength = 160;

    /// <summary>Longest callsign a position field holds; <c>LIRR_N_CTR</c> is ten.</summary>
    public const int MaxPositionLength = 16;

    /// <summary>The fields only an operational document carries, by the name the form uses.</summary>
    private static readonly IReadOnlyList<(string Field, Func<ContentWriteDto, bool> IsSet)> DocumentOnly =
    [
        ("documentType", content => content.DocumentType is not null),
        ("primaryPosition", content => content.PrimaryPosition is not null),
        ("secondaryPosition", content => content.SecondaryPosition is not null),
        ("icao", content => content.Icao is not null),
        ("fir", content => content.Fir is not null),
        ("effectiveOn", content => content.EffectiveOn is not null),
        ("reviewOn", content => content.ReviewOn is not null),
        ("retiredAt", content => content.RetiredAt is not null),
        ("supersededById", content => content.SupersededById is not null),
    ];

    public ContentWriteDtoValidator(
        BlockDocumentWalker walker,
        BlockRegistry blocks,
        IFirDirectory airspace,
        HubDbContext database)
    {
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(airspace);
        ArgumentNullException.ThrowIfNull(database);

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

        // ---- the operational document (G14) ----------------------------------------------------

        // A page with a review date would be a page the reminder job writes to. The fields belong
        // to a document; on anything else each one that is set is refused where it was written.
        RuleFor(content => content).Custom((content, context) =>
        {
            if (content.Kind == ContentKind.Document)
            {
                return;
            }

            foreach (var (field, isSet) in DocumentOnly)
            {
                if (isSet(content))
                {
                    context.AddFailure(new FluentValidation.Results.ValidationFailure(field, "errors.content.notADocument"));
                }
            }
        });

        RuleFor(content => content.PrimaryPosition)
            .Must(position => position is null || PositionPattern().IsMatch(position))
            .WithMessage("errors.content.positionInvalid");

        RuleFor(content => content.SecondaryPosition)
            .Must(position => position is null || PositionPattern().IsMatch(position))
            .WithMessage("errors.content.positionInvalid");

        // Chosen from the snapshot, never typed: the division's own airports and centres are the
        // whole of what a document may be about, and the list the form offers is read from the
        // same cache — so a refusal here means the client made something up (implementation plan,
        // G14, "una casella in cui un errore di battitura non si vede").
        RuleFor(content => content.Icao)
            .MustAsync(async (icao, cancellationToken) =>
                icao is null || (await airspace.GetAirspaceAsync(cancellationToken)).Airports.Contains(icao))
            .WithMessage("errors.content.icaoUnknown");

        RuleFor(content => content.Fir)
            .MustAsync(async (fir, cancellationToken) =>
                fir is null || (await airspace.GetAirspaceAsync(cancellationToken)).Centers.Contains(fir))
            .WithMessage("errors.content.firUnknown");

        // A successor is a document that exists, and one somebody can be sent to: a template is
        // not an address. Read past the query filter on purpose — the successor may well belong to
        // another department, and the reader is sent there by the public site, not by this user.
        RuleFor(content => content.SupersededById)
            .MustAsync(async (id, cancellationToken) =>
                id is null || await CrudSource.BackOffice<ContentEntry>(database)
                    .AnyAsync(row => row.Id == id && row.Kind == ContentKind.Document && !row.IsTemplate, cancellationToken))
            .WithMessage("errors.content.successorUnknown");

        // Superseded is a way of being retired: the notice on the public page says "no longer in
        // force, read this one instead", and it cannot say the second half without the first.
        RuleFor(content => content.RetiredAt)
            .NotNull()
            .When(content => content.SupersededById is not null)
            .WithMessage("errors.content.successorWithoutRetirement");
    }

    /// <summary>
    /// What may appear in an address. Lower case, digits and single dashes: the slug is part of a
    /// URL that outlives the page, and a fork writing in another alphabet still gets one it can
    /// type. The editor proposes one from the title; this is what it is held to.
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    /// <summary>
    /// A callsign as the network writes it: upper case, digits and underscores, <c>LIRF_TWR</c> or
    /// <c>LIRR_N_CTR</c>. Suggested from the ICAO and the FIR chosen, and held to this when typed,
    /// because a division has positions no list knows of yet.
    /// </summary>
    [GeneratedRegex("^[A-Z0-9_]{3,16}$")]
    private static partial Regex PositionPattern();
}
