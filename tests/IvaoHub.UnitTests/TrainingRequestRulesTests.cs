using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Requests;
using IvaoHub.Modules.Training.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of a request (M3, A6a; design M3 §2.2, §2.8) with no database: the rating proposed, the thresholds of hours, the
/// waiting on each ladder after a training, a no-show and the trainer's box, the mock exam from the trainee's history, the ban
/// with and without an end, and the order the design gives them.
/// <para>The ratings are a vocabulary of this test's making, not the network's (design M3 §10): the rules have to hold for
/// whatever ladders the core is given, and so the positions are of a kind the network does not have.</para>
/// </summary>
public sealed class TrainingRequestRulesTests
{
    private static readonly RatingVocabulary Ratings = new(
    [
        new(RatingKind.Atc, 11, "A1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Atc, 12, "A2", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Atc, 13, "A3", HasPracticalTraining: true, PositionType: "BOX"),
        new(RatingKind.Atc, 14, "A4", HasPracticalTraining: true, PositionType: "RING"),
        new(RatingKind.Atc, 15, "A5", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 21, "P1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 22, "P2", HasPracticalTraining: true, PositionType: null),
        new(RatingKind.Pilot, 23, "P3", HasPracticalTraining: false, PositionType: null),
    ]);

    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TrainingSettings Defaults = new();

    [Fact]
    public void TheTrainingProposedIsTheNextRatingOfTheLadderWithAPracticalTraining()
    {
        Assert.Equal(("A3", null), Proposal(RatingKind.Atc, 12));
        Assert.Equal(("A4", null), Proposal(RatingKind.Atc, 13));
        Assert.Equal(("P2", null), Proposal(RatingKind.Pilot, 21));

        // Nothing after the last trained rating, nor after one the vocabulary does not know or the network never said.
        Assert.Equal((null, RequestRules.NothingToAsk), Proposal(RatingKind.Atc, 14));
        Assert.Equal((null, RequestRules.NothingToAsk), Proposal(RatingKind.Pilot, 22));
        Assert.Equal((null, RequestRules.NothingToAsk), Proposal(RatingKind.Atc, 99));
        Assert.Equal((null, RequestRules.NothingToAsk), Proposal(RatingKind.Pilot, null));

        static (string?, string?) Proposal(RatingKind kind, int? rating)
        {
            var standing = Standing(Facts(kind, rating));
            return (standing.Next?.ShortName, standing.Refusal);
        }
    }

    [Fact]
    public void TheHoursOfTheLadderAreHeldAgainstTheThresholdOfTheRatingProposed()
    {
        var settings = new TrainingSettings
        {
            MinimumHours = [new(RatingKind.Atc, 13, 50), new(RatingKind.Pilot, 22, 30)],
        };

        Assert.Equal(50, RequestRules.MinimumHours(settings, Ratings.Find(RatingKind.Atc, 13)!));
        Assert.Null(RequestRules.MinimumHours(settings, Ratings.Find(RatingKind.Atc, 14)!));

        Assert.Equal(RequestRules.HoursTooFew, Standing(Facts(RatingKind.Atc, 12, hours: 49.99m), settings).Refusal);
        Assert.Null(Standing(Facts(RatingKind.Atc, 12, hours: 50m), settings).Refusal);

        // Hours the network never said are not zero: the hub does not know them, and says so.
        Assert.Equal(RequestRules.HoursUnknown, Standing(Facts(RatingKind.Atc, 12, hours: null), settings).Refusal);

        // A rating with no threshold asks for no hours, known or not.
        Assert.Null(Standing(Facts(RatingKind.Atc, 13, hours: null), settings).Refusal);

        // The pilot's threshold is on the pilot's hours.
        var pilot = Standing(Facts(RatingKind.Pilot, 21, hours: 29m), settings);
        Assert.Equal((RequestRules.HoursTooFew, 30), (pilot.Refusal, pilot.MinimumHours));
    }

    [Fact]
    public void TheWaitingRunsOnItsLadderFromTheLastTrainingClosedOnIt()
    {
        var settings = new TrainingSettings { CooldownDays = 5, NoShowCooldownDays = 14 };

        // After a completed training: five days, and then nothing.
        Training[] completed = [Closed(1, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-4))];
        var waiting = Standing(Facts(RatingKind.Atc, 12, history: completed), settings);
        Assert.Equal((RequestRules.Waiting, Now.AddDays(1)), (waiting.Refusal, waiting.WaitUntil));
        Assert.Null(Standing(Facts(RatingKind.Atc, 12, history: [Closed(1, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-5))]), settings).Refusal);

        // The trainer's box takes it away.
        var waived = Closed(1, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-1));
        waived.CooldownWaived = true;
        Assert.Null(Standing(Facts(RatingKind.Atc, 12, history: [waived]), settings).Refusal);

