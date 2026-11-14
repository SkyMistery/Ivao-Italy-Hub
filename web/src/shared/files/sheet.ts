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

/**
 * Excel's built-in format 14, its short date, as SheetJS names it when it reads a workbook (`SSF`'s table, whose types
 * the library does not give). The name of a format with no order of its own, not a pattern of any language's.
 */
const EXCEL_SHORT_DATE = 'm/d/yy';

/** What a file of a sheet may be, for a file control's `accept`: what `readWorkbook` reads. */
export const SHEET_FILE_TYPES = '.csv,.xlsx,.xls,.ods,text/csv';

/**
 * A file read as a workbook; `null` when it cannot be read — SheetJS refuses it, or the library itself does not load
 * (a deploy since the page was opened) —, which is a thing to say and not an exception.
 *
 * `locale` is the language a date of the sheet is written in when it is read `formatted`: Excel's short date (its format
 * 14, what an Italian Excel gives a date typed as `01/10/2026`) is stored with no order of its own, and SheetJS would
 * write it in the American one (`10/1/26`). Every other format of a date is the sheet's own and is kept, and so is a
 * date of a CSV, which is the text it was written as.
 */
export async function readWorkbook(file: Blob, options?: { locale?: string }): Promise<Workbook | null> {
  try {
    const XLSX = await import('xlsx');
    const contents = textOf(new Uint8Array(await file.arrayBuffer()));
    const isText = typeof contents === 'string';
    // `dateNF` is not handed to SheetJS: it would read the dates of a CSV in that order, and its times as dates.
    const shortDate = !isText && options?.locale !== undefined ? shortDateFormat(options.locale) : null;
    const workbook = XLSX.read(contents, {
      type: isText ? 'string' : 'array',
      dense: true,
      cellNF: shortDate !== null,
    });

    if (shortDate !== null) {
      for (const sheet of Object.values(workbook.Sheets)) {
        for (const row of sheet['!data'] ?? []) {
          for (const cell of row ?? []) {
            if (cell?.t === 'n' && cell.z === EXCEL_SHORT_DATE && typeof cell.v === 'number') {
              cell.w = XLSX.utils.format_cell({ t: 'n', v: cell.v, z: shortDate });
            }
            // The format was read only for this: with it a value of a date would come as a `Date` of the browser's time
            // zone, where every other read has the number of days.
            if (cell !== undefined) {
              delete cell.z;
            }
          }
        }
      }
    }

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

/** The first bytes of a workbook that is not text: a ZIP (XLSX, ODS), a compound file (XLS), UTF-16 with its mark. */
const BINARY_SIGNATURES: readonly (readonly number[])[] = [
  [0x50, 0x4b, 0x03, 0x04],
  [0xd0, 0xcf, 0x11, 0xe0],
  [0xff, 0xfe],
  [0xfe, 0xff],
];

/**
 * A file of text (a CSV) as a string, its bytes otherwise. SheetJS reads the bytes of a CSV as UTF-8 only when it begins
 * with the mark of UTF-8, and Google Sheets and LibreOffice write it without: «Lunedì» came in as «LunedÃ¬». So the text is
 * decoded here — UTF-8 if the bytes are UTF-8, else Windows-1252, what an older Excel writes —, and SheetJS is handed a
 * string. What SheetJS reads by its own signature (a workbook, UTF-16) stays bytes, and so does a file with a zero byte,
 * which no text has (the oldest XLS, with no signature of their own).
 */
function textOf(bytes: Uint8Array): string | Uint8Array {
  if (
    BINARY_SIGNATURES.some((signature) => signature.every((byte, at) => bytes[at] === byte)) ||
    bytes.includes(0)
  ) {
    return bytes;
  }

  try {
    // `ignoreBOM: false`, the default, takes the mark away.
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return new TextDecoder('windows-1252').decode(bytes);
  }
}

/**
 * The short date of a language as SheetJS writes a format: the order and the separators of `Intl`'s numeric date
 * (`dd/mm/yyyy` in Italian, `mm/dd/yyyy` in American English), never a pattern written here. A separator is quoted, so
 * that no letter of a language's is read as a code of the format.
 */
export function shortDateFormat(locale: string): string {
  const parts = new Intl.DateTimeFormat(locale, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).formatToParts(new Date(2000, 0, 1));

  return parts
    .map((part) =>
      part.type === 'day'
        ? 'dd'
        : part.type === 'month'
          ? 'mm'
          : part.type === 'year'
            ? 'yyyy'
            : `"${part.value.replace(/"/g, '')}"`,
    )
    .join('');
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
