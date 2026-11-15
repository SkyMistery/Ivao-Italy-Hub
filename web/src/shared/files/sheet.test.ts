import * as XLSX from 'xlsx';
import { expect, test } from 'vitest';

import { cellText, readWorkbook, rowsOfPastedText, shortDateFormat, type SheetCell } from './sheet';

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

test('a CSV is the text it was written as: no number, date or time is guessed from it, and a workbook keeps its values', async () => {
  // SheetJS read `01/10/2026` as 10 January, `18:00` as a day of 1899 and `1,5` as fifteen.
  const csv = await readWorkbook(
    new Blob([
      'Day;From;Share;Code;Flag\r\n01/10/2026;18:00;1,5;007;TRUE\r\n2026-10-01T18:00:00Z;;250;1.5;\r\n',
    ]),
  );
  expect(csv?.cells(csv.sheets[0] ?? '')).toEqual([
    ['Day', 'From', 'Share', 'Code', 'Flag'],
    ['01/10/2026', '18:00', '1,5', '007', 'TRUE'],
    ['2026-10-01T18:00:00Z', null, '250', '1.5', null],
  ]);

  // The same cells in a workbook are what the workbook stored: a date and a time are numbers of days, a number a number.
  const sheet = XLSX.utils.aoa_to_sheet([['Day', 'From', 'Share', 'Code', 'Flag']]);
  sheet.A2 = { t: 'n', v: 46296, z: 'dd/mm/yyyy' };
  sheet.B2 = { t: 'n', v: 0.75, z: 'hh:mm' };
  sheet.C2 = { t: 'n', v: 1.5 };
  sheet.D2 = { t: 's', v: '007' };
  sheet.E2 = { t: 'b', v: true };
  sheet['!ref'] = 'A1:E2';
  const book = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(book, sheet, 'Data');
  const workbook = await readWorkbook(
    new Blob([XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer]),
  );
  expect(workbook?.cells('Data')).toEqual([
    ['Day', 'From', 'Share', 'Code', 'Flag'],
    [46296, 0.75, 1.5, '007', true],
  ]);
});

/** The text of a cell of a CSV, as a cell of `Value;Next`, read as the text a sheet shows. */
async function formattedCsvCell(written: string): Promise<SheetCell | undefined> {
  const workbook = await readWorkbook(new Blob([`Value;Next\r\n${written};end\r\n`]), { locale: 'it' });
  return workbook?.cells(workbook.sheets[0] ?? '', { formatted: true })[1]?.[0];
}

// The text the table copies from a CSV, the same as when SheetJS guessed a value from it: SheetJS kept the text written.
test.each([
  ['007', '007'],
  ['1,5', '1,5'],
  ['1.5', '1.5'],
  ['250', '250'],
  ['1e3', '1e3'],
  ['50%', '50%'],
  ['€ 5', '€ 5'],
  ['1.234,5', '1.234,5'],
  ['-', '-'],
  ['01/10/2026', '01/10/2026'],
  ['18:00', '18:00'],
  ['2026-10-01T18:00:00Z', '2026-10-01T18:00:00Z'],
  ['TRUE', 'TRUE'],
  ['FALSE', 'FALSE'],
  ['=1+1', '=1+1'],
  ['="007"', '="007"'],
  ['" x "', ' x '],
  ['"say ""hi"""', 'say "hi"'],
])('a CSV cell written %s reads, as the sheet shows it, %s', async (written, shown) => {
  expect(await formattedCsvCell(written)).toBe(shown);
});

// An error of Excel written in a CSV was an empty cell, lost without a word — and an empty cell of a table stops its
// publication. It is the text written, as a sheet shows it.
test.each(['#N/A', '#DIV/0!', '#REF!'])(
  'an error of Excel written in a CSV, %s, is its text and not an empty cell',
  async (written) => {
    expect(await formattedCsvCell(written)).toBe(written);
  },
);

test("a language's short date is its own order and separators, from Intl, quoted", () => {
  expect(shortDateFormat('it')).toBe('dd"/"mm"/"yyyy');
  expect(shortDateFormat('en-US')).toBe('mm"/"dd"/"yyyy');
  expect(shortDateFormat('de')).toBe('dd"."mm"."yyyy');
});

test('a file that is not a sheet is null, not an exception', async () => {
  expect(await readWorkbook(new Blob([new Uint8Array([0x50, 0x4b, 0x03, 0x04, 0, 0, 0])]))).toBeNull();
});
