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
  | { kind: 'number'; field: NumberKey<TRow>; sortable: boolean; editable: boolean }
  | { kind: 'boolean'; field: BooleanKey<TRow>; sortable: boolean }
  | { kind: 'date'; field: TextKey<TRow>; sortable: boolean }
  | { kind: 'department'; field: TextKey<TRow>; sortable: boolean }
  | { kind: 'badge'; field: TextKey<TRow>; sortable: boolean; labels: string; editable: readonly string[] }
  | { kind: 'media'; field: NumberKey<TRow>; sortable: boolean }
  | { kind: 'file'; field: NumberKey<TRow>; sortable: boolean };

type KeysOfType<TRow, TValue> = {
  [K in keyof TRow & string]: TRow[K] extends TValue ? K : never;
}[keyof TRow & string];

type TextKey<TRow> = KeysOfType<TRow, string | null>;
type NumberKey<TRow> = KeysOfType<TRow, number | null>;
type BooleanKey<TRow> = KeysOfType<TRow, boolean | null>;
type LocalizedKey<TRow> = KeysOfType<TRow, Record<string, string> | null>;

/** `sortable` defaults to false: a column the server did not declare sortable answers 400. */
type Options = { sortable?: boolean };

/**
 * A column somebody may change without opening the row.
 *
 * ⚠️ Only two kinds have it, and that is the decision rather than an omission (note
 * `2026-09-08-modificare-da-una-lista.md`): a number and a closed set of words are the whole of
 * what a cell can ask for honestly. A translated value, a file or a free text need the form, and a
 * cell that opened half of one would be the second way of writing a row that plan §16.6 forbids.
 *
 * It does nothing on its own: the list draws a field only when the screen also hands it a way to
 * save, because saving means reading the row and writing it back, and only the feature knows how.
 */
type Editable = { editable?: boolean };

export const col = {
  /** A plain column, as it is written. */
  text<TRow>(field: TextKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'text', field, sortable: options.sortable ?? false };
  },

  /** A translated column, read in the language on screen. */
  localized<TRow>(field: LocalizedKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'localized', field, sortable: options.sortable ?? false };
  },

  number<TRow>(field: NumberKey<TRow>, options: Options & Editable = {}): ColumnSpec<TRow> {
    return {
      kind: 'number',
      field,
      sortable: options.sortable ?? false,
      editable: options.editable ?? false,
    };
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
   * A file of the library, held as its identifier and drawn as a thumbnail. A column showing the
   * number itself would be a column nobody can read: what somebody scanning a list of news wants
   * to know about a cover is whether there is one and which picture it is.
   */
  media<TRow>(field: NumberKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'media', field, sortable: options.sortable ?? false };
  },

  /**
   * A file of the library that is not necessarily a picture, drawn as a link that opens it. It is
   * the honest column for an attachment: a list row does not carry the type of what it points at,
   * and a thumbnail handed a PDF draws a broken image — which reads as a failed upload. What
   * somebody scanning a list of documents needs to know is whether there is a file and how to
   * reach it, and that is what a link says.
   */
  file<TRow>(field: NumberKey<TRow>, options: Options = {}): ColumnSpec<TRow> {
    return { kind: 'file', field, sortable: options.sortable ?? false };
  },

  /**
   * A closed set of values, drawn as a badge and read from i18n under
   * `<labels>.options.<field>.<value>` — the same place the form generator reads a select from.
   */
  /**
   * A word out of a closed set, drawn with the sentence the language files give it.
   *
   * `editable` is that set: a cell that offers a choice has to know what the choices are, and a
   * boolean could not carry them. Left out, the column is read as it always was.
   */
  badge<TRow>(
    field: TextKey<TRow>,
    labels: string,
    options: Options & { editable?: readonly string[] } = {},
  ): ColumnSpec<TRow> {
    return {
      kind: 'badge',
      field,
      labels,
      sortable: options.sortable ?? false,
      editable: options.editable ?? [],
    };
  },
};
