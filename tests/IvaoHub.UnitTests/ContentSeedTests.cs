using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The seeds are data, and data can be wrong: a block type that does not exist, two sections
/// sharing an identifier, a column in a layout that has one, a translation key nobody ever wrote.
/// None of it would be noticed until somebody opened a real installation, because the seeder writes
/// straight to the database and does not go through the write endpoint.
/// <para>So the files are checked here, by the same walker the API uses, against the same registry
/// and against the language files themselves. A seed that would be refused if somebody pasted it
/// into the editor never ships.</para>
/// <para>Both folders, on the same terms: the templates of design M0 section 5.6 and the pages of
/// design M1 section 8.2. The one rule that differs between them is the template only keys — a page
/// carrying <c>locked</c> would be a page able to lift its own restrictions.</para>
/// </summary>
public sealed class ContentSeedTests
{
    private const string Templates = "content-templates";
    private const string Pages = "content-pages";

    private static readonly BlockDocumentWalker Walker = new(["it", "en"]);

    /// <summary>Every seed file of both folders, named by the folder it came from.</summary>
    public static TheoryData<string, string> SeedFiles()
    {
        var data = new TheoryData<string, string>();
        foreach (var folder in new[] { Templates, Pages })
        {
            foreach (var file in Directory.EnumerateFiles(SeedDirectory(folder), "*.json"))
            {
                data.Add(folder, Path.GetFileName(file));
            }
        }

        return data;
    }

