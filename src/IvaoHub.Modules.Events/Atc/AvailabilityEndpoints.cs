using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// A member's availability for the events with a roster (design M4 §4.2, §7.1, E11a), under <c>/api/events/mine/availability</c>: reading
/// their own, giving or changing it, withdrawing it. Verbs of a member's flow, written by hand as a registration's are: an availability is
/// the member's hours checked against the event's (<see cref="MemberAvailability"/>). Any signed in member, and their own alone: no
/// permission gives a member the area's <c>View</c> (design §1.1); the staff read them per position (<see cref="AtcPositionEndpoints"/>).
/// </summary>
public static class AvailabilityEndpoints
{
    /// <summary>The member's own availabilities, and the verbs on them.</summary>
    public const string MinePattern = "/api/events/mine/availability";

    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mine = app.MapGroup(MinePattern).WithTags("EventAtc").RequireAuthorization(HubPolicies.SignedIn);

        mine.MapGet("/", (MemberAvailability availability, HttpContext http) => availability.MineAsync(http.RequestAborted))
            .WithName("EventsMyAvailability")
            .Produces<IReadOnlyList<MyAvailabilityDto>>();

        mine.MapPut("/{eventId:long}", SaveAsync)
            .WithName("EventsGiveAvailability")
            .Produces<MyAvailabilityDto>()
            .Produces<MyAvailabilityDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        mine.MapDelete("/{eventId:long}", WithdrawAsync)
            .WithName("EventsWithdrawAvailability")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>Gives it, or changes it: 201 the first time, 200 after; a refusal lands on the field of its window.</summary>
    private static async Task<IResult> SaveAsync(
        long eventId,
        AvailabilityRequest request,
        MemberAvailability availability,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, given, created, problems) = await availability.SaveAsync(eventId, request, http.RequestAborted);

        // No address of its own: an availability is read in the member's list, and nowhere else.
        return result switch
        {
            AvailabilityResult.Done => created ? Results.Created((string?)null, given) : Results.Ok(given),
            AvailabilityResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            AvailabilityResult.Changed => Changed(catalog, currentUser),
            _ => Results.NotFound(),
        };
    }

    /// <summary>The key a 409 of a member's availability says: it changed from another window of theirs, read it again.</summary>
    public const string ChangedKey = "events:errors.availabilityChanged";

    /// <summary>Changed meanwhile from another window of the member's: a 409 that says to read it again (the review of #256, point 4).</summary>
    private static IResult Changed(LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: catalog.Resolve(currentUser.Locale, ChangedKey));

    private static async Task<IResult> WithdrawAsync(
        long eventId,
        MemberAvailability availability,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var (result, problems) = await availability.WithdrawAsync(eventId, http.RequestAborted);

        return result switch
        {
            AvailabilityResult.Done => Results.NoContent(),
            AvailabilityResult.Refused => CrudProblems.Validation(problems, catalog, currentUser.Locale),
            AvailabilityResult.Changed => Changed(catalog, currentUser),
            _ => Results.NotFound(),
        };
    }
}
