using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Content;

/// <summary>
/// The links of the division, exposed by the generic CRUD engine. This file is the whole back end
/// of the resource: no controller, no repository, no paging, no authorisation written here — which
/// is exactly what M0 set out to prove on its guinea pig entity (plan section 16.15).
/// </summary>
public static class LinksEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/links";

    public static RouteGroupBuilder MapLinksEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new LinkMapper();

        return app.MapCrud<Link, LinkListDto, LinkDetailDto, LinkWriteDto>(Pattern, options =>
        {
            // Links.View to read, Links.Edit to write, both scoped to the department of the row.
            options.PermissionArea = CorePermissions.LinksArea;

            // Departments order their own links by hand, so that is the order of the list.
            options.DefaultOrder = link => link.Sort;

            options.Sortable.Add(nameof(Link.Sort));
            options.Sortable.Add(nameof(Link.Url));
            options.Sortable.Add(nameof(Link.Category));
            options.Sortable.Add(nameof(Link.UpdatedAt));

            options.Filterable.Add(nameof(Link.OwnerDepartment));
            options.Filterable.Add(nameof(Link.Visibility));
            options.Filterable.Add(nameof(Link.Category));
            options.Filterable.Add(nameof(Link.IsActive));

            // The title is searched in the language of the reader; the address as it is written.
            options.SearchFields.Add(link => link.Title);
            options.SearchFields.Add(link => link.Url);

            options.ToList = mapper.ToList;
            options.ToDetail = mapper.ToDetail;
            options.Apply = mapper.Apply;
        });
    }
}
