using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Aircraft;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The skeleton of the tours (M2, T5), through the real host with the division file of this repository:
/// the grants of the flight operations department arrive from <c>positionGrants</c> once, an advisor writes
/// the aircraft data and not the settings, a coordinator changes the settings, and the aircraft data refuses
/// a type the hub does not know.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class FlightOpsSkeletonTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // The range the tours module owns in the shared database (design M2 section 13).
    private const int CoordinatorVid = 780051;
    private const int AdvisorVid = 780052;
    private const int EventsCoordinatorVid = 780053;

    private HubWebApplicationFactory _factory = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        var token = TestContext.Current.CancellationToken;

        await SeedUserAsync(CoordinatorVid, "IT-FOC", token);
        await SeedUserAsync(AdvisorVid, "IT-FOA1", token);
        await SeedUserAsync(EventsCoordinatorVid, "IT-EC", token);
        await SeedAircraftTypesAsync(token);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task TheGrantsOfTheBaseDepartmentArriveFromTheDivisionFileOnce()
    {
        var token = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // A second start of the application, as a restart would be: nothing is added.
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PositionGrantSeeder>().SeedAsync(token));

            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var seeded = await database.UserGrants.AsNoTracking()
                .Where(grant => grant.Reason == "division.json" && grant.Value.StartsWith("Tours."))
                .Select(grant => grant.Value)
                .ToListAsync(token);

            Assert.Equal(TourPermissions.All.Count, seeded.Count);
            Assert.Equal(seeded.Count, seeded.Distinct().Count());
        }

        // And they reach the coordinator's session: the section is in the menu the server composes.
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var me = await coordinator.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Contains(
            me.GetProperty("permissions").EnumerateArray(),
            permission => permission.GetProperty("name").GetString() == TourPermissions.ManageSettings);
    }

    [Fact]
    public async Task AnAdvisorWritesAProfileAndNotTheSettingsWhichTheCoordinatorChanges()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(AdvisorVid, token);
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var events = await SignedInAsync(EventsCoordinatorVid, token);

        await RemoveProfilesAsync(token);

        using (var created = await advisor.PostAsJsonAsync(AircraftEndpoints.ProfilesPattern, Profile("a320"), token))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("A320", (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("icaoType").GetString());
        }

        // One profile per type, and a type the hub knows.
        using (var twice = await advisor.PostAsJsonAsync(AircraftEndpoints.ProfilesPattern, Profile("A320"), token))
        {
            await AssertRefusedAsync(twice, "icaoType", "flightops:errors.profileExists", token);
        }

        using (var unknown = await advisor.PostAsJsonAsync(AircraftEndpoints.ProfilesPattern, Profile("ZZZ9"), token))
        {
            await AssertRefusedAsync(unknown, "icaoType", "errors.aircraft.unknownType", token);
        }

        // Somebody of another department holds nothing of the tours.
        using (var outside = await events.PostAsJsonAsync(AircraftEndpoints.ProfilesPattern, Profile("A20N"), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, outside.StatusCode);
        }

        // The settings: an advisor may not read them, the coordinator changes one and reads it back.
        var settingsUri = new Uri(ModuleSettingsEndpoints.Pattern.Replace("{key}", FlightOpsModule.ModuleKey, StringComparison.Ordinal), UriKind.Relative);

        using (var refused = await advisor.GetAsync(settingsUri, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var settings = await coordinator.GetFromJsonAsync<JsonElement>(settingsUri, token);
        var changed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settings.GetRawText())!;
        changed["durationFixedMinutes"] = JsonSerializer.SerializeToElement(25);
        changed["northSouthLevelCountries"] = JsonSerializer.SerializeToElement(new[] { "XX" });

        using (var saved = await coordinator.PutAsJsonAsync(settingsUri, changed, token))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        var reread = await coordinator.GetFromJsonAsync<JsonElement>(settingsUri, token);
        Assert.Equal(25, reread.GetProperty("durationFixedMinutes").GetInt32());
        Assert.Equal(["XX"], reread.GetProperty("northSouthLevelCountries").EnumerateArray().Select(code => code.GetString()));

        // A value out of range is refused on its field, and nothing is saved.
        changed["retentionMonthsLong"] = JsonSerializer.SerializeToElement(3);
        using (var invalid = await coordinator.PutAsJsonAsync(settingsUri, changed, token))
        {
            await AssertRefusedAsync(invalid, "retentionMonthsLong", "flightops:errors.retentionLongShorter", token);
        }

        using (var advisorWrite = await advisor.PutAsJsonAsync(settingsUri, changed, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, advisorWrite.StatusCode);
        }

        // Put back as a fresh installation has them, for whoever reads them next.
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().DivisionSettings
            .Where(row => row.Key == ModuleSettingsStore.SettingsKey(FlightOpsModule.ModuleKey))
            .ExecuteDeleteAsync(token);
    }

    [Fact]
    public async Task AGroupKeepsItsTypesOnceInUpperCaseAndRefusesATypeNobodyKnows()
    {
        var token = TestContext.Current.CancellationToken;
        using var advisor = await SignedInAsync(AdvisorVid, token);

        var name = $"fo-test-group-{Guid.NewGuid():N}";
        using (var created = await advisor.PostAsJsonAsync(
            AircraftEndpoints.GroupsPattern,
            Group(name, ["a20n", "A320", "A320"]),
            token))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var body = await created.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal(["A20N", "A320"], body.GetProperty("icaoTypes").EnumerateArray().Select(type => type.GetString()));
            Assert.Equal("FOD", body.GetProperty("ownerDepartment").GetString());
        }

        using (var unknown = await advisor.PostAsJsonAsync(AircraftEndpoints.GroupsPattern, Group(name, ["A320", "ZZZ9"]), token))
        {
            await AssertRefusedAsync(unknown, "icaoTypes", "errors.aircraft.unknownType", token);
        }

        // The field that offers the types while somebody types.
        var offered = await advisor.GetFromJsonAsync<JsonElement>($"{AircraftTypeEndpoints.Pattern}?q=A32", token);
        Assert.Equal("A320", offered.EnumerateArray().First().GetProperty("icaoCode").GetString());
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static object Profile(string type) =>
        new { ownerDepartment = "FOD", icaoType = type, cruiseTasKt = 450, note = "fo-test-profile" };

    private static object Group(string name, string[] types) =>
        new { ownerDepartment = "FOD", name = new Dictionary<string, string> { ["it"] = name, ["en"] = name }, icaoTypes = types };

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string field, string key, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(key, problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(error => error.GetString()));
    }

    private async Task RemoveProfilesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IvaoHub.Modules.FlightOps.Data.FlightOpsDbContext>();
        database.AircraftProfiles.RemoveRange(await database.AircraftProfiles.IgnoreQueryFilters()
            .Where(profile => profile.IcaoType == "A320" || profile.IcaoType == "A20N")
            .ToListAsync(cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAircraftTypesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var (code, model) in new[] { ("A320", "A320"), ("A20N", "A320neo") })
        {
            if (!await database.IvaoAircraftTypes.AnyAsync(type => type.IcaoCode == code, cancellationToken))
            {
                database.IvaoAircraftTypes.Add(new IvaoAircraftType { IcaoCode = code, Model = model, Manufacturer = "Airbus", SyncedAt = clock.UtcNow });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<HttpClient> SignedInAsync(int vid, CancellationToken cancellationToken)
    {
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "hub");
        await _factory.SignInAsync(client, vid, cancellationToken);
        return client;
    }

    private async Task SeedUserAsync(int vid, string position, CancellationToken cancellationToken)
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
        user.LastName = "Tours";
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, cancellationToken))
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
