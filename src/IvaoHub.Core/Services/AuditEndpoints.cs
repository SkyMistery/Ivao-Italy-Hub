using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Routing;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Core.Services;

/// <summary>One row of the audit log, as the list shows it. The before and after stay out.</summary>
public sealed record AuditListDto(
    long Id,
    int Vid,
    string Action,
    string Entity,
    string EntityId,
    bool IsSuperadmin,
    string? Ip,
    DateTime At);

/// <summary>
/// One row in full. <c>BeforeJson</c> and <c>AfterJson</c> are the scalar columns as they were and
/// as they became, exactly as the interceptor wrote them: they travel as text, because what they
/// contain depends on the entity and the hub does not model it.
/// </summary>
public sealed record AuditDetailDto(
    long Id,
    int Vid,
    string Action,
    string Entity,
    string EntityId,
    string? BeforeJson,
    string? AfterJson,
    bool IsSuperadmin,
    string? Ip,
    DateTime At);

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class AuditMapper
{
    public partial AuditListDto ToList(AuditLogEntry entry);

    public partial AuditDetailDto ToDetail(AuditLogEntry entry);
}

/// <summary>
/// Reading the audit log. The second resource of the hub with no department, and the only one that
/// is read only: <c>ReadOnly = true</c> maps the two reads and nothing else, so there is no way for
/// a caller — or for a later mistake in this file — to write into the record of what happened
/// (design M0 section 3.9).
/// <para>The rows themselves are written by the save changes interceptor and by nobody else.</para>
/// </summary>
public static class AuditEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/admin/audit";

    public static RouteGroupBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new AuditMapper();

        return app.MapCrud<AuditLogEntry, AuditListDto, AuditDetailDto, AuditDetailDto>(Pattern, options =>
        {
            options.PermissionArea = "Audit";
            options.ReadPolicy = CorePermissions.AuditView;
            options.WritePolicy = CorePermissions.AuditView;
            options.ReadOnly = true;

            // By identifier, which is chronological and unlike the timestamp is unique: two rows of
            // the same save share an instant. An audit log is read newest first, and the screen
            // asks for that with `dir=desc` — the engine's default direction is ascending and this
            // resource is not a reason to give it a second one.
            options.DefaultOrder = entry => entry.Id;

            options.Sortable.Add(nameof(AuditLogEntry.At));
            options.Sortable.Add(nameof(AuditLogEntry.Vid));
            options.Sortable.Add(nameof(AuditLogEntry.Entity));

            options.Filterable.Add(nameof(AuditLogEntry.Vid));
            options.Filterable.Add(nameof(AuditLogEntry.Entity));
            options.Filterable.Add(nameof(AuditLogEntry.EntityId));
            options.Filterable.Add(nameof(AuditLogEntry.Action));
            options.Filterable.Add(nameof(AuditLogEntry.IsSuperadmin));

            options.SearchFields.Add(entry => entry.Entity);
            options.SearchFields.Add(entry => entry.EntityId);
            options.SearchFields.Add(entry => entry.Action);

            options.ToList = mapper.ToList;
            options.ToDetail = mapper.ToDetail;
        });
    }
}
