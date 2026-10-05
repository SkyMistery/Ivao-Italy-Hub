using IvaoHub.Core.Data;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What the tests of the events leave in the shared database, taken back: the events of one class — by the stem of their
/// addresses —, their airports and, since an event projects itself (E3b), its calendar entry, its lines in the search and its
/// files in use. A delete that goes past the interceptor takes the rows and not what they projected: without this a published
/// event of a test would stay in the calendar of every class after it.
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
