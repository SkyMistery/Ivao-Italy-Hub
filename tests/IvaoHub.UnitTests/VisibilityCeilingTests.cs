using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The second question about visibility, and the only place that answers it: not "may this reader
/// see this row" — the global query filter owns that — but "may this row be copied into a page that
/// somebody else will read". Publication is where the two part company (design M0 section 5.5).
/// </summary>
public sealed class VisibilityCeilingTests
{
    [Theory]
    [InlineData(Visibility.Public, Visibility.Public, true)]
    [InlineData(Visibility.Public, Visibility.Members, false)]
    [InlineData(Visibility.Public, Visibility.Staff, false)]
    [InlineData(Visibility.Public, Visibility.Department, false)]
    [InlineData(Visibility.Members, Visibility.Public, true)]
    [InlineData(Visibility.Members, Visibility.Members, true)]
    [InlineData(Visibility.Members, Visibility.Staff, false)]
    [InlineData(Visibility.Staff, Visibility.Members, true)]
    [InlineData(Visibility.Staff, Visibility.Staff, true)]
    [InlineData(Visibility.Staff, Visibility.Department, false)]
    [InlineData(Visibility.Department, Visibility.Staff, true)]
    public void ARowMayOnlyBeEmbeddedInAPageThatIsAtLeastAsReserved(
        Visibility page,
        Visibility row,
        bool allowed)
    {
        // Same department throughout, so this theory is about the visibilities alone.
        Assert.Equal(allowed, VisibilityCeiling.Allows(page, Department.ED, row, Department.ED));
    }

    [Fact]
    public void ARowOfOneDepartmentDoesNotTravelIntoAPageOfAnother()
    {
        // "Visible to a department" names a different set of people for each one, so sharing the
        // visibility is not enough: a page of ED may not carry a row FOD keeps to itself.
        Assert.True(VisibilityCeiling.Allows(
            Visibility.Department, Department.ED, Visibility.Department, Department.ED));

        Assert.False(VisibilityCeiling.Allows(
            Visibility.Department, Department.ED, Visibility.Department, Department.FOD));

        // A row of another department that is not itself department-scoped is fine: what it is
        // visible to is what decides, not who owns it.
        Assert.True(VisibilityCeiling.Allows(
            Visibility.Department, Department.ED, Visibility.Staff, Department.FOD));
    }

    [Fact]
    public void EveryVisibilityHasAnAnswerAndThePublicOneIsTheNarrowest()
    {
        foreach (var visibility in Enum.GetValues<Visibility>())
        {
            var embeddable = VisibilityCeiling.For(visibility);

            // Whatever a page is, it may always carry what everybody can see; and it may never
            // carry more than what exists.
            Assert.Contains(Visibility.Public, embeddable);
            Assert.All(embeddable, allowed => Assert.Contains(allowed, Enum.GetValues<Visibility>()));
        }

        Assert.Equal([Visibility.Public], VisibilityCeiling.For(Visibility.Public));
    }
}
