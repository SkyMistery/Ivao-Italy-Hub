using IvaoHub.Core.Data.Crud;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>The page a list request asks for: a huge page number was a 500 of the generated lists and of the public search
/// (issue #319, item 9).</summary>
public sealed class PagingTests
{
    [Theory]
    [InlineData(null, null, 1, 25)]
    [InlineData(0, -3, 1, 25)]
    [InlineData(3, 10, 3, 10)]
    [InlineData(2, 500, 2, 100)]
    public void ThePageAndItsSizeAreReadOneWay(int? page, int? pageSize, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), Paging.Read(page, pageSize, defaultSize: 25, maxSize: 100));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    public void AHugePageSkipsWithoutOverflowing(int pageSize)
    {
        var (page, size) = Paging.Read(int.MaxValue, pageSize, defaultSize: 25, maxSize: 100);

        var skip = (long)Paging.Skip(page, size);
        Assert.InRange(skip, 0, int.MaxValue);
        Assert.Equal(((long)page - 1) * size, skip);
    }
}
