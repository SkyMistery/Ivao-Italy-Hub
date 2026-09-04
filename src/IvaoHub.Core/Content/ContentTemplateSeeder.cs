using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// The templates a fresh installation starts with, read from <c>seed/content-templates/*.json</c>
/// (design M0 section 5.6).
/// <para>Each file is applied <b>once</b>, remembered by the key <c>template.system:&lt;slug&gt;</c>
/// in <c>hub_division_settings</c>. That is the whole point of the key: a later release can add a
/// template without touching one the staff has since edited, and reinstalling does not undo their
/// work. Nothing here ever updates an existing row.</para>
/// <para>The text of a template is not in the file. The file carries translation keys, spelled
/// <c>{ "$t": "seed.templates…" }</c>, and they are resolved here into the languages the division
/// actually speaks — so a division running in English alone gets a template in English, with no
/// Italian in it (docs/FORKING.md).</para>
/// </summary>
public sealed class ContentTemplateSeeder(
    HubDbContext database,
    HubPaths paths,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<ContentTemplateSeeder> logger)
{
    /// <summary>Where the setting that remembers an applied file lives.</summary>
    public const string SettingPrefix = "template.system:";

    /// <summary>Templates belong to the web team: they are a tool of the site, not of a department.</summary>
    public const Department Owner = Department.WD;

    /// <summary>The marker that makes a string in a seed file a translation key.</summary>
    private const string TranslationMarker = "$t";

    /// <summary>A seed file spells a kind as its name, the way the API does.</summary>
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(paths.Seed, "content-templates");
        if (!Directory.Exists(directory))
        {
            // A package without the seed folder is a package that starts with no templates, not a
            // package that refuses to start.
            logger.LogWarning("No content template seed directory at {Directory}.", directory);
            return;
        }

        var applied = await database.DivisionSettings
            .AsNoTracking()
            .Where(setting => setting.Key.StartsWith(SettingPrefix))
            .Select(setting => setting.Key)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(applied, StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(name => name, StringComparer.Ordinal))
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
            if (!known.Add(key))
            {
                continue;
            }

            database.Contents.Add(ToEntity(seed));
            database.DivisionSettings.Add(new DivisionSetting
            {
                Key = key,
                ValueJson = JsonSerializer.Serialize(clock.UtcNow),
                UpdatedAt = clock.UtcNow,
                UpdatedBy = 0,
            });

            logger.LogInformation("Seeded the content template {Slug}.", seed.Slug);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private ContentEntry ToEntity(ContentTemplateSeed seed)
    {
        var body = seed.Body?.DeepClone() ?? new JsonObject();
        Translate(body);

        return new ContentEntry
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
        };
    }

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

    /// <summary>One seed file. The body is opaque here too: it is validated as an envelope, never read.</summary>
    private sealed record ContentTemplateSeed(
        string Slug,
        ContentKind Kind,
        JsonNode Title,
        JsonNode? Summary,
        JsonNode? Body);
}
