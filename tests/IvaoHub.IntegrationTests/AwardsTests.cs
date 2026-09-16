using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Awards;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The awards of the core (M2, T4b), through the real host and real cookies: a row of a module points a
/// member out and proposes an award, somebody holding <c>Awards.Assign</c> answers the line of the queue
/// with an assignment, and the line is handled in the same save. The hub never assigns by itself.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class AwardsTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int SuperadminVid = 780041;
    private const int EventsCoordinatorVid = 780042;
    private const int PilotVid = 780043;
    private const int OtherPilotVid = 780044;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(SuperadminVid, position: null, superadmin: true, token);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", superadmin: false, token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AnAssignmentAnsweringTheQueueHandlesTheLineInTheSameSave()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var award = await CreateAwardAsync(superadmin, imageMediaId: null, token);
        var signal = await SignalAsync(PilotVid, award, token);

        // In the queue, with the name of the award the row proposes.
        var queue = await superadmin.GetFromJsonAsync<JsonElement>(
            $"{AwardEndpoints.SignalsPattern}?filter[vid]={PilotVid}&pageSize=100", token);
        var line = Assert.Single(queue.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetInt64() == signal);
        Assert.Equal(award, line.GetProperty("awardId").GetInt64());
        Assert.StartsWith("fo-test-award-", line.GetProperty("awardName").GetProperty("en").GetString(), StringComparison.Ordinal);

        using (var assigned = await AssignAsync(superadmin, award, PilotVid, signal, token))
        {
            Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        }

        var handled = await SignalRowAsync(signal, token);
        Assert.Equal(AwardSignalStatus.Handled, handled.Status);
        Assert.Equal(SuperadminVid, handled.HandledBy);
        Assert.NotNull(handled.HandledAt);

        // Answered once: a second assignment for the same line is refused on the field.
        using (var again = await AssignAsync(superadmin, award, PilotVid, signal, token))
        {
            await AssertRefusedAsync(again, "signalId", "errors.awards.signalNotPending", token);
        }

        // Handled is not something a person takes back on the queue.
        using (var dismissed = await superadmin.PutAsJsonAsync(
            $"{AwardEndpoints.SignalsPattern}/{signal}", new { status = nameof(AwardSignalStatus.Dismissed) }, token))
        {
            await AssertRefusedAsync(dismissed, "status", "errors.awards.handledByAssignment", token);
        }

        // An award somebody holds is retired, never deleted.
        using (var deleted = await superadmin.DeleteAsync(new Uri($"{AwardEndpoints.AwardsPattern}/{award}", UriKind.Relative), token))
        {
            await AssertRefusedAsync(deleted, "id", "errors.awards.assigned", token);
        }
    }

    [Fact]
    public async Task ADismissedLineLeavesTheQueueAndComesBackAndIsNeverAnsweredForSomebodyElse()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);

        var award = await CreateAwardAsync(superadmin, imageMediaId: null, token);
        var signal = await SignalAsync(OtherPilotVid, award, token);

        using (var wrongMember = await AssignAsync(superadmin, award, PilotVid, signal, token))
        {
            await AssertRefusedAsync(wrongMember, "vid", "errors.awards.signalOtherMember", token);
        }

        using (var dismissed = await superadmin.PutAsJsonAsync(
            $"{AwardEndpoints.SignalsPattern}/{signal}", new { status = nameof(AwardSignalStatus.Dismissed) }, token))
        {
            Assert.Equal(HttpStatusCode.OK, dismissed.StatusCode);
        }

        var row = await SignalRowAsync(signal, token);
        Assert.Equal(AwardSignalStatus.Dismissed, row.Status);
        Assert.Equal(SuperadminVid, row.HandledBy);
        Assert.False(await InQueueAsync(superadmin, signal, token));

        using (var back = await superadmin.PutAsJsonAsync(
            $"{AwardEndpoints.SignalsPattern}/{signal}", new { status = nameof(AwardSignalStatus.Pending) }, token))
        {
            Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        }

        row = await SignalRowAsync(signal, token);
        Assert.Equal(AwardSignalStatus.Pending, row.Status);
        Assert.Null(row.HandledBy);
        Assert.True(await InQueueAsync(superadmin, signal, token));

        // A retired award cannot be chosen, not even for a line that proposes it.
        using (var retire = await superadmin.PutAsJsonAsync($"{AwardEndpoints.AwardsPattern}/{award}", AwardBody("retired", imageMediaId: null, isActive: false), token))
        {
            Assert.Equal(HttpStatusCode.OK, retire.StatusCode);
        }

        using var refused = await AssignAsync(superadmin, award, OtherPilotVid, signal, token);
        await AssertRefusedAsync(refused, "awardId", "errors.awards.retired", token);
    }

    [Fact]
    public async Task TheCatalogueIsReadByEveryDepartmentWrittenByItsOwnAndTheQueueOnlyByWhoAssigns()
    {
        var token = TestContext.Current.CancellationToken;
        using var superadmin = await SignedInAsync(SuperadminVid, token);
        using var events = await SignedInAsync(EventsCoordinatorVid, token);

        var image = 9_000_000_000L + Random.Shared.Next(1, int.MaxValue);
        var award = await CreateAwardAsync(superadmin, image, token);

        // The picture is a use without an end: the library never deletes it while the award exists.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var use = await database.MediaUses.AsNoTracking()
                .SingleAsync(row => row.SourceModule == ProjectionSource.Core && row.SourceId == $"award:{award}", token);
            Assert.Equal(image, use.MediaId);
            Assert.Null(use.UsedUntil);
        }

        // A coordinator of another department reads the award of flight operations...
        using (var read = await events.GetAsync(new Uri($"{AwardEndpoints.AwardsPattern}/{award}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        // ...does not change it...
        using (var write = await events.PutAsJsonAsync($"{AwardEndpoints.AwardsPattern}/{award}", AwardBody("taken", image, isActive: true), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        }

        // ...and without Awards.Assign sees neither the queue nor the register.
        using (var queue = await events.GetAsync(new Uri(AwardEndpoints.SignalsPattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, queue.StatusCode);
        }

        using (var register = await events.GetAsync(new Uri(AwardEndpoints.AssignmentsPattern, UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);
        }

        // Nobody holds it: deleting it is allowed, and takes the use of the picture with it.
        using (var deleted = await superadmin.DeleteAsync(new Uri($"{AwardEndpoints.AwardsPattern}/{award}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            Assert.False(await database.MediaUses.AnyAsync(
                row => row.SourceModule == ProjectionSource.Core && row.SourceId == $"award:{award}", token));
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static object AwardBody(string suffix, long? imageMediaId, bool isActive)
    {
        var name = $"fo-test-award-{suffix}-{Guid.NewGuid():N}";
        return new
        {
            ownerDepartment = nameof(Department.FOD),
            name = new Dictionary<string, string> { ["it"] = name, ["en"] = name },
            description = (object?)null,
            criteria = (object?)null,
            imageMediaId,
            isActive,
        };
    }

    private static async Task<long> CreateAwardAsync(HttpClient client, long? imageMediaId, CancellationToken cancellationToken)
    {
        using var created = await client.PostAsJsonAsync(AwardEndpoints.AwardsPattern, AwardBody("new", imageMediaId, isActive: true), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, long award, int vid, long signal, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            AwardEndpoints.AssignmentsPattern,
            new { awardId = award, vid, reason = "fo-test-award completed the tour", signalId = signal },
            cancellationToken);

    /// <summary>What a completed tour will do: a row of a module that points a member out, and proposes its award.</summary>
    private async Task<long> SignalAsync(int vid, long award, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var sample = new SampleEvent
        {
            Title = $"fo-test-award-{Guid.NewGuid():N}",
            StartsAt = new DateTime(2030, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            Status = PublishStatus.Published,
            AwardeeVid = vid,
            ProposedAwardId = award,
        };

        module.Events.Add(sample);
        await module.SaveChangesAsync(cancellationToken);

        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        return await hub.AwardSignals.AsNoTracking()
            .Where(row => row.SourceModule == SampleModule.ModuleKey && row.SourceId == sample.SourceId)
            .Select(row => row.Id)
            .SingleAsync(cancellationToken);
    }

    private async Task<AwardSignal> SignalRowAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AwardSignals.AsNoTracking()
            .SingleAsync(row => row.Id == id, cancellationToken);
    }

    private static async Task<bool> InQueueAsync(HttpClient client, long signal, CancellationToken cancellationToken)
    {
        var queue = await client.GetFromJsonAsync<JsonElement>(
            $"{AwardEndpoints.SignalsPattern}?filter[vid]={OtherPilotVid}&pageSize=100", cancellationToken);
        return queue.GetProperty("items").EnumerateArray().Any(row => row.GetProperty("id").GetInt64() == signal);
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(key, problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(error => error.GetString()));
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedUserAsync(int vid, string? position, bool superadmin, CancellationToken cancellationToken)
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
        user.LastName = "Awards";
        user.IsStaff = true;
        user.IsSuperadmin = superadmin;
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

        await database.SaveChangesAsync(cancellationToken);
    }
}
