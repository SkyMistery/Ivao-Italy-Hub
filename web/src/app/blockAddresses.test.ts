import { expect, test } from 'vitest';

import { readFields, type FieldNode } from '../shared/forms';

import { registry } from './registry';

/**
 * An address inside a block is called `href`, as a file is called `mediaId` (design of the editor, phase 3,
 * §5.9): it is the name the import of a file checks, and that publication checks, because neither reads a
 * schema. And `.meta({ address: true })` is what the properties panel offers the pages of the site on (§4.5).
 * So the name and the annotation go together, on every block the build registers: a block that had only one of
 * the two would either offer nothing, or offer addresses nobody ever checks.
 */

function leaves(fields: readonly FieldNode[]): FieldNode[] {
  return fields.flatMap((field) =>
    field.kind === 'list' || field.kind === 'object' ? leaves(field.children) : [field],
  );
}

test.each(registry.blocks.map((block) => [block.type, block] as const))(
  '%s: a field is called href if and only if it is an address',
  (_type, block) => {
    for (const field of leaves(readFields(block.schema))) {
      const called = field.path.split('.').at(-1) === 'href';
      expect([field.path, field.meta.address === true]).toEqual([field.path, called]);
    }
  },
);

test('the blocks that lead somewhere are the ones the design names', () => {
  const leading = registry.blocks
    .filter((block) => leaves(readFields(block.schema)).some((field) => field.meta.address === true))
    .map((block) => block.type)
    .sort();

  expect(leading).toEqual(['buttonGroup', 'cardGrid', 'cta', 'hero', 'logoGrid']);
});
