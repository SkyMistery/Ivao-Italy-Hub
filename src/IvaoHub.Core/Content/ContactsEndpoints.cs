using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// The contact queue of a department, and the form a member writes it with (design M1 section 5.1).
/// <para>The back office half is the generic CRUD engine, with two things said in configuration
/// rather than in code: there is no JSON create — a message is born from the form below, never
/// from the queue — and the write payload carries the status alone, so no permission and no screen
/// can rewrite what somebody sent.</para>
/// <para>The submission is written by hand, and it is the second such endpoint of M1 after the
/// upload of G1. The shape is the same one G1 settled on: <c>MapCreate = false</c> plus a
/// <c>POST</c> on the group, rather than a second address for creating the same thing. It is
/// written out because none of the three things it does is a CRUD create — the policy is
/// <c>SignedIn</c> and not the write permission of the area, the sender is the session, and a
/// notification goes out after the row is saved.</para>
/// </summary>
public static class ContactsEndpoints
{
    /// <summary>Where the resource lives; the generated client picks the path up from OpenAPI.</summary>
    public const string Pattern = "/api/contacts";

    public static RouteGroupBuilder MapContactsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new ContactMapper();

        var group = app.MapCrud<ContactMessage, ContactListDto, ContactDetailDto, ContactStatusWriteDto>(
            Pattern,
            options =>
            {
                // Contacts.View to read the queue, Contacts.Edit to move a message along, both
                // scoped to the department the message was sent to.
                options.PermissionArea = CorePermissions.ContactsArea;

                // Newest first: a queue is read from the top.
                options.DefaultOrder = message => message.CreatedAt;

                options.Sortable.Add(nameof(ContactMessage.Subject));
                options.Sortable.Add(nameof(ContactMessage.Status));
                options.Sortable.Add(nameof(ContactMessage.CreatedAt));
                options.Sortable.Add(nameof(ContactMessage.UpdatedAt));

                options.Filterable.Add(nameof(ContactMessage.OwnerDepartment));
                options.Filterable.Add(nameof(ContactMessage.Status));

                options.SearchFields.Add(message => message.Subject);
                options.SearchFields.Add(message => message.Body);

                // A message is sent, not created here; and it is never deleted, because a queue
                // with a delete button is a queue whose history depends on who was embarrassed.
                options.MapCreate = false;
                options.AllowDelete = false;

                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
            });

        group.MapPost("/", SubmitAsync)
            .WithName("ContactsSubmit")
            .Produces<ContactSubmittedDto>()
            .ProducesValidationProblem()
            // Signed in, and that is the whole anti spam design: there is no address to verify and
            // no captcha, because there is no anonymous sender (plan section 9.1).
            .RequireAuthorization(HubPolicies.SignedIn);

        // The thread, for the back office and for /me/contacts alike (M2, T14). Signed in at the door; the single
        // handler decides on the message itself, with Contacts.View: the department, the sender, the participants.
        group.MapGet("/{id:long}/thread", ReadThreadAsync)
            .WithName("ContactsThread")
            .Produces<ContactThreadDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(HubPolicies.SignedIn);

        group.MapPost("/{id:long}/replies", ReplyAsync)
            .WithName("ContactsReply")
            .Produces<ContactThreadDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(HubPolicies.SignedIn);

        // The member's own threads, whichever department they went to: the ones they sent and the ones they were added
        // to. The same engine as the queue, as a personal view (CrudOptions.Participating).
        app.MapCrud<ContactMessage, ContactListDto, ContactListDto, ContactStatusWriteDto>(
            MinePattern,
            options =>
            {
                options.PermissionArea = CorePermissions.ContactsArea;
                options.Name = "MyContacts";
                options.Participating = ContactMessage.TakesPart;
                options.ReadOnly = true;
                options.DefaultOrder = message => message.UpdatedAt;
                options.Sortable.Add(nameof(ContactMessage.Subject));
                options.Sortable.Add(nameof(ContactMessage.Status));
                options.Sortable.Add(nameof(ContactMessage.CreatedAt));
                options.Sortable.Add(nameof(ContactMessage.UpdatedAt));
                options.Filterable.Add(nameof(ContactMessage.Status));
                options.SearchFields.Add(message => message.Subject);
                options.ToList = mapper.ToList;
                options.ToDetail = mapper.ToList;
            });

        return group;
    }

    /// <summary>Where the member's own threads live.</summary>
    public const string MinePattern = "/api/me/contacts";

    private static async Task<Results<Ok<ContactSubmittedDto>, ValidationProblem>> SubmitAsync(
        HttpContext http,
        ContactSubmitDto body,
        ContactThreads threads,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        FluentValidation.IValidator<ContactSubmitDto> validator)
    {
        ArgumentNullException.ThrowIfNull(body);

        var validation = await validator.ValidateAsync(body, http.RequestAborted);
        if (!validation.IsValid)
        {
            return Refused(
                validation.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                        StringComparer.Ordinal),
                catalog,
                currentUser);
        }

        var (message, errors) = await threads.SubmitAsync(body, http.RequestAborted);
        if (message is null)
        {
            return Refused(errors!, catalog, currentUser);
        }

        return TypedResults.Ok(new ContactSubmittedDto(message.Id));
    }

    private static async Task<Results<Ok<ContactThreadDto>, NotFound>> ReadThreadAsync(
        long id,
        HttpContext http,
        ContactThreads threads)
    {
        // Not readable and not there are the same answer: a thread of somebody else is not confirmed to exist.
        var thread = await threads.ReadAsync(http.User, id, http.RequestAborted);
        return thread is null ? TypedResults.NotFound() : TypedResults.Ok(thread);
    }

    private static async Task<Results<Ok<ContactThreadDto>, ValidationProblem, NotFound, Conflict>> ReplyAsync(
        long id,
        HttpContext http,
        ContactReplyWriteDto body,
        ContactThreads threads,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        FluentValidation.IValidator<ContactReplyWriteDto> validator)
    {
        ArgumentNullException.ThrowIfNull(body);

        var validation = await validator.ValidateAsync(body, http.RequestAborted);
        if (!validation.IsValid)
        {
            return Refused(
                validation.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray(),
                        StringComparer.Ordinal),
                catalog,
                currentUser);
        }

        try
        {
            var thread = await threads.ReplyAsync(http.User, id, body.Body, http.RequestAborted);
            return thread is null ? TypedResults.NotFound() : TypedResults.Ok(thread);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody moved the message in the same instant: the answer was not written, and saying so beats
            // writing it against a status nobody saw.
            return TypedResults.Conflict();
        }
    }

    private static ValidationProblem Refused(
        IReadOnlyDictionary<string, string[]> errors,
        LocaleCatalog catalog,
        ICurrentUser currentUser) =>
        TypedResults.ValidationProblem(
            errors,
            title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
}

/// <summary>
/// What the sender gets back: the identifier of their message, which is also the address of the thread in
/// <c>/me/contacts</c> (M2, T14).
/// </summary>
public sealed record ContactSubmittedDto(long Id);
