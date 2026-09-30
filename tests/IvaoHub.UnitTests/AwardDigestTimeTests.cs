using IvaoHub.Core.Awards;
using IvaoHub.Core.Division;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The hour of the mail to whoever assigns the awards is a setting of the division, not a schedule written in the code
/// (M4, E10d, note <c>decisions/2026-09-30-la-mail-a-chi-assegna-gli-award.md</c>, Carmine's answer): <c>awardDigestTime</c>
/// in <c>division.json</c>, in the division's own time zone, 07:00 when left out, and refused at start up when it is not a time
/// of day.
/// </summary>
public sealed class AwardDigestTimeTests
{
    [Fact]
    public void TheTriggerRunsAtTheHourTheDivisionSetsInItsOwnTimeZone()
    {
        var trigger = Trigger(Division() with { AwardDigestTime = "18:30", Timezone = "Europe/Rome" });

        Assert.Equal("0 30 18 * * ?", trigger.CronExpressionString);
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome").Id, trigger.TimeZone.Id);
        Assert.Equal(AwardQueueMailJob.JobName, trigger.JobKey.Name);
    }

    [Fact]
    public void ADivisionThatSaysNothingIsToldAtSevenInTheMorning()
    {
        var division = Division();

        Assert.Equal(DivisionOptions.DefaultAwardDigestTime, division.AwardDigestTime);
        Assert.Equal("0 0 7 * * ?", Trigger(division).CronExpressionString);
    }

    [Theory]
    [InlineData("7am")]
    [InlineData("25:00")]
    [InlineData("07:60")]
    [InlineData("")]
    public void ATimeThatIsNotHHmmStopsTheStart(string time)
    {
        var result = new DivisionOptionsValidator().Validate(null, Division() with { AwardDigestTime = time });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("'awardDigestTime'", StringComparison.Ordinal));

        // And if a file got past the validator, the schedule would still be one: the default, never a site that stops.
        Assert.Equal(new TimeOnly(7, 0), (Division() with { AwardDigestTime = time }).ResolveAwardDigestTime());
    }

    [Theory]
    [InlineData("00:00", 0, 0)]
    [InlineData("07:00", 7, 0)]
    [InlineData("23:59", 23, 59)]
    public void ATimeOfDayIsTakenAsItIsWritten(string time, int hour, int minute)
    {
        var division = Division() with { AwardDigestTime = time };

        Assert.True(new DivisionOptionsValidator().Validate(null, division).Succeeded);
        Assert.Equal(new TimeOnly(hour, minute), division.ResolveAwardDigestTime());
    }

    /// <summary>A division the validator accepts, with nothing said about the awards.</summary>
    private static DivisionOptions Division() => new()
    {
        Code = "XX",
        CountryId = "XX",
        Name = new Dictionary<string, string> { ["en"] = "Example Division" },
        Domain = "example.org",
        Locales = ["en"],
        DefaultLocale = "en",
        Timezone = "UTC",
    };

    /// <summary>The daily trigger the awards register, built the way the host builds it.</summary>
    private static ICronTrigger Trigger(DivisionOptions division)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(division));
        services.AddHubAwards();

        using var provider = services.BuildServiceProvider();
        var quartz = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        return Assert.IsAssignableFrom<ICronTrigger>(
            Assert.Single(quartz.Triggers, trigger => trigger.Key.Name == AwardServiceCollectionExtensions.TriggerName));
    }
}
