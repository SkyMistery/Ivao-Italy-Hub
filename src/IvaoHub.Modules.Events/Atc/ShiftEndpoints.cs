using System.Globalization;
using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>A shift of the roster as its form loads it (design M4 §1.7): its position, its controller, its hours, where it comes from.</summary>
public sealed record EventAtcShiftDto(
    long Id,
    long EventId,
    long PositionId,
    Department OwnerDepartment,
    int ControllerVid,
    string? Fir,
    DateTime FromUtc,
    DateTime ToUtc,
    ShiftOrigin Origin,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What the staff may set on a shift (§4.4): the position — of the same event, on a change —, the controller by VID, and the hours. The event,
/// the care and the FIR are the position's, taken before the permission is asked; the origin is the staff's.
/// </summary>
public sealed record EventAtcShiftWriteDto(long PositionId, int ControllerVid, DateTime FromUtc, DateTime ToUtc, DateTime RowVersion);

/// <summary>The rules one payload can answer by itself; whether it fits its position and the roster is the save's.</summary>
public sealed class EventAtcShiftWriteDtoValidator : AbstractValidator<EventAtcShiftWriteDto>
{
    public EventAtcShiftWriteDtoValidator()
    {
        RuleFor(shift => shift.PositionId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(shift => shift.ControllerVid).GreaterThan(0).WithMessage("errors.required");
        RuleFor(shift => shift.FromUtc).NotEmpty().WithMessage("errors.required");
        RuleFor(shift => shift.ToUtc)
            .GreaterThan(shift => shift.FromUtc)
            .WithMessage(AtcRules.EndsBeforeItStartsKey);
    }
}

/// <summary>
/// The correction of the roster (design M4 §4.4, §7.2, E11b; note 2026-10-13-la-proposta-del-roster, readings 6 and 7).
/// <list type="bullet">
/// <item><b>The shifts</b>: a resource of the CRUD engine, read with <c>EventAtc.View</c> and written with <c>EventAtc.Edit</c> on the event's
/// care and on the position's FIR — «add» creates, «move» changes the controller, the hours or the position, «take away» deletes —, never on
/// a shift of whoever writes (the permission is denied to the controller of the row, the super administrator included). From the proposal to
/// the end of the event, never on a cancelled one; refused only for what cannot be: a controller who never signed in, hours outside the
/// position, the position held by somebody else in those hours. Every other rule of the proposal is a warning on the roster.</item>
/// <item>⚠️ <b>One hand written read</b> (reading 7), the sixth of the ATC: the roster of an event by position and hour, the shifts and the
/// uncovered pieces together — which are no rows —, with the one handler asked on every position, so the staff of a FIR read the roster of
/// their positions.</item>
/// </list>
/// </summary>
public static class ShiftEndpoints
{
    public const string Pattern = "/api/events/atc/shifts";

    /// <summary>The roster of an event: <c>/api/events/atc/events/{id}/roster</c>.</summary>
    public const string RosterPattern = "/api/events/atc/events/{id:long}/roster";

    public static IEndpointRouteBuilder MapShiftEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventAtcShift, EventAtcShiftDto, EventAtcShiftDto, EventAtcShiftWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.AtcArea;
            options.Name = "EventAtcShifts";
            options.ReadPolicy = EventsPermissions.AtcView;
            options.WritePolicy = EventsPermissions.AtcEdit;
            options.ContextType = typeof(EventsDbContext);

            options.DefaultOrder = shift => shift.FromUtc;
            options.Sortable.Add(nameof(EventAtcShift.FromUtc));
            options.Filterable.Add(nameof(EventAtcShift.EventId));
            options.Filterable.Add(nameof(EventAtcShift.PositionId));

            options.ToList = ToDto;
            options.ToDetail = ToDto;
            options.Apply = Apply;
            options.BeforeAuthorize = AdoptAsync;
            options.BeforeSave = SaveAsync;
            options.Delete = DeleteAsync;
        });

        app.MapGet(RosterPattern, RosterAsync)
            .WithTags("EventAtc")
            .WithName("EventsAtcRoster")
            .Produces<PagedResult<RosterRowDto>>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.AtcView);

        return app;
    }

    private static EventAtcShiftDto ToDto(EventAtcShift shift) => new(
        shift.Id,
        shift.EventId,
        shift.PositionId,
        shift.OwnerDepartment,
        shift.ControllerVid,
        shift.Fir,
        shift.FromUtc,
        shift.ToUtc,
        shift.Origin,
        shift.UpdatedAt,
        shift.RowVersion);

    /// <summary>
    /// A write of the staff: the shift is theirs from now on, and the why of a proposal no longer says why it is held — the audit keeps it
    /// (reading 4).
    /// </summary>
    private static void Apply(EventAtcShiftWriteDto payload, EventAtcShift shift)
    {
        shift.PositionId = payload.PositionId;
        shift.ControllerVid = payload.ControllerVid;
        shift.FromUtc = payload.FromUtc;
        shift.ToUtc = payload.ToUtc;
        shift.Origin = ShiftOrigin.Staff;
        shift.Reason = null;
        shift.RowVersion = payload.RowVersion;
    }

    /// <summary>
    /// What a shift takes from its position before its permission is asked: the event, the FIR, and through the event its care. A shift
    /// moves only among the positions of its event.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> AdoptAsync(EventAtcShift shift, CrudSaving saving)
    {
        var database = (EventsDbContext)saving.Database;
        var position = await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == shift.PositionId, saving.CancellationToken);
        if (position is null)
        {
            return EventChildren.Refusal("positionId", "events:errors.positionUnknownForShift");
        }

        if (!saving.IsNew && (long)database.Entry(shift).Property(nameof(EventAtcShift.EventId)).OriginalValue! != position.EventId)
        {
            return EventChildren.Refusal("positionId", "events:errors.positionOfAnotherEvent");
        }

        shift.EventId = position.EventId;
        shift.Fir = position.Fir;

        var (_, refusal) = await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(shift, saving.CancellationToken);
        return refusal;
    }

    /// <summary>
    /// What a shift cannot be (reading 6): on a roster not proposed yet, on an event cancelled or over, outside its position's hours, at
    /// the same time as another shift of its position, for somebody who never signed in.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventAtcShift shift, CrudSaving saving)
    {
        var database = (EventsDbContext)saving.Database;
        if (await saving.Services.GetRequiredService<EventChildren>().EventAsync(shift.EventId, saving.CancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("positionId", "events:errors.eventUnknown");
        }

        if (Closed(parent, saving.Services.GetRequiredService<IClock>().UtcNow) is { } closed)
        {
            return EventChildren.Refusal("positionId", closed);
        }

        var position = await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
            .FirstAsync(candidate => candidate.Id == shift.PositionId, saving.CancellationToken);
        if (shift.FromUtc < position.FromUtc || shift.ToUtc > position.ToUtc)
        {
            return EventChildren.Refusal("fromUtc", "events:errors.shiftOutsidePosition");
        }

        var taken = await CrudSource.BackOffice<EventAtcShift>(database).AnyAsync(
            other => other.PositionId == shift.PositionId
                && other.Id != shift.Id
                && other.FromUtc < shift.ToUtc
                && shift.FromUtc < other.ToUtc,
            saving.CancellationToken);
        if (taken)
        {
            return EventChildren.Refusal("fromUtc", "events:errors.shiftTaken");
        }

        // The roster of the core is whoever signed in once (plan §16.13): somebody who never did is nobody the hub can name or tell.
        var known = await saving.Services.GetRequiredService<HubDbContext>().Users.AsNoTracking()
            .AnyAsync(user => user.Vid == shift.ControllerVid, saving.CancellationToken);
        return known ? null : EventChildren.Refusal("controllerVid", "events:errors.controllerNeverSignedIn");
    }

    /// <summary>A shift is taken away from the proposal to the end of the event; after it, it is the register of reliability (c3).</summary>
    private static async Task DeleteAsync(EventAtcShift shift, IServiceProvider services, CancellationToken cancellationToken)
    {
        var parent = await services.GetRequiredService<EventChildren>().EventAsync(shift.EventId, cancellationToken);
        if (parent is not null && Closed(parent, services.GetRequiredService<IClock>().UtcNow) is { } closed)
        {
            throw new DomainRefusalException("id", closed);
        }

        services.GetRequiredService<EventsDbContext>().AtcShifts.Remove(shift);
    }

    /// <summary>Why the roster of an event takes no correction now, or none: not proposed yet, cancelled, over.</summary>
    private static string? Closed(Event parent, DateTime now) =>
        parent.RosterProposedAt is null ? "events:errors.rosterNotProposed"
        : parent.CancelledAt is not null ? "events:errors.alreadyCancelled"
        : EventState.IsOver(parent, now) ? "events:errors.eventOver"
        : null;

    /// <summary>
    /// The roster of an event (§4.4, reading 7): the positions the reader may read — the one handler asked on each, with its FIR —, their
    /// shifts and their uncovered pieces, a page of them by callsign and hour. The search box: a callsign, a VID, a piece of a name.
    /// </summary>
    private static async Task<IResult> RosterAsync(
        long id,
        int? page,
        int? pageSize,
        string? q,
        EventsDbContext database,
        StaffRoster roster,
        ModuleSettingsStore settings,
        IAuthorizationService authorization,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.HasRoster, http.RequestAborted);
        if (row is null)
        {
            return Results.NotFound();
        }

        var readable = new List<EventAtcPosition>();
        foreach (var position in await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
                     .Where(position => position.EventId == id)
                     .ToListAsync(http.RequestAborted))
        {
            if ((await authorization.AuthorizeAsync(http.User, position, EventsPermissions.AtcView)).Succeeded)
            {
                readable.Add(position);
            }
        }

        var term = q?.Trim();
        var all = (await roster.RowsAsync(row, readable, await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, http.RequestAborted), http.RequestAborted))
            .Where(entry => string.IsNullOrEmpty(term)
                || entry.Callsign.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.Controller?.Vid.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal) == true
                || entry.Controller?.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        var size = Math.Clamp(pageSize ?? 100, 1, 200);
        var number = Math.Max(page ?? 1, 1);

        return Results.Ok(new PagedResult<RosterRowDto>([.. all.Skip((number - 1) * size).Take(size)], number, size, all.Count));
    }
}
