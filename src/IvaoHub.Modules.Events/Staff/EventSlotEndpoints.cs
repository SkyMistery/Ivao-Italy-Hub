using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>A slot of an event as its list and its form show it (design M4 §1.5).</summary>
public sealed record EventSlotDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    SlotKind Kind,
    string EventAirportIcao,
    bool IsArrival,
    string? Callsign,
    string? FlightNumber,
    IReadOnlyList<string> AircraftTypes,
    string? DepartureIcao,
    string? ArrivalIcao,
    DateTime? OffBlockUtc,
    DateTime? OnBlockUtc,
    string? Stand,
    string? RotationCode,
    int? RotationLeg,
    bool Generated,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on a public slot, the form of one slot's corrections: the cells of a row of the table, and the aircraft
/// types as two fields — the main one, and the others written as the table writes them (<c>A20N/A321</c>), read by the same code
/// —, kept main first (note 2026-10-07-gli-slot-sulla-pagina-dell-evento §1). The event is chosen when it is created and never
/// changes; the care is the event's, taken before the permission is asked; the airport of the event and the direction are read off
/// the two airports. No flight number, other type, stand or rotation is none.
/// </summary>
public sealed record EventSlotWriteDto(
    long EventId,
    string Callsign,
    string? FlightNumber,
    string MainAircraftType,
    string? OtherAircraftTypes,
    string DepartureIcao,
    DateTime? OffBlockUtc,
    string ArrivalIcao,
    DateTime? OnBlockUtc,
    string? Stand,
    string? RotationCode,
    int? RotationLeg,
    DateTime RowVersion);

/// <summary>
/// The rules one payload can answer by itself, the same a row of the table is held to (<see cref="SlotDraft"/>); whether its
/// airports and its types exist, whether it is at an airport of its event, once in it and in its place in a rotation, is the save's.
/// Messages are i18n keys.
/// </summary>
public sealed class EventSlotWriteDtoValidator : AbstractValidator<EventSlotWriteDto>
{
    public EventSlotWriteDtoValidator()
    {
        RuleFor(slot => slot.EventId).GreaterThan(0).WithMessage("errors.required");

        RuleFor(slot => slot.Callsign)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(EventSlot.MaxCodeLength).WithMessage("errors.text.tooLong");
        RuleFor(slot => slot.Callsign)
            .Must(callsign => SlotValues.IsCallsign(callsign.Trim().ToUpperInvariant()))
            .When(slot => !string.IsNullOrWhiteSpace(slot.Callsign) && slot.Callsign.Trim().Length <= EventSlot.MaxCodeLength)
            .WithMessage("events:errors.callsignFormat");
        RuleFor(slot => slot.FlightNumber).MaximumLength(EventSlot.MaxCodeLength).WithMessage("errors.text.tooLong");

        RuleFor(slot => slot.MainAircraftType).NotEmpty().WithMessage("errors.required");
        RuleFor(slot => slot.MainAircraftType)
            .Must(main => SlotValues.IsAircraftType(main.Trim().ToUpperInvariant()))
            .When(slot => !string.IsNullOrWhiteSpace(slot.MainAircraftType))
            .WithMessage("events:errors.aircraftFormat");
        RuleFor(slot => slot.OtherAircraftTypes)
            .Must(others => SlotValues.AircraftTypes(others).All(SlotValues.IsAircraftType)).WithMessage("events:errors.aircraftFormat")
            .Must((slot, others) => SlotValues.MainFirst(slot.MainAircraftType, others).Count <= EventSlot.MaxAircraftTypes)
            .WithMessage("events:errors.aircraftTooMany");

        RuleFor(slot => slot.DepartureIcao).NotEmpty().WithMessage("errors.required");
        RuleFor(slot => slot.ArrivalIcao).NotEmpty().WithMessage("errors.required");
        RuleFor(slot => slot.ArrivalIcao)
            .Must((slot, arrival) => !string.Equals(arrival.Trim(), slot.DepartureIcao.Trim(), StringComparison.OrdinalIgnoreCase))
            .When(slot => !string.IsNullOrWhiteSpace(slot.DepartureIcao) && !string.IsNullOrWhiteSpace(slot.ArrivalIcao))
            .WithMessage("events:errors.slotToItself");

        RuleFor(slot => slot.OffBlockUtc).NotNull().WithMessage("errors.required");
        RuleFor(slot => slot.OnBlockUtc).NotNull().WithMessage("errors.required");
        RuleFor(slot => slot.OnBlockUtc)
            .Must((slot, onBlock) => onBlock > slot.OffBlockUtc)
            .When(slot => slot.OffBlockUtc is not null && slot.OnBlockUtc is not null)
            .WithMessage("events:errors.onBlockBeforeOffBlock");

        RuleFor(slot => slot.Stand).MaximumLength(EventSlot.MaxStandLength).WithMessage("errors.text.tooLong");
        RuleFor(slot => slot.RotationCode).MaximumLength(EventSlot.MaxRotationLength).WithMessage("errors.text.tooLong");
        RuleFor(slot => slot.RotationLeg)
            .InclusiveBetween(1, EventSlot.MaxLeg).When(slot => slot.RotationLeg is not null)
            .WithMessage("events:errors.legRange");
        RuleFor(slot => slot.RotationLeg)
            .Null().When(slot => string.IsNullOrWhiteSpace(slot.RotationCode))
            .WithMessage("events:errors.legWithoutRotation");
    }
}

