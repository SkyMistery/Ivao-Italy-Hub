using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the staff's side of a training (M3, A7; design M3 §1.2, §2.4, §4.2) with no database: who may train a training —
/// never its trainee, the staff of the training only, with a rating at least the one trained —, when a session shows as held in
/// the division's time zone, which the views of the list are made of, and the scope of the trainer's grant read back.
/// <para>The ratings are a vocabulary of this test's making, not the network's (design M3 §10): the rule has to hold for whatever
/// ladders the core is given — who stands above every trained rating always may, and no rule of the module says so.</para>
/// </summary>
public sealed class TrainingStaffRulesTests
{
    private static readonly RatingVocabulary Ratings = new(
    [
        new(RatingKind.Atc, 11, "A1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Atc, 12, "A2", HasPracticalTraining: true, PositionType: "BOX"),
        new(RatingKind.Atc, 13, "A3", HasPracticalTraining: true, PositionType: "RING"),
        new(RatingKind.Atc, 14, "A4", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 21, "P1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 22, "P2", HasPracticalTraining: true, PositionType: null),
        new(RatingKind.Pilot, 23, "P3", HasPracticalTraining: false, PositionType: null),
    ]);

    private const int TraineeVid = 1001;

    [Fact]
    public void ATrainerIsOfTheStaffOfTheTrainingWithARatingAtLeastTheOneTrained()
    {
        var atc = Accepted(RatingKind.Atc, 12);

        // The rating trained, and every rating above it on the ladder: the top one trains everything, with no rule saying so.
        Assert.Null(TrainerChoice.Refusal(atc, Staff(2001, atc: 12), Ratings));
        Assert.Null(TrainerChoice.Refusal(atc, Staff(2002, atc: 13), Ratings));
        Assert.Null(TrainerChoice.Refusal(atc, Staff(2003, atc: 14), Ratings));

        // Below it, none, or one the vocabulary does not know: not a trainer of this training.
        Assert.Equal(TrainerChoice.RatingTooLow, TrainerChoice.Refusal(atc, Staff(2004, atc: 11), Ratings));
        Assert.Equal(TrainerChoice.RatingTooLow, TrainerChoice.Refusal(atc, Staff(2005, atc: null), Ratings));
        Assert.Equal(TrainerChoice.RatingTooLow, TrainerChoice.Refusal(atc, Staff(2006, atc: 99), Ratings));

        // The rating that counts is the one of the training's ladder: a high pilot's rating trains no controller.
        Assert.Equal(TrainerChoice.RatingTooLow, TrainerChoice.Refusal(atc, Staff(2007, atc: 11, pilot: 23), Ratings));
        var pilot = Accepted(RatingKind.Pilot, 22);
        Assert.Null(TrainerChoice.Refusal(pilot, Staff(2007, atc: 11, pilot: 23), Ratings));
        Assert.Equal(TrainerChoice.RatingTooLow, TrainerChoice.Refusal(pilot, Staff(2008, atc: 14, pilot: 21), Ratings));
    }

    [Fact]
    public void NobodyTrainsTheirOwnTrainingAndNobodyOutsideTheStaffOfTheTraining()
    {
        var atc = Accepted(RatingKind.Atc, 12);

        // The trainee first, whatever they are: staff with the highest rating, still no.
        Assert.Equal(TrainerChoice.IsTrainee, TrainerChoice.Refusal(atc, Staff(TraineeVid, atc: 14), Ratings));

        // A member with every rating who is not of the staff of the training, or somebody the hub never saw.
        Assert.Equal(TrainerChoice.NotStaff, TrainerChoice.Refusal(atc, new TrainerFacts(3001, IsTrainingStaff: false, 14, 23), Ratings));
        Assert.Equal(TrainerChoice.NotStaff, TrainerChoice.Refusal(atc, TrainerFacts.Unknown(3002), Ratings));
    }

    [Fact]
    public void ASessionShowsAsHeldFromTheDayAfterItsOwnInTheTimeZoneOfTheDivision()
    {
        // Two hours ahead of UTC: a day there begins at 22:00 UTC of the day before.
        var zone = TimeZoneInfo.CreateCustomTimeZone("trn-test+2", TimeSpan.FromHours(2), "trn-test+2", "trn-test+2");
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 9, 25, 22, 0, 0, DateTimeKind.Utc), StaffQueue.HeldBefore(now, zone));

        // Today there — even when it was still yesterday in UTC —: not held yet. Yesterday there: held.
        Assert.False(StaffQueue.IsHeld(new DateTime(2026, 9, 25, 22, 30, 0, DateTimeKind.Utc), now, zone));
        Assert.False(StaffQueue.IsHeld(new DateTime(2026, 9, 26, 11, 0, 0, DateTimeKind.Utc), now, zone));
        Assert.True(StaffQueue.IsHeld(new DateTime(2026, 9, 25, 21, 30, 0, DateTimeKind.Utc), now, zone));

        // Just after midnight there, the session of the evening before is held.
        var justAfter = new DateTime(2026, 9, 26, 22, 1, 0, DateTimeKind.Utc);
        Assert.True(StaffQueue.IsHeld(new DateTime(2026, 9, 26, 20, 0, 0, DateTimeKind.Utc), justAfter, zone));

        // In UTC the day is UTC's.
        Assert.Equal(new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc), StaffQueue.HeldBefore(now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void ADayThatBeginsInAGapOfTheClockBeginsAtItsFirstMomentThatExists()
    {
        // A zone whose clocks jump from 00:00 to 01:00 on 1 October 2026: that day has no midnight.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31),
            TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 10, 1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 12, 30));
        var zone = TimeZoneInfo.CreateCustomTimeZone("trn-test-gap", TimeSpan.Zero, "trn-test-gap", "trn-test-gap", "trn-test-gap-summer", [rule]);

        // 01:00 there, an hour ahead for the summer: midnight UTC.
        Assert.Equal(
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            StaffQueue.HeldBefore(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), zone));
    }

    [Fact]
    public void TheViewsOfTheListAreMadeOfTheStatesAndOfTheDayOfTheSession()
    {
        var heldBefore = new DateTime(2026, 9, 26, 0, 0, 0, DateTimeKind.Utc);
        Training[] all =
        [
            With(TrainingState.Requested),
            With(TrainingState.Accepted),
            With(TrainingState.Assigned),
            With(TrainingState.Scheduled, heldBefore.AddHours(3)),
            With(TrainingState.Scheduled, heldBefore.AddHours(-3)),
            With(TrainingState.Completed),
            With(TrainingState.Rejected),
            With(TrainingState.Cancelled),
            With(TrainingState.Closed),
            With(TrainingState.NoShow),
        ];

        Assert.Equal([0], View(StaffQueue.ToApprove));
        Assert.Equal([1], View(StaffQueue.ToAssign));
        Assert.Equal([2, 3], View(StaffQueue.InProgress));
        Assert.Equal([4], View(StaffQueue.ToClose));
        Assert.Equal([5, 6, 7, 8, 9], View(StaffQueue.History));

        // Every training is in exactly one view, and a view that does not exist is none.
        Assert.Equal(all.Length, StaffQueue.All.Sum(view => View(view).Length));
        Assert.Null(StaffQueue.Narrow(all.AsQueryable(), "someday", heldBefore));

        int[] View(string view) =>
            [.. StaffQueue.Narrow(all.AsQueryable(), view, heldBefore)!.Select(training => Array.IndexOf(all, training))];

        static Training With(TrainingState state, DateTime? start = null) =>
            new() { Kind = RatingKind.Atc, Rating = 12, TraineeVid = TraineeVid, State = state, ScheduledStartUtc = start };
    }

    [Fact]
    public void TheScopeOfTheTrainersGrantIsReadBackToItsTraining()
    {
        Assert.Equal("training:training:42", Training.ScopeOf(42));
        Assert.Equal(42, Training.IdOf(Training.ScopeOf(42)));

        // Another module's scope, another kind of row, or no number: not a training.
        Assert.Null(Training.IdOf("flightops:tour:42"));
        Assert.Null(Training.IdOf("training:exam:42"));
        Assert.Null(Training.IdOf("training:training:"));
        Assert.Null(Training.IdOf("training:training:-1"));
        Assert.Null(Training.IdOf(null));
    }

    private static Training Accepted(RatingKind kind, int rating) =>
        new() { Kind = kind, Rating = rating, TraineeVid = TraineeVid, State = TrainingState.Accepted };

    private static TrainerFacts Staff(int vid, int? atc = null, int? pilot = null) =>
        new(vid, IsTrainingStaff: true, atc, pilot);
}
