import * as XLSX from 'xlsx';
import { expect, test } from 'vitest';

import { legRowsOf, readLegFile, releaseOf, splitCodes } from './legFile';

/**
 * The file of an import of legs (T8), read in the browser, shaped on the FOD's own workbook of 22 September 2026: a
 * sheet per tour opening on a summary, a row of totals above the header, a "Departure" city next to its
 * "Departure ICAO", a callsign cell naming the four daily flights of a route. The release is UTC from a date cell or an
 * ISO text; XLSX and CSV — with the semicolon a spreadsheet writes in much of Europe — go through the same library.
 */

test('the codes of a route flown several times a day are split, and a bare number takes the letters before it', () => {
  expect(splitCodes('ABC1357/1365/1359/1363')).toEqual(['ABC1357', 'ABC1365', 'ABC1359', 'ABC1363']);
  expect(splitCodes('XYZ962,964,966')).toEqual(['XYZ962', 'XYZ964', 'XYZ966']);
  expect(splitCodes('abc881, xyz885')).toEqual(['ABC881', 'XYZ885']);
  expect(splitCodes('UA965/967')).toEqual(['UA965', 'UA967']);
  expect(splitCodes('ABC1165/1171/1167&1173')).toEqual(['ABC1165', 'ABC1171', 'ABC1167', 'ABC1173']);
  expect(splitCodes('AZ 200')).toEqual(['AZ 200']);
  expect(splitCodes('A1/A1')).toEqual(['A1']);
  expect(splitCodes('')).toEqual([]);
  expect(splitCodes(null)).toEqual([]);
});

test('the header is found below a row of totals, and the ICAO column wins over the city', () => {
  const file = legRowsOf([
    [null, null, null, null, null, null, null, null, 18902, '62:55:00'],
    [
      null,
      'Departure',
      'Departure ICAO',
      'Departure IATA',
      'Destination',
      'Destination ICAO',
      'Destination IATA',
      'Callsign',
      null,
      null,
      'Note',
    ],
    [1, 'Frankfurt', 'eddf', 'FRA', 'Munich', 'EDDM', 'MUC', 'DLH1/3', 250, '01:00', 'LH1'],
    [null, null, null, null, null, null, null, null, null, null, null],
    [2, 'Munich', 'EDDM', 'MUC', 'Frankfurt', 'EDDF', 'FRA', null, 250, '01:00', null],
  ]);

  expect(file.problems).toEqual([]);
  expect(file.lines).toEqual([3, 5]);
  expect(file.rows[0]).toEqual({
    departureIcao: 'EDDF',
    arrivalIcao: 'EDDM',
    callsigns: ['DLH1', 'DLH3'],
    flightNumbers: [],
    aircraftTypes: [],
    releaseAt: null,
  });
  expect(file.rows[1]?.callsigns).toEqual([]);
});

test('a row that is not empty is a leg, even when it is a note: the server refuses it on its line', () => {
  const file = legRowsOf([
    ['departure', 'arrival', 'callsign'],
    ['EDDF', 'EDDM', 'DLH1'],
    [null, null, 'Welcome to the tour'],
  ]);

  expect(file.rows).toHaveLength(2);
  expect(file.lines).toEqual([2, 3]);
  expect(file.rows[1]).toMatchObject({ departureIcao: '', arrivalIcao: '' });
});

test('a sheet without a header of legs says so', () => {
  expect(legRowsOf([['departure', 'callsign']]).problems).toEqual([{ line: null, key: 'noHeader' }]);
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
  expect(csv.rows.map((row) => [row.departureIcao, row.arrivalIcao, row.callsigns])).toEqual([
    ['EDDF', 'EDDM', ['DLH1']],
    ['EDDM', 'EDDF', []],
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
  expect(xlsx.rows[0]?.callsigns).toEqual(['DLH1']);
  expect(xlsx.rows[0]?.releaseAt).toBe('2026-10-01T18:00:00.000Z');
});

test('a workbook of tours opens on the first sheet with legs, and another sheet can be asked for', async () => {
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, XLSX.utils.aoa_to_sheet([['Summary'], ['EDDF', 12]]), 'Summary');
  XLSX.utils.book_append_sheet(
    book,
    XLSX.utils.aoa_to_sheet([
      ['Departure ICAO', 'Destination ICAO'],
      ['EDDF', 'EDDM'],
    ]),
    'First tour',
  );
  XLSX.utils.book_append_sheet(
    book,
    XLSX.utils.aoa_to_sheet([
      ['Departure ICAO', 'Destination ICAO'],
      ['LFPG', 'LFMN'],
      ['LFMN', 'LFPG'],
    ]),
    'Second tour',
  );
  const bytes = new Blob([XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer]);

  const first = await readLegFile(bytes);
  expect(first.sheets).toEqual(['Summary', 'First tour', 'Second tour']);
  expect(first.sheet).toBe('First tour');
  expect(first.rows).toHaveLength(1);

  const second = await readLegFile(bytes, 'Second tour');
  expect(second.sheet).toBe('Second tour');
  expect(second.rows.map((row) => row.departureIcao)).toEqual(['LFPG', 'LFMN']);
});

test('something that is not a spreadsheet is one problem, not an exception', async () => {
  const file = await readLegFile(new Blob([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0, 0, 0])]));
  expect(file.rows).toEqual([]);
  expect(file.problems.length).toBeGreaterThan(0);
});
