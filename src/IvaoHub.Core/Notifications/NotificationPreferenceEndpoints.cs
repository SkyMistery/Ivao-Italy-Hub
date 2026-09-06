using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Notifications;

/// <summary>
/// Which notifications a member wants. It is the same shape as <c>/api/me/locale</c> and for the
/// same reason: a setting that belongs to the person asking is not a resource of the back office,
/// so it is not the CRUD engine — there is no department to scope it to and no list to page
/// through, and the only row it can ever reach is the caller's own.
/// <para>Deliberately not part of the bootstrap: the single page application does not need it in
/// order to draw itself, only the profile screen does, and <c>/api/me</c> is answered on every
/// load (plan section 16.7).</para>
/// </summary>
public static class NotificationPreferenceEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/me/notifications";

    /// <summary>The i18n key the client shows when the type is not one the hub sends.</summary>
    public const string UnknownTypeKey = "errors.notifications.unknownType";

    /// <summary>The field the error is reported on, so the form generator can place it.</summary>
    private const string Field = "type";

    public static IEndpointRouteBuilder MapNotificationPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async Task<Ok<IReadOnlyList<NotificationPreferenceDto>>> (
            HttpContext http,
            HubDbContext database,
            ICurrentUser currentUser) =>
        {
            var switchedOff = await database.NotificationPreferences
                .AsNoTracking()
                .Where(preference => preference.Vid == currentUser.Vid && !preference.Enabled)
                .Select(preference => preference.Type)
                .ToListAsync(http.RequestAborted);

            // Every type the hub knows, in the order it declares them, with the effective answer.
            // A member who has never touched this gets every one of them as on, which is what the
            // absence of a row means (see NotificationPreference).
            IReadOnlyList<NotificationPreferenceDto> preferences =
            [
                .. NotificationTypes.All.Select(type =>
                    new NotificationPreferenceDto(type, !switchedOff.Contains(type))),
            ];

            return TypedResults.Ok(preferences);
        })
        .WithName("MeNotificationPreferences")
        .RequireAuthorization(HubPolicies.SignedIn);

        app.MapPut(Pattern, async Task<Results<Ok<NotificationPreferenceDto>, ValidationProblem>> (
            HttpContext http,
            NotificationPreferenceDto body,
            HubDbContext database,
            ICurrentUser currentUser,
            LocaleCatalog catalog) =>
        {
            ArgumentNullException.ThrowIfNull(body);

            if (!NotificationTypes.IsKnown(body.Type))
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal) { [Field] = [UnknownTypeKey] },
                    title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
            }

            var preference = await database.NotificationPreferences
                .FirstOrDefaultAsync(
                    row => row.Vid == currentUser.Vid && row.Type == body.Type,
                    http.RequestAborted);

            if (preference is null)
            {
                preference = new NotificationPreference { Vid = currentUser.Vid, Type = body.Type };
                database.NotificationPreferences.Add(preference);
            }

            preference.Enabled = body.Enabled;
            await database.SaveChangesAsync(http.RequestAborted);

            return TypedResults.Ok(body);
        })
        .WithName("MeSetNotificationPreference")
        .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }
}

/// <summary>One kind of notification, and whether this member wants it.</summary>
public sealed record NotificationPreferenceDto(string Type, bool Enabled);
