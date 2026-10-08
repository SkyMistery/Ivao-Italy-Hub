using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The start-up check of <see cref="WithdrawnByStakeholderAttribute"/> (M4, E10h, note
/// 2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga): the mark lets the member a row is about delete the row they sent, and the
/// interceptor's guard honours it only on an entity that is <see cref="IOwnedByDepartment"/> — a row with no department the guard
/// never looks at —, <see cref="ISubmittedByMembers"/> and <see cref="IHasStakeholder"/>. On any other the hub refuses to start,
/// every mistake of every entity in one message, rather than leave a row open to anybody, or closed to its member, without a word.
/// </summary>
public sealed class VerifyWithdrawalsTests
{
    private const string Mark = "(WithdrawnByStakeholder)";

    [WithdrawnByStakeholder]
    private sealed class WithdrawnAsTheGuardKnowsIt : IOwnedByDepartment, ISubmittedByMembers, IHasStakeholder
    {
        public Department OwnerDepartment => Department.ED;

        public int? StakeholderVid => null;
    }

    // What a pilot's report is: the same row without the mark, which asks nothing of it.
    private sealed class SentButNotMarked : IOwnedByDepartment, ISubmittedByMembers, IHasStakeholder
    {
        public Department OwnerDepartment => Department.ED;

        public int? StakeholderVid => null;
    }

    // Without the mark, a row that is none of the three asks nothing either.
    private sealed class NeitherMarkedNorSent;

    [WithdrawnByStakeholder]
    private sealed class MarkedWithNoDepartment : ISubmittedByMembers, IHasStakeholder
    {
        public int? StakeholderVid => null;
    }

    [WithdrawnByStakeholder]
    private sealed class MarkedButNotSentByMembers : IOwnedByDepartment, IHasStakeholder
    {
        public Department OwnerDepartment => Department.ED;

        public int? StakeholderVid => null;
    }

    [WithdrawnByStakeholder]
    private sealed class MarkedButAboutNobody : IOwnedByDepartment, ISubmittedByMembers
    {
        public Department OwnerDepartment => Department.ED;
    }

    [WithdrawnByStakeholder]
    private sealed class MarkedAndNothingElse;

    [Fact]
    public void WhatTheGuardCanHonourPasses() =>
        Assert.Null(Record.Exception(() => HubSaveChangesInterceptor.VerifyWithdrawals(
            [typeof(WithdrawnAsTheGuardKnowsIt), typeof(SentButNotMarked), typeof(NeitherMarkedNorSent)])));

    [Fact]
    public void AMarkedEntityWithNoDepartmentIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => HubSaveChangesInterceptor.VerifyWithdrawals([typeof(MarkedWithNoDepartment)]));

        Assert.Contains(nameof(MarkedWithNoDepartment), refused.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IOwnedByDepartment), refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedEntityItsMembersDoNotSendIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => HubSaveChangesInterceptor.VerifyWithdrawals([typeof(MarkedButNotSentByMembers)]));

        Assert.Contains(nameof(MarkedButNotSentByMembers), refused.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ISubmittedByMembers), refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedEntityAboutNobodyIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => HubSaveChangesInterceptor.VerifyWithdrawals([typeof(MarkedButAboutNobody)]));

        Assert.Contains(nameof(MarkedButAboutNobody), refused.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IHasStakeholder), refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryMistakeOfEveryEntityComesOutTogetherAndOnce()
    {
        // One mistake, three, none — and the first again, as two contexts of one module would list it.
        var refused = Assert.Throws<InvalidOperationException>(() => HubSaveChangesInterceptor.VerifyWithdrawals(
        [
            typeof(MarkedWithNoDepartment),
            typeof(MarkedAndNothingElse),
            typeof(WithdrawnAsTheGuardKnowsIt),
            typeof(MarkedWithNoDepartment),
        ]));

        Assert.Contains(nameof(MarkedWithNoDepartment), refused.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MarkedAndNothingElse), refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(WithdrawnAsTheGuardKnowsIt), refused.Message, StringComparison.Ordinal);
        Assert.Equal(4, refused.Message.Split(Mark).Length - 1);
    }
}
