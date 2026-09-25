using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Core.Privacy;

/// <summary>
/// Erasing a person's data: what it would do, and doing it (note <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c>).
/// Only a super administrator is answered, as for the list of super administrators: the permission catalogue has nothing
/// that could hand this out, and an action that cannot be undone is not something to hand out.
/// </summary>
public static class ErasureEndpoints
{
    public const string Pattern = "/api/admin/erasure";

    public static RouteGroupBuilder MapErasureEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup(Pattern)
            .WithTags("Erasure")
            .RequireAuthorization(HubPolicies.SignedIn);

        group.MapGet("/{vid:int}", async Task<Results<Ok<ErasurePreviewDto>, ForbidHttpResult>> (
            int vid,
            ICurrentUser user,
            PersonalDataErasure erasure,
            CancellationToken cancellationToken) =>
            user.IsSuperadmin
                ? TypedResults.Ok(await erasure.PreviewAsync(vid, cancellationToken))
                : TypedResults.Forbid())
            .WithName("ErasurePreview")
            .ProducesValidationProblem();

        group.MapPost("/{vid:int}", async Task<Results<Ok<ErasureResultDto>, ForbidHttpResult>> (
            int vid,
            ICurrentUser user,
            PersonalDataErasure erasure,
            CancellationToken cancellationToken) =>
            user.IsSuperadmin
                ? TypedResults.Ok(await erasure.EraseAsync(vid, cancellationToken))
                : TypedResults.Forbid())
            .WithName("ErasureErase")
            .ProducesValidationProblem();

        return group;
    }
}
