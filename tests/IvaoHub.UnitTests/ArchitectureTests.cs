using System.Reflection;
using System.Xml.Linq;
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
