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

    /// <summary>The slots of the events, public and private (E5).</summary>
    public DbSet<EventSlot> Slots => Set<EventSlot>();

    /// <summary>The pilots' bookings of the slots (E6a).</summary>
    public DbSet<EventBooking> Bookings => Set<EventBooking>();

    /// <summary>The enums of the events are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<EventOrganizer>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<SlotKind>().HaveConversion<string>().HaveMaxLength(8);
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

        modelBuilder.Entity<EventSlot>(slot =>
        {
            slot.ToTable("evt_slots");
            slot.HasKey(row => row.Id);
            slot.Ignore(row => row.ResourceScope);
            slot.Ignore(row => row.AircraftTypes);
            slot.Property(row => row.EventAirportIcao).HasMaxLength(4).IsRequired();
            slot.Property(row => row.Callsign).HasMaxLength(EventSlot.MaxCodeLength);
            slot.Property(row => row.FlightNumber).HasMaxLength(EventSlot.MaxCodeLength);
            slot.Property(row => row.AircraftTypesJson).HasColumnName("aircraft_types").HasColumnType("json").IsRequired();
            slot.Property(row => row.DepartureIcao).HasMaxLength(4);
            slot.Property(row => row.ArrivalIcao).HasMaxLength(4);
            slot.Property(row => row.Stand).HasMaxLength(EventSlot.MaxStandLength);
            slot.Property(row => row.RotationCode).HasMaxLength(EventSlot.MaxRotationLength);
            slot.HasRowVersion(row => row.RowVersion);

            // An event deleted takes its slots with it, as it takes its airports and its routes.
            slot.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);

            // A public slot is one flight of its event at one off block time (§1.5); a private one has no callsign, and a unique
            // index lets any number of empty ones through. It is also the index the lists read the slots of an event by.
            slot.HasIndex(row => new { row.EventId, row.Callsign, row.OffBlockUtc }).IsUnique();
        });

        modelBuilder.Entity<EventBooking>(booking =>
        {
            booking.ToTable("evt_bookings");
            booking.HasKey(row => row.Id);
            booking.Ignore(row => row.StakeholderVid);
            booking.Ignore(row => row.ResourceScope);
            booking.Property(row => row.AircraftIcao).HasMaxLength(4).IsRequired();
            booking.Property(row => row.Callsign).HasMaxLength(EventSlot.MaxCodeLength);
            booking.Property(row => row.OtherIcao).HasMaxLength(4);
            booking.Property(row => row.UnflownExcusedNote).HasMaxLength(EventBooking.MaxNoteLength);

            // One slot, one booking (§1.6): the database says it, so two pilots booking the same slot in the same instant meet here
            // and one of them wins (§10.1). It is also the index of the key below.
            booking.HasIndex(row => row.SlotId).IsUnique();

            // A booked slot is not deleted (§1.5): the server says so first, and the key makes sure nobody forgets — an event
            // deleted takes its slots, and a booked one stops it. No key towards the event itself: every insert would take a shared
            // lock on the row the first booking of a pilot locks (§3.5), and two pilots booking the same slot could deadlock there.
            booking.HasOne<EventSlot>().WithMany().HasForeignKey(row => row.SlotId).OnDelete(DeleteBehavior.Restrict);

            // A pilot's bookings of an event: the row the next booking locks, and the ones its compatibility is checked against.
            booking.HasIndex(row => new { row.EventId, row.BookerVid });

            // A pilot's bookings of every event: their own page.
            booking.HasIndex(row => row.BookerVid);
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
