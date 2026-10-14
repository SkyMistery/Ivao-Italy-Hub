using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// The rows of a copy of an event (design M4 §2.3-bis, E8b), built from the rows of the event it copies: pure, and tested as such.
/// Every time moves by the same difference — the new start less the old one —; the rest of a row is copied as it is, and nothing of the
/// members ever comes (note 2026-10-11-duplica-e-la-cancellazione-negli-eventi §1).
/// </summary>
public static class EventCopy
{
    /// <summary>The first and the last instant a DATETIME column of the database holds: a moved time outside them cannot be stored.</summary>
    private static readonly DateTime FirstStored = new(1000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastStored = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    /// <summary>
    /// Whether every time of <paramref name="source"/>, moved to start at <paramref name="startsAtUtc"/>, is still an instant the
    /// database stores — the years 1000 to 9999 of its DATETIME columns (the review of #249, point 4): a start typed at an edge of the
    /// calendar is refused on its field, never a 500 of a time that does not fit. Its slots sit within its hours, so its own times are
    /// the ones to ask.
    /// </summary>
    public static bool MovesWithinTheCalendar(Event source, DateTime startsAtUtc)
    {
        ArgumentNullException.ThrowIfNull(source);

        var difference = startsAtUtc - source.StartsAtUtc;

        return new[] { source.StartsAtUtc, source.EndsAtUtc, source.VisibleFromUtc, source.BookingOpensAtUtc }
            .OfType<DateTime>()
            .All(at => difference >= TimeSpan.Zero ? LastStored - at >= difference : at - FirstStored >= difference.Negate());
    }

    /// <summary>
    /// The new event: every setting of <paramref name="source"/> — its kind, its five switches, who organises it and their page, its
    /// words and its description, who reads it, the length of a shift, the venue, the three limits of whoever books and does not fly —,
    /// in the same care, with its times moved by the difference between <paramref name="startsAtUtc"/> and its start. A draft: never
    /// published, not cancelled, and none of the marks of the jobs. Its banner only when <paramref name="banner"/> says the library still
    /// has the file (reading 4).
    /// </summary>
    public static Event Of(Event source, DateTime startsAtUtc, string slug, bool banner)
    {
        ArgumentNullException.ThrowIfNull(source);

        var difference = startsAtUtc - source.StartsAtUtc;

        return new Event
        {
            Slug = slug,
            Kind = source.Kind,
            PublicSlots = source.PublicSlots,
            PrivateSlots = source.PrivateSlots,
            HasRoster = source.HasRoster,
            WholeDivision = source.WholeDivision,
            InPerson = source.InPerson,
            Organizer = source.Organizer,
            ExternalUrl = source.ExternalUrl,
            Title = source.Title,
            Summary = source.Summary,
            BodyJson = source.BodyJson,
            BannerMediaId = banner ? source.BannerMediaId : null,
            VisibleFromUtc = source.VisibleFromUtc + difference,
            BookingOpensAtUtc = source.BookingOpensAtUtc + difference,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = source.EndsAtUtc + difference,
            ShiftMinutes = source.ShiftMinutes,
            Venue = source.Venue,
            Visibility = source.Visibility,
            RestrictedWindowHours = source.RestrictedWindowHours,
            RestrictedMaxPerWindow = source.RestrictedMaxPerWindow,
            RestrictedMaxPerEvent = source.RestrictedMaxPerEvent,
            OwnerDepartment = source.OwnerDepartment,
            OwnerDepartmentMask = source.OwnerDepartmentMask,
        };
    }

    /// <summary>An airport of the copy, with the capacity it had, in the copy's care.</summary>
    public static EventAirport Airport(EventAirport source, Event copy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);

        return new EventAirport
        {
            EventId = copy.Id,
            Icao = source.Icao,
            Ordinal = source.Ordinal,
            MaxMovementsPerHour = source.MaxMovementsPerHour,
            MaxArrivalsPerHour = source.MaxArrivalsPerHour,
            MaxDeparturesPerHour = source.MaxDeparturesPerHour,
            OwnerDepartment = copy.OwnerDepartment,
            OwnerDepartmentMask = copy.OwnerDepartmentMask,
        };
    }

    /// <summary>A route of the copy, as it was written, in the copy's care.</summary>
    public static EventRoute Route(EventRoute source, Event copy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);

