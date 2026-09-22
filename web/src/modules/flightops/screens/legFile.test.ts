import * as XLSX from 'xlsx';
import { expect, test } from 'vitest';

import { legRowsOf, readLegFile, releaseOf } from './legFile';

/**
 * The file of an import of legs (T8), read in the browser: the columns found by name in any order, a row with nothing
 * in it skipped, the release read as UTC from a date cell or an ISO text, the line of every row kept so that a refusal
 * of the server is said where the file shows it; XLSX and CSV — with the semicolon a spreadsheet writes in much of
 * Europe — through the same library.
 */

test('the columns are found by name, in any order and case, and empty rows are skipped', () => {
  const file = legRowsOf([
    ['Arrival', 'extra', 'DEPARTURE', 'Flight Number', 'aircraft'],
    ['eddm', 'x', 'eddf', 123, 'a320, A20N'],
    [null, null, null, null, null],
    ['EDDF', null, 'EDDM', 'LH 101', null],
  ]);

  expect(file.problems).toEqual([]);
  expect(file.lines).toEqual([2, 4]);
  expect(file.rows[0]).toEqual({
    departureIcao: 'EDDF',
    arrivalIcao: 'EDDM',
    realCallsign: null,
    flightNumber: '123',
    aircraftTypes: ['A320', 'A20N'],
    releaseAt: null,
  });
  expect(file.rows[1]?.aircraftTypes).toEqual([]);
});

test('a file without departure or arrival names what is missing', () => {
  expect(legRowsOf([['departure', 'callsign']]).problems).toEqual([
    { line: 1, key: 'missingColumn', column: 'arrival' },
  ]);
  expect(legRowsOf([['departure', 'arrival']]).problems).toEqual([{ line: null, key: 'empty' }]);
});

test('the release is UTC, from a date cell or an ISO text, and anything else is a problem on its row', () => {
  // 46296.75 is 2026-10-01 18:00 in Excel's days.
  expect(releaseOf(46296.75)).toBe('2026-10-01T18:00:00.000Z');
  expect(releaseOf('2026-10-01 18:00')).toBe('2026-10-01T18:00:00.000Z');
  expect(releaseOf('2026-10-01')).toBe('2026-10-01T00:00:00.000Z');
  expect(releaseOf('')).toBeNull();
  expect(releaseOf('01/10/2026')).toBeUndefined();

  const file = legRowsOf([
    ['departure', 'arrival', 'release'],
    ['EDDF', 'EDDM', 'next week'],
  ]);
  expect(file.problems).toEqual([{ line: 2, key: 'release', column: 'release' }]);
});

test('a CSV with semicolons and a real XLSX read the same', async () => {
  const csv = await readLegFile(new Blob(['departure;arrival;callsign\r\nEDDF;EDDM;DLH1\r\nEDDM;EDDF;\r\n']));
  expect(csv.problems).toEqual([]);
  expect(csv.rows.map((row) => [row.departureIcao, row.arrivalIcao, row.realCallsign])).toEqual([
    ['EDDF', 'EDDM', 'DLH1'],
    ['EDDM', 'EDDF', null],
  ]);

  // A date cell as a spreadsheet stores it: a number of days with a date format, the time as it was typed.
  const sheet = XLSX.utils.aoa_to_sheet([
    ['departure', 'arrival', 'callsign', 'release'],
    ['EDDF', 'EDDM', 'DLH1', null],
  ]);
  sheet.D2 = { t: 'n', v: 46296.75, z: 'yyyy-mm-dd hh:mm' };
  sheet['!ref'] = 'A1:D2';
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'legs');
  const bytes = XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;

  const xlsx = await readLegFile(new Blob([bytes]));
  expect(xlsx.problems).toEqual([]);
  expect(xlsx.rows[0]?.realCallsign).toBe('DLH1');
  expect(xlsx.rows[0]?.releaseAt).toBe('2026-10-01T18:00:00.000Z');
});

test('something that is not a spreadsheet is one problem, not an exception', async () => {
  const file = await readLegFile(new Blob([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0, 0, 0])]));
  expect(file.rows).toEqual([]);
  expect(file.problems.length).toBeGreaterThan(0);
});
