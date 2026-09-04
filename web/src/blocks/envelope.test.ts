import { expect, test } from 'vitest';

import { allBlocks, allSections, columnsOf, emptyBody, readBody, LAYOUTS } from './envelope';

/**
 * The envelope as the browser reads it. The server checks the same shape with
 * `BlockDocumentWalker`, so what matters here is that the client is not stricter than it: a page
 * the API accepted has to be a page the editor can open.
 */

const BODY = {
  schemaVersion: 1,
  sections: [
    {
      id: 's_hero',
      key: 'hero',
      layout: '1/2+1/2',
      blocks: [
        { id: 'b_1', type: 'heading', props: { level: 1 }, column: 0 },
        { id: 'b_2', type: 'text', props: {}, column: 1 },
      ],
      sections: [{ id: 's_nested', blocks: [{ id: 'b_3', type: 'cta', props: {} }] }],
    },
  ],
};

test('reads a body written the way the server stores it', () => {
  const body = readBody(BODY);

  expect(allSections(body).map((section) => section.id)).toEqual(['s_hero', 's_nested']);
  expect(allBlocks(body).map((block) => block.id)).toEqual(['b_1', 'b_2', 'b_3']);
});

test('fills in what an older page left out rather than refusing it', () => {
  // A body written before a section carried a layout is still a body, and the editor has to be
  // able to open it: the defaults are what makes that true.
  const body = readBody({ schemaVersion: 1, sections: [{ id: 's', blocks: [] }] });
  const section = body.sections[0]!;

  expect(section.layout).toBe('stacked');
  expect(section.background).toBe('none');
  expect(section.padding).toBe('md');
  expect(section.width).toBe('default');
  expect(section.sections).toEqual([]);
});

test('a body that makes no sense is drawn as an empty page, never as a crash', () => {
  // A visitor is never shown a stack trace; an editor is told by the server, which refused the
  // save in the first place.
  expect(readBody(null)).toEqual(emptyBody());
  expect(readBody({ schemaVersion: 99, sections: [] })).toEqual(emptyBody());
  expect(readBody('a page')).toEqual(emptyBody());
});

test('every layout says how many columns it has', () => {
  expect(LAYOUTS.map(columnsOf)).toEqual([1, 2, 2, 2, 3]);

  // A section that never chose one is a stacked section.
  expect(columnsOf(null)).toBe(1);
  expect(columnsOf(undefined)).toBe(1);
});
