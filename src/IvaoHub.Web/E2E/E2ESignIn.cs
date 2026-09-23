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

    /// <summary>
    /// A second person, a pilot with no staff position and a mailbox, signed in with <c>?as=pilot</c> (M2, T13b). Nobody
    /// validates their own reports, so the round needs somebody else to fly them, and the mail of the outcome needs an address
    /// to reach — Mailpit's, on the bench. Left out, <c>?as=pilot</c> is a 404.
    /// </summary>
    public E2EPilotOptions? Pilot { get; set; }

    /// <summary>
    /// A third person, a member of staff of the department that looks after the tours, signed in with <c>?as=assistant</c>
    /// (M2, T14b). Whoever decided a report does not judge its dispute, so upholding one needs somebody who holds
    /// <c>Tours.ReopenDecisions</c> and did not decide it. No mailbox. Left out, <c>?as=assistant</c> is a 404.
    /// </summary>
    public E2EAssistantOptions? Assistant { get; set; }
}

/// <summary>The bench's pilot: a member of the division and nothing else.</summary>
internal sealed class E2EPilotOptions
{
    [Range(1, int.MaxValue)]
    public int Vid { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Where the outcome of a report is written to. On the bench, a mailbox of Mailpit.</summary>
    [Required]
    public string Email { get; set; } = string.Empty;
}

/// <summary>The bench's assistant: a member of staff, with positions as IVAO spells them, and no mailbox.</summary>
internal sealed class E2EAssistantOptions
{
    [Range(1, int.MaxValue)]
    public int Vid { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

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

    /// <summary>The value of <c>?as=</c> that signs in the pilot rather than the member of staff.</summary>
    public const string AsPilot = "pilot";

    /// <summary>The value of <c>?as=</c> that signs in the third person, the assistant (T14b).</summary>
    public const string AsAssistant = "assistant";

    /// <summary>
    /// Signs the caller in as the configured staff member — or, with <c>?as=pilot</c> or <c>?as=assistant</c>, as the
    /// configured pilot or assistant —, creating them on first use. The cookie it writes is the one a real login writes, so what the suite exercises afterwards — the
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

        app.MapPost(Path, async Task<IResult> (
            HttpContext context,
            UserSyncService users,
            IOptions<E2EOptions> e2e,
            IOptions<DivisionOptions> division,
            string? @as,
            CancellationToken cancellationToken) =>
        {
            var options = e2e.Value;
            var settings = division.Value;

            // Who, of the three: the member of staff, the pilot, the assistant — or nobody the bench configured.
            (int Vid, string FirstName, string LastName, string? Email, IList<string> Positions)? person = @as switch
            {
                null => (options.Vid, options.FirstName, options.LastName, null, options.Positions),
                AsPilot when options.Pilot is { } pilot => (pilot.Vid, pilot.FirstName, pilot.LastName, pilot.Email, []),
                AsAssistant when options.Assistant is { } assistant =>
                    (assistant.Vid, assistant.FirstName, assistant.LastName, null, assistant.Positions),
                _ => null,
            };

            if (person is not { } who)
            {
                return TypedResults.NotFound();
            }

            var signedIn = await users.UpsertAsync(
                new IvaoUserProfile(
                    who.Vid,
                    who.FirstName,
                    who.LastName,
                    PublicNickname: null,
                    DivisionCode: settings.Code,
                    CountryId: null,
                    RatingAtc: null,
                    RatingPilot: null,
                    DiscordId: null,
                    // A member of staff has no mailbox: an invented address would be one the queue would actually try
                    // to write to. The pilot has one, on the bench's Mailpit (T13b).
                    Email: who.Email,
                    LanguageId: settings.DefaultLocale,
                    IvaoIsStaff: who.Positions.Count > 0,
                    IvaoIsSupervisor: false,
                    StaffPositions: [.. who.Positions]),
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
