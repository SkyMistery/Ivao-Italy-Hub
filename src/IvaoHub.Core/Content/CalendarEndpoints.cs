using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Content;

/// <summary>
/// The one calendar of the division, exposed by the generic CRUD engine (plan section 9.5). Like
/// the links, this file is the whole back end of the resource.
/// <para>Two kinds of row live in the same table and are read the same way: the entries the staff
/// writes here, and the entries a module projects from its own rows. Which module wrote one is not
/// something a reader has to care about — but it is something a <b>writer</b> does, and the one
/// line that says so is <see cref="CrudOptions{TEntity, TListDto, TDetailDto, TWriteDto}.ReadOnlyRows"/>
/// (design M1 section 4).</para>
/// </summary>
public static class CalendarEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/calendar";

    public static RouteGroupBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new CalendarMapper();

        return app.MapCrud<CalendarEntry, CalendarListDto, CalendarDetailDto, CalendarWriteDto>(
            Pattern,
            options =>
            {
                options.PermissionArea = CorePermissions.CalendarArea;

                // A calendar reads forwards, so the soonest entry is the first row.
                options.DefaultOrder = entry => entry.StartsAtUtc;

                options.Sortable.Add(nameof(CalendarEntry.StartsAtUtc));
                options.Sortable.Add(nameof(CalendarEntry.Kind));
                options.Sortable.Add(nameof(CalendarEntry.UpdatedAt));

                options.Filterable.Add(nameof(CalendarEntry.OwnerDepartment));
                options.Filterable.Add(nameof(CalendarEntry.Visibility));
                options.Filterable.Add(nameof(CalendarEntry.Kind));
                options.Filterable.Add(nameof(CalendarEntry.SourceModule));

                options.SearchFields.Add(entry => entry.Title);
                options.SearchFields.Add(entry => entry.Kind);

                // An entry a module owns is a mirror of that module's row: it is shown, and it is
                // not editable by anybody, because a change to it would be undone at the next save
                // of the thing it mirrors. The entity is what decides which rows those are.
                options.ReadOnlyRows = entry => entry.IsProjection;

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;

                options.Apply = (payload, entry) =>
                {
                    mapper.Apply(payload, entry);

                    // A row born here belongs to the core and needs a source identifier of its own,
                    // because `(source_module, source_id)` is unique and every staff entry would
                    // otherwise be ("core", ""). It is opaque and generated, like the name a file of
                    // the library gets: nothing reads it but the index.
                    //
                    // Only when it is empty, which is only ever on a create: regenerating it on
                    // every save would change a row's identity for no reason.
                    if (string.IsNullOrEmpty(entry.SourceId))
                    {
                        entry.SourceModule = ProjectionSource.Core;
                        entry.SourceId = $"staff:{Guid.NewGuid():N}";
                    }
                };
            });
    }
}
