using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Modules.Atc;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules a code review would otherwise have to catch every time. They are cheap here and
/// expensive once broken: a module that reaches into another module, or a second authorization
/// handler, is the point where "one mechanism, one place" stops being true (design M0 section 6.2).
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Core = typeof(HubDbContext).Assembly;
    private static readonly Assembly Atc = typeof(AtcModule).Assembly;

    [Fact]
    public void TheCoreDependsOnNoHostAndOnNoModule()
    {
        // Read from the project file rather than from the assembly: a reference the compiler
        // elided because nothing used it yet is still a dependency of the build.
        Assert.Empty(ProjectReferencesOf("IvaoHub.Core"));
    }

    [Fact]
    public void AModuleDependsOnTheCoreAndOnNoOtherModule()
    {
        foreach (var project in ProjectsMatching("IvaoHub.Modules."))
        {
            Assert.Equal(["IvaoHub.Core"], ProjectReferencesOf(project));
        }
    }

    [Fact]
    public void ThereIsExactlyOneAuthorizationHandlerInTheAssembliesThisProjectSees()
    {
        var handlers = new[] { Core, Atc }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IAuthorizationHandler).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .ToArray();

        Assert.Equal(["DepartmentAuthorizationHandler"], handlers.Select(handler => handler.Name));
    }

    /// <summary>
    /// The same rule over the whole of <c>src/</c>, read from the sources.
    /// <para>The reflection test above only sees the two assemblies this project references, so it
    /// is blind to <c>IvaoHub.Web</c> — which is exactly where writing "just one handler for this
    /// case" is most tempting. Reading the sources also catches a handler that is declared but
    /// never registered, which no container can see. The integration test
    /// <c>AuthorizationHandlerIsTheOnlyOne</c> covers the other half: what the real host actually
    /// resolves.</para>
    /// </summary>
    [Fact]
    public void NoSourceFileOutsideTheAuthorizationOfTheCoreDeclaresAHandler()
    {
        var offenders = SourceFiles()
            .Where(file => Declares(File.ReadAllText(file)))
            .Where(file => Path.GetFileName(file) != "HubAuthorization.cs")
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// A bulk operation goes straight to the server and never reaches the save changes interceptor,
    /// so it writes without audit, without the department guard and without projections. There is
    /// one way into the database, and it is <c>SaveChanges</c>.
    /// </summary>
    [Fact]
    public void NothingBypassesTheInterceptorWithABulkOperation()
    {
        var offenders = SourceFiles()
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains(".ExecuteDelete", StringComparison.Ordinal)
                    || text.Contains(".ExecuteUpdate", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The two registration points of a context are the two places that attach the save changes
    /// interceptor. A context registered any other way compiles, resolves and quietly writes without
    /// audit, without the department guard and without projections — and the CRUD engine, which
    /// resolves a context by type from the container, would serve it happily.
    /// <para>Now that a module can bring a context of its own, this is worth pinning: the first one
    /// to be registered by hand would be the first to escape the backbone.</para>
    /// </summary>
    [Fact]
    public void AContextIsOnlyEverRegisteredByTheTwoMethodsThatAttachTheInterceptor()
    {
        var offenders = SourceFiles()
            .Where(file => File.ReadAllText(file).Contains("AddDbContext<", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file) != "HubDbContextServiceCollectionExtensions.cs")
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// A base list, not a mention. Both shapes a handler can be declared with are covered:
    /// <c>: AuthorizationHandler&lt;T&gt;</c> and <c>: IAuthorizationHandler</c>.
    /// </summary>
    private static bool Declares(string source) =>
        source.Contains(": AuthorizationHandler<", StringComparison.Ordinal)
        || source.Contains(": IAuthorizationHandler", StringComparison.Ordinal);

    [Fact]
    public void OnlyTheCrudEngineIsAllowedToIgnoreTheQueryFilters()
    {
        var offenders = SourceFiles()
            .Where(file => File.ReadAllText(file).Contains(".IgnoreQueryFilters(", StringComparison.Ordinal))
            .Where(file => !file.Replace('\\', '/').Contains("/IvaoHub.Core/Data/Crud/", StringComparison.Ordinal))
            // The projection writer is the second and last place, and it is not a reader serving
            // anybody: it has to find the row it is about to rewrite whoever happens to be logged
            // in, or it would insert a duplicate instead of updating.
            .Where(file => Path.GetFileName(file) != "ProjectionWriter.cs")
            .Select(Path.GetFileName)
            .ToArray();

        // The back office reads drafts and other departments; it does so in one place, so that a
        // public endpoint can never be written with the filters switched off by accident.
        Assert.Empty(offenders);
    }

    /// <summary>
    /// One entity holds a body of blocks, and it is <c>ContentEntry</c>. A page, a news item and a
    /// document are three <c>kind</c>s of it (plan section 9.3), and the moment a second entity
    /// grows a block document the whole editorial half of this hub has two of everything: two
    /// editors, two renderers, two publications, two projections.
    /// <para>This is the closing question of G5 asked as a test rather than in a report: design M1
    /// section 3 says that if news and documents cost a table, section 9.3 has not held. A module
    /// that one day needs rich text of its own reuses the same document inside a
    /// <c>cms_contents</c> row, which is what design M1 section 1 means by "the same
    /// BlockDocument"; if that ever stops being possible, this test is where the decision has to
    /// be taken rather than discovered.</para>
    /// </summary>
    [Fact]
    public void NoSecondContentEntity()
    {
        // Read from the model rather than from the sources: what makes an entity a second content
        // is a column of the database holding a document, and a property nothing maps is not one.
        var withABody = Core.GetTypes()
            .Concat(Atc.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.Name is "BodyJson" or "Body"
                    && property.PropertyType == typeof(string)))
            // A message somebody typed is prose, not a document: no blocks, no editor, no renderer,
            // no publication. `Body` is caught here in the first place because an entity could name
            // a block document that way, and the contact family is named so that it stays a
            // decision rather than a coincidence of spelling. It is the only family excused: the
            // mail that carries one of these calls its own prose `Text`, for this very reason.
            .Where(type => !type.Name.StartsWith("Contact", StringComparison.Ordinal))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // `ContentVersion` is the same document frozen at a moment, not a second kind of content:
        // it is what publication wrote, it has no editor and it is never edited.
        Assert.Equal(["ContentEntry", "ContentVersion"], withABody);
    }

    /// <summary>
    /// SMTP lives in one file. A module never sends a mail: it publishes an intent, the notification
    /// service queues it and the dispatch job hands it to the one sender (plan section 9.7).
    /// <para>The check is on the client and not on the word "mail": what matters is who is allowed
    /// to open a connection to a mail server, and that is <c>SmtpMailSender</c> — the day a module
    /// writes its own, this is what says so, in the phase that wrote it rather than in the incident
    /// that follows it.</para>
    /// </summary>
    [Fact]
    public void NoSmtpOutsideTheNotificationService()
    {
        var offenders = SourceFiles()
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return text.Contains("MailKit", StringComparison.Ordinal)
                    || text.Contains("MimeKit", StringComparison.Ordinal)
                    || text.Contains("SmtpClient", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Equal(["MailSender.cs"], offenders);
    }

    /// <summary>
    /// The address of a member leaves the database only as a mail. It is read from the IVAO profile
    /// for the notification service and for nothing else (decision note of 6 September 2026), so no
    /// payload of the API may carry it — not the bootstrap, not a list, not the staff directory that
    /// G9 is about to build on the same table.
    /// </summary>
    [Fact]
    public void NoDtoCarriesAnEmailAddress()
    {
        var offenders = Core.GetTypes()
            .Concat(Atc.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Name.EndsWith("Dto", StringComparison.Ordinal)
                || type.Name.StartsWith("Bootstrap", StringComparison.Ordinal))
            .Where(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.Name.Contains("mail", StringComparison.OrdinalIgnoreCase)))
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> ProjectsMatching(string prefix) =>
        Directory.EnumerateDirectories(RepositoryRoot("src"))
            .Select(directory => Path.GetFileName(directory)!)
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>The IvaoHub projects one project references, from its own project file.</summary>
    private static IEnumerable<string> ProjectReferencesOf(string project)
    {
        var file = Path.Combine(RepositoryRoot("src"), project, $"{project}.csproj");
        return XDocument.Load(file)
            .Descendants("ProjectReference")
            // A project file always spells its paths with backslashes, whatever runs the build.
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// The two halves of the block registry say the same names.
    ///
    /// <para>The browser holds the schema, the component and the icon; the server holds the type and
    /// its kind, and nothing else (plan section 16.5). A type in one and not the other is a block
    /// that draws in the editor and is **refused on save**, with <c>errors.body.blockTypeUnknown</c>
    /// — which is how this test came to exist on 12 September 2026: the interactive block was
    /// registered in TypeScript only, every unit and integration test stayed green (they write bodies
    /// straight into the database), and the bench against the real API found it six minutes later.
    /// </para>
    ///
    /// <para>⚠️ It reads the TypeScript rather than asking it, because C# cannot ask. That makes it a
    /// test about a file's shape, and the shape it depends on is one line per entry in
    /// <c>CORE_BLOCK_TYPES</c>: if that object is ever written differently this fails loudly rather
    /// than quietly passing, which is the right way round.</para>
    /// </summary>
    [Fact]
    public void TheServerKnowsEveryBlockTypeTheBrowserRegisters()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot("web"), "src", "blocks", "core.ts"));
        var start = source.IndexOf("export const CORE_BLOCK_TYPES", StringComparison.Ordinal);
        Assert.True(start > 0, "CORE_BLOCK_TYPES is not where this test expects it");

        var end = source.IndexOf("} as const;", start, StringComparison.Ordinal);
        Assert.True(end > start, "CORE_BLOCK_TYPES does not end where this test expects it");

        var inTheBrowser = Regex
            .Matches(source[start..end], @"^\s*\w+:\s*'([^']+)',", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var onTheServer = CoreBlocks.All.Select(block => block.Type).Order(StringComparer.Ordinal).ToArray();

        Assert.NotEmpty(inTheBrowser);
        Assert.Equal(onTheServer, inTheBrowser);
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(RepositoryRoot("src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>The repository, found from the test binaries: the solution file is the marker.</summary>
    private static string RepositoryRoot(string folder)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, folder);
    }
}
