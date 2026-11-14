/**
 * A sheet read in the browser: whatever SheetJS reads — XLSX, XLS, ODS, CSV — made rows of cells, and a text copied from a
 * spreadsheet made the same rows (design of the editor's phase 3, §5.1).
 *
 * The generic half of what was the tours' reader of legs (`modules/flightops/screens/legFile.ts`, design M2 §8.4): "a
 * sheet → rows of cells", and nothing about what the rows mean. It moved here when the table block needed it too, because
 * the core never imports from a module (`CLAUDE.md` §2); the module reads its legs through it.
 *
 * The library is loaded only when a file is read, so that nobody else downloads it.
 */

/** One cell as SheetJS gives it: a value of its own type, or — read `formatted` — the text the sheet shows. */
export type SheetCell = string | number | boolean | Date | null;

/** A workbook read: the names of its sheets, in order, and the rows of any of them. */
export interface Workbook {
  readonly sheets: readonly string[];
  /**
   * The rows of a sheet, every one of them — an empty row is an empty row, so that a row's number is the one the sheet
   * shows — with `null` for a cell that holds nothing; none for a sheet that is not there.
   *
   * `formatted` gives every cell as the text the sheet shows (`18:00`, `1,5`, `01/10/2026`) instead of its value (a
   * fraction of a day, a number, a date): what a table copies, where an import of data wants the value.
   */
  cells(name: string, options?: { formatted?: boolean }): SheetCell[][];
}

/** A file read as a workbook; `null` when SheetJS cannot read it, which is a thing to say and not an exception. */
export async function readWorkbook(file: Blob): Promise<Workbook | null> {
  const XLSX = await import('xlsx');

  try {
    const workbook = XLSX.read(new Uint8Array(await file.arrayBuffer()), { type: 'array', dense: true });

    return {
      sheets: workbook.SheetNames,
      cells: (name, options) => {
        const found = workbook.Sheets[name];
        return found === undefined
          ? []
          : XLSX.utils.sheet_to_json<SheetCell[]>(found, {
              header: 1,
              raw: options?.formatted !== true,
              defval: null,
              blankrows: true,
            });
      },
    };
  } catch {
    return null;
  }
}

/** A cell as trimmed text; `null` when it holds nothing but spaces. A date is written as ISO, in UTC. */
export function cellText(cell: SheetCell | undefined): string | null {
  if (cell === null || cell === undefined) {
    return null;
  }

  const value = (cell instanceof Date ? cell.toISOString() : String(cell)).trim();
  return value === '' ? null : value;
}

/**
 * The rows of a text copied from a spreadsheet — Excel, Google Sheets, LibreOffice, a table of Word: a row per line, a
 * cell per tab. A cell that holds a line break or a quote comes between double quotes, with its quotes doubled, as
 * those programs write it; a cell that merely begins with a quote is read as it is. A line break inside a cell is a
 * `\n`, whatever the clipboard made of it (Windows's makes it `\r\n`). The line break at the end of the copy closes the
 * last row and does not open another.
 */
export function rowsOfPastedText(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let at = 0;

  const endCell = () => {
    row.push(cell);
    cell = '';
  };
  const endRow = () => {
    endCell();
    rows.push(row);
    row = [];
  };

  while (at < text.length) {
    const char = text[at] ?? '';

    if (char === '"' && cell === '') {
      // A quoted cell, if a closing quote is followed by the end of the cell; otherwise just a quote.
      const quoted = quotedCellAt(text, at);
      if (quoted !== null) {
        cell = quoted.value;
        at = quoted.end;
        continue;
      }
    }

    if (char === '\t') {
      endCell();
    } else if (char === '\r' || char === '\n') {
      endRow();
      if (char === '\r' && text[at + 1] === '\n') {
        at++;
      }
    } else {
      cell += char;
    }

    at++;
  }

  // What follows the last line break is a row only if it holds something: a copy ends with one.
  if (cell !== '' || row.length > 0) {
    endRow();
  }

  return rows;
}

/** The cell quoted from `start`, and where the text goes on after it; `null` when the quote is not a cell's. */
function quotedCellAt(text: string, start: number): { value: string; end: number } | null {
  let value = '';
  let at = start + 1;

  while (at < text.length) {
    const char = text[at];
    if (char === '"') {
      if (text[at + 1] === '"') {
        value += '"';
        at += 2;
        continue;
      }

      const next = text[at + 1];
      return next === undefined || next === '\t' || next === '\r' || next === '\n'
        ? { value: value.replace(/\r\n?/g, '\n'), end: at + 1 }
        : null;
    }

    value += char;
    at++;
  }

  return null;
}
