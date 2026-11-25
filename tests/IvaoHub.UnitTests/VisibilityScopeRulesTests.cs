using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// <see cref="VisibilityScopeRules.Reads"/> is the query filter's rule read a second time, in memory, for a screen of the site:
/// something that is no row and belongs to no department (design 13 §5.1, the review of #555, finding 4). Two readings of one
/// rule are one too many unless something holds them together, so this asks both, for every visibility and every kind of
/// reader, and they have to answer the same. The filter is the one <see cref="VisibilityQueryFilter"/> builds into a model,
/// compiled and run on a row of a department the reader does not belong to, which is what "no department" means to it.
/// </summary>
public sealed class VisibilityScopeRulesTests
{
    /// <summary>The department of the row: one no reader below belongs to, as a screen belongs to none.</summary>
    private const Department Nobodys = Department.ED;

    public static TheoryData<string, Reader> Readers => new()
    {
        { "a visitor", new Reader() },
        { "a member", new Reader { SeesMemberRows = true } },
        { "the staff of a department", new Reader { SeesMemberRows = true, SeesStaffRows = true, Departments = [Department.WD] } },
        { "whoever sees every department", new Reader { SeesMemberRows = true, SeesStaffRows = true, SeesEveryDepartment = true } },
    };

    [Theory]
    [MemberData(nameof(Readers))]
    public void ItAnswersAsTheQueryFilterDoes(string who, Reader reader)
    {
        var filter = FilterOf(reader);

        foreach (var visibility in Enum.GetValues<Visibility>())
        {
            var row = new Row { Visibility = visibility, OwnerDepartment = Nobodys };
            Assert.True(filter(row) == reader.Reads(visibility), $"{who}, {visibility}: the filter and Reads disagree.");
        }
    }

    [Fact]
    public void TheMapOfTheSiteLeavesOutAScreenTheReaderMayNotOpen()
    {
        // Every screen declared today is public, so only a narrower one shows that the map asks (finding 4: without the
        // `Where` of `OpenTo` every other test stays green).
        SiteScreen[] screens =
        [
            new("/open", "open"),
            new("/members", "members", Visibility.Members),
            new("/staff-only", "staff", Visibility.Staff),
        ];

        Assert.Equal(["/open"], SiteMapProvider.OpenTo(screens, new Reader()).Select(screen => screen.Path));
        Assert.Equal(
            ["/open", "/members"],
            SiteMapProvider.OpenTo(screens, new Reader { SeesMemberRows = true }).Select(screen => screen.Path));
        Assert.Equal(
            ["/open", "/members", "/staff-only"],
            SiteMapProvider.OpenTo(screens, new Reader { SeesMemberRows = true, SeesStaffRows = true }).Select(screen => screen.Path));
    }

    /// <summary>The filter as a model gets it, run on one row with <paramref name="reader"/> as the context.</summary>
    private static Func<Row, bool> FilterOf(Reader reader)
    {
        var model = new ModelBuilder();
        model.Entity<Row>();
        VisibilityQueryFilter.ApplyToModel(model, reader);

        var lambda = model.Model.FindEntityType(typeof(Row))!.GetQueryFilter();
        Assert.NotNull(lambda);

        var compiled = lambda.Compile();
        return row => (bool)compiled.DynamicInvoke(row)!;
    }

    /// <summary>A row that is visible and owned, and nothing else: no version, no state, one department.</summary>
    public sealed class Row : IVisible, IOwnedByDepartment
    {
        public long Id { get; set; }

        public Visibility Visibility { get; set; }

        public Department OwnerDepartment { get; set; }
    }

    /// <summary>
    /// Whoever runs the query, as the filter reads them: the properties of the context. Never used as a database, only as the
    /// scope the filter's expression holds.
    /// </summary>
    public sealed class Reader : DbContext, IVisibilityScope
    {
        public bool SeesEveryDepartment { get; init; }

        public bool SeesMemberRows { get; init; }

        public bool SeesStaffRows { get; init; }

        public List<Department> Departments { get; init; } = [];

        public List<Department> VisibleDepartments => Departments;

        public int VisibleDepartmentMask => Departments.Aggregate(0, (mask, department) => mask | DepartmentMask.Of(department));
    }
}
