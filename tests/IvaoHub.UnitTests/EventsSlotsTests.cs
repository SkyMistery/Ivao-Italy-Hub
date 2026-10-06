using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Events.Staff;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The public slots of an event loaded from a table (design M4 §1.5, §3.1, E5), in the pieces that need no database: the reader of
/// the table — pasted from a spreadsheet, separated by tabs, or a CSV with its quotes, by commas or semicolons —, a row read as a
/// slot, the direction read off the two airports, and the rotations case by case. The airports and the types are invented: the
/// module writes none of its own, and a test does not need a real one.
/// </summary>
public sealed class EventsSlotsTests
{
    private const string Header = "callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand";

    private static readonly string[] EventAirports = ["XED1", "XED2"];

    // ---- the reader ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ATablePastedFromASpreadsheetIsReadByItsTabsWithEveryRowNumberedAsTheSheetNumbersIt()
    {
        var sheet = SlotSheet.Read(
            $"{Header}\trotation\tleg\r\n"
            + "XEA101\tXA101\tXE5A/XE5B\tXED1\t2026-11-21 17:00\tXED3\t2026-11-21 18:10\tB12\tR1\t1\r\n"
            + "\t\t\t\t\t\t\t\t\t\r\n"
            + "XEA102\t\tXE5A\tXED3\t2026-11-21 18:40\tXED1\t2026-11-21 19:50\t\tR1\t2\r\n");

        Assert.True(sheet.Problems.IsEmpty);
        Assert.Equal(1, sheet.HeaderRow);
        Assert.Equal([2, 4], sheet.Rows.Select(row => row.Number));
        Assert.Equal("XE5A/XE5B", sheet.Rows[0][SlotColumns.AircraftTypes]);
        Assert.Equal("B12", sheet.Rows[0][SlotColumns.Stand]);
        Assert.Equal(string.Empty, sheet.Rows[1][SlotColumns.FlightNumber]);
        Assert.Equal("2", sheet.Rows[1][SlotColumns.Leg]);
    }

    [Fact]
    public void ACsvIsReadWithItsQuotesBySemicolonsOrByCommasAndInAnyOrderOfTheColumns()
    {
        // A spreadsheet that writes the comma as the decimal separator saves its CSV with semicolons; a quoted cell holds the
        // separator, a doubled quote and a line break.
        var semicolons = SlotSheet.Read(
            (char)0xFEFF
            + "Stand;Callsign;FLIGHT_NUMBER;aircraft_types;departure_icao;off_block_utc;arrival_icao;on_block_utc;notes\n"
            + "\"Apron; north\";XEA201;XA201;XE5A;XED1;2026-11-21T17:00Z;XED3;2026-11-21T18:10Z;\"said \"\"later\"\"\nand more\"\n");

        Assert.True(semicolons.Problems.IsEmpty);
        var row = Assert.Single(semicolons.Rows);
        Assert.Equal(2, row.Number);
        Assert.Equal("Apron; north", row[SlotColumns.Stand]);
        Assert.Equal("XEA201", row[SlotColumns.Callsign]);
        Assert.Equal("2026-11-21T18:10Z", row[SlotColumns.OnBlockUtc]);
        // A column the hub does not know is left alone.
        Assert.False(row.Cells.ContainsKey("notes"));

        var commas = SlotSheet.Read(
            "callsign,flight_number,aircraft_types,departure_icao,off_block_utc,arrival_icao,on_block_utc,stand\n"
            + "XEA202,\"XA,202\",XE5A,XED1,2026-11-21 17:00,XED3,2026-11-21 18:10,\n");

        Assert.True(commas.Problems.IsEmpty);
        Assert.Equal("XA,202", Assert.Single(commas.Rows)[SlotColumns.FlightNumber]);
    }

    [Fact]
    public void EmptyRowsAreSkippedAndCountedAndTheHeaderIsTheFirstRowWithSomethingInIt()
    {
        var sheet = SlotSheet.Read(
            $"\n  \n{Header}\n\nXEA301\t\tXE5A\tXED1\t2026-11-21 17:00\tXED3\t2026-11-21 18:10\t\n\n\n");

        Assert.True(sheet.Problems.IsEmpty);
        Assert.Equal(3, sheet.HeaderRow);
        Assert.Equal(5, Assert.Single(sheet.Rows).Number);
    }

