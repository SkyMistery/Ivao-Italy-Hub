import * as XLSX from 'xlsx';
import { expect, test } from 'vitest';

import { cellText, readWorkbook, rowsOfPastedText, shortDateFormat } from './sheet';

/**
 * The core's reader of sheets (design of the editor's phase 3, §5.1, PR 3.12): the generic half of the tours' reader of
 * legs, which still has its own tests (`modules/flightops/screens/legFile.test.ts`), and the text a spreadsheet puts on
 * the clipboard.
 */

test('a copy from a spreadsheet is a row per line and a cell per tab, and its last line break opens no row', () => {
  expect(rowsOfPastedText('Day\tFrom\r\nMonday\t18:00z\r\nFriday\t\r\n')).toEqual([
    ['Day', 'From'],
    ['Monday', '18:00z'],
    ['Friday', ''],
  ]);
  expect(rowsOfPastedText('a\tb\nc\td')).toEqual([
    ['a', 'b'],
    ['c', 'd'],
  ]);
  expect(rowsOfPastedText('')).toEqual([]);
});

test('a cell with a line break or a quote comes quoted, as Excel writes it, and a stray quote is only a quote', () => {
  expect(rowsOfPastedText('"two\nlines"\t"say ""hi"""\n"open\tx\n')).toEqual([
    ['two\nlines', 'say "hi"'],
    ['"open', 'x'],
  ]);
  expect(rowsOfPastedText('"quoted" not\tend')).toEqual([['"quoted" not', 'end']]);
  // Windows's clipboard writes the line break inside a cell as CRLF; the cell holds a line break.
  expect(rowsOfPastedText('"Milano\r\nRadar"\t1\r\n')).toEqual([['Milano\nRadar', '1']]);
});

test('a cell is trimmed text, and nothing but spaces is nothing', () => {
  expect(cellText('  a ')).toBe('a');
  expect(cellText('   ')).toBeNull();
  expect(cellText(null)).toBeNull();
  expect(cellText(3)).toBe('3');
});

test('a CSV of either separator is read, a row per line', async () => {
  for (const csv of ['Day;From\r\nMonday;18:00\r\n', 'Day,From\r\nMonday,18:00\r\n']) {
    const workbook = await readWorkbook(new Blob([csv]));
    expect(workbook).not.toBeNull();
    const first = workbook?.sheets[0] ?? '';
    expect(workbook?.cells(first, { formatted: true })).toEqual([
      ['Day', 'From'],
      ['Monday', '18:00'],
    ]);
  }
});

test('a workbook gives every sheet, a value by default and the text the sheet shows when asked', async () => {
  const book = XLSX.utils.book_new();
  const sheet = XLSX.utils.aoa_to_sheet([['Share'], [0.5]]);
  sheet.A2 = { t: 'n', v: 0.5, z: '0%' };
  XLSX.utils.book_append_sheet(book, XLSX.utils.aoa_to_sheet([]), 'Empty');
  XLSX.utils.book_append_sheet(book, sheet, 'Data');
  const bytes = XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;

  const workbook = await readWorkbook(new Blob([bytes]));

  expect(workbook?.sheets).toEqual(['Empty', 'Data']);
  expect(workbook?.cells('Data')).toEqual([['Share'], [0.5]]);
  expect(workbook?.cells('Data', { formatted: true })).toEqual([['Share'], ['50%']]);
  expect(workbook?.cells('Missing')).toEqual([]);
});

test('a CSV is read as UTF-8 with or without its mark, and as Windows-1252 when it is not UTF-8', async () => {
  const utf8 = new TextEncoder().encode('Giorno;Ora\r\nLunedì;18:00\r\n');
  // «ì» is one byte in Windows-1252, what an older Excel writes.
  const windows1252 = new Uint8Array([
    ...new TextEncoder().encode('Giorno;Ora\r\nLuned'),
    0xec,
    ...new TextEncoder().encode(';18:00\r\n'),
  ]);

  for (const bytes of [utf8, new Uint8Array([0xef, 0xbb, 0xbf, ...utf8]), windows1252]) {
    const workbook = await readWorkbook(new Blob([bytes]));
    expect(workbook?.cells(workbook.sheets[0] ?? '', { formatted: true })).toEqual([
      ['Giorno', 'Ora'],
      ['Lunedì', '18:00'],
    ]);
  }
});

/** A workbook of one column: 1 October 2026 in Excel's short date (its format 14), and in a format of its own. */
function datesBook(bookType: XLSX.BookType): Blob {
  const sheet = XLSX.utils.aoa_to_sheet([['Day'], [null], [null]]);
  // SheetJS writes `m/d/yy` as the built-in format 14, and reads format 14 back as `m/d/yy`.
  sheet.A2 = { t: 'n', v: 46296, z: 'm/d/yy' };
  sheet.A3 = { t: 'n', v: 46296, z: 'yyyy-mm-dd' };
  sheet['!ref'] = 'A1:A3';
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'Dates');
  return new Blob([XLSX.write(book, { type: 'array', bookType }) as ArrayBuffer]);
}

test("Excel's short date is written in the order of the language asked for, and a date of its own format as it is", async () => {
  for (const bookType of ['xlsx', 'biff8'] as const) {
    const italian = await readWorkbook(datesBook(bookType), { locale: 'it' });
    expect(italian?.cells('Dates', { formatted: true })).toEqual([['Day'], ['01/10/2026'], ['2026-10-01']]);
    // The value is the value: only the text the sheet shows is the language's.
    expect(italian?.cells('Dates')).toEqual([['Day'], [46296], [46296]]);

    const american = await readWorkbook(datesBook(bookType), { locale: 'en-US' });
    expect(american?.cells('Dates', { formatted: true })).toEqual([['Day'], ['10/01/2026'], ['2026-10-01']]);
  }
});

test('a date in a CSV is the text it is, whatever the language asked for', async () => {
  const workbook = await readWorkbook(new Blob(['Day;From\r\n01/10/2026;18:00\r\n']), { locale: 'it' });

  expect(workbook?.cells(workbook.sheets[0] ?? '', { formatted: true })).toEqual([
    ['Day', 'From'],
    ['01/10/2026', '18:00'],
  ]);
});

test("a language's short date is its own order and separators, from Intl, quoted", () => {
  expect(shortDateFormat('it')).toBe('dd"/"mm"/"yyyy');
  expect(shortDateFormat('en-US')).toBe('mm"/"dd"/"yyyy');
  expect(shortDateFormat('de')).toBe('dd"."mm"."yyyy');
});

test('a file that is not a sheet is null, not an exception', async () => {
  expect(await readWorkbook(new Blob([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0, 0, 0])]))).toBeNull();
});
