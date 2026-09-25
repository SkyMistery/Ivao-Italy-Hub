using IvaoHub.Core.Privacy;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the erasure of a person that need no database (T20b, note 2026-09-25-la-cancellazione-dei-dati-di-una-persona):
/// which names are a person's, and what the walker changes in the JSON of an audit row — the person where a person is named,
/// and nothing else, however much it looks like them.
/// </summary>
public sealed class PersonalDataErasureTests
{
    private const int Vid = 780095;
    private const int Pseudonym = -3;

    [Theory]
    [InlineData("Vid", true)]
    [InlineData("vid", true)]
    [InlineData("DecidedByVid", true)]
    [InlineData("createdBy", true)]
    [InlineData("HandledBy", true)]
    [InlineData("By", false)]
    [InlineData("Id", false)]
    [InlineData("MinPilotRating", false)]
    [InlineData("Vidx", false)]
    public void APersonIsNamedByTheConvention(string name, bool named) =>
        Assert.Equal(named, PersonColumns.IsVidName(name));

    [Fact]
    public void TheWalkerReplacesThePersonWhereAPersonIsNamed()
    {
        const string json = """{"title":"Page","createdBy":780095,"updatedBy":780001,"decidedByVid":780095,"vid":"780095"}""";

        Assert.Equal(
            """{"title":"Page","createdBy":-3,"updatedBy":780001,"decidedByVid":-3,"vid":-3}""",
            AuditRedaction.Rewrite(json, Vid, Pseudonym));
    }

    [Fact]
    public void TheWalkerLeavesANumberThatOnlyLooksLikeThePerson()
    {
        const string json = """{"id":780095,"count":780095,"title":"780095","items":[780095]}""";

        Assert.Same(json, AuditRedaction.Rewrite(json, Vid, Pseudonym));
    }

    [Fact]
    public void TheWalkerReadsTheListsOfPeople()
    {
        Assert.Equal(
            """{"participantsJson":"[780001,-3]","reviewerVids":[-3,780002]}""",
            AuditRedaction.Rewrite("""{"participantsJson":"[780001,780095]","reviewerVids":[780095,780002]}""", Vid, Pseudonym));

        // A whole row that is a list is a list of people: the set of super administrators.
        Assert.Equal("[780001,-3]", AuditRedaction.Rewrite("[780001,780095]", Vid, Pseudonym));
    }

    [Fact]
    public void ANotificationIsAboutThePersonWhenItsDataNamesThem()
    {
        Assert.True(AuditRedaction.Mentions("""{"subject":"Hello","vid":"780095"}""", Vid));
        Assert.False(AuditRedaction.Mentions("""{"subject":"780095","vid":"780001"}""", Vid));
        Assert.False(AuditRedaction.Mentions(null, Vid));
        Assert.False(AuditRedaction.Mentions("not json", Vid));
    }
}