    [Fact]
    public void ATableWithoutItsColumnsOrWithoutRowsIsRefusedAsAWhole()
    {
        // No text, or only blanks.
        Assert.Contains("errors.required", Errors(SlotSheet.Read("  \n \t\n"))[SlotSheet.TextField]);

        // A column missing, another written twice: on the header's row, under the column.
        var errors = Errors(SlotSheet.Read("callsign\tcallsign\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\n"));
        Assert.Contains("events:errors.sheetColumnTwice", errors["rows[1].callsign"]);
        Assert.Contains("events:errors.sheetColumnMissing", errors["rows[1].flight_number"]);
        Assert.Contains("events:errors.sheetColumnMissing", errors["rows[1].stand"]);
        Assert.False(errors.ContainsKey("rows[1].rotation"));

        // A header and nothing under it.
        Assert.Contains("events:errors.sheetEmpty", Errors(SlotSheet.Read($"{Header}\n\n"))[SlotSheet.TextField]);

        // More rows than a table of slots has.
        var many = string.Concat(Enumerable.Repeat("XEA1\t\tXE5A\tXED1\t2026-11-21 17:00\tXED3\t2026-11-21 18:10\t\n", SlotSheet.MaxRows + 1));
        Assert.Contains("events:errors.sheetTooLong", Errors(SlotSheet.Read($"{Header}\n{many}"))[SlotSheet.TextField]);
    }

    // ---- a row as a slot --------------------------------------------------------------------------------------------------

    [Fact]
    public void ARowIsReadAsASlotInUpperCaseWithItsTypesOnceAndItsTimesInUtc()
    {
        var problems = new Refusals();
        var draft = SlotDraft.Read(
            Row(7, ("callsign", " xea401 "), ("flight_number", "xa 401"), ("aircraft_types", "xe5a / XE5B/xe5a"),
                ("departure_icao", "xed1"), ("off_block_utc", "2026-11-21 17:05:30"), ("arrival_icao", "XED3"),
                ("on_block_utc", "2026-11-21T18:10Z"), ("stand", "B 12"), ("rotation", "R7"), ("leg", "3")),
            problems);

        Assert.True(problems.IsEmpty);
        Assert.NotNull(draft);
        Assert.Equal(("XEA401", "XA 401", "XED1", "XED3"), (draft.Callsign, draft.FlightNumber, draft.DepartureIcao, draft.ArrivalIcao));
        Assert.Equal(["XE5A", "XE5B"], draft.AircraftTypes);
        Assert.Equal(new DateTime(2026, 11, 21, 17, 5, 30, DateTimeKind.Utc), draft.OffBlockUtc);
        Assert.Equal(DateTimeKind.Utc, draft.OnBlockUtc.Kind);
        Assert.Equal(("B 12", "R7", (int?)3), (draft.Stand, draft.Rotation, draft.Leg));
    }

    [Fact]
    public void EveryCellARowCannotSayIsRefusedOnItsCellAndTheRowIsNoSlot()
    {
        var problems = new Refusals();
        var draft = SlotDraft.Read(
            Row(12, ("callsign", "XE A1"), ("aircraft_types", "XE5A,XE5B"), ("departure_icao", "XED1"),
                ("off_block_utc", "21/11/2026 17:00"), ("arrival_icao", "xed1"), ("on_block_utc", "17:30"), ("leg", "0")),
            problems);

        Assert.Null(draft);
        var errors = problems.Errors;
        Assert.Contains("events:errors.callsignFormat", errors["rows[12].callsign"]);
        Assert.Contains("events:errors.aircraftFormat", errors["rows[12].aircraft_types"]);
        Assert.Contains("events:errors.slotToItself", errors["rows[12].arrival_icao"]);
        Assert.Contains("events:errors.instantFormat", errors["rows[12].off_block_utc"]);
        Assert.Contains("events:errors.instantFormat", errors["rows[12].on_block_utc"]);
        Assert.Contains("events:errors.legRange", errors["rows[12].leg"]);
        Assert.Contains("events:errors.legWithoutRotation", errors["rows[12].leg"]);

        var empty = new Refusals();
        Assert.Null(SlotDraft.Read(Row(3), empty));
        foreach (var column in new[] { "callsign", "aircraft_types", "departure_icao", "off_block_utc", "arrival_icao", "on_block_utc" })
        {
            Assert.Contains("errors.required", empty.Errors[$"rows[3].{column}"]);
        }

        // An on block that is not after the off block, and an airport that is not one.
        var backwards = new Refusals();
        Assert.Null(SlotDraft.Read(
            Row(4, ("callsign", "XEA4"), ("aircraft_types", "XE5A"), ("departure_icao", "XED"), ("off_block_utc", "2026-11-21 18:00"),
                ("arrival_icao", "XED3"), ("on_block_utc", "2026-11-21 18:00")),
            backwards));
        Assert.Contains("events:errors.onBlockBeforeOffBlock", backwards.Errors["rows[4].on_block_utc"]);
        Assert.Contains("events:errors.airportUnknown", backwards.Errors["rows[4].departure_icao"]);
    }

