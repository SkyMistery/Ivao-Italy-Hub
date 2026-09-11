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

        // ⚠️ `/start` is not in either of them, and that is the rule and not an oversight: what is
        // indexed is the prose inside a translated map, so a bare string — an href here, an
        // enumeration elsewhere — never reaches the index.
        Assert.Equal("Benvenuti Inizia Testo annidato", italian);
        Assert.Equal("Welcome Start Nested text", english);

        // A row of the search index must not carry the other language, or every search would match
        // in every language.
        Assert.DoesNotContain("Welcome", italian, StringComparison.Ordinal);
        Assert.DoesNotContain("Benvenuti", english, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheProseOfABlockIsIndexed()
    {
        // ⚠️ The defect this rule exists for, in the shape it was found in: the snippet of `/start`
        // read "… quattro semplici passi. left muted Prima di tutto…" — `align` and `tone` of the
        // `hero` block, two enumerations stored as strings, sitting in the middle of a sentence.
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "blocks": [ { "id": "b", "type": "hero", "version": 1,
            "props": {
              "title": { "it": "Quattro semplici passi", "en": "Four simple steps" },
              "align": "left",
              "tone": "muted",
              "mediaId": 7,
              "href": "https://example.org/somewhere"
            } } ] } ]
        }
        """);

        var text = Walker.ExtractText(body, "it");

        Assert.Equal("Quattro semplici passi", text);
        Assert.DoesNotContain("left", text, StringComparison.Ordinal);
        Assert.DoesNotContain("muted", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example.org", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMarkdownComesBackOutOfTheProse()
    {
        // A reader of a snippet was shown the asterisks, and somebody searching for a word could
        // miss it for one stuck to its front. The renderer owns Markdown; this only undoes what an
        // editor can write.
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "blocks": [ { "id": "b", "type": "text", "version": 1,
            "props": { "markdown": { "it": "## Chi siamo\n\n**IVAO Italia** è la _community_ dei [piloti](/pilots), con `codice` e:\n\n- un primo punto\n- un secondo" } } } ] } ]
        }
        """);

        var text = Walker.ExtractText(body, "it");

        Assert.Equal(
            "Chi siamo IVAO Italia è la _community_ dei piloti, con codice e: un primo punto un secondo",
            text);
    }

    [Theory]
    // A hyphen inside a sentence is a hyphen, and only the start of a line carries a marker.
    [InlineData("Un titolo mezzo-lungo", "Un titolo mezzo-lungo")]
    // `snake_case` is a word: underscores are deliberately left alone.
    [InlineData("La colonna owner_department", "La colonna owner_department")]
    // A picture keeps what a reader would have been told, and loses the address.
    [InlineData("![Il logo](/media/7.png) sopra", "Il logo sopra")]
    [InlineData("1. primo\n2. secondo", "primo secondo")]
    public void ProseUndoesOnlyWhatAnEditorCanWrite(string markdown, string expected) =>
        Assert.Equal(expected, OneParagraph(markdown));

    /// <summary>
    /// One block holding one translated paragraph, extracted. Asked through the public way in and
    /// not of the helper behind it: what the index holds is the thing under test.
    /// </summary>
    private static string OneParagraph(string markdown)
    {
        var body = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sections"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "s",
                    ["blocks"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "b",
                            ["type"] = "text",
                            ["props"] = new JsonObject
                            {
                                ["markdown"] = new JsonObject { ["it"] = markdown, ["en"] = markdown },
                            },
                        },
                    },
                },
            },
        };

        return Walker.ExtractText(body, "it");
    }

    [Fact]
    public void AnObjectIsTranslatedOnlyWhenAllItsKeysAreLanguagesOfTheDivision()
    {
        var props = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "blocks": [ { "id": "b", "type": "text",
            "props": { "byFir": {
              "it": { "it": "Roma", "en": "Rome" },
              "lirr": { "it": "Roma FIR", "en": "Rome FIR" } } } } ] } ]
        }
        """);

        // "lirr" is not a language, so the object is walked as a normal one and both values count —
        // which is why each of them is itself a translated map here: a bare string would not be
        // indexed at all, and this test is about the detection and not about the rule above it.
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
            "sections": [ { "id": "s4", "sections": [ { "id": "s5" } ] } ] } ] } ] } ]
        }
        """);

        var result = Walker.ValidateEnvelope(body);

        Assert.Contains(result.Errors, error => error.Key == "errors.body.idDuplicated");
        // Four levels are allowed since 11 September 2026; the fifth is the one refused.
        Assert.DoesNotContain(result.Errors, error => error is { Key: "errors.body.tooDeep", Path: "sections[0].sections[0].sections[0].sections[0]" });
        Assert.Contains(result.Errors, error => error is { Key: "errors.body.tooDeep", Path: "sections[0].sections[0].sections[0].sections[0].sections[0]" });
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

    [Fact]
    public void RefusesALayoutAndARenderModeItDoesNotKnow()
    {
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "layout": "1/4+3/4", "blocks": [
            { "id": "b", "type": "text", "renderMode": "cached" } ] } ]
        }
        """);

        var result = Walker.ValidateEnvelope(body, ["text"]);

        Assert.Contains(result.Errors, error => error is { Key: "errors.body.layoutUnknown", Path: "sections[0].layout" });
        Assert.Contains(
            result.Errors,
            error => error is { Key: "errors.body.renderModeUnknown", Path: "sections[0].blocks[0].renderMode" });
    }

    [Fact]
    public void RefusesABackgroundItDoesNotKnow()
    {
        // The fourth background arrived in G3 (`image`, with a picture of the library behind it),
        // and the check arrived with it: a value nobody refuses is drawn by the renderer as no
        // background at all, so the section would quietly lose its ground on the published page.
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [
            { "id": "s1", "background": "gradient", "blocks": [] },
            { "id": "s2", "background": "image", "mediaId": 7, "blocks": [] }
          ]
        }
        """);

        var result = Walker.ValidateEnvelope(body, ["text"]);

        Assert.Equal(
            [new BlockDocumentError("errors.body.backgroundUnknown", "sections[0].background")],
            result.Errors);
    }

    [Theory]
    [InlineData("stacked", 1)]
    [InlineData("1/2+1/2", 2)]
    [InlineData("2/3+1/3", 2)]
    [InlineData("3x1/3", 3)]
    public void ABlockMayOnlyClaimAColumnItsSectionHas(string layout, int columns)
    {
        // The last column of the layout is fine; the one after it is not, and that is the whole
        // rule: a block in a column that does not exist would be drawn nowhere.
        Assert.True(Walker.ValidateEnvelope(Column(layout, columns - 1), ["text"]).IsValid);

        var beyond = Walker.ValidateEnvelope(Column(layout, columns), ["text"]);
        Assert.Contains(
            beyond.Errors,
            error => error is { Key: "errors.body.columnOutOfRange", Path: "sections[0].blocks[0].column" });

        var negative = Walker.ValidateEnvelope(Column(layout, -1), ["text"]);
        Assert.Contains(negative.Errors, error => error.Key == "errors.body.columnOutOfRange");
    }

    [Fact]
    public void NamesEveryTranslatedValueInsideTheBlocksThatIsNotWrittenInEveryLanguage()
    {
        var body = JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "sections": [ { "id": "s", "blocks": [
            { "id": "b_1", "type": "callout", "props": {
                "tone": "info",
                "title": { "it": "Attenzione", "en": "Careful" },
                "text": { "it": "Solo in italiano" } } },
            { "id": "b_2", "type": "heading", "props": {
                "level": 2,
                "text": { "it": "  ", "en": "Only English" } } } ] } ]
        }
        """);

        var missing = Walker.MissingLocales(body);

        // A value written in both languages is not reported, a whitespace one is: a page whose
        // English title is three spaces is not a page that has been translated.
        Assert.Equal(
            ["sections[0].blocks[0].props.text", "sections[0].blocks[1].props.text"],
            missing.Select(gap => gap.Path));
        Assert.Equal(["en"], missing[0].Locales);
        Assert.Equal(["it"], missing[1].Locales);
    }

    [Fact]
    public void ATranslatedValueThatIsCompleteIsNotReported()
    {
        // The body of the other tests is written in both languages throughout, which is exactly
        // what publication is allowed to let through.
        Assert.Empty(Walker.MissingLocales(JsonNode.Parse(Body)));
    }

    private static JsonNode? Column(string layout, int column) => JsonNode.Parse($$"""
    {
      "schemaVersion": 1,
      "sections": [ { "id": "s", "layout": "{{layout}}", "blocks": [
        { "id": "b", "type": "text", "column": {{column}} } ] } ]
    }
    """);
}
