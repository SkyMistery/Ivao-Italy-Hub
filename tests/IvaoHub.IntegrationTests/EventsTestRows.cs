using IvaoHub.Core.Data;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What the tests of the events leave in the shared database, taken back: the events of one class — by the stem of their
/// addresses —, their airports, their routes (E4), their slots (E5) and the bookings of the slots (E6a, first: a booked slot is not
/// deleted), the registrations to an event in person (E16: its questions and its statistics go with the event) and the seats in its activities (E17,
/// first: an activity with seats is not deleted; the activities go with the event), the ATC's positions and availabilities with their windows
/// (E11a), the shifts of the rosters (E11b) with the requests to hand one over (E12), the reports of support with their items (E14a), the rules of award with what the reports met of them and its signals in the core's queue
/// (E14b) and, since
/// an event projects itself (E3b), its calendar
/// entry, its lines in the search and its files in use. A delete
/// that goes past the interceptor takes the rows and not what they projected: without this a published event of a test would stay
/// in the calendar of every class after it.
/// </summary>
internal static class EventsTestRows
{
    public static async Task ForgetAsync(IServiceProvider services, string slugStem, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var ids = await database.Events.IgnoreQueryFilters()
            .Where(row => row.Slug.StartsWith(slugStem))
            .Select(row => row.Id)
            .ToListAsync(cancellationToken);
        var sources = ids.Select(id => $"event:{id}").ToList();

        await database.Airports.IgnoreQueryFilters().Where(airport => ids.Contains(airport.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Routes.IgnoreQueryFilters().Where(route => ids.Contains(route.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Bookings.IgnoreQueryFilters().Where(booking => ids.Contains(booking.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Registrations.IgnoreQueryFilters().Where(registration => ids.Contains(registration.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Seats.IgnoreQueryFilters().Where(seat => ids.Contains(seat.EventId)).ExecuteDeleteAsync(cancellationToken);
        // The rules of award the reports met (E14b) and the signals they put in the core's queue.
        var awards = (await database.ReportAwards.Where(award => ids.Contains(award.EventId)).Select(award => award.Id).ToListAsync(cancellationToken))
            .Select(EventReportAward.SourceOf)
            .ToList();
        await hub.AwardSignals
            .Where(row => row.SourceModule == EventsModule.ModuleKey && awards.Contains(row.SourceId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.ReportAwards.Where(award => ids.Contains(award.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.AwardRules.IgnoreQueryFilters().Where(rule => ids.Contains(rule.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.ReportItems.IgnoreQueryFilters().Where(item => ids.Contains(item.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Reports.IgnoreQueryFilters().Where(report => ids.Contains(report.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.AtcWindows.IgnoreQueryFilters().Where(window => ids.Contains(window.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.AtcAvailabilities.IgnoreQueryFilters().Where(availability => ids.Contains(availability.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.AtcTransfers.IgnoreQueryFilters().Where(transfer => ids.Contains(transfer.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.AtcShifts.IgnoreQueryFilters().Where(shift => ids.Contains(shift.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.AtcPositions.IgnoreQueryFilters().Where(position => ids.Contains(position.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Slots.IgnoreQueryFilters().Where(slot => ids.Contains(slot.EventId)).ExecuteDeleteAsync(cancellationToken);
        await database.Events.IgnoreQueryFilters().Where(row => ids.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);

        await hub.SearchIndex.IgnoreQueryFilters()
            .Where(row => row.SourceModule == EventsModule.ModuleKey && sources.Contains(row.SourceId))
            .ExecuteDeleteAsync(cancellationToken);
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(row => row.SourceModule == EventsModule.ModuleKey && sources.Contains(row.SourceId))
            .ExecuteDeleteAsync(cancellationToken);
        await hub.MediaUses
            .Where(row => row.SourceModule == EventsModule.ModuleKey && sources.Contains(row.SourceId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
