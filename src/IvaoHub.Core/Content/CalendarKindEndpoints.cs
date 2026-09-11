using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Content;

/// <summary>
/// The division's calendar vocabulary, served by the generic CRUD engine. Like the links, this file
/// is the whole back end of the resource.
/// <para>It is the <b>second</b> resource of the hub with no department at all — the grants were the
/// first, in M0 — so it uses the engine's global mode rather than needing anything new: a policy on
/// the endpoint, no narrowing of the list, no row level question, because there is no owner to
/// compare anybody against.</para>
/// <para>The two policies are deliberately different. Reading is <c>Calendar.View</c>: whoever may
/// look at a calendar has to be able to see what its words are, or the select they write an entry
/// with would be empty. Writing is <c>Calendar.ManageKinds</c>, which is global, and a global
/// permission is held by the roles that reach every department — the director, their assistant, the
/// web team — which is the sentence Carmine used: "decided by headquarters, by the web team or from
/// the admin section, and it is the same for everybody".</para>
/// </summary>
public static class CalendarKindEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/calendar-kinds";

    public static RouteGroupBuilder MapCalendarKindEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new CalendarKindMapper();

        return app.MapCrud<CalendarKind, CalendarKindListDto, CalendarKindDetailDto, CalendarKindWriteDto>(
            Pattern,
            options =>
            {
                // No department to scope to, so the policies are named rather than derived from an
                // area — the shape `GrantEndpoints` established.
                options.PermissionArea = CorePermissions.CalendarArea;
                options.Name = "CalendarKinds";
                options.ReadPolicy = CorePermissions.CalendarView;
                options.WritePolicy = CorePermissions.CalendarManageKinds;

                options.DefaultOrder = kind => kind.Sort;

                options.Sortable.Add(nameof(CalendarKind.Key));
                options.Sortable.Add(nameof(CalendarKind.Sort));
                options.Sortable.Add(nameof(CalendarKind.UpdatedAt));

                options.Filterable.Add(nameof(CalendarKind.IsActive));

                options.SearchFields.Add(kind => kind.Key);
                options.SearchFields.Add(kind => kind.Label);

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
            });
    }
}
