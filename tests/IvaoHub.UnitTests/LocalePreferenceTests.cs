using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// Which language a member starts in: the one they chose on IVAO when the division speaks it,
/// English otherwise. English is the fallback because it is the language of IVAO and of this
/// project, not because this division happens to speak it.
/// </summary>
public sealed class LocalePreferenceTests
{
    private static DivisionOptions Division(params string[] locales) => new()
    {
        Code = "XX",
        CountryId = "XX",
        Domain = "example.ivao.aero",
        Locales = locales,
        DefaultLocale = locales[0],
        Timezone = "UTC",
    };

    [Fact]
    public void UsesTheLanguageTheMemberHasOnIvaoWhenTheDivisionSpeaksIt()
    {
        Assert.Equal("it", LocalePreference.Resolve("it", Division("it", "en")));
    }

    [Fact]
    public void FallsBackToEnglishAndNotToTheLanguageOfTheDivision()
    {
        // The division's own default is Italian; a member whose IVAO language is German gets
        // English, because that is the language everyone at IVAO has in common.
        Assert.Equal("en", LocalePreference.Resolve("de", Division("it", "en")));
    }

    [Fact]
    public void FallsBackToEnglishWhenIvaoSaysNothing()
    {
        Assert.Equal("en", LocalePreference.Resolve(null, Division("it", "en")));
        Assert.Equal("en", LocalePreference.Resolve("   ", Division("it", "en")));
    }

    [Fact]
    public void AcceptsARegionalTag()
    {
        // IVAO and browsers both hand out things like en-GB and pt-BR.
        Assert.Equal("en", LocalePreference.Resolve("en-GB", Division("it", "en")));
        Assert.Equal("it", LocalePreference.Resolve("it-IT", Division("it", "en")));
    }

    [Fact]
    public void IsNotFussyAboutCase()
    {
        Assert.Equal("it", LocalePreference.Resolve("IT", Division("it", "en")));
    }

    [Fact]
    public void FallsBackToTheDivisionOnlyWhenItDoesNotSpeakEnglishAtAll()
    {
        // A fork that ships no English at all still has to answer with something it can render.
        Assert.Equal("fr", LocalePreference.Resolve("de", Division("fr")));
        Assert.Equal("fr", LocalePreference.Resolve(null, Division("fr")));
    }

    [Fact]
    public void EnglishStillWinsOverTheDefaultOfADivisionThatSpeaksBoth()
    {
        var division = Division("fr", "en");

        Assert.Equal("en", LocalePreference.Resolve("ja", division));
        Assert.Equal("fr", division.DefaultLocale);
    }

    [Theory]
    [InlineData("it", "it")]
    [InlineData("EN", "en")]
    [InlineData("en-US", "en")]
    [InlineData("de", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void SpokenAnswersHowTheDivisionSpellsALanguage(string? language, string? expected)
    {
        Assert.Equal(expected, LocalePreference.Spoken(Division("it", "en"), language));
    }
}
