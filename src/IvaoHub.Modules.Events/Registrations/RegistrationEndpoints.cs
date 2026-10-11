using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// The registrations to the events in person (design M4 §4-bis.2, §7.1, §7.2, E16): the member's own, under
/// <c>/api/events/mine/registrations</c> — reading them, registering or changing the answers, withdrawing —, and the staff's list of an
/// event's registrations with «take away», with a reason the member reads in a mail. Verbs of a member's flow, written by hand because
/// a registration is the member's answers to the rows of the staff, checked by their type (<see cref="MemberRegistrations"/>), as a
/// booking is a choice among slots.
/// <para>Any signed in member registers and withdraws, and reads their own registrations and nobody else's: no permission gives a member
/// the area's <c>View</c> (design §1.1). Taking a registration away is the staff's, with <c>EventBookings.Edit</c> asked of the one
/// handler on the registration — on its event's departments and scope as they are now.</para>
/// </summary>
public static class RegistrationEndpoints
{
    /// <summary>The member's own registrations, and the verbs on them.</summary>
    public const string MinePattern = "/api/events/mine/registrations";

    /// <summary>The registrations as the staff of an event reach them.</summary>
    public const string StaffPattern = "/api/events/registrations";

    public static IEndpointRouteBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The staff's list: every registration of an event, with its member and their answers; filter[eventId] is the event, ?q= a VID.
        // Read only: a registration changes through the member's verbs and «take away».
        var noNames = new Dictionary<int, string>();
        app.MapCrud<EventRegistration, EventRegistrationDto, EventRegistrationDto, EventRegistrationDto>(StaffPattern, options =>
        {
            options.PermissionArea = EventsPermissions.BookingsArea;
            options.Name = "EventRegistrations";
            options.ReadPolicy = EventsPermissions.BookingsView;
            options.WritePolicy = EventsPermissions.BookingsEdit;
            options.ReadOnly = true;
            options.ContextType = typeof(EventsDbContext);

            // As they came: the first registered first.
            options.DefaultOrder = registration => registration.Id;
            options.Sortable.Add(nameof(EventRegistration.CreatedAt));
            options.Sortable.Add(nameof(EventRegistration.UpdatedAt));
            options.Filterable.Add(nameof(EventRegistration.EventId));
            options.SearchFields.Add(registration => registration.Vid.ToString());

            options.ToList = registration => StaffRegistrations.Row(registration, noNames, []);
            options.ToListPage = (registrations, services, cancellationToken) =>
                services.GetRequiredService<StaffRegistrations>().RowsAsync(registrations, cancellationToken);
            options.ToDetail = registration => StaffRegistrations.Row(registration, noNames, []);
        });

        var mine = app.MapGroup(MinePattern).WithTags("EventRegistrations").RequireAuthorization(HubPolicies.SignedIn);

        mine.MapGet("/", (MemberRegistrations registrations, HttpContext http) => registrations.MineAsync(http.RequestAborted))
            .WithName("EventsMyRegistrations")
            .Produces<IReadOnlyList<MyRegistrationDto>>();

        mine.MapPut("/{eventId:long}", SaveAsync)
            .WithName("EventsRegister")
            .Produces<MyRegistrationDto>()
            .Produces<MyRegistrationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        mine.MapDelete("/{eventId:long}", WithdrawAsync)
            .WithName("EventsWithdrawRegistration")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost($"{StaffPattern}/{{id:long}}/remove", RemoveAsync)
            .WithName("EventsRegistrationRemove")
            .WithTags("EventRegistrations")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.BookingsEdit);

        return app;
    }

    /// <summary>Registers, or changes the answers: 201 the first time, 200 after; a refusal lands on the field of its answer.</summary>
    private static async Task<IResult> SaveAsync(
        long eventId,
        RegistrationRequest request,
        MemberRegistrations registrations,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, registration, created, problems) = await registrations.SaveAsync(eventId, request, http.RequestAborted);

        // No address of its own: a registration is read in the member's list, and nowhere else.
        return result switch
        {
            BookingResult.Done => created ? Results.Created((string?)null, registration) : Results.Ok(registration),
            BookingResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> WithdrawAsync(
        long eventId,
        MemberRegistrations registrations,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, problems) = await registrations.WithdrawAsync(eventId, http.RequestAborted);

        return result switch
        {
            BookingResult.Done => Results.NoContent(),
            BookingResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            _ => Results.NotFound(),
        };
    }

    /// <summary>
    /// The staff take a registration away (§4-bis.2): any registration of an event whose bookings they write, with a reason — the audit
    /// keeps who took what away, and the member is told why (<see cref="EventsNotifications.RegistrationRemoved"/>), once the
    /// registration is gone. The permission is asked on the registration in its event's care as it is now, as for a booking.
    /// </summary>
    private static async Task<IResult> RemoveAsync(
        long id,
        BookingRemovalRequest request,
        EventsDbContext database,
        EventChildren children,
        EventsMail mail,
        IValidator<BookingRemovalRequest> validator,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var registration = await CrudSource.BackOffice<EventRegistration>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, http.RequestAborted);
        if (registration is null || (await children.AdoptAsync(registration, http.RequestAborted)).Event is not { } row)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, registration, EventsPermissions.BookingsEdit)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser);
        }

        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        database.Registrations.Remove(registration);

        try
        {
            await database.SaveChangesAsync(http.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Withdrawn by its member, or taken away by somebody else, a moment before.
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        await mail.RegistrationRemovedAsync(row, registration.Vid, request.Reason!.Trim(), http.RequestAborted);

        return Results.NoContent();
    }

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
