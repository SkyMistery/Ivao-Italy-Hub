using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Checks;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Tours;
using IvaoHub.Modules.FlightOps.Weather;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The zones of the tours' eight triggers (after E10j, note 2026-10-09-i-job-che-recuperano §3): the nightly cleaning and the
/// METARs in UTC, as their hours are; the two with a cadence in UTC too, which a cadence falls on in every zone anyway; the
/// validators' digest and the tours' retention in the division's zone — for a division whose zone is not UTC.
/// </summary>
public sealed class FlightOpsJobTriggersTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    public static TheoryData<string, string, string> InUtc => new()
    {
        { $"{FlightCheckJob.JobName}-ten-minutes", FlightCheckJob.JobName, FlightCheckJob.Cron },
        { $"{TourReleaseJob.JobName}-quarterly", TourReleaseJob.JobName, TourReleaseJob.Cron },
        { $"{PirepWithdrawalJob.JobName}-daily", PirepWithdrawalJob.JobName, PirepWithdrawalJob.Cron },
        { $"{TrackRetentionJob.JobName}-daily", TrackRetentionJob.JobName, TrackRetentionJob.Cron },
        { $"{WeatherJob.JobName}-half-hourly", WeatherJob.JobName, WeatherJob.Cron },
        { $"{WeatherRetentionJob.JobName}-daily", WeatherRetentionJob.JobName, WeatherRetentionJob.Cron },
    };

    public static TheoryData<string, string, string> InTheDivisionsZone => new()
    {
        { $"{ReviewDigestJob.JobName}-daily", ReviewDigestJob.JobName, ReviewDigestJob.Cron },
        { $"{TourRetentionJob.JobName}-monthly", TourRetentionJob.JobName, TourRetentionJob.Cron },
    };

    [Theory]
    [MemberData(nameof(InUtc))]
    public void TheseRunInUtc(string trigger, string job, string cron)
    {
        var found = TriggerOf(trigger);

        Assert.Equal(TimeZoneInfo.Utc.Id, found.TimeZone.Id);
        Assert.Equal(cron, found.CronExpressionString);
        Assert.Equal(job, found.JobKey.Name);
    }

    [Theory]
    [MemberData(nameof(InTheDivisionsZone))]
    public void TheseRunInTheDivisionsZone(string trigger, string job, string cron)
    {
        var found = TriggerOf(trigger);

        Assert.Equal(Rome.Id, found.TimeZone.Id);
        Assert.Equal(cron, found.CronExpressionString);
        Assert.Equal(job, found.JobKey.Name);
    }

    /// <summary>Eight triggers, each named above: one added without a line here would be a ninth.</summary>
    [Fact]
    public void TheModuleHasEightTriggersAndEachIsNamedAbove()
    {
        string[] named =
        [
            .. InUtc.Select(row => row.Data.Item1),
            .. InTheDivisionsZone.Select(row => row.Data.Item1),
        ];

        Assert.Equal(
            named.Order(StringComparer.Ordinal),
            Triggers().Select(trigger => trigger.Key.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>A trigger of the module as the host builds it, by name, for a division in Rome's zone.</summary>
    private static ICronTrigger TriggerOf(string name) =>
        Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(Triggers(), trigger => trigger.Key.Name == name));

    private static List<ITrigger> Triggers()
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
        new FlightOpsModule().ConfigureServices(services, new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        return [.. provider.GetRequiredService<IOptions<QuartzOptions>>().Value.Triggers];
    }
}
