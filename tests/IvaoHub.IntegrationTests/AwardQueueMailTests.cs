using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The mail to whoever assigns the awards (M4, E10d, note <c>decisions/2026-09-30-la-mail-a-chi-assegna-gli-award.md</c>), through
/// the real host: a signal of the tours — projected by the tours' own enrolment, as a completed tour projects it — and one of the
/// test module reach whoever holds <c>Awards.Assign</c>, once, and neither whoever switched the mail off from the profile nor a
/// member without the permission. A signal dismissed before the run is not told.
/// <para>Whoever assigns is a super administrator with no position: a grant never gives a global permission (note §5), and a
/// person with no position is staff of no department, so nobody here joins the recipients <c>ContactsAndNotificationsTests</c>
/// counts exactly.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AwardQueueMailTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // M4's range, E10d's share of it (761050–761059).
    private const int AssignerVid = 761050;
    private const int SwitchedOffVid = 761051;
    private const int MemberVid = 761052;
    private const int TourPilotVid = 761053;
    private const int SampleMemberVid = 761054;
    private const int DismissedVid = 761055;

    private static readonly int[] People = [AssignerVid, SwitchedOffVid, MemberVid, TourPilotVid, SampleMemberVid, DismissedVid];

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _tours = [];
    private readonly List<long> _samples = [];
    private readonly List<long> _awards = [];

    public async ValueTask InitializeAsync()
    {
        // With the fixtures: a host started on a database whose snapshot of IVAO is still empty would otherwise ask IVAO for it
        // (E10b's handoff), and no test calls IVAO.
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        // The host runs the job on its own schedule; paused before any signal exists, the only runs that can tell them are the
        // test's (the lesson of TourTests, at 12:45:00 on 26 September 2026).
        var scheduler = await _factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(token);
        await scheduler.PauseJob(new JobKey(AwardQueueMailJob.JobName), token);

        await SeedUserAsync(AssignerVid, "e10d-assigner@example.org", superadmin: true, token);
        await SeedUserAsync(SwitchedOffVid, "e10d-switched-off@example.org", superadmin: true, token);
        await SeedUserAsync(MemberVid, "e10d-member@example.org", superadmin: false, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = CancellationToken.None;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await sample.Events.Where(row => _samples.Contains(row.Id)).ExecuteDeleteAsync(token);

            var flightOps = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await flightOps.Enrolments.Where(row => _tours.Contains(row.TourId)).ExecuteDeleteAsync(token);
            await flightOps.Tours.IgnoreQueryFilters().Where(row => _tours.Contains(row.Id)).ExecuteDeleteAsync(token);

            // What the rows of the test module projected besides their signals.
            string[] sources = [.. _samples.Select(id => $"event:{id}")];
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await hub.SearchIndex.IgnoreQueryFilters()
                .Where(row => row.SourceModule == SampleModule.ModuleKey && sources.Contains(row.SourceId))
                .ExecuteDeleteAsync(token);
            await hub.CalendarEntries.IgnoreQueryFilters()
                .Where(row => row.SourceModule == SampleModule.ModuleKey && sources.Contains(row.SourceId))
                .ExecuteDeleteAsync(token);
            await hub.AwardSignals.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(token);
            await hub.Awards.Where(row => _awards.Contains(row.Id)).ExecuteDeleteAsync(token);
            await hub.Notifications.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(token);
            await hub.NotificationPreferences.Where(row => People.Contains(row.Vid)).ExecuteDeleteAsync(token);

            // The same people are super administrators only for this class: no other test hears about them.
            await hub.Users.Where(user => People.Contains(user.Vid))
                .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false).SetProperty(user => user.Email, (string?)null), token);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ATourSignalAndATestOneReachWhoeverAssignsOnceAndNotWhoeverSwitchedItOff()
    {
        var token = TestContext.Current.CancellationToken;

        // Switched off from the profile, the way the screen does it: the kind is one the profile lists.
        using (var switchedOff = await SignedInAsync(SwitchedOffVid, token))
        {
            var kinds = await switchedOff.GetFromJsonAsync<JsonElement>(NotificationPreferenceEndpoints.Pattern, token);
            Assert.Contains(kinds.EnumerateArray(), kind =>
                kind.GetProperty("type").GetString() == NotificationTypes.AwardToAssign && kind.GetProperty("enabled").GetBoolean());

            using var off = await switchedOff.PutAsJsonAsync(
                NotificationPreferenceEndpoints.Pattern, new { type = NotificationTypes.AwardToAssign, enabled = false }, token);
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        }

        var marker = Guid.NewGuid().ToString("N");
        var award = await AwardAsync($"e10d-test award {marker}", token);
        var tourReason = $"e10d-test completed the tour {marker}";
        var tourSignal = await TourSignalAsync(TourPilotVid, tourReason, award, token);
        var (sampleSignal, sampleReason) = await SampleSignalAsync(SampleMemberVid, $"e10d-test-{marker}", token);
        var (dismissedSignal, dismissedReason) = await SampleSignalAsync(DismissedVid, $"e10d-test-dismissed-{marker}", token);
        await DismissAsync(dismissedSignal, token);

        var since = DateTime.UtcNow.AddSeconds(-1);
        Assert.True(await RunAsync(token) >= 2);

        // Whoever assigns: one mail, both signals in it — the tours' with the award it proposes —, and not the dismissed one.
        var mine = Assert.Single(await ToldAsync(AssignerVid, since, token), row => row.DataJson.Contains(marker, StringComparison.Ordinal));
        Assert.Equal("e10d-assigner@example.org", mine.Address);
        Assert.Contains($"{tourReason} (e10d-test award {marker}): 1", mine.DataJson, StringComparison.Ordinal);
        Assert.Contains($"{sampleReason}: 1", mine.DataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(dismissedReason, mine.DataJson, StringComparison.Ordinal);

        // Nobody else of this class: not who switched it off, not a member without the permission.
        Assert.Empty(await ToldAsync(SwitchedOffVid, since, token));
        Assert.Empty(await ToldAsync(MemberVid, since, token));

        // Each signal told remembers it; the dismissed one was never told.
        Assert.NotNull((await SignalRowAsync(tourSignal, token)).NotifiedAt);
        Assert.NotNull((await SignalRowAsync(sampleSignal, token)).NotifiedAt);
        Assert.Null((await SignalRowAsync(dismissedSignal, token)).NotifiedAt);

        // The mail as it will be read: every placeholder filled, the lines and the queue in it.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<LocaleCatalog>();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(mine.DataJson)!;
            var mail = MailTemplate.Render(catalog, mine.Locale, mine.Type, mine.Address, data);

            Assert.DoesNotContain("{{", mail.Subject, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", mail.Text, StringComparison.Ordinal);
            Assert.Contains(tourReason, mail.Text, StringComparison.Ordinal);
            Assert.Contains("/staff/awards/queue", mail.Text, StringComparison.Ordinal);
        }

        // Once: a second run finds nothing new, and writes nothing.
        Assert.Equal(0, await RunAsync(token));
        Assert.Single(await ToldAsync(AssignerVid, since, token), row => row.DataJson.Contains(marker, StringComparison.Ordinal));
    }

    /// <summary>The host's own schedule: the division's hour (<c>awardDigestTime</c>), in the division's time zone.</summary>
    [Fact]
    public async Task TheHostSendsItAtTheHourOfTheDivisionInItsTimeZone()
    {
        var token = TestContext.Current.CancellationToken;
        var division = _factory.Services.GetRequiredService<IOptions<DivisionOptions>>().Value;

        var scheduler = await _factory.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(token);
        var trigger = Assert.IsAssignableFrom<ICronTrigger>(
            await scheduler.GetTrigger(new TriggerKey(AwardServiceCollectionExtensions.TriggerName), token));

        Assert.Equal(AwardQueueMailJob.CronAt(division.ResolveAwardDigestTime()), trigger.CronExpressionString);
        Assert.Equal(division.ResolveTimeZone().Id, trigger.TimeZone.Id);
        Assert.Equal(AwardQueueMailJob.JobName, trigger.JobKey.Name);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AwardQueueMailJob>().RunAsync(cancellationToken);
    }

    /// <summary>The mails of the kind queued for one person since the test began.</summary>
    private async Task<List<Notification>> ToldAsync(int vid, DateTime since, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => row.Type == NotificationTypes.AwardToAssign && row.Vid == vid && row.CreatedAt >= since)
            .ToListAsync(cancellationToken);
    }

    /// <summary>An award to propose, as a tour proposes its own.</summary>
    private async Task<long> AwardAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var award = new Award
        {
            OwnerDepartment = Department.FOD,
            Name = new Localized<string>(new Dictionary<string, string> { ["it"] = name, ["en"] = name }),
        };
        hub.Awards.Add(award);
        await hub.SaveChangesAsync(cancellationToken);
        _awards.Add(award.Id);
        return award.Id;
    }

    /// <summary>
    /// What a completed tour signals, written by the tours' own code: an enrolment carrying its <c>AwardSignalProjection</c>,
    /// saved through the tours' context, where the interceptor projects it into the core's queue. Nothing of the tours is
    /// changed for the mail.
    /// </summary>
    private async Task<long> TourSignalAsync(int pilot, string reason, long award, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var flightOps = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
        var title = $"e10d-test-tour-{Guid.NewGuid():N}";

        var tour = new Tour
        {
            Slug = title[..28],
            Kind = TourKind.Free,
            Title = new Localized<string>(new Dictionary<string, string> { ["it"] = title, ["en"] = title }),
            Summary = new Localized<string>(new Dictionary<string, string> { ["it"] = title, ["en"] = title }),
            OwnerDepartment = Department.FOD,
            AwardId = award,
        };
        flightOps.Tours.Add(tour);
        await flightOps.SaveChangesAsync(cancellationToken);
        _tours.Add(tour.Id);

        var now = DateTime.UtcNow;
        var enrolment = new Enrolment
        {
            TourId = tour.Id,
            Vid = pilot,
            StartedAt = now,
            CompletedAt = now,
            AwardSignal = new AwardSignalProjection(pilot, reason, award),
        };
        flightOps.Enrolments.Add(enrolment);
        await flightOps.SaveChangesAsync(cancellationToken);

        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var source = Enrolment.ReferenceOf(enrolment.Id);
        return await hub.AwardSignals.AsNoTracking()
            .Where(row => row.SourceModule == FlightOpsModule.ModuleKey && row.SourceId == source)
            .Select(row => row.Id)
            .SingleAsync(cancellationToken);
    }

    /// <summary>What a row of the test module signals: its member and the reason it gives, <c>completed {title}</c>.</summary>
    private async Task<(long Signal, string Reason)> SampleSignalAsync(int vid, string title, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var sample = new SampleEvent
        {
            Title = title,
            StartsAt = new DateTime(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            Status = PublishStatus.Published,
            AwardeeVid = vid,
        };

        module.Events.Add(sample);
        await module.SaveChangesAsync(cancellationToken);
        _samples.Add(sample.Id);

        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var signal = await hub.AwardSignals.AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sample.SourceId)
            .SingleAsync(cancellationToken);

        return (signal.Id, signal.Reason);
    }

    /// <summary>A line of the queue dismissed before the mail goes, as the queue's screen does it.</summary>
    private async Task DismissAsync(long signal, CancellationToken cancellationToken)
    {
        using var assigner = await SignedInAsync(AssignerVid, cancellationToken);
        using var dismissed = await assigner.PutAsJsonAsync(
            $"{AwardEndpoints.SignalsPattern}/{signal}", new { status = nameof(AwardSignalStatus.Dismissed) }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, dismissed.StatusCode);
    }

    private async Task<AwardSignal> SignalRowAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AwardSignals.AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedUserAsync(int vid, string email, bool superadmin, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "Awards";
        user.IsStaff = false;
        user.IsSuperadmin = superadmin;
        user.Email = email;
        user.Locale = "en";
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        // A preference left over from another run of this class would decide this one.
        database.NotificationPreferences.RemoveRange(
            await database.NotificationPreferences.Where(row => row.Vid == vid).ToListAsync(cancellationToken));

        await database.SaveChangesAsync(cancellationToken);
    }
}
