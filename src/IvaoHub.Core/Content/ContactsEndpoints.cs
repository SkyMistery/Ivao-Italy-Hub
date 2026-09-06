using System.Globalization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        return group;
    }

    private static async Task<Results<Ok<ContactSubmittedDto>, ValidationProblem>> SubmitAsync(
        HttpContext http,
        ContactSubmitDto body,
        HubDbContext database,
        INotificationService notifications,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        IOptions<DivisionOptions> division,
        FluentValidation.IValidator<ContactSubmitDto> validator)
    {
        ArgumentNullException.ThrowIfNull(body);

        var validation = await validator.ValidateAsync(body, http.RequestAborted);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(
                validation.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(failure => failure.ErrorMessage).ToArray(),
                        StringComparer.Ordinal),
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
        }

        var message = new ContactMessage
        {
            OwnerDepartment = body.Department,
            Subject = body.Subject.Trim(),
            Body = body.Body.Trim(),
            Status = ContactStatus.New,
        };

        // The sender is the session: CreatedBy is stamped by the interceptor, and the write guard
        // lets this one row through because ContactMessage declares that it accepts submissions.
        database.ContactMessages.Add(message);
        await database.SaveChangesAsync(http.RequestAborted);

        await notifications.QueueAsync(
            await IntentFor(message, database, division.Value, http.RequestAborted),
            http.RequestAborted);

        return TypedResults.Ok(new ContactSubmittedDto(message.Id));
    }

    /// <summary>
    /// Who hears about a message: the shared inbox of the department, if the division has one, and
    /// every member of its staff. Whether each of them actually wants it is the notification
    /// service's question, not this one's — here it is only "who is this about".
    /// </summary>
    private static async Task<NotificationIntent> IntentFor(
        ContactMessage message,
        HubDbContext database,
        DivisionOptions division,
        CancellationToken cancellationToken)
    {
        var recipients = new List<NotificationRecipient>();

        if (division.DepartmentMailboxes.TryGetValue(message.OwnerDepartment.ToString(), out var mailbox)
            && !string.IsNullOrWhiteSpace(mailbox))
        {
            recipients.Add(NotificationRecipient.Mailbox(mailbox));
        }

        var staff = await database.UserStaffPositions
            .AsNoTracking()
            .Where(position => position.Department == message.OwnerDepartment)
            .Select(position => position.Vid)
            .Distinct()
            .ToListAsync(cancellationToken);

        recipients.AddRange(staff.Select(NotificationRecipient.Member));

        var department = message.OwnerDepartment.ToString();

        return new NotificationIntent(
            NotificationTypes.ContactReceived,
            recipients,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["department"] = department,
                ["subject"] = message.Subject,
                ["body"] = message.Body,
                ["vid"] = message.CreatedBy.ToString(CultureInfo.InvariantCulture),
                // Where to go and read it. The department is spelled the way the address bar
                // spells it, which is the same rule the single page application follows.
                ["url"] = $"https://{division.Domain}/staff/{department.ToLowerInvariant()}/contacts",
            });
    }
}

/// <summary>
/// What the sender gets back: the identifier of their message and nothing else. They cannot read
/// it again — the queue belongs to the department — so there is nothing more to hand over.
/// </summary>
public sealed record ContactSubmittedDto(long Id);
