using System.Security.Claims;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Agent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The version of a contract with a member's external program, in the core (M4, E10g, note
/// 2026-10-06-la-versione-di-un-contratto-nel-nucleo): what the tours' agent asks of a request (<see cref="AgentContract"/>,
/// M2, T19b), for any module's program. A request goes through the filter as an endpoint runs it, and a refusal is written
/// as the server writes it: what the program reads.
/// </summary>
public sealed class ContractVersionTests
{
    private const string Header = "Hub-Program-Contract";

    /// <summary>A module's key that no language file declares: the catalog answers with the key itself.</summary>
    private const string TitleKey = "program:errors.contract";

    private const string Code = "programContract";

    /// <summary>A program's contract at its first version.</summary>
    private static readonly ContractVersion First = new(Header, 1, [1], TitleKey, Code);

    /// <summary>The same contract at its second version, which still speaks the first for a release.</summary>
    private static readonly ContractVersion Second = new(Header, 2, [1, 2], TitleKey, Code);

    /// <summary>The tours' contract given to the core, as their move onto it will declare it (note §5).</summary>
    private static readonly ContractVersion ToursInTheCore = new(
        AgentContract.Header,
        AgentContract.Current,
        AgentContract.Accepted,
        AgentContract.VersionTitleKey,
        "agentContract");

    /// <summary>A division whose language is not the first a member may pick, so that falling back to it shows.</summary>
    private static readonly DivisionOptions Division = new()
    {
        Code = "XX",
        CountryId = "XX",
        Domain = "example.org",
        Timezone = "UTC",
        Locales = ["en", "it"],
        DefaultLocale = "it",
    };

    /// <summary>The language files of this repository, read the way the server reads them.</summary>
    private static readonly LocaleCatalog Catalog = new(HubPaths.Resolve(AppContext.BaseDirectory), Options.Create(Division));

