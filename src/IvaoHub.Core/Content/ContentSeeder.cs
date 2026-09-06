using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// What a fresh installation starts with: the system templates from
/// <c>seed/content-templates/*.json</c> (design M0 section 5.6) and the pages built from them,
/// <c>seed/content-pages/*.json</c> (design M1 section 8.2).
/// <para>Each file is applied <b>once</b>, remembered by a key in <c>hub_division_settings</c>.
/// That is the whole point of the key: a later release can add a template or a page without
/// touching one the staff has since edited, and reinstalling does not undo their work. Nothing here
/// ever updates an existing row.</para>
/// <para>The text is not in the file. A file carries translation keys, spelled
/// <c>{ "$t": "seed…" }</c>, and they are resolved here into the languages the division actually
/// speaks — so a division running in English alone gets its site in English, with no Italian in it
/// (docs/FORKING.md). The filler prose says nothing about any particular division, which the
/// forkability test checks by reading these very rows.</para>
/// <para>Templates first and pages second, in one class rather than two, because the mechanism is
/// the same in every respect that matters: the same key, the same resolution of translation keys,
/// the same opaque envelope. What differs between them is three fields.</para>
/// </summary>
public sealed class ContentSeeder(
    HubDbContext database,
    ContentPublishService publication,
    BlockDocumentWalker walker,
    HubPaths paths,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<ContentSeeder> logger)
{
    /// <summary>Where the setting that remembers an applied template lives.</summary>
    public const string SettingPrefix = "template.system:";

    /// <summary>Where the setting that remembers an applied system page lives.</summary>
    public const string PageSettingPrefix = "page.system:";

    /// <summary>
    /// Where the setting that remembers the dashboard of one department lives. A department added
    /// to the enum in a later release gets its own on the first start after it, and the eight that
    /// already have one are not touched.
    /// </summary>
    public const string DashboardSettingPrefix = "page.dashboard:";

    /// <summary>Templates and the site's own pages belong to the web team: they are tools of the site.</summary>
    public const Department Owner = Department.WD;

    /// <summary>The marker that makes a string in a seed file a translation key.</summary>
    private const string TranslationMarker = "$t";

    /// <summary>A seed file spells a kind and a visibility as their names, the way the API does.</summary>
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var applied = new HashSet<string>(
            await database.DivisionSettings
                .AsNoTracking()
                .Where(setting =>
                    setting.Key.StartsWith(SettingPrefix)
                    || setting.Key.StartsWith(PageSettingPrefix)
                    || setting.Key.StartsWith(DashboardSettingPrefix))
                .Select(setting => setting.Key)
                .ToListAsync(cancellationToken),
            StringComparer.Ordinal);

        await SeedTemplatesAsync(applied, cancellationToken);
        await SeedPagesAsync(applied, cancellationToken);
    }

    private async Task SeedTemplatesAsync(HashSet<string> applied, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(paths.Seed, "content-templates");
        if (!Directory.Exists(directory))
        {
            // A package without the seed folder is a package that starts with no templates, not a
            // package that refuses to start.
            logger.LogWarning("No content template seed directory at {Directory}.", directory);
            return;
        }

        foreach (var file in Files(directory))
        {
            var seed = JsonSerializer.Deserialize<ContentTemplateSeed>(
                await File.ReadAllTextAsync(file, cancellationToken),
                ReadOptions);

            if (seed is null || string.IsNullOrWhiteSpace(seed.Slug))
            {
                logger.LogWarning("The content template seed {File} has no slug and was skipped.", file);
                continue;
            }

            var key = SettingPrefix + seed.Slug;
            if (!applied.Add(key))
            {
                continue;
            }

            var body = seed.Body?.DeepClone() ?? new JsonObject();
            Translate(body);

            database.Contents.Add(new ContentEntry
            {
                Kind = seed.Kind,
                Slug = seed.Slug,
                IsTemplate = true,
                OwnerDepartment = Owner,
                Visibility = Visibility.Staff,
                Status = PublishStatus.Draft,
                Title = Translated(seed.Title),
                Summary = seed.Summary is null ? null : Translated(seed.Summary),
                BodyJson = body.ToJsonString(),
                SchemaVersion = BlockDocumentWalker.SupportedSchemaVersion,
            });

            Remember(key);
            logger.LogInformation("Seeded the content template {Slug}.", seed.Slug);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The pages an installation is born with: the public ones the site is made of, and the
    /// dashboard every department opens its own back office on.
    /// <para>A page seed names the template it comes from, and that is not decoration: it is what
    /// lets the editor say later that the template has moved on (design M1 section 9.1). A seed
    /// without a body of its own is a copy of that template, deep copied exactly as "new from a
    /// template" copies one — the dashboards are all of them.</para>
    /// </summary>
    private async Task SeedPagesAsync(HashSet<string> applied, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(paths.Seed, "content-pages");
        if (!Directory.Exists(directory))
        {
            logger.LogWarning("No content page seed directory at {Directory}.", directory);
            return;
        }

        foreach (var file in Files(directory))
        {
            var seed = JsonSerializer.Deserialize<ContentPageSeed>(
                await File.ReadAllTextAsync(file, cancellationToken),
                ReadOptions);

            if (seed is null || string.IsNullOrWhiteSpace(seed.Template))
            {
                logger.LogWarning("The content page seed {File} names no template and was skipped.", file);
                continue;
            }

            var template = await CrudSource.BackOffice<ContentEntry>(database)
                .FirstOrDefaultAsync(
                    row => row.IsTemplate && row.Slug == seed.Template,
                    cancellationToken);

            if (template is null)
            {
                // A page whose template is not installed is a page nobody could edit sensibly. It
                // is skipped and said out loud rather than created half formed, and the key is not
                // written, so shipping the missing template later still seeds the page.
                logger.LogWarning(
                    "The content page seed {File} names the template {Template}, which is not installed.",
                    file,
                    seed.Template);
                continue;
            }

            foreach (var (key, slug, department) in Targets(seed))
            {
                if (!applied.Add(key))
                {
                    continue;
                }

                await SeedOnePageAsync(seed, template, slug, department, cancellationToken);
                Remember(key);

                logger.LogInformation("Seeded the {Kind} {Slug}.", seed.Kind, slug);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Which rows one seed file stands for: one, or one per department. A dashboard is the second
    /// (design M1 section 14): the slug is the department's own code, so nine of them live side by
    /// side under the unique index of <c>(kind, slug, is_template)</c> without touching it.
    /// </summary>
    private IEnumerable<(string Key, string Slug, Department Department)> Targets(ContentPageSeed seed)
    {
        if (!seed.PerDepartment)
        {
            yield return (PageSettingPrefix + seed.Slug, seed.Slug, Owner);
            yield break;
        }

        foreach (var department in Enum.GetValues<Department>())
        {
            var code = department.ToString();
            yield return (DashboardSettingPrefix + code, code.ToLowerInvariant(), department);
        }
    }

    private async Task SeedOnePageAsync(
        ContentPageSeed seed,
        ContentEntry template,
        string slug,
        Department department,
        CancellationToken cancellationToken)
    {
        JsonNode body;
        if (seed.Body is null)
        {
            body = JsonNode.Parse(template.BodyJson) ?? new JsonObject();
            TemplateCopy.Reidentify(walker, body);
        }
        else
        {
            body = seed.Body.DeepClone();
            Translate(body);
        }

        var page = new ContentEntry
        {
            Kind = seed.Kind,
            Slug = slug,
            IsTemplate = false,
            TemplateId = template.Id,
            OwnerDepartment = department,
            Visibility = seed.Visibility,
            Status = PublishStatus.Draft,
            Title = Translated(seed.Title),
            Summary = seed.Summary is null ? null : Translated(seed.Summary),
            BodyJson = body.ToJsonString(),
            SchemaVersion = BlockDocumentWalker.SupportedSchemaVersion,
        };

        database.Contents.Add(page);

        // The entry that leads to it, when the seed says the page belongs in a menu. It is the same
        // act — this page exists, and here is where the site points at it — so it is one file and
        // one key, and the label is the page's own title rather than a second thing to translate.
        // ⚠️ After this, the menu is a table like any other: whoever edits it moves, renames or
        // deletes this row, and nothing seeds it again.
        if (seed.Menu is { } menu)
        {
            database.MenuItems.Add(new MenuItem
            {
                Scope = menu.Scope,
                Sort = menu.Sort,
                Label = page.Title,
                Path = page.Url,
                Visibility = page.Visibility,
                IsActive = true,
                OwnerDepartment = MenuItem.Owner,
            });
        }

        // Saved before it is published, because a version points at a row that has an identifier.
        await database.SaveChangesAsync(cancellationToken);

        // And published, because a page nobody has published is a page nobody can read: the query
        // filter stops a draft for the visitor and for the department alike. What the staff then
        // edits is the draft, and the site keeps showing this version until somebody republishes —
        // which is the ordinary rule and not a special one for a seed.
        if (await publication.PublishAsync(page, changelog: null, cancellationToken) is { } failure)
        {
            logger.LogWarning(
                "The seeded {Kind} {Slug} could not be published: {Errors}.",
                seed.Kind,
                slug,
                string.Join(", ", failure.Errors.Select(error => $"{error.Key} {string.Join('/', error.Value)}")));
        }
    }

    private static IEnumerable<string> Files(string directory) =>
        Directory.EnumerateFiles(directory, "*.json").OrderBy(name => name, StringComparer.Ordinal);

    private void Remember(string key) =>
        database.DivisionSettings.Add(new DivisionSetting
        {
            Key = key,
            ValueJson = JsonSerializer.Serialize(clock.UtcNow),
            UpdatedAt = clock.UtcNow,
            UpdatedBy = 0,
        });

    /// <summary>Every <c>{ "$t": "key" }</c> in the tree becomes the text in each language.</summary>
    private void Translate(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, child) in obj.ToArray())
                {
                    if (Key(child) is { } key)
                    {
                        obj[name] = Resolve(key);
                    }
                    else if (child is not null)
                    {
                        Translate(child);
                    }
                }

                break;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (Key(array[index]) is { } key)
                    {
                        array[index] = Resolve(key);
                    }
                    else if (array[index] is { } child)
                    {
                        Translate(child);
                    }
                }

                break;

            default:
                break;
        }
    }

    private static string? Key(JsonNode? node) =>
        node is JsonObject marker
        && marker.Count == 1
        && marker[TranslationMarker] is JsonValue value
        && value.TryGetValue<string>(out var key)
            ? key
            : null;

    private JsonObject Resolve(string key)
    {
        var translated = new JsonObject();
        foreach (var locale in division.Value.Locales)
        {
            translated[locale] = catalog.Resolve(locale, key);
        }

        return translated;
    }

    private Localized<string> Translated(JsonNode marker) =>
        new(division.Value.Locales.Select(locale => KeyValuePair.Create(
            locale,
            Key(marker) is { } key ? catalog.Resolve(locale, key) : marker.ToString())));

    /// <summary>One template file. The body is opaque here too: validated as an envelope, never read.</summary>
    private sealed record ContentTemplateSeed(
        string Slug,
        ContentKind Kind,
        JsonNode Title,
        JsonNode? Summary,
        JsonNode? Body);

    /// <summary>
    /// One page file. <paramref name="PerDepartment"/> turns it into one row per department, which
    /// is what a dashboard is; <paramref name="Body"/> absent means "the template's body", which is
    /// how a dashboard says that its shape is the template's and not a second copy of it.
    /// </summary>
    private sealed record ContentPageSeed(
        string Slug,
        ContentKind Kind,
        string Template,
        Visibility Visibility,
        bool PerDepartment,
        ContentPageMenuSeed? Menu,
        JsonNode Title,
        JsonNode? Summary,
        JsonNode? Body);

    /// <summary>Where a seeded page sits in the menu of the site, when it sits in one at all.</summary>
    private sealed record ContentPageMenuSeed(MenuScope Scope, int Sort);
}