        return new EventRoute
        {
            EventId = copy.Id,
            DepartureIcao = source.DepartureIcao,
            ArrivalIcao = source.ArrivalIcao,
            Route = source.Route,
            Remarks = source.Remarks,
            OwnerDepartment = copy.OwnerDepartment,
            OwnerDepartmentMask = copy.OwnerDepartmentMask,
        };
    }

    /// <summary>
    /// A position of the copy (E11a): its callsign, its FIR and the staff's note as they were, its hours moved by
    /// <paramref name="difference"/>, in the copy's care. Not checked again against the core's directory: its next save checks its hours,
    /// and asks the directory only if its callsign is written again — <b>the FIR stays the one it was first written with</b>, year after
    /// year of copies, even if the directory has since moved the callsign (the review of #256, point 7: the FIR is copied when the callsign
    /// is written, never on a save that keeps it).
    /// </summary>
    public static EventAtcPosition AtcPosition(EventAtcPosition source, Event copy, TimeSpan difference)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);

        return new EventAtcPosition
        {
            EventId = copy.Id,
            Callsign = source.Callsign,
            Fir = source.Fir,
            FromUtc = source.FromUtc + difference,
            ToUtc = source.ToUtc + difference,
            Note = source.Note,
            OwnerDepartment = copy.OwnerDepartment,
            OwnerDepartmentMask = copy.OwnerDepartmentMask,
        };
    }

    /// <summary>
    /// A public slot of the copy: its flight and its place in its rotation as they were, its two times moved by
    /// <paramref name="difference"/>, in the copy's care. Not checked again against the airports and the types the core knows today
    /// (reading 5): it was accepted, and its form checks it at the next save.
    /// </summary>
    public static EventSlot PublicSlot(EventSlot source, Event copy, TimeSpan difference)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(copy);

        return new EventSlot
        {
            EventId = copy.Id,
            Kind = SlotKind.Public,
            EventAirportIcao = source.EventAirportIcao,
            IsArrival = source.IsArrival,
            Callsign = source.Callsign,
            FlightNumber = source.FlightNumber,
            AircraftTypesJson = source.AircraftTypesJson,
            DepartureIcao = source.DepartureIcao,
            ArrivalIcao = source.ArrivalIcao,
            OffBlockUtc = source.OffBlockUtc + difference,
            OnBlockUtc = source.OnBlockUtc + difference,
            Stand = source.Stand,
            RotationCode = source.RotationCode,
            RotationLeg = source.RotationLeg,
            OwnerDepartment = copy.OwnerDepartment,
            OwnerDepartmentMask = copy.OwnerDepartmentMask,
        };
    }
}

/// <summary>
/// What a copy writes, read before anything is written: the new event, how far its times move, the rows that come with it — its ATC
/// positions too (E11a) —, and whether its private slots are generated with it.
/// </summary>
public sealed record EventDuplicatePlan(
    Event Copy,
    TimeSpan Difference,
    IReadOnlyList<EventAirport> Airports,
    IReadOnlyList<EventRoute> Routes,
    IReadOnlyList<EventSlot> PublicSlots,
    bool GeneratesPrivateSlots,
    IReadOnlyList<EventAtcPosition>? AtcPositions = null)
{
    /// <summary>
    /// The permissions a copy asks besides <c>Events.Edit</c> (reading 8): the area of every row it writes, as deleting an event asks
    /// the routes' (E4) — its routes, its slots when it carries some, the public ones copied or the private ones generated, and its ATC
    /// positions (E11a).
    /// </summary>
    public IReadOnlyList<string> AreasWritten
    {
        get
        {
            var areas = new List<string>();

            if (Routes.Count > 0)
            {
                areas.Add(EventsPermissions.RoutesEdit);
            }

            if (PublicSlots.Count > 0 || GeneratesPrivateSlots)
            {
                areas.Add(EventsPermissions.BookingsEdit);
            }

            if (AtcPositions is { Count: > 0 })
            {
                areas.Add(EventsPermissions.AtcEdit);
            }

            return areas;
        }
    }
}

/// <summary>
/// «Duplicate» (design M4 §2.3-bis, E8b; note 2026-10-11-duplica-e-la-cancellazione-negli-eventi §1): a new start and a new address,
/// and the hub makes <b>a draft</b> with every time moved by the same difference, copying the event's settings, its description and its
/// banner, its airports with their capacity, its routes, its ATC positions (E11a) and — if the staff choose — its public slots with their
/// rotations. <b>Nothing of the members</b>: no booking, no availability. One endpoint, one transaction.
/// <para><b>The private slots are generated again</b>, never copied (Carmine's answer 1 on issue #248, A′): in
/// the same transaction, by the generator of E7, when the copy's public slots are already there — it copied them, or the event takes
/// none (an MSE) —; otherwise «Generate the private slots» makes them on the new event once the new table is loaded, since private
/// slots generated before it would take the steps and the capacity the new table then takes.</para>
/// <para>Any event is duplicated, in any state: the RFE of last year, ended, is the case it exists for.</para>
/// </summary>
public sealed class EventDuplication(EventsDbContext database, HubDbContext hub, PrivateSlotGeneration generation)
{
    /// <summary>Where a refusal of the address lands: the field of the request.</summary>
    public const string SlugField = "slug";

