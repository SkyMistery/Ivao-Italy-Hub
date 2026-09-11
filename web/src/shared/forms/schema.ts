import { z } from 'zod';

/**
 * The zod schema of an entity, read as a list of fields. This is the whole of what `SchemaForm`
 * knows: give it a schema and it draws the form, so a back office screen is configuration and not
 * JSX (design M0 §7.5).
 *
 * Everything that reaches into zod's own shape is in this file and nowhere else. A schema is
 * described by `.def` and annotated by `.meta()`, and both are read here so that a version of zod
 * that moves them is one file to fix rather than a form generator to rewrite.
 */

/** What `.meta({ ... })` may say about a field. Anything else is ignored. */
export interface FieldMeta {
  /** A translated field: `LocaleFields` draws it, one tab per language of the division. */
  localized?: boolean;
  /** A long text: a textarea rather than a single line input. */
  multiline?: boolean;
  /** Carried by the form and submitted, never shown. `rowVersion` is the reason this exists. */
  hidden?: boolean;
  /**
   * A closed set of values, drawn as a select rather than a free input. Three needs, one
   * annotation:
   * <br />— a **number**, because a `z.enum` would make the value a *string*, and every string
   * inside a block's properties is extracted as the text of the page for the search index: the
   * level of a heading is not text (design M0 §5.3);
   * <br />— a **string** whose set is only known at runtime, so it cannot be a `z.enum` at all. The
   * permission catalogue is the first: what it holds depends on which modules are installed, and
   * its members are identifiers rather than prose — `Links.Edit` is shown as `Links.Edit` in every
   * language, exactly as a VID or a department code is;
   * <br />— a **string whose label is not the value**, written as `{ value, label }`. The category
   * of a news item is the first: the value is the stable key stored on the row, the label is the
   * translated word a coordinator wrote in `cms_categories`, and neither an i18n key nor the value
   * itself could stand in for it (design M1 §3.4). The caller has already resolved the label into
   * the language on screen — the generator never translates a value it was handed.
   */
  choices?: readonly number[] | readonly string[] | readonly ChoiceOption[];
  /**
   * A file of the media library, held as its identifier. The field opens `MediaPicker` and shows
   * what was chosen; it is never a number to type, because a free numeric field produces pages
   * pointing at files that were deleted years ago (design M1 section 1.5).
   */
  media?: boolean;
  /**
   * An icon, held as a `lucide` name out of the allow list in `shared/icons`. A choice among thirty
   * pictures is a choice; a box for any of the thousands `lucide` ships is a text field with extra
   * steps.
   */
  icon?: boolean;
  /** A calendar day, held as an ISO instant at midnight UTC. */
  date?: boolean;
  /** An instant, held as ISO UTC and shown in UTC and in the time zone of the division. */
  datetime?: boolean;
  /** One small object per language. Set by `localizedObject()`, never written by hand. */
  localizedObject?: boolean;
  /**
   * The path of the field this one proposes itself from: a title, and the address made out of it.
   * The proposal follows the source while nobody has written in this field, and stops the moment
   * somebody does — an address that kept rewriting itself under the person typing it would be
   * worse than one they had to type in full.
   *
   * It is a proposal and never a rule: what an address may look like is the server's to say, and
   * it says it (`slugify` explains where that line is). A source that is translated is read in the
   * default language of the division, which is the language of the address the site publishes.
   *
   * The seventh extension of the generator, asked for by Carmine after the demo of M1: an address
   * was typed from scratch beside a title that had just been written.
   */
  /**
   * What a field offers while somebody types, **without** closing the set: the address of a menu
   * entry is the case it was built for — the pages of the site, grouped by the department that
   * wrote them, and an address of somewhere else typed in full.
   *
   * ⚠️ It is not `choices`. A select refuses everything it does not list, and a menu that could
   * only point at a page of this site would be a menu that cannot link the forum. The value stays
   * free text: the suggestions are a way of not typing, never a rule (asked for while running the
   * demo of M1, part 1).
   */
  suggestions?: readonly Suggestion[];
  /**
   * With `suggestions`, whether the list is the **whole** of what the field accepts.
   *
   * ⚠️ It is the difference between offering and deciding, and the menu is the case that wanted the
   * second (decided by Carmine on 8 September 2026): a menu entry leads to a page of this site, to
   * a screen of the application, or to a link of the library — so that **every address leaving the
   * site lives in one table**, and changing where the forum lives is one row rather than a hunt.
   *
   * The field still filters as somebody types: what closing it changes is that a word nobody
   * offered is put back rather than kept. The server refuses it too, which is what makes it a rule
   * rather than a habit of one screen.
   */
  suggestionsOnly?: boolean;
  slugFrom?: string;
  /**
   * What the proposal starts with, for a field that is a **path** rather than a slug: the menu
   * writes `/chi-siamo` where a page writes `chi-siamo`. It is a separate annotation and not a
   * shape of `slugFrom`, so that reading a schema stays reading one word per idea.
   */
  slugPrefix?: string;
  /**
   * An array of values out of `choices`, drawn as one checkbox each rather than as a repeatable
   * list. It is the difference between "pick several of a closed set" and "write as many of these
   * as you like": the first has an answer that fits on the screen, and a list of selects for it is
   * five gestures where there should be one click.
   *
   * The sixth extension of the generator, and the first that design M1 section 12 did not predict:
   * `allowedBlocks` of a template section is a subset of the block registry (design M1 section 9.1).
   */
  multi?: boolean;
}

