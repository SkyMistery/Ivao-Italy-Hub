using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Preferences;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    // Signs in through the IVAO round trip, whose range this is, from two addresses of the documentation range of their own.
    private const int SignInVid = 648002;
    private const string HomeAddress = "192.0.2.95";
    private const string AwayAddress = "192.0.2.96";

    // The person's digits by chance, never the person: inside an instant's decimals, inside a hex stamp, inside a longer number.
    private const string CoincidenceJson = """{"UpdatedAt":"2026-10-18T18:37:06.7800951Z","SecurityStamp":"0A780095FF","Count":17800950}""";

    private HubWebApplicationFactory _factory = null!;
    private readonly List<long> _items = [];
    private readonly List<long> _threads = [];
    private readonly List<long> _audit = [];

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
            await database.AuditLog.Where(row => _audit.Contains(row.Id)).ExecuteDeleteAsync(token);
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
            _items.Add(await CreateItemAsync(person, "erasure-for-other", OtherVid, token));
            _items.Add(await CreateItemAsync(person, "erasure-gone", PersonVid, token));

            // And one about themselves that the module keeps as it is, the way it keeps a ban still in force.
            _items.Add(await CreateItemAsync(person, $"{SampleEraser.KeptPrefix}-ban", PersonVid, token));

            using var other = await SignedInAsync(OtherVid, token);
            var theirs = await SubmitAsync(other, $"Other's question {Guid.NewGuid():N}", token);
            _threads.Add(theirs);
            await ReplyAsync(person, theirs, "An answer from the person.", token);

            // And writes to the division themselves: a thread of their own, and the mail the department gets about it.
            _threads.Add(await SubmitAsync(person, $"Person's question {Guid.NewGuid():N}", token));
        }

        await SeedAboutThePersonAsync(token);
        var coincidence = await SeedCoincidenceAsync(token);
        var (forOther, gone, kept) = (_items[0], _items[1], _items[2]);
        var (theirThread, ownThread) = (_threads[0], _threads[1]);

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var preview = await superadmin.GetFromJsonAsync<JsonElement>($"{ErasureEndpoints.Pattern}/{PersonVid}", token);
        Assert.Equal("Erasure Test", preview.GetProperty("name").GetString());
        Assert.False(preview.GetProperty("isSuperadmin").GetBoolean());
        Assert.Equal(1, Line(preview, "erasure.lines.threads"));
        Assert.Equal(1, Line(preview, SampleEraser.ItemsKey));
        Assert.Equal(1, Line(preview, SampleEraser.KeptKey));

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
        var item = await sample.Items.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == forOther, token);
        Assert.Equal((pseudonym, pseudonym, OtherVid), (item.CreatedBy, item.UpdatedBy, item.StakeholderVid));

        // What the module keeps on purpose: as it was, VID and all.
        var ban = await sample.Items.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == kept, token);
        Assert.Equal((PersonVid, PersonVid), (ban.StakeholderVid, ban.CreatedBy));
        var thread = await database.ContactMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == theirThread, token);
        Assert.Equal((OtherVid, pseudonym), (thread.CreatedBy, thread.UpdatedBy));
        Assert.Equal(pseudonym, (await database.ContactReplies.SingleAsync(row => row.MessageId == theirThread, token)).AuthorVid);

        // The audit log: the VID nowhere but in the kept row's copies, the erased rows' copies empty, the rest of the history kept,
        // one row that says so.
        var audit = database.AuditLog.AsNoTracking();
        var keptKey = kept.ToString(CultureInfo.InvariantCulture);
        Assert.False(await audit.AnyAsync(row => row.Vid == PersonVid && !(row.Entity == "smp_items" && row.EntityId == keptKey), token));
        Assert.False(await audit.AnyAsync(row => row.Entity == "hub_users" && row.EntityId == text, token));
        // The number alone, wherever it stands: the shared database holds thousands of instants with seven decimals and hex
        // stamps, and its six digits turn up inside one of them by chance (about one six-digit number in sixty, measured on
        // the whole suite) — the row seeded below is one. Those rows are read past and kept as they were; each row that
        // names the person is printed.
        var mentions = await audit
            .Where(row => row.BeforeJson!.Contains(text) || row.AfterJson!.Contains(text))
            .Select(row => new { row.Id, row.Entity, row.EntityId, row.BeforeJson, row.AfterJson })
            .ToListAsync(token);
        Assert.Empty(mentions
            .Where(row => !(row.Entity == "smp_items" && row.EntityId == keptKey))
            .Where(row => NamesTheNumber(row.BeforeJson, text) || NamesTheNumber(row.AfterJson, text))
            .Select(row => $"{row.Entity} {row.EntityId}: {row.BeforeJson} {row.AfterJson}"));
        Assert.Contains(mentions, row => row.Id == coincidence && row.AfterJson == CoincidenceJson);
        Assert.False(await audit.AnyAsync(
            row => row.Entity == "smp_items" && row.EntityId == gone.ToString(CultureInfo.InvariantCulture)
                && (row.BeforeJson != null || row.AfterJson != null),
            token));
        Assert.True(await audit.AnyAsync(
            row => row.Entity == "smp_items" && row.EntityId == forOther.ToString(CultureInfo.InvariantCulture)
                && row.Vid == pseudonym && row.AfterJson != null && row.Ip == null,
            token));
        Assert.True(await audit.AnyAsync(
            row => row.Action == PersonalDataErasure.AuditAction && row.EntityId == pseudonym.ToString(CultureInfo.InvariantCulture)
                && row.Vid == SuperadminVid,
            token));

        // The ban kept in force: its copies stay on the VID like the row, as author and in their JSON, without an address. The
        // pseudonym next to the VID there would tie every other row of theirs back to them, so asking the log for the pseudonym
        // never finds the ban (issue #323, note 2026-10-21-le-tracce-della-persona-nell-audit).
        var alias = pseudonym.ToString(CultureInfo.InvariantCulture);
        var copies = await audit.Where(row => row.Entity == "smp_items" && row.EntityId == keptKey).ToListAsync(token);
        Assert.NotEmpty(copies);
        Assert.All(copies, row => Assert.Equal((PersonVid, (string?)null), (row.Vid, row.Ip)));
        Assert.Contains(copies, row => NamesTheNumber(row.AfterJson, text));
        Assert.DoesNotContain(copies, row => NamesTheNumber(row.BeforeJson, alias) || NamesTheNumber(row.AfterJson, alias));
        var byPseudonym = await audit
            .Where(row => row.Vid == pseudonym || row.BeforeJson!.Contains(alias) || row.AfterJson!.Contains(alias))
            .Select(row => new { row.Entity, row.EntityId, row.BeforeJson, row.AfterJson, row.Vid })
            .ToListAsync(token);
        Assert.NotEmpty(byPseudonym);
        Assert.DoesNotContain(byPseudonym, row => row.Entity == "smp_items" && row.EntityId == keptKey);
    }

    /// <summary>
    /// What the check of the audit above calls the person: the number alone — a JSON number, a key, a word of a text, an item of
    /// a list written as a string, a JSON document inside a string — and never its digits inside an instant, a hex stamp or a
    /// longer number.
    /// </summary>
    [Fact]
    public void TheAuditCheckFindsTheNumberAloneAndNotItsDigitsInsideAnother()
    {
        var text = PersonVid.ToString(CultureInfo.InvariantCulture);

        Assert.All(
            new[]
            {
                """{"createdBy":780095}""",
                """{"vid":"780095"}""",
                """{"780095":true}""",
                """{"note":"asked by 780095, twice"}""",
                """{"participantsJson":"[780001,780095]"}""",
                """{"DataJson":"{\"vid\":\"780095\"}"}""",
                """{"ValueJson":""780095""}""",
                """[780001,{"items":[780095]}]""",
                "not JSON, 780095",
            },
            json => Assert.True(NamesTheNumber(json, text), json));

        Assert.All(
            new string?[]
            {
                CoincidenceJson,
                """{"At":"2026-10-18T18:37:39.780095Z"}""",
                """{"Latitude":45.780095}""",
                """{"Id":-1780095,"Hash":"ab780095cd"}""",
                null,
            },
            json => Assert.False(NamesTheNumber(json, text), json));
    }

    /// <summary>Whether a JSON document holds this number alone, anywhere: see the test above.</summary>
    private static bool NamesTheNumber(string? json, string number)
    {
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            return Holds(JsonNode.Parse(json), number);
        }
        catch (JsonException)
        {
            return Alone(json, number);
        }

        static bool Holds(JsonNode? node, string number) => node switch
        {
            JsonObject item => item.Any(pair => Alone(pair.Key, number) || Holds(pair.Value, number)),
            JsonArray items => items.Any(child => Holds(child, number)),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => NamesTheNumber(value.GetValue<string>(), number),
            JsonValue value => value.ToJsonString() == number,
            _ => false,
        };

        // A word of its own: no letter, digit or decimal point against it, so 780095 is found in "by 780095," and not in
        // ".7800951Z", "ab780095cd" or "45.780095".
        static bool Alone(string text, string number) =>
            Regex.IsMatch(text, $"(?<![0-9A-Za-z.]){Regex.Escape(number)}(?![0-9A-Za-z])", RegexOptions.CultureInvariant);
    }

    /// <summary>An audit row that holds the person's digits only by chance, like the ones the other tests leave behind.</summary>
    private async Task<long> SeedCoincidenceAsync(CancellationToken cancellationToken)
    {
        Assert.Contains(PersonVid.ToString(CultureInfo.InvariantCulture), CoincidenceJson, StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var row = new AuditLogEntry
        {
            Vid = OtherVid,
            Action = "updated",
            Entity = "erasure_coincidences",
            EntityId = Guid.NewGuid().ToString("N"),
            AfterJson = CoincidenceJson,
            At = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow,
        };
        database.AuditLog.Add(row);
        await database.SaveChangesAsync(cancellationToken);

        _audit.Add(row.Id);
        return row.Id;
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
    /// A sign in writes about the member before they are anybody (issue #323): the callback runs with nobody signed in, so its
    /// audit rows — their user, and the grants it suspends — carry no VID and the member's address. The erasure takes the
    /// address out of every one of them: the user's, a grant's erased with the person, and a grant's revoked before.
    /// </summary>
    [Fact]
    public async Task NoAuditRowAboutThePersonKeepsTheAddressOfTheirSignIns()
    {
        var token = TestContext.Current.CancellationToken;
        var text = SignInVid.ToString(CultureInfo.InvariantCulture);
        long since;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            since = await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.MaxAsync(row => (long?)row.Id, token) ?? 0;
        }

        using var ivao = new FakeIvao(SignInVid);
        await using var host = ivao.Serve(_factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IStartupFilter, TestPeerStartupFilter>())));

        // From home, the first time: their user is born. Then two grants, which the next sign in, from elsewhere, suspends
        // because the member holds no position; one is revoked before the erasure, the other is erased with them.
        await SignInFromAsync(host, ivao, HomeAddress, token);
        var (kept, revoked) = (await SeedGrantAsync(SignInVid, token), await SeedGrantAsync(SignInVid, token));
        await SignInFromAsync(host, ivao, AwayAddress, token);
        await RevokeGrantAsync(revoked, token);

        // What the callback really writes: nobody is signed in yet, so no VID, and the member's address.
        string[] grants = [kept.ToString(CultureInfo.InvariantCulture), revoked.ToString(CultureInfo.InvariantCulture)];
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking().Where(row => row.Id > since);
            var users = await audit.Where(row => row.Entity == AuditRedaction.UsersTable && row.EntityId == text && row.Ip != null)
                .ToListAsync(token);
            Assert.All(users, row => Assert.Equal(0, row.Vid));
            Assert.Equal([HomeAddress, AwayAddress], users.Select(row => row.Ip).Distinct().Order(StringComparer.Ordinal));

            var suspended = await audit.Where(row => row.Entity == "hub_user_grants" && grants.Contains(row.EntityId) && row.Ip != null)
                .ToListAsync(token);
            Assert.Equal(2, suspended.Count);
            Assert.All(suspended, row => Assert.Equal((0, AwayAddress), (row.Vid, row.Ip)));
        }

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var alias = (await EraseAsync(superadmin, SignInVid, token)).GetProperty("pseudonym").GetInt32().ToString(CultureInfo.InvariantCulture);

        // Every address of theirs, wherever it was: these two belong to no other test. The rows stay, without it.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog.AsNoTracking();
            Assert.Empty(await audit
                .Where(row => row.Ip == HomeAddress || row.Ip == AwayAddress)
                .Select(row => $"{row.Id} {row.Action} {row.Entity} {row.EntityId} by {row.Vid}")
                .ToListAsync(token));
            Assert.True(await audit.AnyAsync(row => row.Id > since && row.Entity == AuditRedaction.UsersTable && row.EntityId == alias, token));
            Assert.Equal(2, await audit.CountAsync(
                row => row.Id > since && row.Entity == "hub_user_grants" && grants.Contains(row.EntityId) && row.Action == "updated",
                token));
        }
    }

    /// <summary>
    /// Every column the erasure gives the pseudonym to, read from the models of the real host (note §3). A new column of a
    /// person that follows the convention changes this list, and whoever adds it sees it here; one that does not follow it
    /// would be missed by the erasure, and this is the list a review compares a new table against.
    /// <para>The contexts are the core's and those of every module the erasure goes through, so a module's are read the day
    /// it is born (note <c>2026-09-29-la-persona-cancellata-nel-nucleo</c>, answer 2).</para>
    /// </summary>
    [Fact]
    public async Task TheColumnsThatNameAPersonAreTheOnesTheErasureKnows()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var modules = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<HubDbContext>(),

            // Every module of the hub, the way the erasure goes through them; the test module is not one of the hub's.
            .. modules.Enabled
                .Where(module => module.Key != SampleModule.ModuleKey)
                .SelectMany(module => module.DbContextTypes)
                .Select(type => (DbContext)scope.ServiceProvider.GetRequiredService(type)),
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
        "evt_activities.created_by",
        "evt_activities.updated_by",
        "evt_activity_bookings.vid",
        "evt_atc_availability.controller_vid",
        "evt_atc_availability_windows.controller_vid",
        "evt_atc_positions.created_by",
        "evt_atc_positions.updated_by",
        "evt_atc_shift_transfers.controller_vid",
        "evt_atc_shift_transfers.decided_by",
        "evt_atc_shift_transfers.to_vid",
        "evt_atc_shifts.attendance_by",
        "evt_atc_shifts.controller_vid",
        "evt_atc_shifts.covered_by_vid",
        "evt_atc_shifts.created_by",
        "evt_atc_shifts.updated_by",
        "evt_award_rules.created_by",
        "evt_award_rules.updated_by",
        "evt_bookings.booker_vid",
        "evt_bookings.unflown_excused_by",
        "evt_event_airports.created_by",
        "evt_event_airports.updated_by",
        "evt_events.cancelled_by",
        "evt_events.created_by",
        "evt_events.updated_by",
        "evt_questions.created_by",
        "evt_questions.updated_by",
        "evt_registrations.vid",
        "evt_report_awards.vid",
        "evt_report_items.vid",
        "evt_reports.decided_by",
        "evt_reports.vid",
        "evt_routes.created_by",
        "evt_routes.updated_by",
        "evt_slots.created_by",
        "evt_slots.updated_by",
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
        "trn_bans.created_by",
        "trn_bans.lifted_by",
        "trn_bans.updated_by",
        "trn_bans.vid",
        "trn_exams.candidate_vid",
        "trn_exams.created_by",
        "trn_exams.examiner_vid",
        "trn_exams.updated_by",
        "trn_sessions.created_by",
        "trn_sessions.updated_by",
        "trn_sheet_items.created_by",
        "trn_sheet_items.updated_by",
        "trn_slots.created_by",
        "trn_slots.updated_by",
        "trn_trainings.assigned_by",
        "trn_trainings.closed_by",
        "trn_trainings.created_by",
        "trn_trainings.decided_by",
        "trn_trainings.trainee_vid",
        "trn_trainings.trainer_vid",
        "trn_trainings.updated_by",
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

    /// <summary>A browser of its own, from this address, all the way through IVAO and back.</summary>
    private static async Task SignInFromAsync(
        WebApplicationFactory<Program> host,
        FakeIvao ivao,
        string address,
        CancellationToken cancellationToken)
    {
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        browser.DefaultRequestHeaders.Add(TestPeerStartupFilter.Header, address);
        await ivao.SignInAsync(browser, cancellationToken);
    }

    private async Task<long> SeedGrantAsync(int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var grant = new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = SampleModule.ViewPermission,
            Department = Department.ED,
            Effect = GrantEffect.Grant,
            Reason = "erasure test",
        };
        database.UserGrants.Add(grant);
        await database.SaveChangesAsync(cancellationToken);
        return grant.Id;
    }

    private async Task RevokeGrantAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Remove(await database.UserGrants.SingleAsync(row => row.Id == id, cancellationToken));
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
