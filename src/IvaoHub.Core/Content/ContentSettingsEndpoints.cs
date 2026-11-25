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

/// <summary>What the settings of the content say: the two pages of the staff the editor names.</summary>
/// <param name="StylePageId">The page of the style chosen, or null when none is.</param>
/// <param name="StylePagePath">
/// Its address, read with the visibility of whoever asks and only while it is published: null when they cannot read it, so
/// that the instructions for Claude leave the style out rather than promise a page their reader will never open.
/// </param>
/// <param name="GuidePageId">The page of the guide «How to make a page» chosen, or null when none is.</param>
/// <param name="GuidePagePath">
/// Its address, read the same way: null when whoever asks cannot open it, so that the editor draws no link to a page
/// they would not reach.
/// </param>
public sealed record ContentSettingsDto(
    long? StylePageId,
    string? StylePagePath,
    long? GuidePageId,
    string? GuidePagePath);

/// <summary>
/// What the settings panel saves, the whole of it: a setting the body leaves null is taken away, like a field of a form
/// that is emptied.
/// </summary>
public sealed record ContentSettingsWriteDto(long? StylePageId, long? GuidePageId);

/// <summary>
/// The settings of the content, one key per setting in <c>hub_division_settings</c>
/// (<c>decisions/2026-11-04-la-pagina-dello-stile.md</c>, decided by Carmine in chat to the master, 4 November 2026). Two
/// today, each a page of the staff that the editor names:
/// <list type="bullet">
/// <item>the **page of the style**, which the instructions «Download for Claude» end with (note
/// 2026-10-30-una-pagina-scritta-con-claude §3, answer 1);</item>
/// <item>the **page of the guide** «How to make a page», which the form of a row and its editor link to (design
/// <c>13-design-struttura-del-sito.md</c> §5.2, answer 6 of Carmine; note <c>2026-11-25-la-guida-per-chi-scrive</c>). The
/// seed chooses the page it makes (<see cref="ChooseIfNoneAsync"/>), when nobody has chosen another.</item>
/// </list>
/// Both follow one rule, <see cref="PageSetting"/>: a page that whoever chooses can read online, and that the whole staff
/// reads, because whoever downloads the instructions or opens the editor may be of any department.
/// <para>⚠️ Two hand written verbs, counted in the content family (plan §16.6): the content is not a module, so the
/// settings of a module (<c>/api/modules/{key}/settings</c>) are not where these can live, and no row of the CRUD engine
/// holds them.</para>
/// </summary>
public static class ContentSettingsEndpoints
{
    public const string Pattern = "/api/content/settings";

    /// <summary>The row of <c>hub_division_settings</c> that holds the page of the style.</summary>
    public const string StylePageKey = "content.stylePage";

    /// <summary>The row of <c>hub_division_settings</c> that holds the page of the guide.</summary>
    public const string GuidePageKey = "content.guidePage";

    /// <summary>Not a published page whoever chooses can read.</summary>
    public const string StylePageNotFoundKey = "errors.content.settings.stylePageNotFound";

    /// <summary>A page only one department reads: a member of the staff of another department downloads the instructions too.</summary>
    public const string StylePageTooNarrowKey = "errors.content.settings.stylePageTooNarrow";

    /// <summary>Not a published page whoever chooses can read.</summary>
    public const string GuidePageNotFoundKey = "errors.content.settings.guidePageNotFound";

    /// <summary>A page only one department reads: whoever writes a page in another department opens the guide too.</summary>
    public const string GuidePageTooNarrowKey = "errors.content.settings.guidePageTooNarrow";

    /// <summary>The page of the style: its row, the field a refusal is said on as the browser names it, the two refusals.</summary>
    private static readonly PageSetting Style = new(StylePageKey, "stylePageId", StylePageNotFoundKey, StylePageTooNarrowKey);

    /// <summary>The page of the guide, the same four facts.</summary>
    private static readonly PageSetting Guide = new(GuidePageKey, "guidePageId", GuidePageNotFoundKey, GuidePageTooNarrowKey);

    /// <summary>
    /// The keys a seed file may name as the setting its page is chosen for (<see cref="ChooseIfNoneAsync"/>): the settings
    /// that hold a page, and nothing else of <c>hub_division_settings</c>.
    /// </summary>
    public static IReadOnlyList<string> PageKeys { get; } = [StylePageKey, GuidePageKey];

    private sealed record PageSetting(string Key, string Field, string NotFoundKey, string TooNarrowKey);

    private sealed record StoredPage(long ContentId);

    public static IEndpointRouteBuilder MapContentSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Whoever downloads the instructions or opens the editor asks: they hold `Content.Edit` on the row they write.
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