    /// <summary>Where a refusal of the new start lands: the field of the request.</summary>
    public const string StartField = "startsAtUtc";

    /// <summary>The rows of <paramref name="source"/> its copy carries, read once, and the copy built from them; nothing written.</summary>
    public async Task<EventDuplicatePlan> PlanAsync(Event source, EventDuplicateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        // The banner of an event is deleted with the event a week after its end (§2.4): copied only while the library has its file.
        var banner = source.BannerMediaId is { } media
            && await CrudSource.BackOffice<MediaAsset>(hub).AnyAsync(row => row.Id == media && row.DeletedAt == null && row.HasFile, cancellationToken);

        var copy = EventCopy.Of(source, request.StartsAtUtc!.Value, request.Slug.Trim().ToLowerInvariant(), banner);

        var airports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == source.Id)
            .OrderBy(airport => airport.Ordinal)
            .ThenBy(airport => airport.Id)
            .ToListAsync(cancellationToken);

        // In the order they were written, the order the page shows them in (E4).
        var routes = await database.Routes.AsNoTracking()
            .Where(route => route.EventId == source.Id)
            .OrderBy(route => route.Id)
            .ToListAsync(cancellationToken);

        List<EventSlot> slots = request.PublicSlots
            ? await database.Slots.AsNoTracking()
                .Where(slot => slot.EventId == source.Id && slot.Kind == SlotKind.Public)
                .OrderBy(slot => slot.Id)
                .ToListAsync(cancellationToken)
            : [];

        // Generated around the public slots when they are there already; left to «Generate» when a new table is still to come.
        var generates = copy.PrivateSlots && (request.PublicSlots || !copy.PublicSlots);

        // The positions the ATC opened (E11a), by the hour they open; never the members' availabilities.
        var positions = await database.AtcPositions.AsNoTracking()
            .Where(position => position.EventId == source.Id)
            .OrderBy(position => position.FromUtc)
            .ThenBy(position => position.Id)
            .ToListAsync(cancellationToken);

        return new EventDuplicatePlan(copy, copy.StartsAtUtc - source.StartsAtUtc, airports, routes, slots, generates, positions);
    }

    /// <summary>
    /// Writes the copy and its rows, all or nothing: the address refused when another event has it, also when the unique index answers
    /// in the same instant. The private slots the plan generates are what «Generate» would make on the new event; what it would refuse —
    /// no airport with a capacity, more than it makes at once — leaves the copy without them, and its key comes back with the copy
    /// (<c>PrivateSlotsNotGenerated</c>, the review of #249, point 3): the page says so, and the same press on the copy says it again.
    /// </summary>
    public async Task<(Event? Copy, string? PrivateSlotsNotGenerated, Refusals Problems)> SaveAsync(
        EventDuplicatePlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var problems = new Refusals();
        var copy = plan.Copy;

        if (await CrudSource.BackOffice<Event>(database).AnyAsync(other => other.Slug == copy.Slug, cancellationToken))
        {
            return (null, null, problems.Add(SlugField, "events:errors.slugTaken"));
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.Events.Add(copy);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.Duplicated(exception))
        {
            // Another event took the address an instant before: the unique index answered.
            database.Entry(copy).State = EntityState.Detached;
            return (null, null, problems.Add(SlugField, "events:errors.slugTaken"));
        }

        database.Airports.AddRange(plan.Airports.Select(airport => EventCopy.Airport(airport, copy)));
        database.Routes.AddRange(plan.Routes.Select(route => EventCopy.Route(route, copy)));
        database.Slots.AddRange(plan.PublicSlots.Select(slot => EventCopy.PublicSlot(slot, copy, plan.Difference)));
        database.AtcPositions.AddRange((plan.AtcPositions ?? []).Select(position => EventCopy.AtcPosition(position, copy, plan.Difference)));
        await database.SaveChangesAsync(cancellationToken);

        string? notGenerated = null;
        if (plan.GeneratesPrivateSlots)
        {
            // In this transaction: the generator takes the copy's row under it and writes there, and the commit below is one for all.
            var (_, _, refused) = await generation.GenerateAsync(copy, cancellationToken);
            notGenerated = refused.Errors.Values.SelectMany(keys => keys).FirstOrDefault();
        }

        await transaction.CommitAsync(cancellationToken);
        return (copy, notGenerated, problems);
    }
}
