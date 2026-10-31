using System.Net;
using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// What a per-address limit counts a caller by (issue #320, item 3): an IPv6 caller holds a whole <c>/64</c>, and counted by the
/// full address it could rotate past the ten sign-ins a minute.
/// </summary>
public sealed class AddressPartitionTests
{
    [Theory]
    [InlineData("198.51.100.7", "198.51.100.7")]
    [InlineData("::ffff:198.51.100.7", "198.51.100.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:3::1", "2001:db8:1:3::/64")]
    public void AnIpv6CallerCountsByItsSlash64(string address, string partition)
    {
        Assert.Equal(partition, HubRateLimits.AddressPartition(IPAddress.Parse(address)));
    }

    [Fact]
    public void NoAddressIsOnePartition()
    {
        Assert.Equal("unknown", HubRateLimits.AddressPartition(null));
    }
}
