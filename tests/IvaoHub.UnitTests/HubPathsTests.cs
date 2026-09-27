using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// Where the hub finds its folders (note 2026-09-27-l-avvio-da-qualunque-cartella): the first installation on a server
/// answered "could not be started" and wrote nothing, because the host started the process from a working directory
/// with no <c>config/division.json</c> above it, while the application's own folder had one.
/// <para>These tests set <see cref="HubPaths.RootVariable"/>, which every other resolution in the process reads, so they
/// run alone.</para>
/// </summary>
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class HubPathsTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"ivaohub-paths-{Guid.NewGuid():N}");
    private readonly string? _previousRoot = Environment.GetEnvironmentVariable(HubPaths.RootVariable);

    public HubPathsTests()
    {
        Directory.CreateDirectory(_scratch);
        Environment.SetEnvironmentVariable(HubPaths.RootVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(HubPaths.RootVariable, _previousRoot);
        Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public void AWorkingDirectoryElsewhereFindsTheRootFromTheFolderOfTheApplication()
    {
        var workingDirectory = Folder("somewhere-else");
        var application = Installation("webapp");

        // As AppContext.BaseDirectory gives it, with a separator at the end that the root does not keep.
        var paths = HubPaths.Resolve(workingDirectory, application + Path.DirectorySeparatorChar);

        Assert.Equal(application, paths.Root);
        Assert.Equal(HubRootSource.ApplicationFolder, paths.Source);
        Assert.Equal(Path.Combine(application, "config", "division.json"), paths.DivisionFile);
    }

    [Fact]
    public void TheContentRootComesFirstAsItAlwaysDidDuringDevelopment()
    {
        // The repository: the content root is the web project, the folders are two levels up; the application folder is
        // bin/, which has no division file of its own.
        var repository = Installation("repository");
        var webProject = Folder(Path.Combine("repository", "src", "IvaoHub.Web"));
        var bin = Folder(Path.Combine("repository", "src", "IvaoHub.Web", "bin", "Debug", "net10.0"));

        var paths = HubPaths.Resolve(webProject, bin);

        Assert.Equal(repository, paths.Root);
        Assert.Equal(HubRootSource.ContentRoot, paths.Source);

        // An application folder with a division file of its own does not take over from the content root.
        var otherInstallation = Installation("other");
        Assert.Equal(repository, HubPaths.Resolve(webProject, otherInstallation).Root);
    }

    [Fact]
    public void TheVariableWinsOverBoth()
    {
        var pinned = Folder("pinned");
        Environment.SetEnvironmentVariable(HubPaths.RootVariable, pinned);

        var paths = HubPaths.Resolve(Installation("content"), Installation("application"));

        Assert.Equal(pinned, paths.Root);
        Assert.Equal(HubRootSource.Pinned, paths.Source);
        Assert.Contains(HubPaths.RootVariable, paths.SourceDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void FoundNowhereTheRootIsTheFolderOfTheApplicationWhereTheDivisionFileBelongs()
    {
        var workingDirectory = Folder("working");
        var application = Folder("application");

        var paths = HubPaths.Resolve(workingDirectory, application + Path.DirectorySeparatorChar);

        Assert.Equal(application, paths.Root);
        Assert.Equal(HubRootSource.NotFound, paths.Source);
        Assert.StartsWith("not found", paths.SourceDescription, StringComparison.Ordinal);

        // Without an application folder, what it always was: the content root.
        Assert.Equal(workingDirectory, HubPaths.Resolve(workingDirectory).Root);
    }

    [Fact]
    public void TheSecretFilesAreEveryJsonUnderSecretsInOrder()
    {
        var root = Installation("secrets");
        Assert.Empty(HubPaths.Resolve(root).SecretFiles());

        Directory.CreateDirectory(Path.Combine(root, "secrets"));
        File.WriteAllText(Path.Combine(root, "secrets", "b.json"), "{}");
        File.WriteAllText(Path.Combine(root, "secrets", "a.json"), "{}");
        File.WriteAllText(Path.Combine(root, "secrets", "notes.txt"), "not configuration");

        Assert.Equal(
            [Path.Combine(root, "secrets", "a.json"), Path.Combine(root, "secrets", "b.json")],
            HubPaths.Resolve(root).SecretFiles());
    }

    private string Folder(string relative) => Directory.CreateDirectory(Path.Combine(_scratch, relative)).FullName;

    private string Installation(string relative)
    {
        var root = Folder(relative);
        Directory.CreateDirectory(Path.Combine(root, "config"));
        File.WriteAllText(Path.Combine(root, "config", "division.json"), "{}");
        return root;
    }
}

/// <summary>Tests that change the environment of the process, run after the others and one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "process environment";
}
