using System.Globalization;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>
/// What a module says about one of its objects a thread cites (note 2026-09-15-contatti-con-risposte §3.1): how it
/// reads, where it lives for the person asking, and who else should take part — the validator of a report. The core
/// never parses a <c>pirep:123</c>; the module that registered <see cref="SourceModule"/> does.
/// <para>Scoped, like a data block provider, because it answers <b>as the caller</b>: <c>null</c> means the object does
/// not exist or the caller may not see it, and the two are the same answer on purpose.</para>
/// </summary>
public interface IContactReferenceResolver
{
    /// <summary>The module whose references this resolves: its key.</summary>
    string SourceModule { get; }

    Task<ContactReferenceTarget?> ResolveAsync(string sourceId, CancellationToken cancellationToken);
}

/// <param name="Label">How the object reads, taken into the thread when it opens.</param>
/// <param name="Url">Where the caller finds it: a pilot and a validator are sent to different pages.</param>
/// <param name="Participants">Who else takes part in a thread about it; the sender is left out by whoever opens it.</param>
public sealed record ContactReferenceTarget(Localized<string> Label, string Url, IReadOnlyList<int> Participants);

/// <summary>Every resolver of the installation, one per module, composed like the data block providers.</summary>
public sealed class ContactReferenceResolvers
{
    private readonly Dictionary<string, IContactReferenceResolver> _byModule = new(StringComparer.Ordinal);

    public ContactReferenceResolvers(IEnumerable<IContactReferenceResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);

        foreach (var resolver in resolvers)
        {
            if (!_byModule.TryAdd(resolver.SourceModule, resolver))
            {
                throw new InvalidOperationException(
                    $"Two contact reference resolvers answer for the module '{resolver.SourceModule}'.");
            }
        }
    }

    public IContactReferenceResolver? Find(string? module) =>
        module is not null && _byModule.TryGetValue(module, out var resolver) ? resolver : null;
}

