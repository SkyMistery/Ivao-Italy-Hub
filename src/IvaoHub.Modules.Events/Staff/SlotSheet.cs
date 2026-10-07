using System.Globalization;
using System.Text;
using IvaoHub.Core.Data.Crud;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// The columns of the sheet of the public slots (design M4 §3.1), as its header names them: the same names, in any order and
/// any case. The first eight are required in the header — the cells of a flight number and of a stand may be empty —, the
/// rotation and the place of a leg in it are not. A column the sheet has and the hub does not know is left alone.
/// </summary>
public static class SlotColumns
{
    public const string Callsign = "callsign";
    public const string FlightNumber = "flight_number";
    public const string AircraftTypes = "aircraft_types";
    public const string DepartureIcao = "departure_icao";
    public const string OffBlockUtc = "off_block_utc";
    public const string ArrivalIcao = "arrival_icao";
    public const string OnBlockUtc = "on_block_utc";
    public const string Stand = "stand";
    public const string Rotation = "rotation";
    public const string Leg = "leg";

    /// <summary>In the order the page shows the header to copy.</summary>
    public static readonly IReadOnlyList<string> Required =
        [Callsign, FlightNumber, AircraftTypes, DepartureIcao, OffBlockUtc, ArrivalIcao, OnBlockUtc, Stand];

    public static readonly IReadOnlyList<string> Optional = [Rotation, Leg];

    /// <summary>The field a refusal of a cell is keyed by: <c>rows[12].aircraft_types</c>, the row as the table numbers it.</summary>
    public static string Field(int row, string column) =>
        string.Create(CultureInfo.InvariantCulture, $"rows[{row}].{column}");
}

/// <summary>One row of the sheet: its number in the table — the header's row counts, and so does an empty one — and its cells.</summary>
public sealed record SlotSheetRow(int Number, IReadOnlyDictionary<string, string> Cells)
{
    /// <summary>The cell of a column, trimmed; empty when the row has none.</summary>
    public string this[string column] => Cells.TryGetValue(column, out var cell) ? cell : string.Empty;
}

/// <summary>
/// The table the staff pasted or loaded (design M4 §3.1, E5): text copied from a spreadsheet, which arrives separated by tabs,
/// or a CSV file — separated by commas, or by the semicolons a spreadsheet writes where the comma is the decimal separator —, with
/// a header. The separator is the header's: a tab, else a semicolon, else a comma (no name of a column holds one). A cell may be
/// quoted, as a spreadsheet quotes one that holds the separator, a quote (written twice) or a line break.
/// <para>It only turns text into rows: what a cell says is checked by the load, row by row (<see cref="SlotLoading"/>). The rows are
/// numbered as the table numbers them — the header is row 1 when the text starts with it, an empty row counts and is skipped —, so
/// that a refusal of <c>rows[12]</c> is the row 12 the spreadsheet or the file shows.</para>
/// </summary>
public sealed class SlotSheet
{
    /// <summary>The field of the whole table, on the form of the page that loads it.</summary>
    public const string TextField = "text";

    /// <summary>A day of a big event has a few hundred slots (441 at one airport in October 2026); a thousand rows is a file of something else.</summary>
    public const int MaxRows = 1000;

    private const char Quote = '"';

    /// <summary>The mark a file saved by a spreadsheet may start with, which is not text.</summary>
    private const char ByteOrderMark = (char)0xFEFF;

    private SlotSheet(int headerRow, IReadOnlyList<SlotSheetRow> rows, Refusals problems)
    {
        HeaderRow = headerRow;
        Rows = rows;
        Problems = problems;
    }

    /// <summary>The row of the header in the table: 1, unless empty rows come before it.</summary>
    public int HeaderRow { get; }

    /// <summary>The rows under the header that hold something, in their order.</summary>
    public IReadOnlyList<SlotSheetRow> Rows { get; }

    /// <summary>What stops the table from being read at all: no text, no header, a column missing or twice, no row, too many rows.</summary>
    public Refusals Problems { get; }

