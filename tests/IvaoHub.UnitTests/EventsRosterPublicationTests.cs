using IvaoHub.Core.Division;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the publication of a roster and of handing a shift over, on rows of their own (M4, E12; design §4.4, §4.4-bis; note
/// 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, readings 1, 2, 7 and 8): when a roster is published, and only once; when a shift may
/// be handed over; where a roster stands for the block of the coverage; and how a mail writes a shift.
/// </summary>
public sealed class EventsRosterPublicationTests
{
    private static readonly DateTime Starts = new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

    private static readonly EventsSettings Settings = new() { ApplicationsCloseDays = 3, RosterPublishDays = 2 };

    private static Event Proposed() => new()
    {
        Slug = "x",
        Status = PublishStatus.Published,
        HasRoster = true,
        StartsAtUtc = Starts,
        EndsAtUtc = Starts.AddHours(6),
        RosterProposedAt = Starts.AddDays(-3),
    };

    [Fact]
    public void ARosterIsPublishedAtTheStartLessTheDaysOfTheSetting()
    {
        Assert.Equal(Starts.AddDays(-2), RosterPublication.PublishesAt(Proposed(), Settings));
        Assert.False(RosterPublication.IsDue(Proposed(), Settings, Starts.AddDays(-2).AddMinutes(-1)));
        Assert.True(RosterPublication.IsDue(Proposed(), Settings, Starts.AddDays(-2)));
    }

    [Fact]
    public void ARosterIsPublishedOnceProposedOfAPublishedEventNotCancelledNorOver()
    {
        var now = Starts.AddDays(-1);

        var published = Proposed();
        published.RosterPublishedAt = now.AddHours(-1);
        Assert.False(RosterPublication.IsDue(published, Settings, now));

        var notProposed = Proposed();
        notProposed.RosterProposedAt = null;
        Assert.False(RosterPublication.IsDue(notProposed, Settings, now));

        var draft = Proposed();
        draft.Status = PublishStatus.Draft;
        Assert.False(RosterPublication.IsDue(draft, Settings, now));

        var cancelled = Proposed();
        cancelled.CancelledAt = now.AddDays(-1);
        Assert.False(RosterPublication.IsDue(cancelled, Settings, now));

        Assert.False(RosterPublication.IsDue(Proposed(), Settings, Starts.AddHours(6)));
        Assert.True(RosterPublication.IsDue(Proposed(), Settings, now));
    }

    [Fact]
    public void AShiftIsHandedOverOnAPublishedRosterUntilTheStart()
    {
        var row = Proposed();
        Assert.False(MemberShifts.IsTransferable(row, Starts.AddDays(-1)));

        row.RosterPublishedAt = Starts.AddDays(-2);
        Assert.True(MemberShifts.IsTransferable(row, Starts.AddMinutes(-1)));
        Assert.False(MemberShifts.IsTransferable(row, Starts));

        row.CancelledAt = Starts.AddDays(-1);
        Assert.False(MemberShifts.IsTransferable(row, Starts.AddDays(-1)));
    }

    [Fact]
    public void TheStageOfARosterIsReadFromItsMarksAndItsClose()
    {
        var closes = Starts.AddDays(-3);
        var row = Proposed();
        row.RosterProposedAt = null;

        Assert.Equal(RosterStage.Open, AtcCoverageProvider.Stage(row, closes, closes.AddMinutes(-1)));
        Assert.Equal(RosterStage.ToPropose, AtcCoverageProvider.Stage(row, closes, closes));

        row.RosterProposedAt = closes;
        Assert.Equal(RosterStage.Proposed, AtcCoverageProvider.Stage(row, closes, closes));

        row.RosterPublishedAt = closes.AddDays(1);
        Assert.Equal(RosterStage.Published, AtcCoverageProvider.Stage(row, closes, closes));
    }

    [Fact]
    public void AMailWritesAShiftWithTheDayOfItsEndOnlyWhenItIsAnother()
    {
        Assert.Equal("XXAA_TWR 2026-11-21 17:00 – 18:00", EventsMail.Shift("XXAA_TWR", Starts, Starts.AddHours(1)));
        Assert.Equal(
            "XXAA_TWR 2026-11-21 23:00 – 2026-11-22 01:00",
            EventsMail.Shift("XXAA_TWR", Starts.AddHours(6), Starts.AddHours(8)));
    }
}
