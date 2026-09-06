using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The acceptance of G7, run rather than described: a member writes to a department, the message
/// lands in that department's queue and nowhere else, and one notification per person who wants it
/// leaves the queue in that person's own language (design M1 section 5).
/// <para>Everything goes over the wire against a real MariaDB, with the real cookie, the real
/// policies and the real interceptor — because the first question of this phase, "may somebody who
/// belongs to no department write a row into one?", is a question about the whole stack and about
/// the write guard in particular.</para>
/// <para>The dispatch job is built by hand rather than resolved, with its own SMTP settings and its
/// own sender. The host is left with no mail server configured on purpose: its Quartz trigger fires
/// every minute, and a background run halfway through a test would be counting attempts nobody
/// asked for.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContactsAndNotificationsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own: the suite shares one database across the collection, so two classes on
    // the same VID are one row.
    private const int SenderVid = 670001;
    private const int AtcCoordinatorVid = 670002;
    private const int AtcAssistantVid = 670003;
    private const int EventsCoordinatorVid = 670004;

    /// <summary>The shared inbox this class gives the ATC department, in the host it starts.</summary>
    private const string AtcMailbox = "atc@example.org";

    private HubWebApplicationFactory _plain = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public ValueTask InitializeAsync()
    {
        _plain = new HubWebApplicationFactory(mariaDb.ConnectionString);

        // The mailbox is given here rather than written into config/division.json: a division's own
        // addresses are that division's, and a test that needed one of them written down would be a
        // test that only passes in Italy.
        _factory = _plain.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<DivisionOptions>(division =>
                division.DepartmentMailboxes[nameof(Department.AOD)] = AtcMailbox)));

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _plain.DisposeAsync();
    }

    // ---- the form ------------------------------------------------------------------------------

    [Fact]
    public async Task ContactFormRefusesAnonymous()
    {
        var token = TestContext.Current.CancellationToken;
        using var anonymous = CreateClient();

        using var response = await SendAsync(
            anonymous,
            HttpMethod.Post,
            ContactsEndpoints.Pattern,
            new { department = nameof(Department.AOD), subject = "Hello", body = "Anybody there?" },
            token);

        // 401 and not a redirect: /api answers a machine, and there is no login page to send it to.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ContactMessageQueuesOneIntentForTheTargetDepartment()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");
        await SeedUserAsync(EventsCoordinatorVid, token, position: "IT-EC", email: "ec@example.org");

        var subject = $"Frequency question {Guid.NewGuid():N}";
        var id = await SubmitAsync(SenderVid, Department.AOD, subject, "Which one for LIRR?", token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var message = await database.ContactMessages
            .IgnoreQueryFilters()
            .FirstAsync(row => row.Id == id, token);

        // The department it was sent to owns it, which is what puts it in that queue and no other.
        Assert.Equal(Department.AOD, message.OwnerDepartment);
        Assert.Equal(ContactStatus.New, message.Status);

        // The sender is the session, written by the interceptor. There is no FromVid column to
        // disagree with it.
        Assert.Equal(SenderVid, message.CreatedBy);

        var queued = await QueuedAsync(scope, subject, token);

        // The shared inbox of the department, and the one person who is staff of it. The events
        // coordinator is staff of somewhere else and hears nothing.
        Assert.Equal(
            ["ac@example.org", AtcMailbox],
            queued.Select(row => row.Address).OrderBy(address => address, StringComparer.Ordinal).ToArray());

        Assert.DoesNotContain("ec@example.org", queued.Select(row => row.Address));
        Assert.All(queued, row => Assert.Equal(NotificationStatus.Pending, row.Status));
    }

    [Fact]
    public async Task AMemberWithoutAnAddressIsNotQueued()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        // Staff of the department, and has never signed in since the hub started keeping addresses.
        await SeedUserAsync(AtcAssistantVid, token, position: "IT-AOAC", email: null);

        var subject = $"Nobody to write to {Guid.NewGuid():N}";
        await SubmitAsync(SenderVid, Department.AOD, subject, "Is this reaching anyone?", token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var queued = await QueuedAsync(scope, subject, token);

        // The mailbox of the department still gets it, which is the whole reason a department has
        // one; the member with no address is skipped rather than queued to nowhere.
        Assert.Contains(AtcMailbox, queued.Select(row => row.Address));
        Assert.DoesNotContain(queued, row => row.Vid == AtcAssistantVid);
    }

    [Fact]
    public async Task NotificationSkippedWhenThePreferenceIsOff()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");

        using var coordinator = CreateClient();
        await SignInAsync(coordinator, AtcCoordinatorVid, token);

        using var saved = await SendAsync(
            coordinator,
            HttpMethod.Put,
            NotificationPreferenceEndpoints.Pattern,
            new { type = NotificationTypes.ContactReceived, enabled = false },
            token);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var subject = $"Not for me {Guid.NewGuid():N}";
        await SubmitAsync(SenderVid, Department.AOD, subject, "You said no thanks.", token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var queued = await QueuedAsync(scope, subject, token);

        // Nothing was written for them at all: the queue is what is going out, and a row that will
        // never be sent is a row somebody has to explain later.
        Assert.DoesNotContain(queued, row => row.Vid == AtcCoordinatorVid);
        Assert.Equal([AtcMailbox], queued.Select(row => row.Address));
    }

    // ---- the queue of the department -----------------------------------------------------------

    [Fact]
    public async Task AMessageIsNotReadableByAnotherDepartment()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(EventsCoordinatorVid, token, position: "IT-EC", email: "ec@example.org");

        var id = await SubmitAsync(SenderVid, Department.AOD, $"Private {Guid.NewGuid():N}", "…", token);

        using var events = CreateClient();
        await SignInAsync(events, EventsCoordinatorVid, token);

        using var read = await events.GetAsync(
            new Uri($"{ContactsEndpoints.Pattern}/{id}", UriKind.Relative),
            token);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task AStaffMemberMovesTheStatusAndCannotRewriteTheMessage()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");

        const string written = "The frequency on the chart is wrong.";
        var id = await SubmitAsync(SenderVid, Department.AOD, $"Chart {Guid.NewGuid():N}", written, token);

        using var coordinator = CreateClient();
        await SignInAsync(coordinator, AtcCoordinatorVid, token);

        var detail = await coordinator.GetFromJsonAsync<JsonElement>(
            $"{ContactsEndpoints.Pattern}/{id}",
            token);

        var rowVersion = detail.GetProperty("rowVersion").GetString();
        var sentSubject = detail.GetProperty("subject").GetString();

        // The payload carries a subject and a body that the contract does not have. They are not
        // refused, they are simply not there to apply: the write DTO has one field.
        using var updated = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContactsEndpoints.Pattern}/{id}",
            new
            {
                status = nameof(ContactStatus.Answered),
                rowVersion,
                subject = "Rewritten by the department",
                body = "This is not what was sent.",
            },
            token);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var message = await database.ContactMessages.IgnoreQueryFilters().FirstAsync(row => row.Id == id, token);

        Assert.Equal(ContactStatus.Answered, message.Status);
        Assert.Equal(written, message.Body);
        Assert.Equal(sentSubject, message.Subject);

        // Who last moved it is the audit trail, not a HandledBy column beside it.
        Assert.Equal(AtcCoordinatorVid, message.UpdatedBy);
    }

    [Fact]
    public async Task AMessageCannotBeDeleted()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");

        var id = await SubmitAsync(SenderVid, Department.AOD, $"Kept {Guid.NewGuid():N}", "…", token);

        using var coordinator = CreateClient();
        await SignInAsync(coordinator, AtcCoordinatorVid, token);

        using var deleted = await SendAsync(
            coordinator,
            HttpMethod.Delete,
            $"{ContactsEndpoints.Pattern}/{id}",
            payload: null,
            token);

        // The resource does not answer DELETE at all — there is no route to match, so it is a plain
        // 404 — because a queue with a delete button is a queue whose history depends on who was
        // embarrassed.
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        Assert.True(await database.ContactMessages.IgnoreQueryFilters().AnyAsync(row => row.Id == id, token));
    }

    // ---- the queue that goes out -----------------------------------------------------------------

    [Fact]
    public async Task NotificationUsesRecipientLocale()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org", locale: "it");
        await SeedUserAsync(AtcAssistantVid, token, position: "IT-AOAC", email: "aac@example.org", locale: "en");

        var subject = $"Two languages {Guid.NewGuid():N}";
        await SubmitAsync(SenderVid, Department.AOD, subject, "One message, two readers.", token);

        var sender = new RecordingMailSender();
        await RunDispatchAsync(sender, token);

        // The queue is the installation's, not this test's, so the mails are picked by the subject
        // this test made unique rather than by "the only one that went to that address".
        var mine = sender.Sent.Where(mail => mail.Subject.Contains(subject, StringComparison.Ordinal)).ToArray();
        var italian = mine.Single(mail => mail.To == "ac@example.org");
        var english = mine.Single(mail => mail.To == "aac@example.org");

        await using var scope = _factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<LocaleCatalog>();

        // Not "contains an Italian word": each body begins with what the template of that language
        // begins with, so the test says which language was used without repeating the words.
        Assert.StartsWith(OpeningOf(catalog, "it"), italian.Text, StringComparison.Ordinal);
        Assert.StartsWith(OpeningOf(catalog, "en"), english.Text, StringComparison.Ordinal);
        Assert.NotEqual(italian.Text, english.Text);

        // The placeholders were filled in, in both: a mail that says {{subject}} is a mail nobody
        // can act on, and it is exactly what a missing value looks like.
        Assert.Contains(subject, italian.Subject, StringComparison.Ordinal);
        Assert.Contains(subject, english.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", italian.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", english.Text, StringComparison.Ordinal);

        // The shared inbox belongs to nobody, so it reads the language of the division.
        var mailbox = mine.Single(mail => mail.To == AtcMailbox);
        var defaultLocale = scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value.DefaultLocale;
        Assert.StartsWith(OpeningOf(catalog, defaultLocale), mailbox.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotificationRetriesThenGivesUp()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");

        var subject = $"Nobody home {Guid.NewGuid():N}";
        await SubmitAsync(SenderVid, Department.AOD, subject, "The mail server is down.", token);

        var sender = new RecordingMailSender { Throws = true };

        for (var run = 0; run < NotificationDispatchJob.MaxAttempts; run++)
        {
            await RunDispatchAsync(sender, token);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var failed = await QueuedAsync(scope, subject, token);

            Assert.NotEmpty(failed);
            Assert.All(failed, row =>
            {
                Assert.Equal(NotificationStatus.Failed, row.Status);
                Assert.Equal(NotificationDispatchJob.MaxAttempts, row.Attempts);
                Assert.False(string.IsNullOrWhiteSpace(row.LastError));
            });
        }

        // And then it is left alone: out of attempts is a decision, not a thing to keep retrying
        // every minute for as long as the installation lives.
        var attemptsBefore = sender.Attempts;
        await RunDispatchAsync(sender, token);
        Assert.Equal(attemptsBefore, sender.Attempts);
    }

    [Fact]
    public async Task TheQueueIsLeftAloneWhenNoMailServerIsConfigured()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token);
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");

        var subject = $"Waiting {Guid.NewGuid():N}";
        await SubmitAsync(SenderVid, Department.AOD, subject, "There is no server yet.", token);

        var sender = new RecordingMailSender();
        await RunDispatchAsync(sender, token, smtp: new SmtpOptions());

        Assert.Empty(sender.Sent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var waiting = await QueuedAsync(scope, subject, token);

        // Nothing was tried and nothing was lost: the rows go out on the first run after somebody
        // configures a server.
        Assert.All(waiting, row =>
        {
            Assert.Equal(NotificationStatus.Pending, row.Status);
            Assert.Equal(0, row.Attempts);
        });
    }

    [Fact]
    public async Task AnUnknownNotificationTypeIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(AtcCoordinatorVid, token, position: "IT-AOC", email: "ac@example.org");

        using var coordinator = CreateClient();
        await SignInAsync(coordinator, AtcCoordinatorVid, token);

        using var response = await SendAsync(
            coordinator,
            HttpMethod.Put,
            NotificationPreferenceEndpoints.Pattern,
            new { type = "contact.invented", enabled = false },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The template of a language up to its first placeholder: the longest piece of it that is the
    /// same in every mail of that type, whatever the message was about.
    /// </summary>
    private static string OpeningOf(LocaleCatalog catalog, string locale)
    {
        var template = catalog.Resolve(locale, NotificationTypes.BodyKey(NotificationTypes.ContactReceived));
        var placeholder = template.IndexOf("{{", StringComparison.Ordinal);

        Assert.True(placeholder > 0, $"The {locale} template has nothing before its first placeholder.");
        return template[..placeholder];
    }

    // ---- the pieces the tests share --------------------------------------------------------------

    /// <summary>
    /// Runs the dispatch job with a sender and settings of the test's own. The host itself has no
    /// mail server configured, so its own trigger does nothing while this runs.
    /// </summary>
    private async Task RunDispatchAsync(
        IMailSender sender,
        CancellationToken cancellationToken,
        SmtpOptions? smtp = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var job = new NotificationDispatchJob(
            scope.ServiceProvider.GetRequiredService<HubDbContext>(),
            sender,
            scope.ServiceProvider.GetRequiredService<LocaleCatalog>(),
            Options.Create(smtp ?? new SmtpOptions { Host = "localhost", From = "hub@example.org" }),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            NullLogger<NotificationDispatchJob>.Instance);

        await job.RunAsync(cancellationToken);
    }

    /// <summary>The rows queued for one message, found by the subject the test made unique.</summary>
    private static async Task<IReadOnlyList<Notification>> QueuedAsync(
        AsyncServiceScope scope,
        string subject,
        CancellationToken cancellationToken)
    {
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var all = await database.Notifications
            .AsNoTracking()
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);

        // Filtered here rather than in SQL: the column is json, the subject is what makes this
        // test's rows its own, and a LIKE inside a json document is a translation waiting to break.
        return [.. all.Where(row => row.DataJson.Contains(subject, StringComparison.Ordinal))];
    }

    private async Task<long> SubmitAsync(
        int vid,
        Department department,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        await SignInAsync(client, vid, cancellationToken);

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            ContactsEndpoints.Pattern,
            new { department = department.ToString(), subject, body },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var submitted = await response.Content.ReadFromJsonAsync<ContactSubmittedDto>(cancellationToken);
        Assert.NotNull(submitted);
        return submitted.Id;
    }

    private HttpClient CreateClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private async Task SignInAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedUserAsync(
        int vid,
        CancellationToken cancellationToken,
        string? position = null,
        string? email = null,
        string? locale = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsStaff = position is not null;
        user.Email = email;
        user.Locale = locale;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
                row => row.Vid == vid && row.Position == position,
                cancellationToken))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        // A preference left over from another test in this class would decide this one.
        var preferences = await database.NotificationPreferences
            .Where(row => row.Vid == vid)
            .ToListAsync(cancellationToken);

        database.NotificationPreferences.RemoveRange(preferences);

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A mail server that writes down what it was handed, and fails on demand.</summary>
    private sealed class RecordingMailSender : IMailSender
    {
        private readonly List<OutgoingMail> _sent = [];

        public bool Throws { get; init; }

        public int Attempts { get; private set; }

        public IReadOnlyList<OutgoingMail> Sent => _sent;

        public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default)
        {
            Attempts++;

            if (Throws)
            {
                return Task.FromException(new InvalidOperationException("The mail server refused the message."));
            }

            _sent.Add(mail);
            return Task.CompletedTask;
        }
    }
}
