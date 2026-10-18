using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.Events.Reports;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of the reports of support (M4, E14a; design §5.2, §5.3; note 2026-10-17-il-pirep-di-supporto, readings 3–7): which event takes a
/// report and until when, what an item may say, which session a flight flew, what the items make of the report, the decisions a report may
/// take, and the range of <c>reportDays</c>.
/// </summary>
public sealed class EventsReportsTests
{
    private static readonly DateTime Start = new(2001, 1, 1, 16, 0, 0, DateTimeKind.Utc);

    private static readonly HashSet<string> Known = new(["XEA1", "XEA2", "XEA3", "XEA4"], StringComparer.Ordinal);

    [Fact]
    public void AnEventOverTakesReportsForReportDaysFromItsEnd()
    {
        var row = Evening();

        Assert.False(ReportRules.IsOpen(row, 14, Start.AddHours(1)));
        Assert.True(ReportRules.IsOpen(row, 14, Start.AddHours(2)));
        Assert.True(ReportRules.IsOpen(row, 14, Start.AddHours(2).AddDays(14).AddTicks(-1)));
        Assert.False(ReportRules.IsOpen(row, 14, Start.AddHours(2).AddDays(14)));
        Assert.Equal(Start.AddHours(2).AddDays(3), ReportRules.ClosesAt(row, 3));

        // A draft, a cancelled event and one in person take none.
        Assert.False(ReportRules.IsOpen(Evening(status: PublishStatus.Draft), 14, Start.AddDays(1)));
        Assert.False(ReportRules.IsOpen(Evening(cancelled: true), 14, Start.AddDays(1)));
        Assert.False(ReportRules.IsOpen(Evening(inPerson: true), 14, Start.AddDays(1)));
    }

    [Fact]
    public void AFlightHasItsAirportsTouchesTheEventAndFlewInItsWindow()
    {
        var problems = new Refusals();
        var drafts = ReportRules.Check(
            ReportKind.Pilot,
            Evening(),
            ["XEA1"],
            Known,
            [
                new ReportItemRequest(" tst1 ", "xea1", "XEA2", Start, null, null),
                new ReportItemRequest("TST2", "XEA2", "XEA1", Start.AddHours(4), null, null),
            ],
            problems);

        Assert.True(problems.IsEmpty);
        Assert.NotNull(drafts);
        Assert.Equal(("TST1", "XEA1", "XEA2", DateOnly.FromDateTime(Start)), (drafts[0].Callsign, drafts[0].DepartureIcao, drafts[0].ArrivalIcao, drafts[0].FlightDay));

        // The refusals, each on its field: no callsign, the same airport twice, none of the event's, one the hub does not know, a day out of
        // the window of flights, no day.
        problems = new Refusals();
        Assert.Null(ReportRules.Check(
            ReportKind.Pilot,
            Evening(),
            ["XEA1"],
            Known,
            [
                new ReportItemRequest(null, "XEA1", "XEA1", Start, null, null),
                new ReportItemRequest("TST2", "XEA3", "XEA4", Start, null, null),
                new ReportItemRequest("TST3", "XEA1", "ZZZZ", Start.AddDays(2), null, null),
                new ReportItemRequest("TST4", "XEA1", "XEA2", null, null, null),
            ],
            problems));
        Assert.Equal(["errors.required"], problems.Errors["items[0].callsign"]);
        Assert.Equal([ReportRules.SameAirportKey], problems.Errors["items[0].arrivalIcao"]);
        Assert.Equal([ReportRules.NotAtTheEventKey], problems.Errors["items[1].arrivalIcao"]);
        Assert.Equal([ReportRules.AirportUnknownKey], problems.Errors["items[2].arrivalIcao"]);
        Assert.Equal([ReportRules.OutsideTheEventKey], problems.Errors["items[2].flightDay"]);
        Assert.Equal(["errors.required"], problems.Errors["items[3].flightDay"]);

        // An event of the whole division, or one without airports, takes a flight anywhere.
        Assert.NotNull(ReportRules.Check(ReportKind.Pilot, Evening(wholeDivision: true), ["XEA1"], Known, [new ReportItemRequest("T", "XEA3", "XEA4", Start, null, null)], new Refusals()));
        Assert.NotNull(ReportRules.Check(ReportKind.Pilot, Evening(), [], Known, [new ReportItemRequest("T", "XEA3", "XEA4", Start, null, null)], new Refusals()));
    }

