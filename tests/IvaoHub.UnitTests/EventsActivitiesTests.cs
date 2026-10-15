using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Registrations;
using IvaoHub.Modules.Events.Staff;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the parallel activities that need no database (M4, E17, design §4-bis; note 2026-10-15-le-attivita-e-la-conservazione-breve):
/// the turns of a window — the ones that fit whole —, when an event is past its short retention, what the statistics keep and read back,
/// the copy of a question and of an activity, and the turns of a member as the mail says them.
/// </summary>
public sealed class EventsActivitiesTests
{
    private static readonly DateTime Ten = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Localized<string> Simulator = new(new Dictionary<string, string> { ["en"] = "Simulator", ["it"] = "Simulatore" });

    private static EventActivity Activity(DateTime from, DateTime to, int minutes) =>
        new() { Id = 7, Name = Simulator, FromUtc = from, ToUtc = to, SlotMinutes = minutes, SeatsPerSlot = 2 };

    [Fact]
    public void TheTurnsAreTheOnesThatFitWhole()
    {
        // From ten to a quarter past noon, thirty minutes each: four turns, the last one ending at noon.
        Assert.Equal(
            [Ten, Ten.AddMinutes(30), Ten.AddHours(1), Ten.AddMinutes(90)],
            EventActivity.Turns(Ten, Ten.AddHours(2).AddMinutes(15), 30));

        // A window shorter than a turn, an empty one, a turn of no length: none.
        Assert.Empty(EventActivity.Turns(Ten, Ten.AddMinutes(20), 30));
        Assert.Empty(EventActivity.Turns(Ten, Ten, 30));
        Assert.Empty(EventActivity.Turns(Ten, Ten.AddHours(1), 0));

        // A day of five minutes: one past the most, so that the save can tell it is too many.
        Assert.Equal(EventActivity.MaxTurns + 1, EventActivity.Turns(Ten, Ten.AddDays(1), 5).Count);
    }

    [Fact]
    public void ATurnIsAStartOfTheWindowThatFitsWhole()
    {
        var activity = Activity(Ten, Ten.AddHours(2).AddMinutes(15), 30);

        Assert.True(activity.IsTurn(Ten));
        Assert.True(activity.IsTurn(Ten.AddMinutes(90)));
        Assert.False(activity.IsTurn(Ten.AddMinutes(10)));
        Assert.False(activity.IsTurn(Ten.AddHours(2)));
        Assert.False(activity.IsTurn(Ten.AddMinutes(-30)));
    }

    [Fact]
    public void AnEventIsPastItsRetentionOnlyOverAndThatLongAfterItsEnd()
    {
        var row = new Event { StartsAtUtc = Ten, EndsAtUtc = Ten.AddHours(8) };

        Assert.False(InPersonRetention.IsDue(row, 3, Ten.AddHours(9)));
        Assert.False(InPersonRetention.IsDue(row, 3, Ten.AddHours(8).AddMonths(3).AddSeconds(-1)));
        Assert.True(InPersonRetention.IsDue(row, 3, Ten.AddHours(8).AddMonths(3)));
    }

    [Fact]
    public void TheStatisticsReadBackTheSumsAndTheSeatsTheyKept()
    {
        var sums = new[] { new QuestionSum(12, 3, 2, 1, 0, new Dictionary<string, int>()), new QuestionSum(13, 2, 0, 0, 0, new Dictionary<string, int> { ["1"] = 2 }) };
        var stats = new EventStats
        {
            AnswerSumsJson = InPersonRetention.SumsJson(sums),
            ActivitySeatsJson = InPersonRetention.SeatsJson(new Dictionary<long, int> { [7] = 5, [8] = 0 }),
        };

        var read = InPersonRetention.Sums(stats);
        Assert.Equal((3, 2, 1), (read[12].Answered, read[12].Yes, read[12].No));
        Assert.Equal(2, read[13].Choices["1"]);
        Assert.Equal(new Dictionary<long, int> { [7] = 5, [8] = 0 }, InPersonRetention.Seats(stats));

        // A column nobody wrote reads as nothing.
        Assert.Empty(InPersonRetention.Sums(new EventStats()));
        Assert.Empty(InPersonRetention.Seats(new EventStats()));
    }

    [Fact]
    public void TheCopyTakesTheQuestionWithItsKeysAndTheActivityMoved()
    {
        var copy = new Event { Id = 99, OwnerDepartment = Department.ED, OwnerDepartmentMask = 5 };
        var question = new EventQuestion
        {
            Id = 1,
            EventId = 1,
            Ordinal = 3,
            Type = QuestionType.MultiChoice,
            Label = Simulator,
            Choices = [new QuestionChoice("2", Simulator), new QuestionChoice("5", Simulator)],
            Required = true,
        };

        var copied = EventCopy.Question(question, copy);
        Assert.Equal((99L, 3, QuestionType.MultiChoice, true, 5), (copied.EventId, copied.Ordinal, copied.Type, copied.Required, copied.OwnerDepartmentMask));
        Assert.Equal(["2", "5"], copied.Choices.Select(choice => choice.Key));

        var activity = EventCopy.Activity(Activity(Ten, Ten.AddHours(2), 30), copy, TimeSpan.FromDays(7));
        Assert.Equal((99L, Ten.AddDays(7), Ten.AddDays(7).AddHours(2), 30, 2), (activity.EventId, activity.FromUtc, activity.ToUtc, activity.SlotMinutes, activity.SeatsPerSlot));
        Assert.Equal(0, activity.Id);
    }

    [Fact]
    public void TheMailSaysTheTurnsInTheOrderTheyComeInTheReadersLanguage()
    {
        var activity = Activity(Ten, Ten.AddHours(2), 30);
        activity.Place = new Localized<string>(new Dictionary<string, string> { ["en"] = "Hangar", ["it"] = "Hangar 2" });
        var later = new EventActivitySeat { ActivityId = 7, StartsAtUtc = Ten.AddMinutes(30) };
        var first = new EventActivitySeat { ActivityId = 7, StartsAtUtc = Ten };

        Assert.Equal(
            ["Simulatore (Hangar 2): 2026-11-14 10:00–10:30 UTC", "Simulatore (Hangar 2): 2026-11-14 10:30–11:00 UTC"],
            EventsMail.SeatLines([(later, activity), (first, activity)], "it", "en"));
    }

    [Fact]
    public void TheRetentionRunsOnceAMonthInUtcWhateverTheDivisionsZone()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new DivisionOptions
        {
            Code = "XX",
            CountryId = "XX",
            Name = new Dictionary<string, string> { ["en"] = "Example Division" },
            Domain = "example.org",
            Locales = ["en"],
            DefaultLocale = "en",
            Timezone = "Europe/Rome",
        }));
        new EventsModule().ConfigureServices(services, new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(
            provider.GetRequiredService<IOptions<QuartzOptions>>().Value.Triggers,
            candidate => candidate.Key.Name == EventsRetentionJob.TriggerName));

        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(EventsRetentionJob.Cron, trigger.CronExpressionString);
        Assert.Equal(EventsRetentionJob.JobName, trigger.JobKey.Name);
    }
}