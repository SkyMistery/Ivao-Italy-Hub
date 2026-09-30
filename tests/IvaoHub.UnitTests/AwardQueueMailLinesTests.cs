using IvaoHub.Core.Awards;
using IvaoHub.Core.Content;
using IvaoHub.Core.Localization;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The lines of the mail to whoever assigns the awards (M4, E10d, note <c>decisions/2026-09-30-la-mail-a-chi-assegna-gli-award.md</c>):
/// one per reason and proposed award, with how many signals share them, in the order they entered the queue, and never a VID.
/// </summary>
public sealed class AwardQueueMailLinesTests
{
    private static readonly Dictionary<long, Localized<string>> Awards = new()
    {
        [7] = new([new("en", "Giro award"), new("it", "Award del Giro")]),
    };

    [Fact]
    public void SignalsWithTheSameReasonAndAwardAreOneLineWithTheirCount()
    {
        var lines = AwardQueueMailJob.Lines(
            [Signal(1, 761056, "Completed the tour \"Giro\"", 7), Signal(2, 761057, "Completed the tour \"Giro\"", 7)],
            Awards,
            "it");

        // The award in the division's language, the one the reason is written in.
        Assert.Equal("- Completed the tour \"Giro\" (Award del Giro): 2", lines);
    }

    [Fact]
    public void ALineKeepsTheOrderTheFirstOfItsSignalsEnteredTheQueue()
    {
        var lines = AwardQueueMailJob.Lines(
            [Signal(9, 761056, "Second", null), Signal(3, 761057, "First", 7), Signal(12, 761058, "First", 7)],
            Awards,
            "en");

        Assert.Equal("- First (Giro award): 2\n- Second: 1", lines);
    }

    [Fact]
    public void ASignalWithoutAnAwardOrWithOneThatIsGoneShowsItsReasonAlone()
    {
        var lines = AwardQueueMailJob.Lines(
            [Signal(1, 761056, "Supported the event", null), Signal(2, 761057, "Flew the route", 404)],
            Awards,
            "en");

        Assert.Equal("- Supported the event: 1\n- Flew the route: 1", lines);
    }

    [Fact]
    public void NoLineNamesAMember()
    {
        var lines = AwardQueueMailJob.Lines([Signal(1, 761059, "Completed the tour \"Giro\"", 7)], Awards, "en");

        Assert.DoesNotContain("761059", lines, StringComparison.Ordinal);
    }

    private static AwardSignal Signal(long id, int vid, string reason, long? awardId) => new()
    {
        Id = id,
        SourceModule = "sample",
        SourceId = $"line:{id}",
        Vid = vid,
        Reason = reason,
        AwardId = awardId,
    };
}
