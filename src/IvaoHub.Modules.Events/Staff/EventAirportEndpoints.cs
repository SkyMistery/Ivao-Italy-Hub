using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>An airport of an event as its list and its form show it (design M4 §1.3).</summary>
public sealed record EventAirportDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    string Icao,
    int Ordinal,
    int? MaxMovementsPerHour,
    int? MaxArrivalsPerHour,
    int? MaxDeparturesPerHour,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on an airport of an event. The event is chosen when it is created and never changes; the care is the
/// event's, taken before the permission is asked. The capacity is either movements an hour, or arrivals and departures an hour,
/// and it may wait: the private slots read it (E7).
/// </summary>
public sealed record EventAirportWriteDto(
    long EventId,
    string Icao,
    int Ordinal,
    int? MaxMovementsPerHour,
    int? MaxArrivalsPerHour,
    int? MaxDeparturesPerHour,
    DateTime RowVersion);

/// <summary>The rules one payload can answer by itself; whether the airport exists and is once in its event is the save's. Messages are i18n keys.</summary>
public sealed class EventAirportWriteDtoValidator : AbstractValidator<EventAirportWriteDto>
{
    public EventAirportWriteDtoValidator()
    {
        RuleFor(airport => airport.EventId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(airport => airport.Icao).NotEmpty().WithMessage("errors.required");
        RuleFor(airport => airport.Ordinal).InclusiveBetween(0, EventAirport.MaxOrdinal).WithMessage("errors.number.range");

        RuleFor(airport => airport.MaxMovementsPerHour)
            .InclusiveBetween(1, EventAirport.MaxPerHour).When(airport => airport.MaxMovementsPerHour is not null)
            .WithMessage("errors.number.range");
        RuleFor(airport => airport.MaxArrivalsPerHour)
            .InclusiveBetween(1, EventAirport.MaxPerHour).When(airport => airport.MaxArrivalsPerHour is not null)
            .WithMessage("errors.number.range");
        RuleFor(airport => airport.MaxDeparturesPerHour)
            .InclusiveBetween(1, EventAirport.MaxPerHour).When(airport => airport.MaxDeparturesPerHour is not null)
            .WithMessage("errors.number.range");

        // One way of saying the capacity or the other (§1.3): movements, or arrivals and departures — never both.
        RuleFor(airport => airport.MaxMovementsPerHour)
            .Null()
            .When(airport => airport.MaxArrivalsPerHour is not null || airport.MaxDeparturesPerHour is not null)
            .WithMessage("events:errors.capacityEitherOr");
    }
}

/// <summary>Airports to and from their payloads: by hand, the ICAO in upper case as the core writes it.</summary>
internal static class AirportMapper
{
    public static EventAirportDto ToDto(EventAirport airport) => new(
        airport.Id,
        airport.EventId,
        airport.OwnerDepartment,
        airport.Icao,
        airport.Ordinal,
        airport.MaxMovementsPerHour,
        airport.MaxArrivalsPerHour,
        airport.MaxDeparturesPerHour,
        airport.UpdatedAt,
        airport.RowVersion);

    /// <summary>The event only on a new row: an airport never moves to another event.</summary>
    public static void Apply(EventAirportWriteDto payload, EventAirport airport)
    {
        if (airport.Id == 0)
        {
            airport.EventId = payload.EventId;
        }

        airport.Icao = payload.Icao.Trim().ToUpperInvariant();
        airport.Ordinal = payload.Ordinal;
        airport.MaxMovementsPerHour = payload.MaxMovementsPerHour;
        airport.MaxArrivalsPerHour = payload.MaxArrivalsPerHour;
        airport.MaxDeparturesPerHour = payload.MaxDeparturesPerHour;
        airport.RowVersion = payload.RowVersion;
    }
}

/// <summary>
/// The airports of an event and their capacity (design M4 §1.3, §7.2, E3a): a resource of the CRUD engine filtered by
/// <c>filter[eventId]</c>, read with <c>Events.View</c> and written with <c>Events.Edit</c> on the event's care — the tab of the
/// event's page is a generated list, and each airport a generated form. No hand written verb.
/// </summary>
public static class EventAirportEndpoints
{
    public const string Pattern = "/api/events/airports";

    public static IEndpointRouteBuilder MapEventAirportEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventAirport, EventAirportDto, EventAirportDto, EventAirportWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.Area;
            options.Name = "EventAirports";
            options.ReadPolicy = EventsPermissions.View;
            options.WritePolicy = EventsPermissions.Edit;
            options.ContextType = typeof(EventsDbContext);

            options.DefaultOrder = airport => airport.Ordinal;
            options.Sortable.Add(nameof(EventAirport.Ordinal));
            options.Filterable.Add(nameof(EventAirport.EventId));

            options.ToList = AirportMapper.ToDto;
            options.ToDetail = AirportMapper.ToDto;
            options.Apply = AirportMapper.Apply;
            options.BeforeAuthorize = async (airport, saving) =>
                (await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(airport, saving.CancellationToken)).Refusal;
            options.BeforeSave = SaveAsync;
        });

        return app;
    }

    /// <summary>
    /// An airport belongs to an event about its own airports — one about the whole division has none (§1.2) —, is one the core
    /// knows, and is in its event once.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventAirport airport, CrudSaving saving)
    {
        var children = saving.Services.GetRequiredService<EventChildren>();
        var database = (EventsDbContext)saving.Database;

        if (await children.EventAsync(airport.EventId, saving.CancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventUnknown");
        }

        if (parent.WholeDivision)
        {
            return EventChildren.Refusal("eventId", "events:errors.wholeDivisionHasNoAirports");
        }

        var found = await saving.Services.GetRequiredService<IAirportDirectory>().FindAsync([airport.Icao], saving.CancellationToken);
        if (!found.ContainsKey(airport.Icao))
        {
            return EventChildren.Refusal("icao", "events:errors.airportUnknown");
        }

        if (await database.Airports.AnyAsync(
            row => row.EventId == airport.EventId && row.Icao == airport.Icao && row.Id != airport.Id,
            saving.CancellationToken))
        {
            return EventChildren.Refusal("icao", "events:errors.airportTwice");
        }

        return null;
    }
}
