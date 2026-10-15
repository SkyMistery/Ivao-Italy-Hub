using System.Globalization;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IvaoHub.Modules.Events;

/// <summary>
/// The retention of the events (design M4 §8.4, §11), <c>events-retention</c>, once a month — born with E17 for the events in person, since
/// M4b is not there yet (plan «Parallelismo possibile»); the retention of the pilots' bookings and reports joins it with E15b. Note
/// 2026-10-15-le-attivita-e-la-conservazione-breve §4.
/// <para><b>The events in person</b>: their registrations and the seats of their activities say who came in person and with what, so they go
/// <c>inPersonRetentionMonths</c> after the event's end (<see cref="InPersonRetention.IsDue"/>). Before they go, in the same save, the event's
/// statistics (<see cref="EventStats"/>, created when missing, only their own columns written) keep how many registered, the sums of the
/// answers (<see cref="RegistrationSums"/>) and the seats taken in each activity; the staff's lists read them from there afterwards. One
/// save per event, through the interceptor like every write (no bulk statement), as the retention of the tours.</para>
/// <para><b>It decides from its data, never from the hour it runs</b> (note 2026-09-28-i-job-quando-passenger-spegne-l-hub §8): an event past
/// its retention with registrations or seats left. A run lost while the hub slept is made up by the next, or by the core's catch up (E10j);
/// a run done twice finds nothing. Past the filter of the members: the job is nobody. It never throws: a failure is a row in
/// <c>hub_jobs_log</c>, as for every job of the hub, written with its end on every way out.</para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class EventsRetentionJob(
    EventsDbContext database,
    HubDbContext hub,
    ModuleSettingsStore settingsStore,
    IClock clock,
    ILogger<EventsRetentionJob> logger) : IJob
{
    public const string JobName = "events-retention";

    /// <summary>The first day of every month at 04:40, in UTC (the trigger says so), after the retention of the tours.</summary>
    public const string Cron = "0 40 4 1 * ?";

    private const int MaxMessageLength = 2000;

    public Task Execute(IJobExecutionContext context) => RunAsync(context.CancellationToken);

    /// <summary>What a run did: events in person whose registrations went, registrations and seats deleted.</summary>
    public sealed record Outcome(int Events, int Registrations, int Seats);

    public async Task<Outcome> RunAsync(CancellationToken cancellationToken = default)
    {
        var entry = new JobLogEntry { Job = JobName, StartedAt = clock.UtcNow, Status = "running" };
        hub.JobsLog.Add(entry);
        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            var now = entry.StartedAt;
            var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

            // The events that ended at least that long ago; the exact rule is asked of each below, in one place.
            var endedBefore = now.AddMonths(-settings.InPersonRetentionMonths);
            var holding = CrudSource.BackOffice<EventRegistration>(database).Select(registration => registration.EventId)
                .Union(CrudSource.BackOffice<EventActivitySeat>(database).Select(seat => seat.EventId));
            var due = (await CrudSource.BackOffice<Event>(database).AsNoTracking()
                    .Where(row => row.EndsAtUtc <= endedBefore && holding.Contains(row.Id))
                    .OrderBy(row => row.EndsAtUtc)
                    .ThenBy(row => row.Id)
                    .ToListAsync(cancellationToken))
                .Where(row => InPersonRetention.IsDue(row, settings.InPersonRetentionMonths, now))
                .ToList();

            var outcome = new Outcome(0, 0, 0);
            foreach (var row in due)
            {
                var (registrations, seats) = await RetireAsync(row, now, cancellationToken);
                outcome = new Outcome(outcome.Events + 1, outcome.Registrations + registrations, outcome.Seats + seats);
            }

            entry.FinishedAt = clock.UtcNow;
            entry.Status = "succeeded";
            entry.Message = string.Create(
                CultureInfo.InvariantCulture,
                $"{outcome.Events} event(s) in person retired: {outcome.Registrations} registration(s), {outcome.Seats} seat(s) deleted");
            await hub.SaveChangesAsync(cancellationToken);

            return outcome;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The events retention job failed.");

            database.ChangeTracker.Clear();
            hub.ChangeTracker.Clear();
            hub.JobsLog.Attach(entry);
            entry.FinishedAt = clock.UtcNow;
            entry.Status = "failed";
            entry.Message = exception.Message.Length <= MaxMessageLength ? exception.Message : exception.Message[..MaxMessageLength];
            await hub.SaveChangesAsync(cancellationToken);

            return new Outcome(0, 0, 0);
        }
    }

    /// <summary>
    /// One event in person, in one save: its statistics written — how many registered, the sums of the answers to the questions it has, the
    /// seats taken by activity — and then its registrations and seats deleted. Returns how many of each went.
    /// </summary>
    private async Task<(int Registrations, int Seats)> RetireAsync(Event row, DateTime now, CancellationToken cancellationToken)
    {
        var registrations = await CrudSource.BackOffice<EventRegistration>(database)
            .Where(registration => registration.EventId == row.Id)
            .ToListAsync(cancellationToken);
        var seats = await CrudSource.BackOffice<EventActivitySeat>(database)
            .Where(seat => seat.EventId == row.Id)
            .ToListAsync(cancellationToken);
        var questions = await database.Questions.AsNoTracking()
            .Where(question => question.EventId == row.Id)
            .OrderBy(question => question.Ordinal)
            .ThenBy(question => question.Id)
            .ToListAsync(cancellationToken);

        var stats = await CrudSource.BackOffice<EventStats>(database).FirstOrDefaultAsync(candidate => candidate.EventId == row.Id, cancellationToken);
        if (stats is null)
        {
            stats = new EventStats { EventId = row.Id };
            database.Stats.Add(stats);
        }

        // In the event's care, as every row of the event (§1.1).
        stats.OwnerDepartment = row.OwnerDepartment;
        stats.OwnerDepartmentMask = row.OwnerDepartmentMask;
        stats.Registrations = registrations.Count;
        stats.AnswerSumsJson = InPersonRetention.SumsJson(RegistrationSums.Of(questions, registrations).Values);
        stats.ActivitySeatsJson = InPersonRetention.SeatsJson(
            seats.GroupBy(seat => seat.ActivityId).ToDictionary(activity => activity.Key, activity => activity.Count()));
        stats.ComputedAt = now;

        database.Seats.RemoveRange(seats);
        database.Registrations.RemoveRange(registrations);

        await database.SaveChangesAsync(cancellationToken);
        database.ChangeTracker.Clear();

        return (registrations.Count, seats.Count);
    }
}
