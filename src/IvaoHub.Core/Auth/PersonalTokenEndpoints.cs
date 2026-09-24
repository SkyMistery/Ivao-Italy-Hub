using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Auth;

/// <summary>Where a personal token stands. Worked out from its dates, never stored.</summary>
public enum PersonalTokenStatus
{
    Active,
    Expired,
    Revoked,
}

/// <summary>One of the member's tokens as the list shows it: never the token, never its hash.</summary>
public sealed record PersonalTokenDto(
    long Id,
    string Name,
    string Audience,
    string Prefix,
    PersonalTokenStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);

/// <summary>A new token: what it is for, what to call it, and for how many days (1 to 90).</summary>
public sealed record PersonalTokenWriteDto(string Name, string Audience, int Days);

/// <summary>The token just created, with its text: the one and only time the hub shows it.</summary>
public sealed record PersonalTokenIssuedDto(PersonalTokenDto Row, string Token);

/// <summary>
/// <c>/me/tokens</c> (M2, T19a, note 2026-09-15-token-personali-e-agente-del-validatore §3.1): the member's own tokens as a
/// personal view of the generic list, and two verbs — create, which shows the token once, and revoke. A member only ever
/// reaches their own rows: somebody else's token is 404, as if it did not exist.
/// </summary>
public static class PersonalTokenEndpoints
{
    public const string Pattern = "/api/me/tokens";

    /// <summary><c>filter[active]=true</c>, the default: the tokens that still open something.</summary>
    public const string ActiveFilter = "active";

    public static IEndpointRouteBuilder MapPersonalTokenEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clock = app.ServiceProvider.GetRequiredService<IClock>();

        var group = app.MapCrud<PersonalToken, PersonalTokenDto, PersonalTokenDto, PersonalTokenWriteDto>(Pattern, options =>
        {
            options.PermissionArea = "Tokens";
            options.Name = "MyTokens";
            options.Participating = (token, vid) => token.Vid == vid;
            options.ReadOnly = true;
            options.DefaultOrder = token => token.CreatedAt;
            options.Sortable.Add(nameof(PersonalToken.Name));
            options.Sortable.Add(nameof(PersonalToken.CreatedAt));
            options.Sortable.Add(nameof(PersonalToken.ExpiresAt));
            options.Sortable.Add(nameof(PersonalToken.LastUsedAt));
            options.Filterable.Add(nameof(PersonalToken.Audience));
            options.CustomFilters[ActiveFilter] = (query, raw) =>
            {
                var now = clock.UtcNow;
                return raw switch
                {
                    "true" => query.Where(token => token.RevokedAt == null && token.ExpiresAt > now),
                    "false" => query.Where(token => token.RevokedAt != null || token.ExpiresAt <= now),
                    _ => null,
                };
            };
            options.DefaultFilters[ActiveFilter] = "true";
            options.SearchFields.Add(token => token.Name);
            options.ToList = token => ToDto(token, clock.UtcNow);
            options.ToDetail = token => ToDto(token, clock.UtcNow);
        });

        group.MapPost("/", CreateAsync)
            .WithName("MyTokensCreate")
            .RequireAuthorization(HubPolicies.SignedIn);

        group.MapPost("/{id:long}/revoke", RevokeAsync)
            .WithName("MyTokensRevoke")
            .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }

    public static PersonalTokenDto ToDto(PersonalToken token, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(token);

        var status = token.RevokedAt is not null
            ? PersonalTokenStatus.Revoked
            : token.ExpiresAt <= now ? PersonalTokenStatus.Expired : PersonalTokenStatus.Active;

        return new PersonalTokenDto(
            token.Id,
            token.Name,
            token.Audience,
            token.Prefix,
            status,
            token.CreatedAt,
            token.ExpiresAt,
            token.LastUsedAt,
            token.RevokedAt);
    }

    private static async Task<Results<Ok<PersonalTokenIssuedDto>, ValidationProblem>> CreateAsync(
        PersonalTokenWriteDto body,
        PersonalTokens tokens,
        ICurrentUser user,
        LocaleCatalog catalog,
        IClock clock,
        HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);

        var (issued, problems) = await tokens.IssueAsync(user, body.Name, body.Audience, body.Days, http.RequestAborted);
        if (issued is null)
        {
            return TypedResults.ValidationProblem(
                problems!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
                title: catalog.Resolve(user.Locale, CrudProblems.ValidationTitleKey));
        }

        return TypedResults.Ok(new PersonalTokenIssuedDto(ToDto(issued.Row, clock.UtcNow), issued.Token));
    }

    private static async Task<Results<NoContent, NotFound>> RevokeAsync(long id, PersonalTokens tokens, ICurrentUser user, HttpContext http) =>
        await tokens.RevokeAsync(user.Vid, id, http.RequestAborted) ? TypedResults.NoContent() : TypedResults.NotFound();
}
