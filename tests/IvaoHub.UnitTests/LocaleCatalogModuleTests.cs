using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The words of more than one module in the catalogue the server reads (M3, A4a, note 2026-09-26-le-parole-di-piu-moduli).
/// Until the training there was one module, and the catalogue refused a key declared by two files: with a second module the
/// hub did not start, on the marker every copy carries (<c>_source</c>) and on the title every section of the back office
/// has (<c>nav.section</c>).
/// <para>Each test writes the language files of an installation of its own, so what is proved is the rule and not the
/// words of this repository.</para>
/// </summary>
public sealed class LocaleCatalogModuleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ivaohub-locales-{Guid.NewGuid():N}");

    public LocaleCatalogModuleTests()
    {
        // The file HubPaths recognises an installation by.
        Directory.CreateDirectory(Path.Combine(_root, "config"));
        File.WriteAllText(Path.Combine(_root, "config", "division.json"), "{}");
        Directory.CreateDirectory(Path.Combine(_root, "locales", "en"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void TwoModulesSayingTheSameKeysStartAndEachIsAskedByItsNamespace()
    {
        Write("common.json", """{ "nav": { "home": "Home" } }""");
        Write("flightops.json", """
            { "_source": "web/src/modules/flightops/locales", "nav": { "section": "Tours" }, "mail": { "flightops": { "allTours": "every tour" } } }
            """);
        Write("training.json", """
            { "_source": "web/src/modules/training/locales", "nav": { "section": "Training" }, "settings": { "title": "Training settings" } }
            """);

        var catalog = Catalog();

        // Always with the namespace, as the client asks.
        Assert.Equal("Tours", catalog.Get("en", "flightops:nav.section"));
        Assert.Equal("Training", catalog.Get("en", "training:nav.section"));

        // Without it only when one module says it: the mails of a module keep being asked for as they are today.
        Assert.Equal("every tour", catalog.Get("en", "mail.flightops.allTours"));
        Assert.Equal("every tour", catalog.Get("en", "flightops:mail.flightops.allTours"));
        Assert.Equal("Training settings", catalog.Get("en", "settings.title"));

        // Two modules: no answer that depends on the order of the files.
        Assert.Null(catalog.Get("en", "nav.section"));

        // The marker of a copy is not a word, and the core's words are where they were.
        Assert.Null(catalog.Get("en", LocaleCatalog.ModuleSourceKey));
        Assert.Null(catalog.Get("en", $"training:{LocaleCatalog.ModuleSourceKey}"));
        Assert.Equal("Home", catalog.Get("en", "nav.home"));
    }

    [Fact]
    public void AModuleDoesNotSayAgainWhatTheCoreSays()
    {
        Write("common.json", """{ "nav": { "home": "Home" } }""");
        Write("training.json", """{ "_source": "web/src/modules/training/locales", "nav": { "home": "Start" } }""");

        var refused = Assert.Throws<InvalidOperationException>(() => Catalog().Get("en", "nav.home"));

        Assert.Contains("'nav.home'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("training.json", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFilesOfTheCoreSayingTheSameKeyStillStopTheStart()
    {
        Write("common.json", """{ "list": { "empty": "Nothing here" } }""");
        Write("errors.json", """{ "list": { "empty": "Nothing" } }""");

        var refused = Assert.Throws<InvalidOperationException>(() => Catalog().Get("en", "list.empty"));

        Assert.Contains("'list.empty'", refused.Message, StringComparison.Ordinal);
    }

    private void Write(string file, string json) => File.WriteAllText(Path.Combine(_root, "locales", "en", file), json);

    private LocaleCatalog Catalog() => new(
        HubPaths.Resolve(_root),
        Options.Create(new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Domain = "example.org",
            Locales = ["en"],
            DefaultLocale = "en",
            Timezone = "UTC",
        }));
}
