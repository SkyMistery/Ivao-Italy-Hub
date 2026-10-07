using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Bookings;

/// <summary>
/// The bookings (design M4 §3.3, §3.6, §7.1, E6a): the pilot's own, under <c>/api/events/mine/bookings</c> — reading them, booking a
/// public slot, booking the whole rotation of one, withdrawing one —, and the staff's «take away» of any booking of an event, with a
/// reason the pilot reads in a mail. Verbs the design names (§7.2: «prenotare, ritirare»), written by hand because a booking is not a
/// form over a row: it is a choice among slots, checked against the pilot's other bookings under a lock (<see cref="PilotBookings"/>).
/// <para>Any signed in member books and withdraws, and reads their own bookings and nobody else's: a pilot is not a role, and no
/// permission gives a member the area's <c>View</c> (design §1.1, no <c>IHasParticipants</c>). Taking a booking away is the staff's,
/// with <c>EventBookings.Edit</c> asked of the one handler on the booking — on its event's departments and scope.</para>
/// </summary>
public static class BookingEndpoints
{
    /// <summary>The pilot's own bookings, and the verbs on them.</summary>
    public const string MinePattern = "/api/events/mine/bookings";

    /// <summary>The bookings as the staff of an event reach them.</summary>
    public const string StaffPattern = "/api/events/bookings";

    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mine = app.MapGroup(MinePattern).WithTags("EventBookings").RequireAuthorization(HubPolicies.SignedIn);

        mine.MapGet("/", (PilotBookings bookings, HttpContext http) => bookings.MineAsync(http.RequestAborted))
            .WithName("EventsMyBookings")
            .Produces<IReadOnlyList<MyBookingDto>>();

        mine.MapPost("/", BookAsync)
            .WithName("EventsBook")
            .Produces<MyBookingDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        mine.MapPost("/rotation", BookRotationAsync)
            .WithName("EventsBookRotation")
            .Produces<RotationBookingDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        mine.MapDelete("/{id:long}", WithdrawAsync)
            .WithName("EventsWithdraw")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        app.MapPost($"{StaffPattern}/{{id:long}}/remove", RemoveAsync)
            .WithName("EventsBookingRemove")
            .WithTags("EventBookings")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.BookingsEdit);

        return app;
    }

    private static async Task<IResult> BookAsync(
        BookingRequest request,
        PilotBookings bookings,
        IValidator<BookingRequest> validator,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        var (result, booking, problems) = await bookings.BookAsync(request, http.RequestAborted);

        return result switch
        {
            BookingResult.Done => Results.Created($"{MinePattern}/{booking!.Id}", booking),
            BookingResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> BookRotationAsync(
        BookingRequest request,
        PilotBookings bookings,
        IValidator<BookingRequest> validator,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        try
        {
            var (result, rotation, problems) = await bookings.BookRotationAsync(request, http.RequestAborted);

            return result switch
            {
                BookingResult.Done => Results.Ok(rotation),
                BookingResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
                _ => Results.NotFound(),
            };
        }
        catch (Exception exception) when (DatabaseErrors.Deadlocked(exception))
        {
            // The database rolled the whole transaction back, the legs booked before it included: nothing was booked, try again.
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));
        }
    }

    private static async Task<IResult> WithdrawAsync(
        long id,
        PilotBookings bookings,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, problems) = await bookings.WithdrawAsync(id, http.RequestAborted);

        return result switch
        {
            BookingResult.Done => Results.NoContent(),
            BookingResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            _ => Results.NotFound(),
        };
    }

    /// <summary>
    /// The staff take a booking away (§3.6): any booking of an event whose bookings they write, with a reason — the slot is free
    /// again, the audit keeps who took what away, and the pilot is told why (<see cref="EventsNotifications.BookingRemoved"/>),
    /// once the booking is gone.
    /// </summary>
    private static async Task<IResult> RemoveAsync(
        long id,
        BookingRemovalRequest request,
        EventsDbContext database,
        EventsMail mail,
        IValidator<BookingRemovalRequest> validator,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var booking = await CrudSource.BackOffice<EventBooking>(database).FirstOrDefaultAsync(row => row.Id == id, http.RequestAborted);
        if (booking is null)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, booking, EventsPermissions.BookingsEdit)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser);
        }

        var validation = await validator.ValidateAsync(request, http.RequestAborted);
        if (!validation.IsValid)
        {
            return CrudProblems.Validation(validation, catalog, currentUser.Locale);
        }

        var slot = await database.Slots.AsNoTracking().FirstAsync(row => row.Id == booking.SlotId, http.RequestAborted);
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstAsync(candidate => candidate.Id == booking.EventId, http.RequestAborted);

        database.Bookings.Remove(booking);

        try
        {
            await database.SaveChangesAsync(http.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Withdrawn by its pilot, or taken away by somebody else, a moment before.
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        await mail.BookingRemovedAsync(row, slot, booking, request.Reason!.Trim(), http.RequestAborted);

        return Results.NoContent();
    }

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));
}
