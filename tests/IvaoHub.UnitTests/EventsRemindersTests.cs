using IvaoHub.Modules.Events.Bookings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rule of one reminder (M4, E6b; design §3.8): a booking of a pilot is due when its off block is no later than
/// <c>reminderLeadHours</c> from now, and the mail takes with it the near ones of the same pilot and event — up to the lead after the
/// first that is due. Off blocks are instants after a fixed now; a booking whose off block has come is gone, never reminded.
/// </summary>
public sealed class EventsRemindersTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TimeSpan Lead = TimeSpan.FromHours(24);

    private static IReadOnlyList<DateTime> Window(params double[] hoursFromNow) =>
        BookingRemindersJob.Window(hoursFromNow.Select(hours => Now.AddHours(hours)), offBlock => offBlock, Now, Lead);

    [Fact]
    public void NothingDueIsNoMailWhateverIsNear()
    {
        Assert.Empty(Window());
        Assert.Empty(Window(24.5, 30, 47));
    }

    [Fact]
    public void ABookingIsDueUpToTheLeadAndNotAtNow()
    {
        Assert.Equal([Now.AddHours(24)], Window(24));
        Assert.Equal([Now.AddMinutes(1)], Window(1.0 / 60));
        Assert.Empty(Window(0));
        Assert.Empty(Window(-1));
    }

    [Fact]
    public void TheNearOnesGoWithTheFirstUpToTheLeadAfterIt()
    {
        // The first due at 22 hours: with it the ones up to 46 hours, exactly 46 included; not the one at 47, nor the one gone.
        Assert.Equal(
            [Now.AddHours(22), Now.AddHours(23), Now.AddHours(30), Now.AddHours(46)],
            Window(-2, 47, 30, 22, 46, 23));
    }

    [Fact]
    public void TheMailIsInTheOrderOfTheFlights()
    {
        Assert.Equal([Now.AddHours(2), Now.AddHours(5), Now.AddHours(9)], Window(9, 2, 5));
    }
}
