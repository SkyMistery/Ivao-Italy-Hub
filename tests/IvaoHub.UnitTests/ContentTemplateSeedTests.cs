using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The system templates are data, and data can be wrong: a block type that does not exist, two
/// sections sharing an identifier, a column in a layout that has one. None of it would be noticed
/// until a coordinator opened the template picker on a real installation, because the seeder writes
/// straight to the database and does not go through the write endpoint.
/// <para>So the files are checked here, by the same walker the API uses, against the same registry.
/// A seed that would be refused if somebody pasted it into the editor never ships.</para>
/// </summary>
public sealed class ContentTemplateSeedTests
{
    private static readonly BlockDocumentWalker Walker = new(["it", "en"]);

    public static TheoryData<string> SeedFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(SeedDirectory(), "*.json"))
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    [Fact]
    public void ThereAreTemplatesToSeed()
    {
        // Guards the theory below: a glob that matches nothing passes every test it feeds.
        Assert.NotEmpty(Directory.EnumerateFiles(SeedDirectory(), "*.json"));
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void EverySeededTemplateIsAValidTemplateEnvelope(string name)
    {
        var seed = JsonNode.Parse(File.ReadAllText(Path.Combine(SeedDirectory(), name)))!.AsObject();

        Assert.False(string.IsNullOrWhiteSpace(seed["slug"]?.GetValue<string>()));
        Assert.True(
            Enum.TryParse<ContentKind>(seed["kind"]?.GetValue<string>(), out _),
            $"{name} does not name a content kind the hub knows.");

        var validation = Walker.ValidateEnvelope(
            seed["body"],
            [.. CoreBlocks.All.Select(block => block.Type)],
            isTemplate: true);

        Assert.True(
            validation.IsValid,
            $"{name}: {string.Join(", ", validation.Errors.Select(error => $"{error.Path} {error.Key}"))}");
    }

    [Theory]
    [MemberData(nameof(SeedFiles))]
    public void ASeedCarriesTranslationKeysAndNeverATranslationOfItsOwn(string name)
    {
        // A fork does not read Italian. The seeder resolves `{ "$t": … }` into the languages the
        // division actually speaks, so a `{ "it": …, "en": … }` written into a seed file would be
        // the one thing that arrives in Italian whatever the fork does (docs/FORKING.md).
        var seed = JsonNode.Parse(File.ReadAllText(Path.Combine(SeedDirectory(), name)))!.AsObject();

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

    /// <summary>An object in a seed is a translation key or it is a mistake.</summary>
    private static void AssertTranslationKeys(JsonNode? value, string what)
    {
        if (value is not JsonObject candidate)
        {
            return;
        }

        Assert.True(candidate.Count == 1 && candidate["$t"] is not null, $"{what} should be a translation key.");
    }

    /// <summary>
    /// The seeds sit at the root of the repository, next to <c>locales/</c>, because they are files
    /// of the installation and not of a project.
    /// </summary>
    private static string SeedDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "seed", "content-templates");
    }
}
