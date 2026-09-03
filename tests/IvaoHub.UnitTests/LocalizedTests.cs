using System.Text.Json;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// A translated field is one JSON column and one object on the wire. These tests pin the three
/// things everything else relies on: how a language is resolved, what "complete" means before
/// publishing, and that a value survives a round trip through both converters unchanged.
/// </summary>
public sealed class LocalizedTests
{
    private static readonly DivisionOptions Division = new()
    {
        Code = "IT",
        Locales = ["it", "en"],
        DefaultLocale = "it",
    };

    [Fact]
    public void ResolveFallsBackFromTheLanguageToTheDefaultAndThenToWhateverExists()
    {
        var both = "Ciao".L("Hello");

        Assert.Equal("Hello", both.Resolve("en", "it"));
        Assert.Equal("Ciao", both.Resolve("it", "en"));

        // A language nobody wrote falls back to the default one...
        Assert.Equal("Ciao", both.Resolve("de", "it"));

        // ...and when even the default is missing, something is better than an empty page.
        var onlyEnglish = new Localized<string>([new KeyValuePair<string, string>("en", "Hello")]);
        Assert.Equal("Hello", onlyEnglish.Resolve("de", "it"));
        Assert.Null(Localized<string>.Empty.Resolve("it", "en"));
    }

    [Fact]
    public void HasAllTreatsAnEmptyTranslationAsMissing()
    {
        var blank = "Ciao".L(" ");

        Assert.False(blank.HasAll(Division.Locales));
        Assert.Equal(["en"], blank.MissingLocales(Division.Locales));
        Assert.True("Ciao".L("Hello").HasAll(Division.Locales));
    }

    [Fact]
    public void GetAndWithDoNotTouchTheOriginalValue()
    {
        var original = "Ciao".L("Hello");
        var updated = original.With("en", "Hi");

        Assert.Equal("Hello", original.Get("en"));
        Assert.Equal("Hi", updated.Get("en"));
        Assert.Null(original.Get("de"));
    }

    [Fact]
    public void TheDatabaseConverterKeepsTheKeysSortedSoTwoEqualValuesSerialiseTheSameWay()
    {
        var converter = new LocalizedConverter<string>();
        var toJson = converter.ConvertToProviderExpression.Compile();
        var fromJson = converter.ConvertFromProviderExpression.Compile();

        var written = toJson(new Localized<string>(
        [
            new KeyValuePair<string, string>("it", "Ciao"),
            new KeyValuePair<string, string>("en", "Hello"),
        ]));

        Assert.Equal("""{"en":"Hello","it":"Ciao"}""", written);
        Assert.Equal("Ciao".L("Hello"), fromJson(written));
        Assert.Equal(Localized<string>.Empty, fromJson(string.Empty));
    }

    [Fact]
    public void TheApiConverterSendsAndAcceptsThePlainObject()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new LocalizedJsonConverterFactory());

        var json = JsonSerializer.Serialize("Ciao".L("Hello"), options);
        Assert.Equal("""{"en":"Hello","it":"Ciao"}""", json);

        var back = JsonSerializer.Deserialize<Localized<string>>(json, options);
        Assert.Equal("Ciao".L("Hello"), back);

        // A field the client left out is an empty value, never null: no caller has to guard.
        Assert.Equal(Localized<string>.Empty, JsonSerializer.Deserialize<Localized<string>>("null", options));
    }

    [Fact]
    public void AFieldThatIsItselfOptionalTravelsAsNullAndNotAsAnEmptyObject()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new LocalizedJsonConverterFactory());

        // Two different absences. A missing language inside the value is empty, so that reading it
        // never needs a guard; a whole field declared optional and never written is null, because
        // "nobody wrote a description" is not "the description is blank" — and because that is what
        // the generated contract declares. See docs/internal/decisions/2026-09-03-localized-nullable-nelle-api.md.
        Assert.Equal("""{"description":null}""", JsonSerializer.Serialize(new Optional(null), options));
        Assert.Equal("""{"description":{}}""", JsonSerializer.Serialize(new Optional(Localized<string>.Empty), options));
    }

    private sealed record Optional(Localized<string>? Description);
}