/// <summary>
/// The threads of the contacts (M2, T14, notes 2026-09-15-contatti-con-risposte and 2026-09-23-i-fili-dei-contatti):
/// opening one from the form, reading one, answering one, and the mail each of those sends. One service behind the
/// back office and <c>/me/contacts</c>, so the conversation is written once.
/// <para>Who may read and answer is the single handler's, asked with <c>Contacts.View</c> on the message: the
/// department, the sender and whoever was added (<see cref="IHasParticipants"/>). Who is <b>shown</b> is this class's:
/// the sender never reads who of the department answered (design M2 section 3.5).</para>
/// </summary>
public sealed class ContactThreads(
    HubDbContext database,
    ContactReferenceResolvers resolvers,
    IAuthorizationService authorization,
    INotificationService notifications,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<DivisionOptions> division)
{
    /// <summary>How many objects one message may cite. A clarification asks about a few, not a tour.</summary>
    public const int MaxReferences = 10;

    /// <summary>
    /// A member opens a thread from the form. Returns the refusals by field, or the new message.
    /// </summary>
    public async Task<(ContactMessage? Message, IReadOnlyDictionary<string, string[]>? Errors)> SubmitAsync(
        ContactSubmitDto body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var targets = new List<(ContactReferenceInput Input, ContactReferenceTarget Target)>();
        foreach (var input in (body.References ?? []).DistinctBy(reference => (reference.SourceModule, reference.SourceId)))
        {
            // A reference the module does not recognise, or one the sender may not see, is the same refusal: telling
            // the two apart would say that a report nobody showed them exists.
            var target = resolvers.Find(input.SourceModule) is { } resolver
                ? await resolver.ResolveAsync(input.SourceId, cancellationToken)
                : null;

            if (target is null)
            {
                return (null, Refusal("references", "errors.contacts.referenceUnknown"));
            }

            targets.Add((input, target));
        }

        var message = new ContactMessage
        {
            OwnerDepartment = body.Department,
            Subject = body.Subject.Trim(),
            Body = body.Body.Trim(),
            Status = ContactStatus.New,
            Kind = body.Kind ?? ContactKinds.General,
            ParticipantsJson = ContactParticipants.Write(
                targets.SelectMany(target => target.Target.Participants),
                except: currentUser.Vid),
        };

        // The sender is the session: CreatedBy is stamped by the interceptor, and the write guard lets this one row
        // through because ContactMessage declares that it accepts submissions.
        database.ContactMessages.Add(message);
        foreach (var (input, target) in targets)
        {
            database.ContactReferences.Add(new ContactReference
            {
                Message = message,
                SourceModule = input.SourceModule,
                SourceId = input.SourceId,
                Label = target.Label,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        await NotifyOpenedAsync(message.Id, cancellationToken);

        return (message, null);
    }

    /// <summary>
    /// Tells the department that a thread arrived, and whoever was added that they take part. Called after the save
    /// that opened it: by the form above, and by a module after the save that projected a
    /// <see cref="ThreadOpeningProjection"/> (T14b).
    /// </summary>
    public async Task NotifyOpenedAsync(long messageId, CancellationToken cancellationToken)
    {
        var message = await database.ContactMessages.AsNoTracking().FirstAsync(row => row.Id == messageId, cancellationToken);

        await notifications.QueueAsync(
            new NotificationIntent(
                NotificationTypes.ContactReceived,
                await DepartmentAudienceAsync(message, except: [], cancellationToken),
                Data(message, message.Body, StaffUrl(message), message.CreatedBy)),
            cancellationToken);

        if (message.AddedParticipants.Count > 0)
        {
            await notifications.QueueAsync(
                new NotificationIntent(
                    NotificationTypes.ContactThreadOpened,
                    [.. message.AddedParticipants.Select(NotificationRecipient.Member)],
                    Data(message, message.Body, MemberUrl(message), message.CreatedBy)),
                cancellationToken);
        }
    }

    /// <summary>The thread as this reader may see it, or null when they may not see it at all.</summary>
    public async Task<ContactThreadDto?> ReadAsync(ClaimsPrincipal principal, long id, CancellationToken cancellationToken)
    {
        var message = await database.ContactMessages.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (message is null || !await MayReadAsync(principal, message))
        {
            return null;
        }

        var replies = await database.ContactReplies
            .AsNoTracking()
            .Where(reply => reply.MessageId == id)
            .OrderBy(reply => reply.CreatedAt)
            .ThenBy(reply => reply.Id)
            .ToListAsync(cancellationToken);

        var references = await database.ContactReferences
            .AsNoTracking()
            .Where(reference => reference.MessageId == id)
            .OrderBy(reference => reference.Id)
            .ToListAsync(cancellationToken);

        // The member who wrote reads the department as the author of every answer that is not theirs: no VID, no name.
        var readerIsSender = message.CreatedBy == currentUser.Vid;
        var authors = readerIsSender ? [] : await NamesAsync(replies.Select(reply => reply.AuthorVid), cancellationToken);

        var locale = currentUser.Locale;
        var fallback = division.Value.DefaultLocale;
        var cited = new List<ContactReferenceDto>();
        foreach (var reference in references)
        {
            // The label is the one taken when the thread opened; the link is asked now, for this reader, and is missing
            // when the object is gone or is not theirs to open.
            var target = resolvers.Find(reference.SourceModule) is { } resolver
                ? await resolver.ResolveAsync(reference.SourceId, cancellationToken)
                : null;

            cited.Add(new ContactReferenceDto(
                reference.SourceModule,
                reference.SourceId,
                reference.Label.Resolve(locale, fallback) ?? reference.SourceId,
                target?.Url));
        }

        return new ContactThreadDto(
            message.Id,
            message.OwnerDepartment,
            message.Kind,
            message.Subject,
            message.Body,
            message.Status,
            message.CreatedBy,
            message.CreatedAt,
            readerIsSender,
            readerIsSender ? [] : message.AddedParticipants,
            cited,
            [
                .. replies.Select(reply =>
                {
                    var hidden = readerIsSender && reply.Side != ContactReplySide.Sender;
                    return new ContactReplyDto(
                        reply.Id,
                        reply.Side,
                        hidden ? null : reply.AuthorVid,
                        hidden ? null : authors.GetValueOrDefault(reply.AuthorVid),
                        reply.Body,
                        reply.CreatedAt);
                }),
            ]);
    }

    /// <summary>
    /// Answers a thread, moves its status and tells the other side. Null when the reader may not see the thread;
    /// otherwise the thread as it now reads.
    /// </summary>
    public async Task<ContactThreadDto?> ReplyAsync(
        ClaimsPrincipal principal,
        long id,
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        var message = await database.ContactMessages.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (message is null || !await MayReadAsync(principal, message))
        {
            return null;
        }

        var side = message.CreatedBy == currentUser.Vid
            ? ContactReplySide.Sender
            : message.AddedParticipants.Contains(currentUser.Vid) ? ContactReplySide.Participant : ContactReplySide.Department;

        var reply = new ContactReply
        {
            MessageId = message.Id,
            AuthorVid = currentUser.Vid,
            Side = side,
            Body = text.Trim(),
            CreatedAt = clock.UtcNow,
        };

        database.ContactReplies.Add(reply);

        // The status moves by itself (note §3.1): the sender writing again puts it back on top of the queue, even from
        // Closed; an answer from the department's side marks it answered.
        message.Status = side == ContactReplySide.Sender ? ContactStatus.New : ContactStatus.Answered;

        await database.SaveChangesAsync(cancellationToken);
        await NotifyRepliedAsync(message, reply, cancellationToken);

        return await ReadAsync(principal, id, cancellationToken);
    }

    private async Task NotifyRepliedAsync(ContactMessage message, ContactReply reply, CancellationToken cancellationToken)
    {
        var participants = message.AddedParticipants.Where(vid => vid != reply.AuthorVid).ToList();

        if (reply.Side == ContactReplySide.Sender)
        {
            // The member wrote again: the people the first message reached, and whoever takes part, each where they
            // read it — the department in its queue, a participant in their own threads.
            await notifications.QueueAsync(
                new NotificationIntent(
                    NotificationTypes.ContactThreadReplied,
                    // Not whoever wrote it, and not a participant twice: they hear it below, with their own link.
                    await DepartmentAudienceAsync(message, except: [reply.AuthorVid, .. participants], cancellationToken),
                    Data(message, reply.Body, StaffUrl(message), message.CreatedBy)),
                cancellationToken);
        }
        else
        {
            // Somebody of the department's side answered: the sender, and the other participants (Carmine, 23 September
            // 2026). The shared mailbox does not hear it; it sees the status in the queue.
            participants.Insert(0, message.CreatedBy);
        }

        if (participants.Count > 0)
        {
            await notifications.QueueAsync(
                new NotificationIntent(
                    NotificationTypes.ContactThreadReplied,
                    [.. participants.Where(vid => vid != reply.AuthorVid).Select(NotificationRecipient.Member)],
                    Data(message, reply.Body, MemberUrl(message), message.CreatedBy)),
                cancellationToken);
        }
    }

    private async Task<bool> MayReadAsync(ClaimsPrincipal principal, ContactMessage message) =>
        (await authorization.AuthorizeAsync(principal, message, CorePermissions.ContactsView)).Succeeded;

    /// <summary>
    /// Who hears about a thread on the department's side: the shared inbox of the department, if the division has one,
    /// and every member of its staff. Whether each of them actually wants it is the notification service's question.
    /// </summary>
    private async Task<List<NotificationRecipient>> DepartmentAudienceAsync(
        ContactMessage message,
        IReadOnlyCollection<int> except,
        CancellationToken cancellationToken)
    {
        var recipients = new List<NotificationRecipient>();

        if (division.Value.DepartmentMailboxes.TryGetValue(message.OwnerDepartment.ToString(), out var mailbox)
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

        recipients.AddRange(staff.Where(vid => !except.Contains(vid)).Select(NotificationRecipient.Member));

        return recipients;
    }

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> vids, CancellationToken cancellationToken)
    {
        var wanted = vids.Distinct().ToArray();
        return await database.Users
            .AsNoTracking()
            .Where(user => wanted.Contains(user.Vid))
            .ToDictionaryAsync(user => user.Vid, user => $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);
    }

    private Dictionary<string, string> Data(ContactMessage message, string body, string url, int sender) =>
        new(StringComparer.Ordinal)
        {
            ["department"] = message.OwnerDepartment.ToString(),
            ["subject"] = message.Subject,
            ["body"] = body,
            ["vid"] = sender.ToString(CultureInfo.InvariantCulture),
            ["url"] = url,
        };

    /// <summary>Where the department reads it. The department is spelled the way the address bar spells it.</summary>
    private string StaffUrl(ContactMessage message) =>
        $"https://{division.Value.Domain}/staff/{message.OwnerDepartment.ToString().ToLowerInvariant()}/contacts/{message.Id}";

    /// <summary>Where the sender and the participants read it: their own threads.</summary>
    private string MemberUrl(ContactMessage message) => $"https://{division.Value.Domain}/me/contacts/{message.Id}";

    private static Dictionary<string, string[]> Refusal(string field, string key) =>
        new(StringComparer.Ordinal) { [field] = [key] };
}
