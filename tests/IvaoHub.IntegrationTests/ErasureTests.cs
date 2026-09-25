using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Preferences;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Erasing a person's data (M2, T20b, note 2026-09-25-la-cancellazione-dei-dati-di-una-persona), through the real host, the
/// core and the test module:
/// <list type="bullet">
/// <item>what is about the person goes — their user, access, notifications (to them and about them), preferences, award
/// signals, the threads they opened and the module's rows about them;</item>
/// <item>what they did for others stays, with the pseudonym where their VID was — a thread they answered, a row of the
/// module they created — in the core's context and in the module's, with nothing written by the module for it;</item>
/// <item>the audit log no longer holds the VID anywhere, the copies of what was erased are empty, the history of the rest
/// is kept, and one row says an erasure happened;</item>
/// <item>only a super administrator erases, never one, and running it again does no harm.</item>
/// </list>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ErasureTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int PersonVid = 780095;
    private const int OtherVid = 780096;
    private const int SuperadminVid = 780097;

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _items = [];
    private readonly List<long> _threads = [];

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(OtherVid, superadmin: false, token);
        await SeedUserAsync(SuperadminVid, superadmin: true, token);
    }

    public async ValueTask DisposeAsync()
    {
        var token = CancellationToken.None;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await sample.Items.IgnoreQueryFilters().Where(row => _items.Contains(row.Id)).ExecuteDeleteAsync(token);

            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.ContactReplies.Where(row => _threads.Contains(row.MessageId)).ExecuteDeleteAsync(token);
            await database.ContactReferences.Where(row => _threads.Contains(row.MessageId)).ExecuteDeleteAsync(token);
            await database.ContactMessages.IgnoreQueryFilters().Where(row => _threads.Contains(row.Id)).ExecuteDeleteAsync(token);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task WhatIsAboutThePersonGoesAndWhatTheyDidForOthersStaysUnderThePseudonym()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(PersonVid, superadmin: true, token);
        var text = PersonVid.ToString(CultureInfo.InvariantCulture);

        // As a super administrator for a while, the person works for others: a row of the module about somebody else, and an
        // answer in somebody else's thread.
        using (var person = await SignedInAsync(PersonVid, token))
        {
            _items.Add(await CreateItemAsync(person, "erasure-kept", OtherVid, token));
            _items.Add(await CreateItemAsync(person, "erasure-gone", PersonVid, token));

            using var other = await SignedInAsync(OtherVid, token);
            var theirs = await SubmitAsync(other, $"Other's question {Guid.NewGuid():N}", token);
            _threads.Add(theirs);
            await ReplyAsync(person, theirs, "An answer from the person.", token);

            // And writes to the division themselves: a thread of their own, and the mail the department gets about it.
            _threads.Add(await SubmitAsync(person, $"Person's question {Guid.NewGuid():N}", token));
        }

        await SeedAboutThePersonAsync(token);
        var (kept, gone) = (_items[0], _items[1]);
        var (theirThread, ownThread) = (_threads[0], _threads[1]);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var preview = await superadmin.GetFromJsonAsync<JsonElement>($"{ErasureEndpoints.Pattern}/{PersonVid}", token);
        Assert.Equal("Erasure Test", preview.GetProperty("name").GetString());
        Assert.False(preview.GetProperty("isSuperadmin").GetBoolean());
        Assert.Equal(1, Line(preview, "erasure.lines.threads"));
        Assert.Equal(1, Line(preview, SampleEraser.ItemsKey));

        using var response = await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{PersonVid}", UriKind.Relative), null, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        var pseudonym = result.GetProperty("pseudonym").GetInt32();
        Assert.True(pseudonym < 0);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var sample = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        // About the person: gone.
        Assert.False(await database.Users.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await database.UserGrants.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await database.UserStaffPositions.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await database.NotificationPreferences.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await database.UserPreferences.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await database.AwardSignals.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await database.Notifications.AnyAsync(row => row.Vid == PersonVid || row.DataJson.Contains(text), token));
        Assert.False(await database.ContactMessages.IgnoreQueryFilters().AnyAsync(row => row.Id == ownThread, token));
        Assert.False(await sample.Items.IgnoreQueryFilters().AnyAsync(row => row.Id == gone, token));

        // What they did for others: kept, under the pseudonym, in the core's context and in the module's.
        var item = await sample.Items.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == kept, token);
        Assert.Equal((pseudonym, pseudonym, OtherVid), (item.CreatedBy, item.UpdatedBy, item.StakeholderVid));
        var thread = await database.ContactMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == theirThread, token);
        Assert.Equal((OtherVid, pseudonym), (thread.CreatedBy, thread.UpdatedBy));
        Assert.Equal(pseudonym, (await database.ContactReplies.SingleAsync(row => row.MessageId == theirThread, token)).AuthorVid);

        // The audit log: the VID nowhere, the erased rows' copies empty, the rest of the history kept, one row that says so.
        var audit = database.AuditLog.AsNoTracking();
        Assert.False(await audit.AnyAsync(row => row.Vid == PersonVid, token));
        Assert.False(await audit.AnyAsync(row => row.Entity == "hub_users" && row.EntityId == text, token));
        Assert.False(await audit.AnyAsync(row => row.BeforeJson!.Contains(text) || row.AfterJson!.Contains(text), token));
        Assert.False(await audit.AnyAsync(
            row => row.Entity == "smp_items" && row.EntityId == gone.ToString(CultureInfo.InvariantCulture)
                && (row.BeforeJson != null || row.AfterJson != null),
            token));
        Assert.True(await audit.AnyAsync(
            row => row.Entity == "smp_items" && row.EntityId == kept.ToString(CultureInfo.InvariantCulture)
                && row.Vid == pseudonym && row.AfterJson != null && row.Ip == null,
            token));
        Assert.True(await audit.AnyAsync(
            row => row.Action == PersonalDataErasure.AuditAction && row.EntityId == pseudonym.ToString(CultureInfo.InvariantCulture)
                && row.Vid == SuperadminVid,
            token));
    }

    [Fact]
    public async Task OnlyASuperadminErasesAndNeverASuperadmin()
    {
        var token = TestContext.Current.CancellationToken;

        using var other = await SignedInAsync(OtherVid, token);
        using (var forbidden = await other.GetAsync(new Uri($"{ErasureEndpoints.Pattern}/{SuperadminVid}", UriKind.Relative), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var preview = await superadmin.GetFromJsonAsync<JsonElement>($"{ErasureEndpoints.Pattern}/{SuperadminVid}", token);
        Assert.True(preview.GetProperty("isSuperadmin").GetBoolean());

        using var refused = await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{SuperadminVid}", UriKind.Relative), null, token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.True((await refused.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors").TryGetProperty("vid", out _));

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<HubDbContext>().Users.AnyAsync(row => row.Vid == SuperadminVid, token));
    }

    [Fact]
    public async Task ASecondErasureOfTheSamePersonFindsNothingAndTakesANewNumber()
    {
        var token = TestContext.Current.CancellationToken;
        const int vid = 780098;
        await SeedUserAsync(vid, superadmin: false, token);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var first = await EraseAsync(superadmin, vid, token);
        var second = await EraseAsync(superadmin, vid, token);

        Assert.True(second.GetProperty("pseudonym").GetInt32() < first.GetProperty("pseudonym").GetInt32());
        Assert.Equal(1, Line(first, "erasure.lines.user"));
        Assert.All(second.GetProperty("lines").EnumerateArray(), line => Assert.Equal(0, line.GetProperty("count").GetInt32()));
    }

    /// <summary>
    /// Every column the erasure gives the pseudonym to, read from the models of the real host (note §3). A new column of a
    /// person that follows the convention changes this list, and whoever adds it sees it here; one that does not follow it
    /// would be missed by the erasure, and this is the list a review compares a new table against.
    /// </summary>
    [Fact]
    public async Task TheColumnsThatNameAPersonAreTheOnesTheErasureKnows()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<HubDbContext>(),
            scope.ServiceProvider.GetRequiredService<IvaoHub.Modules.FlightOps.Data.FlightOpsDbContext>(),
        ];

        var columns = contexts
            .SelectMany(context => context.Model.GetEntityTypes().Where(entity => PersonColumns.RewrittenBy(context, entity)))
            .SelectMany(entity => entity.GetProperties()
                .Where(property => PersonColumns.IsVid(property))
                .Select(property => $"{entity.GetTableName()}.{property.GetColumnName()}{(property.IsPrimaryKey() ? " (key)" : string.Empty)}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(PersonColumnsOfTheHub, columns);
    }

    private static readonly string[] PersonColumnsOfTheHub =
    [
        "cms_award_signals.handled_by",
        "cms_award_signals.vid",
        "cms_calendar_entries.created_by",
        "cms_calendar_entries.updated_by",
        "cms_calendar_kinds.created_by",
        "cms_calendar_kinds.updated_by",
        "cms_categories.created_by",
        "cms_categories.updated_by",
        "cms_contact_messages.created_by",
        "cms_contact_messages.updated_by",
        "cms_contact_replies.author_vid",
        "cms_content_versions.approved_by",
        "cms_content_versions.published_by",
        "cms_contents.created_by",
        "cms_contents.ready_by",
        "cms_contents.updated_by",
        "cms_links.created_by",
        "cms_links.updated_by",
        "cms_media.created_by",
        "cms_media.updated_by",
        "cms_menu_items.created_by",
        "cms_menu_items.updated_by",
        "fo_aircraft_groups.created_by",
        "fo_aircraft_groups.updated_by",
        "fo_aircraft_profiles.created_by",
        "fo_aircraft_profiles.updated_by",
        "fo_bans.created_by",
        "fo_bans.updated_by",
        "fo_bans.vid",
        "fo_callsign_rules.created_by",
        "fo_callsign_rules.updated_by",
        "fo_check_results.by_vid",
        "fo_enrolments.created_by",
        "fo_enrolments.updated_by",
        "fo_enrolments.vid",
        "fo_errors.created_by",
        "fo_errors.updated_by",
        "fo_hubs.created_by",
        "fo_hubs.updated_by",
        "fo_leg_issues.created_by",
        "fo_leg_issues.updated_by",
        "fo_legs.created_by",
        "fo_legs.updated_by",
        "fo_pirep_events.by_vid",
        "fo_pireps.assigned_to_vid",
        "fo_pireps.created_by",
        "fo_pireps.decided_by_vid",
        "fo_pireps.dispute_decided_by_vid",
        "fo_pireps.updated_by",
        "fo_pireps.vid",
        "fo_rotations.created_by",
        "fo_rotations.updated_by",
        "fo_rules.created_by",
        "fo_rules.updated_by",
        "fo_tour_constraints.created_by",
        "fo_tour_constraints.updated_by",
        "fo_tours.created_by",
        "fo_tours.updated_by",
        "hub_award_assignments.created_by",
        "hub_award_assignments.updated_by",
        "hub_award_assignments.vid",
        "hub_awards.created_by",
        "hub_awards.updated_by",
        "hub_division_settings.updated_by",
        "hub_notification_preferences.vid (key)",
        "hub_notifications.vid",
        "hub_personal_tokens.created_by",
        "hub_personal_tokens.updated_by",
        "hub_personal_tokens.vid",
        "hub_user_grants.created_by",
        "hub_user_grants.updated_by",
        "hub_user_grants.vid",
        "hub_user_preferences.vid (key)",
        "hub_user_staff_positions.vid (key)",
        "hub_user_tokens.vid (key)",
        "hub_users.vid (key)",
    ];

    private static int Line(JsonElement response, string key) =>
        response.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString() == key)
            .Sum(line => line.GetProperty("count").GetInt32());

    private static async Task<JsonElement> EraseAsync(HttpClient client, int vid, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{vid}", UriKind.Relative), null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>What the person has that only they have: access, preferences, a signal, a notification — and no longer the role.</summary>
    private async Task SeedAboutThePersonAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        database.UserGrants.Add(new UserGrant
        {
            Vid = PersonVid,
            Kind = GrantKind.Permission,
            Value = SampleModule.ViewPermission,
            Department = Department.ED,
            Effect = GrantEffect.Grant,
            Reason = "erasure test",
        });
        database.NotificationPreferences.Add(new NotificationPreference { Vid = PersonVid, Type = NotificationTypes.ContactThreadReplied, Enabled = false });
        database.UserPreferences.Add(new UserPreference { Vid = PersonVid, Key = SampleModule.OrderPreference, ValueJson = "\"tour\"", UpdatedAt = clock.UtcNow });
        database.AwardSignals.Add(new AwardSignal
        {
            SourceModule = SampleModule.ModuleKey,
            SourceId = $"erasure:{Guid.NewGuid():N}",
            Vid = PersonVid,
            Reason = "erasure test",
            CreatedAt = clock.UtcNow,
        });
        database.Notifications.Add(new Notification
        {
            Type = NotificationTypes.ContactThreadReplied,
            Vid = PersonVid,
            Address = "erasure-person@example.org",
            Locale = "en",
            CreatedAt = clock.UtcNow,
        });

        var user = await database.Users.SingleAsync(row => row.Vid == PersonVid, cancellationToken);
        user.IsSuperadmin = false;
        user.SecurityStamp = SuperadminService.NewStamp();

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private static async Task<long> CreateItemAsync(HttpClient client, string title, int stakeholder, CancellationToken cancellationToken)
    {
        using var created = await client.PostAsJsonAsync(
            SampleModule.ItemsPattern,
            new
            {
                title,
                visibility = nameof(Visibility.Department),
                ownerDepartments = new[] { nameof(Department.ED) },
                stakeholderVid = stakeholder,
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt64();
    }

    private static async Task<long> SubmitAsync(HttpClient client, string subject, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            ContactsEndpoints.Pattern,
            new { department = nameof(Department.SOD), subject, body = "Something to ask." },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ContactSubmittedDto>(cancellationToken))!.Id;
    }

    private static async Task ReplyAsync(HttpClient client, long id, string body, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync($"{ContactsEndpoints.Pattern}/{id}/replies", new { body }, cancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"The reply answered {(int)response.StatusCode}.");
    }

    private async Task SeedUserAsync(int vid, bool superadmin, CancellationToken cancellationToken)
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

        user.FirstName = "Erasure";
        user.LastName = "Test";
        user.IsSuperadmin = superadmin;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