/**
 * What a select puts in the DOM for "nothing chosen". An optional enum needs one because the empty
 * string is reserved by the underlying select, and `undefined` is not a value a DOM node can hold.
 * No option of any schema of this hub is spelled like this.
 */
export const NO_CHOICE = '__none__';

/**
 * What every field says about itself, whatever its kind.
 *
 * `defaultValue` is the one the schema declares with `.default(...)`, and it is read rather than
 * guessed: what a new row or a new block starts with belongs next to the field it belongs to, and
 * a caller that invented its own would be the second description of the same thing.
 */
interface FieldCommon {
  path: string;
  meta: FieldMeta;
  optional: boolean;
  defaultValue: unknown;
}

/** One entry of a select whose label is not its value. */
export interface ChoiceOption {
  value: string;
  label: string;
}

/**
 * One thing a field offers without demanding it. `group` is a heading in the list — "the pages of
 * Events", "the pages of Training" — and is what tells thirty suggestions apart from a wall.
 */
export interface Suggestion extends ChoiceOption {
  group?: string;
}

export type FieldNode =
  | ({ kind: 'text'; choices: ChoiceOption[] | null } & FieldCommon)
  | ({ kind: 'suggest'; suggestions: Suggestion[]; only: boolean } & FieldCommon)
  | ({ kind: 'number'; choices: number[] | null } & FieldCommon)
  | ({ kind: 'boolean' } & FieldCommon)
  | ({ kind: 'enum'; options: string[] } & FieldCommon)
  | ({ kind: 'multi'; options: ChoiceOption[] } & FieldCommon)
  | ({ kind: 'localized' } & FieldCommon)
  | ({ kind: 'media' } & FieldCommon)
  | ({ kind: 'icon' } & FieldCommon)
  | ({ kind: 'instant'; withTime: boolean } & FieldCommon)
  | ({ kind: 'localizedObject'; children: FieldNode[] } & FieldCommon)
  | ({ kind: 'object'; children: FieldNode[] } & FieldCommon)
  | ({ kind: 'list'; children: FieldNode[] } & FieldCommon);

/**
 * A translated field. The server marks the same thing in the contract with `x-localized`; here the
 * annotation is on the schema, which is what the generator can actually read at runtime.
 */
export function localized() {
  // The return type is inferred rather than declared: widening it to `z.ZodType` would erase the
  // input side, and the form resolver needs both.
  return z.record(z.string(), z.string()).meta({ localized: true });
}

/**
 * A translated **object**: one value per language, each of them a small record rather than a line
 * of text. `Seo` is the first — a title, a description and a picture per language (design M1
 * section 9.2) — and it is why this exists: a coordinator does not write JSON, and the column is a
 * `Localized<JsonNode>` that nothing could draw before.
 *
 * The shape is handed in rather than inferred so that its fields are drawn by the same rules as
 * any other object's: a media selector inside a language tab is a media selector.
 */
export function localizedObject<TShape extends z.ZodRawShape>(shape: TShape) {
  return z.record(z.string(), z.object(shape)).meta({ localizedObject: true });
}

