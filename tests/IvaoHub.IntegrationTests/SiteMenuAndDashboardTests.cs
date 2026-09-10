using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The acceptance of G8, run rather than described: the menu of the site is a table, the pages an
/// installation is born with are seeded once and never rewritten, every department opens on a
/// dashboard of its own, and none of that leaks to a visitor (design M1 sections 8 and 14).
/// <para>Everything here goes over the wire against a real MariaDB, with the real cookie, the real
/// policies and the real interceptor, because the questions being asked — "may this coordinator
/// touch that?", "does the visitor see it?" — are questions about the whole stack.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SiteMenuAndDashboardTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // ⚠️ A range of its own, and it is the second time this suite has had to learn it: the whole
    // collection shares one database, so two classes on the same VID are **one row**. 660001-660004
    // looked free and belongs to `CalendarEndToEndTests`, whose 660002 is a super administrator —
    // so "a coordinator of another department" was somebody who may do anything, and two refusals
    // this class asserts stopped being refusals. In isolation every test still passed; only the
    // whole suite showed it, which is exactly what a shared database costs.
    private const int WebCoordinatorVid = 690001;
    private const int EventsCoordinatorVid = 690002;
    private const int DirectorVid = 690003;
    private const int GrantedVid = 690004;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    // ---- the menu (design M1 section 8.1) ------------------------------------------------------

    [Fact]
    public async Task MenuComposesEditorialAndModuleItems()
    {
        // The two halves of the navigation meet in `/api/me`, and they stay two kinds of thing: an
        // editorial row carries its words in every language, a module's entry carries a translation
        // key, because the server does not know which language the browser is drawing.
        var token = TestContext.Current.CancellationToken;

        var slug = Slug("composed");
        await SeedMenuItemAsync(slug, sort: 900, cancellationToken: token);

        using var anonymous = _factory.CreateApiClient();
        var bootstrap = await anonymous.GetFromJsonAsync<JsonElement>("/api/me", token);

        var entries = bootstrap.GetProperty("navigation").GetProperty("public").EnumerateArray().ToArray();

        var editorial = entries.Single(entry => entry.GetProperty("path").GetString() == $"/{slug}");
        Assert.Equal(JsonValueKind.Null, editorial.GetProperty("key").ValueKind);
        Assert.Equal("Menu entry", editorial.GetProperty("label").GetProperty("en").GetString());

        // And the module's, which is the other kind and has to survive the composition.
        var fromModule = entries.Single(entry => entry.GetProperty("path").GetString() == "/atc");
        Assert.Equal("nav.atc", fromModule.GetProperty("key").GetString());
        Assert.Equal(JsonValueKind.Null, fromModule.GetProperty("label").ValueKind);

        // Ordered by what the staff decided, so an entry moved in the back office moves on the site.
        var paths = entries.Select(entry => entry.GetProperty("path").GetString()).ToArray();
        Assert.True(
            Array.IndexOf(paths, "/") < Array.IndexOf(paths, $"/{slug}"),
            $"the seeded home should come before an entry sorted 900: {string.Join(", ", paths)}");
    }

    [Fact]
    public async Task AnEditorialEntryHidesTheModuleEntryWithTheSameAddress()
    {
        // Both halves may name one address, and only one of them can be drawn. The editorial row
        // wins because somebody chose its wording and its place; without this the day a division
        // puts /atc in its own menu is the day /atc appears twice.
        var token = TestContext.Current.CancellationToken;
        var id = await SeedMenuItemAsync("atc-shadow", sort: 950, path: "/atc", cancellationToken: token);

        try
        {
            using var anonymous = _factory.CreateApiClient();
            var bootstrap = await anonymous.GetFromJsonAsync<JsonElement>("/api/me", token);

            var atc = bootstrap.GetProperty("navigation").GetProperty("public").EnumerateArray()
                .Where(entry => entry.GetProperty("path").GetString() == "/atc")
                .ToArray();

            Assert.Single(atc);
            Assert.Equal(JsonValueKind.Null, atc[0].GetProperty("key").ValueKind);
        }
        finally
        {
            // ⚠️ Taken away again, and the `finally` is the point: the suite shares one database, so
            // a row left here would hide the module's entry from every class that looks at `/api/me`
            // afterwards. It did — `ModuleRegistryComposesNavAndExclusions` failed in the whole suite
            // and passed on its own, which is the shape of a test that pollutes rather than one that
            // fails. A row a test writes to change an answer is a row that test takes back.
            await RemoveMenuItemAsync(id, token);
        }
    }

    [Fact]
    public async Task AMenuEntryIsOnlyOfferedToWhoeverMaySeeIt()
    {
        // The menu is `IVisible` like everything else, so the rule that hides a members' page is the
        // one that hides the entry leading to it. There is no line about menus in it.
        var token = TestContext.Current.CancellationToken;
        var slug = Slug("members-only");
        await SeedMenuItemAsync(slug, sort: 960, visibility: Visibility.Members, cancellationToken: token);

        using var anonymous = _factory.CreateApiClient();
        var visitor = await anonymous.GetFromJsonAsync<JsonElement>("/api/me", token);

        Assert.DoesNotContain(
            visitor.GetProperty("navigation").GetProperty("public").EnumerateArray(),
            entry => entry.GetProperty("path").GetString() == $"/{slug}");

        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);
        using var member = _factory.CreateApiClient();
        await _factory.SignInAsync(member, EventsCoordinatorVid, token);

        var signedIn = await member.GetFromJsonAsync<JsonElement>("/api/me", token);

        Assert.Contains(
            signedIn.GetProperty("navigation").GetProperty("public").EnumerateArray(),
            entry => entry.GetProperty("path").GetString() == $"/{slug}");
    }

    [Fact]
    public async Task MenuIsOwnedByTheWebDepartment()
    {
        // The whole authorisation of the resource, and it is not written anywhere: every row belongs
        // to the department that owns the site, so the handler that exists says no to everybody else
        // without a rule of its own (design M1 section 8.1).
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        using var created = await SendAsync(web, HttpMethod.Post, MenuEndpoints.Pattern, Entry(), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var row = await created.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.Equal(nameof(Department.WD), row.GetProperty("ownerDepartment").GetString());

        // A coordinator of another department holds `Menu.Edit` — on their own department, where no
        // menu row exists — so writing one of these is refused.
        using var events = _factory.CreateApiClient();
        await _factory.SignInAsync(events, EventsCoordinatorVid, token);

        using var refused = await SendAsync(events, HttpMethod.Post, MenuEndpoints.Pattern, Entry(), token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var id = row.GetProperty("id").GetInt64();
        using var refusedEdit = await SendAsync(
            events,
            HttpMethod.Put,
            $"{MenuEndpoints.Pattern}/{id}",
            Entry(rowVersion: row.GetProperty("rowVersion").GetString()),
            token);
        Assert.Equal(HttpStatusCode.Forbidden, refusedEdit.StatusCode);

        // And their list is empty rather than somebody else's, which is the department filter of the
        // engine doing its job.
        var listed = await events.GetFromJsonAsync<JsonElement>(MenuEndpoints.Pattern, token);
        Assert.Equal(0, listed.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task AMenuEntryGoesNoDeeperThanOneLevel()
    {
        // Depth one is a rule of the design and therefore a refusal of the server, not a select that
        // happens to offer nothing: a payload naming a child as its parent is answered on the field.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);

        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        using var parent = await SendAsync(web, HttpMethod.Post, MenuEndpoints.Pattern, Entry(), token);
        var parentId = (await parent.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        using var child = await SendAsync(
            web,
            HttpMethod.Post,
            MenuEndpoints.Pattern,
            Entry(parentId: parentId),
            token);
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);

        var childId = (await child.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        using var grandchild = await SendAsync(
            web,
            HttpMethod.Post,
            MenuEndpoints.Pattern,
            Entry(parentId: childId),
            token);

        Assert.Equal(HttpStatusCode.BadRequest, grandchild.StatusCode);

        var problem = await grandchild.Content.ReadFromJsonAsync<JsonElement>(token);
        Assert.True(problem.GetProperty("errors").TryGetProperty("parentId", out _));
    }

    [Fact]
    public async Task AMenuEntryOnlyLeadsWhereTheSiteOwnsSomething()
    {
        // ⚠️ The closed set, decided by Carmine on 8 September 2026 running the demo: a menu entry
        // leads to a page of this site, a screen of the application, or a link of the library, and
        // to nothing else. The point is not the menu — it is that every address that leaves the site
        // lives in `cms_links`, so moving the forum is one row and the menu follows.
        //
        // The client offers exactly these three groups and refuses to hold anything else, but the
        // client is a convenience: what makes it a rule is here.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);

        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        // A screen of the application: no table knows about it, the validator does.
        using var screen = await SendAsync(web, HttpMethod.Post, MenuEndpoints.Pattern, Entry("/news"), token);
        Assert.Equal(HttpStatusCode.Created, screen.StatusCode);

        // A page that is still a draft, on purpose: writing the entry before publishing the page is
        // how a menu is actually built, and the entry can wait, switched off, until the page is out.
        var draft = Slug("still-a-draft");
        await SeedContentAsync(draft, Visibility.Public, PublishStatus.Draft, token);

        using var page = await SendAsync(web, HttpMethod.Post, MenuEndpoints.Pattern, Entry($"/{draft}"), token);
        Assert.Equal(HttpStatusCode.Created, page.StatusCode);

        // A link of the library, and one of **another** department: the site's menu may point at it,
        // which is why the question is asked past the query filter.
        var elsewhere = $"https://example.test/{Guid.NewGuid():N}";
        await SeedLinkAsync(elsewhere, Department.ED, isActive: true, cancellationToken: token);

        using var link = await SendAsync(web, HttpMethod.Post, MenuEndpoints.Pattern, Entry(elsewhere), token);
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);

        // And the three refusals. A link that has been retired is no longer an address of this site.
        var retired = $"https://example.test/{Guid.NewGuid():N}";
        await SeedLinkAsync(retired, Department.WD, isActive: false, cancellationToken: token);

        await RefusedAsync(web, Entry(retired), token);

        // An address nobody wrote down anywhere, which is the case this rule exists for.
        await RefusedAsync(web, Entry("https://somewhere.else.test/forum"), token);

        // And a path of this site that is not a page: a slug that does not exist is a menu entry
        // leading to the not found screen, which used to be accepted in silence.
        await RefusedAsync(web, Entry($"/{Slug("never-written")}"), token);
    }

    /// <summary>
    /// The one entry allowed to lead nowhere: the heading of a footer column, decided by Carmine on
    /// 10 September 2026 when he asked for a footer in columns.
    /// <para>Both halves of the rule are asserted, and the second is the one that matters: an entry
    /// with no address is accepted at the top of the <b>footer</b> and refused everywhere else. A
    /// child with none would be a line nobody can click; a heading in the bar at the top would be an
    /// entry that does nothing when pressed.</para>
    /// </summary>
    [Fact]
    public async Task OnlyATopLevelFooterEntryMayLeadNowhere()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, position: "IT-WM", cancellationToken: token);

        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        // A heading, and the mark it may carry: the server keeps the name and never resolves it,
        // because which names exist is a list that lives only in the client.
        using var heading = await SendAsync(
            web,
            HttpMethod.Post,
            MenuEndpoints.Pattern,
            Entry(path: string.Empty, scope: MenuScope.Footer, icon: "globe"),
            token);

        Assert.Equal(HttpStatusCode.Created, heading.StatusCode);

        // Read as JSON rather than as the DTO: the payload spells its enums as words, which is the
        // contract, and a plain deserialiser here would need a converter of its own to read them.
        using var created = JsonDocument.Parse(await heading.Content.ReadAsStringAsync(token));
        var id = created.RootElement.GetProperty("id").GetInt64();

        Assert.Equal("globe", created.RootElement.GetProperty("icon").GetString());

        // A child of it with no address: refused, because nobody could click it.
        await RefusedForItsPathAsync(
            web,
            Entry(path: string.Empty, parentId: id, scope: MenuScope.Footer),
            token);

        // And a headline in the bar at the top: refused, because pressing it would do nothing.
        await RefusedForItsPathAsync(web, Entry(path: string.Empty), token);
    }

    // ---- the pages an installation is born with (design M1 section 8.2) -------------------------

    [Fact]
    public async Task SystemPagesSeedAppliesOnceAndKeepsStaffEdits()
    {
        // The seed is a first start and not a synchronisation: a page the staff has since rewritten
        // must survive the next one, or every deploy would undo their work.
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var home = await CrudSource.BackOffice<ContentEntry>(database)
            .FirstOrDefaultAsync(row => row.Kind == ContentKind.Page && row.Slug == "home", token);

        Assert.NotNull(home);
        Assert.Equal(PublishStatus.Published, home.Status);
        Assert.NotNull(home.PublishedVersionId);

        // Born from a template, which is what lets the editor say later that the template has moved
        // on (design M1 section 9.1).
        Assert.NotNull(home.TemplateId);

        var edited = new Localized<string>(
        [
            new KeyValuePair<string, string>("it", "Scritta dallo staff"),
            new KeyValuePair<string, string>("en", "Written by the staff"),
        ]);

        home.Title = edited;
        await database.SaveChangesAsync(token);

        // The seeder runs again, exactly as the next start of the application would run it.
        await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(token);

        var after = await CrudSource.BackOffice<ContentEntry>(database)
            .Where(row => row.Kind == ContentKind.Page && row.Slug == "home")
            .ToListAsync(token);

        // One row, still, and still the one the staff wrote.
        var single = Assert.Single(after);
        Assert.Equal("Written by the staff", single.Title.Get("en"));
    }

    [Fact]
    public async Task ASeededPageIsReadableByAVisitorAndCarriesTheMenuThatLeadsToIt()
    {
        // The point of the whole phase, asked of the wire: a visitor who is nobody opens an address
        // of the site and reads what was published there, and the menu leads them to it.
        var token = TestContext.Current.CancellationToken;

        using var anonymous = _factory.CreateApiClient();

        var page = await anonymous.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Page)}/start",
            token);

        Assert.Equal("start", page.GetProperty("slug").GetString());
        Assert.False(string.IsNullOrWhiteSpace(page.GetProperty("title").GetProperty("en").GetString()));

        var bootstrap = await anonymous.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Contains(
            bootstrap.GetProperty("navigation").GetProperty("public").EnumerateArray(),
            entry => entry.GetProperty("path").GetString() == "/start");
    }

    // ---- the dashboard of a department (design M1 section 14) -----------------------------------

    [Fact]
    public async Task EveryDepartmentIsBornWithADashboard()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var dashboards = await CrudSource.BackOffice<ContentEntry>(database)
            .Where(row => row.Kind == ContentKind.Dashboard && !row.IsTemplate)
            .ToListAsync(token);

        foreach (var department in Enum.GetValues<Department>())
        {
            var dashboard = Assert.Single(
                dashboards.Where(row => row.OwnerDepartment == department),
                row => row.Slug == department.ToString().ToLowerInvariant());

            // Published, or the query filter would hide it from the department it belongs to; and
            // `Department`, or it would not be theirs.
            Assert.Equal(PublishStatus.Published, dashboard.Status);
            Assert.Equal(Visibility.Department, dashboard.Visibility);

            // The address of a dashboard is the department's own space and never a public one.
            Assert.Equal($"/staff/{department.ToString().ToLowerInvariant()}", dashboard.Url);
        }
    }

    [Fact]
    public async Task ADashboardIsNotPublic()
    {
        // A `Visibility.Department` row never leaves the department, whichever address is asked for
        // it. Three ways of asking, because it is the same rule each time and one of them is the one
        // somebody will actually try.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(EventsCoordinatorVid, position: "IT-EC", cancellationToken: token);

        var web = nameof(Department.WD).ToLowerInvariant();

        using var anonymous = _factory.CreateApiClient();

        using var asVisitor = await anonymous.GetAsync(
            new Uri($"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Dashboard)}/{web}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.NotFound, asVisitor.StatusCode);

        // The public page route serves pages, so the slug of a dashboard is not an address there.
        using var asPage = await anonymous.GetAsync(
            new Uri($"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Page)}/{web}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.NotFound, asPage.StatusCode);

        // And a member of staff of another department is told the same thing: the filter compares a
        // department, not a role.
        using var events = _factory.CreateApiClient();
        await _factory.SignInAsync(events, EventsCoordinatorVid, token);

        using var asOtherStaff = await events.GetAsync(
            new Uri($"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Dashboard)}/{web}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.NotFound, asOtherStaff.StatusCode);

        // Their own, on the other hand, is theirs to read.
        using var ownDashboard = await events.GetAsync(
            new Uri(
                $"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Dashboard)}/{nameof(Department.ED).ToLowerInvariant()}",
                UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.OK, ownDashboard.StatusCode);
    }

    [Fact]
    public async Task AGrantReachesTheListAndTheDepartmentRowsOfThatDepartment()
    {
        // The correction this phase owed (note 2026-09-06-autorizzare-su-un-pezzo-di-un-altro-
        // dipartimento). A grant used to give the permission and nothing else: the list came back
        // empty and the `Visibility.Department` rows stayed hidden, because the claims a filter
        // reads were written from the staff positions alone. The test of F8 asked for one row by
        // identifier and never for a list, which is why nobody had noticed.
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(DirectorVid, position: "IT-DIR", cancellationToken: token);
        await SeedUserAsync(GrantedVid, position: "IT-EC", cancellationToken: token);

        using var granted = _factory.CreateApiClient();
        await _factory.SignInAsync(granted, GrantedVid, token);

        var web = nameof(Department.WD).ToLowerInvariant();

        // Before: not their department, so neither its list nor its rows.
        var before = await granted.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}?filter[ownerDepartment]={nameof(Department.WD)}",
            token);
        Assert.Equal(0, before.GetProperty("total").GetInt32());

        using var beforeDashboard = await granted.GetAsync(
            new Uri($"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Dashboard)}/{web}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.NotFound, beforeDashboard.StatusCode);

        using var director = _factory.CreateApiClient();
        await _factory.SignInAsync(director, DirectorVid, token);

        using var granting = await SendAsync(
            director,
            HttpMethod.Post,
            GrantEndpoints.Pattern,
            new
            {
                vid = GrantedVid,
                kind = nameof(GrantKind.Permission),
                effect = nameof(GrantEffect.Grant),
                value = "Content.View",
                department = nameof(Department.WD),
                reason = "helping the web team",
            },
            token);
        Assert.Equal(HttpStatusCode.Created, granting.StatusCode);

        // The grant regenerated their security stamp, so the cookie they were carrying is refused
        // and the next sign in carries the department they were authorised on.
        await _factory.SignInAsync(granted, GrantedVid, token);

        var after = await granted.GetFromJsonAsync<JsonElement>(
            $"{ContentEndpoints.Pattern}?filter[ownerDepartment]={nameof(Department.WD)}",
            token);
        Assert.True(
            after.GetProperty("total").GetInt32() > 0,
            "a grant on a department has to make its list readable, not only one row of it");

        // ⚠️ And the part worth saying out loud: they now see the `Visibility.Department` rows of
        // that department, the dashboard included. That is the reading which makes the visibility
        // decided for a dashboard true, and it is what whoever hands a grant out is agreeing to.
        using var afterDashboard = await granted.GetAsync(
            new Uri($"{ContentEndpoints.Pattern}/public/{nameof(ContentKind.Dashboard)}/{web}", UriKind.Relative),
            token);
        Assert.Equal(HttpStatusCode.OK, afterDashboard.StatusCode);

        var bootstrap = await granted.GetFromJsonAsync<JsonElement>("/api/me", token);
        Assert.Contains(
            bootstrap.GetProperty("user").GetProperty("departments").EnumerateArray(),
            department => department.GetString() == nameof(Department.WD));
    }

    // ---- what a crawler is told (design M1 section 8.4) ----------------------------------------

    [Fact]
    public async Task SitemapListsOnlyPublishedAndVisible()
    {
        var token = TestContext.Current.CancellationToken;

        var hidden = Slug("draft");
        await SeedContentAsync(hidden, Visibility.Public, PublishStatus.Draft, token);

        var members = Slug("members");
        await SeedContentAsync(members, Visibility.Members, PublishStatus.Published, token);

        using var anonymous = _factory.CreateApiClient();
        var sitemap = await anonymous.GetStringAsync(new Uri(SeoEndpoints.SitemapPattern, UriKind.Relative), token);

        // What is in it: the front page and the pages an installation was born with.
        Assert.Contains("<loc>https://it.ivao.aero/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://it.ivao.aero/start</loc>", sitemap, StringComparison.Ordinal);

        // What is not: a draft, a members' page, and every dashboard.
        Assert.DoesNotContain($"/{hidden}<", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain($"/{members}<", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("/staff/", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RobotsIsServedByTheServerAndNotBySinglePageApplication()
    {
        // Both files are excluded from the fallback. Without that the application would answer them
        // with index.html and a crawler would read a page of JavaScript where it asked for text.
        var token = TestContext.Current.CancellationToken;

        using var anonymous = _factory.CreateApiClient();

        using var robots = await anonymous.GetAsync(new Uri(SeoEndpoints.RobotsPattern, UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, robots.StatusCode);
        Assert.Equal("text/plain", robots.Content.Headers.ContentType?.MediaType);

        var text = await robots.Content.ReadAsStringAsync(token);
        Assert.Contains("Disallow: /staff", text, StringComparison.Ordinal);
        Assert.Contains($"Sitemap: https://it.ivao.aero{SeoEndpoints.SitemapPattern}", text, StringComparison.Ordinal);

        using var sitemap = await anonymous.GetAsync(new Uri(SeoEndpoints.SitemapPattern, UriKind.Relative), token);
        Assert.Equal("application/xml", sitemap.Content.Headers.ContentType?.MediaType);
    }

    // ---- helpers -------------------------------------------------------------------------------

    /// <summary>A slug of this run, so a class that does not clean up after itself can be run twice.</summary>
    private static string Slug(string what) => $"g8-{what}-{Guid.NewGuid():N}"[..24];

    /// <summary>
    /// A payload for a new entry, pointing by default at a <b>screen of the application</b>.
    /// <para>⚠️ It used to point at an invented slug, and since 8 September 2026 that is refused:
    /// a menu entry may only lead where the site owns something
    /// (<see cref="MenuItemWriteDtoValidator.Screens"/> and the two tables). These tests ask about
    /// ownership and depth, so where the entry leads is not their question — but it still has to be
    /// somewhere real.</para>
    /// </summary>
    private static object Entry(
        string path = "/calendar",
        long? parentId = null,
        string? rowVersion = null,
        MenuScope scope = MenuScope.Public,
        string? icon = null) => new
        {
            scope = scope.ToString(),
            parentId,
            sort = 500,
            label = new Dictionary<string, string> { ["it"] = "Voce", ["en"] = "Entry" },
            path,
            icon,
            visibility = nameof(Visibility.Public),
            isActive = true,
            rowVersion = rowVersion ?? "0001-01-01T00:00:00",
        };

    private async Task<long> SeedMenuItemAsync(
        string slug,
        int sort,
        string? path = null,
        Visibility visibility = Visibility.Public,
        CancellationToken cancellationToken = default)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var item = new MenuItem
        {
            Scope = MenuScope.Public,
            Sort = sort,
            Label = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Voce di menu"),
                new KeyValuePair<string, string>("en", "Menu entry"),
            ]),
            Path = path ?? $"/{slug}",
            Visibility = visibility,
            IsActive = true,
        };

        database.MenuItems.Add(item);
        await database.SaveChangesAsync(cancellationToken);

        return item.Id;
    }

    private async Task RemoveMenuItemAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var item = await database.MenuItems.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (item is not null)
        {
            database.MenuItems.Remove(item);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Posts an entry that must be refused, and says on which field and with which key.</summary>
    private static async Task RefusedAsync(HttpClient client, object entry, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, MenuEndpoints.Pattern, entry, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(
            "errors.menu.pathNotAllowed",
            problem.GetProperty("errors").GetProperty("path")[0].GetString());
    }

    /// <summary>
    /// Posts an entry that must be refused **on its address**, without pinning which rule said so.
    /// An empty path fails the first rule and a wrong one fails the last, and both are the same
    /// answer to whoever is writing: not there.
    /// </summary>
    private static async Task RefusedForItsPathAsync(
        HttpClient client,
        object entry,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(client, HttpMethod.Post, MenuEndpoints.Pattern, entry, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty("path").EnumerateArray());
    }

    private async Task SeedLinkAsync(
        string url,
        Department owner,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.Links.Add(new Link
        {
            OwnerDepartment = owner,
            Visibility = Visibility.Public,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Collegamento"),
                new KeyValuePair<string, string>("en", "Link"),
            ]),
            Url = url,
            IsActive = isActive,
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedContentAsync(
        string slug,
        Visibility visibility,
        PublishStatus status,
        CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.Contents.Add(new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = slug,
            OwnerDepartment = Department.WD,
            Visibility = visibility,
            Status = status,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Prova"),
                new KeyValuePair<string, string>("en", "Test"),
            ]),
            BodyJson = """{"schemaVersion":1,"sections":[]}""",
        });

        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedUserAsync(int vid, string? position = null, CancellationToken cancellationToken = default)
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
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (position is not null
            && !await database.UserStaffPositions.AnyAsync(
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
