using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The rules a menu entry has to satisfy before it is written. Messages are i18n keys, never
/// sentences (design M0 sections 3.9 and 7.5).
/// </summary>
public sealed class MenuItemWriteDtoValidator : AbstractValidator<MenuItemWriteDto>
{
    /// <summary>Longest path the column holds; an address of this site or of somewhere else.</summary>
    public const int MaxPathLength = 512;

    public MenuItemWriteDtoValidator(IOptions<DivisionOptions> division, HubDbContext database)
    {
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(database);

        // A menu entry is read by every visitor, so it exists in every language of the division;
        // which ones are missing travels with the failure.
        RuleFor(item => item.Label).Required(division.Value);

        RuleFor(item => item.Path)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxPathLength).WithMessage("errors.text.tooLong")
            .Must(BeAPathOrAWebAddress).WithMessage("errors.path.invalid");

        RuleFor(item => item.Sort)
            .GreaterThanOrEqualTo(0).WithMessage("errors.number.min");

        // Depth one, checked where the answer is: the parent has to exist, sit in the same menu,
        // and be a top level entry itself. Asked of the database because it is the only thing that
        // knows, and refused here rather than flattened later — a menu that silently redraws what
        // was saved is a menu nobody can reason about.
        //
        // Read through the query filter and not around it, which is also the stricter reading: an
        // entry nobody may see is not an entry to hang something under.
        RuleFor(item => item.ParentId)
            .MustAsync(async (payload, parentId, cancellation) =>
                parentId is not { } id
                || await database.MenuItems
                    .AnyAsync(
                        parent => parent.Id == id
                            && parent.ParentId == null
                            && parent.Scope == payload.Scope,
                        cancellation))
            .WithMessage("errors.menu.parentInvalid");
    }

    /// <summary>
    /// A menu leads either somewhere on this site — a path, which the client follows with the
    /// router — or somewhere else entirely, which needs a scheme a browser will follow. Anything
    /// in between, a <c>javascript:</c> URL included, is refused here rather than sanitised later.
    /// </summary>
    private static bool BeAPathOrAWebAddress(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith('/'))
        {
            // "//elsewhere.example" is a protocol relative address and leaves the site while
            // looking like a path, which is the one thing a leading slash must not be able to hide.
            return !path.StartsWith("//", StringComparison.Ordinal);
        }

        return Uri.TryCreate(path, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }
}
