using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Preferences;

/// <summary>
/// A member's own preferences, one key at a time (M2, T4b). The same shape as
/// <c>/api/me/notifications</c> and for the same reason: a setting of the person asking is not a
/// resource of the back office — no department, no list, and the only row it can reach is the
/// caller's own. It is not part of the bootstrap either: a screen that needs a preference asks for it.
/// </summary>
public static class UserPreferenceEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/me/preferences/{key}";

    /// <summary>A key no enabled module declares.</summary>
    public const string UnknownKeyKey = "errors.preferences.unknownKey";

    /// <summary>A value the declaring module would not be able to read back.</summary>
    public const string InvalidValueKey = "errors.preferences.invalidValue";

    public static IEndpointRouteBuilder MapUserPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async Task<Results<Ok<UserPreferenceDto>, ValidationProblem>> (
            HttpContext http,
            string key,
            HubDbContext database,
            PreferenceCatalog preferences,
            ICurrentUser currentUser,
            LocaleCatalog catalog) =>
        {
            if (!preferences.TryGet(key, out _))
            {
                return Refuse(nameof(key), UnknownKeyKey, currentUser, catalog);
            }

            var stored = await database.UserPreferences
                .AsNoTracking()
                .Where(row => row.Vid == currentUser.Vid && row.Key == key)
                .Select(row => row.ValueJson)
                .FirstOrDefaultAsync(http.RequestAborted);

            // No row is an answer and not a 404: the member never chose, and the module has a default.
            return TypedResults.Ok(new UserPreferenceDto(
                key,
                stored is null ? null : JsonSerializer.Deserialize<JsonElement>(stored)));
        })
        .WithName("MePreference")
        .RequireAuthorization(HubPolicies.SignedIn);

        app.MapPut(Pattern, async Task<Results<Ok<UserPreferenceDto>, ValidationProblem>> (
            HttpContext http,
            string key,
            UserPreferenceWriteDto body,
            HubDbContext database,
            PreferenceCatalog preferences,
            ICurrentUser currentUser,
            IClock clock,
            LocaleCatalog catalog) =>
        {
            ArgumentNullException.ThrowIfNull(body);

            if (!preferences.TryGet(key, out var descriptor))
            {
                return Refuse(nameof(key), UnknownKeyKey, currentUser, catalog);
            }

            var json = body.Value.ValueKind == JsonValueKind.Undefined ? null : body.Value.GetRawText();
            if (json is null
                || System.Text.Encoding.UTF8.GetByteCount(json) > PreferenceCatalog.MaxValueBytes
                || !descriptor.Accepts(body.Value))
            {
                return Refuse("value", InvalidValueKey, currentUser, catalog);
            }

            var preference = await database.UserPreferences
                .FirstOrDefaultAsync(row => row.Vid == currentUser.Vid && row.Key == key, http.RequestAborted);

            if (preference is null)
            {
                preference = new UserPreference { Vid = currentUser.Vid, Key = key };
                database.UserPreferences.Add(preference);
            }

            preference.ValueJson = json;
            preference.UpdatedAt = clock.UtcNow;
            await database.SaveChangesAsync(http.RequestAborted);

            return TypedResults.Ok(new UserPreferenceDto(key, body.Value));
        })
        .WithName("MeSetPreference")
        .RequireAuthorization(HubPolicies.SignedIn);

        return app;
    }

    private static ValidationProblem Refuse(string field, string messageKey, ICurrentUser currentUser, LocaleCatalog catalog) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [messageKey] },
            title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
}

/// <summary>One preference of the member asking; <c>null</c> when they never chose.</summary>
public sealed record UserPreferenceDto(string Key, JsonElement? Value);

/// <summary>The value to keep, whatever JSON the declaring module accepts.</summary>
public sealed record UserPreferenceWriteDto(JsonElement Value);
