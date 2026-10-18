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

    /// <summary>The questions of the events in person (E16).</summary>
    public DbSet<EventQuestion> Questions => Set<EventQuestion>();

    /// <summary>The members' registrations to the events in person (E16).</summary>
    public DbSet<EventRegistration> Registrations => Set<EventRegistration>();

    /// <summary>The numbers of each event (§1.10), born whole with E16.</summary>
    public DbSet<EventStats> Stats => Set<EventStats>();

    /// <summary>The positions the events open (E11a).</summary>
    public DbSet<EventAtcPosition> AtcPositions => Set<EventAtcPosition>();

    /// <summary>The controllers' availabilities for the events (E11a).</summary>
    public DbSet<EventAtcAvailability> AtcAvailabilities => Set<EventAtcAvailability>();

    /// <summary>The windows of the availabilities (E11a).</summary>
    public DbSet<EventAtcWindow> AtcWindows => Set<EventAtcWindow>();

    /// <summary>The shifts of the rosters (E11b).</summary>
    public DbSet<EventAtcShift> AtcShifts => Set<EventAtcShift>();

    /// <summary>The controllers' requests to hand a shift over (E12).</summary>
    public DbSet<EventAtcTransfer> AtcTransfers => Set<EventAtcTransfer>();

    /// <summary>The members' reports of support (E14a).</summary>
    public DbSet<EventReport> Reports => Set<EventReport>();

    /// <summary>The items of the reports: the flights and the shifts declared, with what the check found (E14a).</summary>
    public DbSet<EventReportItem> ReportItems => Set<EventReportItem>();

    /// <summary>The rules of award of the events (E14b).</summary>
    public DbSet<EventAwardRule> AwardRules => Set<EventAwardRule>();

    /// <summary>The rules of award the accepted reports met, each pointing its member out in the core's queue (E14b).</summary>
    public DbSet<EventReportAward> ReportAwards => Set<EventReportAward>();

    /// <summary>The parallel activities of the events in person (E17).</summary>
    public DbSet<EventActivity> Activities => Set<EventActivity>();

    /// <summary>The seats the members took in the activities' turns (E17).</summary>
    public DbSet<EventActivitySeat> Seats => Set<EventActivitySeat>();

    /// <summary>The enums of the events are stored as text, like the core's: readable without the code next to them.</summary>
    protected override void ConfigureModuleConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<EventOrganizer>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<SlotKind>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<QuestionType>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ShiftOrigin>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ShiftAttendance>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<TransferStatus>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ReportKind>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<ReportSource>().HaveConversion<string>().HaveMaxLength(8);
        configurationBuilder.Properties<ReportState>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ReportVerification>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<AwardCriterion>().HaveConversion<string>().HaveMaxLength(16);
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

        modelBuilder.Entity<EventQuestion>(question =>
        {
            question.ToTable("evt_questions");
            question.HasKey(row => row.Id);
            question.Ignore(row => row.ResourceScope);
            question.Ignore(row => row.Choices);
            question.Property(row => row.ChoicesJson).HasColumnName("choices").HasColumnType("json").IsRequired();
            question.HasRowVersion(row => row.RowVersion);

            // An event deleted takes its questions with it, as it takes its airports. The key is also the index the form reads by.
            question.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventRegistration>(registration =>
        {
            registration.ToTable("evt_registrations");
            registration.HasKey(row => row.Id);
            registration.Ignore(row => row.StakeholderVid);
            registration.Ignore(row => row.ResourceScope);
            registration.Property(row => row.AnswersJson).HasColumnName("answers").HasColumnType("json").IsRequired();

            // One registration per member per event (§4-bis.1): the database says it, so two requests of the same member in the
            // same instant meet here. It is also the index the staff's list reads an event by. No key towards the event, as for a
            // booking: an event with registrations is not deleted (the save refuses it), it is cancelled.
            registration.HasIndex(row => new { row.EventId, row.Vid }).IsUnique();

            // A member's registrations of every event: their own page.
            registration.HasIndex(row => row.Vid);
        });

        modelBuilder.Entity<EventStats>(stats =>
        {
            stats.ToTable("evt_event_stats");
            stats.HasKey(row => row.Id);
            stats.Ignore(row => row.ResourceScope);
            stats.Property(row => row.AnswerSumsJson).HasColumnName("answer_sums").HasColumnType("json").IsRequired();
            stats.Property(row => row.ActivitySeatsJson).HasColumnName("activity_seats").HasColumnType("json").IsRequired();

            // One row per event, which an event deleted takes with it.
            stats.HasIndex(row => row.EventId).IsUnique();
            stats.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventAtcPosition>(position =>
        {
            position.ToTable("evt_atc_positions");
            position.HasKey(row => row.Id);
            position.Ignore(row => row.ResourceScope);
            position.Property(row => row.Callsign).HasMaxLength(EventAtcPosition.MaxCallsignLength).IsRequired();
            position.Property(row => row.Fir).HasMaxLength(8);
            position.Property(row => row.Note).HasMaxLength(EventAtcPosition.MaxNoteLength);
            position.HasRowVersion(row => row.RowVersion);

            // An event deleted takes its positions with it, as it takes its airports. The key is also the index the tab reads by.
            position.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventAtcAvailability>(availability =>
        {
            availability.ToTable("evt_atc_availability");
            availability.HasKey(row => row.Id);
            availability.Ignore(row => row.StakeholderVid);
            availability.Ignore(row => row.ResourceScope);
            availability.Property(row => row.Note).HasMaxLength(EventAtcAvailability.MaxNoteLength);

            // One availability per controller per event (§1.7): the database says it, so two requests of the same member in the same
            // instant meet here. It is also the index the staff read an event's by. No key towards the event, as for a registration:
            // an event with availabilities is not deleted (the save refuses it), it is cancelled.
            availability.HasIndex(row => new { row.EventId, row.ControllerVid }).IsUnique();

            // A controller's availabilities of every event: their own page.
            availability.HasIndex(row => row.ControllerVid);
        });

        modelBuilder.Entity<EventAtcWindow>(window =>
        {
            window.ToTable("evt_atc_availability_windows");
            window.HasKey(row => row.Id);
            window.Ignore(row => row.StakeholderVid);
            window.Ignore(row => row.ResourceScope);

            // The windows of an availability go with it: the module deletes them first, each with its row in the audit, and the key
            // makes sure nobody forgets. It is also the index an availability's windows are read by.
            window.HasOne<EventAtcAvailability>().WithMany().HasForeignKey(row => row.AvailabilityId).OnDelete(DeleteBehavior.Cascade);

            // The windows of an event, read by the staff per position.
            window.HasIndex(row => new { row.EventId, row.FromUtc });
        });

        modelBuilder.Entity<EventAtcShift>(shift =>
        {
            shift.ToTable("evt_atc_shifts");
            shift.HasKey(row => row.Id);
            shift.Ignore(row => row.StakeholderVid);
            shift.Ignore(row => row.ResourceScope);
            shift.Ignore(row => row.Reason);
            shift.Property(row => row.Fir).HasMaxLength(8);
            shift.Property(row => row.ReasonJson).HasColumnName("reason").HasColumnType("json");
            shift.Property(row => row.AttendanceNote).HasMaxLength(EventAtcShift.MaxNoteLength);
            shift.HasRowVersion(row => row.RowVersion);

            // A position deleted takes its shifts — the module deletes them first, each with its row in the audit, and the key makes sure
            // nobody forgets. It is also the index the roster reads a position's shifts by. No key towards the event: an event with
            // shifts is not deleted (the save refuses it), it is cancelled.
            shift.HasOne<EventAtcPosition>().WithMany().HasForeignKey(row => row.PositionId).OnDelete(DeleteBehavior.Cascade);

            // The shifts of an event, which the roster reads.
            shift.HasIndex(row => new { row.EventId, row.FromUtc });

            // A controller's shifts of every event: their penalty (§4.5), their own page (E12) and their register (E13b).
            shift.HasIndex(row => row.ControllerVid);
        });

        modelBuilder.Entity<EventAtcTransfer>(transfer =>
        {
            transfer.ToTable("evt_atc_shift_transfers");
            transfer.HasKey(row => row.Id);
            transfer.Ignore(row => row.StakeholderVid);
            transfer.Ignore(row => row.ResourceScope);
            transfer.Property(row => row.Fir).HasMaxLength(8);
            transfer.Property(row => row.Note).HasMaxLength(EventAtcTransfer.MaxNoteLength);
            transfer.Property(row => row.DecisionNote).HasMaxLength(EventAtcTransfer.MaxNoteLength);
            transfer.HasRowVersion(row => row.RowVersion);

            // A shift taken away takes its requests — the module deletes them first, each with its row in the audit, and the key makes sure
            // nobody forgets. It is also the index a shift's requests are read by.
            transfer.HasOne<EventAtcShift>().WithMany().HasForeignKey(row => row.ShiftId).OnDelete(DeleteBehavior.Cascade);

            // The requests of an event by their state: the staff's list and the block of the coverage.
            transfer.HasIndex(row => new { row.EventId, row.Status });

            // A controller's requests of every event: their own page.
            transfer.HasIndex(row => row.ControllerVid);
        });

        modelBuilder.Entity<EventReport>(report =>
        {
            report.ToTable("evt_reports");
            report.HasKey(row => row.Id);
            report.Ignore(row => row.StakeholderVid);
            report.Ignore(row => row.ResourceScope);
            report.Property(row => row.DecisionNote).HasMaxLength(EventReport.MaxNoteLength);

            // One report per member per event per kind (§1.8): the database says it, so two sends of the same member in the same instant
            // meet here and the second becomes a correction of the first. It is also the index the staff's list reads an event by. No key
            // towards the event, as for a registration: an event over is not deleted, it is kept.
            report.HasIndex(row => new { row.EventId, row.Vid, row.Kind }).IsUnique();

            // A member's reports of every event: their own page.
            report.HasIndex(row => row.Vid);

            // The reports waiting for a validator: the staff's queue.
            report.HasIndex(row => row.State);
        });

        modelBuilder.Entity<EventReportItem>(item =>
        {
            item.ToTable("evt_report_items");
            item.HasKey(row => row.Id);
            item.Ignore(row => row.StakeholderVid);
            item.Ignore(row => row.ResourceScope);
            item.Property(row => row.Callsign).HasMaxLength(EventReportItem.MaxCallsignLength).IsRequired();
            item.Property(row => row.DepartureIcao).HasMaxLength(4);
            item.Property(row => row.ArrivalIcao).HasMaxLength(4);
            item.Property(row => row.DistanceNm).HasPrecision(7, 1);

            // The items of a report go with it: the module deletes them first, each with its row in the audit, and the key makes sure nobody
            // forgets. It is also the index a report's items are read by.
            item.HasOne<EventReport>().WithMany().HasForeignKey(row => row.ReportId).OnDelete(DeleteBehavior.Cascade);

            // A member's items of every event: their erasure.
            item.HasIndex(row => row.Vid);
        });

        modelBuilder.Entity<EventAwardRule>(rule =>
        {
            rule.ToTable("evt_award_rules");
            rule.HasKey(row => row.Id);
            rule.Ignore(row => row.ResourceScope);
            rule.Property(row => row.DepartureIcao).HasMaxLength(4);
            rule.Property(row => row.ArrivalIcao).HasMaxLength(4);
            rule.Property(row => row.PositionType).HasMaxLength(EventAwardRule.MaxPositionTypeLength);
            rule.HasRowVersion(row => row.RowVersion);

            // An event deleted takes its rules with it, as it takes its routes. The key is also the index the tab and an acceptance read by.
            rule.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventReportAward>(award =>
        {
            award.ToTable("evt_report_awards");
            award.HasKey(row => row.Id);
            award.Ignore(row => row.ResourceScope);
            award.Property(row => row.Reason).HasMaxLength(EventReportAward.MaxReasonLength).IsRequired();

            // The rows of a report go with it, as its items do.
            award.HasOne<EventReport>().WithMany().HasForeignKey(row => row.ReportId).OnDelete(DeleteBehavior.Cascade);

            // One row per report and rule, never deleted: a rule met again finds its own row and its signal (the review of #293, point 1). It is
            // also the index a decision reads them by.
            award.HasIndex(row => new { row.ReportId, row.RuleId }).IsUnique();

            // A member's rows of every event: their erasure; and an event's, for its statistics.
            award.HasIndex(row => row.Vid);
            award.HasIndex(row => row.EventId);
        });

        modelBuilder.Entity<EventActivity>(activity =>
        {
            activity.ToTable("evt_activities");
            activity.HasKey(row => row.Id);
            activity.Ignore(row => row.ResourceScope);
            activity.HasRowVersion(row => row.RowVersion);

            // An event deleted takes its activities with it, as it takes its questions. The key is also the index the tab reads by.
            activity.HasOne<Event>().WithMany().HasForeignKey(row => row.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventActivitySeat>(seat =>
        {
            seat.ToTable("evt_activity_bookings");
            seat.HasKey(row => row.Id);
            seat.Ignore(row => row.StakeholderVid);
            seat.Ignore(row => row.ResourceScope);

            // A turn has several seats, so no unique index can count them: they are counted under a lock on the activity's row. A
            // member takes a turn once, which the index says too. It is also the index the seats of a turn are counted by.
            seat.HasIndex(row => new { row.ActivityId, row.StartsAtUtc, row.Vid }).IsUnique();

            // A seat taken stops its activity from going: the server says so first (an activity with seats is not deleted). No key
            // towards the event, as for a registration.
            seat.HasOne<EventActivity>().WithMany().HasForeignKey(row => row.ActivityId).OnDelete(DeleteBehavior.Restrict);

            // A member's seats of an event: the ones a new turn is checked against, and the ones that go with their registration.
            seat.HasIndex(row => new { row.EventId, row.Vid });

            // A member's seats of every event: their own page.
            seat.HasIndex(row => row.Vid);
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
