import { expect, test } from 'vitest';

import {
  TABLE_LIMITS,
  emptyTable,
  tableFromSheet,
  tableOf,
  withCell,
  withColumnAdded,
  withLabel,
  withRowAdded,
  withTable,
  withoutColumn,
  withoutRow,
  writtenIn,
} from './tableGrid';

/** The table as the editor writes it outside the generated form (design of the editor's phase 3, §5.1, PR 3.12). */

const locales = ['it', 'en'];
const both = (it: string, en: string) => ({ it, en });

test('a sheet becomes columns and rows in the language on screen, the others left to write', () => {
  const made = tableFromSheet(
    [
      ['Giorno', 'Dalle'],
      ['Lunedì', '18:00'],
    ],
    { header: true, locale: 'it', locales },
  );

  expect(made).toEqual({
    kind: 'table',
    table: {
      columns: [
        { label: both('Giorno', ''), align: 'left' },
        { label: both('Dalle', ''), align: 'left' },
      ],
      rows: [[both('Lunedì', ''), both('18:00', '')]],
    },
  });
});

test('without a heading every row is a row, and the headings are left to write', () => {
  const made = tableFromSheet([['a', 'b']], { header: false, locale: 'en', locales });

  expect(made.kind === 'table' && made.table.columns.map((column) => column.label)).toEqual([
    both('', ''),
    both('', ''),
  ]);
  expect(made.kind === 'table' && made.table.rows).toEqual([[both('', 'a'), both('', 'b')]]);
});

test('empty rows and the empty columns at the right are not part of the table; a gap inside a row is', () => {
  const made = tableFromSheet([['a', null, 'c', null, ''], [null, null, null], ['d', 'e'], []], {
    header: false,
    locale: 'it',
    locales,
  });

  expect(made.kind === 'table' && made.table.rows.map((row) => row.map((cell) => cell.it))).toEqual([
    ['a', '', 'c'],
    ['d', 'e', ''],
  ]);
});

test('a line break inside a cell is a space, as the page draws it', () => {
  const made = tableFromSheet([['Milano\nRadar', 'a \n b']], { header: false, locale: 'it', locales });

  expect(made.kind === 'table' && made.table.rows[0]?.map((cell) => cell.it)).toEqual([
    'Milano Radar',
    'a b',
  ]);
});

test('nothing to read is said, and a table larger than the grid is refused rather than cut', () => {
  expect(tableFromSheet([[null, '  ']], { header: true, locale: 'it', locales })).toEqual({ kind: 'empty' });

  const wide = [Array.from({ length: TABLE_LIMITS.columns + 1 }, () => 'x')];
  expect(tableFromSheet(wide, { header: false, locale: 'it', locales })).toEqual({
    kind: 'tooLarge',
    rows: 1,
    columns: TABLE_LIMITS.columns + 1,
  });

  // The heading is not a row: the limit counts the rows under it.
  const long = Array.from({ length: TABLE_LIMITS.rows + 1 }, () => ['x']);
  expect(tableFromSheet(long, { header: true, locale: 'it', locales }).kind).toBe('table');
  expect(tableFromSheet(long, { header: false, locale: 'it', locales }).kind).toBe('tooLarge');
});

test('a block is read as a rectangle, every language there, and written back with the rest of its props', () => {
  const props = {
    caption: both('Orari', 'Hours'),
    rowStyle: 'striped',
    columns: [{ label: { it: 'Giorno' }, align: 'right' }],
    rows: [{ cells: [{ text: both('Lun', 'Mon') }, { text: { en: 'extra' } }] }, { cells: [] }],
  };

  const table = tableOf(props, locales);

  // The cell beyond the last column is shown, with a column of its own, rather than hidden.
  expect(table.columns).toEqual([
    { label: both('Giorno', ''), align: 'right' },
    { label: both('', ''), align: 'left' },
  ]);
  expect(table.rows).toEqual([
    [both('Lun', 'Mon'), both('', 'extra')],
    [both('', ''), both('', '')],
  ]);

  expect(withTable(props, table)).toEqual({
    caption: both('Orari', 'Hours'),
    rowStyle: 'striped',
    columns: table.columns,
    rows: [
      { cells: [{ text: both('Lun', 'Mon') }, { text: both('', 'extra') }] },
      { cells: [{ text: both('', '') }, { text: both('', '') }] },
    ],
  });
});

test('a block with no table yet is an empty one', () => {
  expect(tableOf({}, locales)).toEqual({ columns: [], rows: [] });
});

test('the grid writes a cell or a heading in one language, and adds and takes away rows and columns', () => {
  let table = emptyTable(2, 2, locales);
  expect(writtenIn(table, 'it')).toBe(false);

  table = withLabel(withLabel(table, 0, 'it', 'A'), 1, 'it', 'B');
  table = withCell(withCell(table, 0, 0, 'it', 'a1'), 0, 1, 'it', 'b1');
  table = withCell(withCell(table, 1, 0, 'it', 'a2'), 1, 1, 'it', 'b2');
  expect(writtenIn(table, 'it')).toBe(true);
  expect(writtenIn(table, 'en')).toBe(false);

  table = withColumnAdded(withRowAdded(table, locales), locales);
  expect(table.columns).toHaveLength(3);
  expect(table.rows.map((row) => row.length)).toEqual([3, 3, 3]);

  table = withoutColumn(withoutRow(table, 0), 0);
  expect(table.columns.map((column) => column.label.it)).toEqual(['B', '']);
  expect(table.rows.map((row) => row.map((cell) => cell.it))).toEqual([
    ['b2', ''],
    ['', ''],
  ]);
});
