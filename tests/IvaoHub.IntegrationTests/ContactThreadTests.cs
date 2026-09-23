using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The threads of the contacts (M2, T14a, notes 2026-09-15-contatti-con-risposte §5 and 2026-09-23-i-fili-dei-contatti):
/// a thread a module row opens once and never rewrites; the sender and a participant read and answer, another member
/// does not, the department does; the status moves with the answers; the sender never reads who answered; a reference
/// the sender may not see is refused.
/// <para>Over the wire with the real cookie, the real handler and the real interceptor, like the contact form: the
/// question "may a member who belongs to no department write into one?" is about the whole stack.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ContactThreadTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own (the contact form has 670001-670007), and a department of its own for the threads the form
    // opens — special operations — so the staff who hear about them are this class's.
    private const int SenderVid = 670011;
    private const int CoordinatorVid = 670012;
    private const int OtherMemberVid = 670013;
    private const int ReaderOnlyVid = 670014;
    private const int ParticipantVid = SampleReferenceResolver.Participant;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ARowOfAModuleOpensItsThreadOnceAndNeverRewritesIt()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var sample = new SampleEvent
        {
            Title = $"fo-test-thread-{Guid.NewGuid():N}"[..40],
            StartsAt = new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            Status = PublishStatus.Published,
            DisputedBy = SenderVid,
            DisputeParticipant = ParticipantVid,
        };

        module.Events.Add(sample);
        await module.SaveChangesAsync(token);

        var opened = await ThreadsOf(hub, sample.SourceId, token);
        var thread = Assert.Single(opened);
        Assert.Equal(ContactKinds.Dispute, thread.Kind);
        Assert.Equal(Department.ED, thread.OwnerDepartment);
        Assert.Equal(SenderVid, thread.CreatedBy);
        Assert.Equal([ParticipantVid], thread.AddedParticipants);
        Assert.Equal(ContactStatus.New, thread.Status);

        var reference = Assert.Single(await hub.ContactReferences.Where(row => row.MessageId == thread.Id).ToListAsync(token));
        Assert.Equal(sample.SourceId, reference.SourceId);
        Assert.Equal(sample.Title, reference.Label["en"]);

        // Saved again with a new title, and then without asking at all: the thread is still the one it was.
        var subject = thread.Subject;
        sample.Title += "-renamed";
        await module.SaveChangesAsync(token);
        sample.DisputedBy = null;
        await module.SaveChangesAsync(token);

        thread = Assert.Single(await ThreadsOf(hub, sample.SourceId, token));
        Assert.Equal(subject, thread.Subject);

        // Asking again opens nothing new either: once only is the row and the kind.
        sample.DisputedBy = SenderVid;
        await module.SaveChangesAsync(token);
        Assert.Single(await ThreadsOf(hub, sample.SourceId, token));

        module.Events.Remove(sample);
        await module.SaveChangesAsync(token);
        await RemoveThreadsAsync(hub, sample.SourceId, token);
    }

    [Fact]
    public async Task TheSenderAndAParticipantReadAndAnswerAnotherMemberDoesNotAndTheSenderNeverSeesWhoAnswered()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token, email: "threads-sender@example.org");
        await SeedUserAsync(ParticipantVid, token, email: "threads-participant@example.org");
        await SeedUserAsync(OtherMemberVid, token, email: "threads-other@example.org");
        await SeedUserAsync(CoordinatorVid, token, position: "IT-SOC", email: "threads-soc@example.org");

        using var sender = await SignedInAsync(SenderVid, token);
        using var participant = await SignedInAsync(ParticipantVid, token);
        using var other = await SignedInAsync(OtherMemberVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);

        var subject = $"Why this rule {Guid.NewGuid():N}";
        var id = await SubmitAsync(sender, subject, token);

        // The reference brought its participant along, and they were told.
        Assert.Contains(await QueuedAsync(subject, NotificationTypes.ContactThreadOpened, token), row => row.Vid == ParticipantVid);

        // Who reads: the sender, the participant, the department. Not another member: not even a 403, a 404.
        var asSender = await ThreadAsync(sender, id, token);
        Assert.True(asSender.GetProperty("readerIsSender").GetBoolean());
        Assert.Equal(0, asSender.GetProperty("participants").GetArrayLength());
        // The label taken when the thread opened, in the reader's language: the division's, for a member who chose none.
        Assert.Contains(asSender.GetProperty("references")[0].GetProperty("label").GetString(), new[] { "Event 5", "Evento 5" });
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(participant, $"{ContactsEndpoints.Pattern}/{id}/thread", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(other, $"{ContactsEndpoints.Pattern}/{id}/thread", token)).StatusCode);
        var asStaff = await ThreadAsync(coordinator, id, token);
        Assert.Equal(ParticipantVid, asStaff.GetProperty("participants")[0].GetInt32());

        // The department answers: the status moves, the sender and the participant hear it, the department does not.
        await ReplyAsync(coordinator, id, $"Because of the rule. {subject}", HttpStatusCode.OK, token);
        Assert.Equal(ContactStatus.Answered, (await MessageAsync(id, token)).Status);
        var told = await QueuedAsync(subject, NotificationTypes.ContactThreadReplied, token);
        Assert.Contains(told, row => row.Vid == SenderVid);
        Assert.Contains(told, row => row.Vid == ParticipantVid);
        Assert.DoesNotContain(told, row => row.Vid == CoordinatorVid);

        // The participant answers too, and so does the sender, which puts it back on top of the queue.
        await ReplyAsync(participant, id, "The validator agrees.", HttpStatusCode.OK, token);
        await ReplyAsync(sender, id, "Thank you, one more question.", HttpStatusCode.OK, token);
        Assert.Equal(ContactStatus.New, (await MessageAsync(id, token)).Status);
        Assert.Contains(await QueuedAsync(subject, NotificationTypes.ContactThreadReplied, token), row => row.Vid == CoordinatorVid);

        // Another member cannot answer either.
        await ReplyAsync(other, id, "Me too!", HttpStatusCode.NotFound, token);

        // The sender reads the department for both answers of its side; the staff reads the names.
        asSender = await ThreadAsync(sender, id, token);
        var replies = asSender.GetProperty("replies").EnumerateArray().ToList();
        Assert.Equal(["Department", "Participant", "Sender"], replies.Select(reply => reply.GetProperty("side").GetString()));
        Assert.All(replies.Take(2), reply =>
        {
            Assert.Equal(JsonValueKind.Null, reply.GetProperty("authorVid").ValueKind);
            Assert.Equal(JsonValueKind.Null, reply.GetProperty("authorName").ValueKind);
        });

        asStaff = await ThreadAsync(coordinator, id, token);
        var first = asStaff.GetProperty("replies")[0];
        Assert.Equal(CoordinatorVid, first.GetProperty("authorVid").GetInt32());
        Assert.Equal("Test User", first.GetProperty("authorName").GetString());

        // The member's own threads: the sender's and the participant's, not the other member's.
        Assert.Contains(id, await MineAsync(sender, token));
        Assert.Contains(id, await MineAsync(participant, token));
        Assert.DoesNotContain(id, await MineAsync(other, token));
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(other, $"{ContactsEndpoints.MinePattern}/{id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(participant, $"{ContactsEndpoints.MinePattern}/{id}", token)).StatusCode);

        // Taking part reads the thread; it does not open the back office's row to a write.
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(other, $"{ContactsEndpoints.Pattern}/{id}", token)).StatusCode);
    }

    [Fact]
    public async Task AReferenceTheSenderCannotSeeAndAKindTheyCannotOpenAreRefused()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token, email: "threads-sender@example.org");
        using var sender = await SignedInAsync(SenderVid, token);

        async Task<HttpStatusCode> Submit(string? kind, object[] references)
        {
            using var response = await sender.PostAsJsonAsync(
                ContactsEndpoints.Pattern,
                new { department = nameof(Department.SOD), subject = $"Refused {Guid.NewGuid():N}", body = "…", kind, references },
                token);
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.BadRequest, await Submit(ContactKinds.Clarification, [new { sourceModule = "sample", sourceId = "hidden:1" }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Submit(ContactKinds.Clarification, [new { sourceModule = "nobody", sourceId = "event:1" }]));
        Assert.Equal(HttpStatusCode.BadRequest, await Submit(ContactKinds.Clarification, []));
        Assert.Equal(HttpStatusCode.BadRequest, await Submit(ContactKinds.Dispute, []));
        Assert.Equal(HttpStatusCode.OK, await Submit(null, []));
    }

    [Fact]
    public async Task WhoOnlyReadsTheQueueAnswersAndStillCannotMoveTheStatusByHand()
    {
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SenderVid, token, email: "threads-sender@example.org");
        await SeedUserAsync(ReaderOnlyVid, token, email: "threads-reader@example.org");
        await GrantAsync(ReaderOnlyVid, "Contacts.View", Department.SOD, token);

        using var sender = await SignedInAsync(SenderVid, token);
        using var reader = await SignedInAsync(ReaderOnlyVid, token);

        var id = await SubmitAsync(sender, $"Read only {Guid.NewGuid():N}", token, clarification: false);

        // Contacts.View answers (note §3.2): the write of the status that goes with it passes the interceptor's net.
        await ReplyAsync(reader, id, "We read you.", HttpStatusCode.OK, token);
        var message = await MessageAsync(id, token);
        Assert.Equal(ContactStatus.Answered, message.Status);

        // The PUT of the status is still Contacts.Edit.
        using var put = await reader.PutAsJsonAsync(
            $"{ContactsEndpoints.Pattern}/{id}",
            new { status = nameof(ContactStatus.Closed), rowVersion = message.RowVersion },
            token);
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static Task<List<ContactMessage>> ThreadsOf(HubDbContext hub, string sourceId, CancellationToken cancellationToken) =>
        hub.ContactMessages.AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
            .ToListAsync(cancellationToken);

    private static async Task RemoveThreadsAsync(HubDbContext hub, string sourceId, CancellationToken cancellationToken)
    {
        var threads = await hub.ContactMessages
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sourceId)
            .ToListAsync(cancellationToken);
        var ids = threads.Select(row => row.Id).ToList();
        hub.ContactReferences.RemoveRange(await hub.ContactReferences.Where(row => ids.Contains(row.MessageId)).ToListAsync(cancellationToken));
        hub.ContactMessages.RemoveRange(threads);
        await hub.SaveChangesAsync(cancellationToken);
    }

    private async Task<ContactMessage> MessageAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>()
            .ContactMessages.AsNoTracking().FirstAsync(row => row.Id == id, cancellationToken);
    }

    private static async Task<long> SubmitAsync(HttpClient client, string subject, CancellationToken cancellationToken, bool clarification = true)
    {
        using var response = await client.PostAsJsonAsync(
            ContactsEndpoints.Pattern,
            new
            {
                department = nameof(Department.SOD),
                subject,
                body = "Can somebody explain this to me?",
                kind = clarification ? ContactKinds.Clarification : null,
                references = clarification ? new[] { new { sourceModule = SampleModule.ModuleKey, sourceId = "event:5" } } : null,
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContactSubmittedDto>(cancellationToken))!.Id;
    }

    private static async Task<JsonElement> ThreadAsync(HttpClient client, long id, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<JsonElement>($"{ContactsEndpoints.Pattern}/{id}/thread", cancellationToken);

    private static async Task ReplyAsync(HttpClient client, long id, string body, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync($"{ContactsEndpoints.Pattern}/{id}/replies", new { body }, cancellationToken);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<List<long>> MineAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{ContactsEndpoints.MinePattern}?pageSize=100", cancellationToken);
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt64())];
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, CancellationToken cancellationToken) =>
        client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);

    private async Task<IReadOnlyList<Notification>> QueuedAsync(string subject, string type, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var all = await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .AsNoTracking()
            .Where(row => row.Type == type)
            .ToListAsync(cancellationToken);

        return [.. all.Where(row => row.DataJson.Contains(subject, StringComparison.Ordinal))];
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        using var response = await client.PostAsync(
            new Uri($"{TestSignInStartupFilter.Path}?vid={vid}", UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return client;
    }

    private async Task GrantAsync(int vid, string permission, Department department, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (!await database.UserGrants.AnyAsync(
                row => row.Vid == vid && row.Value == permission && row.Department == department,
                cancellationToken))
        {
            database.UserGrants.Add(new UserGrant
            {
                Vid = vid,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = department,
                Effect = GrantEffect.Grant,
                Reason = "test",
            });
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedUserAsync(int vid, CancellationToken cancellationToken, string? position = null, string? email = null)
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
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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

        database.NotificationPreferences.RemoveRange(
            await database.NotificationPreferences.Where(row => row.Vid == vid).ToListAsync(cancellationToken));

        await database.SaveChangesAsync(cancellationToken);
    }
}