    [Fact]
    public void AShiftEndsAfterItStartsWithinTheEventAndMeetsNoOther()
    {
        var problems = new Refusals();
        Assert.Null(ReportRules.Check(
            ReportKind.Atc,
            Evening(),
            [],
            Known,
            [
                new ReportItemRequest("XEA1_TWR", null, null, null, Start, Start.AddHours(1)),
                new ReportItemRequest("XEA1_APP", null, null, null, Start.AddMinutes(30), Start.AddHours(2)),
                new ReportItemRequest("XEA1_GND", null, null, null, Start.AddHours(2), Start.AddHours(1)),
                new ReportItemRequest("XEA1_DEL", null, null, null, Start.AddHours(-5), Start.AddHours(8)),
                new ReportItemRequest("XEA1_CTR", null, null, null, Start.AddDays(1), Start.AddDays(1).AddHours(1)),
                new ReportItemRequest(new string('X', EventReportItem.MaxCallsignLength + 1), null, null, null, null, null),
            ],
            problems));

        Assert.Equal([ReportRules.OverlapKey], problems.Errors["items[1].fromUtc"]);
        Assert.Equal([ReportRules.EndsBeforeItStartsKey], problems.Errors["items[2].toUtc"]);
        Assert.Equal([ReportRules.TooLongKey], problems.Errors["items[3].toUtc"]);
        Assert.Equal([ReportRules.OutsideTheEventKey], problems.Errors["items[4].fromUtc"]);
        Assert.Equal(["errors.text.tooLong"], problems.Errors["items[5].callsign"]);
        Assert.Equal(["errors.required"], problems.Errors["items[5].fromUtc"]);

        // None and too many.
        Assert.Null(ReportRules.Check(ReportKind.Atc, Evening(), [], Known, [], problems = new Refusals()));
        Assert.Equal([ReportRules.NoItemsKey], problems.Errors["items"]);
        var many = Enumerable.Repeat(new ReportItemRequest("A", null, null, null, Start, Start.AddMinutes(1)), ReportRules.MaxItems + 1).ToList();
        Assert.Null(ReportRules.Check(ReportKind.Atc, Evening(), [], Known, many, problems = new Refusals()));
        Assert.Equal([ReportRules.TooManyItemsKey], problems.Errors["items"]);
    }

    [Fact]
    public void AnInstantSentWithoutItsZoneIsReadAsUtcNeverAsTheServersTime()
    {
        // The review of #289, point 2: what the client sends without a zone is UTC, whatever the host's zone.
        var unspecified = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);
        var drafts = ReportRules.Check(
            ReportKind.Atc,
            Evening(),
            [],
            Known,
            [new ReportItemRequest("XEA1_TWR", null, null, null, unspecified, unspecified.AddHours(1))],
            new Refusals());

        Assert.NotNull(drafts);
        Assert.Equal((Start, DateTimeKind.Utc), (drafts[0].FromUtc!.Value, drafts[0].FromUtc!.Value.Kind));