    [Fact]
    public void ThereAreTemplatesAndPagesToSeed()
    {
        // Guards the theories below: a glob that matches nothing passes every test it feeds.
        Assert.NotEmpty(Directory.EnumerateFiles(SeedDirectory(Templates), "*.json"));
        Assert.NotEmpty(Directory.EnumerateFiles(SeedDirectory(Pages), "*.json"));
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void EverySeedIsAValidEnvelope(string folder, string name)
    {
        var seed = Read(folder, name);
        var isTemplate = folder == Templates;

        Assert.False(string.IsNullOrWhiteSpace(seed["slug"]?.GetValue<string>()));
        Assert.True(
            Enum.TryParse<ContentKind>(seed["kind"]?.GetValue<string>(), out _),
            $"{name} does not name a content kind the hub knows.");

        if (seed["body"] is null)
        {
            // A page with no body of its own is a copy of its template's, and the template's body
            // is checked by this very test one file away.
            Assert.False(isTemplate, $"{name} is a template and a template has to carry a body.");
            return;
        }

        var validation = Walker.ValidateEnvelope(
            seed["body"],
            [.. CoreBlocks.All.Select(block => block.Type)],
            isTemplate);

        Assert.True(
            validation.IsValid,
            $"{name}: {string.Join(", ", validation.Errors.Select(error => $"{error.Path} {error.Key}"))}");
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void ASeedCarriesTranslationKeysAndNeverATranslationOfItsOwn(string folder, string name)
    {
        // A fork does not read Italian. The seeder resolves `{ "$t": … }` into the languages the
        // division actually speaks, so a `{ "it": …, "en": … }` written into a seed file would be
        // the one thing that arrives in Italian whatever the fork does (docs/FORKING.md).
        var seed = Read(folder, name);

        AssertTranslationKeys(seed["title"], $"{name}: title");
        AssertTranslationKeys(seed["summary"], $"{name}: summary");

        foreach (var section in Walker.EnumerateSections(seed["body"]))
        {
            AssertTranslationKeys(section.Node["title"], $"{name}: {section.Path}.title");
        }

        foreach (var block in Walker.EnumerateBlocks(seed["body"]))
        {
            foreach (var (property, value) in block.Node["props"]?.AsObject() ?? [])
            {
                AssertTranslationKeys(value, $"{name}: {block.Path}.props.{property}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void EveryKeyASeedNamesIsWrittenInEveryLanguage(string folder, string name)
    {
        // The other half of the rule above, and the half that actually bites: a `$t` pointing at a
        // key nobody wrote seeds a page whose heading is the string "seed.pages.home.hero.title",
        // in every language at once, and no test of the shape of the file would notice. It is
        // `pnpm i18n:check` for the seeds, which that script cannot do because these keys are not
        // written in the source of the client.
        var seed = Read(folder, name);
        var missing = new List<string>();

        foreach (var key in Keys(seed))
        {
            foreach (var (locale, catalogue) in Catalogues())
            {
                if (!catalogue.Contains(key))
                {
                    missing.Add($"{key} ({locale})");
                }
            }
        }

        Assert.True(missing.Count == 0, $"{name} names keys nobody wrote: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryPageSeedIsBornFromATemplateThatShips()
    {
        // A page naming a template that is not in the package is a page the seeder skips and says
        // so about — which is the right behaviour at run time and a mistake to ship.
        var templates = Directory
            .EnumerateFiles(SeedDirectory(Templates), "*.json")
            .Select(file => Read(Templates, Path.GetFileName(file))["slug"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(SeedDirectory(Pages), "*.json"))
        {
            var name = Path.GetFileName(file);
            var template = Read(Pages, name)["template"]?.GetValue<string>();

            Assert.True(
                template is not null && templates.Contains(template),
                $"{name} is born from the template \"{template}\", which no seed file ships.");
        }
    }

    private static JsonObject Read(string folder, string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(SeedDirectory(folder), name)))!.AsObject();

    /// <summary>Every translation key the file names, at any depth.</summary>
    private static IEnumerable<string> Keys(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.Count == 1 && obj["$t"] is JsonValue marker && marker.TryGetValue<string>(out var key))
                {
                    yield return key;
                    break;
                }

                foreach (var child in obj.SelectMany(pair => Keys(pair.Value)))
                {
                    yield return child;
                }

                break;

            case JsonArray array:
                foreach (var child in array.SelectMany(Keys))
                {
                    yield return child;
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// The keys each language file holds, flattened the way the catalogue of the server flattens
    /// them. Read here rather than through <c>LocaleCatalog</c> because a catalogue answers with the
    /// default language when a key is missing, and "missing in Italian" is exactly the failure this
    /// test is looking for.
    /// </summary>
    private static IEnumerable<(string Locale, HashSet<string> Keys)> Catalogues()
    {
        foreach (var directory in Directory.EnumerateDirectories(Path.Combine(RepositoryRoot(), "locales")))
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                Flatten(JsonNode.Parse(File.ReadAllText(file)), prefix: string.Empty, keys);
            }

            yield return (Path.GetFileName(directory), keys);
        }
    }

    private static void Flatten(JsonNode? node, string prefix, HashSet<string> keys)
    {
        if (node is not JsonObject obj)
        {
            if (prefix.Length > 0)
            {
                keys.Add(prefix);
            }

            return;
        }

        foreach (var (name, value) in obj)
        {
            Flatten(value, prefix.Length == 0 ? name : $"{prefix}.{name}", keys);
        }
    }

    /// <summary>
    /// A translated value in a seed is a key and never the words themselves, at any depth.
    /// <para>What it is looking for is an object whose keys are all languages of the division —
    /// that, and only that, is a translation written into the file, and it is the same question
    /// the walker asks of a body. Everything else is structure and is walked into: the pair of
    /// links a hero carries is an object too, and it is not a mistake.</para>
    /// </summary>
    private static void AssertTranslationKeys(JsonNode? value, string what)
    {
        switch (value)
        {
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    AssertTranslationKeys(array[index], $"{what}[{index}]");
                }

                return;

            case JsonObject candidate:
                if (candidate.Count == 1 && candidate["$t"] is not null)
                {
                    return;
                }

                Assert.False(
                    Walker.IsLocalizedObject(candidate),
                    $"{what} is written out in a language instead of being a translation key.");

                foreach (var (name, child) in candidate)
                {
                    AssertTranslationKeys(child, $"{what}.{name}");
                }

                return;

            default:
                return;
        }
    }

    /// <summary>
    /// The seeds sit at the root of the repository, next to <c>locales/</c>, because they are files
    /// of the installation and not of a project.
    /// </summary>
    private static string SeedDirectory(string folder) =>
        Path.Combine(RepositoryRoot(), "seed", folder);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
