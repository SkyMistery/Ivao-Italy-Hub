import { describe, expect, test } from 'vitest';

import englishCommon from '../../../locales/en/common.json';
import italianCommon from '../../../locales/it/common.json';
import { readFields, type FieldNode } from '../shared/forms';

import { coreBlocks } from './registry';

/**
 * The registry, checked against the two things that would otherwise only fail in front of a
 * coordinator: a schema the form generator cannot draw, and a label that is not translated.
 *
 * `pnpm i18n:check` cannot see any of these keys. They are built at run time from the block's own
 * type and the path of a field — `blocks.callout.options.tone.warning` — which is exactly the case
 * the script says is out of its reach, and this is the test it points at instead.
 */

const CATALOGUES = { en: englishCommon, it: italianCommon } as Record<string, unknown>;

/**
 * Whether the language file answers a key, resolved the way i18next resolves one.
 *
 * ⚠️ Not simply a walk down the dots. A key may sit in the file whole — `"seo.title"` is a key of
 * `content.fields`, next to `"seo"` itself — because the two cannot both be nested: a field that is
 * an object has a label of its own *and* a label per child, and one of them would have to overwrite
 * the other. i18next handles that (`deepFind` joins the remaining segments back together and keeps
 * looking when what it found is a string), and so does this: a check stricter than the runtime
 * would fail on keys that work.
 */
function has(catalogue: unknown, key: string): boolean {
  if (catalogue === null || typeof catalogue !== 'object') {
    return false;
  }

  const table = catalogue as Record<string, unknown>;

  if (typeof table[key] === 'string') {
    return true;
  }

  const segments = key.split('.');

  return segments.some((_, index) => {
    if (index === segments.length - 1) {
      return false;
    }

    const head = segments.slice(0, index + 1).join('.');
    return head in table && has(table[head], segments.slice(index + 1).join('.'));
  });
}

/** Every key a block needs: its name, a label per field, and a label per choice of a select. */
function keysOf(type: string, fields: FieldNode[]): string[] {
  return fields.flatMap((field) => [
    `blocks.${type}.fields.${field.path}`,
    ...(field.kind === 'enum'
      ? [
          ...field.options.map((option) => `blocks.${type}.options.${field.path}.${option}`),
          // An optional enum draws a way back to "nothing chosen", and that entry has a label
          // like any other.
          ...(field.optional ? [`blocks.${type}.options.${field.path}.none`] : []),
        ]
      : []),
    // A number with a closed set of values is drawn as a select too, and its choices are labelled
    // the same way — otherwise a heading level would read "3" and mean nothing.
    ...(field.kind === 'number' && field.choices !== null
      ? field.choices.map((choice) => `blocks.${type}.options.${field.path}.${choice}`)
      : []),
    ...(field.kind === 'object' || field.kind === 'list' ? keysOf(type, field.children) : []),
  ]);
}

test('no two blocks answer to the same type', () => {
  const types = coreBlocks.map((block) => block.type);
  expect(new Set(types).size).toBe(types.length);
});

test('a data block says what the gallery should show instead of calling the server', () => {
  for (const block of coreBlocks.filter((candidate) => candidate.kind === 'Data')) {
    expect(block.exampleData, `${block.type} has no example data`).toBeDefined();
  }
});

describe.each(coreBlocks.map((block) => [block.type, block] as const))('%s', (type, block) => {
  test('its schema is one the form generator can draw', () => {
    // `readFields` throws on a kind it does not know rather than skipping the field, so this is
    // the whole check: a block whose property form would come up short does not ship.
    expect(() => readFields(block.schema)).not.toThrow();
  });

  test('its example properties satisfy its own schema', () => {
    expect(block.schema.safeParse(block.example).success).toBe(true);
  });

  test('its name and every one of its fields are translated in every language', () => {
    const keys = [block.editorLabelKey, ...keysOf(type, readFields(block.schema))];

    for (const [locale, catalogue] of Object.entries(CATALOGUES)) {
      for (const key of keys) {
        expect(has(catalogue, key), `${key} is missing in ${locale}`).toBe(true);
      }
    }
  });
});
