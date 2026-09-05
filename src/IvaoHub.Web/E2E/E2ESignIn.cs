using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IvaoHub.Web.E2E;

/// <summary>
/// Who the browser suite is when it runs the round: a staff member of this division, made up.
/// <para>Not a login: IVAO cannot be part of a run that has to be reproducible in CI, and a real
/// login needs a real person's credentials. What is faked is the identity provider and nothing
/// else — the row in <c>hub_users</c>, the staff positions, the effective permissions, the
/// application cookie and every policy behind it are the production ones (design M1 §11.1).</para>
/// </summary>
internal sealed class E2EOptions
{
    public const string SectionName = "E2E";

    /// <summary>
    /// The second lock. The first is the environment name; this one exists so that the flag
    /// appearing in a configuration file that is not the bench's own stops the application instead
    /// of being quietly ignored (<see cref="HubConfiguration.RequireE2EEnvironment"/>).
    /// </summary>
    public bool Enabled { get; set; }

    [Range(1, int.MaxValue)]
    public int Vid { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Staff positions as IVAO spells them, for example <c>IT-EC</c>. They are parsed by the real
    /// <c>StaffRoleMap</c>, so the permissions the suite runs with are the ones the matrix gives
    /// that role and not a list somebody wrote out.
    /// </summary>
    public IList<string> Positions { get; init; } = [];
}

internal static class E2ESignIn
{
    /// <summary>Where the bench signs itself in. Outside the SPA, and outside the API.</summary>
    public const string Path = "/e2e/signin";

    /// <summary>The prefix the SPA must not swallow while the bench is running.</summary>
    public const string PathPrefix = "/e2e";

    public static IServiceCollection AddE2ESignIn(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<E2EOptions>()
            .Bind(configuration.GetSection(E2EOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Signs the caller in as the configured staff member, creating them on first use. The cookie
    /// it writes is the one a real login writes, so what the suite exercises afterwards — the
    /// security stamp, the permission claims, the department guard — is the real thing.
    /// </summary>
    public static void MapE2ESignIn(this WebApplication app)
    {
        // Both locks, and this is the second one: the environment carries the name, the
        // configuration carries the intent. Neither alone opens the door.
        if (!app.Services.GetRequiredService<IOptions<E2EOptions>>().Value.Enabled)
        {
            return;
        }

        app.MapPost(Path, async (
            HttpContext context,
            UserSyncService users,
            IOptions<E2EOptions> e2e,
            IOptions<DivisionOptions> division,
            CancellationToken cancellationToken) =>
        {
            var options = e2e.Value;
            var settings = division.Value;

            var signedIn = await users.UpsertAsync(
                new IvaoUserProfile(
                    options.Vid,
                    options.FirstName,
                    options.LastName,
                    PublicNickname: null,
                    DivisionCode: settings.Code,
                    CountryId: null,
                    RatingAtc: null,
                    RatingPilot: null,
                    DiscordId: null,
                    LanguageId: settings.DefaultLocale,
                    IvaoIsStaff: options.Positions.Count > 0,
                    IvaoIsSupervisor: false,
                    StaffPositions: [.. options.Positions]),
                cancellationToken);

            var identity = HubClaims.BuildIdentity(
                signedIn.User.Vid,
                signedIn.User.FirstName,
                signedIn.User.LastName,
                signedIn.User.Locale ?? settings.DefaultLocale,
                signedIn.User.SecurityStamp,
                signedIn.User.IsSuperadmin,
                signedIn.User.IsStaff,
                signedIn.Positions,
                signedIn.Permissions);

            await context.SignInAsync(HubClaims.CookieScheme, new ClaimsPrincipal(identity));

            return TypedResults.Ok(new E2ESignInResponse(
                signedIn.User.Vid,
                [.. signedIn.Positions.Select(position => position.Raw)],
                [.. signedIn.Permissions.Select(permission => permission.Name).Distinct().Order(StringComparer.Ordinal)]));
        });
    }
}

/// <summary>What the bench got, so a failing run says who it was rather than only that it failed.</summary>
internal sealed record E2ESignInResponse(int Vid, IReadOnlyList<string> Positions, IReadOnlyList<string> Permissions);
