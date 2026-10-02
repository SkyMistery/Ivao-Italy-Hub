using IvaoHub.Core.Division;
using IvaoHub.Modules.Events;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The state of an event read off its dates (design M4 §2.1, E3a), at its edges: every instant is the first moment of what it
/// opens, a cancellation wins over everything and a draft over every date. And the views of the staff's list, which SQL asks in
/// its own words, say of every event what the state says.
/// </summary>
public sealed class EventsStateTests
{
    private static readonly DateTime Visible = new(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Opens = new(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Starts = new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Ends = new(2026, 11, 21, 22, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    [Fact]
    public void APublishedEventGoesThroughItsStatesAtTheFirstMomentOfEach()
    {
        var row = Published();

        Assert.Equal(EventStateKind.Scheduled, EventState.Of(row, Visible - Tick));
        Assert.Equal(EventStateKind.Announced, EventState.Of(row, Visible));
        Assert.Equal(EventStateKind.Announced, EventState.Of(row, Opens - Tick));
        Assert.Equal(EventStateKind.BookingOpen, EventState.Of(row, Opens));
        Assert.Equal(EventStateKind.BookingOpen, EventState.Of(row, Starts - Tick));
        Assert.Equal(EventStateKind.InProgress, EventState.Of(row, Starts));
        Assert.Equal(EventStateKind.InProgress, EventState.Of(row, Ends - Tick));
        Assert.Equal(EventStateKind.Ended, EventState.Of(row, Ends));
    }

    [Fact]
    public void AnEventWithNeitherDateIsSeenOnceItIsPublishedAndNeverOpensTheBookings()
    {
        var row = Published();
        row.VisibleFromUtc = null;
        row.BookingOpensAtUtc = null;

        Assert.Equal(EventStateKind.Announced, EventState.Of(row, Visible - TimeSpan.FromDays(30)));
        Assert.Equal(EventStateKind.Announced, EventState.Of(row, Starts - Tick));
        Assert.Equal(EventStateKind.InProgress, EventState.Of(row, Starts));
    }

    [Fact]
    public void ADraftIsADraftWhateverItsDatesSay()
    {
        var row = Published();
        row.Status = PublishStatus.Draft;

        Assert.Equal(EventStateKind.Draft, EventState.Of(row, Visible - Tick));
        Assert.Equal(EventStateKind.Draft, EventState.Of(row, Starts));
        Assert.Equal(EventStateKind.Draft, EventState.Of(row, Ends + TimeSpan.FromDays(365)));
    }

    [Fact]
    public void ACancelledEventIsCancelledBeforeAnythingElse()
    {
        var published = Published();
        published.CancelledAt = Opens;

        var draft = Published();
        draft.Status = PublishStatus.Draft;
        draft.CancelledAt = Opens;

        foreach (var now in new[] { Visible - Tick, Opens, Starts, Ends, Ends + TimeSpan.FromDays(1) })
        {
            Assert.Equal(EventStateKind.Cancelled, EventState.Of(published, now));
            Assert.Equal(EventStateKind.Cancelled, EventState.Of(draft, now));
        }
    }

    [Fact]
    public void EveryViewHoldsTheEventsItsStatesSayAndNoOther()
    {
        var instants = new[] { Visible - Tick, Visible, Opens - Tick, Opens, Starts - Tick, Starts, Ends - Tick, Ends, Ends + Tick };
        var rows = Variants().ToList();

        // Every state appears somewhere in the grid, so no view is checked against nothing.
        Assert.Equal(
            Enum.GetValues<EventStateKind>().Order(),
            rows.SelectMany(row => instants.Select(now => EventState.Of(row, now))).Distinct().Order());

        foreach (var view in EventViews.All)
        {
            foreach (var now in instants)
            {
                var where = EventViews.Where(view, now);
                Assert.NotNull(where);
                var holds = where.Compile();

                foreach (var row in rows)
                {
                    Assert.Equal(EventViews.Of(EventState.Of(row, now)) == view, holds(row));
                }
            }
        }
    }

    [Fact]
    public void AViewThatDoesNotExistIsNoView() => Assert.Null(EventViews.Where("past", Starts));

    /// <summary>An event published with all four dates, in the order «Publish» will ask for (E3b).</summary>
    private static Event Published() => new()
    {
        Status = PublishStatus.Published,
        VisibleFromUtc = Visible,
        BookingOpensAtUtc = Opens,
        StartsAtUtc = Starts,
        EndsAtUtc = Ends,
    };

    /// <summary>Drafts and published events, with and without their two optional dates, cancelled or not.</summary>
    private static IEnumerable<Event> Variants()
    {
        foreach (var status in new[] { PublishStatus.Draft, PublishStatus.Published })
        {
            foreach (var visible in new DateTime?[] { Visible, null })
            {
                foreach (var opens in new DateTime?[] { Opens, null })
                {
                    foreach (var cancelled in new DateTime?[] { null, Opens })
                    {
                        yield return new Event
                        {
                            Status = status,
                            VisibleFromUtc = visible,
                            BookingOpensAtUtc = opens,
                            StartsAtUtc = Starts,
                            EndsAtUtc = Ends,
                            CancelledAt = cancelled,
                        };
                    }
                }
            }
        }
    }
}
