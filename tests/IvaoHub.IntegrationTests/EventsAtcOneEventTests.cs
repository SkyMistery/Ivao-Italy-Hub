using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The heads of the events with a roster, for the staff of the ATC, read with a grant of <c>EventAtc.View</c> on one event (issue #311,
/// note 2026-11-01-l-atc-di-un-solo-evento): the list holds that event and no other, and the head of another event is refused — until the
/// fix, the endpoints asked the one handler without a row, which answers «held somewhere», and the member read the heads of every event
/// with a roster still to end. The same permission held without a scope still reads them all, as since E11a.
/// <para>⚠️ The members are seeded <b>without an address</b> and no position, and every event is written by the installation, as in
/// <see cref="EventsScopedGrantTests"/>: what each member holds is the grant of the test.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsAtcOneEventTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The events module's range handed to the fix of #311: 761330–761339.
    private const int OneEventVid = 761330;
    private const int DepartmentVid = 761331;

    private const string SlugStem = "evt-test-fix311";

    private const string AtcPath = "/staff/events/atc";

    private static readonly string[] Locales = ["it", "en"];

    private static readonly int[] Vids = [OneEventVid, DepartmentVid];

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        foreach (var vid in Vids)
        {
            await SeedMemberAsync(vid, token);
        }

        await ForgetAsync(token);
    }

    /// <summary>What the class seeded is taken back: its events with their rows and the members' grants.</summary>
    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AGrantOnOneEventReadsTheHeadOfThatEventAndOfNoOther()
    {
        var token = TestContext.Current.CancellationToken;
        var theirs = await EventRowAsync("theirs", token);
        var other = await EventRowAsync("other", token);

        await GrantAsync(OneEventVid, Event.ScopeOf(theirs), token);
        using var member = await SignedInAsync(OneEventVid, token);

        // The list: their event alone, counted alone.
        var heads = await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}?q={SlugStem}&pageSize=100", token), token);
        var head = Assert.Single(heads.GetProperty("items").EnumerateArray());
        Assert.Equal(theirs, head.GetProperty("id").GetInt64());
        Assert.Equal(1, heads.GetProperty("total").GetInt32());

        // The head of their event, read; of the other, refused.
        var read = await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{theirs}", token), token);
        Assert.Equal(Slug("theirs"), read.GetProperty("slug").GetString());

        using (var refused = await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{other}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // The menu leads to the list, which shows their event.
        Assert.Contains(AtcPath, StaffPaths(await member.GetFromJsonAsync<JsonElement>("/api/me", token)));
    }

    [Fact]
    public async Task HeldWithoutAScopeTheHeadsOfEveryEventAreRead()
    {
        var token = TestContext.Current.CancellationToken;
        var first = await EventRowAsync("first", token);
        var second = await EventRowAsync("second", token);

        await GrantAsync(DepartmentVid, scope: null, token);
        using var member = await SignedInAsync(DepartmentVid, token);

        var heads = await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}?q={SlugStem}&pageSize=100", token), token);
        Assert.Equal(
            [first, second],
            heads.GetProperty("items").EnumerateArray().Select(head => head.GetProperty("id").GetInt64()).Order());

        foreach (var id in new[] { first, second })
        {
            await OkAsync(await member.GetAsync($"{AtcPositionEndpoints.EventsPattern}/{id}", token), token);
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static List<string> StaffPaths(JsonElement me) =>
        [.. me.GetProperty("navigation").GetProperty("staff").EnumerateArray().Select(entry => entry.GetProperty("path").GetString()!)];

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

    /// <summary>A draft with a roster some days from today, in the care of the base department of the module, written by the installation.</summary>
    private async Task<long> EventRowAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var starts = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(25).AddHours(17), DateTimeKind.Utc);

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfo",
            HasRoster = true,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = starts.AddHours(4),
            Visibility = Visibility.Public,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    /// <summary>
    /// <c>EventAtc.View</c> granted to the member on the base department of the module, on one event or — with no scope — on all of it,
    /// written straight to the table. The member's other grants go first: each test holds one.
    /// </summary>
    private async Task GrantAsync(int vid, string? scope, CancellationToken cancellationToken)
    {
        await using var services = _factory.Services.CreateAsyncScope();
        var database = services.ServiceProvider.GetRequiredService<HubDbContext>();

        await database.UserGrants.Where(grant => grant.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = EventsPermissions.AtcView,
            Department = Department.ED,
            ResourceScope = scope,
            Effect = GrantEffect.Grant,
            Reason = SlugStem,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Signed in after the grant: the effective permissions are computed at login.</summary>
    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member of the staff the grants are given to: no position and never an address.</summary>
    private async Task SeedMemberAsync(int vid, CancellationToken cancellationToken)
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
        user.LastName = "Events";
        user.Email = null;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows, and the members' grants, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().UserGrants
            .Where(grant => grant.Vid == OneEventVid || grant.Vid == DepartmentVid)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
