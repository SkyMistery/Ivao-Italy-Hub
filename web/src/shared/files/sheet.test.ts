import * as XLSX from 'xlsx';
import { expect, test } from 'vitest';

import { cellText, readWorkbook, rowsOfPastedText } from './sheet';

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

test('a file that is not a sheet is null, not an exception', async () => {
  expect(await readWorkbook(new Blob([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0, 0, 0])]))).toBeNull();
});
