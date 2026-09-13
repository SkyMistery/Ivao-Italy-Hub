using System.Net;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The frame of an interactive block, over the wire (12 September 2026,
/// <c>decisions/2026-09-12-il-blocco-interattivo.md</c>).
///
/// <para>What is asserted here is what the design rests on rather than what it draws. That the
/// document comes back with a policy **of its own** — which is the whole reason it is an endpoint
/// and not a <c>srcdoc</c>; that <c>X-Frame-Options: DENY</c>, which every other response of this
/// application carries, is gone from this one, or our own page could not frame it; and that a
/// **draft** is refused to somebody who may not edit that row, because the draft of a page is the
/// one thing here that is not already public.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EmbedFrameTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // ⚠️ A range of its own, and not 640001: that VID is the **superadmin** of
    // `DataBlockEndToEndTests`, and the integration tests share one database. In CI that class ran
    // first, so the "web coordinator" here was already a superadmin and every assertion about who may
    // see a draft passed for the wrong reason — including with a position that does not exist
    // (`IT-WC`, where the web coordinator is `IT-WM`). Run on its own, the class failed three tests
    // out of five, which is what found it (12 September 2026).
    private const int WebCoordinatorVid = 740001;
    private const int OtherDepartmentVid = 740002;

    private const string Source = "<svg viewBox=\"0 0 10 10\"><title>A circuit</title></svg>";

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task APublishedFrameIsServedToAnybodyWithItsOwnPolicy()
    {
        var token = TestContext.Current.CancellationToken;
        var content = await SeedAsync(publish: true, cancellationToken: token);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/embed/{content}/1/b_live", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync(token);
        Assert.Contains(Source, html, StringComparison.Ordinal);
        Assert.Contains("window.HUB", html, StringComparison.Ordinal);

        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("sandbox allow-scripts", policy, StringComparison.Ordinal);

        // ⚠️ `frame-ancestors 'self'` and **no** X-Frame-Options: the page sends DENY on everything,
        // and a frame nobody may frame is a frame nobody can use.
        Assert.Contains("frame-ancestors 'self'", policy, StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("X-Frame-Options"));

        // A published version never changes, so the answer keeps for as long as a browser will keep
        // anything: this is what makes an endpoint cost nothing after the first reader.
        Assert.Contains("immutable", response.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADraftIsForWhoeverMayEditThatRowAndNobodyElse()
    {
        var token = TestContext.Current.CancellationToken;
        var content = await SeedAsync(publish: false, cancellationToken: token);

        // ⚠️ The row of this test is a **draft that has never been published**, which is the case the
        // first version of this endpoint got wrong: the global query filter hides an unpublished row
        // from everybody, its own author included, so reading a draft has to go past the filter and
        // lean on the handler alone. CI said so — the stranger was getting a 404 because nobody could
        // see the row at all, which would have meant an author cannot see the frame they are writing.

        // Somebody of another department: signed in, staff, and none of this is theirs.
        using var stranger = _factory.CreateApiClient();
        await _factory.SignInAsync(stranger, OtherDepartmentVid, token);
        using var refused = await stranger.GetAsync($"/embed/{content}/draft/b_live", token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // And the coordinator of the department the row belongs to, which is the editor's own case.
        using var editor = _factory.CreateApiClient();
        await _factory.SignInAsync(editor, WebCoordinatorVid, token);
        using var served = await editor.GetAsync($"/embed/{content}/draft/b_live", token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Contains(Source, await served.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        Assert.True(served.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task ABlockWithNoSourceIsNotAFrameAtAll()
    {
        var token = TestContext.Current.CancellationToken;
        var content = await SeedAsync(publish: true, cancellationToken: token);

        using var client = _factory.CreateClient();

        // A block that exists and carries nothing, and one that does not exist: both are nothing to
        // serve, and both answer the same way rather than with an empty document.
        using var quiet = await client.GetAsync($"/embed/{content}/1/b_quiet", token);
        using var absent = await client.GetAsync($"/embed/{content}/1/b_nowhere", token);

        Assert.Equal(HttpStatusCode.NotFound, quiet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
    }

    [Fact]
    public async Task TheGuidelinesAreForWhoeverMayAddOne()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var anonymous = _factory.CreateClient();
        using var refused = await anonymous.GetAsync(EmbedEndpoints.GuidelinesPattern, token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, WebCoordinatorVid, token);
        using var served = await client.GetAsync(EmbedEndpoints.GuidelinesPattern, token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        var markdown = await served.Content.ReadAsStringAsync(token);

        // The shell is quoted inside the guidelines rather than described by them, which is the only
        // way a document about a contract stays true to it.
        Assert.Contains("HUB.t({ en:", markdown, StringComparison.Ordinal);
        Assert.Contains("<!doctype html>", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLocalPreviewIsADownloadWithTheShellSafelyInsideIt()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);

        using var anonymous = _factory.CreateClient();
        using var refused = await anonymous.GetAsync(EmbedEndpoints.PreviewPattern, token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        using var client = _factory.CreateApiClient();
        await _factory.SignInAsync(client, WebCoordinatorVid, token);
        using var served = await client.GetAsync(EmbedEndpoints.PreviewPattern, token);

        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        // A download and never a page of this site: whoever opens it does so from a disk, which is
        // the only place running a pasted fragment is harmless.
        Assert.Equal("attachment", served.Content.Headers.ContentDisposition?.DispositionType);

        var html = await served.Content.ReadAsStringAsync(token);

        // The shell is inside it, and is the shell — `window.HUB` is the contract.
        Assert.Contains("window.HUB", html, StringComparison.Ordinal);

        // ⚠️ And inside it **safely**. The shell carries script elements of its own; written raw into
        // the preview's script, the first of its closing tags would end that script halfway through
        // and the preview would be a page of broken text. As a JSON string with `<` escaped there is
        // exactly one closing script tag in the whole file — the preview's own.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "</script>"));
        Assert.DoesNotContain("{{shellJson}}", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A page of the web department with two interactive blocks — one carrying a source, one not —
    /// written straight into the database: what is being tested is the endpoint, and going through
    /// the editor's own API to get there would be testing that instead.
    /// </summary>
    private async Task<long> SeedAsync(bool publish, CancellationToken cancellationToken)
    {
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", cancellationToken);
        await SeedUserAsync(OtherDepartmentVid, "IT-EC", cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var body = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sections"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "s1",
                    ["blocks"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "b_live",
                            ["type"] = "interactive",
                            ["source"] = Source,
                        },
                        new JsonObject { ["id"] = "b_quiet", ["type"] = "text" },
                    },
                },
            },
        };

        var content = new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = $"frame-{Guid.NewGuid():N}"[..20],
            OwnerDepartment = Department.WD,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Con un blocco interattivo"),
                new KeyValuePair<string, string>("en", "With an interactive block"),
            ]),
            BodyJson = body.ToJsonString(),
            Visibility = Visibility.Public,
            CreatedAt = clock.UtcNow,
        };

        database.Contents.Add(content);
        await database.SaveChangesAsync(cancellationToken);

        if (publish)
        {
            var version = new ContentVersion
            {
                ContentId = content.Id,
                Version = 1,
                Title = content.Title,
                BodyJson = content.BodyJson,
                PublishedAt = clock.UtcNow,
                PublishedBy = WebCoordinatorVid,
            };

            database.ContentVersions.Add(version);
            await database.SaveChangesAsync(cancellationToken);

            content.PublishedVersionId = version.Id;
            content.Status = PublishStatus.Published;
            await database.SaveChangesAsync(cancellationToken);
        }

        return content.Id;
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
        user.LastName = "User";
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(
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

        await database.SaveChangesAsync(cancellationToken);
    }
}
