using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The most bookings a member makes in one event (issue #314; note 2026-11-01-il-tetto-delle-prenotazioni), through the real host: five by
/// default, for the whole division, checked by the three verbs of a pilot — a public slot, a whole rotation, a private slot with its linked
/// departure — under the pilot's lock. A rotation is one booking, whatever its legs and however they were booked; a private arrival and its
/// linked departure are two. A member with the limits of whoever books and does not fly (E13b) has the lower of the two.
/// <para><b>The same instant</b> (design §10.1): a transaction of the test holds what another request of the pilot would hold — their lock
/// and a fifth booking, not committed —, the request for a sixth waits for it in the database (<c>INNODB_TRX</c>), and reads it once the test
/// commits. Without the lock it would not wait, and both would pass.</para>
/// <para>⚠️ The people are 761340 and 761341 (the range of this fix, 761340–761359): a pilot, and a pilot with three bookings not flown. The
/// events, the slots and the record are written straight into the database; the bookings go through the verbs.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EventsBookingCapTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int PilotVid = 761340;
    private const int RestrictedVid = 761341;

    private static readonly int[] People = [PilotVid, RestrictedVid];

    /// <summary>The airport of the events, one away from it, and an aircraft type: of no country the network has.</summary>
    private const string Home = "XEU1";
    private const string Away = "XEU2";
    private const string Type = "XE4U";

    private static readonly string[] Airports = [Home, Away];

    private const string SlugStem = "evt-test-fix314";

    private static readonly string[] Locales = ["it", "en"];

    private static int _callsigns;

    private HubWebApplicationFactory _factory = null!;
    private string? _settingsBefore;

    public async ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, useIvaoFixtures: true);
        var token = TestContext.Current.CancellationToken;

        await ForgetAsync(token);
        await SeedPersonAsync(PilotVid, token);
        await SeedPersonAsync(RestrictedVid, token);
        await SeedReferenceAsync(token);

        // The defaults, written out so that a class before this one leaves nothing of its own: five in an event, ten minutes apart.
        _settingsBefore = await WriteSettingsAsync("""{"maxBookingsPerEvent":5,"bookingGapMinutes":10}""", token);
    }

    public async ValueTask DisposeAsync()
    {
        await ForgetAsync(CancellationToken.None);
        await WriteSettingsAsync(_settingsBefore, CancellationToken.None);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            await database.IvaoAirports.Where(airport => Airports.Contains(airport.Icao)).ExecuteDeleteAsync();
            await database.IvaoAircraftTypes.Where(type => type.IcaoCode == Type).ExecuteDeleteAsync();
        }

        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Five public slots go, the sixth is refused on the slot with the key that says why; a withdrawal makes room for one, at the boundary
    /// again, and no more. Another event is counted apart.
    /// </summary>
    [Fact]
    public async Task APilotBooksAtMostTheDivisionsNumberOfPublicSlotsInAnEvent()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(3));
        var id = await EventRowAsync("public", starts, token);
        var slots = await SinglesAsync(id, starts, count: 7, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        var booked = new List<long>();
        foreach (var slot in slots.Take(5))
        {
            booked.Add(await CreatedAsync(await BookAsync(pilot, slot, token), token));
        }

        Assert.Equal([BookingCap.Key], (await RefusedAsync(await BookAsync(pilot, slots[5], token), token))[PilotBookings.SlotField]);

        await NoContentAsync(await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{booked[0]}", token), token);
        await CreatedAsync(await BookAsync(pilot, slots[5], token), token);
        Assert.Equal([BookingCap.Key], (await RefusedAsync(await BookAsync(pilot, slots[6], token), token))[PilotBookings.SlotField]);
        Assert.Equal(5, (await BookingsAsync(id, token)).Count);

        // Another event of the same evening has its own five.
        var other = await EventRowAsync("public-other", starts, token);
        await CreatedAsync(await BookAsync(pilot, (await SinglesAsync(other, starts, count: 1, token))[0], token), token);
    }

    /// <summary>
    /// A rotation is one booking, whatever its legs (Carmine's answer on #314): with four slots booked, the three legs of a rotation go and
    /// make five; a second rotation is refused leg by leg with the key, nothing of it booked. A leg of the rotation the pilot holds, withdrawn
    /// and booked again alone, does not count again; the legs of a rotation booked one at a time count once.
    /// </summary>
    [Fact]
    public async Task ARotationIsOneBookingWhateverItsLegs()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(3));
        var id = await EventRowAsync("rotation", starts, token);
        var singles = await SinglesAsync(id, starts, count: 4, token);
        var first = await RotationAsync(id, "R1", starts.AddHours(4), legs: 3, token);
        var second = await RotationAsync(id, "R2", starts.AddHours(7), legs: 2, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        var booked = new List<long>();
        foreach (var slot in singles)
        {
            booked.Add(await CreatedAsync(await BookAsync(pilot, slot, token), token));
        }

        var whole = await OkAsync(await BookRotationAsync(pilot, first[0], token), token);
        Assert.Equal(3, whole.GetProperty("booked").GetArrayLength());
        Assert.Equal(0, whole.GetProperty("notBooked").GetArrayLength());

        // Five bookings in seven rows: the next rotation is refused leg by leg.
        var refused = await OkAsync(await BookRotationAsync(pilot, second[0], token), token);
        Assert.Equal(0, refused.GetProperty("booked").GetArrayLength());
        Assert.Equal(
            [BookingCap.Key, BookingCap.Key],
            refused.GetProperty("notBooked").EnumerateArray().Select(leg => leg.GetProperty("reason").GetString()));

        // A leg of R1, withdrawn and booked again alone: R1 is still one of the five.
        var middle = whole.GetProperty("booked")[1].GetProperty("id").GetInt64();
        await NoContentAsync(await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{middle}", token), token);
        await CreatedAsync(await BookAsync(pilot, first[1], token), token);

        // One slot withdrawn: the legs of R2 booked one at a time are one booking, the fifth.
        await NoContentAsync(await pilot.DeleteAsync($"{BookingEndpoints.MinePattern}/{booked[0]}", token), token);
        await CreatedAsync(await BookAsync(pilot, second[0], token), token);
        await CreatedAsync(await BookAsync(pilot, second[1], token), token);
        Assert.Equal([BookingCap.Key], (await RefusedAsync(await BookAsync(pilot, singles[0], token), token))[PilotBookings.SlotField]);
        Assert.Equal(8, (await BookingsAsync(id, token)).Count);
    }

    /// <summary>
    /// A private slot is one booking, and an arrival's linked departure a second: with four booked, an arrival with its departure is refused
    /// on the departure, and neither is booked; the arrival alone goes, the fifth, and another private slot is refused.
    /// </summary>
    [Fact]
    public async Task APrivateArrivalAndItsLinkedDepartureAreTwoBookings()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(3));
        var id = await EventRowAsync("private", starts, token, privateSlots: true);
        var singles = await SinglesAsync(id, starts, count: 4, token);
        var arrival = await PrivateSlotAsync(id, isArrival: true, starts.AddHours(5), token);
        var departure = await PrivateSlotAsync(id, isArrival: false, starts.AddHours(6), token);
        var later = await PrivateSlotAsync(id, isArrival: true, starts.AddHours(8), token);

        using var pilot = await SignedInAsync(PilotVid, token);
        foreach (var slot in singles)
        {
            await CreatedAsync(await BookAsync(pilot, slot, token), token);
        }

        var both = Arrival(arrival, "XEU901", starts.AddHours(4)) with
        {
            Departure = new PrivateDepartureRequest(departure, "XEU902", Away, starts.AddHours(7)),
        };
        var refused = await RefusedAsync(await BookPrivateAsync(pilot, both, token), token);
        Assert.Equal([BookingCap.Key], refused[PilotBookings.DepartureSlotField]);
        Assert.False(refused.ContainsKey(PilotBookings.SlotField));
        Assert.Equal(4, (await BookingsAsync(id, token)).Count);

        await CreatedAsync(await BookPrivateAsync(pilot, Arrival(arrival, "XEU901", starts.AddHours(4)), token), token);
        Assert.Equal(
            [BookingCap.Key],
            (await RefusedAsync(await BookPrivateAsync(pilot, Arrival(later, "XEU903", starts.AddHours(7).AddMinutes(30)), token), token))[PilotBookings.SlotField]);
        Assert.Equal(5, (await BookingsAsync(id, token)).Count);
    }

    /// <summary>
    /// Two bookings of the same pilot in the same instant, the fourth and fifth already theirs: the test holds the pilot's lock — the row of
    /// their first booking — with a fifth written and not committed; the request for a sixth waits for it, reads it, and is refused.
    /// </summary>
    [Fact]
    public async Task TwoBookingsOfThePilotInTheSameInstantDoNotPassTheMostTogether()
    {
        var token = TestContext.Current.CancellationToken;
        var starts = Hour(DateTime.UtcNow.AddDays(3));
        var id = await EventRowAsync("same-instant", starts, token);
        var slots = await SinglesAsync(id, starts, count: 6, token);

        using var pilot = await SignedInAsync(PilotVid, token);
        var booked = new List<long>();
        foreach (var slot in slots.Take(4))
        {
            booked.Add(await CreatedAsync(await BookAsync(pilot, slot, token), token));
        }

        await using (var held = await HeldAsync(id, token))
        {
            await held.LockAsync($"SELECT id AS `Value` FROM evt_bookings WHERE id = {booked[0]} FOR UPDATE", token);
            await held.BookAsync(slots[4], PilotVid, token);

            var racing = BookAsync(pilot, slots[5], token);
            await WaitForALockWaitAsync(token);
            await held.CommitAsync(token);

            Assert.Equal([BookingCap.Key], (await RefusedAsync(await racing, token))[PilotBookings.SlotField]);
        }

        Assert.Equal(slots.Take(5).Order(), (await BookingsAsync(id, token)).Select(booking => booking.SlotId).Order());
    }

    /// <summary>
    /// A pilot with the limits of whoever books and does not fly (E13b) has the lower of the two: in an event whose own limits are ten, the
    /// division's five stop them; in one with the settings' limits — two in the event —, their own stop them at the third.
    /// </summary>
    [Fact]
    public async Task APilotWithTheLimitsHasTheLowerOfTheTwo()
    {
        var token = TestContext.Current.CancellationToken;
        var history = await EventRowAsync("restricted-history", Hour(DateTime.UtcNow.AddDays(-10)), token, checksDone: true);
        for (var index = 0; index < 3; index++)
        {
            await UnflownAsync(history, RestrictedVid, token);
        }

        var starts = Hour(DateTime.UtcNow.AddDays(3));
        using var pilot = await SignedInAsync(RestrictedVid, token);

        var generous = await EventRowAsync("restricted-generous", starts, token, restrictedMax: 10);
        var slots = await SinglesAsync(generous, starts, count: 6, token);
        foreach (var slot in slots.Take(5))
        {
            await CreatedAsync(await BookAsync(pilot, slot, token), token);
        }

        Assert.Equal([BookingCap.Key], (await RefusedAsync(await BookAsync(pilot, slots[5], token), token))[PilotBookings.SlotField]);

        // The settings' limits: one in each band of two hours, two in the event. Three slots in three bands.
        var strict = await EventRowAsync("restricted-strict", starts, token);
        var spread = await SinglesAsync(strict, starts, count: 3, token, apartMinutes: 150);
        await CreatedAsync(await BookAsync(pilot, spread[0], token), token);
        await CreatedAsync(await BookAsync(pilot, spread[1], token), token);
        Assert.Equal([BookingLimits.EventKey], (await RefusedAsync(await BookAsync(pilot, spread[2], token), token))[PilotBookings.SlotField]);
    }

    // ---- requests ------------------------------------------------------------------------------------------------------------------

    private static DateTime Hour(DateTime instant) =>
        DateTime.SpecifyKind(new DateTime(instant.Year, instant.Month, instant.Day, instant.Hour, 0, 0), DateTimeKind.Utc);

    private static string Slug(string name) => $"{SlugStem}-{name}";

    private static Localized<string> Text(string text) => new(Locales.ToDictionary(locale => locale, _ => text));

    private static string Callsign() => $"XEU{Interlocked.Increment(ref _callsigns) % 100000:00000}";

    private static PrivateBookingRequest Arrival(long slotId, string callsign, DateTime leaves) =>
        new(slotId, Type, callsign, Away, leaves, Departure: null);

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, long slotId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(BookingEndpoints.MinePattern, new BookingRequest(slotId, Type), cancellationToken);

    private static Task<HttpResponseMessage> BookRotationAsync(HttpClient client, long slotId, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{BookingEndpoints.MinePattern}/rotation", new BookingRequest(slotId, Type), cancellationToken);

    private static Task<HttpResponseMessage> BookPrivateAsync(HttpClient client, PrivateBookingRequest request, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync($"{BookingEndpoints.MinePattern}/private", request, cancellationToken);

    /// <summary>A booking made: its id — a private one's, the arrival's.</summary>
    private static async Task<long> CreatedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var made = await CreatedJsonAsync(response, cancellationToken);
        return (made.TryGetProperty("booking", out var booking) ? booking : made).GetProperty("id").GetInt64();
    }

    private static async Task<JsonElement> CreatedJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.Created,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        }
    }

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

    private static async Task NoContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            Assert.True(
                response.StatusCode == HttpStatusCode.NoContent,
                $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
    }

    /// <summary>The keys of a 400, field by field.</summary>
    private static async Task<Dictionary<string, string[]>> RefusedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.RequestMessage?.RequestUri}: {response.StatusCode}: {text}");

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").EnumerateObject()
                .ToDictionary(field => field.Name, field => field.Value.EnumerateArray().Select(key => key.GetString()!).ToArray());
        }
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

    // ---- rows and people -----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// An event of the class, published and seen, its bookings open, eight hours long; private slots when asked, its own most bookings of
    /// whoever has the limits — per band and per event — when asked, the check after it done when it is over.
    /// </summary>
    private async Task<long> EventRowAsync(
        string name,
        DateTime starts,
        CancellationToken cancellationToken,
        bool privateSlots = false,
        int? restrictedMax = null,
        bool checksDone = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var row = new Event
        {
            Slug = Slug(name),
            Kind = "rfe",
            PublicSlots = true,
            PrivateSlots = privateSlots,
            Organizer = EventOrganizer.Division,
            Title = Text(Slug(name)),
            Summary = Text(Slug(name)),
            StartsAtUtc = starts,
            EndsAtUtc = starts.AddHours(10),
            BookingOpensAtUtc = DateTime.UtcNow.AddDays(-30),
            Status = PublishStatus.Published,
            PublishedAt = DateTime.UtcNow.AddDays(-30),
            Visibility = Visibility.Public,
            AfterDoneAt = checksDone ? starts.AddHours(17) : null,
            RestrictedMaxPerWindow = restrictedMax,
            RestrictedMaxPerEvent = restrictedMax,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };

        database.Events.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    /// <summary>
    /// Public departures of no rotation from the airport of the event, half an hour each, from ten minutes past its start and
    /// <paramref name="apartMinutes"/> apart — more than the minutes between two bookings of a pilot.
    /// </summary>
    private async Task<long[]> SinglesAsync(long eventId, DateTime starts, int count, CancellationToken cancellationToken, int apartMinutes = 40)
    {
        var slots = Enumerable.Range(0, count)
            .Select(index => Public(eventId, starts.AddMinutes(10 + index * apartMinutes), rotation: null, leg: null))
            .ToList();
        return await SaveAsync(slots, cancellationToken);
    }

    /// <summary>The legs of a rotation, from <paramref name="first"/>, half an hour each and forty minutes apart.</summary>
    private async Task<long[]> RotationAsync(long eventId, string code, DateTime first, int legs, CancellationToken cancellationToken)
    {
        var slots = Enumerable.Range(0, legs)
            .Select(index => Public(eventId, first.AddMinutes(index * 40), code, index + 1))
            .ToList();
        return await SaveAsync(slots, cancellationToken);
    }

    /// <summary>A private slot at the airport of the event: an arrival's time is its on block, a departure's its off block.</summary>
    private async Task<long> PrivateSlotAsync(long eventId, bool isArrival, DateTime time, CancellationToken cancellationToken)
    {
        var slot = new EventSlot
        {
            EventId = eventId,
            Kind = SlotKind.Private,
            EventAirportIcao = Home,
            IsArrival = isArrival,
            DepartureIcao = isArrival ? null : Home,
            ArrivalIcao = isArrival ? Home : null,
            OffBlockUtc = isArrival ? null : time,
            OnBlockUtc = isArrival ? time : null,
            Generated = true,
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        };
        return (await SaveAsync([slot], cancellationToken))[0];
    }

    private static EventSlot Public(long eventId, DateTime offBlock, string? rotation, int? leg) => new()
    {
        EventId = eventId,
        Kind = SlotKind.Public,
        EventAirportIcao = Home,
        IsArrival = false,
        Callsign = Callsign(),
        AircraftTypes = [Type],
        DepartureIcao = Home,
        ArrivalIcao = Away,
        OffBlockUtc = offBlock,
        OnBlockUtc = offBlock.AddMinutes(30),
        RotationCode = rotation,
        RotationLeg = leg,
        OwnerDepartment = Department.ED,
        OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
    };

    private async Task<long[]> SaveAsync(List<EventSlot> slots, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        database.Slots.AddRange(slots);
        await database.SaveChangesAsync(cancellationToken);
        return [.. slots.Select(slot => slot.Id)];
    }

    /// <summary>A booking of a pilot in an event over, which the check after it found not flown: one more in their record.</summary>
    private async Task UnflownAsync(long eventId, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var row = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(candidate => candidate.Id == eventId, cancellationToken);

        var slot = Public(eventId, row.StartsAtUtc.AddMinutes(30), rotation: null, leg: null);
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);

        database.Bookings.Add(new EventBooking
        {
            EventId = eventId,
            SlotId = slot.Id,
            BookerVid = vid,
            AircraftIcao = Type,
            CreatedAt = row.StartsAtUtc.AddDays(-5),
            FlownCheckedAt = row.EndsAtUtc.AddHours(7),
            OwnerDepartment = Department.ED,
            OwnerDepartmentMask = DepartmentMask.Of(Department.ED),
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The bookings of an event as stored, read past every filter.</summary>
    private async Task<List<EventBooking>> BookingsAsync(long eventId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(booking => booking.EventId == eventId)
            .OrderBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A transaction of the test on the events, in the isolation the verbs use: what another request of the pilot holds while it runs.
    /// Nobody is signed in on it — the installation itself —, so the write guard leaves it alone.
    /// </summary>
    private async Task<HeldTransaction> HeldAsync(long eventId, CancellationToken cancellationToken)
    {
        var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        var parent = await database.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(row => row.Id == eventId, cancellationToken);
        var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new HeldTransaction(scope, database, transaction, parent);
    }

    /// <summary>Until a transaction waits for a lock: the request under test, behind the transaction the test holds.</summary>
    /// <remarks>InnoDB refreshes <c>INNODB_TRX</c> only when nobody has read it for 100 ms (CONTRIBUTING.md): read every 250 ms.</remarks>
    private async Task WaitForALockWaitAsync(CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(mariaDb.RootConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_TRX WHERE trx_state = 'LOCK WAIT'", connection);
        for (var tries = 0; tries < 40; tries++)
        {
            await Task.Delay(250, cancellationToken);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
            {
                return;
            }
        }

        Assert.Fail("The request never waited for the transaction the test holds.");
    }

    private async Task<string?> WriteSettingsAsync(string? json, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var key = ModuleSettingsStore.SettingsKey(EventsModule.ModuleKey);
        var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        var before = row?.ValueJson;

        if (json is null)
        {
            if (row is not null)
            {
                database.DivisionSettings.Remove(row);
            }
        }
        else
        {
            if (row is null)
            {
                row = new DivisionSetting { Key = key };
                database.DivisionSettings.Add(row);
            }

            row.ValueJson = json;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await database.SaveChangesAsync(cancellationToken);
        return before;
    }

    /// <summary>A member of the division and nothing else, signed in a moment ago, with no address.</summary>
    private async Task SeedPersonAsync(int vid, CancellationToken cancellationToken)
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
        user.LastName = "Cap";
        user.Email = null;
        user.Locale = "en";
        user.IsStaff = false;
        user.IsSuperadmin = false;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.LastLoginAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        await database.UserStaffPositions.Where(row => row.Vid == vid).ExecuteDeleteAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The airports and the aircraft type of the class, in the core's snapshots, where a private flight's are checked.</summary>
    private async Task SeedReferenceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        foreach (var icao in Airports)
        {
            if (!await database.IvaoAirports.AnyAsync(airport => airport.Icao == icao, cancellationToken))
            {
                database.IvaoAirports.Add(new IvaoAirport
                {
                    Icao = icao,
                    Name = $"evt-test {icao}",
                    CountryId = "XX",
                    Latitude = 41.8,
                    Longitude = 12.24,
                    SyncedAt = clock.UtcNow,
                });
            }
        }

        if (!await database.IvaoAircraftTypes.AnyAsync(type => type.IcaoCode == Type, cancellationToken))
        {
            database.IvaoAircraftTypes.Add(new IvaoAircraftType { IcaoCode = Type, Model = "evt-test", Manufacturer = "evt-test", SyncedAt = clock.UtcNow });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The events of this class with their rows, and the notifications of its people, whatever a stopped run left.</summary>
    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await EventsTestRows.ForgetAsync(_factory.Services, SlugStem, cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .Where(row => People.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// A transaction the test holds on the events, as a request of the pilot in flight would: their lock taken, a booking written and not
    /// committed. Disposed without a commit, it is rolled back.
    /// </summary>
    private sealed class HeldTransaction(
        AsyncServiceScope scope,
        EventsDbContext database,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Event parent) : IAsyncDisposable
    {
        /// <summary>A booking of <paramref name="vid"/> written in the transaction, in the event's care, as the verbs write one.</summary>
        public async Task BookAsync(long slotId, int vid, CancellationToken cancellationToken)
        {
            database.Bookings.Add(new EventBooking
            {
                EventId = parent.Id,
                SlotId = slotId,
                BookerVid = vid,
                AircraftIcao = Type,
                CreatedAt = DateTime.UtcNow,
                OwnerDepartment = parent.OwnerDepartment,
                OwnerDepartmentMask = parent.OwnerDepartmentMask,
            });
            await database.SaveChangesAsync(cancellationToken);
        }

        /// <summary>A row locked in the transaction, as the lock of a pilot is taken.</summary>
        public Task LockAsync(FormattableString sql, CancellationToken cancellationToken) =>
            database.Database.SqlQuery<long>(sql).ToListAsync(cancellationToken);

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await database.DisposeAsync();
            await scope.DisposeAsync();
        }
    }
}