/**
 * What a field holds when nobody has written into it yet.
 *
 * It lives here, next to the reader of the schema, because three things ask the same question: a
 * new block, a new entry of a repeatable list, and a form that must not hand React an input with
 * no value. Three answers would be three descriptions of the same defaults, and the one that would
 * go stale is whichever is furthest from the schema.
 */
export function blankValue(node: FieldNode, locales: readonly string[]): unknown {
  if (node.defaultValue !== undefined) {
    // What the schema says beats what the kind implies: a `limit` that declares 10 starts at 10.
    return node.defaultValue;
  }

  switch (node.kind) {
    case 'localized':
      return Object.fromEntries(locales.map((locale) => [locale, '']));
    // A field that suggests starts empty like any other text: what it offers is a way of not
    // typing, not a value somebody chose.
    case 'text':
    case 'suggest':
      return '';
    case 'number':
      return node.choices?.[0] ?? 0;
    case 'boolean':
      return false;
    case 'enum':
      // An optional choice starts at "nothing chosen", which is absent from the payload rather
      // than an empty string the server would have to interpret.
      return node.optional ? undefined : (node.options[0] ?? '');
    case 'list':
    case 'multi':
      return [];
    case 'object':
      return blankEntry(node.children, node.path, locales);
    // A file, an icon and an instant start at nothing chosen. Each of them draws that state — no
    // picture, no icon, an empty date — so there is nothing to invent here.
    case 'media':
    case 'icon':
    case 'instant':
    case 'localizedObject':
      return undefined;
  }
}

/**
 * A fresh entry of an object or of a repeatable list. The children carry the whole path from the
 * top of the schema, because that is what their label is looked up by; the key inside the object
 * is only the last segment of it.
 */
export function blankEntry(
  children: FieldNode[],
  path: string,
  locales: readonly string[],
): Record<string, unknown> {
  return Object.fromEntries(
    children.map((child) => [child.path.slice(path.length + 1), blankValue(child, locales)]),
  );
}

/** Every field of a schema at its blank value: what a new row, or a new block, starts holding. */
export function blankValues(
  schema: z.ZodType<Record<string, unknown>>,
  locales: readonly string[],
): Record<string, unknown> {
  return Object.fromEntries(readFields(schema).map((field) => [field.path, blankValue(field, locales)]));
}

/**
 * Whether a value is one nobody has written into. A translated value counts as unwritten only when
 * *every* language of it is empty: one language written and another not is a hole, and saying so is
 * publication's job, not this one's.
 */
function unwritten(node: FieldNode, value: unknown): boolean {
  if (value === undefined || value === null || value === '') {
    return true;
  }

  switch (node.kind) {
    case 'localized':
      return Object.values(value as Record<string, unknown>).every(
        (written) => typeof written !== 'string' || written.trim() === '',
      );
    case 'list':
    case 'multi':
      return Array.isArray(value) && value.length === 0;
    case 'object':
      return node.children.every((child) =>
        unwritten(child, (value as Record<string, unknown>)[child.path.slice(node.path.length + 1)]),
      );
    default:
      return false;
  }
}

/**
 * Whether nothing that *reads* as content has been written: every field that carries words, a
 * file, a date or a list is still empty, whatever the settings — a level, a tone, a column count —
 * happen to be. What the editor draws a placeholder for (Carmine, 11 September 2026: a heading just
 * added drew nothing and looked lost; "some background text so that whoever looks understands it is
 * there, gone the moment something is written in the properties").
 */
export function isBlank<TValues extends Record<string, unknown>>(
  schema: z.ZodType<TValues>,
  values: TValues,
): boolean {
  return readFields(schema).every((field) => !carriesContent(field) || unwritten(field, values[field.path]));
}

/** A field somebody writes into, as opposed to one they choose a setting in. */
function carriesContent(node: FieldNode): boolean {
  switch (node.kind) {
    case 'localized':
    case 'localizedObject':
    case 'media':
    case 'suggest':
    case 'instant':
    case 'list':
      return true;
    case 'text':
      return node.choices === null;
    case 'object':
      return node.children.some(carriesContent);
    default:
      return false;
  }
}