        var flight = ReportRules.Check(
            ReportKind.Pilot,
            Evening(),
            [],
            Known,
            [new ReportItemRequest("T", "XEA1", "XEA2", DateTime.SpecifyKind(Start.Date, DateTimeKind.Unspecified), null, null)],
            new Refusals());
        Assert.Equal(DateOnly.FromDateTime(Start), flight![0].FlightDay);
    }

    [Fact]
    public void EveryFlightTakesTheSessionWithItsCallsignFirstAndNoSessionTwice()
    {
        // The review of #289, point 4: Y at six, Z at eight; items «X» then «Y». Item by item, X took Y and Y got Z.
        NetworkSession y = new(1, 7, "Y", Start.AddHours(2), Start.AddHours(3));
        NetworkSession z = new(2, 7, "Z", Start.AddHours(4), Start.AddHours(5));
        Assert.Equal([2L, 1L], ReportRules.Flown([("X", [y, z]), ("y", [y, z])]).Select(session => session!.Id));

        // Without a callsign of its own a flight takes the first that started; a session is never given twice; none left is none.
        NetworkSession other = new(3, 7, "OTHER", Start, Start.AddHours(1));
        Assert.Equal([3L, 1L, null], ReportRules.Flown([("A", [y, other]), ("B", [y, other]), ("C", [y, other])]).Select(session => session?.Id));

        // A source that did not answer gives nothing to its flight, and takes nothing from the others.
        Assert.Equal([null, 1L], ReportRules.Flown([("Y", null), ("Y", [y])]).Select(session => session?.Id));
    }

    [Fact]
    public void AFlightsDayIsCutToTheWindowOfTheEvent()
    {
        // The evening ends at six: its window ends at midnight, six hours later, and starts at ten in the morning.
        Assert.Equal((Start.AddHours(-6), Start.AddHours(8)), ReportRules.DayWindow(Evening(), DateOnly.FromDateTime(Start)));
    }

    [Fact]
    public void TheItemsMakeTheReportValidPartialInvalidOrUnavailable()
    {
        Assert.Equal(ReportVerification.Valid, ReportRules.Verification([true, true]));
        Assert.Equal(ReportVerification.Partial, ReportRules.Verification([true, false]));
        Assert.Equal(ReportVerification.Invalid, ReportRules.Verification([false, false]));
        Assert.Equal(ReportVerification.Unavailable, ReportRules.Verification([true, null]));
        Assert.Equal(ReportVerification.Unavailable, ReportRules.Verification([]));
    }

    [Fact]
    public void AReportWaitingIsAcceptedOrRejectedAndADecidedOneTakesTheOther()
    {
        Assert.True(ReportDecisions.Allowed(ReportState.Submitted, ReportState.Accepted));
        Assert.True(ReportDecisions.Allowed(ReportState.Submitted, ReportState.Rejected));
        Assert.True(ReportDecisions.Allowed(ReportState.Accepted, ReportState.Rejected));
        Assert.True(ReportDecisions.Allowed(ReportState.Rejected, ReportState.Accepted));
        Assert.False(ReportDecisions.Allowed(ReportState.Accepted, ReportState.Accepted));
        Assert.All(Enum.GetValues<ReportState>(), from => Assert.False(ReportDecisions.Allowed(from, ReportState.Submitted)));
    }

    [Fact]
    public void AnEventStartedMoreThanHalfAnHourAgoIsKept()
    {
        // Carmine's answer on reading 13 (#289): half an hour after its start, not a tick more.
        var row = Evening();
        Assert.False(EventSaving.StartedTooLongAgo(row, Start.AddMinutes(-5)));
        Assert.False(EventSaving.StartedTooLongAgo(row, Start.AddMinutes(30)));
        Assert.True(EventSaving.StartedTooLongAgo(row, Start.AddMinutes(30).AddTicks(1)));
    }

    [Fact]
    public void ReportDaysIsFourteenFromOneToSixty()
    {
        Assert.Equal(14, new EventsSettings().ReportDays);

        var validator = new EventsSettingsValidator();
        Assert.True(validator.Validate(new EventsSettings { ReportDays = 1 }).IsValid);
        Assert.True(validator.Validate(new EventsSettings { ReportDays = EventsSettingsValidator.MaxReportDays }).IsValid);
        Assert.False(validator.Validate(new EventsSettings { ReportDays = 0 }).IsValid);
        Assert.False(validator.Validate(new EventsSettings { ReportDays = EventsSettingsValidator.MaxReportDays + 1 }).IsValid);
    }

    /// <summary>An evening of two hours, published, over at six.</summary>
    private static Event Evening(
        PublishStatus status = PublishStatus.Published,
        bool cancelled = false,
        bool inPerson = false,
        bool wholeDivision = false) =>
        new()
        {
            Id = 1,
            Slug = "evening",
            StartsAtUtc = Start,
            EndsAtUtc = Start.AddHours(2),
            Status = status,
            CancelledAt = cancelled ? Start.AddDays(-1) : null,
            InPerson = inPerson,
            WholeDivision = wholeDivision,
        };
}
