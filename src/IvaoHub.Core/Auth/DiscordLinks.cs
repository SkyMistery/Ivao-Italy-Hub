using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace IvaoHub.Core.Auth;

/// <summary>How a round trip to Discord ended.</summary>
public enum DiscordLinkOutcome
{
    Linked,

    /// <summary>The account is linked to another VID: one account, one person, so the division's bot never finds two.</summary>
    Taken,

    Failed,
}

/// <summary>Writes a member's link with their Discord account: the three columns of <c>hub_users</c> and nothing else.</summary>
public sealed class DiscordLinks(HubDbContext database, IClock clock)
{
    /// <summary>The limit Discord gives both: a snowflake is shorter, and a username is at most this.</summary>
    private const int MaxLength = 32;

    /// <summary>Links, or links again: a new account replaces the old one, and the same account brings its name up to date.</summary>
    public async Task<DiscordLinkOutcome> LinkAsync(int vid, string accountId, string username, CancellationToken cancellationToken = default)
    {
        if (accountId.Length is 0 or > MaxLength || username.Length is 0 or > MaxLength)
        {
            return DiscordLinkOutcome.Failed;
        }

        if (await database.Users.AnyAsync(row => row.DiscordId == accountId && row.Vid != vid, cancellationToken))
        {
            return DiscordLinkOutcome.Taken;
        }

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            return DiscordLinkOutcome.Failed;
        }

        user.DiscordId = accountId;
        user.DiscordUsername = username;
        user.DiscordLinkedAt = clock.UtcNow;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Two members racing for one account: the unique index is what answers. Anything else (the row changed under a
            // sign in) is a round trip to do again.
            return exception.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry }
                ? DiscordLinkOutcome.Taken
                : DiscordLinkOutcome.Failed;
        }

        return DiscordLinkOutcome.Linked;
    }

    /// <summary>Forgets the link. No token was kept, so there is nothing else to undo here.</summary>
    public async Task UnlinkAsync(int vid, CancellationToken cancellationToken = default)
    {
        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.DiscordId = null;
        user.DiscordUsername = null;
        user.DiscordLinkedAt = null;
        await database.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>What a member sees of their own link. <paramref name="Available"/> says whether this installation offers it.</summary>
public sealed record DiscordLinkDto(bool Available, string? Username, DateTime? LinkedAt);

/// <summary>
/// A member's own link, read and undone: only ever the person asking, since nobody else sees it (plan section 9.7). Not in the
/// bootstrap, like the notification preferences: only the profile needs it. The round trip that makes one starts at
/// <c>/auth/discord</c>.
/// </summary>
public static class DiscordLinkEndpoints
{
    public const string Pattern = "/api/me/discord";

    public static IEndpointRouteBuilder MapDiscordLinkEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async Task<Ok<DiscordLinkDto>> (
            HttpContext http,
            HubDbContext database,
            ICurrentUser currentUser,
            IOptions<DiscordOAuthOptions> discord) =>
        {
            var link = await database.Users
                .AsNoTracking()
                .Where(row => row.Vid == currentUser.Vid)
                .Select(row => new { row.DiscordUsername, row.DiscordLinkedAt })
                .FirstOrDefaultAsync(http.RequestAborted);

            return TypedResults.Ok(new DiscordLinkDto(discord.Value.IsConfigured, link?.DiscordUsername, link?.DiscordLinkedAt));
        })
        .WithName("MeDiscordLink")
        .RequireAuthorization(HubPolicies.SignedIn);

        app.MapDelete(Pattern, async Task<NoContent> (
            HttpContext http,
            DiscordLinks links,
            ICurrentUser currentUser) =>
        {
            await links.UnlinkAsync(currentUser.Vid, http.RequestAborted);
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(HubRateLimits.MemberWrites)
        .WithName("MeDiscordUnlink")
        .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }
}