/**
 * The values worth storing: the same object, without the optional fields nobody filled in.
 *
 * ⚠️ This is not tidiness. Publication refuses a page holding a translated value that is written in
 * one language and not another, and it reads the body without knowing what any block *means* — so
 * an optional `caption` left empty in both languages, carried along as `{ en: "", it: "" }`, would
 * be read as a translation hole and would stop the page from being published. Dropping the key is
 * what makes "optional" mean optional; the rule on the server stays exactly as strict as it was.
 *
 * A required field is never dropped, empty or not: an empty one is a mistake the editor should see
 * named, and it is publication that names it.
 */
export function writtenValues<TValues extends Record<string, unknown>>(
  schema: z.ZodType<TValues>,
  values: TValues,
): TValues {
  const kept: Record<string, unknown> = { ...values };

  for (const field of readFields(schema)) {
    const value = kept[field.path];

    if (field.optional && unwritten(field, value)) {
      delete kept[field.path];
      continue;
    }

    // A list keeps its entries, and each entry is pruned by the same rule: the optional text of a
    // card nobody wrote is exactly the same problem one level down.
    if (field.kind === 'list' && Array.isArray(value)) {
      kept[field.path] = value.map((entry) => pruneEntry(field.children, field.path, entry));
    } else if (field.kind === 'object' && value !== null && typeof value === 'object') {
      kept[field.path] = pruneEntry(field.children, field.path, value);
    }
  }

  return kept as TValues;
}

function pruneEntry(children: FieldNode[], path: string, entry: unknown): unknown {
  if (entry === null || typeof entry !== 'object') {
    return entry;
  }

  const kept: Record<string, unknown> = { ...(entry as Record<string, unknown>) };

  for (const child of children) {
    const key = child.path.slice(path.length + 1);
    const value = kept[key];

    if (child.optional && unwritten(child, value)) {
      delete kept[key];
    } else if (child.kind === 'list' && Array.isArray(value)) {
      kept[key] = value.map((nested) => pruneEntry(child.children, child.path, nested));
    } else if (child.kind === 'object' && value !== null && typeof value === 'object') {
      kept[key] = pruneEntry(child.children, child.path, value);
    }
  }

  return kept;
}

/** The shape zod exposes. Narrow on purpose: only what the walk below actually looks at. */
interface ZodInternals {
  type: string;
  innerType?: unknown;
  element?: unknown;
  /** The value schema of a record, which is what a translated object holds per language. */
  valueType?: unknown;
  entries?: Record<string, string>;
  shape?: Record<string, unknown>;
  /** Present on a `default` wrapper, and in zod 4 it is the value itself and not a thunk. */
  defaultValue?: unknown;
}

function definition(schema: unknown): ZodInternals {
  return (schema as { def: ZodInternals }).def;
}

function annotation(schema: unknown): FieldMeta {
  const read = (schema as { meta?: () => FieldMeta | undefined }).meta;
  return typeof read === 'function' ? (read.call(schema) ?? {}) : {};
}

/**
 * Peels `optional`, `nullable`, `default` and `nonoptional` off a field, collecting the annotations
 * on the way: `z.string().meta({ multiline: true }).optional()` keeps its annotation, and so does
 * the same pair written the other way round.
 */
function unwrap(schema: unknown): {
  inner: unknown;
  meta: FieldMeta;
  optional: boolean;
  defaultValue: unknown;
} {
  const wrappers = new Set(['optional', 'nullable', 'default', 'prefault', 'nonoptional', 'readonly']);

  let current = schema;
  let meta: FieldMeta = annotation(current);
  let optional = false;
  let defaultValue: unknown = undefined;

  for (;;) {
    const def = definition(current);
    if (!wrappers.has(def.type) || def.innerType === undefined) {
      return { inner: current, meta, optional, defaultValue };
    }

    optional ||= def.type === 'optional' || def.type === 'nullable';
    // The outermost default wins, which is the one the schema was written with.
    defaultValue ??= def.type === 'default' || def.type === 'prefault' ? def.defaultValue : undefined;
    current = def.innerType;
    meta = { ...annotation(current), ...meta };
  }
}

/**
 * The fields of an object schema, in declaration order. Unknown kinds are refused rather than
 * skipped: a field that silently does not appear is a field a coordinator cannot fill in, and the
 * rule of the phase is to extend the generator, never to fall back to a hand written form.
 */
