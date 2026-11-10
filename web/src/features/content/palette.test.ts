import { expect, test } from 'vitest';

import { registry } from '../../app/registry';

import { addableBlocks, tooFewReaders } from './palette';

/**
 * One answer to "which blocks may this person add here", for the palette and for the instructions for Claude
 * (review of #449, finding 6): the two used to be written twice.
 */

test('a retired block and one behind a permission not held are not addable', () => {
  const addable = addableBlocks(registry.blocks, () => false).map((block) => block.type);

  expect(addable).toContain('text');
  expect(addable).not.toContain('interactive');
  expect(addable).not.toContain('cta');
  expect(addable).not.toContain('coordination');
});

test('without a way to ask, a permission is not what keeps a block out', () => {
  expect(addableBlocks(registry.blocks, undefined).map((block) => block.type)).toContain('interactive');
});

test('a block for fewer readers than the page is said so, and nothing is too narrow without readers of its own', () => {
  const audiences = [{ type: 'myWork', audience: 'Staff' as const }];

  expect(tooFewReaders(audiences, 'Public')('myWork')).toBe(true);
  expect(tooFewReaders(audiences, 'Staff')('myWork')).toBe(false);
  expect(tooFewReaders(audiences, undefined)('myWork')).toBe(false);
  expect(tooFewReaders(audiences, 'Public')('text')).toBe(false);
});