    [Theory]
    [InlineData("2026-11-21 17:00", true)]
    [InlineData("2026-11-21T17:00", true)]
    [InlineData("2026-11-21T17:00:59Z", true)]
    [InlineData(" 2026-11-21 17:00 ", true)]
    [InlineData("2026-02-30 17:00", false)]
    [InlineData("2026-11-21 25:00", false)]
    [InlineData("21/11/2026 17:00", false)]
    [InlineData("11/21/2026 17:00", false)]
    [InlineData("17:00", false)]
    [InlineData("2026-11-21", false)]
    [InlineData("2026-11-21 17:00+02:00", false)]
    public void AnInstantIsWrittenInUtcInOneShapeOnly(string cell, bool read) => Assert.Equal(read, SlotValues.Instant(cell) is not null);

    // ---- the direction ----------------------------------------------------------------------------------------------------

    [Fact]
    public void TheDirectionIsReadOffTheTwoAirports()
    {
        Assert.Equal(new SlotDirection("XED1", IsArrival: false), SlotDirection.Of("XED1", "XED3", EventAirports));
        Assert.Equal(new SlotDirection("XED2", IsArrival: true), SlotDirection.Of("XED3", "XED2", EventAirports));

        // Between two airports of the event: the departure from the first, whose off block is the slot's time there.
        Assert.Equal(new SlotDirection("XED1", IsArrival: false), SlotDirection.Of("XED1", "XED2", EventAirports));
        Assert.Equal(new SlotDirection("XED2", IsArrival: false), SlotDirection.Of("XED2", "XED1", EventAirports));

        // Away from the event: no slot of it.
        Assert.Null(SlotDirection.Of("XED3", "XED4", EventAirports));
    }

    // ---- the rotations ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARotationOutAndBackWithItsPlacesPasses()
    {
        var check = SlotChains.Check(
        [
            Leg(2, "R1", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "R1", 2, "XED3", "XED1", At(18, 20), At(19, 30)),
            Leg(4, "R1", 3, "XED1", "XED4", At(19, 40), At(21, 0)),
        ],
            gapMinutes: 10);

        Assert.Empty(check.Problems);
        Assert.Empty(check.Assigned);
    }

    [Fact]
    public void LegsWrittenWithoutPlacesTakeThemFromTheirTimes()
    {
        var check = SlotChains.Check(
        [
            Leg(5, "R2", leg: null, "XED3", "XED1", At(18, 20), At(19, 30)),
            Leg(4, "R2", leg: null, "XED1", "XED3", At(17, 0), At(18, 10)),
        ],
            gapMinutes: 10);

        Assert.Empty(check.Problems);
        Assert.Equal(1, check.Assigned[4]);
        Assert.Equal(2, check.Assigned[5]);
    }

    [Fact]
    public void ALegThatDoesNotLeaveFromWhereTheOneBeforeArrivedBreaksTheChain()
    {
        var check = SlotChains.Check(
        [
            Leg(2, "R3", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "R3", 2, "XED4", "XED1", At(18, 20), At(19, 30)),
        ],
            gapMinutes: 10);

        var problem = Assert.Single(check.Problems);
        Assert.Equal((3, SlotColumns.DepartureIcao, "events:errors.chainBroken"), ((int)problem.Leg.Key, problem.Column, problem.Key));
    }

    [Fact]
    public void ALegThatLeavesBeforeTheOneBeforeItIsOutOfOrder()
    {
        var check = SlotChains.Check(
        [
            Leg(2, "R4", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "R4", 2, "XED3", "XED1", At(16, 0), At(16, 50)),
        ],
            gapMinutes: 10);

        var problem = Assert.Single(check.Problems);
        Assert.Equal((3, SlotColumns.OffBlockUtc, "events:errors.chainOrder"), ((int)problem.Leg.Key, problem.Column, problem.Key));
    }

    [Theory]
    [InlineData(10, 18, 19, true)]
    [InlineData(10, 18, 20, false)]
    [InlineData(0, 18, 10, false)]
    [InlineData(0, 18, 9, true)]
    public void TheNextLegLeavesAtLeastTheMinutesBetweenTwoBookingsAfterTheOneBeforeArrived(
        int gapMinutes,
        int hour,
        int minute,
        bool tooClose)
    {
        var check = SlotChains.Check(
        [
            Leg(2, "R5", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "R5", 2, "XED3", "XED1", At(hour, minute), At(hour + 1, minute)),
        ],
            gapMinutes);

        if (tooClose)
        {
            var problem = Assert.Single(check.Problems);
            Assert.Equal((3, SlotColumns.OffBlockUtc, "events:errors.chainTooClose"), ((int)problem.Leg.Key, problem.Column, problem.Key));
        }
        else
        {
            Assert.Empty(check.Problems);
        }
    }

