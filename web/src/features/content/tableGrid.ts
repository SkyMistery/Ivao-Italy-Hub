import { cellText, type SheetCell } from '../../shared/files/sheet';
import { emptyLocalized } from '../../shared/i18n/localized';

/**
 * The table block's columns and rows as the editor writes them outside the generated form (design of the editor's phase
 * 3, §5.1, PR 3.12): pasted from a spreadsheet, read from a file, made N × M, or written in the grid
 * (`BlockRegistration.editedAsGrid`). Pure functions over the block's `props`: what they return goes back through the
 * panel's `onApplyProps`, like everything the form writes.
 */

type Localized = Record<string, string>;

export interface TableColumn {
  label: Localized;
  align: string;
}

export interface TableShape {
  columns: TableColumn[];
  /** Every row has one cell per column: a grid is a rectangle. */
  rows: Localized[][];
}

/**
 * How big a table the editor makes. Not a limit of the block — a body has its own, in bytes, on the server — but of the
 * grid, which draws a box per cell in every language: a sheet of a thousand rows is a list, not a table of a page.
 */
export const TABLE_LIMITS = { columns: 20, rows: 200 } as const;

const DEFAULT_ALIGN = 'left';

function localizedOf(value: unknown, locales: readonly string[]): Localized {
  const written =
    value !== null && typeof value === 'object' && !Array.isArray(value)
      ? Object.fromEntries(
          Object.entries(value as Record<string, unknown>).filter(
            (entry): entry is [string, string] => typeof entry[1] === 'string',
          ),
        )
      : {};

  return { ...emptyLocalized(locales), ...written };
}

function listOf(value: unknown): Record<string, unknown>[] {
  return Array.isArray(value)
    ? value.filter((entry): entry is Record<string, unknown> => entry !== null && typeof entry === 'object')
    : [];
}

/**
 * The table a block holds, as a rectangle: as many columns as the widest of its heading and its rows — a row may carry
 * a cell more than the columns, left by a column taken away in the form, and the grid shows it rather than hide what
 * the page draws —, every row filled to that width with empty cells, every text in every language of the division.
 */
export function tableOf(props: Record<string, unknown>, locales: readonly string[]): TableShape {
  const columns = listOf(props.columns);
  const rows = listOf(props.rows).map((row) =>
    listOf(row.cells).map((cell) => localizedOf(cell.text, locales)),
  );
  const width = Math.max(columns.length, ...rows.map((row) => row.length));

  return {
    columns: Array.from({ length: width }, (_, index) => ({
      label: localizedOf(columns[index]?.label, locales),
      align: typeof columns[index]?.align === 'string' ? columns[index].align : DEFAULT_ALIGN,
    })),
    rows: rows.map((row) => [
      ...row,
      ...Array.from({ length: width - row.length }, () => emptyLocalized(locales)),
    ]),
  };
}

/** The block's props with this table in them; whatever else they hold — the caption, the style of the rows — stays. */
export function withTable(props: Record<string, unknown>, table: TableShape): Record<string, unknown> {
  return {
    ...props,
    columns: table.columns.map((column) => ({ label: column.label, align: column.align })),
    rows: table.rows.map((row) => ({ cells: row.map((text) => ({ text })) })),
  };
}

/** An empty table of `rows` × `columns`, with a heading to write. */
export function emptyTable(rows: number, columns: number, locales: readonly string[]): TableShape {
  return {
    columns: Array.from({ length: columns }, () => ({
      label: emptyLocalized(locales),
      align: DEFAULT_ALIGN,
    })),
    rows: Array.from({ length: rows }, () => Array.from({ length: columns }, () => emptyLocalized(locales))),
  };
}

export type SheetTable =
  | { kind: 'table'; table: TableShape }
  | { kind: 'empty' }
  | { kind: 'tooLarge'; rows: number; columns: number };

/**
 * The rows of a sheet or of a paste made a table, written in `locale` — the language on screen — with the others left
 * to write (design §5.1). The first row is the heading when `header` says so; otherwise the heading is left to write. A
 * row with nothing in it is not a row of the table, and nor is a column with nothing in it at the right-hand end: a
 * selection of a sheet often takes one more of each. A line break inside a cell becomes a space. A table larger than
 * `TABLE_LIMITS` is refused, not cut.
 */
export function tableFromSheet(
  sheet: readonly (readonly (SheetCell | string)[])[],
  { header, locale, locales }: { header: boolean; locale: string; locales: readonly string[] },
): SheetTable {
  // A line break inside a cell is a space: the page draws it as one anyway, and a box of the grid is one line.
  const rows = sheet
    .map((row) => row.map((cell) => (cellText(cell) ?? '').replace(/\s*\n\s*/g, ' ')))
    .filter((row) => row.some((cell) => cell !== ''));
  const width = Math.max(0, ...rows.map((row) => row.findLastIndex((cell) => cell !== '') + 1));
  if (rows.length === 0 || width === 0) {
    return { kind: 'empty' };
  }

  const body = header ? rows.slice(1) : rows;
  if (width > TABLE_LIMITS.columns || body.length > TABLE_LIMITS.rows) {
    return { kind: 'tooLarge', rows: body.length, columns: width };
  }

  const written = (text: string) => ({ ...emptyLocalized(locales), [locale]: text });
  const cellsOf = (row: readonly string[]) =>
    Array.from({ length: width }, (_, index) => written(row[index] ?? ''));

  return {
    kind: 'table',
    table: {
      columns: (header ? cellsOf(rows[0] ?? []) : cellsOf([])).map((label) => ({
        label,
        align: DEFAULT_ALIGN,
      })),
      rows: body.map(cellsOf),
    },
  };
}

// ---- what the grid does to a table ---------------------------------------------------------------------------------

export function withCell(
  table: TableShape,
  row: number,
  column: number,
  locale: string,
  text: string,
): TableShape {
  return {
    ...table,
    rows: table.rows.map((cells, at) =>
      at === row
        ? cells.map((cell, index) => (index === column ? { ...cell, [locale]: text } : cell))
        : cells,
    ),
  };
}

export function withLabel(table: TableShape, column: number, locale: string, text: string): TableShape {
  return {
    ...table,
    columns: table.columns.map((entry, index) =>
      index === column ? { ...entry, label: { ...entry.label, [locale]: text } } : entry,
    ),
  };
}

export function withRowAdded(table: TableShape, locales: readonly string[]): TableShape {
  return { ...table, rows: [...table.rows, table.columns.map(() => emptyLocalized(locales))] };
}

export function withColumnAdded(table: TableShape, locales: readonly string[]): TableShape {
  return {
    columns: [...table.columns, { label: emptyLocalized(locales), align: DEFAULT_ALIGN }],
    rows: table.rows.map((cells) => [...cells, emptyLocalized(locales)]),
  };
}

export function withoutRow(table: TableShape, row: number): TableShape {
  return { ...table, rows: table.rows.filter((_, at) => at !== row) };
}

export function withoutColumn(table: TableShape, column: number): TableShape {
  return {
    columns: table.columns.filter((_, index) => index !== column),
    rows: table.rows.map((cells) => cells.filter((_, index) => index !== column)),
  };
}

/** Whether every heading and every cell holds something in `locale`: what takes the "empty" badge off its tab. */
export function writtenIn(table: TableShape, locale: string): boolean {
  return [...table.columns.map((column) => column.label), ...table.rows.flat()].every(
    (text) => (text[locale] ?? '').trim() !== '',
  );
}
