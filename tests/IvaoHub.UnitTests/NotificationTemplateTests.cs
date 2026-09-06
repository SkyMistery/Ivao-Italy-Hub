using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The words of the mails. Their keys are built at run time — <c>mail.{type}.subject</c> — so
/// <c>pnpm i18n:check</c> cannot see them, exactly like the keys of a block: the language check of
/// the build reads literals, and a key made of a variable is not one.
/// <para>So the same discipline the block registry gets is applied here: whoever adds a kind of
/// notification does not add a test, because this one already reads the list.</para>
/// </summary>
public sealed class NotificationTemplateTests
{
    [Fact]
    public void EveryNotificationTypeHasATemplateInEveryLanguage()
    {
        var catalog = Catalog(out var locales);

        foreach (var type in NotificationTypes.All)
        {
            foreach (var locale in locales)
            {
                var subject = catalog.Get(locale, NotificationTypes.SubjectKey(type));
                var body = catalog.Get(locale, NotificationTypes.BodyKey(type));

                Assert.False(
                    string.IsNullOrWhiteSpace(subject),
                    $"{NotificationTypes.SubjectKey(type)} is missing in '{locale}'.");

                Assert.False(
                    string.IsNullOrWhiteSpace(body),
                    $"{NotificationTypes.BodyKey(type)} is missing in '{locale}'.");
            }
        }
    }

    [Fact]
    public void APlaceholderTheDataHasIsFilledInAndOneItHasNotIsLeftVisible()
    {
        var catalog = Catalog(out _);

        var mail = MailTemplate.Render(
            catalog,
            "en",
            NotificationTypes.ContactReceived,
            "somebody@example.org",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["department"] = "AOD",
                ["subject"] = "A question",
                ["body"] = "The body of it",
                ["vid"] = "700000",
                // `url` is deliberately absent: a template that quietly renders an empty gap where
                // a link should be is a mail nobody can act on and nobody can report.
            });

        Assert.Contains("A question", mail.Subject, StringComparison.Ordinal);
        Assert.Contains("AOD", mail.Text, StringComparison.Ordinal);
        Assert.Contains("The body of it", mail.Text, StringComparison.Ordinal);
        Assert.Contains("{{url}}", mail.Text, StringComparison.Ordinal);
    }

    /// <summary>The language files of this repository, read the way the server reads them.</summary>
    private static LocaleCatalog Catalog(out IReadOnlyList<string> locales)
    {
        var paths = HubPaths.Resolve(RepositoryRoot());
        var division = new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Domain = "example.org",
            Locales = ["en", "it"],
            DefaultLocale = "en",
            Timezone = "UTC",
        };

        locales = division.Locales;
        return new LocaleCatalog(paths, Options.Create(division));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
