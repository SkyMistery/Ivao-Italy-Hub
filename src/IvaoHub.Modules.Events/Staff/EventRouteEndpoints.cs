using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>A route of an event as its list and its form show it (design M4 §1.4).</summary>
public sealed record EventRouteDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    string DepartureIcao,
    string ArrivalIcao,
    string Route,
    Localized<string>? Remarks,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on a route of an event. The event is chosen when it is created and never changes; the care is the
/// event's, taken before the permission is asked. No remarks is none.
/// </summary>
public sealed record EventRouteWriteDto(
    long EventId,
    string DepartureIcao,
    string ArrivalIcao,
    string Route,
    Localized<string>? Remarks,
    DateTime RowVersion);

/// <summary>
/// The rules one payload can answer by itself; whether its airports exist is the save's. A route goes from one airport to another,
/// never back to the one it leaves. The remarks are read by everybody on the page of the event, so once written in one language
/// they are written in every language of the division, as every text the site shows. Messages are i18n keys.
/// </summary>
public sealed class EventRouteWriteDtoValidator : AbstractValidator<EventRouteWriteDto>
{
    public EventRouteWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(route => route.EventId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(route => route.DepartureIcao).NotEmpty().WithMessage("errors.required");
        RuleFor(route => route.ArrivalIcao).NotEmpty().WithMessage("errors.required");
        RuleFor(route => route.ArrivalIcao)
            .Must((route, arrival) => !string.Equals(arrival.Trim(), route.DepartureIcao.Trim(), StringComparison.OrdinalIgnoreCase))
            .When(route => !string.IsNullOrWhiteSpace(route.DepartureIcao) && !string.IsNullOrWhiteSpace(route.ArrivalIcao))
            .WithMessage("events:errors.routeToItself");
        RuleFor(route => route.Route)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(EventRoute.MaxRouteLength).WithMessage("errors.text.tooLong");

        RuleFor(route => route.Remarks!).Required(division.Value).When(route => RouteMapper.IsWritten(route.Remarks));
        RuleFor(route => route.Remarks)
            .Must(remarks => remarks is null || remarks.Values.All(text => (text?.Length ?? 0) <= EventRoute.MaxRemarksLength))
            .WithMessage("errors.text.tooLong");
    }
}

/// <summary>Routes to and from their payloads: by hand, the airports in upper case as the core writes them.</summary>
internal static class RouteMapper
{
    public static EventRouteDto ToDto(EventRoute route) => new(
        route.Id,
        route.EventId,
        route.OwnerDepartment,
        route.DepartureIcao,
        route.ArrivalIcao,
        route.Route,
        route.Remarks,
        route.UpdatedAt,
        route.RowVersion);

    /// <summary>The event only on a new row: a route never moves to another event. Remarks written in no language are none.</summary>
    public static void Apply(EventRouteWriteDto payload, EventRoute route)
    {
        if (route.Id == 0)
        {
            route.EventId = payload.EventId;
        }

        route.DepartureIcao = payload.DepartureIcao.Trim().ToUpperInvariant();
        route.ArrivalIcao = payload.ArrivalIcao.Trim().ToUpperInvariant();
        route.Route = payload.Route.Trim();
        route.Remarks = IsWritten(payload.Remarks) ? payload.Remarks : null;
        route.RowVersion = payload.RowVersion;
    }

    /// <summary>Whether the remarks say something in some language: then they say it in every one.</summary>
    public static bool IsWritten(Localized<string>? remarks) =>
        remarks is not null && remarks.Values.Any(text => !string.IsNullOrWhiteSpace(text));
}

/// <summary>
/// The routes of an event (design M4 §1.4, §7.2, E4): a resource of the CRUD engine filtered by <c>filter[eventId]</c>, read with
/// <c>EventRoutes.View</c> and written with <c>EventRoutes.Edit</c> on the event's care — the area of the flight operations, who
/// hold it on the base department of the module from a grant to their positions (§6.2), and who therefore write the routes of
/// every event and not the event. The tab of the event's page is a generated list, and each route a generated form. No hand
/// written verb.
/// </summary>
public static class EventRouteEndpoints
{
    public const string Pattern = "/api/events/routes";

    public static IEndpointRouteBuilder MapEventRouteEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventRoute, EventRouteDto, EventRouteDto, EventRouteWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.RoutesArea;
            options.Name = "EventRoutes";
            options.ReadPolicy = EventsPermissions.RoutesView;
            options.WritePolicy = EventsPermissions.RoutesEdit;
            options.ContextType = typeof(Data.EventsDbContext);

            options.DefaultOrder = route => route.DepartureIcao;
            options.Sortable.Add(nameof(EventRoute.DepartureIcao));
            options.Sortable.Add(nameof(EventRoute.ArrivalIcao));
            options.Filterable.Add(nameof(EventRoute.EventId));

            options.ToList = RouteMapper.ToDto;
            options.ToDetail = RouteMapper.ToDto;
            options.Apply = RouteMapper.Apply;
            options.BeforeAuthorize = async (route, saving) =>
                (await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(route, saving.CancellationToken)).Refusal;
            options.BeforeSave = SaveAsync;
        });

        return app;
    }

    /// <summary>Both ends of a route are airports the core knows. Several routes may join the same two: the remarks say which is which.</summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventRoute route, CrudSaving saving)
    {
        var found = await saving.Services.GetRequiredService<IAirportDirectory>()
            .FindAsync([route.DepartureIcao, route.ArrivalIcao], saving.CancellationToken);
        var refusals = new Refusals();

        if (!found.ContainsKey(route.DepartureIcao))
        {
            refusals.Add("departureIcao", "events:errors.airportUnknown");
        }

        if (!found.ContainsKey(route.ArrivalIcao))
        {
            refusals.Add("arrivalIcao", "events:errors.airportUnknown");
        }

        return refusals.IsEmpty ? null : refusals.Errors;
    }
}
