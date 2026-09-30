using IvaoHub.Core.Ivao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// How long the hub waits for IVAO (M4, E10a). The page of the tracker holding the last session of an airport takes IVAO
/// about ten and a half seconds, and its gateway gives up at fifteen (both measured on 30 September 2026): the standard
/// handler's ten seconds per attempt cut that page every time, so an airport's sessions could never be read.
/// </summary>
public sealed class IvaoApiTimeoutTests
{
    [Fact]
    public void AnAttemptWaitsLongerThanIvaosOwnGateway()
    {
        using var provider = new ServiceCollection().AddIvaoIntegration().BuildServiceProvider();
        var handlers = provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();

        // The options of a client's standard handler go by the name its pipeline logs, "IvaoApiClient-standard". Reading
        // them runs the handler's own validation too, which refuses a circuit breaker sampling less than two attempts.
        var data = handlers.Get($"{nameof(IvaoApiClient)}-standard");

        Assert.True(data.AttemptTimeout.Timeout > TimeSpan.FromSeconds(15), $"an attempt waits {data.AttemptTimeout.Timeout}");

        // A call that never answers costs what it always did.
        Assert.Equal(TimeSpan.FromSeconds(30), data.TotalRequestTimeout.Timeout);

        // The token's own client keeps the standard ten seconds: its endpoint answers in a third of one.
        Assert.Equal(TimeSpan.FromSeconds(10), handlers.Get($"{nameof(IvaoApiTokenProvider)}-standard").AttemptTimeout.Timeout);
    }
}