export function readFields(schema: z.ZodType, prefix = ''): FieldNode[] {
  const { inner } = unwrap(schema);
  const shape = definition(inner).shape;

  if (!shape) {
    throw new Error(`SchemaForm needs an object schema${prefix ? ` at "${prefix}"` : ''}.`);
  }

  return Object.entries(shape).map(([name, field]) => readField(field, prefix ? `${prefix}.${name}` : name));
}

/**
 * The values of a `choices` annotation, when they are the kind this field can hold. A bare string
 * is its own label, which is the case the permission catalogue needs; a `{ value, label }` pair
 * carries a name the value could not, which is the case a category needs.
 */
function stringChoices(choices: FieldMeta['choices']): ChoiceOption[] | null {
  if (choices === undefined) {
    return null;
  }

  if (choices.every((choice) => typeof choice === 'string')) {
    return choices.map((choice) => ({ value: choice, label: choice }));
  }

  return choices.every((choice) => typeof choice === 'object' && choice !== null)
    ? choices.map((choice) => ({ ...choice }))
    : null;
}

function numberChoices(choices: FieldMeta['choices']): number[] | null {
  return choices?.every((choice) => typeof choice === 'number') ? [...choices] : null;
}

function readField(schema: unknown, path: string): FieldNode {
  const { inner, meta, optional, defaultValue } = unwrap(schema);
  const def = definition(inner);
  const common: FieldCommon = { path, meta, optional, defaultValue };

  if (meta.localized === true) {
    // Annotated wins over shape: a translated field is a record, and a record of anything else is
    // not something this generator draws.
    return { kind: 'localized', ...common };
  }

  if (meta.localizedObject === true) {
    // The children are read from the *value* schema of the record, once, and drawn inside every
    // language tab: one description of what a language holds, not one per language.
    const value = definition(inner).valueType;
    if (value === undefined) {
      throw new Error(`A localized object at "${path}" has no shape. Build it with localizedObject().`);
    }

    return { kind: 'localizedObject', ...common, children: readFields(value as z.ZodType, path) };
  }

  // Annotations that decide what a field *is*, before its type gets a say. Each of them is a
  // promise about the value — an identifier of the media library, a name from the icon allow list,
  // an ISO instant — that the plain type could not carry on its own.
  if (meta.media === true) {
    return { kind: 'media', ...common };
  }

  if (meta.icon === true) {
    return { kind: 'icon', ...common };
  }

  if (meta.date === true || meta.datetime === true) {
    return { kind: 'instant', ...common, withTime: meta.datetime === true };
  }

  switch (def.type) {
    case 'string':
      // Suggestions before choices: a field that offers without demanding is a different field
      // from one that refuses everything it does not list, and only one of the two annotations is
      // ever written on a field.
      return meta.suggestions === undefined
        ? { kind: 'text', ...common, choices: stringChoices(meta.choices) }
        : {
            kind: 'suggest',
            ...common,
            suggestions: [...meta.suggestions],
            only: meta.suggestionsOnly === true,
          };
    case 'number':
    case 'int':
      return { kind: 'number', ...common, choices: numberChoices(meta.choices) };
    case 'boolean':
      return { kind: 'boolean', ...common };
    case 'enum':
      return { kind: 'enum', ...common, options: Object.values(def.entries ?? {}) };
    case 'object':
      return { kind: 'object', ...common, children: readFields(inner as z.ZodType, path) };
    case 'array':
      // Several out of a closed set is a different field from a list somebody fills in, and the
      // annotation is what tells them apart. Without it an array of plain strings would reach
      // `readFields` below, which needs an object and says so.
      if (meta.multi === true) {
        return { kind: 'multi', ...common, options: stringChoices(meta.choices) ?? [] };
      }

      // The children keep the path of the list itself, without an index: `aliases.name` is the
      // label of every entry's name, and the index only ever belongs to the form field.
      return { kind: 'list', ...common, children: readFields(def.element as z.ZodType, path) };
    default:
      throw new Error(
        `SchemaForm does not draw a "${def.type}" at "${path}". Extend the generator rather than ` +
          'writing the form by hand (implementation plan §E).',
      );
  }
}