/// <summary>Slots to and from their payloads: by hand, the codes in upper case as the core writes them.</summary>
internal static class SlotMapper
{
    public static EventSlotDto ToDto(EventSlot slot) => new(
        slot.Id,
        slot.EventId,
        slot.OwnerDepartment,
        slot.Kind,
        slot.EventAirportIcao,
        slot.IsArrival,
        slot.Callsign,
        slot.FlightNumber,
        slot.AircraftTypes,
        slot.DepartureIcao,
        slot.ArrivalIcao,
        slot.OffBlockUtc,
        slot.OnBlockUtc,
        slot.Stand,
        slot.RotationCode,
        slot.RotationLeg,
        slot.Generated,
        slot.UpdatedAt,
        slot.RowVersion);

    /// <summary>The event and the kind only on a new row: a slot never moves to another event, and the form writes public ones.</summary>
    public static void Apply(EventSlotWriteDto payload, EventSlot slot)
    {
        if (slot.Id == 0)
        {
            slot.EventId = payload.EventId;
            slot.Kind = SlotKind.Public;
        }

        slot.Callsign = payload.Callsign.Trim().ToUpperInvariant();
        slot.FlightNumber = Text(payload.FlightNumber)?.ToUpperInvariant();
        slot.AircraftTypes = SlotValues.MainFirst(payload.MainAircraftType, payload.OtherAircraftTypes);
        slot.DepartureIcao = payload.DepartureIcao.Trim().ToUpperInvariant();
        slot.ArrivalIcao = payload.ArrivalIcao.Trim().ToUpperInvariant();
        slot.OffBlockUtc = payload.OffBlockUtc;
        slot.OnBlockUtc = payload.OnBlockUtc;
        slot.Stand = Text(payload.Stand);
        slot.RotationCode = Text(payload.RotationCode);
        slot.RotationLeg = slot.RotationCode is null ? null : payload.RotationLeg;
        slot.RowVersion = payload.RowVersion;
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The slots of an event (design M4 §1.5, §3.1, §7.2, E5): a resource of the CRUD engine filtered by <c>filter[eventId]</c>, read
/// with <c>EventBookings.View</c> and written with <c>EventBookings.Edit</c> on the event's care — the tab «Slots» of the event's
/// page is a generated list, and each slot a generated form for the corrections after a load —, and three verbs on the slots of one
/// event, written by hand next to it: <b>load</b> a table, pasted or from a file (<see cref="SlotLoading"/>), <b>delete the free
/// ones</b>, and <b>generate the private slots</b> (E7, <see cref="PrivateSlotGeneration"/>). All three are verbs of design §7.2
/// («incollare»; «genera gli slot privati», «elimina i liberi» of the tab).
/// </summary>
public static class EventSlotEndpoints
{
    public const string Pattern = "/api/events/slots";

    /// <summary>The verbs on the slots of one event, under the event: <c>/api/events/events/{id}/slots/…</c>.</summary>
    public const string EventSlotsPattern = $"{EventEndpoints.Pattern}/{{id:long}}/slots";

    public static IEndpointRouteBuilder MapEventSlotEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventSlot, EventSlotDto, EventSlotDto, EventSlotWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.BookingsArea;
            options.Name = "EventSlots";
            options.ReadPolicy = EventsPermissions.BookingsView;
            options.WritePolicy = EventsPermissions.BookingsEdit;
            options.ContextType = typeof(EventsDbContext);

            // By the time at the airport of the event: the off block of a departure, else the on block of an arrival.
            options.DefaultOrder = slot => slot.OffBlockUtc ?? slot.OnBlockUtc;
            options.Sortable.Add(nameof(EventSlot.OffBlockUtc));
            options.Sortable.Add(nameof(EventSlot.OnBlockUtc));
            options.Sortable.Add(nameof(EventSlot.Callsign));
            options.Sortable.Add(nameof(EventSlot.RotationCode));
            options.Filterable.Add(nameof(EventSlot.EventId));
            options.Filterable.Add(nameof(EventSlot.Kind));
            options.SearchFields.Add(slot => slot.Callsign);
            options.SearchFields.Add(slot => slot.FlightNumber);
            options.SearchFields.Add(slot => slot.DepartureIcao);
            options.SearchFields.Add(slot => slot.ArrivalIcao);
            options.SearchFields.Add(slot => slot.RotationCode);

            options.ToList = SlotMapper.ToDto;
            options.ToDetail = SlotMapper.ToDto;
            options.Apply = SlotMapper.Apply;
            options.BeforeAuthorize = async (slot, saving) =>
                (await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(slot, saving.CancellationToken)).Refusal;
            options.BeforeSave = (slot, saving) =>
                saving.Services.GetRequiredService<SlotSaving>().PrepareAsync(slot, saving.IsNew, saving.CancellationToken);

            // A booked slot whose flight a correction changed: its pilot is told (E6a, Carmine's answer 3 on #233).
            options.AfterSave = (slot, saving) =>
                saving.Services.GetRequiredService<SlotSaving>().AfterSaveAsync(slot, saving.CancellationToken);

            // A booked slot is not deleted (§1.5, E6a): the staff take its booking away first.
            options.Delete = (slot, services, cancellationToken) =>
                services.GetRequiredService<SlotSaving>().DeleteAsync(slot, cancellationToken);
        });