    /// <summary>
    /// The seed's choice (design 13 §5.2): the page it has just made becomes the page of <paramref name="key"/>, unless a row
    /// of that setting is there already — somebody chose another, or chose none and then a page that has since gone; either
    /// way it is theirs. Added to the context and saved with the seed's own save, audited with VID 0 like every row the
    /// installation writes for itself.
    /// </summary>
    public static async Task<bool> ChooseIfNoneAsync(
        HubDbContext database,
        string key,
        long contentId,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        if (!PageKeys.Contains(key, StringComparer.Ordinal)
            || database.DivisionSettings.Local.Any(setting => setting.Key == key)
            || await database.DivisionSettings.AnyAsync(setting => setting.Key == key, cancellationToken))
        {
            return false;
        }

        database.DivisionSettings.Add(new DivisionSetting
        {
            Key = key,
            ValueJson = Serialized(contentId),
            UpdatedBy = 0,
            UpdatedAt = clock.UtcNow,
        });

        return true;
    }

    private static async Task<ContentSettingsDto> ReadAsync(HubDbContext database, HttpContext http)
    {
        var (styleId, stylePath) = await ReadOneAsync(database, Style, http.RequestAborted);
        var (guideId, guidePath) = await ReadOneAsync(database, Guide, http.RequestAborted);
        return new ContentSettingsDto(styleId, stylePath, guideId, guidePath);
    }

    private static async Task<(long? Id, string? Path)> ReadOneAsync(
        HubDbContext database,
        PageSetting setting,
        CancellationToken cancellationToken)
    {
        var chosen = await StoredAsync(database, setting.Key, cancellationToken);
        if (chosen is null)
        {
            return (null, null);
        }

        // The public read of the hub's context: the visibility of whoever asks, and a published version, by the query
        // filter (`VisibilityQueryFilter`). What it does not return, they cannot read.
        var page = await ReadablePageAsync(database, chosen.Value, cancellationToken);
        return (chosen, page?.Url);
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

        var refusals = new Refusals();
        var stylePath = await CheckedPathAsync(database, Style, body.StylePageId, refusals, http.RequestAborted);
        var guidePath = await CheckedPathAsync(database, Guide, body.GuidePageId, refusals, http.RequestAborted);

        if (!refusals.IsEmpty)
        {
            return CrudProblems.Validation(refusals, catalog, currentUser.Locale);
        }

        await StoreAsync(database, Style, body.StylePageId, currentUser, clock, http.RequestAborted);
        await StoreAsync(database, Guide, body.GuidePageId, currentUser, clock, http.RequestAborted);

        // Audited by the interceptor: `DivisionSetting` is `[Audited]`.
        await database.SaveChangesAsync(http.RequestAborted);
        return TypedResults.Ok(new ContentSettingsDto(body.StylePageId, stylePath, body.GuidePageId, guidePath));
    }

    /// <summary>
    /// The address of the page chosen for a setting, or a refusal: a page — not a template, not a news item — that whoever
    /// chooses can read online, and not one of a single department, because whoever reads it may be of any.
    /// </summary>
    private static async Task<string?> CheckedPathAsync(
        HubDbContext database,
        PageSetting setting,
        long? id,
        Refusals refusals,
        CancellationToken cancellationToken)
    {
        if (id is null)
        {
            return null;
        }

        var page = await ReadablePageAsync(database, id.Value, cancellationToken);
        if (page is null)
        {
            refusals.Add(setting.Field, setting.NotFoundKey);
            return null;
        }

        if (page.Visibility == Visibility.Department)
        {
            refusals.Add(setting.Field, setting.TooNarrowKey);
            return null;
        }

        return page.Url;
    }

    private static async Task StoreAsync(
        HubDbContext database,
        PageSetting setting,
        long? id,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var row = await database.DivisionSettings.FirstOrDefaultAsync(each => each.Key == setting.Key, cancellationToken);
        if (id is null)
        {
            if (row is not null)
            {
                database.DivisionSettings.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new DivisionSetting { Key = setting.Key };
            database.DivisionSettings.Add(row);
        }

        var value = Serialized(id.Value);
        if (row.ValueJson != value)
        {
            // Only a change is a change: the panel saves both settings, and the one left as it was leaves no trace.
            row.ValueJson = value;
            row.UpdatedBy = currentUser.Vid;
            row.UpdatedAt = clock.UtcNow;
        }
    }

    private static string Serialized(long contentId) =>
        JsonSerializer.Serialize(new StoredPage(contentId), JsonSerializerOptions.Web);

    private static async Task<long?> StoredAsync(HubDbContext database, string key, CancellationToken cancellationToken)
    {
        var stored = await database.DivisionSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.ValueJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredPage>(stored, JsonSerializerOptions.Web)?.ContentId;
        }
        catch (JsonException)
        {
            // A value nobody can read is no choice at all: the instructions leave the style out, the editor the guide.
            return null;
        }
    }

    /// <summary>A page — not a template, not a news item — that whoever asks can read online.</summary>
    private static Task<ContentEntry?> ReadablePageAsync(HubDbContext database, long id, CancellationToken cancellationToken) =>
        database.Contents
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id && row.Kind == ContentKind.Page && !row.IsTemplate, cancellationToken);
}
