using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace IvaoHub.Core.Notifications;

/// <summary>
/// One mail, already written in the language of the person who will read it.
/// <para><c>Text</c> and not <c>Body</c>: the body of a mail is prose, and the hub keeps the word
/// "body" for the one thing that is a document of blocks — a test of the architecture watches for a
/// second entity growing one, and a plain string called <c>Body</c> is exactly what that looks
/// like from the outside.</para>
/// </summary>
public sealed record OutgoingMail(string To, string Subject, string Text);

/// <summary>
/// The way out of the hub. It exists as an interface for one reason: a test must be able to read
/// what was sent without a mail server, and the dispatch job must stay a job.
/// </summary>
public interface IMailSender
{
    Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default);
}

/// <summary>
/// <b>The only file of the hub that talks SMTP.</b> A module never sends a mail; it publishes an
/// intent, the service queues it, the job hands it here (plan section 9.7). A test of the
/// architecture keeps it that way, by refusing any other file that names the client.
/// <para>Plain text and nothing else. A mail the hub sends is short and says where to go on the
/// site; an HTML body would be a second rendering of the same words, with a second way of looking
/// wrong in somebody's client.</para>
/// </summary>
public sealed class SmtpMailSender(IOptions<SmtpOptions> options) : IMailSender
{
    public async Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var settings = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName ?? string.Empty, settings.From));
        message.To.Add(MailboxAddress.Parse(mail.To));
        message.Subject = mail.Subject;
        message.Body = new TextPart(TextFormat.Plain) { Text = mail.Text };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.User))
        {
            await client.AuthenticateAsync(settings.User, settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