        app.MapPost($"{EventSlotsPattern}/load", LoadAsync)
            .WithName("EventsSlotsLoad")
            .WithTags("EventSlots")
            .Produces<SlotLoadResultDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.BookingsEdit);

        app.MapPost($"{EventSlotsPattern}/delete-free", DeleteFreeAsync)
            .WithName("EventsSlotsDeleteFree")
            .WithTags("EventSlots")
            .Produces<SlotsDeletedDto>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.BookingsEdit);

        app.MapPost($"{EventSlotsPattern}/generate", GenerateAsync)
            .WithName("EventsSlotsGenerate")
            .WithTags("EventSlots")
            .Produces<PrivateSlotsGeneratedDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(EventsPermissions.BookingsEdit);

        return app;
    }

    /// <summary>
    /// «Generate the private slots» (§3.2, §7.2, E7): the free private slots of the event are replaced by the ones its airports' capacity
    /// leaves room for, the booked ones stay and count. Asked of the one handler on the event, as every write of its slots is. A pilot
    /// booking one of the free slots in the same moment, or another write taking them first, is a 409: read the slots again.
    /// </summary>
    private static async Task<IResult> GenerateAsync(
        long id,
        EventsDbContext database,
        PrivateSlotGeneration generation,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var (row, refusal) = await WritableEventAsync(id, database, authorization, currentUser, catalog, http);
        if (row is null)
        {
            return refusal!;
        }

        try
        {
            var (result, problems) = await generation.GenerateAsync(row, http.RequestAborted);
            return result is null ? CrudProblems.Validation(problems, catalog, currentUser.Locale) : Results.Ok(result);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException || DatabaseErrors.SlotWasBooked(exception))
        {
            return Problem(StatusCodes.Status409Conflict, CrudProblems.ConflictTitleKey, catalog, currentUser);
        }
    }

    /// <summary>
    /// Loads the public slots of an event from a table (§3.1): all or nothing, each refusal on its row and column. Asked of the one
    /// handler on the event, as every write of its slots is.
    /// </summary>
    private static async Task<IResult> LoadAsync(
        long id,
        SlotLoadRequest request,
        EventsDbContext database,
        SlotLoading loading,
        IValidator<SlotLoadRequest> validator,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var (row, refusal) = await WritableEventAsync(id, database, authorization, currentUser, catalog, http);
        if (row is null)
        {
            return refusal!;
        }

        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        try
        {
            var (result, problems) = await loading.LoadAsync(row, request, http.RequestAborted);
            return result is null ? CrudProblems.Validation(problems, catalog, currentUser.Locale) : Results.Ok(result);
        }
        catch (Exception exception) when (DatabaseErrors.TookTheKey(exception) || DatabaseErrors.SlotWasBooked(exception))
        {
            // Another write of the same rows in the same moment: a load of the same event took a slot this one wanted, or a pilot
            // booked a free slot this one replaces (E6a). Read the slots again and load again. Any other failure is not answered
            // with «load again», which would be said for ever: it surfaces as itself.
            return Problem(StatusCodes.Status409Conflict, CrudProblems.ConflictTitleKey, catalog, currentUser);
        }
    }

    /// <summary>
    /// «Delete the free ones» (§7.2): every slot of the event nobody booked goes, public and private, each with its audit row. A slot a
    /// pilot books in the same moment stays — the key of the booking stops its delete —, and the staff are told to look again.
    /// </summary>
    private static async Task<IResult> DeleteFreeAsync(
        long id,
        EventsDbContext database,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var (row, refusal) = await WritableEventAsync(id, database, authorization, currentUser, catalog, http);
        if (row is null)
        {
            return refusal!;
        }

        var free = await SlotRows.Free(database, row.Id).ToListAsync(http.RequestAborted);
        database.Slots.RemoveRange(free);

        try
        {
            await database.SaveChangesAsync(http.RequestAborted);
        }
        catch (Exception exception) when (DatabaseErrors.SlotWasBooked(exception))
        {
            return Problem(StatusCodes.Status409Conflict, CrudProblems.ConflictTitleKey, catalog, currentUser);
        }

        return Results.Ok(new SlotsDeletedDto(free.Count));
    }

    /// <summary>The event, when its slots are the reader's to write: 404 for none, 403 for one whose slots are not.</summary>
    private static async Task<(Event? Event, IResult? Refusal)> WritableEventAsync(
        long id,
        EventsDbContext database,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, http.RequestAborted);

        if (row is null)
        {
            return (null, Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser));
        }

        return (await authorization.AuthorizeAsync(http.User, row, EventsPermissions.BookingsEdit)).Succeeded
            ? (row, null)
            : (null, Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser));
    }

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}