        // After a no-show: the longer wait.
        Assert.Equal(
            Now.AddDays(4),
            Standing(Facts(RatingKind.Atc, 12, history: [Closed(1, RatingKind.Atc, TrainingState.NoShow, Now.AddDays(-10))]), settings).WaitUntil);

        // Nothing after a refusal, a cancellation or a closure: the last one closed decides.
        foreach (var state in new[] { TrainingState.Rejected, TrainingState.Cancelled, TrainingState.Closed })
        {
            Training[] history =
            [
                Closed(1, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-10)),
                Closed(2, RatingKind.Atc, state, Now.AddHours(-1)),
            ];
            Assert.Null(RequestRules.WaitUntil(history, RatingKind.Atc, settings));
        }

        // The ladders are two: a training of the one keeps nobody waiting on the other (d4).
        Assert.Null(Standing(Facts(RatingKind.Pilot, 21, history: completed), settings).Refusal);
        Assert.Null(RequestRules.WaitUntil([], RatingKind.Atc, settings));
    }

    [Fact]
    public void OneTrainingAtATimeOnEachLadder()
    {
        foreach (var state in new[] { TrainingState.Requested, TrainingState.Accepted, TrainingState.Assigned, TrainingState.Scheduled })
        {
            var open = Open(7, RatingKind.Atc, state);
            var standing = Standing(Facts(RatingKind.Atc, 12, history: [open]));

            Assert.Equal((RequestRules.Open, 7L), (standing.Refusal, standing.OpenTrainingId));
            Assert.True(Training.IsOpen(state));
            Assert.Equal(RatingKind.Atc, open.OpenKind);

            // The other ladder is free.
            Assert.Null(Standing(Facts(RatingKind.Pilot, 21, history: [open])).Refusal);
        }

        foreach (var state in new[] { TrainingState.Completed, TrainingState.Rejected, TrainingState.Cancelled, TrainingState.Closed, TrainingState.NoShow })
        {
            Assert.False(Training.IsOpen(state));
            Assert.Null(Closed(1, RatingKind.Atc, state, Now.AddDays(-30)).OpenKind);
        }
    }

    [Fact]
    public void AMockExamFollowsACompletedTrainingOnTheSameRatingReadyForIt()
    {
        var rating = Ratings.Find(RatingKind.Atc, 13)!;

        var ready = Closed(1, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-30), trained: 13);
        ready.ReadyForMockExam = true;
        Assert.True(RequestRules.IsMockExam([ready], rating));
        Assert.True(Standing(Facts(RatingKind.Atc, 12, history: [ready])).IsMockExam);

        // A mock exam already: the next one is a training again.
        var mock = Closed(2, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-10), trained: 13);
        mock.ReadyForMockExam = true;
        mock.IsMockExam = true;
        Assert.False(RequestRules.IsMockExam([ready, mock], rating));

        // The last completed one decides, whatever came before it.
        var notReady = Closed(3, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-10), trained: 13);
        Assert.False(RequestRules.IsMockExam([ready, notReady], rating));

        // Only the same ladder and rating, and only a completed training.
        var otherRating = Closed(4, RatingKind.Atc, TrainingState.Completed, Now.AddDays(-5), trained: 14);
        otherRating.ReadyForMockExam = true;
        Assert.True(RequestRules.IsMockExam([ready, otherRating], rating));
        Assert.False(RequestRules.IsMockExam([otherRating], rating));

        var noShow = Closed(5, RatingKind.Atc, TrainingState.NoShow, Now.AddDays(-5), trained: 13);
        noShow.ReadyForMockExam = true;
        Assert.False(RequestRules.IsMockExam([noShow], rating));
    }

    [Fact]
    public void ABanHoldsOnBothLaddersUntilItsEndOrUntilSomebodyLiftsIt()
    {
        var withEnd = Ban(Now.AddDays(-1), endsAt: Now.AddDays(3));
        var forGood = Ban(Now.AddDays(-1), endsAt: null);

        Assert.True(withEnd.Holds(Now));
        Assert.False(withEnd.Holds(Now.AddDays(3)));
        Assert.True(forGood.Holds(Now.AddYears(10)));
        Assert.False(forGood.Holds(Now.AddDays(-2)));

        var lifted = Ban(Now.AddDays(-5), endsAt: null);
        lifted.LiftedAt = Now.AddDays(-1);
        lifted.LiftedBy = 1;
        Assert.False(lifted.Holds(Now));
        Assert.True(lifted.Holds(Now.AddDays(-2)));

        foreach (var kind in new[] { RatingKind.Atc, RatingKind.Pilot })
        {
            var banned = Standing(Facts(kind, kind == RatingKind.Atc ? 12 : 21, bans: [withEnd]));
            Assert.Equal((RequestRules.Banned, Now.AddDays(3)), (banned.Refusal, banned.BannedUntil));
        }

        // Until somebody lifts it: no end to show, even next to one that has one.
        var both = Standing(Facts(RatingKind.Atc, 12, bans: [withEnd, forGood]));
        Assert.Equal((RequestRules.Banned, (DateTime?)null), (both.Refusal, both.BannedUntil));

        // Over, or lifted: the request goes on to the next rule.
        Assert.Null(Standing(Facts(RatingKind.Atc, 12, bans: [Ban(Now.AddDays(-9), endsAt: Now.AddDays(-2)), lifted])).Refusal);
    }

    [Fact]
    public void ARatingTrainedOnAPositionNeedsOneOffered()
    {
        Assert.Equal(RequestRules.NoPosition, Standing(Facts(RatingKind.Atc, 12, positionsOffered: 0)).Refusal);
        Assert.Null(Standing(Facts(RatingKind.Atc, 12, positionsOffered: 1)).Refusal);

        // A rating trained on no position asks for none.
        Assert.Null(Standing(Facts(RatingKind.Pilot, 21, positionsOffered: 0)).Refusal);
    }

    [Fact]
    public void TheFirstRuleThatRefusesIsTheDesigns()
    {
        var settings = new TrainingSettings { MinimumHours = [new(RatingKind.Atc, 13, 50)] };
        var ban = Ban(Now.AddDays(-1), endsAt: null);
        var open = Open(2, RatingKind.Atc, TrainingState.Requested);
        var noShow = Closed(1, RatingKind.Atc, TrainingState.NoShow, Now.AddDays(-1));

        // The ban, one open training, the waiting, something to ask for, the hours, a position.
        Assert.Equal(RequestRules.Banned, Standing(Facts(RatingKind.Atc, 12, hours: 1m, history: [noShow, open], bans: [ban], positionsOffered: 0), settings).Refusal);
        Assert.Equal(RequestRules.Open, Standing(Facts(RatingKind.Atc, 12, hours: 1m, history: [noShow, open], positionsOffered: 0), settings).Refusal);
        Assert.Equal(RequestRules.Waiting, Standing(Facts(RatingKind.Atc, 14, hours: 1m, history: [noShow], positionsOffered: 0), settings).Refusal);
        Assert.Equal(RequestRules.NothingToAsk, Standing(Facts(RatingKind.Atc, 14, hours: 1m, positionsOffered: 0), settings).Refusal);
        Assert.Equal(RequestRules.HoursTooFew, Standing(Facts(RatingKind.Atc, 12, hours: 1m, positionsOffered: 0), settings).Refusal);
        Assert.Equal(RequestRules.NoPosition, Standing(Facts(RatingKind.Atc, 12, hours: 50m, positionsOffered: 0), settings).Refusal);

        // What a refusal leaves out is still said: the page shows the rating, the waiting and the threshold beside it.
        var standing = Standing(Facts(RatingKind.Atc, 12, hours: 1m, history: [noShow], bans: [ban]), settings);
        Assert.Equal(("A3", Now.AddDays(13), 50), (standing.Next?.ShortName, standing.WaitUntil, standing.MinimumHours));
    }

    [Fact]
    public void ATrainingEndsWhenItsStateSays()
    {
        var training = Open(1, RatingKind.Atc, TrainingState.Requested);
        Assert.Null(RequestRules.EndedAt(training));

        training.State = TrainingState.Completed;
        training.CompletedAt = Now;
        Assert.Equal(Now, RequestRules.EndedAt(training));

        training.State = TrainingState.Rejected;
        training.DecidedAt = Now.AddDays(-1);
        Assert.Equal(Now.AddDays(-1), RequestRules.EndedAt(training));

        training.State = TrainingState.Cancelled;
        training.ClosedAt = Now.AddDays(-2);
        Assert.Equal(Now.AddDays(-2), RequestRules.EndedAt(training));

        // A row written by hand without its moment ends at its last change.
        var bare = new Training { State = TrainingState.NoShow, UpdatedAt = Now.AddDays(-3) };
        Assert.Equal(Now.AddDays(-3), RequestRules.EndedAt(bare));
    }

    [Fact]
    public void ATrainingIsScopedToItselfAndAboutItsTrainee()
    {
        var training = new Training { Id = 42, TraineeVid = 4242 };

        Assert.Equal($"{TrainingModule.ModuleKey}:training:42", training.ResourceScope);
        Assert.Equal(Training.ScopeOf(42), training.ResourceScope);
        Assert.Equal(4242, training.StakeholderVid);
        Assert.Equal(IvaoHub.Core.Division.Visibility.Members, training.Visibility);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------------------

    private static PathStanding Standing(PathFacts facts, TrainingSettings? settings = null) =>
        RequestRules.Standing(facts, settings ?? Defaults, Ratings, Now);

    private static PathFacts Facts(
        RatingKind kind,
        int? rating,
        decimal? hours = 100m,
        IReadOnlyList<Training>? history = null,
        IReadOnlyList<TraineeBan>? bans = null,
        int positionsOffered = 3) =>
        new(kind, rating, hours, history ?? [], bans ?? [], positionsOffered);

    private static Training Open(long id, RatingKind kind, TrainingState state) =>
        new() { Id = id, Kind = kind, State = state, TraineeVid = 1 };

    /// <summary>A training closed at that moment, in the column its state keeps it in.</summary>
    private static Training Closed(long id, RatingKind kind, TrainingState state, DateTime at, int trained = 0)
    {
        var training = new Training { Id = id, Kind = kind, Rating = trained, State = state, TraineeVid = 1 };

        switch (state)
        {
            case TrainingState.Completed:
                training.CompletedAt = at;
                break;
            case TrainingState.Rejected:
                training.DecidedAt = at;
                break;
            default:
                training.ClosedAt = at;
                break;
        }

        return training;
    }

    private static TraineeBan Ban(DateTime given, DateTime? endsAt) =>
        new() { Vid = 1, Reason = "trn-test", CreatedAt = given, EndsAt = endsAt };
}
