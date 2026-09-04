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

function has(catalogue: unknown, key: string): boolean {
  let current: unknown = catalogue;

  for (const segment of key.split('.')) {
    if (current === null || typeof current !== 'object') {
      return false;
    }
    current = (current as Record<string, unknown>)[segment];
  }

  return typeof current === 'string';
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
