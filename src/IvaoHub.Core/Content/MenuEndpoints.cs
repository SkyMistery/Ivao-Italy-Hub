using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Content;

/// <summary>
/// The site menu, exposed by the generic CRUD engine. Like the links and the categories, this file
/// is the whole back end of the resource: no controller, no hand written paging, no rule about who
/// may touch a row (design M1 section 8.1).
/// <para>Who may touch one is decided entirely by <see cref="MenuItem.Owner"/>: every row belongs
/// to the web team, so the department filter of the list and the single authorization handler give
/// the answer without a line being written for it. That is why the area is <c>Menu</c> and not
/// <c>Content</c> — a division hands the site navigation to the web team and its news to nine
/// departments, and those are two permissions.</para>
/// </summary>
public static class MenuEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/menu";

    public static RouteGroupBuilder MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new MenuItemMapper();

        return app.MapCrud<MenuItem, MenuItemListDto, MenuItemDetailDto, MenuItemWriteDto>(
            Pattern,
            options =>
            {
                options.PermissionArea = CorePermissions.MenuArea;

                // The order the site draws them in, so the back office reads the way the menu does.
                options.DefaultOrder = item => item.Sort;

                options.Sortable.Add(nameof(MenuItem.Sort));
                options.Sortable.Add(nameof(MenuItem.Scope));
                options.Sortable.Add(nameof(MenuItem.Path));
                options.Sortable.Add(nameof(MenuItem.UpdatedAt));

                options.Filterable.Add(nameof(MenuItem.Scope));
                options.Filterable.Add(nameof(MenuItem.ParentId));
                options.Filterable.Add(nameof(MenuItem.Visibility));
                options.Filterable.Add(nameof(MenuItem.IsActive));
                options.Filterable.Add(nameof(MenuItem.OwnerDepartment));

                options.SearchFields.Add(item => item.Label);
                options.SearchFields.Add(item => item.Path);

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
            });
    }
}
