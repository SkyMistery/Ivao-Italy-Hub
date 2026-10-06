using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IvaoHub.Modules.Events.Data;

/// <summary>
/// The context of the events: every <c>evt_</c> table, with its own history <c>__EFMigrationsHistory_events</c> (design M4 §1,
/// §14). No foreign key towards the core: a VID, an ICAO, a file of the media library are plain columns.
/// <para>Its <c>Initial</c> migration (E2) holds the event whole and its airports, and it is never touched again: every later
/// table arrives with the phase that needs it, one additive migration each, and the module's phases migrate this context one
/// after the other.</para>
/// </summary>
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options, ICurrentUser? currentUser = null)
    : ModuleDbContext(options, currentUser)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventAirport> Airports => Set<EventAirport>();

    /// <summary>The routes of the events, which the flight operations write (E4).</summary>
    public DbSet<EventRoute> Routes => Set<EventRoute>();

    /// <summary>The enums of the events are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<EventOrganizer>().HaveConversion<string>().HaveMaxLength(16);
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("evt_events");
            entity.HasKey(row => row.Id);
            entity.Ignore(row => row.ResourceScope);
            entity.Property(row => row.Slug).HasMaxLength(Event.MaxSlugLength).IsRequired();
            entity.Property(row => row.Kind).HasMaxLength(CalendarKindWriteDtoValidator.MaxKeyLength).IsRequired();
            entity.Property(row => row.ExternalUrl).HasMaxLength(LinkWriteDtoValidator.MaxUrlLength);
            entity.Property(row => row.BodyJson).HasColumnName("body_json").HasColumnType("json").IsRequired();
            entity.HasRowVersion(row => row.RowVersion);

            // The address of the page of an event.
            entity.HasIndex(row => row.Slug).IsUnique();

            // The lists of the staff and of the site, which read the events by state and by their start.
            entity.HasIndex(row => new { row.Status, row.StartsAtUtc });
        });

        modelBuilder.Entity<EventAirport>(airport =>
        {
            airport.ToTable("evt_event_airports");
            airport.HasKey(row => row.Id);
            airport.Ignore(row => row.ResourceScope);
            airport.Property(row => row.Icao).HasMaxLength(4).IsRequired();
            airport.HasRowVersion(row => row.RowVersion);

            // Inside one context a key is allowed: an event deleted takes its airports with it.
            airport.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);

            // An airport is in an event once; the server says so before the index does.
            airport.HasIndex(row => new { row.EventId, row.Icao }).IsUnique();
        });

        modelBuilder.Entity<EventRoute>(route =>
        {
            route.ToTable("evt_routes");
            route.HasKey(row => row.Id);
            route.Ignore(row => row.ResourceScope);
            route.Property(row => row.DepartureIcao).HasMaxLength(4).IsRequired();
            route.Property(row => row.ArrivalIcao).HasMaxLength(4).IsRequired();
            route.Property(row => row.Route).HasMaxLength(EventRoute.MaxRouteLength).IsRequired();
            route.HasRowVersion(row => row.RowVersion);

            // An event deleted takes its routes with it, as it takes its airports. The key is also the index the page reads by.
            route.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model of the events, the same way the hub's is built.</summary>
public sealed class EventsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<EventsDbContext>
{
    public EventsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>();
        options.UseMySql(
            "Server=localhost;Port=3306;Database=ivaohub;User ID=ivaohub;Password=ivaohub",
            new MariaDbServerVersion(HubDbContext.ServerVersion),
            mySql => mySql.MigrationsHistoryTable($"__EFMigrationsHistory_{EventsModule.ModuleKey}"));
        options.UseSnakeCaseNamingConvention();

        return new EventsDbContext(options.Options);
    }
}
