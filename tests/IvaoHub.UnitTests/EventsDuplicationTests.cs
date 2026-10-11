using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rows of a copy of an event that need no database (design M4 §2.3-bis, E8b; note 2026-10-11-duplica-e-la-cancellazione-negli-eventi
/// §1): the new event takes every setting of the one it copies, in the same care, with every time moved by the same difference, and is a
/// draft with none of the marks of the life of the other; its airports, routes and public slots come as they were, at the new times; and
/// a copy asks the permission of the area of every row it writes. The times are invented, at round hours.
/// </summary>
public sealed class EventsDuplicationTests
{
    private static readonly DateTime Start = new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

    /// <summary>Thirty days later, two hours and a quarter earlier in the day: no difference a round number could hide.</summary>
    private static readonly DateTime NewStart = Start.AddDays(30).AddHours(-2).AddMinutes(-15);

    private static readonly TimeSpan Difference = NewStart - Start;

    [Fact]
    public void TheCopyTakesEverySettingMovesEveryTimeByTheSameDifferenceAndIsADraft()
    {
        var source = Source();
        var copy = EventCopy.Of(source, NewStart, "evt-copy", banner: true);

        // Every setting, in the same care.
        Assert.Equal(
            (source.Kind, source.PublicSlots, source.PrivateSlots, source.HasRoster, source.WholeDivision, source.InPerson),
            (copy.Kind, copy.PublicSlots, copy.PrivateSlots, copy.HasRoster, copy.WholeDivision, copy.InPerson));
        Assert.Equal((source.Organizer, source.ExternalUrl, source.Visibility), (copy.Organizer, copy.ExternalUrl, copy.Visibility));
        Assert.Equal((source.Title, source.Summary, source.Venue, source.BodyJson), (copy.Title, copy.Summary, copy.Venue, copy.BodyJson));
        Assert.Equal(
            (source.ShiftMinutes, source.RestrictedWindowHours, source.RestrictedMaxPerWindow, source.RestrictedMaxPerEvent),
            (copy.ShiftMinutes, copy.RestrictedWindowHours, copy.RestrictedMaxPerWindow, copy.RestrictedMaxPerEvent));
        Assert.Equal((Department.ED, source.OwnerDepartmentMask), (copy.OwnerDepartment, copy.OwnerDepartmentMask));
        Assert.Equal((source.BannerMediaId, "evt-copy"), (copy.BannerMediaId, copy.Slug));

        // Every time, by the same difference.
        Assert.Equal(NewStart, copy.StartsAtUtc);
        Assert.Equal(source.EndsAtUtc + Difference, copy.EndsAtUtc);
        Assert.Equal(source.VisibleFromUtc + Difference, copy.VisibleFromUtc);
        Assert.Equal(source.BookingOpensAtUtc + Difference, copy.BookingOpensAtUtc);

        // A draft: never published, not cancelled, no job has looked at it; who made it and when is the interceptor's.
        Assert.Equal(PublishStatus.Draft, copy.Status);
        Assert.Equal(
            (0L, (DateTime?)null, (DateTime?)null, (int?)null, (Localized<string>?)null, (DateTime?)null, (DateTime?)null),
            (copy.Id, copy.PublishedAt, copy.CancelledAt, copy.CancelledBy, copy.CancellationNote, copy.RosterProposedAt, copy.AfterDoneAt));
        Assert.Equal((default(DateTime), 0, default(DateTime)), (copy.CreatedAt, copy.CreatedBy, copy.RowVersion));
    }

    [Fact]
    public void TheCopyNamesEveryColumnOfTheEvent()
    {
        // The columns the copy takes, moves, or leaves as a new draft has them: a column added to the event one day is decided here, or
        // this test fails — «Duplicate» never forgets one by accident (§2.3-bis).
        string[] decided =
        [
            nameof(Event.Slug), nameof(Event.Kind), nameof(Event.PublicSlots), nameof(Event.PrivateSlots), nameof(Event.HasRoster),
            nameof(Event.WholeDivision), nameof(Event.InPerson), nameof(Event.Organizer), nameof(Event.ExternalUrl), nameof(Event.Title),
            nameof(Event.Summary), nameof(Event.BodyJson), nameof(Event.BannerMediaId), nameof(Event.VisibleFromUtc),
            nameof(Event.BookingOpensAtUtc), nameof(Event.StartsAtUtc), nameof(Event.EndsAtUtc), nameof(Event.ShiftMinutes),
            nameof(Event.Venue), nameof(Event.Visibility), nameof(Event.RestrictedWindowHours), nameof(Event.RestrictedMaxPerWindow),
            nameof(Event.RestrictedMaxPerEvent), nameof(Event.OwnerDepartment), nameof(Event.OwnerDepartmentMask),

            // Left as a new draft has them.
            nameof(Event.Id), nameof(Event.Status), nameof(Event.PublishedAt), nameof(Event.CancelledAt), nameof(Event.CancelledBy),
            nameof(Event.CancellationNote), nameof(Event.RosterProposedAt), nameof(Event.AfterDoneAt), nameof(Event.CreatedAt),
            nameof(Event.CreatedBy), nameof(Event.UpdatedAt), nameof(Event.UpdatedBy), nameof(Event.RowVersion),
        ];

        var columns = typeof(Event).GetProperties().Where(property => property.CanWrite && property.GetSetMethod() is not null).Select(property => property.Name);
        Assert.Equal(decided.Order(StringComparer.Ordinal), columns.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ABannerTheLibraryNoLongerHasIsLeftBehind()
    {
        Assert.Null(EventCopy.Of(Source(), NewStart, "evt-copy", banner: false).BannerMediaId);
    }

    [Fact]
    public void AnEventWithoutTheOptionalDatesKeepsThemEmpty()
    {
        var source = Source();
        source.VisibleFromUtc = null;
        source.BookingOpensAtUtc = null;

        var copy = EventCopy.Of(source, NewStart, "evt-copy", banner: true);
        Assert.Equal(((DateTime?)null, (DateTime?)null), (copy.VisibleFromUtc, copy.BookingOpensAtUtc));
    }

    [Fact]
    public void TheAirportsRoutesAndPublicSlotsComeAsTheyWereAtTheNewTimesInTheCareOfTheCopy()
    {
        var copy = EventCopy.Of(Source(), NewStart, "evt-copy", banner: true);
        copy.Id = 42;
        copy.OwnerDepartmentMask = 3;

        var airport = EventCopy.Airport(
            new EventAirport { Id = 7, EventId = 1, Icao = "XEJ1", Ordinal = 2, MaxArrivalsPerHour = 6, MaxDeparturesPerHour = 4, OwnerDepartmentMask = 1 },
            copy);
        Assert.Equal((0L, 42L, "XEJ1", 2), (airport.Id, airport.EventId, airport.Icao, airport.Ordinal));
        Assert.Equal(((int?)null, (int?)6, (int?)4), (airport.MaxMovementsPerHour, airport.MaxArrivalsPerHour, airport.MaxDeparturesPerHour));
        Assert.Equal((copy.OwnerDepartment, 3), (airport.OwnerDepartment, airport.OwnerDepartmentMask));

        var remarks = new Localized<string>(new Dictionary<string, string> { ["en"] = "Expect holding", ["it"] = "Attese in volo" });
        var route = EventCopy.Route(
            new EventRoute { Id = 8, EventId = 1, DepartureIcao = "XEJ1", ArrivalIcao = "XEJ2", Route = "XEJ DCT XEJJ", Remarks = remarks },
            copy);
        Assert.Equal((0L, 42L, "XEJ1", "XEJ2", "XEJ DCT XEJJ"), (route.Id, route.EventId, route.DepartureIcao, route.ArrivalIcao, route.Route));
        Assert.Equal((remarks, 3), (route.Remarks, route.OwnerDepartmentMask));

        var leg = new EventSlot
        {
            Id = 9,
            EventId = 1,
            Kind = SlotKind.Public,
            EventAirportIcao = "XEJ1",
            IsArrival = false,
            Callsign = "XEJ101",
            FlightNumber = "XJ101",
            AircraftTypes = ["XEJA", "XEJB"],
            DepartureIcao = "XEJ1",
            ArrivalIcao = "XEJ2",
            OffBlockUtc = Start.AddHours(1),
            OnBlockUtc = Start.AddHours(2).AddMinutes(10),
            Stand = "B12",
            RotationCode = "R1",
            RotationLeg = 2,
        };
        var slot = EventCopy.PublicSlot(leg, copy, Difference);
        Assert.Equal((0L, 42L, SlotKind.Public, false), (slot.Id, slot.EventId, slot.Kind, slot.Generated));
        Assert.Equal(("XEJ1", false, "XEJ101", "XJ101"), (slot.EventAirportIcao, slot.IsArrival, slot.Callsign, slot.FlightNumber));
        Assert.Equal(["XEJA", "XEJB"], slot.AircraftTypes);
        Assert.Equal(("XEJ1", "XEJ2", "B12", "R1", (int?)2), (slot.DepartureIcao, slot.ArrivalIcao, slot.Stand, slot.RotationCode, slot.RotationLeg));
        Assert.Equal((leg.OffBlockUtc + Difference, leg.OnBlockUtc + Difference), (slot.OffBlockUtc, slot.OnBlockUtc));
        Assert.Equal((copy.OwnerDepartment, 3), (slot.OwnerDepartment, slot.OwnerDepartmentMask));
    }

    [Fact]
    public void ANewStartThatWouldMoveATimeOffTheCalendarIsRefusedBeforeAnythingIsBuilt()
    {
        // The review of #249, point 4: the source opens its page ten days before its start and ends four hours after it.
        var source = Source();

        Assert.True(EventCopy.MovesWithinTheCalendar(source, NewStart));
        Assert.True(EventCopy.MovesWithinTheCalendar(source, new DateTime(9999, 12, 31, 19, 0, 0, DateTimeKind.Utc)));
        Assert.True(EventCopy.MovesWithinTheCalendar(source, new DateTime(1000, 1, 11, 0, 0, 0, DateTimeKind.Utc)));

        // Its end past the last day a DATETIME holds, its page opened before the first one.
        Assert.False(EventCopy.MovesWithinTheCalendar(source, new DateTime(9999, 12, 31, 21, 0, 0, DateTimeKind.Utc)));
        Assert.False(EventCopy.MovesWithinTheCalendar(source, new DateTime(1000, 1, 10, 0, 0, 0, DateTimeKind.Utc)));

        // Without the dates it does not have, only its own start and end count.
        source.VisibleFromUtc = null;
        source.BookingOpensAtUtc = null;
        Assert.True(EventCopy.MovesWithinTheCalendar(source, new DateTime(1000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ACopyAsksTheAreaOfEveryRowItWrites()
    {
        var copy = EventCopy.Of(Source(), NewStart, "evt-copy", banner: true);
        EventRoute[] routes = [new()];
        EventSlot[] slots = [new()];

        Assert.Empty(new EventDuplicatePlan(copy, Difference, [], [], [], GeneratesPrivateSlots: false).AreasWritten);
        Assert.Equal([EventsPermissions.RoutesEdit], new EventDuplicatePlan(copy, Difference, [], routes, [], GeneratesPrivateSlots: false).AreasWritten);
        Assert.Equal([EventsPermissions.BookingsEdit], new EventDuplicatePlan(copy, Difference, [], [], slots, GeneratesPrivateSlots: false).AreasWritten);
        Assert.Equal([EventsPermissions.BookingsEdit], new EventDuplicatePlan(copy, Difference, [], [], [], GeneratesPrivateSlots: true).AreasWritten);
        Assert.Equal(
            [EventsPermissions.RoutesEdit, EventsPermissions.BookingsEdit],
            new EventDuplicatePlan(copy, Difference, [], routes, slots, GeneratesPrivateSlots: true).AreasWritten);
    }

    /// <summary>An event with every column written: published, cancelled, looked at by the jobs, in the care of two departments.</summary>
    private static Event Source() => new()
    {
        Id = 11,
        Slug = "evt-source",
        Kind = "rfo",
        PublicSlots = true,
        PrivateSlots = true,
        HasRoster = true,
        WholeDivision = false,
        InPerson = true,
        Organizer = EventOrganizer.Network,
        ExternalUrl = "https://example.org/rfo",
        Title = new Localized<string>(new Dictionary<string, string> { ["en"] = "The RFO", ["it"] = "L'RFO" }),
        Summary = new Localized<string>(new Dictionary<string, string> { ["en"] = "Every year", ["it"] = "Ogni anno" }),
        BodyJson = """{"schemaVersion":1,"sections":[{"blocks":[]}]}""",
        BannerMediaId = 5,
        VisibleFromUtc = Start.AddDays(-10),
        BookingOpensAtUtc = Start.AddDays(-7),
        StartsAtUtc = Start,
        EndsAtUtc = Start.AddHours(4),
        ShiftMinutes = 90,
        Venue = new Localized<string>(new Dictionary<string, string> { ["en"] = "The club", ["it"] = "Il circolo" }),
        Status = PublishStatus.Published,
        PublishedAt = Start.AddDays(-20),
        Visibility = Visibility.Members,
        CancelledAt = Start.AddDays(-1),
        CancelledBy = 1001,
        CancellationNote = new Localized<string>(new Dictionary<string, string> { ["en"] = "Weather", ["it"] = "Meteo" }),
        RosterProposedAt = Start.AddDays(-3),
        AfterDoneAt = Start.AddDays(1),
        RestrictedWindowHours = 3,
        RestrictedMaxPerWindow = 2,
        RestrictedMaxPerEvent = 4,
        OwnerDepartment = Department.ED,
        OwnerDepartmentMask = 5,
        CreatedAt = Start.AddDays(-30),
        CreatedBy = 1002,
        UpdatedAt = Start.AddDays(-2),
        UpdatedBy = 1002,
        RowVersion = Start.AddDays(-2),
    };
}
