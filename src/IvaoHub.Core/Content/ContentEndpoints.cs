using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Content;

/// <summary>What "new from template" needs to know that the template does not say.</summary>
public sealed record ContentFromTemplateRequest(Department OwnerDepartment, string Slug);

/// <summary>
/// Editorial content: the generic CRUD engine for the back office, plus the three things a page can
/// do that a link cannot — be born from a template, be published, and be read by a visitor.
/// <para>Everything about paging, filtering, department narrowing and row level authorisation is
/// the engine's; what is written here is only what publication means (design M0 section 5.5).</para>
/// </summary>
public static class ContentEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/content";

    /// <summary>Where a live data block is resolved from, for the reader looking at the page.</summary>
    public const string BlockDataPattern = "/api/blocks/data/{type}";

    public static RouteGroupBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new ContentMapper();
        var clock = app.ServiceProvider.GetRequiredService<IClock>();

        var group = app.MapCrud<ContentEntry, ContentListDto, ContentDetailDto, ContentWriteDto>(
            Pattern,
            options =>
            {
                options.PermissionArea = CorePermissions.ContentArea;

                options.DefaultOrder = content => content.Slug;

                options.Sortable.Add(nameof(ContentEntry.Slug));
                options.Sortable.Add(nameof(ContentEntry.Kind));
                options.Sortable.Add(nameof(ContentEntry.Status));
                options.Sortable.Add(nameof(ContentEntry.UpdatedAt));
                options.Sortable.Add(nameof(ContentEntry.PublishedAt));
                options.Sortable.Add(nameof(ContentEntry.ReviewOn));

                options.Filterable.Add(nameof(ContentEntry.Kind));
                options.Filterable.Add(nameof(ContentEntry.OwnerDepartment));
                options.Filterable.Add(nameof(ContentEntry.Visibility));
                options.Filterable.Add(nameof(ContentEntry.Status));
                options.Filterable.Add(nameof(ContentEntry.IsTemplate));
                options.Filterable.Add(nameof(ContentEntry.TemplateId));
                options.Filterable.Add(nameof(ContentEntry.Category));

                // A template is a tool, not a page: it stays out of the list of what a department
                // publishes, and `filter[isTemplate]=true` is how the template picker asks for it.
                options.DefaultFilters[nameof(ContentEntry.IsTemplate)] = "false";

                // "Which pages use this file?", asked of the resource that owns the answer rather
                // than through an endpoint of its own. It is not an equality on a column — the
                // identifier is somewhere inside an opaque body — so it is a function, and the
                // function is the one helper of Core/Data that knows how to ask a JSON column.
                options.CustomFilters[UsesMediaFilter] = (query, raw) =>
                    long.TryParse(raw, CultureInfo.InvariantCulture, out var mediaId)
                        ? query.UsingMedia(mediaId)
                        : null;

                // "Which documents are due for a look?" (G14): a date compared with today, which
                // no equality filter can say. The clock is the host's, read once here.
                options.CustomFilters[ReviewDueFilter] = (query, raw) =>
                    bool.TryParse(raw, out var due)
                        ? due
                            ? query.Where(content => content.ReviewOn != null
                                && content.ReviewOn <= clock.UtcNow.Date
                                && content.RetiredAt == null)
                            : query
                        : null;

                options.SearchFields.Add(content => content.Title);
                options.SearchFields.Add(content => content.Slug);

                // A template is a tool of a department and read by every department, so that "new
                // from a template" exists outside the one that made it (design M1 section 9.4). The
                // expression is the entity's own, and the handler asks the row the same question.
                options.SharedForReading = ContentEntry.SharedForReading;

                // The one extension point of the engine, and the reason it exists: editing a
                // template needs a permission of its own (design M0 section 5.7).
                options.ExtraWritePolicy = content =>
                    content.IsTemplate ? CorePermissions.ContentManageTemplates : null;

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
            });

        group.MapPost("/from-template/{templateId:long}", CreateFromTemplateAsync)
            .WithName("ContentCreateFromTemplate")
            .Produces<ContentDetailDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(CorePermissions.ContentEdit);

        group.MapPost("/{id:long}/publish", PublishAsync)
            .WithName("ContentPublish")
            .Produces<ContentDetailDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(CorePermissions.ContentPublish);

        // ⚠️ A hand written verb hanging off the CRUD group, and the fourth of M1 — the plan asks
        // for each of them to be justified. This one exists because the alternative was the client
        // working out what publication would refuse, which is the rules of publication written a
        // second time (plan §16.E, rule (b)). It runs the very same checks and writes nothing.
        group.MapGet("/{id:long}/publish-problems", PublishProblemsAsync)
            .WithName("ContentPublishProblems")
            .Produces<ContentPublishProblemsDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(CorePermissions.ContentPublish);

        group.MapGet("/public/{kind}/{slug}", ReadPublicAsync)
            .WithName("ContentPublicRead")
            .Produces<PublicContentDto>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return group;
    }

    /// <summary>
    /// One data block, resolved for whoever is asking. It is the same provider and the same
    /// visibility rules publication uses; the difference is only when the question is asked
    /// (design M0 section 5.5).
    /// </summary>
    public static void MapBlockDataEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(BlockDataPattern, async (
                string type,
                string? props,
                BlockRegistry blocks,
                DataBlockProviders providers,
                HttpContext http) =>
            {
                var descriptor = blocks.Find(type);
                if (descriptor is null || providers.For(descriptor) is not { } provider)
                {
                    return Results.NotFound();
                }

                if (!TryDecodeProps(props, out var decoded))
                {
                    return Results.BadRequest(new { props = "errors.body.notAnObject" });
                }

                // Read live, by whoever is asking: the query filter has already had the last word,
                // and there is no page keeping the answer afterwards.
                var resolved = await provider.ResolveAsync(decoded, DataBlockContext.Reader, http.RequestAborted);
                return Results.Ok(resolved);
            })
            .WithName("BlockData")
            .WithTags(CorePermissions.ContentArea)
            .Produces<JsonNode>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }

    /// <summary>
    /// <c>filter[usesMedia]=42</c>: the contents that show a file. Named here because the client
    /// writes it too, and a filter name spelled twice is a filter name that drifts.
    /// </summary>
    public const string UsesMediaFilter = "usesMedia";

    /// <summary><c>filter[reviewDue]=true</c>: the documents whose review date has passed (G14).</summary>
    public const string ReviewDueFilter = "reviewDue";

    private static async Task<IResult> CreateFromTemplateAsync(
        long templateId,
        ContentFromTemplateRequest request,
        HubDbContext database,
        ContentPublishService content,
        BlockDocumentWalker walker,
        IValidator<ContentWriteDto> validator,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var template = await content.FindAsync(templateId, http.RequestAborted);
        if (template is null || !template.IsTemplate)
        {
            return Results.NotFound();
        }

        // Reading the template needs the permission to read content; making a page from it needs
        // the permission to write in the department the page will belong to. Neither is
        // ManageTemplates: a coordinator may use a template without being allowed to change one.
        if (!(await authorization.AuthorizeAsync(http.User, template, CorePermissions.ContentView)).Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey));
        }

        var body = JsonNode.Parse(template.BodyJson) ?? new JsonObject();
        TemplateCopy.Reidentify(walker, body);

        var payload = new ContentWriteDto(
            template.Kind,
            request.Slug,
            request.OwnerDepartment,
            Visibility.Staff,
            IsTemplate: false,
            template.Title,
            template.Summary,
            template.Seo,
            body,
            template.SchemaVersion,
            // A template carries structure, never the editorial facts of one row: a page born from
            // one starts with no category, no cover, unpinned, first in order and no file — and a
            // document with none of its dates, which the form asks for next.
            Category: null,
            CoverMediaId: null,
            Pinned: false,
            Sort: 0,
            FileMediaId: null,
            EffectiveOn: null,
            ReviewOn: null,
            RetiredAt: null,
            SupersededById: null,
            RowVersion: default);

        var validation = await validator.ValidateAsync(payload, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        var page = new ContentEntry
        {
            TemplateId = template.Id,
            Status = PublishStatus.Draft,
        };

        new ContentMapper().Apply(payload, page);

        if (!(await authorization.AuthorizeAsync(http.User, page, CorePermissions.ContentEdit)).Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey));
        }

        database.Contents.Add(page);
        await database.SaveChangesAsync(http.RequestAborted);

        return Results.Created($"{Pattern}/{page.Id}", new ContentMapper().ToDetail(page));
    }

    private static async Task<IResult> PublishAsync(
        long id,
        ContentPublishRequest? request,
        ContentPublishService publish,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var content = await publish.FindAsync(id, http.RequestAborted);
        if (content is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.NotFoundTitleKey));
        }

        if (!(await authorization.AuthorizeAsync(http.User, content, CorePermissions.ContentPublish)).Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey));
        }

        var failure = await publish.PublishAsync(content, request?.Changelog, http.RequestAborted);
        if (failure is not null)
        {
            return CrudProblems.Validation(failure.Errors, failure.MissingLocales, catalog, currentUser.Locale);
        }

        return Results.Ok(new ContentMapper().ToDetail(content));
    }

    /// <summary>
    /// The same answer publication would give, without publishing. Behind the same permission and
    /// the same per row check as publishing itself: what is missing from a page is only somebody's
    /// business if they could have published it.
    /// </summary>
    private static async Task<IResult> PublishProblemsAsync(
        long id,
        ContentPublishService publish,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var content = await publish.FindAsync(id, http.RequestAborted);
        if (content is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.NotFoundTitleKey));
        }

        if (!(await authorization.AuthorizeAsync(http.User, content, CorePermissions.ContentPublish)).Succeeded)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ForbiddenTitleKey));
        }

        var problems = await publish.ProblemsAsync(content, http.RequestAborted);

        return Results.Ok(new ContentPublishProblemsDto(
            problems?.Errors ?? EmptyProblems,
            problems?.MissingLocales ?? EmptyProblems));
    }

    /// <summary>Nothing in the way, said once rather than allocated per request.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> EmptyProblems =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>
    /// What a visitor reads. Two things keep a draft out of it: the query filter, which is on here
    /// because this is a public read and not the back office, and the fact that the body served is
    /// the published version's and never the row's own.
    /// </summary>
    private static async Task<Results<Ok<PublicContentDto>, NotFound>> ReadPublicAsync(
        ContentKind kind,
        string slug,
        HubDbContext database,
        ContentPublishService publish,
        HttpContext http)
    {
        var content = await database.Contents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Kind == kind && row.Slug == slug && !row.IsTemplate,
                http.RequestAborted);

        if (content is null)
        {
            return TypedResults.NotFound();
        }

        var version = await publish.PublishedVersionAsync(content, http.RequestAborted);
        if (version is null)
        {
            return TypedResults.NotFound();
        }

        // What the footer and the notice of a document say about people and other rows, read here
        // and given as words: the visitor gets a name and an address, never a VID or an identifier.
        var publishedBy = await database.Users
            .AsNoTracking()
            .Where(user => user.Vid == version.PublishedBy)
            .Select(user => user.FirstName + " " + user.LastName)
            .FirstOrDefaultAsync(http.RequestAborted);

        var successor = content.SupersededById is { } successorId
            ? await database.Contents
                .AsNoTracking()
                .Where(row => row.Id == successorId && row.PublishedVersionId != null)
                .Select(row => new { row.Slug, row.Title })
                .FirstOrDefaultAsync(http.RequestAborted)
            : null;

        return TypedResults.Ok(new PublicContentDto(
            content.Id,
            content.Kind,
            content.Slug,
            content.OwnerDepartment,
            version.Title,
            content.Summary,
            content.Seo,
            JsonNode.Parse(version.BodyJson) ?? new JsonObject(),
            version.SchemaVersion,
            content.Category,
            content.CoverMediaId,
            content.FileMediaId,
            version.Version,
            version.PublishedAt,
            content.EffectiveOn,
            content.ReviewOn,
            content.RetiredAt,
            successor?.Slug,
            successor?.Title,
            content.ShowFooter,
            string.IsNullOrWhiteSpace(publishedBy) ? null : publishedBy.Trim()));
    }

    /// <summary>
    /// The properties of a live block travel base64 encoded in the query string: they are an opaque
    /// JSON object and would otherwise need escaping rules of their own. Absent means "no
    /// properties", which is a block that takes none.
    /// <para>Base64url is what the client sends, because plain base64 carries <c>+</c> and a query
    /// string reads that as a space. Both alphabets are accepted here: the padding and the two
    /// characters are the whole difference, and refusing one of them would only ever be a way of
    /// failing on a caller that is not our own client.</para>
    /// </summary>
    private static bool TryDecodeProps(string? encoded, out JsonNode? props)
    {
        props = null;

        if (string.IsNullOrWhiteSpace(encoded))
        {
            return true;
        }

        var normalized = encoded.Replace('-', '+').Replace('_', '/').Replace(' ', '+');
        normalized = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');

        try
        {
            props = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(normalized)));
            return props is JsonObject;
        }
        catch (Exception exception) when (exception is FormatException or System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
