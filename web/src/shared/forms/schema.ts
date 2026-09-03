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
}

export type FieldNode =
  | { kind: 'text'; path: string; meta: FieldMeta; optional: boolean }
  | { kind: 'number'; path: string; meta: FieldMeta; optional: boolean }
  | { kind: 'boolean'; path: string; meta: FieldMeta; optional: boolean }
  | { kind: 'enum'; path: string; meta: FieldMeta; optional: boolean; options: string[] }
  | { kind: 'localized'; path: string; meta: FieldMeta; optional: boolean }
  | { kind: 'object'; path: string; meta: FieldMeta; optional: boolean; children: FieldNode[] }
  | { kind: 'list'; path: string; meta: FieldMeta; optional: boolean; children: FieldNode[] };

/**
 * A translated field. The server marks the same thing in the contract with `x-localized`; here the
 * annotation is on the schema, which is what the generator can actually read at runtime.
 */
export function localized() {
  // The return type is inferred rather than declared: widening it to `z.ZodType` would erase the
  // input side, and the form resolver needs both.
  return z.record(z.string(), z.string()).meta({ localized: true });
}

/** The shape zod exposes. Narrow on purpose: only what the walk below actually looks at. */
interface ZodInternals {
  type: string;
  innerType?: unknown;
  element?: unknown;
  entries?: Record<string, string>;
  shape?: Record<string, unknown>;
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
function unwrap(schema: unknown): { inner: unknown; meta: FieldMeta; optional: boolean } {
  const wrappers = new Set(['optional', 'nullable', 'default', 'prefault', 'nonoptional', 'readonly']);

  let current = schema;
  let meta: FieldMeta = annotation(current);
  let optional = false;

  for (;;) {
    const def = definition(current);
    if (!wrappers.has(def.type) || def.innerType === undefined) {
      return { inner: current, meta, optional };
    }

    optional ||= def.type === 'optional' || def.type === 'nullable';
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

function readField(schema: unknown, path: string): FieldNode {
  const { inner, meta, optional } = unwrap(schema);
  const def = definition(inner);
  const common = { path, meta, optional };

  if (meta.localized === true) {
    // Annotated wins over shape: a translated field is a record, and a record of anything else is
    // not something this generator draws.
    return { kind: 'localized', ...common };
  }

  switch (def.type) {
    case 'string':
      return { kind: 'text', ...common };
    case 'number':
    case 'int':
      return { kind: 'number', ...common };
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
