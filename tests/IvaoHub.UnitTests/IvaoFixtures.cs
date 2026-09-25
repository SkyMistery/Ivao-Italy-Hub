using System.Text.Json;
using IvaoHub.Core.Ivao;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The answers recorded from the live API in <c>tests/fixtures/ivao/</c>, for the tests of the profile and of the ratings
/// (M3, A1). One reader for both, found from the solution file up, as the tests of the tracker find theirs.
/// </summary>
internal static class IvaoFixtures
{
    public static JsonElement Read(string name)
    {
        var path = Path.Combine(RepositoryRoot(), FixtureIvaoApiClient.Directory, name);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
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
