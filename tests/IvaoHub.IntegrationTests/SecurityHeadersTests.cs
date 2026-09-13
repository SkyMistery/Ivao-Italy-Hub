using System.Text.Json;
using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The headers every response carries, checked on the wire against the file that declares them.
///
/// <para>⚠️ Until 12 September 2026 there were none — no policy, no <c>nosniff</c>, nothing — and
/// the gap was found while designing something else. What this asserts is therefore not a
/// refinement: it is that the four headers leave the host at all, and that the one that matters,
/// the content security policy, is the one <c>config/security.json</c> says and not a weaker one
/// somebody relaxed to make a screen work.</para>
///
/// <para>Whether the application can <b>live</b> under the policy is a different question and a
/// browser answers it: <c>web/e2e/security.spec.ts</c> walks the screens and fails on a refusal,
/// because a blocked stylesheet breaks no assertion — it just makes the page quietly wrong.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class SecurityHeadersTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    /// <summary>The declaration both halves read: this test and, through Vite, the smoke suite.</summary>
    private static JsonElement Configuration()
    {
        var paths = HubPaths.Resolve(Directory.GetCurrentDirectory());
        return JsonDocument.Parse(File.ReadAllText(paths.SecurityFile)).RootElement;
    }

    [Fact]
    public async Task EveryResponseCarriesTheHeadersTheFileDeclares()
    {
        using var client = _factory.CreateClient();
        var declared = Configuration();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        foreach (var header in declared.GetProperty("headers").EnumerateObject())
        {
            Assert.True(
                response.Headers.TryGetValues(header.Name, out var values),
                $"{header.Name} is missing from the response");
            Assert.Equal(header.Value.GetString(), string.Join(", ", values!));
        }
    }

    [Fact]
    public async Task ThePolicyOnTheWireIsThePolicyInTheFile()
    {
        using var client = _factory.CreateClient();
        var policy = Configuration().GetProperty("contentSecurityPolicy");

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.True(policy.GetProperty("enabled").GetBoolean(), "the policy is switched off in the repository");
        Assert.True(
            response.Headers.TryGetValues("Content-Security-Policy", out var values),
            "no content security policy on the response");

        var sent = string.Join(", ", values!);

        // Directive by directive and not as one string: two readers compose the same policy and
        // neither owes the other the order in which it writes it.
        foreach (var directive in policy.GetProperty("directives").EnumerateObject())
        {
            var sources = string.Join(' ', directive.Value.EnumerateArray().Select(source => source.GetString()));
            Assert.Contains($"{directive.Name} {sources}", sent, StringComparison.Ordinal);
        }

        // The one that would make the rest decoration. Styles carry 'unsafe-inline' and the note of
        // 12 September says why it was measured and accepted; scripts must not, or an injected
        // string runs and everything above is theatre.
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-eval'", sent, StringComparison.Ordinal);
    }
}
