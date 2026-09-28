using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// "Which package is running?" The footer of every page answers "0.2.0 · 51f946b": the number we give the build, with the
/// rule of <c>Directory.Build.props</c> behind it, and the commit that says which code it is (note
/// 2026-09-27-la-versione-del-sito).
/// </summary>
public sealed class BuildInfoTests
{
    private static readonly BuildInfo Compiled = BuildInfo.FromAssembly(typeof(BuildInfo).Assembly);

    /// <summary>
    /// The shape of the number, read from the <b>compiled assembly</b> and not from the build file: that is the only way to
    /// know the number of <c>Directory.Build.props</c> reached the binary, which is where the site reads it. Three numbers
    /// separated by dots, no "v" in front, no suffix — PATCH, MINOR and MAJOR cannot be told apart without three places,
    /// and <c>release.yml</c> compares the tag with "v" followed by exactly this.
    /// </summary>
    [Fact]
    public void TheVersionHasThreeNumbersAndNothingElse() =>
        Assert.Matches(@"^\d+\.\d+\.\d+$", Compiled.Version);

    /// <summary>
    /// The commit is written by the SDK after the "+" of the informational version, from the .git of the checkout. This
    /// runs in CI as well, so it is what proves that a GitHub Actions checkout stamps it: without it the footer would show
    /// a number alone, and two builds under the same number can be two different codes.
    /// </summary>
    [Fact]
    public void TheBuildCarriesTheCommitItWasBuiltFrom()
    {
        Assert.Matches("^[0-9a-f]{40,64}$", Compiled.Commit);
        Assert.Equal(Compiled.Commit[..BuildInfo.ShortCommitLength], Compiled.ShortCommit);
    }

    [Fact]
    public void TheScreenShowsSevenCharactersOfTheCommit()
    {
        var build = new BuildInfo("0.2.0", "51f946b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2", DateTime.UnixEpoch, ".NET");

        Assert.Equal("51f946b", build.ShortCommit);
    }

    /// <summary>A build made outside a git checkout has no commit, and the screen says nothing rather than a wrong one.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("51f94")]
    public void WithoutACommitTheScreenShowsNone(string commit)
    {
        var build = new BuildInfo("0.2.0", commit, DateTime.UnixEpoch, ".NET");

        Assert.Null(build.ShortCommit);
    }
}
