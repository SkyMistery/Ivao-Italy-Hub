using IvaoHub.Modules.Atc;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// F0 has no domain logic yet; this only keeps the unit test project real from the first phase.
/// The architecture tests (project references, single authorization handler) arrive in F4.
/// </summary>
public sealed class RepositoryLayoutTests
{
    [Fact]
    public void AtcModulePlaceholderExposesItsKey()
    {
        Assert.Equal("atc", new AtcModule().Key);
    }
}
