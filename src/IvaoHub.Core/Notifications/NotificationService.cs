using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Notifications;

/// <summary>
/// The one notification service of the hub. Modules publish intents; nobody else ever talks to a
/// mail server (plan section 9.7).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Puts one intent in the queue, as one row per recipient who is actually going to hear about
    /// it. Returns how many rows were written, which is how a caller — or a test — finds out that
    /// everybody had switched it off.
    /// </summary>
    Task<int> QueueAsync(NotificationIntent intent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns an intent into rows of <c>hub_notifications</c>: it resolves each recipient into an
/// address and a language, drops whoever does not want it, and writes what is left. Sending is the
/// job's business, and the job does nothing but send (design M1 section 5.2).
/// <para>Three ways a recipient disappears here rather than later, all of them silent on purpose:
/// a member who has switched the type off, a member the hub has no address for — somebody who has
/// not signed in since the address column existed — and a duplicate, which is what happens when the
/// shared inbox of a department is also somebody's own address.</para>
/// </summary>
public sealed class NotificationService(
    HubDbContext database,
    IOptions<DivisionOptions> division,
    IClock clock,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<int> QueueAsync(NotificationIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!NotificationTypes.IsKnown(intent.Type))
        {
            // A type nobody declared has no template and no preference, so it would be a mail with
            // the key where the words should be. That is a mistake in the calling code.
            throw new InvalidOperationException(
                $"'{intent.Type}' is not a notification type. Declare it in NotificationTypes.");
        }

        var data = JsonSerializer.Serialize(intent.Data);
        var now = clock.UtcNow;
        var defaultLocale = division.Value.DefaultLocale;

        var vids = intent.Recipients.Where(recipient => recipient.Vid > 0).Select(recipient => recipient.Vid).Distinct().ToArray();

        var members = new Dictionary<int, MemberContact>();
        var switchedOff = new HashSet<int>();

        if (vids.Length > 0)
        {
            members = await database.Users
                .AsNoTracking()
                .Where(user => vids.Contains(user.Vid))
                .Select(user => new MemberContact(user.Vid, user.Email, user.Locale))
                .ToDictionaryAsync(member => member.Vid, cancellationToken);

            switchedOff = [.. await database.NotificationPreferences
                .AsNoTracking()
                .Where(preference => vids.Contains(preference.Vid)
                    && preference.Type == intent.Type
                    && !preference.Enabled)
                .Select(preference => preference.Vid)
                .ToListAsync(cancellationToken)];
        }

        var queued = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipient in intent.Recipients)
        {
            var (address, locale, vid) = Resolve(recipient, members, switchedOff, defaultLocale);

            if (address is null || !seen.Add(address))
            {
                continue;
            }

            database.Notifications.Add(new Notification
            {
                Type = intent.Type,
                Vid = vid,
                Address = address,
                Locale = locale,
                DataJson = data,
                Status = NotificationStatus.Pending,
                CreatedAt = now,
            });

            queued++;
        }

        if (queued > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Queued {Queued} notification(s) of type {Type} out of {Asked} recipient(s).",
            queued,
            intent.Type,
            intent.Recipients.Count);

        return queued;
    }

    private static (string? Address, string Locale, int Vid) Resolve(
        NotificationRecipient recipient,
        IReadOnlyDictionary<int, MemberContact> members,
        IReadOnlySet<int> switchedOff,
        string defaultLocale)
    {
        if (recipient.Vid == 0)
        {
            // A shared inbox belongs to nobody, so there is no preference to consult and no
            // language to prefer: it is written in the language of the division.
            return (recipient.Address, defaultLocale, 0);
        }

        if (switchedOff.Contains(recipient.Vid) || !members.TryGetValue(recipient.Vid, out var member))
        {
            return (null, defaultLocale, recipient.Vid);
        }

        return string.IsNullOrWhiteSpace(member.Email)
            ? (null, defaultLocale, recipient.Vid)
            : (member.Email, member.Locale ?? defaultLocale, recipient.Vid);
    }

    /// <summary>What a member is, to a queue: an address and a language.</summary>
    private sealed record MemberContact(int Vid, string? Email, string? Locale);
}