/// <summary>
/// What a save of one slot may refuse only by looking at other rows (design M4 §1.5, §3.1), run by the CRUD engine before every
/// save of the form: the same as a row of a load — its airports and its types the core's, one of its airports the event's, which
/// gives it its airport and its direction; its time there inside the event's window with a margin; its callsign and off block once
/// in the event; its place in its rotation — so that a correction cannot make what a load would refuse. And what deleting one
/// refuses: a slot a pilot booked (E6a).
/// <para>A booked slot is still corrected, and its booking stays (E6a, Carmine's answer 3 on #233): when the correction changes the
/// flight its pilot booked — the callsign, the times, the airports, the aircraft types it admits —, the pilot is told once the save
/// is done (<see cref="EventsNotifications.BookingChanged"/>). Nothing is refused and nothing is checked again against the pilot's
/// other bookings: a retiming of many slots must not make the staff take the bookings away one by one.</para>
/// </summary>
public sealed class SlotSaving(
    EventsDbContext database,
    EventChildren children,
    IAirportDirectory airports,
    IAircraftTypeDirectory aircraft,
    ModuleSettingsStore settings,
    EventsMail mail)
{
    /// <summary>Whether the save being prepared changes the flight a pilot may have booked: what <see cref="AfterSaveAsync"/> tells.</summary>
    private bool _flightChanged;

    /// <summary>The fields of the form, by the columns of the table the rules name: the aircraft types are two fields, below.</summary>
    private static readonly Dictionary<string, string> Fields = new(StringComparer.Ordinal)
    {
        [SlotColumns.Callsign] = "callsign",
        [SlotColumns.FlightNumber] = "flightNumber",
        [SlotColumns.DepartureIcao] = "departureIcao",
        [SlotColumns.OffBlockUtc] = "offBlockUtc",
        [SlotColumns.ArrivalIcao] = "arrivalIcao",
        [SlotColumns.OnBlockUtc] = "onBlockUtc",
        [SlotColumns.Stand] = "stand",
        [SlotColumns.Rotation] = "rotationCode",
        [SlotColumns.Leg] = "rotationLeg",
    };

    /// <summary>The field of the main aircraft type, the first of the slot's types.</summary>
    private const string MainTypeField = "mainAircraftType";

    /// <summary>The field of the other aircraft types the slot admits.</summary>
    private const string OtherTypesField = "otherAircraftTypes";

    public async Task<IReadOnlyDictionary<string, string[]>?> PrepareAsync(EventSlot slot, bool isNew, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);

        // Read before anything is refused, and told only after the save, as new times of an event are.
        _flightChanged = !isNew && FlightChanged(slot);

        if (await children.EventAsync(slot.EventId, cancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventUnknown");
        }

        // A private slot is the generator's (E7): it is not corrected as a flight.
        if (slot.Kind != SlotKind.Public)
        {
            return EventChildren.Refusal("callsign", "events:errors.slotNotPublic");
        }

        if (isNew && !parent.PublicSlots)
        {
            return EventChildren.Refusal("eventId", "events:errors.noPublicSlots");
        }

        var refusals = new Refusals();
        var eventAirports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == slot.EventId)
            .Select(airport => airport.Icao)
            .ToListAsync(cancellationToken);

        var known = await airports.FindAsync([slot.DepartureIcao!, slot.ArrivalIcao!], cancellationToken);
        if (!known.ContainsKey(slot.DepartureIcao!))
        {
            refusals.Add(Fields[SlotColumns.DepartureIcao], "events:errors.airportUnknown");
        }

        if (!known.ContainsKey(slot.ArrivalIcao!))
        {
            refusals.Add(Fields[SlotColumns.ArrivalIcao], "events:errors.airportUnknown");
        }

        // Each refused on its own field: the main type is the first of the slot's types, the others follow it.
        var unknownTypes = (await aircraft.UnknownAsync([.. slot.AircraftTypes], cancellationToken)).ToHashSet(StringComparer.Ordinal);
        if (slot.AircraftTypes.Take(1).Any(unknownTypes.Contains))
        {
            refusals.Add(MainTypeField, "events:errors.aircraftUnknown");
        }

        if (slot.AircraftTypes.Skip(1).Any(unknownTypes.Contains))
        {
            refusals.Add(OtherTypesField, "events:errors.aircraftUnknown");
        }

        if (SlotDirection.Of(slot.DepartureIcao!, slot.ArrivalIcao!, eventAirports) is { } direction)
        {
            slot.EventAirportIcao = direction.EventAirportIcao;
            slot.IsArrival = direction.IsArrival;

            // Days away from the event is a typing mistake (point 10 on #228), as in a load.
            if (!SlotWindow.Holds(SlotWindow.AtTheEvent(direction, slot.OffBlockUtc!.Value, slot.OnBlockUtc!.Value), parent.StartsAtUtc, parent.EndsAtUtc))
            {
                refusals.Add(Fields[direction.IsArrival ? SlotColumns.OnBlockUtc : SlotColumns.OffBlockUtc], SlotWindow.OutsideKey);
            }
        }
        else
        {
            refusals.Add(Fields[SlotColumns.DepartureIcao], "events:errors.slotAwayFromEvent");
        }

        var others = await database.Slots.AsNoTracking()
            .Where(other => other.EventId == slot.EventId && other.Id != slot.Id)
            .Where(other => other.Callsign == slot.Callsign || (slot.RotationCode != null && other.RotationCode == slot.RotationCode))
            .ToListAsync(cancellationToken);

        if (others.Any(other => other.Callsign == slot.Callsign && other.OffBlockUtc == slot.OffBlockUtc))
        {
            refusals.Add(Fields[SlotColumns.Callsign], "events:errors.slotTwice");
        }

        if (slot.RotationCode is { } rotation)
        {
            var gap = (await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken)).BookingGapMinutes;
            var chains = SlotChains.Check(
                [
                    .. others
                        .Where(other => string.Equals(other.RotationCode, rotation, StringComparison.Ordinal)
                            && other.DepartureIcao is not null && other.ArrivalIcao is not null
                            && other.OffBlockUtc is not null && other.OnBlockUtc is not null)
                        .Select(other => new ChainLeg(
                            other.Id,
                            IsNew: false,
                            rotation,
                            other.RotationLeg,
                            other.DepartureIcao!,
                            other.ArrivalIcao!,
                            other.OffBlockUtc!.Value,
                            other.OnBlockUtc!.Value)),
                    new ChainLeg(
                        slot,
                        IsNew: true,
                        rotation,
                        slot.RotationLeg,
                        slot.DepartureIcao!,
                        slot.ArrivalIcao!,
                        slot.OffBlockUtc!.Value,
                        slot.OnBlockUtc!.Value),
                ],
                gap);

            foreach (var problem in chains.Problems)
            {
                refusals.Add(Fields[problem.Column], problem.Key);
            }

            // The first leg of a rotation written without its place takes the first one.
            if (slot.RotationLeg is null && chains.Assigned.TryGetValue(slot, out var place))
            {
                slot.RotationLeg = place;
            }
        }

        return refusals.IsEmpty ? null : refusals.Errors;
    }

    /// <summary>
    /// What follows a correction that changed the flight of a booked slot (E6a, Carmine's answer 3 on #233): its pilot is told the
    /// flight as it is now, and keeps the booking and the aircraft they chose. Nothing for a slot nobody booked, and nothing when only
    /// the stand, the flight number, the rotation or which admitted type comes first moved.
    /// </summary>
    public async Task AfterSaveAsync(EventSlot slot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (!_flightChanged)
        {
            return;
        }

        _flightChanged = false;
        var booking = await CrudSource.BackOffice<EventBooking>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.SlotId == slot.Id, cancellationToken);
        if (booking is null || await children.EventAsync(slot.EventId, cancellationToken) is not { } parent)
        {
            return;
        }

        await mail.BookingChangedAsync(parent, slot, booking, cancellationToken);
    }

    /// <summary>
    /// Whether the correction changes what a pilot booked: the callsign they fly, the times, the two airports, or the aircraft types
    /// the slot admits — as a set, so that only putting another of them first is no change. The stand and the flight number are the
    /// staff's to plan with, and the rotation the slot's place in a day.
    /// </summary>
    private bool FlightChanged(EventSlot slot)
    {
        var entry = database.Entry(slot);
        object? Was(string property) => entry.Property(property).OriginalValue;

        return !Equals(Was(nameof(EventSlot.Callsign)), slot.Callsign)
            || !Equals(Was(nameof(EventSlot.DepartureIcao)), slot.DepartureIcao)
            || !Equals(Was(nameof(EventSlot.ArrivalIcao)), slot.ArrivalIcao)
            || !Equals(Was(nameof(EventSlot.OffBlockUtc)), slot.OffBlockUtc)
            || !Equals(Was(nameof(EventSlot.OnBlockUtc)), slot.OnBlockUtc)
            || Was(nameof(EventSlot.AircraftTypesJson)) is not string types
            || !new EventSlot { AircraftTypesJson = types }.AircraftTypes.ToHashSet(StringComparer.Ordinal).SetEquals(slot.AircraftTypes);
    }

    /// <summary>
    /// Deleting one slot (§1.5): only one nobody booked — a booked slot is not deleted, the staff take its booking away first, and the
    /// key of the booking holds it in the database too.
    /// </summary>
    public async Task DeleteAsync(EventSlot slot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);

        if (await CrudSource.BackOffice<EventBooking>(database).AnyAsync(booking => booking.SlotId == slot.Id, cancellationToken))
        {
            throw new DomainRefusalException("id", "events:errors.slotBooked");
        }

        database.Slots.Remove(slot);
    }
}
