using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Every instant the API sends says that it is UTC.
/// <para>It did not, and the consequence was invisible to every test that existed: MariaDB's
/// <c>datetime</c> carries no zone, Pomelo returned <see cref="DateTimeKind.Unspecified"/>, and
/// <c>System.Text.Json</c> wrote <c>2026-09-07T19:35:00</c> with no <c>Z</c>. A browser reads a bare
/// date-time as **local**, so `new Date(...)` moved it by the offset and every screen in the hub
/// showed every instant **two hours early** through a European summer — lists, audit, calendar,
/// published dates, contact messages.</para>
/// <para>⚠️ Nothing caught it because nothing looked at the **string on the wire**: the values in
/// the database were right, the C# round-tripped fine, and the SPA formatted correctly what it was
/// given. Found in G12 by a person who read a date on a screen and knew what time it had been.</para>
/// <para>So the assertion here is deliberately about the **text**, not about a parsed
/// <see cref="DateTime"/>: parsing is what hid the defect. <c>GetDateTime()</c> on a value with no
/// zone happily gives you the right numbers.</para>
/// <para>⚠️ And it asserts on a **read**, not on the answer to the create. The first version of this
/// test looked at the body the POST returned and passed with the fix taken out: that response
/// serialises the entity still sitting in the change tracker, whose kind the interceptor had just
/// set. The defect lives on the way **out of the database**, so the test has to go and fetch the
/// row.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class InstantsAreUtcOnTheWireTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // Its own range, because the suite shares one database and a VID is a row (handoff, "Igiene").
    private const int SuperadminVid = 710001;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AnInstantIsWrittenWithItsZoneOrABrowserReadsItAsLocalTime()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedSuperadminAsync(token);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, SuperadminVid, token);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(LinksEndpoints.Pattern, UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                ownerDepartment = nameof(Department.WD),
                visibility = nameof(Visibility.Public),
                title = new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Ora", ["en"] = "Now" },
                url = "https://example.invalid/utc",
                description = (Dictionary<string, string>?)null,
                category = (string?)null,
                sort = 0,
                isActive = true,
                rowVersion = "0001-01-01T00:00:00",
            }),
        };

        request.Headers.Add("X-Requested-With", "hub");

        using var created = await client.SendAsync(request, token);
        created.EnsureSuccessStatusCode();

        var id = JsonSerializer
            .Deserialize<JsonElement>(await created.Content.ReadAsStringAsync(token))
            .GetProperty("id")
            .GetInt64();

        // A second request, so the instants come back from MariaDB rather than from the change
        // tracker. This is the whole difference between a test that catches it and one that does not.
        using var fetched = await client.GetAsync(
            new Uri($"{LinksEndpoints.Pattern}/{id}", UriKind.Relative),
            token);

        fetched.EnsureSuccessStatusCode();

        var body = await fetched.Content.ReadAsStringAsync(token);
        var row = JsonSerializer.Deserialize<JsonElement>(body);

        foreach (var field in new[] { "createdAt", "updatedAt" })
        {
            var written = row.GetProperty(field).GetString();

            Assert.NotNull(written);

            // The whole test. Without the `Z` the same characters mean a different instant to a
            // browser, and the hub is read in a browser.
            Assert.EndsWith("Z", written, StringComparison.Ordinal);

            // And it is really the instant the row carries, not merely a string with a Z on it.
            var parsed = DateTimeOffset.Parse(written, CultureInfo.InvariantCulture);
            Assert.Equal(TimeSpan.Zero, parsed.Offset);
            Assert.True(
                (DateTimeOffset.UtcNow - parsed).Duration() < TimeSpan.FromMinutes(5),
                $"{field} is {parsed:O}, which is not around now");
        }
    }

    private async Task SeedSuperadminAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == SuperadminVid, cancellationToken);
        if (user is null)
        {
            user = new HubUser { Vid = SuperadminVid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsSuperadmin = true;
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
    }
}