    [Fact]
    public void PlacesWrittenForSomeLegsAndNotForOthersOrTwiceAreRefused()
    {
        var some = SlotChains.Check(
        [
            Leg(2, "R6", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "R6", leg: null, "XED3", "XED1", At(18, 20), At(19, 30)),
        ],
            gapMinutes: 10);
        Assert.Equal((3, SlotColumns.Leg, "events:errors.legMissing"), Single(some));

        var twice = SlotChains.Check(
        [
            Leg(2, "R7", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "R7", 1, "XED3", "XED1", At(18, 20), At(19, 30)),
        ],
            gapMinutes: 10);
        Assert.Equal((3, SlotColumns.Leg, "events:errors.legTwice"), Single(twice));
    }

    [Fact]
    public void ARefusalLandsOnTheLegBeingWrittenAndNeverOnOneAlreadyStored()
    {
        // A stored rotation of two legs, and a third written now that leaves from the wrong airport.
        var stored = new[]
        {
            Leg(1001L, "R8", 1, "XED1", "XED3", At(17, 0), At(18, 10), isNew: false),
            Leg(1002L, "R8", 2, "XED3", "XED1", At(18, 20), At(19, 30), isNew: false),
        };

        var after = SlotChains.Check([.. stored, Leg(9, "R8", 3, "XED4", "XED3", At(19, 45), At(21, 0))], gapMinutes: 10);
        Assert.Equal((9, SlotColumns.DepartureIcao, "events:errors.chainBroken"), Single(after));

        // Written before a stored leg, it is refused on its own end: its arrival, or its on block.
        var before = SlotChains.Check(
        [
            Leg(1003L, "R9", 2, "XED3", "XED1", At(18, 20), At(19, 30), isNew: false),
            Leg(8, "R9", 1, "XED1", "XED4", At(17, 0), At(18, 15)),
        ],
            gapMinutes: 10);
        Assert.Equal((8, SlotColumns.ArrivalIcao, "events:errors.chainBroken"), Single(before));

        // A new leg on a place a stored one has, and without a place where the stored ones have theirs.
        var taken = SlotChains.Check([.. stored, Leg(7, "R8", 2, "XED3", "XED1", At(18, 20), At(19, 30))], gapMinutes: 10);
        Assert.Equal((7, SlotColumns.Leg, "events:errors.legTwice"), Single(taken));
        var unplaced = SlotChains.Check([.. stored, Leg(6, "R8", leg: null, "XED1", "XED3", At(20, 0), At(21, 0))], gapMinutes: 10);
        Assert.Equal((6, SlotColumns.Leg, "events:errors.legMissing"), Single(unplaced));

        // Two stored legs that disagree are what they are: nothing to refuse in a write that does not touch them.
        var old = SlotChains.Check(
        [
            Leg(1004L, "R10", 1, "XED1", "XED3", At(17, 0), At(18, 10), isNew: false),
            Leg(1005L, "R10", 2, "XED4", "XED1", At(18, 0), At(19, 0), isNew: false),
        ],
            gapMinutes: 10);
        Assert.Empty(old.Problems);
    }

    [Fact]
    public void EachRotationIsCheckedOnItsOwnAndALegAloneIsAChainOfOne()
    {
        var check = SlotChains.Check(
        [
            Leg(2, "A", 1, "XED1", "XED3", At(17, 0), At(18, 10)),
            Leg(3, "B", 1, "XED4", "XED2", At(17, 30), At(18, 30)),
            Leg(4, "A", 2, "XED3", "XED1", At(18, 20), At(19, 30)),
            Leg(5, "a", 1, "XED2", "XED3", At(9, 0), At(10, 0)),
        ],
            gapMinutes: 10);

        Assert.Empty(check.Problems);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------------

    private static DateTime At(int hour, int minute) =>
        new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc).AddHours(hour).AddMinutes(minute);

    private static ChainLeg Leg(
        object key,
        string rotation,
        int? leg,
        string departure,
        string arrival,
        DateTime offBlock,
        DateTime onBlock,
        bool isNew = true) =>
        new(key, isNew, rotation, leg, departure, arrival, offBlock, onBlock);

    private static (int Key, string Column, string Problem) Single(ChainCheck check)
    {
        var problem = Assert.Single(check.Problems);
        return ((int)problem.Leg.Key, problem.Column, problem.Key);
    }

    private static SlotSheetRow Row(int number, params (string Column, string Cell)[] cells) =>
        new(number, cells.ToDictionary(cell => cell.Column, cell => cell.Cell.Trim(), StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, string[]> Errors(SlotSheet sheet) => sheet.Problems.Errors;
}
