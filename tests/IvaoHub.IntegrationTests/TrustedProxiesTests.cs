using System.Net;
using IvaoHub.Web;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Which senders of <c>X-Forwarded-For</c> are believed.
/// <para>The answer used to be "anybody": the forwarded headers were accepted with the known
/// networks and known proxies both cleared. That makes the caller the author of its own address,
/// and two things rest on that address — the per IP rate limiter that is the only thing standing
/// in front of the login, and the <c>ip</c> column of <c>hub_audit_log</c>. So the list has to be
/// spelled out, and in production the application refuses to start without it.</para>
/// </summary>
public sealed class TrustedProxiesTests
{
    private static IConfiguration Configuration(params string[] networks)
    {
        var values = new Dictionary<string, string?>();
        for (var index = 0; index < networks.Length; index++)
        {
            values[$"{HubConfiguration.TrustedProxiesKey}:{index}"] = networks[index];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void ProductionRefusesToStartWithoutTheList()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => HubConfiguration.TrustedProxies(Configuration(), required: true));

        Assert.Contains("X-Forwarded-For", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentSimplyDoesNotTrustAnything()
    {
        // Nothing configured and nothing required: the forwarded headers middleware is not put in
        // the pipeline at all, so the address is the one the socket actually came from.
        Assert.Empty(HubConfiguration.TrustedProxies(Configuration(), required: false));
    }

    [Fact]
    public void TheNetworksAreRead()
    {
        var networks = HubConfiguration.TrustedProxies(
            Configuration("173.245.48.0/20", "2400:cb00::/32", "127.0.0.1/32"),
            required: true);

        Assert.Equal(3, networks.Count);
        Assert.Contains(networks, network => network.Contains(IPAddress.Parse("173.245.48.9")));
        Assert.DoesNotContain(networks, network => network.Contains(IPAddress.Parse("8.8.8.8")));
    }

    [Fact]
    public void SomethingThatIsNotANetworkIsRefusedByName()
    {
        // A typo here silently disables the trust, which is the kind of silence that only shows up
        // as "the rate limiting does not work" months later.
        var refused = Assert.Throws<InvalidOperationException>(
            () => HubConfiguration.TrustedProxies(Configuration("173.245.48.0"), required: true));

        Assert.Contains("173.245.48.0", refused.Message, StringComparison.Ordinal);
        Assert.Contains("CIDR", refused.Message, StringComparison.Ordinal);
    }
}
