using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Auth;

/// <summary>
/// Handing a permission to one member by name, and the small list of who administers the system.
/// <para>This is the first resource of the hub with <b>no department at all</b>, and therefore the
/// first real use of the CRUD engine's global mode: one policy on the endpoint, no narrowing of the
/// list, no row level question to ask, because there is no owner to compare anybody against
/// (design M0 section 3.9). Everything else — paging, sorting, searching, validation, optimistic
/// concurrency — is the engine's, exactly as it is for links.</para>
/// <para>What makes a grant bite immediately is not written here either: <c>UserGrant</c> is
/// <c>IAffectsUserSession</c>, so the save changes interceptor gives its holder a fresh
/// <c>security_stamp</c> in the same transaction and the cookie they are carrying is refused on
/// their very next request.</para>
/// </summary>
public static class GrantEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/admin/grants";

    /// <summary>Where the super administrators are listed and changed.</summary>
    public const string SuperadminPattern = "/api/admin/superadmins";

    public static RouteGroupBuilder MapGrantEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new GrantMapper();

        return app.MapCrud<UserGrant, GrantListDto, GrantDetailDto, GrantWriteDto>(Pattern, options =>
        {
            // No department to scope to, so the policy is named rather than derived from an area.
            // Reading who holds what is as sensitive as changing it: there is one permission.
            options.PermissionArea = "Grants";
            options.ReadPolicy = CorePermissions.PermissionsManage;
            options.WritePolicy = CorePermissions.PermissionsManage;

            options.DefaultOrder = grant => grant.Vid;

            options.Sortable.Add(nameof(UserGrant.Vid));
            options.Sortable.Add(nameof(UserGrant.Value));
            options.Sortable.Add(nameof(UserGrant.Effect));
            options.Sortable.Add(nameof(UserGrant.UpdatedAt));

            options.Filterable.Add(nameof(UserGrant.Vid));
            options.Filterable.Add(nameof(UserGrant.Value));
            options.Filterable.Add(nameof(UserGrant.Department));
            options.Filterable.Add(nameof(UserGrant.Effect));

            options.SearchFields.Add(grant => grant.Value);
            options.SearchFields.Add(grant => grant.Reason);

            options.ToList = mapper.ToList;
            options.ToDetail = mapper.ToDetail;
            options.Apply = mapper.Apply;
        });
    }

    /// <summary>
    /// Who may bypass every policy. Only a super administrator sees this list or changes it: the
    /// permission catalogue has nothing above <c>Permissions.Manage</c>, and it deliberately does
    /// not, because a permission that could hand out the bypass would make the bypass ordinary.
    /// </summary>
    public static RouteGroupBuilder MapSuperadminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(SuperadminPattern)
            .WithTags("Superadmins")
            .RequireAuthorization(HubPolicies.SignedIn);

        group.MapGet("/", async Task<Results<Ok<IReadOnlyList<int>>, ForbidHttpResult>> (
            ICurrentUser user,
            SuperadminService superadmins,
            CancellationToken cancellationToken) =>
            user.IsSuperadmin
                ? TypedResults.Ok<IReadOnlyList<int>>(await superadmins.ListAsync(cancellationToken))
                : TypedResults.Forbid())
            .WithName("SuperadminsList");

        group.MapPost("/{vid:int}", async Task<Results<NoContent, ForbidHttpResult>> (
            int vid,
            ICurrentUser user,
            SuperadminService superadmins,
            CancellationToken cancellationToken) =>
        {
            if (!user.IsSuperadmin)
            {
                return TypedResults.Forbid();
            }

            // A VID nobody has ever seen is refused by the service itself, as a
            // DomainRefusalException the exception handler turns into the same 400 with an i18n key
            // per field that a validator produces. There is nothing to catch here.
            await superadmins.AddAsync(vid, cancellationToken);
            return TypedResults.NoContent();
        })
            .WithName("SuperadminsAdd")
            .ProducesValidationProblem();

        group.MapDelete("/{vid:int}", async Task<Results<NoContent, ForbidHttpResult>> (
            int vid,
            ICurrentUser user,
            SuperadminService superadmins,
            CancellationToken cancellationToken) =>
        {
            if (!user.IsSuperadmin)
            {
                return TypedResults.Forbid();
            }

            // Likewise for the last one, which cannot be removed.
            await superadmins.RemoveAsync(vid, cancellationToken);
            return TypedResults.NoContent();
        })
            .WithName("SuperadminsRemove")
            .ProducesValidationProblem();

        return group;
    }
}