    /// <summary>What the endpoint behind the filter answers: the filter hands it on untouched.</summary>
    private static readonly object FromTheEndpoint = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("one")]
    [InlineData("1 1")]
    [InlineData("99999999999")]
    public async Task ARequestThatSpeaksNoAcceptedVersionIsRefusedWithTheVersionsAccepted(string? said)
    {
        var answer = await AskAsync(First.RequireAsync, Header, said is null ? StringValues.Empty : new StringValues(said));

        Assert.False(answer.Reached);
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);
        Assert.Equal(Code, answer.Body.GetProperty("code").GetString());
        Assert.Equal(1, answer.Body.GetProperty("current").GetInt32());
        Assert.Equal([1], Versions(answer.Body.GetProperty("accepted")));

        // The hub spoke no version, and the answer says none.
        Assert.Null(answer.Spoken);
    }

    [Fact]
    public async Task AHeaderSentTwiceSaysNoVersion()
    {
        // Two values are read together, "1,1": not a version.
        var answer = await AskAsync(First.RequireAsync, Header, new StringValues(["1", "1"]));

        Assert.False(answer.Reached);
        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);
    }

    [Theory]
    [InlineData("1")]
    [InlineData(" 1 ")]
    public async Task AnAcceptedVersionReachesTheEndpointAndTheAnswerSaysIt(string said)
    {
        var answer = await AskAsync(First.RequireAsync, Header, said);

        Assert.True(answer.Reached);
        Assert.Same(FromTheEndpoint, answer.Result);
        Assert.Equal("1", answer.Spoken);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public async Task TheAnswerSaysTheVersionTheRequestSpokeNotTheCurrentOne(string said)
    {
        // A program still on the first version is answered in the first, for as long as the hub speaks it.
        var answer = await AskAsync(Second.RequireAsync, Header, said);

        Assert.True(answer.Reached);
        Assert.Equal(said, answer.Spoken);
    }

    [Fact]
    public async Task ARefusalSaysTheCurrentVersionAndEveryAcceptedOne()
    {
        var answer = await AskAsync(Second.RequireAsync, Header, "3");

        Assert.Equal(StatusCodes.Status400BadRequest, answer.Status);
        Assert.Equal(2, answer.Body.GetProperty("current").GetInt32());
        Assert.Equal([1, 2], Versions(answer.Body.GetProperty("accepted")));
    }

    [Fact]
    public async Task TheRefusalIsTitledInTheLanguageOfWhoeverAsks()
    {
        var english = Catalog.Get("en", AgentContract.VersionTitleKey);
        var italian = Catalog.Get("it", AgentContract.VersionTitleKey);
        Assert.NotNull(english);
        Assert.NotNull(italian);
        Assert.NotEqual(english, italian);

        Assert.Equal(english, await TitleAsync(Catalog, "en"));
        Assert.Equal(italian, await TitleAsync(Catalog, "it"));

        // Nobody to ask the language of: the division's.
        Assert.Equal(italian, await TitleAsync(Catalog, locale: null));

        // No catalog, or a key no file declares: the key, never an empty title.
        Assert.Equal(AgentContract.VersionTitleKey, await TitleAsync(catalog: null, "en"));
        Assert.Equal(TitleKey, (await AskAsync(First.RequireAsync, Header, StringValues.Empty, Catalog, "en")).Body.GetProperty("title").GetString());

        static async Task<string?> TitleAsync(LocaleCatalog? catalog, string? locale) =>
            (await AskAsync(ToursInTheCore.RequireAsync, AgentContract.Header, StringValues.Empty, catalog, locale))
                .Body.GetProperty("title").GetString();
    }

    [Fact]
    public void WhatCannotBeAContractIsRefusedWhenItIsDeclared()
    {
        // The header: a token of HTTP, nothing else.
        Assert.Throws<ArgumentNullException>(() => new ContractVersion(null!, 1, [1], TitleKey, Code));
        foreach (var header in new[] { string.Empty, " ", "Hub Contract", "Hub:Contract", "Hub-Contract\r\n", "Hub-Contràct" })
        {
            Assert.Throws<ArgumentException>(() => new ContractVersion(header, 1, [1], TitleKey, Code));
        }

        // The versions: at least one, each positive, each once, and the current one among them.
        Assert.Throws<ArgumentNullException>(() => new ContractVersion(Header, 1, null!, TitleKey, Code));
        foreach (var accepted in new int[][] { [], [0], [-1, 1], [1, 1] })
        {
            Assert.Throws<ArgumentException>(() => new ContractVersion(Header, 1, accepted, TitleKey, Code));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new ContractVersion(Header, 2, [1], TitleKey, Code));

        // The words: a key and a code, never empty.
        Assert.Throws<ArgumentNullException>(() => new ContractVersion(Header, 1, [1], null!, Code));
        Assert.Throws<ArgumentNullException>(() => new ContractVersion(Header, 1, [1], TitleKey, null!));
        foreach (var empty in new[] { string.Empty, " " })
        {
            Assert.Throws<ArgumentException>(() => new ContractVersion(Header, 1, [1], empty, Code));
            Assert.Throws<ArgumentException>(() => new ContractVersion(Header, 1, [1], TitleKey, empty));
        }
    }

    [Fact]
    public void AContractKeepsItsOwnVersions()
    {
        // The list a module passes is copied: adding to it afterwards opens no version.
        var versions = new List<int> { 1 };
        var contract = new ContractVersion(Header, 1, versions, TitleKey, Code);
        versions.Add(2);

        Assert.Equal([1], contract.Accepted);
    }

    /// <summary>
    /// The twin test: the core with the tours' values and the tours' own filter, on the same requests, give the same answer —
    /// the same status, the same body (title, code, current, accepted), the same header, the endpoint reached or not. It is
    /// the proof that the tours' move onto the core changes no answer an agent reads, and it goes away with their copy (note
    /// §5).
    /// </summary>
    [Fact]
    public async Task TheCoreAnswersEveryRequestAsTheToursFilterDoes()
    {
        StringValues[] requests =
        [
            StringValues.Empty, string.Empty, " ", "1", " 1 ", "\t1", "01", "2", "0", "-1", "+1", "1.0", "1e0", "0x1", "one",
            char.ConvertFromUtf32(0x0661), "99999999999", "1 1", new StringValues(["1", "1"]), new StringValues(["1", "2"]),
        ];

        var answers = new List<(string Said, Answer Answer)>();
        foreach (var said in requests)
        {
            foreach (var catalog in new[] { Catalog, null })
            {
                foreach (var locale in new[] { "en", "it", null })
                {
                    var core = await AskAsync(ToursInTheCore.RequireAsync, AgentContract.Header, said, catalog, locale);
                    var tours = await AskAsync(AgentContract.RequireVersionAsync, AgentContract.Header, said, catalog, locale);

                    Assert.Equal(tours.Reached, core.Reached);
                    Assert.Equal(tours.Status, core.Status);
                    Assert.Equal(tours.Text, core.Text);
                    Assert.Equal(tours.Spoken, core.Spoken);
                    answers.Add((said.ToString(), core));
                }
            }
        }

        // What the tours accept, and so the core: ASCII digits only, spaces around and zeros in front allowed.
        Assert.Equal(["1", " 1 ", "\t1", "01"], answers.Where(entry => entry.Answer.Reached).Select(entry => entry.Said).Distinct());

        // The refusals were written in more than one language.
        Assert.True(answers.Where(entry => !entry.Answer.Reached).Select(entry => entry.Answer.Text).Distinct(StringComparer.Ordinal).Count() > 2);
    }

    /// <summary>
    /// One request through a filter, as an endpoint runs it, from a member who reads in <paramref name="locale"/> (none:
    /// nobody to ask), and a refusal written as the server writes a problem.
    /// </summary>
    private static async Task<Answer> AskAsync(
        Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> filter,
        string header,
        StringValues said,
        LocaleCatalog? catalog = null,
        string? locale = null)
    {
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        if (said.Count > 0)
        {
            http.Request.Headers[header] = said;
        }

        List<object> services = [NullLoggerFactory.Instance];
        if (catalog is not null)
        {
            services.Add(catalog);
        }

        if (locale is not null)
        {
            http.User = new ClaimsPrincipal(HubClaims.BuildIdentity(
                vid: 600000,
                firstName: "Test",
                lastName: "Member",
                locale: locale,
                securityStamp: "stamp",
                isSuperadmin: false,
                isStaff: false,
                positions: [],
                permissions: []));
            services.Add(new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = http }, Options.Create(Division)));
        }

        http.RequestServices = new Services(services);

        var reached = false;
        var result = await filter(new DefaultEndpointFilterInvocationContext(http), _ =>
        {
            reached = true;
            return ValueTask.FromResult<object?>(FromTheEndpoint);
        });

        if (result is IResult refusal)
        {
            await refusal.ExecuteAsync(http);
        }

        return new Answer(
            reached,
            result,
            http.Response.StatusCode,
            Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray()),
            http.Response.Headers.TryGetValue(header, out var spoken) ? spoken.ToString() : null);
    }

    private static int[] Versions(JsonElement accepted) => [.. accepted.EnumerateArray().Select(version => version.GetInt32())];

    /// <summary>What came back: whether the endpoint was reached, its status, the body as written, the version spoken.</summary>
    private sealed record Answer(bool Reached, object? Result, int Status, string Text, string? Spoken)
    {
        public JsonElement Body => JsonSerializer.Deserialize<JsonElement>(Text);
    }

    /// <summary>The request's services: the logger the server writes a problem with, and what the test gives.</summary>
    private sealed class Services(IEnumerable<object> services) : IServiceProvider
    {
        public object? GetService(Type serviceType) => services.FirstOrDefault(serviceType.IsInstanceOfType);
    }
}
