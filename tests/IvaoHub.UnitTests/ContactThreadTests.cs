using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of the threads of the contacts that need no database (M2, T14a): the participants column, the registry of
/// the resolvers, and what the form accepts.
/// </summary>
public sealed class ContactThreadTests
{
    [Fact]
    public void TheParticipantsAreWrittenDistinctAndInOrderWithoutTheSender()
    {
        Assert.Equal("[3,7]", ContactParticipants.Write([7, 3, 7, 0, 42], except: 42));
        Assert.Equal([3, 7], ContactParticipants.Parse("[3,7]"));
        Assert.Empty(ContactParticipants.Parse(""));
    }

    [Fact]
    public void TheSenderTakesPartInTheirOwnThread()
    {
        var message = new ContactMessage { CreatedBy = 42, ParticipantsJson = "[7]" };
        Assert.Equal([42, 7], message.ParticipantVids);
    }

    [Fact]
    public void TwoResolversForOneModuleAreRefusedAtStartUp()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new ContactReferenceResolvers([new Resolver("tours"), new Resolver("tours")]));
        Assert.Contains("tours", error.Message, StringComparison.Ordinal);

        var resolvers = new ContactReferenceResolvers([new Resolver("tours")]);
        Assert.NotNull(resolvers.Find("tours"));
        Assert.Null(resolvers.Find("events"));
        Assert.Null(resolvers.Find(null));
    }

    [Theory]
    [InlineData(null, 0, true)]
    [InlineData("general", 0, true)]
    [InlineData("clarification", 1, true)]
    [InlineData("clarification", 0, false)]
    [InlineData("clarification", 11, false)]
    [InlineData("dispute", 1, false)]
    [InlineData("anything", 0, false)]
    public void TheFormOpensOnlyItsOwnKindsAndAClarificationIsAboutSomething(string? kind, int references, bool valid)
    {
        var body = new ContactSubmitDto(
            Department.FOD,
            "Subject",
            "Body",
            kind,
            [.. Enumerable.Range(1, references).Select(id => new ContactReferenceInput("tours", $"pirep:{id}"))]);

        Assert.Equal(valid, new ContactSubmitDtoValidator().Validate(body).IsValid);
    }

    private sealed class Resolver(string module) : IContactReferenceResolver
    {
        public string SourceModule => module;

        public Task<ContactReferenceTarget?> ResolveAsync(string sourceId, CancellationToken cancellationToken) =>
            Task.FromResult<ContactReferenceTarget?>(null);
    }
}
