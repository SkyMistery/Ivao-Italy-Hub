import { expect, test } from 'vitest';

import { readBody, type Body } from '../../blocks';
import { calloutSchema, headingSchema } from '../../blocks/schemas';

import {
  addBlock,
  addSection,
  clampColumns,
  defaultProps,
  duplicateBlock,
  findBlock,
  moveBlock,
  moveSection,
  removeSection,
  updateBlock,
} from './body';

/**
 * What the editor does to a page, without the editor. These are the operations behind the arrows
 * and the buttons of the section tree, and they are pure rewrites of the tree: the one on screen is
 * never reached into, which is what makes the draft a single piece of state.
 */

const LOCALES = ['it', 'en'];

function body(): Body {
  return readBody({
    schemaVersion: 1,
    sections: [
      {
        id: 's_1',
        layout: '3x1/3',
        blocks: [
          { id: 'b_1', type: 'heading', props: { level: 1 }, column: 2 },
          { id: 'b_2', type: 'text', props: {}, column: 0 },
        ],
      },
      { id: 's_2', blocks: [] },
    ],
  });
}

test('moving a block swaps it with its neighbour, and stops at the ends', () => {
  const moved = moveBlock(body(), 'b_1', 1);
  expect(moved.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_2', 'b_1']);

  const stuck = moveBlock(body(), 'b_1', -1);
  expect(stuck.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', 'b_2']);
});

test('moving a section does the same, one level at a time', () => {
  const moved = moveSection(body(), 's_2', -1);
  expect(moved.sections.map((section) => section.id)).toEqual(['s_2', 's_1']);
});

test('a duplicate is a copy with its own identifier, its own properties and no capture', () => {
  const original = updateBlock(body(), 'b_1', { frozen: { items: [] } });
  const copied = duplicateBlock(original, 'b_1');

  const copy = findBlock(copied.body, copied.id)!.block;
  expect(copy.id).not.toBe('b_1');
  expect(copy.type).toBe('heading');
  expect(copy.frozen).toBeNull();
  expect(copied.body.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', copied.id, 'b_2']);

  // The properties are a copy and not the same object: editing one must not edit the other.
  const changed = updateBlock(copied.body, copied.id, { props: { level: 3 } });
  expect(findBlock(changed, 'b_1')!.block.props.level).toBe(1);
});

test('narrowing a layout pulls its blocks back into a column that still exists', () => {
  // Otherwise the server refuses the save with "column out of range" and the editor cannot say
  // which block it means.
  const narrowed = clampColumns(body(), 's_1', '1/2+1/2', 2);
  expect(narrowed.sections[0]!.blocks.map((block) => block.column)).toEqual([1, 0]);
});

test('removing a section takes its blocks with it', () => {
  const pruned = removeSection(body(), 's_1');
  expect(pruned.sections.map((section) => section.id)).toEqual(['s_2']);
});

test('a new section is stacked, named in every language, and empty', () => {
  const added = addSection(body(), LOCALES);
  const section = added.body.sections.at(-1)!;

  expect(section.id).toBe(added.id);
  expect(section.layout).toBe('stacked');
  expect(section.blocks).toEqual([]);
  expect(section.title).toEqual({ it: '', en: '' });
});

test('a new block starts with the properties its own schema describes', () => {
  const added = addBlock(body(), 's_2', 'callout', defaultProps(calloutSchema, LOCALES), null);
  const block = findBlock(added.body, added.id)!.block;

  // Never partly filled in: a field with no value is an input a coordinator cannot use.
  expect(block.props).toEqual({
    tone: 'info',
    title: { it: '', en: '' },
    text: { it: '', en: '' },
  });
});

test('defaults are read off the schema, not written next to the block', () => {
  expect(defaultProps(headingSchema, LOCALES)).toEqual({ level: 0, text: { it: '', en: '' } });
});
