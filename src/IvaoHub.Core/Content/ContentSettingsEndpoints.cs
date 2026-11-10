using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>What the settings of the content say: the page of the staff that describes how the division writes.</summary>
/// <param name="StylePageId">The page chosen, or null when none is.</param>
/// <param name="StylePagePath">
/// Its address, read with the visibility of whoever asks and only while it is published: null when they cannot read it, so
/// that the instructions for Claude leave the style out rather than promise a page their reader will never open.
/// </param>
public sealed record ContentSettingsDto(long? StylePageId, string? StylePagePath);

/// <summary>What the settings panel saves. Null takes the choice away.</summary>
public sealed record ContentSettingsWriteDto(long? StylePageId);

/// <summary>
/// The settings of the content, one key per setting in <c>hub_division_settings</c>
/// (<c>decisions/2026-11-04-la-pagina-dello-stile.md</c>, decided by Carmine in chat to the master, 4 November 2026). One
/// today: the **page of the style**, which the instructions «Download for Claude» end with (note
/// 2026-10-30-una-pagina-scritta-con-claude §3, answer 1).
/// <para>⚠️ Two hand written verbs, counted in the content family (plan §16.6): the content is not a module, so the
/// settings of a module (<c>/api/modules/{key}/settings</c>) are not where these can live, and no row of the CRUD engine
/// holds them.</para>
/// </summary>
public static class ContentSettingsEndpoints
{
    public const string Pattern = "/api/content/settings";

    /// <summary>The row of <c>hub_division_settings</c> that holds the page of the style.</summary>
    public const string StylePageKey = "content.stylePage";

    /// <summary>Not a published page whoever chooses can read.</summary>
    public const string StylePageNotFoundKey = "errors.content.settings.stylePageNotFound";

    /// <summary>A page only one department reads: a member of the staff of another department downloads the instructions too.</summary>
    public const string StylePageTooNarrowKey = "errors.content.settings.stylePageTooNarrow";

    /// <summary>The field a refusal is said on, as the browser names it.</summary>
    private const string StylePageField = "stylePageId";

    private sealed record StoredStylePage(long ContentId);

    public static IEndpointRouteBuilder MapContentSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Whoever downloads the instructions asks: they hold `Content.Edit` on the row they write.
        app.MapGet(Pattern, ReadAsync)
            .WithName("ContentSettings")
            .Produces<ContentSettingsDto>()
            .RequireAuthorization(CorePermissions.ContentEdit);

        // At the door, the permission held anywhere; inside, only whoever reaches every department — the direction, the web
        // team, the superadmin: a setting of the whole division is not one department's to change (Carmine, 4 November
        // 2026, «Solo chi ha tutti»; note 2026-11-04-la-pagina-dello-stile §2).
        app.MapPut(Pattern, WriteAsync)
            .WithName("ContentSettingsWrite")
            .Produces<ContentSettingsDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization(CorePermissions.ContentManageTemplates);

        return app;
    }

    private static async Task<ContentSettingsDto> ReadAsync(HubDbContext database, HttpContext http)
    {
        var chosen = await StoredAsync(database, http.RequestAborted);
        if (chosen is null)
        {
            return new ContentSettingsDto(null, null);
        }

        // The public read of the hub's context: the visibility of whoever asks, and a published version, by the query
        // filter (`VisibilityQueryFilter`). What it does not return, they cannot read.
        var page = await ReadablePageAsync(database, chosen.Value, http.RequestAborted);
        return new ContentSettingsDto(chosen, page?.Url);
    }

    private static async Task<IResult> WriteAsync(
        ContentSettingsWriteDto body,
        HubDbContext database,
        ICurrentUser currentUser,
        IClock clock,
        LocaleCatalog catalog,
        HttpContext http)
    {
        if (!(currentUser.IsSuperadmin || currentUser.HasAllDepartments)
            || !currentUser.HasAny(CorePermissions.ContentManageTemplates))
        {
            return TypedResults.Forbid();
        }

        string? path = null;
        if (body.StylePageId is { } id)
        {
            var page = await ReadablePageAsync(database, id, http.RequestAborted);
            var refusals = new Refusals();

            if (page is null)
            {
                refusals.Add(StylePageField, StylePageNotFoundKey);
            }
            else if (page.Visibility == Visibility.Department)
            {
                refusals.Add(StylePageField, StylePageTooNarrowKey);
            }

            if (!refusals.IsEmpty)
            {
                return CrudProblems.Validation(refusals, catalog, currentUser.Locale);
            }

            path = page!.Url;
        }

        var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == StylePageKey, http.RequestAborted);
        if (body.StylePageId is null)
        {
            if (row is not null)
            {
                database.DivisionSettings.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new DivisionSetting { Key = StylePageKey };
                database.DivisionSettings.Add(row);
            }

            row.ValueJson = JsonSerializer.Serialize(new StoredStylePage(body.StylePageId.Value), JsonSerializerOptions.Web);
            row.UpdatedBy = currentUser.Vid;
            row.UpdatedAt = clock.UtcNow;
        }

        // Audited by the interceptor: `DivisionSetting` is `[Audited]`.
        await database.SaveChangesAsync(http.RequestAborted);
        return TypedResults.Ok(new ContentSettingsDto(body.StylePageId, path));
    }

    private static async Task<long?> StoredAsync(HubDbContext database, CancellationToken cancellationToken)
    {
        var stored = await database.DivisionSettings
            .AsNoTracking()
            .Where(setting => setting.Key == StylePageKey)
            .Select(setting => setting.ValueJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredStylePage>(stored, JsonSerializerOptions.Web)?.ContentId;
        }
        catch (JsonException)
        {
            // A value nobody can read is no choice at all: the instructions leave the style out.
            return null;
        }
    }

    /// <summary>A page — not a template, not a news item — that whoever asks can read online.</summary>
    private static Task<ContentEntry?> ReadablePageAsync(HubDbContext database, long id, CancellationToken cancellationToken) =>
        database.Contents
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id && row.Kind == ContentKind.Page && !row.IsTemplate, cancellationToken);
}
