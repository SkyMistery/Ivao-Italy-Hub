using IvaoHub.Core.Division;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// A division file that would produce a broken site must stop the start up, with a message a
/// division that forks can act on (design M0 section 2.1).
/// </summary>
public sealed class DivisionOptionsValidationTests
{
    private static DivisionOptions Valid() => new()
    {
        Code = "XX",
        CountryId = "XX",
        Name = new Dictionary<string, string> { ["en"] = "IVAO Example" },
        Domain = "example.ivao.aero",
        Locales = ["en"],
        DefaultLocale = "en",
        Timezone = "UTC",
        IcaoPrefixes = ["XX"],
        SuperAdmins = [],
    };

    private static ValidateOptionsResult Validate(DivisionOptions options) =>
        new DivisionOptionsValidator().Validate(null, options);

    [Fact]
    public void AcceptsAMinimalForkedDivision()
    {
        Assert.True(Validate(Valid()).Succeeded);
    }

    [Fact]
    public void AcceptsAThreeLetterDivisionCode()
    {
        var options = Valid() with { Code = "XXX", CountryId = "XXX" };

        Assert.True(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("i")]
    [InlineData("it")]
    [InlineData("ITAL")]
    [InlineData("I1")]
    [InlineData("")]
    public void RejectsACodeThatIsNotTwoOrThreeUpperCaseLetters(string code)
    {
        var result = Validate(Valid() with { Code = code });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'code'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("li")]
    [InlineData("LIRRX")]
    [InlineData("L1")]
    public void RejectsAnIcaoPrefixThatIsNotOne(string prefix)
    {
        // Otherwise it is a field nobody looks at until the day it is wrong, and a lower case or
        // over long prefix simply matches nothing, in silence.
        var result = Validate(Valid() with { IcaoPrefixes = [prefix] });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'icaoPrefixes'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://hub.example.org")]
    [InlineData("hub.example.org/")]
    [InlineData("hub.example.org:8443")]
    [InlineData("hub example.org")]
    public void RejectsADomainThatIsNotAHostName(string domain)
    {
        // Every absolute link is "https://" + the domain, and since 27 September 2026 an
        // administrator can type it on the server as Installation:Domain: a scheme or a slash
        // written there would go out in every mail.
        var result = Validate(Valid() with { Domain = domain });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'domain'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("test.hub.example.org")]
    [InlineData("localhost")]
    public void AcceptsAHostName(string domain)
    {
        Assert.True(Validate(Valid() with { Domain = domain }).Succeeded);
    }

    [Fact]
    public void AcceptsNoIcaoPrefixAtAll()
    {
        Assert.True(Validate(Valid() with { IcaoPrefixes = [] }).Succeeded);
    }

    [Fact]
    public void RejectsADefaultLocaleThatIsNotInTheList()
    {
        var result = Validate(Valid() with { Locales = ["en"], DefaultLocale = "it" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'defaultLocale'", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsAnEmptyLocaleList()
    {
        var result = Validate(Valid() with { Locales = [], DefaultLocale = "en" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'locales'", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsALanguageThatTheDivisionNameDoesNotCover()
    {
        var result = Validate(Valid() with { Locales = ["en", "fr"] });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'fr'", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsATimeZoneThisMachineDoesNotKnow()
    {
        var result = Validate(Valid() with { Timezone = "Middle/Earth" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'timezone'", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvesTheDivisionNameWithFallback()
    {
        var options = Valid() with
        {
            Name = new Dictionary<string, string> { ["en"] = "IVAO Example" },
            Locales = ["en"],
            DefaultLocale = "en",
        };

        Assert.Equal("IVAO Example", options.Name[options.DefaultLocale]);
    }

    // The footer's tagline and "Built by" (note 2026-10-17-il-motto-e-chi-ha-costruito-il-sito): optional, and once written
    // written whole — a language left out, a name left out, a VID left out or written twice stop the start.

    [Fact]
    public void AcceptsATaglineInEveryLanguageAndTheBuilders()
    {
        var options = Valid() with
        {
            Locales = ["en", "fr"],
            Name = new Dictionary<string, string> { ["en"] = "IVAO Example", ["fr"] = "IVAO Exemple" },
            Tagline = new Dictionary<string, string> { ["en"] = "it takes time", ["fr"] = "il faut du temps" },
            BuiltBy = [new SiteBuilder { Name = "Ada", Vid = 100001 }, new SiteBuilder { Name = "Bo", Vid = 100002 }],
        };

        Assert.True(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void RejectsATaglineThatLeavesALanguageOut(string? french)
    {
        var tagline = new Dictionary<string, string> { ["en"] = "it takes time" };
        if (french is not null)
        {
            tagline["fr"] = french;
        }

        var result = Validate(Valid() with
        {
            Locales = ["en", "fr"],
            Name = new Dictionary<string, string> { ["en"] = "IVAO Example", ["fr"] = "IVAO Exemple" },
            Tagline = tagline,
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'tagline'", StringComparison.Ordinal) && failure.Contains("'fr'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", 100001, "'name'")]
    [InlineData("Ada", 0, "'vid'")]
    [InlineData("Ada", -5, "'vid'")]
    public void RejectsABuilderWithoutANameOrAVid(string name, int vid, string field)
    {
        var result = Validate(Valid() with { BuiltBy = [new SiteBuilder { Name = name, Vid = vid }] });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'builtBy[0]'", StringComparison.Ordinal) && failure.Contains(field, StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsTheSameBuilderTwice()
    {
        var result = Validate(Valid() with
        {
            BuiltBy = [new SiteBuilder { Name = "Ada", Vid = 100001 }, new SiteBuilder { Name = "Ada L.", Vid = 100001 }],
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("100001 twice", StringComparison.Ordinal));
    }
}
