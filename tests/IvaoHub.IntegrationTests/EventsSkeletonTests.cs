using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The skeleton of the events (M4, E2), through the real host with the division file of this repository: the grants of the
/// events department arrive from <c>positionGrants</c> once and reach its people, whoever holds <c>Events.ManageSettings</c>
/// changes the settings and reads them back and nobody else does, a preset is for a kind of the calendar, and no page may be
/// called like the section.
/// <para>⚠️ The people of the events department are seeded **without an address**: the tests of the contacts assert who
/// receives a message to a department and seed a coordinator of the events with one, and the notification service leaves
/// out a member with no address — so these receive nothing and change no count of theirs (<c>CONTRIBUTING.md</c>, "Tests").
/// Everybody else holds what they hold by a grant to their VID.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsSkeletonTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the events module owns in the shared database (CONTRIBUTING.md); E2 has 761001–761019.
    private const int CoordinatorVid = 761001;
    private const int AssistantVid = 761002;
    private const int AdvisorVid = 761003;
    private const int ManagerVid = 761004;
    private const int ViewerVid = 761005;

    private const string GrantReason = "evt-test";

    private static readonly Uri SettingsUri = new(
        ModuleSettingsEndpoints.Pattern.Replace("{key}", EventsModule.ModuleKey, StringComparison.Ordinal),
        UriKind.Relative);

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-EC", token);
        await SeedUserAsync(AssistantVid, "IT-EAC", token);
        await SeedUserAsync(AdvisorVid, "IT-EA1", token);
        await SeedUserAsync(ManagerVid, position: null, token);
        await SeedUserAsync(ViewerVid, position: null, token);
        await GrantAsync(ManagerVid, EventsPermissions.ManageSettings, token);
        await GrantAsync(ViewerVid, EventsPermissions.View, token);
    }

    /// <summary>
    /// What the class seeded is taken back — the grants to its VIDs and the positions it gave them —, so that no class after it
    /// finds a staff of the events department it did not seed. The members themselves stay, as every class leaves its own: the hub
    /// never deletes a member by hand, their erasure is the core's.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        int[] vids = [CoordinatorVid, AssistantVid, AdvisorVid, ManagerVid, ViewerVid];

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            database.UserGrants.RemoveRange(await database.UserGrants
                .Where(grant => grant.Reason == GrantReason && grant.Vid.HasValue && vids.Contains(grant.Vid.Value))
                .ToListAsync());
            database.UserStaffPositions.RemoveRange(await database.UserStaffPositions
                .Where(position => vids.Contains(position.Vid))
                .ToListAsync());
            await database.SaveChangesAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TheGrantsOfTheEventsDepartmentArriveOnceAndReachItsPeople()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A second start of the application, as a restart would be: nothing is added.
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PositionGrantSeeder>().SeedAsync(token));

            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var seeded = await database.UserGrants.AsNoTracking()
                .Where(grant => grant.Reason == "division.json")
                .Select(grant => new { grant.Value, grant.PositionDepartment, grant.PositionFirTeam })
                .ToListAsync(token);
            var events = seeded.Where(grant => EventsPermissions.All.Any(permission => permission.Name == grant.Value)).ToList();

            // Once per permission of the events department, which holds all of them but validating a report of support (§6.2).
            Assert.Equal(
                EventsPermissions.All.Select(permission => permission.Name).Where(name => name != EventsPermissions.ReportsEdit).Order(StringComparer.Ordinal),
                events.Where(grant => grant.PositionDepartment == Department.ED).Select(grant => grant.Value).Order(StringComparer.Ordinal));

            // The grants to the team of a FIR wait for the first row of the ATC that says its FIR (E11a): not applied, and not
            // remembered either, so the start that brings that row applies them.
            Assert.DoesNotContain(events, grant => grant.PositionFirTeam);

            var remembered = await database.DivisionSettings.AsNoTracking()
                .Where(row => row.Key == PositionGrantSeeder.AppliedSettingKey)
                .Select(row => row.ValueJson)
                .SingleAsync(token);
            var division = scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value;
            var toTheTeam = division.PositionGrants
                .Where(seed => seed.FirTeam && seed.Permission.StartsWith($"{EventsPermissions.AtcArea}.", StringComparison.Ordinal))
                .ToList();

            Assert.Equal(2, toTheTeam.Count);
            Assert.All(toTheTeam, seed => Assert.DoesNotContain(PositionGrantSeeder.Fingerprint(seed), remembered, StringComparison.Ordinal));
        }

        // The coordinator and the assistant everything but validating a report of support; the advisor neither deletes nor
        // manages the settings — all of it on the events department.
        string[] advisors =
        [
            EventsPermissions.View, EventsPermissions.Edit, EventsPermissions.RoutesView, EventsPermissions.RoutesEdit,
            EventsPermissions.BookingsView, EventsPermissions.BookingsEdit, EventsPermissions.AtcView, EventsPermissions.AtcEdit,
            EventsPermissions.ReportsView,
        ];
        var heads = advisors.Append(EventsPermissions.Delete).Append(EventsPermissions.ManageSettings).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(heads, await EventsPermissionsOfAsync(CoordinatorVid, token));
        Assert.Equal(heads, await EventsPermissionsOfAsync(AssistantVid, token));
        Assert.Equal(advisors.Order(StringComparer.Ordinal), await EventsPermissionsOfAsync(AdvisorVid, token));

        // The section of the back office is there for whoever may follow its entry, and for nobody else.
        Assert.Contains("/staff/events/settings", await StaffEntriesOfAsync(CoordinatorVid, token));
        Assert.DoesNotContain("/staff/events/settings", await StaffEntriesOfAsync(AdvisorVid, token));
    }

    [Fact]
    public async Task WhoManagesTheSettingsChangesThemAndReadsThemBackAndNobodyElseDoes()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetSettingsAsync(token);

        try
        {
            using var manager = await SignedInAsync(ManagerVid, token);
            using var coordinator = await SignedInAsync(CoordinatorVid, token);

            // An installation that never saved reads the design's defaults, with no kind preset.
            var defaults = await manager.GetFromJsonAsync<JsonElement>(SettingsUri, token);
            Assert.Empty(defaults.GetProperty("kindPresets").EnumerateArray());
            Assert.Equal(10, defaults.GetProperty("bookingGapMinutes").GetInt32());
            Assert.Equal(24, defaults.GetProperty("pilotRetentionMonths").GetInt32());
            Assert.Equal(24, defaults.GetProperty("reminderLeadHours").GetInt32());

            // Two kinds a fresh calendar has since E1, spelled as it spells them.
            var changed = Settings(defaults);
            changed["kindPresets"] = JsonSerializer.SerializeToElement(new[]
            {
                Preset("rfe", publicSlots: true, privateSlots: false, wholeDivision: false),
                Preset("online-day", publicSlots: false, privateSlots: false, wholeDivision: true),
            });
            changed["bookingGapMinutes"] = JsonSerializer.SerializeToElement(15);
            changed["pilotRetentionMonths"] = JsonSerializer.SerializeToElement(12);
            changed["reminderLeadHours"] = JsonSerializer.SerializeToElement(48);

            using (var saved = await manager.PutAsJsonAsync(SettingsUri, changed, token))
            {
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            }

            var reread = await manager.GetFromJsonAsync<JsonElement>(SettingsUri, token);
            Assert.Equal(15, reread.GetProperty("bookingGapMinutes").GetInt32());
            Assert.Equal(12, reread.GetProperty("pilotRetentionMonths").GetInt32());
            Assert.Equal(48, reread.GetProperty("reminderLeadHours").GetInt32());

            var presets = reread.GetProperty("kindPresets").EnumerateArray().ToArray();
            Assert.Equal(["rfe", "online-day"], presets.Select(preset => preset.GetProperty("kind").GetString()));
            Assert.True(presets[0].GetProperty("publicSlots").GetBoolean());
            Assert.False(presets[0].GetProperty("wholeDivision").GetBoolean());
            Assert.True(presets[1].GetProperty("wholeDivision").GetBoolean());

            // The coordinator holds the same permission by the position, and reads the same.
            Assert.Equal(15, (await coordinator.GetFromJsonAsync<JsonElement>(SettingsUri, token)).GetProperty("bookingGapMinutes").GetInt32());

            // Every value out of its rules is refused on its field, a preset on the field of its row, and nothing is saved: a kind
            // the calendar does not have, one spelled otherwise than the calendar spells it — the database would take it, the
            // browser would not find it —, and the same kind twice.
            var invalid = Settings(reread);
            invalid["kindPresets"] = JsonSerializer.SerializeToElement(new[]
            {
                Preset("evt-test-no-such-kind", publicSlots: true, privateSlots: false, wholeDivision: false),
                Preset("RFE", publicSlots: true, privateSlots: false, wholeDivision: false),
                Preset("rfo", publicSlots: true, privateSlots: true, wholeDivision: false),
                Preset("rfo", publicSlots: true, privateSlots: false, wholeDivision: false),
            });
            invalid["bookingGapMinutes"] = JsonSerializer.SerializeToElement(-1);
            invalid["pilotRetentionMonths"] = JsonSerializer.SerializeToElement(0);
            invalid["reminderLeadHours"] = JsonSerializer.SerializeToElement(169);

            using (var refused = await manager.PutAsJsonAsync(SettingsUri, invalid, token))
            {
                Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
                var errors = (await refused.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("errors");

                Assert.Equal(
                    new Dictionary<string, string>
                    {
                        ["kindPresets[0].kind"] = "events:errors.calendarKindUnknown",
                        ["kindPresets[1].kind"] = "events:errors.calendarKindUnknown",
                        ["kindPresets[2].kind"] = "events:errors.kindTwice",
                        ["kindPresets[3].kind"] = "events:errors.kindTwice",
                        ["bookingGapMinutes"] = "errors.number.range",
                        ["pilotRetentionMonths"] = "errors.number.range",
                        ["reminderLeadHours"] = "errors.number.range",
                    },
                    errors.EnumerateObject().ToDictionary(field => field.Name, field => field.Value[0].GetString()!));
            }

            Assert.Equal(15, (await manager.GetFromJsonAsync<JsonElement>(SettingsUri, token)).GetProperty("bookingGapMinutes").GetInt32());

            // Reading the events is not managing them, nor is being an advisor of the department.
            using var viewer = await SignedInAsync(ViewerVid, token);
            using var advisor = await SignedInAsync(AdvisorVid, token);

            foreach (var client in new[] { viewer, advisor })
            {
                using (var read = await client.GetAsync(SettingsUri, token))
                {
                    Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
                }

                using (var write = await client.PutAsJsonAsync(SettingsUri, changed, token))
                {
                    Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
                }
            }
        }
        finally
        {
            // Put back as a fresh installation has them, for whoever reads them next.
            await ForgetSettingsAsync(token);
        }
    }

    [Fact]
    public async Task NoPageMayBeCalledEvents()
    {
        var token = TestContext.Current.CancellationToken;

        // The pages of the events live under /events (design M4 §0.4): a page with that address would never be reached.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var described = await coordinator.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/address?kind=Page&department=ED&slug={EventsModule.ModuleKey}",
            token);

        Assert.Equal(nameof(ContentAddressState.Reserved), described.GetProperty("state").GetString());

        // Among the modules the bootstrap names, for whoever asks.
        using var anonymous = _factory.CreateApiClient();
        var me = await anonymous.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Contains(me.GetProperty("modules").EnumerateArray(), module => module.GetProperty("key").GetString() == EventsModule.ModuleKey);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------------

    private static object Preset(string kind, bool publicSlots, bool privateSlots, bool wholeDivision) => new
    {
        kind,
        publicSlots,
        privateSlots,
        hasRoster = false,
        wholeDivision,
        inPerson = false,
    };

    /// <summary>The permissions of the events a member holds on the events department, as their session says.</summary>
    private async Task<IEnumerable<string>> EventsPermissionsOfAsync(int vid, CancellationToken cancellationToken)
    {
        using var client = await SignedInAsync(vid, cancellationToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken);

        return me.GetProperty("permissions").EnumerateArray()
            .Where(permission => permission.GetProperty("department").GetString() == nameof(Department.ED))
            .Select(permission => permission.GetProperty("name").GetString()!)
            .Where(name => EventsPermissions.All.Any(permission => permission.Name == name))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The addresses of the back office entries of the events a member is offered.</summary>
    private async Task<IEnumerable<string>> StaffEntriesOfAsync(int vid, CancellationToken cancellationToken)
    {
        using var client = await SignedInAsync(vid, cancellationToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me", cancellationToken);

        return me.GetProperty("navigation").GetProperty("staff").EnumerateArray()
            .Where(entry => entry.TryGetProperty("module", out var module) && module.GetString() == EventsModule.ModuleKey)
            .Select(entry => entry.GetProperty("path").GetString()!)
            .ToList();
    }

    private static Dictionary<string, JsonElement> Settings(JsonElement settings) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settings.GetRawText())!;

    private async Task ForgetSettingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var row = await database.DivisionSettings
            .FirstOrDefaultAsync(setting => setting.Key == ModuleSettingsStore.SettingsKey(EventsModule.ModuleKey), cancellationToken);

        if (row is not null)
        {
            database.DivisionSettings.Remove(row);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    /// <summary>A member of the staff, with a position of the events department or none — and never an address.</summary>
    private async Task SeedUserAsync(int vid, string? position, CancellationToken cancellationToken)
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

        if (position is not null && !await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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

    /// <summary>A permission on the events department, to one VID, once.</summary>
    private async Task GrantAsync(int vid, string permission, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (await database.UserGrants.AnyAsync(grant => grant.Vid == vid && grant.Value == permission && grant.Reason == GrantReason, cancellationToken))
        {
            return;
        }

        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = permission,
            Department = Department.ED,
            Effect = GrantEffect.Grant,
            Reason = GrantReason,
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
