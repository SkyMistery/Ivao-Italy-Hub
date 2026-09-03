using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The language files as the back end reads them. There is one set of them for the whole product,
/// and the server has to be able to reach it: the titles of the problem details answers come from
/// there today, the mail templates will in M1 (plan section 16.8).
/// </summary>
public sealed class LocaleCatalogTests
{
    private static LocaleCatalog Catalog(params string[] locales) => new(
        HubPaths.Resolve(AppContext.BaseDirectory),
        Options.Create(new DivisionOptions
        {
            Code = "IT",
            CountryId = "IT",
            Domain = "it.ivao.aero",
            Timezone = "Europe/Rome",
            Locales = locales,
            DefaultLocale = locales[0],
        }));

    [Fact]
    public void AKeyIsReadFromTheFilesOfTheRequestedLanguage()
    {
        var catalog = Catalog("it", "en");

        Assert.Equal("IVAO Division Hub", catalog.Get("en", "app.title"));
        Assert.NotEqual(catalog.Get("en", "errors.validation.title"), catalog.Get("it", "errors.validation.title"));
    }

    [Fact]
    public void NamespacesAreALoadingDetailAndNotPartOfTheKey()
    {
        var catalog = Catalog("en");

        // common.json and errors.json flatten into one map: a key is written the same way in the
        // client and in the server.
        Assert.NotNull(catalog.Get("en", "nav.home"));
        Assert.NotNull(catalog.Get("en", LocalizedRules.MissingMessageKey));
    }

    [Fact]
    public void AnUnknownLanguageFallsBackToTheDefaultOne()
    {
        var catalog = Catalog("it", "en");

        Assert.Equal(catalog.Get("it", "app.title"), catalog.Get("de", "app.title"));
    }

    [Fact]
    public void AnUnknownKeyIsReturnedAsItselfRatherThanAsNothing()
    {
        var catalog = Catalog("it", "en");

        // A missing key is a mistake the language check of the build catches; meanwhile it must
        // not turn into an empty screen.
        Assert.Null(catalog.Get("it", "errors.nothing.here"));
        Assert.Equal("errors.nothing.here", catalog.Resolve("it", "errors.nothing.here"));
    }

    [Fact]
    public void EveryTitleTheCrudEngineUsesExistsInEveryLanguageOfTheDivision()
    {
        var catalog = Catalog("it", "en");

        string[] keys =
        [
            CrudProblems.ValidationTitleKey,
            CrudProblems.ForbiddenTitleKey,
            CrudProblems.ConflictTitleKey,
            CrudProblems.NotFoundTitleKey,
        ];

        foreach (var locale in new[] { "it", "en" })
        {
            foreach (var key in keys)
            {
                Assert.NotNull(catalog.Get(locale, key));
            }
        }
    }

    [Fact]
    public void ALanguageWithNoDirectoryIsNotAReasonToRefuseToStart()
    {
        // A fork that has declared a language it has not translated yet still has to come up: the
        // reader gets the default language rather than a site that refuses to boot.
        var catalog = Catalog("en", "xx");

        Assert.DoesNotContain("xx", catalog.Locales);
        Assert.Equal(catalog.Get("en", "app.title"), catalog.Resolve("xx", "app.title"));
    }
}
