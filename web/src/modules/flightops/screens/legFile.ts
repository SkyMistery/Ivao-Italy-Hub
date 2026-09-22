import type { LegImportRowDto } from '../api';

/**
 * The file of an import of legs (design M2 §8.4, T8), read in the browser (answer 24): XLSX, XLS, ODS or CSV, whatever
 * SheetJS reads. The browser only turns cells into rows; whether an airport exists, and what the rows change, is the
 * server's to say.
 *
 * Shaped on the FOD's own workbook (one sheet per tour, 22 September 2026): the header is the first of the first rows
 * that names a departure and an arrival — a row of totals may come before it —, the columns are found by name in any
 * order and case, and where a sheet has both "Departure" (the city) and "Departure ICAO", the code wins. A column the
 * import does not know is ignored, and so is a row with nothing in it; any other row is a leg, and one that is not
 * gets refused on its row (Carmine: the file is cleaned, not guessed). The order of the rows is the order of the tour:
 * there is no column of numbers. The library is loaded only when a file is read, so that nobody else downloads it.
 */

/** The columns of the file, as the model writes them. */
export const LEG_FILE_COLUMNS = [
  'departure',
  'arrival',
  'callsign',
  'flightNumber',
  'aircraft',
  'release',
] as const;

type Column = (typeof LEG_FILE_COLUMNS)[number];

/** Header names, lower case and letters only, with a rank: the lowest one found wins the column. */
const ALIASES: Readonly<Record<string, readonly [Column, number]>> = {
  departureicao: ['departure', 0],
  departure: ['departure', 1],
  arrivalicao: ['arrival', 0],
  destinationicao: ['arrival', 0],
  arrival: ['arrival', 1],
  destination: ['arrival', 1],
  callsigns: ['callsign', 0],
  callsign: ['callsign', 0],
  flightnumbers: ['flightNumber', 0],
  flightnumber: ['flightNumber', 0],
  aircrafttypes: ['aircraft', 0],
  aircraft: ['aircraft', 0],
  releaseat: ['release', 0],
  release: ['release', 0],
};

/** How far down a header may be: a row of totals, a title, a note above it. */
const HEADER_SEARCH_ROWS = 10;

/** A problem of the file, with the row of the sheet it is on (from 1) when it is on one. */
export interface LegFileProblem {
  readonly line: number | null;
  /** An i18n key of `flightops:legs.import.file`. */
  readonly key: string;
  readonly column?: string;
}

export interface LegFile {
  readonly rows: LegImportRowDto[];
  /** The row of the sheet every row came from, so that a refusal of `rows[n]` is said on the line the file shows. */
  readonly lines: number[];
  readonly problems: LegFileProblem[];
}

/** A workbook read: its sheets, the one read, and what it holds. */
export interface LegWorkbook extends LegFile {
  readonly sheets: string[];
  readonly sheet: string | null;
}

type Cell = string | number | boolean | Date | null;

/** Midnight of 1 January 1970 in Excel's days (the 1900 system, with its leap year that was not). */
const EXCEL_EPOCH_DAYS = 25569;
const DAY_MS = 86_400_000;

/** `2026-10-01`, `2026-10-01 18:00`, `2026-10-01T18:00:00Z`: read as UTC, like every instant the hub writes. */
const ISO = /^(\d{4})-(\d{2})-(\d{2})(?:[ T](\d{2}):(\d{2})(?::(\d{2}))?)?Z?$/;

function text(cell: Cell | undefined): string | null {
  if (cell === null || cell === undefined) {
    return null;
  }

  const value = (cell instanceof Date ? cell.toISOString() : String(cell)).trim();
  return value === '' ? null : value;
}

/**
 * The codes of one cell — callsigns or flight numbers — as a sheet writes them when a route is flown more than once
 * a day: `ITY1357/1365`, `ISS962,964,966`, `EEZ881, ISS885`, `ITY1167&1173`. Separated by a slash, a comma, a semicolon
 * or an ampersand; a piece of digits only takes the letters of the one before it, so `UA965/967` is UA965 and UA967. A
 * space is part of a code (`AZ 200`). The editor's cells use the same rule.
 */
export function splitCodes(value: string | null | undefined): string[] {
  const codes: string[] = [];
  for (const piece of (value ?? '').split(/[,;/&]+/)) {
    const code = piece.trim().toUpperCase();
    if (code === '') {
      continue;
    }

    const previous = codes.at(-1);
    const prefix = previous === undefined ? null : /^(.*?)\d+$/.exec(previous)?.[1];
    codes.push(/^\d+$/.test(code) && prefix ? `${prefix}${code}` : code);
  }

  return [...new Set(codes)];
}

