using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The walker is the only thing in the backend that reads a body of blocks. It has to work knowing
/// nothing about any block in particular, because the set of blocks grows with every module.
/// </summary>
public sealed class BlockDocumentWalkerTests
{
    private static readonly BlockDocumentWalker Walker = new(["it", "en"]);

    private const string Body = """
    {
      "schemaVersion": 1,
      "sections": [
        {
          "id": "s_hero",
          "key": "hero",
          "blocks": [
            { "id": "b_1", "type": "heading", "version": 1,
              "props": { "level": 1, "text": { "it": "Benvenuti", "en": "Welcome" } } },
            { "id": "b_2", "type": "cta", "version": 1,
              "props": { "label": { "it": "Inizia", "en": "Start" }, "href": "/start" } }
          ],
          "sections": [
            {
              "id": "s_nested",
              "blocks": [
                { "id": "b_3", "type": "text", "version": 1,
                  "props": { "markdown": { "it": "Testo annidato", "en": "Nested text" } } }
              ]
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void EnumeratesEveryBlockInReadingOrderIncludingNestedSections()
    {
        var blocks = Walker.EnumerateBlocks(JsonNode.Parse(Body)).ToArray();

        Assert.Equal(["b_1", "b_2", "b_3"], blocks.Select(block => block.Id));
        Assert.Equal(["heading", "cta", "text"], blocks.Select(block => block.Type));
        Assert.Equal("sections[0].blocks[0]", blocks[0].Path);
        Assert.Equal("sections[0].sections[0].blocks[0]", blocks[2].Path);
        Assert.Equal(2, blocks[2].Depth);
    }

    [Fact]
    public void ExtractsTheTextOfOneLanguageOnly()
    {
        var italian = Walker.ExtractText(JsonNode.Parse(Body), "it");
        var english = Walker.ExtractText(JsonNode.Parse(Body), "en");

        // Plain strings inside props belong to every language: an href is not translated.
        Assert.Equal("Benvenuti Inizia /start Testo annidato", italian);
        Assert.Equal("Welcome Start /start Nested text", english);

        // A row of the search index must not carry the other language, or every search would match
        // in every language.
        Assert.DoesNotContain("Welcome", italian, StringComparison.Ordinal);
        Assert.DoesNotContain("Benvenuti", english, StringComparison.Ordinal);
    }

    [Fact]
    public void AnObjectIsTranslatedOnlyWhenAllItsKeysAreLanguagesOfTheDivision()
    {
        var props = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "blocks": [ { "id": "b", "type": "text",
            "props": { "byFir": { "it": "Roma", "lirr": "Roma FIR" } } } ] } ]
        }
        """);

        // "lirr" is not a language, so the object is walked as a normal one and both values count.
        var text = Walker.ExtractText(props, "it");
        Assert.Contains("Roma FIR", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsAValidEnvelope()
    {
        var result = Walker.ValidateEnvelope(JsonNode.Parse(Body), ["heading", "cta", "text"]);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RefusesAnUnsupportedSchemaVersionAndAnUnknownBlockType()
    {
        var body = JsonNode.Parse("""
        { "schemaVersion": 2, "sections": [ { "id": "s", "blocks": [ { "id": "b", "type": "mystery" } ] } ] }
        """);

        var result = Walker.ValidateEnvelope(body, ["heading"]);

        Assert.Contains(result.Errors, error => error.Key == "errors.body.schemaVersion");
        Assert.Contains(result.Errors, error => error is { Key: "errors.body.blockTypeUnknown", Path: "sections[0].blocks[0]" });
    }

    [Fact]
    public void RefusesDuplicatedIdentifiersAndSectionsNestedTooDeeply()
    {
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "sections": [ { "id": "s", "sections": [ { "id": "s3",
            "sections": [ { "id": "s4" } ] } ] } ] } ]
        }
        """);

        var result = Walker.ValidateEnvelope(body);

        Assert.Contains(result.Errors, error => error.Key == "errors.body.idDuplicated");
        Assert.Contains(result.Errors, error => error is { Key: "errors.body.tooDeep", Path: "sections[0].sections[0].sections[0].sections[0]" });
    }

    [Fact]
    public void OnlyATemplateMayLockASectionOrRestrictItsBlocks()
    {
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "locked": true, "required": true, "allowedBlocks": ["text"] } ]
        }
        """);

        Assert.True(Walker.ValidateEnvelope(body, isTemplate: true).IsValid);

        var asPage = Walker.ValidateEnvelope(body, isTemplate: false);
        Assert.Equal(3, asPage.Errors.Count);
        Assert.All(asPage.Errors, error => Assert.Equal("errors.body.templateOnlyKey", error.Key));
    }

    [Fact]
    public void RefusesABodyThatIsNotAnObject()
    {
        var result = Walker.ValidateEnvelope(JsonNode.Parse("[]"));
        Assert.Equal([new BlockDocumentError("errors.body.notAnObject", "$")], result.Errors);
    }
}
