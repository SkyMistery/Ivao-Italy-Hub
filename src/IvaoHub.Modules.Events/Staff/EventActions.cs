using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Modules.Events.Atc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// What the reader may do on an event (issue #427, note 2026-11-01-le-azioni-di-un-evento-dal-server): the buttons of its page and which of
/// its tabs it shows and writes, each the one handler's answer on the event — a grant on one event reaches that event and no other, a
/// permission held from outside or on a FIR as the handler says — and never a rule of the page. «Publish» and «Cancel» say too what their
/// verbs refuse by the state: a draft not cancelled is published once, an event not cancelled and not over is cancelled.
/// </summary>
/// <param name="CanEdit"><c>Events.Edit</c> on the event: its form, its description, its airports, its rules of award, its questions.</param>
/// <param name="CanDelete"><c>Events.Delete</c> on the event; whether nobody took part yet is the deletion's to answer.</param>
/// <param name="CanPublish"><c>Events.Edit</c> on a draft not cancelled; what it still needs is the publication's to list.</param>
/// <param name="CanCancel"><c>Events.Edit</c> on an event not cancelled and not over.</param>
/// <param name="CanDuplicate">
/// <c>Events.Edit</c> on the new event as it would be stored — the duplicate's first question —, so a grant on one event alone never
/// duplicates it.
/// </param>
/// <param name="CanReadRoutes"><c>EventRoutes.View</c> on the event: the tab of its routes.</param>
/// <param name="CanWriteRoutes"><c>EventRoutes.Edit</c> on the event: its buttons.</param>
/// <param name="CanReadBookings"><c>EventBookings.View</c> on the event: the tabs of its slots, bookings, registrations and activities.</param>
/// <param name="CanWriteBookings"><c>EventBookings.Edit</c> on the event: their buttons.</param>
/// <param name="CanReadAtc">
/// Whether the reader reaches the event's ATC, as its head answers (<see cref="AtcPositionEndpoints"/>): <c>EventAtc.View</c> held without a
/// scope, the staff of a FIR among them, or on this event.
/// </param>
/// <param name="CanReadReports"><c>EventReports.View</c> on the event: the tab of its reports of support, each decided where it says.</param>
public sealed record EventActionsDto(
    bool CanEdit,
    bool CanDelete,
    bool CanPublish,
    bool CanCancel,
    bool CanDuplicate,
    bool CanReadRoutes,
    bool CanWriteRoutes,
    bool CanReadBookings,
    bool CanWriteBookings,
    bool CanReadAtc,
    bool CanReadReports)
{
    /// <summary>Nothing: what a detail says with no reader in hand.</summary>
    public static readonly EventActionsDto None = new(false, false, false, false, false, false, false, false, false, false, false);
}

/// <summary>
/// An event's detail with the reader's actions on it (#427): what the page of an event loads — through the engine's <c>ToDetailPage</c> —
/// and what «Publish», «Cancel» and «Duplicate» answer with, so the buttons drawn after a press are the reader's too.
/// </summary>
public sealed class EventDetails(IAuthorizationService authorization, ICurrentUser currentUser, IHttpContextAccessor http)
{
    public async Task<EventDetailDto> DetailAsync(Event row, DateTime now) =>
        EventMapper.ToDetail(row, now, await ActionsAsync(row, now));

    /// <summary>Each action asked of the one handler on the event, and on the new event for «Duplicate».</summary>
    public async Task<EventActionsDto> ActionsAsync(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (http.HttpContext?.User is not { } reader)
        {
            return EventActionsDto.None;
        }

        async Task<bool> MayAsync(object resource, string permission) =>
            (await authorization.AuthorizeAsync(reader, resource, permission)).Succeeded;

        var edit = await MayAsync(row, EventsPermissions.Edit);
        var unborn = new Event { OwnerDepartment = row.OwnerDepartment, OwnerDepartmentMask = row.OwnerDepartmentMask };

        return new EventActionsDto(
            CanEdit: edit,
            CanDelete: await MayAsync(row, EventsPermissions.Delete),
            CanPublish: edit && row.CancelledAt is null && row.Status != PublishStatus.Published,
            CanCancel: edit && row.CancelledAt is null && now < row.EndsAtUtc,
            CanDuplicate: await MayAsync(unborn, EventsPermissions.Edit),
            CanReadRoutes: await MayAsync(row, EventsPermissions.RoutesView),
            CanWriteRoutes: await MayAsync(row, EventsPermissions.RoutesEdit),
            CanReadBookings: await MayAsync(row, EventsPermissions.BookingsView),
            CanWriteBookings: await MayAsync(row, EventsPermissions.BookingsEdit),
            CanReadAtc: await AtcPositionEndpoints.ReachesAsync(row, authorization, currentUser, reader),
            CanReadReports: await MayAsync(row, EventsPermissions.ReportsView));
    }
}
