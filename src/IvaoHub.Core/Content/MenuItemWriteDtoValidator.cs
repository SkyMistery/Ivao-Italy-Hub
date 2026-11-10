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

    /// <summary>
    /// Longest icon name the column holds. A length and nothing more: which names exist is a list
    /// that lives only in TypeScript, and checking it here would be a second copy of it.
    /// </summary>
    public const int MaxIconLength = 64;

    public MenuItemWriteDtoValidator(
        IOptions<DivisionOptions> division,
        HubDbContext database,
        SiteDestinations destinations)
    {
        ArgumentNullException.ThrowIfNull(division);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(destinations);

        // A menu entry is read by every visitor, so it exists in every language of the division;
        // which ones are missing travels with the failure.
        RuleFor(item => item.Label).Required(division.Value);

        // ⚠️ Everything about the address is skipped for a **heading**: a top level entry of the
        // footer with nothing in its path. The footer is drawn in columns and the word at the top of
        // one leads nowhere, so there is no address to check — and saying so here, once, is what
        // keeps the closed set of destinations (below) exactly as strict as it was for every entry
        // that does lead somewhere.
        //
        // Top level and footer only, and both halves matter: a *child* with no address would be a
        // line nobody can click, and a heading in the bar at the top would be a menu entry that
        // does nothing when pressed.
        RuleFor(item => item.Path)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxPathLength).WithMessage("errors.text.tooLong")
            .Must(BeAPathOrAWebAddress).WithMessage("errors.path.invalid")
            .MustAsync((path, cancellation) => destinations.MenuMayLeadToAsync(path, cancellation))
            .WithMessage("errors.menu.pathNotAllowed")
            .Unless(IsAHeading);

        RuleFor(item => item.Icon)
            .MaximumLength(MaxIconLength).WithMessage("errors.text.tooLong");

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
    /// A column heading of the footer: top level, in the footer menu, leading nowhere. It is the one
    /// entry allowed to have no address, and the shape of the rule is what makes that safe — a child
    /// with no address would be an unclickable line, and a headline in the top bar would be an entry
    /// that does nothing when pressed (decided by Carmine, 10 September 2026).
    /// </summary>
    private static bool IsAHeading(MenuItemWriteDto item) =>
        item.Scope == MenuScope.Footer && item.ParentId is null && string.IsNullOrWhiteSpace(item.Path);

    /// <summary>
    /// A menu leads either somewhere on this site — a path, which the client follows with the
    /// router — or somewhere else entirely, which needs a scheme a browser will follow. Anything
    /// in between, a <c>javascript:</c> URL included, is refused here rather than sanitised later.
    /// <para>It stays in front of the check above as the cheap half: the shape first, the existence
    /// second, so a <c>javascript:</c> URL never reaches a database query.</para>
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