    public static SlotSheet Read(string? text)
    {
        var problems = new Refusals();
        var records = Records(text ?? string.Empty);
        var header = records.FindIndex(record => !IsEmpty(record.Cells));

        if (header < 0)
        {
            problems.Add(TextField, "errors.required");
            return new SlotSheet(0, [], problems);
        }

        var headerRow = records[header].Number;
        var columns = records[header].Cells.Select(name => name.Trim().ToLowerInvariant()).ToList();

        foreach (var column in SlotColumns.Required.Concat(SlotColumns.Optional))
        {
            var count = columns.Count(name => name == column);
            if (count == 0 && SlotColumns.Required.Contains(column))
            {
                problems.Add(SlotColumns.Field(headerRow, column), "events:errors.sheetColumnMissing");
            }
            else if (count > 1)
            {
                problems.Add(SlotColumns.Field(headerRow, column), "events:errors.sheetColumnTwice");
            }
        }

        if (!problems.IsEmpty)
        {
            return new SlotSheet(headerRow, [], problems);
        }

        var known = SlotColumns.Required.Concat(SlotColumns.Optional).ToHashSet(StringComparer.Ordinal);
        var rows = records
            .Skip(header + 1)
            .Where(record => !IsEmpty(record.Cells))
            .Select(record => new SlotSheetRow(
                record.Number,
                columns
                    .Select((name, index) => (name, index))
                    .Where(column => known.Contains(column.name))
                    .ToDictionary(
                        column => column.name,
                        column => column.index < record.Cells.Count ? record.Cells[column.index].Trim() : string.Empty,
                        StringComparer.Ordinal)))
            .ToList();

        if (rows.Count == 0)
        {
            problems.Add(TextField, "events:errors.sheetEmpty");
        }
        else if (rows.Count > MaxRows)
        {
            problems.Add(TextField, "events:errors.sheetTooLong");
        }

        return new SlotSheet(headerRow, problems.IsEmpty ? rows : [], problems);
    }

    private static bool IsEmpty(IReadOnlyList<string> cells) => cells.All(string.IsNullOrWhiteSpace);

    /// <summary>
    /// The records of the text, each with its number in the table and its cells as written: a line break inside quotes is part
    /// of its cell, any other ends the record. A quote opens a quoted cell only at its start, and after the closing one the rest of
    /// the cell is kept as written: a table is read as it was meant, not refused for a stray quote.
    /// </summary>
    private static List<(int Number, IReadOnlyList<string> Cells)> Records(string text)
    {
        text = text.TrimStart(ByteOrderMark);
        var separator = Separator(text);
        var records = new List<(int Number, IReadOnlyList<string> Cells)>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var startOfCell = true;

        void EndCell()
        {
            cells.Add(cell.ToString());
            cell.Clear();
            startOfCell = true;
        }

        void EndRecord()
        {
            EndCell();
            records.Add((records.Count + 1, cells));
            cells = [];
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (quoted)
            {
                if (character != Quote)
                {
                    cell.Append(character);
                }
                else if (index + 1 < text.Length && text[index + 1] == Quote)
                {
                    cell.Append(Quote);
                    index++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (character)
            {
                case Quote when startOfCell:
                    quoted = true;
                    startOfCell = false;
                    break;
                case '\r' when index + 1 < text.Length && text[index + 1] == '\n':
                    break;
                case '\r' or '\n':
                    EndRecord();
                    break;
                default:
                    if (character == separator)
                    {
                        EndCell();
                    }
                    else
                    {
                        cell.Append(character);
                        startOfCell = startOfCell && char.IsWhiteSpace(character);
                    }

                    break;
            }
        }

        // The last line, unless the text ended with its line break.
        if (cells.Count > 0 || cell.Length > 0 || quoted)
        {
            EndRecord();
        }

        return records;
    }

    /// <summary>The separator of the header line — the first one with something in it —: a tab, else a semicolon, else a comma.</summary>
    private static char Separator(string text)
    {
        var header = text.Split('\n').FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;

        return header.Contains('\t', StringComparison.Ordinal) ? '\t'
            : header.Contains(';', StringComparison.Ordinal) ? ';'
            : ',';
    }
}
