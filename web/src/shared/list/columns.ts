/**
 * How a list declares its columns. A feature writes `col.localized('title')` and nothing else: no
 * cell renderer, no header, no date formatting, no badge (design M0 §7.5).
 *
 * These are descriptions, not components, which is why `features/<x>/list.ts` is plain TypeScript
 * with no JSX in it. `DataList` is what turns a description into a column, and it is the only place
 * that knows how a date or a translated value is drawn.
 */

/** The kinds of cell M0 needs. A new one is a line here, never a renderer in a screen. */
export type ColumnSpec<TRow> =
  | { kind: 'text'; field: TextKey<TRow>; sortable: boolean }
  | { kind: 'localized'; field: LocalizedKey<TRow>; sortable: boolean }
  | { kind: 'number'; field: NumberKey<TRow>; sortable: boolean }
  | { kind: 'boolean'; field: BooleanKey<TRow>; sortable: boolean }
  | { kind: 'date'; field: TextKey<TRow>; sortable: boolean }
  | { kind: 'department'; field: TextKey<TRow>; sortable: boolean }
  | { kind: 'badge'; field: TextKey<TRow>; sortable: boolean; labels: string };

type KeysOfType<TRow, TValue> = {
  [K in keyof TRow & string]: TRow[K] extends TValue ? K : never;
}[keyof TRow & string];

type TextKey<TRow> = KeysOfType<TRow, string | null>;
type NumberKey<TRow> = KeysOfType<TRow, number | null>;
type BooleanKey<TRow> = KeysOfType<TRow, boolean | null>;
type LocalizedKey<TRow> = KeysOfType<TRow, Record<string, string> | null>;

/** `sortable` defaults to false: a column the server did not declare sortable answers 400. */
type Options = { sortable?: boolean };

export const col = {
  /** A plain column, as it is written. */
  text<TRow>(field: TextKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'text', field, sortable: options.sortable ?? false };
  },

  /** A translated column, read in the language on screen. */
  localized<TRow>(field: LocalizedKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'localized', field, sortable: options.sortable ?? false };
  },

  number<TRow>(field: NumberKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'number', field, sortable: options.sortable ?? false };
  },

  /** Yes or no, drawn as the status badge so a list reads at a glance. */
  boolean<TRow>(field: BooleanKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'boolean', field, sortable: options.sortable ?? false };
  },

  /** An instant, shown in UTC and in the time zone of the division (docs/UI-GUIDELINES.md). */
  date<TRow>(field: TextKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'date', field, sortable: options.sortable ?? false };
  },

  /** The owner department, as its badge. */
  department<TRow>(field: TextKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'department', field, sortable: options.sortable ?? false };
  },

  /**
   * A closed set of values, drawn as a badge and read from i18n under
   * `<labels>.options.<field>.<value>` — the same place the form generator reads a select from.
   */
  badge<TRow>(field: TextKey<TRow>, labels: string, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'badge', field, labels, sortable: options.sortable ?? false };
  },
};
