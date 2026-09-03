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
}
