import { expect, test } from 'vitest';

import { readBody, type Body } from '../../blocks';
import { calloutSchema, headingSchema, linkListSchema } from '../../blocks/schemas';

import {
  addBlock,
  addSection,
  clampColumns,
  defaultProps,
  duplicateBlock,
  duplicateSection,
  findBlock,
  moveBlock,
  moveBlockTo,
  moveSection,
  removeSection,
  reorderBlocks,
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

test('moving a block swaps it with its neighbour in the same column, and stops at the ends', () => {
  // `b_1` stands in the third column and `b_2` in the first: neither has a neighbour, so neither
  // moves. Before 11 September 2026 they swapped places in the list — which changed nothing on
  // the page, since each column draws its own — and the arrows in the outline seemed broken.
  const alone = moveBlock(body(), 'b_1', 1);
  expect(alone.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', 'b_2']);

  // Two in the first column, with one of another column between them in the list: they swap, and
  // the one in between keeps its place.
  const shared = updateBlock(body(), 'b_1', { column: 0 });
  const third = addBlock(shared, 's_1', 'text', {}, null, 2).body;
  const withTwo = addBlock(third, 's_1', 'text', {}, null, 0).body;
  const ids = (page: Body) => page.sections[0]!.blocks.map((block) => block.id);
  const [, , other, last] = ids(withTwo);

  const moved = moveBlock(withTwo, last!, -1);
  expect(ids(moved)).toEqual(['b_1', last, other, 'b_2']);

  const stuck = moveBlock(body(), 'b_1', -1);
  expect(ids(stuck)).toEqual(['b_1', 'b_2']);
});

test('a block dragged to a slot lands there: its own column, another, another section', () => {
  const ids = (page: Body, section = 0) => page.sections[section]!.blocks.map((block) => block.id);

  // Into the second section, which is empty: the only slot is its end.
  const across = moveBlockTo(body(), 'b_1', 's_2', 0, 0);
  expect(ids(across)).toEqual(['b_2']);
  expect(ids(across, 1)).toEqual(['b_1']);
  expect(findBlock(across, 'b_1')!.block.column).toBe(0);

  // Into another column of its own section, before the block that stands there.
  const sideways = moveBlockTo(body(), 'b_1', 's_1', 0, 0);
  expect(ids(sideways)).toEqual(['b_1', 'b_2']);
  expect(findBlock(sideways, 'b_1')!.block.column).toBe(0);

  // Within its column: two blocks in the first column, the second dropped on the slot before the
  // first — and dropped on the slot just before itself, it stays where it is.
  const two = addBlock(updateBlock(body(), 'b_1', { column: 0 }), 's_1', 'text', {}, null, 0).body;
  const [, , third] = ids(two);
  expect(ids(moveBlockTo(two, third!, 's_1', 0, 0))).toEqual([third, 'b_1', 'b_2']);
  expect(ids(moveBlockTo(two, third!, 's_1', 0, 2))).toEqual(['b_1', 'b_2', third]);
  expect(ids(moveBlockTo(two, 'b_1', 's_1', 0, 3))).toEqual(['b_2', third, 'b_1']);
});

test('a duplicated section is a copy right after it, with its own identifiers and no key', () => {
  const withRow = addSection(body(), LOCALES, 's_1').body;
  const original = withRow.sections[0]!;
  const copied = duplicateSection(withRow, 's_1');
  const copy = copied.body.sections[1]!;

  expect(copied.body.sections.map((section) => section.id)).toEqual(['s_1', copied.id, 's_2']);
  expect(copy.layout).toBe(original.layout);
  expect(copy.blocks.map((block) => block.type)).toEqual(original.blocks.map((block) => block.type));
  expect(copy.blocks.map((block) => block.id)).not.toEqual(original.blocks.map((block) => block.id));
  expect(copy.sections).toHaveLength(1);
  expect(copy.sections[0]!.id).not.toBe(original.sections[0]!.id);
  // A key names what a template imposes; a copy is the page's own, and nothing is imposed on it.
  expect(copy.key).toBeNull();
});

test('a block dropped onto one of another column in the outline moves nothing', () => {
  const refused = reorderBlocks(body(), 'b_1', 'b_2');
  expect(refused.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', 'b_2']);

  const sameColumn = updateBlock(body(), 'b_1', { column: 0 });
  const moved = reorderBlocks(sameColumn, 'b_1', 'b_2');
  expect(moved.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_2', 'b_1']);
});

test('moving a section does the same, among its siblings at whichever level', () => {
  const moved = moveSection(body(), 's_2', -1);
  expect(moved.sections.map((section) => section.id)).toEqual(['s_2', 's_1']);

  // A row moves among the rows of its section, and the page's sections stay where they are.
  const withRows = addSection(addSection(body(), LOCALES, 's_1').body, LOCALES, 's_1');
  const [first, second] = withRows.body.sections[0]!.sections.map((row) => row.id);
  const rowMoved = moveSection(withRows.body, second!, -1);

  expect(rowMoved.sections.map((section) => section.id)).toEqual(['s_1', 's_2']);
  expect(rowMoved.sections[0]!.sections.map((row) => row.id)).toEqual([second, first]);
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

test('a row is a section inside a section, and it is born without a frame of its own', () => {
  // ⚠️ Nesting was in the model, in the validator (`MaxDepth` is 3) and in the renderer since M1,
  // and there was no way to make one: none of the ten seeded pages nests. This is that way
  // (nota `2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`).
  const added = addSection(body(), LOCALES, 's_1');

  // Inside the section it was asked for, and nowhere else.
  expect(added.body.sections.map((section) => section.id)).toEqual(['s_1', 's_2']);
  expect(added.body.sections[0]!.sections.map((section) => section.id)).toEqual([added.id]);

  const row = added.body.sections[0]!.sections[0]!;

  // No background and no air of its own: the section around it already draws the frame, and a
  // second one inside the first is what makes a page look assembled rather than composed.
  expect(row.background).toBe('none');
  expect(row.padding).toBe('none');

  // But its own layout, which is the whole point: one band of colour, several column arrangements.
  expect(row.layout).toBe('stacked');
  expect(row.blocks).toEqual([]);
});

test('a section with no parent named still goes to the top, as it always did', () => {
  const added = addSection(body(), LOCALES);

  expect(added.body.sections.map((section) => section.id)).toEqual(['s_1', 's_2', added.id]);
  expect(added.body.sections.at(-1)!.padding).toBe('md');
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

test('a block dropped at a place in a column goes before the block that stood there', () => {
  const props = defaultProps(calloutSchema, LOCALES);

  // The blocks of a section are one list whatever column they stand in: `b_1` is in the third
  // column and `b_2` in the first. Dropped at the top of the first column, the new block goes
  // before `b_2` in that list — where `b_1` stands is another column's business.
  const first = addBlock(body(), 's_1', 'callout', props, null, 0, 0);
  expect(first.body.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', first.id, 'b_2']);
  expect(findBlock(first.body, first.id)!.block.column).toBe(0);

  // Past the last block of the column — one block, position one — is the end of the list.
  const last = addBlock(body(), 's_1', 'callout', props, null, 0, 1);
  expect(last.body.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', 'b_2', last.id]);

  // An empty column has one place, and it is the end.
  const middle = addBlock(body(), 's_1', 'callout', props, null, 1, 0);
  expect(middle.body.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', 'b_2', middle.id]);
  expect(findBlock(middle.body, middle.id)!.block.column).toBe(1);

  // And with no place named, the end, as the palette's click has always done.
  const clicked = addBlock(body(), 's_1', 'callout', props, null, 2);
  expect(clicked.body.sections[0]!.blocks.map((block) => block.id)).toEqual(['b_1', 'b_2', clicked.id]);
});

test('defaults are read off the schema, not written next to the block', () => {
  // ⚠️ A heading starts at **2**, because the schema says so. Before G13 it had no default, so it
  // took the first of its choices and every heading anybody added was an `h1` — `/start` had four
  // by the time somebody measured the page (`decisions/2026-09-07-giro-visivo-m1.md`). The page's
  // own title is the `h1`; what a writer adds under it is a level below.
  expect(defaultProps(headingSchema, LOCALES)).toEqual({ level: 2, text: { it: '', en: '' } });
});

test('a choice that is optional starts at nothing chosen, and leaves the payload', () => {
  const props = defaultProps(linkListSchema, LOCALES);

  expect(props.department).toBeUndefined();
  expect(JSON.parse(JSON.stringify(props))).toEqual({ category: '', limit: 10 });

  // And what starts there is valid: a block added to a page must not be born refused.
  expect(linkListSchema.safeParse(props).success).toBe(true);
});
