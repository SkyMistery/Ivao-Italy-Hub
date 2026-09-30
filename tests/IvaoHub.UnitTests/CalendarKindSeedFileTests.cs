using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The seed of the calendar's words is data, and data can be wrong (M4, E1, note
/// <c>decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md</c>). The seeder writes it straight into the table,
/// past the validator of the back office — and the validator reads the key again at every save, so a word it would refuse
/// is a word the web team can no longer save once it is there, not even to change its colour. The design's
/// <c>onlineDay</c> was one: the key of a kind has the shape of a slug.
/// <para>So every word of <c>seed/calendar-kinds/</c> goes through that validator here, with its label resolved from the
/// language files of every language this repository ships, as <see cref="ContentSeedTests"/> does for the templates and
/// the pages.</para>
/// </summary>
public sealed class CalendarKindSeedFileTests
{
    /// <summary>The words of the events (M4), which every installation is born with since E1.</summary>
    private static readonly string[] TheEvents = ["rfe", "rfo", "mse", "online-day"];

    private static readonly HubPaths Paths = HubPaths.Resolve(AppContext.BaseDirectory);

    [Fact]
    public void EveryWordOfTheSeedIsOneTheBackOfficeWouldSave()
    {
        var catalogues = Catalogues();
        var validator = new CalendarKindWriteDtoValidator(Options.Create(new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Domain = "example.org",
            Timezone = "UTC",
            Locales = [.. catalogues.Keys],
            DefaultLocale = "en",
        }));

        var kinds = Kinds();
        Assert.NotEmpty(kinds);

        foreach (var kind in kinds)
        {
            var key = kind["key"]?.GetValue<string>() ?? string.Empty;

            // A key and never the words: a fork gets the label in its own languages (docs/FORKING.md).
            var label = kind["label"]?["$t"]?.GetValue<string>();
            Assert.True(label is not null, $"The calendar kind \"{key}\" carries its label as words instead of a translation key.");

            // Only the languages that have the key: a language that does not is what the validator is asked about.
            var payload = new CalendarKindWriteDto(
                key,
                new Localized<string>(catalogues
                    .Where(catalogue => catalogue.Value.ContainsKey(label))
                    .Select(catalogue => KeyValuePair.Create(catalogue.Key, catalogue.Value[label]))),
                kind["colour"]?.GetValue<string>() ?? string.Empty,
                kind["sort"]?.GetValue<int>() ?? -1,
                IsActive: true,
                RowVersion: default);

            var result = validator.Validate(payload);
            Assert.True(
                result.IsValid,
                $"The back office would refuse the calendar kind \"{key}\": "
                + string.Join(", ", result.Errors.Select(error => $"{error.PropertyName} {error.ErrorMessage}")));
        }

        // Once each, as the column compares them: the database ignores case, and the seed would skip the second one.
        Assert.Equal(
            kinds.Count,
            kinds.Select(kind => kind["key"]?.GetValue<string>()).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TheWordsOfTheEventsAreInTheSeed()
    {
        // The type of an event is one of these words (note 2026-09-29-i-tipi-di-evento): without them, the form of an
        // event would have nothing of the events to choose from.
        var keys = Kinds().Select(kind => kind["key"]?.GetValue<string>()).ToList();

        Assert.All(TheEvents, key => Assert.Contains(key, keys));
    }

    /// <summary>Every word of every file of the folder: the seeder reads them all.</summary>
    private static List<JsonNode> Kinds() =>
    [
        .. Directory.EnumerateFiles(Path.Combine(Paths.Seed, "calendar-kinds"), "*.json")
            .SelectMany(file => JsonNode.Parse(File.ReadAllText(file))?["kinds"]?.AsArray() ?? new JsonArray())
            .OfType<JsonNode>(),
    ];

    /// <summary>
    /// Each language of the repository, as the keys its files hold and their words. Read here rather than through
    /// <c>LocaleCatalog</c>, which answers with the default language when a key is missing — and "missing in one
    /// language" is one of the failures this test is looking for.
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>> Catalogues()
    {
        var catalogues = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var directory in Directory.EnumerateDirectories(Paths.Locales))
        {
            var words = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                Flatten(JsonNode.Parse(File.ReadAllText(file)), prefix: string.Empty, words);
            }

            catalogues[Path.GetFileName(directory)] = words;
        }

        return catalogues;
    }

    private static void Flatten(JsonNode? node, string prefix, Dictionary<string, string> words)
    {
        if (node is JsonObject obj)
        {
            foreach (var (name, child) in obj)
            {
                Flatten(child, prefix.Length == 0 ? name : $"{prefix}.{name}", words);
            }

            return;
        }

        if (prefix.Length > 0 && node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            words[prefix] = text;
        }
    }
}
