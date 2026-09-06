using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Content;

/// <summary>
/// The vocabulary a department files its news and its documents under, exposed by the generic CRUD
/// engine. Like the links, this file is the whole back end of the resource.
/// <para>It sits in the <c>Content</c> permission area rather than one of its own, and that is a
/// decision and not an omission: the vocabulary is part of writing content, whoever may write the
/// news of a department may name the shelves those news sit on, and design M1 section 10.1 lists
/// the three new areas of M1 without one for this. A fourth area would be a permission to hand out
/// separately for a thing nobody manages separately.</para>
/// </summary>
public static class CategoriesEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/categories";

    public static RouteGroupBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new CategoryMapper();

        return app.MapCrud<ContentCategory, CategoryListDto, CategoryDetailDto, CategoryWriteDto>(
            Pattern,
            options =>
            {
                options.PermissionArea = CorePermissions.ContentArea;

                // Two resources, one area: the contract still needs two names, or both would answer
                // to `ContentList` and the generated client would keep only one of them.
                options.Name = "Categories";

                // The order a grouped list shows them in, so the back office and the public site
                // read the same way round.
                options.DefaultOrder = category => category.Sort;

                options.Sortable.Add(nameof(ContentCategory.Key));
                options.Sortable.Add(nameof(ContentCategory.Kind));
                options.Sortable.Add(nameof(ContentCategory.Sort));
                options.Sortable.Add(nameof(ContentCategory.UpdatedAt));

                options.Filterable.Add(nameof(ContentCategory.Kind));
                options.Filterable.Add(nameof(ContentCategory.OwnerDepartment));
                options.Filterable.Add(nameof(ContentCategory.IsActive));

                options.SearchFields.Add(category => category.Key);
                options.SearchFields.Add(category => category.Label);

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
            });
    }
}
