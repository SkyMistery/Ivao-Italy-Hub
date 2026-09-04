using System.Text;
using System.Text.Json.Nodes;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

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

        var group = app.MapCrud<ContentEntry, ContentListDto, ContentDetailDto, ContentWriteDto>(
            Pattern,
            options =>
            {
                options.PermissionArea = ContentArea;

                options.DefaultOrder = content => content.Slug;

                options.Sortable.Add(nameof(ContentEntry.Slug));
                options.Sortable.Add(nameof(ContentEntry.Kind));
                options.Sortable.Add(nameof(ContentEntry.Status));
                options.Sortable.Add(nameof(ContentEntry.UpdatedAt));
                options.Sortable.Add(nameof(ContentEntry.PublishedAt));

                options.Filterable.Add(nameof(ContentEntry.Kind));
                options.Filterable.Add(nameof(ContentEntry.OwnerDepartment));
                options.Filterable.Add(nameof(ContentEntry.Visibility));
                options.Filterable.Add(nameof(ContentEntry.Status));
                options.Filterable.Add(nameof(ContentEntry.IsTemplate));
                options.Filterable.Add(nameof(ContentEntry.TemplateId));

                // A template is a tool, not a page: it stays out of the list of what a department
                // publishes, and `filter[isTemplate]=true` is how the template picker asks for it.
                options.DefaultFilters[nameof(ContentEntry.IsTemplate)] = "false";

                options.SearchFields.Add(content => content.Title);
                options.SearchFields.Add(content => content.Slug);

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

                var resolved = await provider.ResolveAsync(decoded, http.RequestAborted);
                return Results.Ok(resolved);
            })
            .WithName("BlockData")
            .WithTags(ContentArea)
            .Produces<JsonNode>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }

    private const string ContentArea = "Content";

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
        Reidentify(walker, body);

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

        return TypedResults.Ok(new PublicContentDto(
            content.Kind,
            content.Slug,
            version.Title,
            content.Summary,
            content.Seo,
            JsonNode.Parse(version.BodyJson) ?? new JsonObject(),
            version.SchemaVersion,
            version.Version,
            version.PublishedAt));
    }

    /// <summary>
    /// A deep copy is only a copy if nothing in it still answers to the old name: every section and
    /// block gets a fresh identifier, any capture from the template is dropped, and the keys only a
    /// template may carry are left behind — a page holding them would be able to lift its own
    /// restrictions, which is exactly what the envelope validator refuses (design M0 section 5.2).
    /// </summary>
    private static void Reidentify(BlockDocumentWalker walker, JsonNode body)
    {
        foreach (var section in walker.EnumerateSections(body))
        {
            section.Node["id"] = NewId("s");
            section.Node.Remove("required");
            section.Node.Remove("locked");
            section.Node.Remove("allowedBlocks");
        }

        foreach (var block in walker.EnumerateBlocks(body))
        {
            block.Node["id"] = NewId("b");
            block.Node["frozen"] = null;
        }
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..10];

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
