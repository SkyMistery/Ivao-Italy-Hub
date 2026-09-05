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
   * A closed set of values, drawn as a select rather than a free input, with the values themselves
   * as the labels. Two different needs, one annotation:
   * <br />— a **number**, because a `z.enum` would make the value a *string*, and every string
   * inside a block's properties is extracted as the text of the page for the search index: the
   * level of a heading is not text (design M0 §5.3);
   * <br />— a **string** whose set is only known at runtime, so it cannot be a `z.enum` at all. The
   * permission catalogue is the first: what it holds depends on which modules are installed, and
   * its members are identifiers rather than prose — `Links.Edit` is shown as `Links.Edit` in every
   * language, exactly as a VID or a department code is.
   */
  choices?: readonly number[] | readonly string[];
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

export type FieldNode =
  | ({ kind: 'text'; choices: string[] | null } & FieldCommon)
  | ({ kind: 'number'; choices: number[] | null } & FieldCommon)
  | ({ kind: 'boolean' } & FieldCommon)
  | ({ kind: 'enum'; options: string[] } & FieldCommon)
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

/** The values of a `choices` annotation, when they are the kind this field can hold. */
function stringChoices(choices: FieldMeta['choices']): string[] | null {
  return choices?.every((choice) => typeof choice === 'string') ? [...choices] : null;
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
      return { kind: 'text', ...common, choices: stringChoices(meta.choices) };
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
