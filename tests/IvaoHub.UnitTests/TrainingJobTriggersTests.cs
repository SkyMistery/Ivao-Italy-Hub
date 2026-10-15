using IvaoHub.Core.Division;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Staff;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The zones of the training's two triggers (A13f, after E10j): the reminders every quarter of an hour say UTC, which a quarter of an
/// hour falls on in every zone anyway (note 2026-09-28-i-job-quando-passenger-spegne-l-hub, §7); the closing by time runs in the
/// division's night.
/// </summary>
public sealed class TrainingJobTriggersTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    [Fact]
    public void TheRemindersRunInUtc()
    {
        var trigger = TriggerOf(TrainingRemindersJob.TriggerName);

        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(TrainingRemindersJob.Cron, trigger.CronExpressionString);
        Assert.Equal(TrainingRemindersJob.JobName, trigger.JobKey.Name);
    }

    [Fact]
    public void TheClosingByTimeRunsInTheDivisionsNight()
    {
        var trigger = TriggerOf($"{TrainingExpiryJob.JobName}-nightly");

        Assert.Equal(Rome.Id, trigger.TimeZone.Id);
        Assert.Equal(TrainingExpiryJob.Cron, trigger.CronExpressionString);
        Assert.Equal(TrainingExpiryJob.JobName, trigger.JobKey.Name);
    }

    /// <summary>A trigger of the module as the host builds it, by name, for a division in Rome's zone.</summary>
    private static ICronTrigger TriggerOf(string name)
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
        new TrainingModule().ConfigureServices(services, new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var quartz = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        return Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(quartz.Triggers, trigger => trigger.Key.Name == name));
    }
}
