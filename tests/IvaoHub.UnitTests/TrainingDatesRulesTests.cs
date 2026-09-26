using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Settings;
using IvaoHub.Modules.Training.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the date of a training (M3, A8; design M3 §1.2, §2.5, §5.1, §5.3) with no database: the days a date touches in the
/// division's time zone; the warnings — another training with its session that day, whoever trains it, and an entry of the calendar
/// of a kind the division checks, the trainings' own sessions never twice —; what the three policies make of them; a session shown
/// as held from the day after its own, there; the session in the calendar with its rating and position and nobody's name; and which
/// trainings the reminder and the closing by time pick.
/// <para>The queries are the ones the database is asked, run here on lists. The ratings are a vocabulary of this test's making, not
/// the network's (design M3 §10).</para>
/// </summary>
public sealed class TrainingDatesRulesTests
{
    private static readonly RatingVocabulary Ratings = new(
    [
        new(RatingKind.Atc, 11, "A1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Atc, 12, "A2", HasPracticalTraining: true, PositionType: "BOX"),
        new(RatingKind.Pilot, 21, "P1", HasPracticalTraining: false, PositionType: null),
        new(RatingKind.Pilot, 22, "P2", HasPracticalTraining: true, PositionType: null),
    ]);

    /// <summary>Two hours ahead of UTC: a day there begins at 22:00 UTC of the day before.</summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("trn-test+2", TimeSpan.FromHours(2), "trn-test+2", "trn-test+2");

    private static readonly DateTime Day = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ADateTouchesTheDaysOfTheDivisionItFallsOn()
    {
        // 20:00–21:00 UTC is 22:00–23:00 there: the 10th, which there runs from 22:00 UTC of the 9th.
        Assert.Equal((Day.AddHours(-2), Day.AddHours(22)), DivisionDays.Touched(Day.AddHours(20), Day.AddHours(21), Zone));

        // 21:30–23:00 UTC crosses midnight there: the 10th and the 11th.
        Assert.Equal((Day.AddHours(-2), Day.AddHours(46)), DivisionDays.Touched(Day.AddHours(21.5), Day.AddHours(23), Zone));

        // Ending at midnight there does not touch the day after; with no end, the day of the start.
        Assert.Equal((Day.AddHours(-2), Day.AddHours(22)), DivisionDays.Touched(Day.AddHours(20), Day.AddHours(22), Zone));
        Assert.Equal((Day.AddHours(22), Day.AddHours(46)), DivisionDays.Touched(Day.AddHours(22.5), null, Zone));

        // In UTC the day is UTC's.
        Assert.Equal((Day, Day.AddDays(1)), DivisionDays.Touched(Day.AddHours(20), null, TimeZoneInfo.Utc));
    }

    [Fact]
    public void AnotherTrainingWithItsSessionThatDayIsAWarningWhoeverTrainsIt()
    {
        var (from, to) = DivisionDays.Touched(Day.AddHours(16), Day.AddHours(18), Zone);
        Training[] all =
        [
            Dated(1, Day.AddHours(10)),                                // this one: never a warning of itself
            Dated(2, Day.AddHours(8), trainer: 5001),                  // the same day there, another trainer
            Dated(3, Day.AddHours(-1), trainer: 5002),                 // 01:00 of the 10th there, still the 9th in UTC
            Dated(4, Day.AddHours(22)),                                // 00:00 of the 11th there: the day after
            Dated(5, Day.AddHours(12), state: TrainingState.Closed),   // closed: no session any more
            Dated(6, null, state: TrainingState.Assigned),             // no date yet
        ];

        var found = DateConflicts.Sessions(all.AsQueryable(), except: 1, from, to).Select(training => training.Id).Order().ToList();
        Assert.Equal([2L, 3L], found);

        // As the trainer reads it: when, and the training by its rating and position — nobody's name, nobody's VID.
        var warnings = DateConflicts.Warnings(all.Where(training => found.Contains(training.Id)), [], Ratings);
        Assert.Equal([3L, 2L], warnings.Select(warning => warning.TrainingId!.Value));
        var first = warnings[0];
        Assert.Equal((DateWarningKind.Training, Day.AddHours(-1), RatingKind.Atc, "A2", "TRNTEST_BOX"), (first.Kind, first.StartsAtUtc, first.TrainingKind!.Value, first.RatingShortName, first.Position));
        Assert.Null(first.Title);
        Assert.DoesNotContain("5002", DateWarnings.Write(warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEntryOfTheCalendarOfAKindCheckedThatTouchesTheDayIsAWarning()
    {
        var (from, to) = DivisionDays.Touched(Day.AddHours(16), Day.AddHours(18), Zone);
        CalendarEntry[] all =
        [
            Entry(1, "event", Day.AddHours(19), Day.AddHours(21)),                       // that evening
            Entry(2, "meeting", Day.AddHours(16), null),                                 // a kind nobody checks
            Entry(3, "event", Day.AddHours(22), null),                                   // midnight there: the day after
            Entry(4, "event", Day.AddDays(-2), Day.AddHours(1)),                         // three days, the last one this
            Entry(5, "event", Day.AddDays(-1), Day.AddHours(-2)),                        // ends as the day begins there
            Entry(6, "event", Day.AddHours(-2), null),                                   // starts as the day begins there
            Entry(7, Training.CalendarKind, Day.AddHours(10), null, source: Training.SourceIdOf(42)), // a session: counted as a training
            Entry(8, "exam", Day.AddHours(12), null, source: "exam:3"),                  // the module's, but not a session
        ];

        var found = DateConflicts.Entries(all.AsQueryable(), ["event", Training.CalendarKind, "exam"], from, to)
            .Select(entry => entry.Id).Order().ToList();
        Assert.Equal([1L, 4L, 6L, 8L], found);

        // A kind not checked finds nothing, and no kind at all finds nothing.
        Assert.Empty(DateConflicts.Entries(all.AsQueryable(), ["deadline"], from, to));
        Assert.Empty(DateConflicts.Entries(all.AsQueryable(), [], from, to));

        // As the trainer reads it: the kind, when, the title in every language, where it is read.
        var warning = DateConflicts.Warnings([], all.Where(entry => entry.Id == 1), Ratings).Single();
        Assert.Equal((DateWarningKind.Calendar, "event", Day.AddHours(19), Day.AddHours(21), "/events/1"), (warning.Kind, warning.CalendarKind, warning.StartsAtUtc, warning.EndsAtUtc!.Value, warning.Url));
        Assert.Equal("trn-test 1", warning.Title!.Get("en"));
        Assert.Null(warning.TrainingId);
    }

    [Fact]
    public void ThePolicyWarnsAndAsksForAConfirmationBlocksOrLeavesAlone()
    {
        // Nothing found: nothing to say, whatever the policy.
        foreach (var policy in Enum.GetValues<ConflictPolicy>())
        {
            Assert.Null(DateConflicts.Refusal(policy, warnings: 0, confirmed: false));
        }

        // Warn: shown, and written once confirmed.
        Assert.Equal(DateConflicts.NotConfirmed, DateConflicts.Refusal(ConflictPolicy.Warn, warnings: 2, confirmed: false));
        Assert.Null(DateConflicts.Refusal(ConflictPolicy.Warn, warnings: 2, confirmed: true));

        // Block: refused, confirmed or not.
        Assert.Equal(DateConflicts.Blocked, DateConflicts.Refusal(ConflictPolicy.Block, warnings: 1, confirmed: false));
        Assert.Equal(DateConflicts.Blocked, DateConflicts.Refusal(ConflictPolicy.Block, warnings: 1, confirmed: true));

        // None: nobody looks — and were something found, it would still say nothing.
        Assert.Null(DateConflicts.Refusal(ConflictPolicy.None, warnings: 3, confirmed: false));
    }

    [Fact]
    public void TheWarningsKeptWithADateAreReadBackAsTheyWereWritten()
    {
        IReadOnlyList<DateWarning> warnings =
        [
            new(DateWarningKind.Training, Day.AddHours(8), null, 7, RatingKind.Pilot, "P2", null, null, null, null),
            new(DateWarningKind.Calendar, Day.AddHours(19), Day.AddHours(21), null, null, null, null, "event", "trn-test".L("trn-test"), "/events/1"),
        ];

        var json = DateWarnings.Write(warnings);

        // The names of the enums, every language of a title: readable without the code next to it.
        Assert.Contains("\"Calendar\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Pilot\"", json, StringComparison.Ordinal);
        Assert.Contains("\"it\"", json, StringComparison.Ordinal);
        Assert.Equal(warnings, DateWarnings.Read(json));
        Assert.Empty(DateWarnings.Read("[]"));
        Assert.Empty(DateWarnings.Read(null));
    }

    [Fact]
    public void ASessionShowsAsHeldFromTheDayAfterItsOwnInTheTimeZoneOfTheDivision()
    {
        // Noon of the 11th in UTC: the 11th there too, which began at 22:00 UTC of the 10th.
        var now = Day.AddHours(36);

        // 23:00 of the 10th there: held. 00:30 of the 11th there — still the 10th in UTC —, and later today: not yet.
        Assert.True(StaffQueue.IsHeld(Dated(1, Day.AddHours(21)), now, Zone));
        Assert.False(StaffQueue.IsHeld(Dated(2, Day.AddHours(22.5)), now, Zone));
        Assert.False(StaffQueue.IsHeld(Dated(3, Day.AddHours(35)), now, Zone));

        // Only a dated training: one that is not, or no longer, dated is not held whatever its column says.
        Assert.False(StaffQueue.IsHeld(Dated(4, Day.AddDays(-3), state: TrainingState.Closed), now, Zone));
        Assert.False(StaffQueue.IsHeld(Dated(5, null), now, Zone));
    }

    [Fact]
    public void TheSessionIsInThePublicCalendarByItsRatingAndPositionWhileTheTrainingIsDated()
    {
        var context = new ProjectionContext(["it", "en"], "it", new BlockDocumentWalker(["it", "en"]), new StubClock(Day));
        var training = Dated(42, Day.AddHours(16), trainer: 5003);
        training.OwnerDepartment = Department.TD;
        training.RatingShortName = "A2";

        var entry = Assert.Single(training.Project(context)!.Calendar);
        Assert.Equal((Training.CalendarKind, Day.AddHours(16), Visibility.Public, Department.TD, "/training/sessions/42"), (entry.Kind, entry.StartsAtUtc, entry.Visibility, entry.OwnerDepartment, entry.Url));
        Assert.Null(entry.EndsAtUtc);
        Assert.False(entry.AllDay);

        // The same title in every language, and nothing that names the trainee or the trainer.
        Assert.Equal("A2 · TRNTEST_BOX", entry.Title.Get("it"));
        Assert.Equal("A2 · TRNTEST_BOX", entry.Title.Get("en"));
        Assert.DoesNotContain("1001", entry.Title.Get("en"), StringComparison.Ordinal);
        Assert.DoesNotContain("5003", entry.Title.Get("en"), StringComparison.Ordinal);
        Assert.Equal("training:42", training.SourceId);

        // A pilot's has no position; a training no longer dated, or not yet, leaves the calendar.
        var pilot = Dated(43, Day.AddHours(16));
        pilot.Kind = RatingKind.Pilot;
        pilot.Position = null;
        pilot.RatingShortName = "P2";
        Assert.Equal("P2", Assert.Single(pilot.Project(context)!.Calendar).Title.Get("en"));
        Assert.Null(Dated(44, Day.AddHours(16), state: TrainingState.Closed).Project(context));
        Assert.Null(Dated(45, null, state: TrainingState.Assigned).Project(context));
    }

    [Fact]
    public void TheReminderPicksTheSessionsAboutToStartThatWereNotRemindedYet()
    {
        var now = Day.AddHours(12);
        var reminded = Dated(3, Day.AddHours(20));
        reminded.RemindedAt = now.AddHours(-1);
        Training[] all =
        [
            Dated(1, Day.AddHours(20)),                                 // in eight hours
            Dated(2, Day.AddHours(36)),                                 // the lead is 24 hours: exactly then, still in
            reminded,                                                   // once is enough
            Dated(4, Day.AddHours(37)),                                 // beyond the lead
            Dated(5, Day.AddHours(11)),                                 // already started
            Dated(6, Day.AddHours(20), state: TrainingState.Closed),    // no session any more
        ];

        var due = TrainingRemindersJob.Due(all.AsQueryable(), now, now.AddHours(24)).Select(training => training.Id).Order();
        Assert.Equal([1L, 2L], due);
    }

    [Fact]
    public void TheNightClosesOnlyTheTrainingsWhoseTraineeLetTheLastDatesProposedWait()
    {
        var before = Day;
        Training[] all =
        [
            Dated(1, null, state: TrainingState.Assigned),   // dates proposed before, none since
            Dated(2, null, state: TrainingState.Assigned),   // a date proposed since: the trainee has time again
            Dated(3, null, state: TrainingState.Assigned),   // nothing proposed yet: not the trainee's to answer
            Dated(4, Day.AddDays(-5)),                       // dated already
        ];
        TrainingSlot[] slots =
        [
            Slot(1, created: before.AddDays(-4)),
            Slot(1, created: before.AddDays(-2)),
            Slot(2, created: before.AddDays(-4)),
            Slot(2, created: before.AddHours(1)),
            Slot(4, created: before.AddDays(-9)),
        ];

        var closed = TrainingDates.Unanswered(all.AsQueryable(), slots.AsQueryable(), before).Select(training => training.Id);
        Assert.Equal([1L], closed);
    }

    private static Training Dated(long id, DateTime? start, TrainingState state = TrainingState.Scheduled, int? trainer = null) => new()
    {
        Id = id,
        Kind = RatingKind.Atc,
        Rating = 12,
        Position = "TRNTEST_BOX",
        TraineeVid = 1001,
        TrainerVid = trainer,
        State = state,
        ScheduledStartUtc = start,
    };

    private static CalendarEntry Entry(long id, string kind, DateTime start, DateTime? end, string? source = null) => new()
    {
        Id = id,
        Kind = kind,
        StartsAtUtc = start,
        EndsAtUtc = end,
        SourceModule = source is null ? ProjectionSource.Core : TrainingModule.ModuleKey,
        SourceId = source ?? $"staff:{id}",
        Url = $"/events/{id}",
        Title = $"trn-test {id}".L($"trn-test {id}"),
    };

    private static TrainingSlot Slot(long trainingId, DateTime created) =>
        new() { TrainingId = trainingId, StartsAtUtc = created.AddDays(10), EndsAtUtc = created.AddDays(10).AddHours(2), CreatedAt = created };

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}