/** A release: an Excel date (a number of days) or an ISO text, in UTC; undefined when it is neither. */
export function releaseOf(cell: Cell | undefined): string | null | undefined {
  if (typeof cell === 'number') {
    return new Date(Math.round(((cell - EXCEL_EPOCH_DAYS) * DAY_MS) / 60_000) * 60_000).toISOString();
  }

  const value = text(cell);
  if (value === null) {
    return null;
  }

  const match = ISO.exec(value);
  if (match === null) {
    return undefined;
  }

  const [, year, month, day, hours = '00', minutes = '00', seconds = '00'] = match;
  const date = new Date(`${year}-${month}-${day}T${hours}:${minutes}:${seconds}Z`);

  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

/** The columns a row names, each by the best of its names; null when it names no departure and no arrival. */
function columnsOf(row: readonly Cell[]): Map<Column, number> | null {
  const found = new Map<Column, { index: number; rank: number }>();
  row.forEach((cell, index) => {
    const alias = ALIASES[(text(cell) ?? '').toLowerCase().replace(/[^a-z]/g, '')];
    if (alias === undefined) {
      return;
    }

    const [column, rank] = alias;
    const current = found.get(column);
    if (current === undefined || rank < current.rank) {
      found.set(column, { index, rank });
    }
  });

  if (!found.has('departure') || !found.has('arrival')) {
    return null;
  }

  return new Map([...found].map(([column, { index }]) => [column, index]));
}

/** The index of the header among the first rows, and its columns; null when none names a departure and an arrival. */
function headerOf(
  sheet: readonly (readonly Cell[])[],
): { index: number; columns: Map<Column, number> } | null {
  for (let index = 0; index < Math.min(sheet.length, HEADER_SEARCH_ROWS); index++) {
    const columns = columnsOf(sheet[index] ?? []);
    if (columns !== null) {
      return { index, columns };
    }
  }

  return null;
}

/** The rows of a sheet made the rows of an import. */
export function legRowsOf(sheet: readonly (readonly Cell[])[]): LegFile {
  const problems: LegFileProblem[] = [];
  const rows: LegImportRowDto[] = [];
  const lines: number[] = [];

  const header = headerOf(sheet);
  if (header === null) {
    return { rows, lines, problems: [{ line: null, key: 'noHeader' }] };
  }

  const at = (row: readonly Cell[], column: Column) => {
    const index = header.columns.get(column);
    return index === undefined ? undefined : row[index];
  };

  sheet.slice(header.index + 1).forEach((row, offset) => {
    const line = header.index + offset + 2;
    if (row.every((cell) => text(cell) === null)) {
      return;
    }

    const release = releaseOf(at(row, 'release'));
    if (release === undefined) {
      problems.push({ line, key: 'release', column: 'release' });
      return;
    }

    rows.push({
      departureIcao: (text(at(row, 'departure')) ?? '').toUpperCase(),
      arrivalIcao: (text(at(row, 'arrival')) ?? '').toUpperCase(),
      callsigns: splitCodes(text(at(row, 'callsign'))),
      flightNumbers: splitCodes(text(at(row, 'flightNumber'))),
      aircraftTypes: (text(at(row, 'aircraft')) ?? '')
        .split(/[\s,;]+/)
        .map((type) => type.toUpperCase())
        .filter((type) => type !== ''),
      releaseAt: release,
    });
    lines.push(line);
  });

  if (rows.length === 0 && problems.length === 0) {
    problems.push({ line: null, key: 'empty' });
  }

  return { rows, lines, problems };
}

/**
 * A file read: the sheet asked for, or else the first one with a header of legs — a workbook of tours opens on a
 * summary. A file SheetJS cannot read is one problem, not an exception.
 */
export async function readLegFile(file: Blob, sheet?: string): Promise<LegWorkbook> {
  const XLSX = await import('xlsx');

  try {
    const workbook = XLSX.read(new Uint8Array(await file.arrayBuffer()), { type: 'array', dense: true });
    const cells = (name: string) => {
      const found = workbook.Sheets[name];
      return found === undefined
        ? []
        : XLSX.utils.sheet_to_json<Cell[]>(found, { header: 1, raw: true, defval: null, blankrows: true });
    };

    const sheets = workbook.SheetNames;
    const chosen =
      sheet !== undefined && sheets.includes(sheet)
        ? sheet
        : (sheets.find((name) => headerOf(cells(name)) !== null) ?? sheets[0]);
    if (chosen === undefined) {
      return { sheets, sheet: null, rows: [], lines: [], problems: [{ line: null, key: 'empty' }] };
    }

    return { sheets, sheet: chosen, ...legRowsOf(cells(chosen)) };
  } catch {
    return { sheets: [], sheet: null, rows: [], lines: [], problems: [{ line: null, key: 'unreadable' }] };
  }
}

/**
 * The model of the file, downloaded from the editor: the header and nothing else — an example row would name some
 * division's airports. The callsign and flight number columns are text, so that a leading zero survives.
 */
export async function downloadLegFileModel(name: string): Promise<void> {
  const XLSX = await import('xlsx');

  const sheet = XLSX.utils.aoa_to_sheet([[...LEG_FILE_COLUMNS]]);
  sheet['!cols'] = LEG_FILE_COLUMNS.map((column) => ({ wch: column === 'release' ? 18 : 14 }));
  for (const column of ['callsign', 'flightNumber'] as const) {
    const letter = XLSX.utils.encode_col(LEG_FILE_COLUMNS.indexOf(column));
    for (let row = 2; row <= 200; row++) {
      sheet[`${letter}${row}`] = { t: 's', v: '', z: '@' };
    }
  }
  sheet['!ref'] = `A1:${XLSX.utils.encode_col(LEG_FILE_COLUMNS.length - 1)}200`;

  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'legs');
  XLSX.writeFile(book, `${name}.xlsx`);
}
