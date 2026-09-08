using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
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
            .Must(BeAPathOrAWebAddress).WithMessage("errors.path.invalid")
            .MustAsync((path, cancellation) => LeadsSomewhereThisSiteOwnsAsync(database, path, cancellation))
            .WithMessage("errors.menu.pathNotAllowed");

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
    /// The addresses of this application that are not rows: they are screens of the single page
    /// application, so no table can be asked about them.
    /// <para>⚠️ The other half is <c>SITE_SCREENS</c> in
    /// <c>web/src/routes/_staff/staff.$dept.menu.$id.tsx</c>, and the two agree **by hand** — a
    /// route of the client is not something the contract can carry. The integration test that posts
    /// a screen the server does not know is what keeps them agreeing, exactly as it does for the
    /// backgrounds of a section.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Screens =
        ["/", "/calendar", "/news", "/documents", "/search", "/contact"];

    /// <summary>
    /// Where a menu entry may lead, and it is a **closed** set (decided by Carmine on 8 September
    /// 2026, while running the demo): a page of this site, a screen of the application, or a link
    /// of the library — and nothing else.
    /// <para>The point of it is not the menu: it is that <b>every address that leaves this site
    /// lives in one table</b>. A menu that could carry any URL is a site with addresses scattered
    /// through it, each of them a thing nobody maintains; with this rule, changing where the forum
    /// lives is one row of <c>cms_links</c> and the menu follows.</para>
    /// <para>A page that is still a draft counts, on purpose: writing the entry before publishing
    /// the page is how a menu is actually built, and the entry can wait, switched off, until the
    /// page is public.</para>
    /// <para>Read past the query filter, like the pictures publication checks: the question is
    /// whether the destination <b>exists</b>, not whether the person writing the entry may see it.
    /// A link of another department is a perfectly good destination for the site's own menu.</para>
    /// </summary>
    private static async Task<bool> LeadsSomewhereThisSiteOwnsAsync(
        HubDbContext database,
        string? path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Screens.Contains(path, StringComparer.Ordinal))
        {
            return true;
        }

        if (path.StartsWith('/'))
        {
            var slug = path[1..];

            return await CrudSource.BackOffice<ContentEntry>(database)
                .AnyAsync(
                    row => !row.IsTemplate && row.Kind == ContentKind.Page && row.Slug == slug,
                    cancellationToken);
        }

        return await CrudSource.BackOffice<Link>(database)
            .AnyAsync(row => row.IsActive && row.Url == path, cancellationToken);
    }

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
