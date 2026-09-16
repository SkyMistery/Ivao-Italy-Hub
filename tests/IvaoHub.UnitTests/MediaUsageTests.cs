using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// When a file of the library is still needed, and when the expiry job takes it (M2, T4, note
/// 2026-09-15-file-con-scadenza §3). The whole rule is in <see cref="MediaUsage"/>, and the delete
/// of the library, the job and the list all read it from there.
/// </summary>
public sealed class MediaUsageTests
{
    private static readonly DateTime Now = new(2030, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ContentAppearanceDto Page = new(1, "/about", Department.WD, "Chi siamo".L("About"));

    [Fact]
    public void OneUseStillAliveKeepsTheFileWhateverTheOthersSay()
    {
        var usage = new MediaUsage([], [Now.AddDays(-3), Now.AddDays(5)]);

        Assert.True(usage.IsInUse(Now));
        Assert.Equal(Now.AddDays(5), usage.DeletesOn);
    }

    [Fact]
    public void AFileIsFreeOnceEveryUseHasEnded()
    {
        var usage = new MediaUsage([], [Now.AddDays(-3), Now.AddDays(-1)]);

        Assert.False(usage.IsInUse(Now));
        Assert.Equal(Now.AddDays(-1), usage.DeletesOn);
    }

    [Fact]
    public void AUseWithoutAnEndMeansTheJobNeverTakesIt()
    {
        var usage = new MediaUsage([], [Now.AddDays(-3), null]);

        Assert.True(usage.IsInUse(Now));
        Assert.Null(usage.DeletesOn);
    }

    [Fact]
    public void APublishedPageWinsOverEveryEndedUse()
    {
        var usage = new MediaUsage([Page], [Now.AddDays(-3)]);

        Assert.True(usage.IsInUse(Now));
        Assert.Null(usage.DeletesOn);
    }

    [Fact]
    public void AFileNoModuleEverDeclaredIsNotTheJobsBusiness()
    {
        var usage = new MediaUsage([], []);

        Assert.False(usage.IsInUse(Now));
        Assert.Null(usage.DeletesOn);
    }
}
