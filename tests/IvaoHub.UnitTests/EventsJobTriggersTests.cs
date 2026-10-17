using IvaoHub.Core.Division;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.After;
using IvaoHub.Modules.Events.Bookings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The zones of the events' triggers (E4d, after E10j): the release of the events and the reminders of the bookings, both every
/// quarter of an hour, and the checks after an event every hour (E13a), say UTC, which a quarter of an hour falls on in every zone anyway (note
/// 2026-09-28-i-job-quando-passenger-spegne-l-hub, §7) — even for a division whose zone is not UTC.
/// </summary>
public sealed class EventsJobTriggersTests
{
    [Fact]
    public void TheReleaseRunsInUtc()
    {
        var trigger = TriggerOf(EventReleaseJob.TriggerName);

        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(EventReleaseJob.Cron, trigger.CronExpressionString);
        Assert.Equal(EventReleaseJob.JobName, trigger.JobKey.Name);
    }

    [Fact]
    public void TheRemindersOfTheBookingsRunInUtc()
    {
        var trigger = TriggerOf($"{BookingRemindersJob.JobName}-quarterly");

        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(BookingRemindersJob.Cron, trigger.CronExpressionString);
        Assert.Equal(BookingRemindersJob.JobName, trigger.JobKey.Name);
    }

    /// <summary>The checks after an event (E13a): every hour at seven past, in UTC too.</summary>
    [Fact]
    public void TheChecksAfterAnEventRunInUtc()
    {
        var trigger = TriggerOf(EventsAfterJob.TriggerName);

        Assert.Equal(TimeZoneInfo.Utc.Id, trigger.TimeZone.Id);
        Assert.Equal(EventsAfterJob.Cron, trigger.CronExpressionString);
        Assert.Equal(EventsAfterJob.JobName, trigger.JobKey.Name);
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
        new EventsModule().ConfigureServices(services, new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var quartz = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        return Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(quartz.Triggers, trigger => trigger.Key.Name == name));
    }
}
