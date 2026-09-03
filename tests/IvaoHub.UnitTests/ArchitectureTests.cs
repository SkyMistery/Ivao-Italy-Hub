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
    public void ThereIsExactlyOneAuthorizationHandlerInTheWholeSolution()
    {
        var handlers = new[] { Core, Atc }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IAuthorizationHandler).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
            .ToArray();

        Assert.Equal(["DepartmentAuthorizationHandler"], handlers.Select(handler => handler.Name));
    }

    [Fact]
    public void OnlyTheCrudEngineIsAllowedToIgnoreTheQueryFilters()
    {
        var offenders = SourceFiles()
            .Where(file => File.ReadAllText(file).Contains(".IgnoreQueryFilters(", StringComparison.Ordinal))
            .Where(file => !file.Replace('\\', '/').Contains("/IvaoHub.Core/Data/Crud/", StringComparison.Ordinal))
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
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value))
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
