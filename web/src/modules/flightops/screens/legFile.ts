import type { LegImportRowDto } from '../api';

/**
 * The file of an import of legs (design M2 §8.4, T8), read in the browser (answer 24): XLSX, XLS, ODS or CSV, whatever
 * SheetJS reads. The browser only turns cells into rows; whether an airport exists, and what the rows change, is the
 * server's to say.
 *
 * The first row names the columns, in any order and case; a column the import does not know is ignored, and so is a
 * row with nothing in it. The order of the rows is the order of the tour (Carmine, 22 September 2026): there is no
 * column of numbers. The library is loaded only when a file is read, so that nobody else downloads it.
 */

/** The columns of the file, as the model writes them; the names of the payload are understood too. */
export const LEG_FILE_COLUMNS = [
  'departure',
  'arrival',
  'callsign',
  'flightNumber',
  'aircraft',
  'release',
] as const;

type Column = (typeof LEG_FILE_COLUMNS)[number];

const ALIASES: Readonly<Record<string, Column>> = {
  departure: 'departure',
  departureicao: 'departure',
  arrival: 'arrival',
  arrivalicao: 'arrival',
  callsign: 'callsign',
  realcallsign: 'callsign',
  flightnumber: 'flightNumber',
  aircraft: 'aircraft',
  aircrafttypes: 'aircraft',
  release: 'release',
  releaseat: 'release',
};

/** A problem of the file, with the row of the sheet it is on (from 1, the header being 1) when it is on one. */
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

/** The rows of a sheet — the first row its header — made the rows of an import. */
export function legRowsOf(sheet: readonly (readonly Cell[])[]): LegFile {
  const problems: LegFileProblem[] = [];
  const rows: LegImportRowDto[] = [];
  const lines: number[] = [];

  const header = sheet[0] ?? [];
  const columns = new Map<Column, number>();
  header.forEach((cell, index) => {
    const column = ALIASES[(text(cell) ?? '').toLowerCase().replace(/[^a-z]/g, '')];
    if (column !== undefined && !columns.has(column)) {
      columns.set(column, index);
    }
  });

  for (const required of ['departure', 'arrival'] as const) {
    if (!columns.has(required)) {
      problems.push({ line: 1, key: 'missingColumn', column: required });
    }
  }

  if (problems.length > 0) {
    return { rows, lines, problems };
  }

  const at = (row: readonly Cell[], column: Column) => {
    const index = columns.get(column);
    return index === undefined ? undefined : row[index];
  };

  sheet.slice(1).forEach((row, index) => {
    const line = index + 2;
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
      realCallsign: text(at(row, 'callsign')),
      flightNumber: text(at(row, 'flightNumber')),
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

/** A file read: its first sheet, as rows of cells. A file SheetJS cannot read is one problem, not an exception. */
export async function readLegFile(file: Blob): Promise<LegFile> {
  const XLSX = await import('xlsx');

  try {
    const workbook = XLSX.read(new Uint8Array(await file.arrayBuffer()), { type: 'array', dense: true });
    const first = workbook.SheetNames[0];
    const sheet = first === undefined ? undefined : workbook.Sheets[first];
    if (sheet === undefined) {
      return { rows: [], lines: [], problems: [{ line: null, key: 'empty' }] };
    }

    return legRowsOf(
      XLSX.utils.sheet_to_json<Cell[]>(sheet, { header: 1, raw: true, defval: null, blankrows: true }),
    );
  } catch {
    return { rows: [], lines: [], problems: [{ line: null, key: 'unreadable' }] };
  }
}

/**
 * The model of the file, downloaded from the editor: the header and nothing else — an example row would name some
 * division's airports. The flight number column is text, so that a leading zero survives.
 */
export async function downloadLegFileModel(name: string): Promise<void> {
  const XLSX = await import('xlsx');

  const sheet = XLSX.utils.aoa_to_sheet([[...LEG_FILE_COLUMNS]]);
  sheet['!cols'] = LEG_FILE_COLUMNS.map((column) => ({ wch: column === 'release' ? 18 : 12 }));
  const flightNumber = XLSX.utils.encode_col(LEG_FILE_COLUMNS.indexOf('flightNumber'));
  for (let row = 2; row <= 200; row++) {
    sheet[`${flightNumber}${row}`] = { t: 's', v: '', z: '@' };
  }
  sheet['!ref'] = `A1:${XLSX.utils.encode_col(LEG_FILE_COLUMNS.length - 1)}200`;

  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'legs');
  XLSX.writeFile(book, `${name}.xlsx`);
}
