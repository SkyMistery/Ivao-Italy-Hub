using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
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
/// <c>Content.EmbedCode</c> on the server (#318, note 2026-09-12-il-blocco-interattivo §5): the code of an
/// interactive block is added or changed only by whoever holds it on the row, through the save the editor
/// uses and through "new from a template"; text around code somebody else wrote is anybody's to edit; and
/// only an interactive block carries code at all.
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class EmbedCodeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    // A range of its own: the integration tests share one database.
    private const int CoordinatorVid = 770101;
    private const int CoderVid = 770102;

    private const string Code = "<svg viewBox=\"0 0 10 10\"><title>A circuit</title></svg>";
    private const string OtherCode = "<svg viewBox=\"0 0 10 10\"><title>Another circuit</title></svg>";

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task CodeIsAddedOnlyByWhoeverMayEmbedIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, coder) = await PeopleAsync(token);

        // Content.Edit is not enough: the palette hiding the block was the only thing in the way.
        using var refused = await SendAsync(coordinator, HttpMethod.Post, ContentEndpoints.Pattern, Payload(Slug(), Body("Testo", Code), null), token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("errors.body.sourceNotPermitted", await FirstErrorAsync(refused, "body.sections[0].blocks[1].source", token));

        using var accepted = await SendAsync(coder, HttpMethod.Post, ContentEndpoints.Pattern, Payload(Slug(), Body("Testo", Code), null), token);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task TheTextAroundCodeSomebodyElseWroteIsAnybodysButTheCodeIsNot()
    {
        var token = TestContext.Current.CancellationToken;
        var (coordinator, coder) = await PeopleAsync(token);

        var slug = Slug();
        using var created = await SendAsync(coder, HttpMethod.Post, ContentEndpoints.Pattern, Payload(slug, Body("Testo", Code), null), token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(token)).GetProperty("id").GetInt64();

        // The text changes, the code stays as it was: nothing to ask.
        var current = await coordinator.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", token);
        using var text = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(slug, Body("Testo corretto", Code), current.GetProperty("rowVersion").GetString()),
            token);
        Assert.True(text.StatusCode == HttpStatusCode.OK, await text.Content.ReadAsStringAsync(token));

        // The code changes: refused, and the row keeps the code it had.
        current = await coordinator.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", token);
        using var code = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(slug, Body("Testo corretto", OtherCode), current.GetProperty("rowVersion").GetString()),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, code.StatusCode);
        Assert.Equal("errors.body.sourceNotPermitted", await FirstErrorAsync(code, "body.sections[0].blocks[1].source", token));

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var body = await database.Contents.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => row.BodyJson)
            .SingleAsync(token);
        Assert.Contains("A circuit", body, StringComparison.Ordinal);

        // And taking the block away asks nothing either.
        current = await coordinator.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", token);
        using var removed = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(slug, Body("Testo corretto", code: null), current.GetProperty("rowVersion").GetString()),
            token);
        Assert.True(removed.StatusCode == HttpStatusCode.OK, await removed.Content.ReadAsStringAsync(token));
    }

    [Fact]
    public async Task OnlyAnInteractiveBlockCarriesCode()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, coder) = await PeopleAsync(token);

        var body = Body("Testo", code: null);
        body["sections"]![0]!["blocks"]![0]!["source"] = Code;

        using var refused = await SendAsync(coder, HttpMethod.Post, ContentEndpoints.Pattern, Payload(Slug(), body, null), token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("errors.body.sourceNotInteractive", await FirstErrorAsync(refused, "body.sections[0].blocks[0].source", token));
    }

    [Fact]
    public async Task ATemplatesCodeCopiedUnchangedAsksNothingAndChangingItDoes()
    {
        // Carmine, 21 October 2026, on #341: the code of a template was checked when the template was
        // saved, so a page born from it carries it unchanged without Content.EmbedCode; adding code, or
        // changing what came from the template, asks for it on the new page as on any other.
        var token = TestContext.Current.CancellationToken;
        var (coordinator, _) = await PeopleAsync(token);
        var template = await TemplateAsync(token);

        using var made = await FromTemplateAsync(coordinator, template, token);
        Assert.True(made.StatusCode == HttpStatusCode.Created, await made.Content.ReadAsStringAsync(token));
        var page = await made.Content.ReadFromJsonAsync<JsonElement>(token);
        var id = page.GetProperty("id").GetInt64();

        var current = await coordinator.GetFromJsonAsync<JsonElement>($"{ContentEndpoints.Pattern}/{id}", token);
        var body = JsonNode.Parse(current.GetProperty("body").GetRawText())!;
        body["sections"]![0]!["blocks"]![1]!["source"] = OtherCode;

        using var changed = await SendAsync(
            coordinator,
            HttpMethod.Put,
            $"{ContentEndpoints.Pattern}/{id}",
            Payload(current.GetProperty("slug").GetString()!, body, current.GetProperty("rowVersion").GetString()),
            token);
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal("errors.body.sourceNotPermitted", await FirstErrorAsync(changed, "body.sections[0].blocks[1].source", token));
    }

    private static string Slug() => $"c{Guid.NewGuid():N}"[..14];

    private async Task<(HttpClient Coordinator, HttpClient Coder)> PeopleAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(CoordinatorVid, "IT-EC", cancellationToken);
        await SeedUserAsync(CoderVid, "IT-EC", cancellationToken);
        await SeedGrantAsync(CoderVid, CorePermissions.ContentEmbedCode, Department.ED, cancellationToken);

        var coordinator = _factory.CreateApiClient();
        await _factory.SignInAsync(coordinator, CoordinatorVid, cancellationToken);

        var coder = _factory.CreateApiClient();
        await _factory.SignInAsync(coder, CoderVid, cancellationToken);

        return (coordinator, coder);
    }

    /// <summary>A news item of the events department: a heading, and an interactive block when there is code.</summary>
    private static JsonObject Body(string heading, string? code)
    {
        var blocks = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "b_heading",
                ["type"] = "heading",
                ["version"] = 1,
                ["props"] = new JsonObject
                {
                    ["level"] = 2,
                    ["text"] = new JsonObject { ["it"] = heading, ["en"] = heading },
                },
            },
        };

        if (code is not null)
        {
            blocks.Add(new JsonObject
            {
                ["id"] = "b_live",
                ["type"] = CoreBlocks.Interactive,
                ["version"] = 1,
                ["source"] = code,
                ["props"] = new JsonObject(),
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sections"] = new JsonArray { new JsonObject { ["id"] = "s_main", ["layout"] = "stacked", ["blocks"] = blocks } },
        };
    }

    private static object Payload(string slug, JsonNode body, string? rowVersion) => new
    {
        kind = nameof(ContentKind.News),
        slug,
        ownerDepartment = nameof(Department.ED),
        visibility = nameof(Visibility.Public),
        isTemplate = false,
        title = new Dictionary<string, string> { ["it"] = "Con un'animazione", ["en"] = "With an animation" },
        summary = (Dictionary<string, string>?)null,
        seo = (Dictionary<string, object>?)null,
        body,
        schemaVersion = 1,
        rowVersion = rowVersion ?? "0001-01-01T00:00:00",
    };

    /// <summary>A news template of the events department carrying code, written straight in.</summary>
    private async Task<long> TemplateAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var template = new ContentEntry
        {
            Kind = ContentKind.News,
            Slug = Slug(),
            OwnerDepartment = Department.ED,
            Visibility = Visibility.Staff,
            Status = PublishStatus.Draft,
            IsTemplate = true,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Modello con un'animazione"),
                new KeyValuePair<string, string>("en", "Template with an animation"),
            ]),
            BodyJson = Body("Testo", Code).ToJsonString(),
            CreatedAt = clock.UtcNow,
        };

        database.Contents.Add(template);
        await database.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    private static Task<HttpResponseMessage> FromTemplateAsync(HttpClient client, long templateId, CancellationToken cancellationToken) =>
        SendAsync(
            client,
            HttpMethod.Post,
            $"{ContentEndpoints.Pattern}/from-template/{templateId}",
            new { ownerDepartment = nameof(Department.ED), slug = Slug() },
            cancellationToken);

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response, string field, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.True(
            problem.GetProperty("errors").TryGetProperty(field, out var errors),
            problem.GetProperty("errors").ToString());
        return errors.EnumerateArray().First().GetString();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Content = JsonContent.Create(payload, payload.GetType());
        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task SeedGrantAsync(int vid, string permission, Department department, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        if (!await database.UserGrants.AnyAsync(
                row => row.Vid == vid && row.Value == permission && row.Department == department,
                cancellationToken))
        {
            database.UserGrants.Add(new UserGrant
            {
                Vid = vid,
                Kind = GrantKind.Permission,
                Value = permission,
                Department = department,
                Effect = GrantEffect.Grant,
                Reason = "test",
            });
            await database.SaveChangesAsync(cancellationToken);
        }
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
