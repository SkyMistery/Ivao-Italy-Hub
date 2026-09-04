using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Modules;

/// <summary>What an administrator sets on a module. One switch, and that is the whole screen.</summary>
public sealed record ModuleMaintenanceRequest(bool Maintenance);

/// <summary>
/// Closing a module for maintenance and opening it again.
/// <para>There is deliberately no list endpoint here: which modules exist, whether they are enabled
/// and whether they are closed is part of <c>/api/me</c>, because the client needs it in order to
/// draw itself anyway and a second answer to the same question is a second thing to keep in step
/// (plan section 16.7).</para>
/// </summary>
public static class ModuleAdminEndpoints
{
    /// <summary>Where the switch lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/admin/modules";

    public static RouteGroupBuilder MapModuleAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Pattern).WithTags("Modules");

        group.MapPut("/{key}/maintenance", async Task<Results<NoContent, NotFound>> (
            string key,
            ModuleMaintenanceRequest request,
            ModuleRegistry registry,
            HubDbContext database,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            // A module this build does not have, or one the division switched off: there is nothing
            // to close, and saying so is better than writing a setting nobody will ever read.
            if (registry.Find(key) is not { } module)
            {
                return TypedResults.NotFound();
            }

            // The audit row comes from the interceptor, because DivisionSetting is [Audited].
            await registry.SetMaintenanceAsync(module.Key, request.Maintenance, database, clock, cancellationToken);
            return TypedResults.NoContent();
        })
            .WithName("ModuleSetMaintenance")
            .RequireAuthorization(CorePermissions.ModulesManage);

        return group;
    }
}
