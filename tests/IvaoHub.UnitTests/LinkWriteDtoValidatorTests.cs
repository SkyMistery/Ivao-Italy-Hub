using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the guinea pig entity. They are here because the CRUD engine does not validate:
/// it asks the validator of the payload and turns the failures into problem details, which is what
/// keeps "the server validates, the client shows" true for every resource (plan section 16.6).
/// </summary>
public sealed class LinkWriteDtoValidatorTests
{
    private static readonly DivisionOptions Division = new()
    {
        Code = "IT",
        CountryId = "IT",
        Domain = "it.ivao.aero",
        Timezone = "Europe/Rome",
        Locales = ["it", "en"],
        DefaultLocale = "it",
    };

    private static readonly LinkWriteDtoValidator Validator = new(Options.Create(Division));

    private static LinkWriteDto Payload(
        string url = "https://it.ivao.aero/discord",
        Localized<string>? title = null,
        int sort = 0,
        string? category = null) =>
        new(
            Department.ED,
            Visibility.Public,
            title ?? "Invito Discord".L("Discord invitation"),
            url,
            Description: null,
            category,
            sort,
            IsActive: true,
            RowVersion: default);

    [Fact]
    public void APayloadInEveryLanguageWithAWebAddressIsValid()
    {
        Assert.True(Validator.Validate(Payload()).IsValid);
    }

    [Fact]
    public void AMissingLanguageNamesItselfInTheFailure()
    {
        var incomplete = new Localized<string>([new KeyValuePair<string, string>("it", "Invito Discord")]);

        var result = Validator.Validate(Payload(title: incomplete));

        var failure = Assert.Single(result.Errors, error => error.PropertyName == nameof(LinkWriteDto.Title));
        Assert.Equal(LocalizedRules.MissingMessageKey, failure.ErrorMessage);

        // What is missing has to travel with the failure, or the form can only say "invalid".
        var missing = Assert.IsType<LocalizedMissing>(failure.CustomState);
        Assert.Equal(["en"], missing.Locales);
    }

    [Fact]
    public void ABlankTranslationCountsAsMissing()
    {
        var blank = new Localized<string>(
        [
            new KeyValuePair<string, string>("it", "Invito Discord"),
            new KeyValuePair<string, string>("en", "   "),
        ]);

        Assert.False(Validator.Validate(Payload(title: blank)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/discord")]
    [InlineData("it.ivao.aero/discord")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://it.ivao.aero/discord")]
    public void AnAddressABrowserWouldNotFollowIsRefused(string url)
    {
        // A link leaves the site: it needs a scheme, and only the two a browser follows.
        Assert.False(Validator.Validate(Payload(url: url)).IsValid);
    }

    [Theory]
    [InlineData("http://it.ivao.aero/discord")]
    [InlineData("https://it.ivao.aero/discord")]
    public void BothWebSchemesAreAccepted(string url)
    {
        Assert.True(Validator.Validate(Payload(url: url)).IsValid);
    }

    [Fact]
    public void ANegativeSortIsRefusedAndTheMessageIsAKey()
    {
        var result = Validator.Validate(Payload(sort: -1));

        var failure = Assert.Single(result.Errors, error => error.PropertyName == nameof(LinkWriteDto.Sort));
        Assert.Equal("errors.number.min", failure.ErrorMessage);
    }

    [Fact]
    public void TextLongerThanItsColumnIsRefusedBeforeTheDatabaseTruncatesIt()
    {
        var longUrl = "https://it.ivao.aero/" + new string('a', LinkWriteDtoValidator.MaxUrlLength);
        Assert.False(Validator.Validate(Payload(url: longUrl)).IsValid);

        Assert.False(Validator.Validate(Payload(category: new string('a', LinkWriteDtoValidator.MaxCategoryLength + 1))).IsValid);
    }

    [Fact]
    public void EveryMessageIsATranslationKeyAndNeverASentence()
    {
        var result = Validator.Validate(new LinkWriteDto(
            Department.ED,
            Visibility.Public,
            Localized<string>.Empty,
            string.Empty,
            Description: null,
            Category: null,
            Sort: -5,
            IsActive: true,
            RowVersion: default));

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.StartsWith("errors.", error.ErrorMessage, StringComparison.Ordinal));
    }
}
