import { screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { z } from 'zod';

import { registry } from '../app/registry';
import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import { ALIGNS, alignOf, carriesOldAlign, placedAt, readBody, type Body } from './envelope';

/**
 * Where a block stands in its column (PR 3.9, design of the editor's phase 3, §5.6): `align` on the envelope, drawn by
 * the renderer's wrapper for a block that declares `alignable`, and a group of buttons written before that still drawn
 * where its own props put it.
 */

const en = (value: string) => ({ en: value, it: value });

function page(...blocks: Record<string, unknown>[]): Body {
  return readBody({
    schemaVersion: 1,
    sections: [
      {
        id: 's_1',
        blocks: blocks.map((block, index) => ({ id: `b_${index + 1}`, version: 1, props: {}, ...block })),
      },
    ],
  });
}

/** The wrapper around a block: the nearest element that says how it is aligned. */
const placeOf = (element: HTMLElement) => element.closest('[data-align]');

describe('the envelope', () => {
  test('carries one of three alignments, and none is the left', () => {
    expect(ALIGNS).toEqual(['left', 'center', 'right']);

    const [heading] = page({ type: 'heading', props: { text: en('A') } }).sections[0]!.blocks;
    expect(heading!.align).toBeNull();
    expect(alignOf(heading!)).toBe('left');
  });

  test('an alignment the browser does not know makes the body unreadable, as the server refuses it', () => {
    // The two halves agree by hand (`BlockDocumentWalker.Aligns`): what the server would refuse to store, the browser
    // does not pretend to read.
    expect(
      readBody({
        schemaVersion: 1,
        sections: [{ id: 's', blocks: [{ id: 'b', type: 'text', align: 'justify' }] }],
      }).sections,
    ).toEqual([]);
  });

  test('a group of buttons written before PR 3.9 is read where its props put it, until the envelope says otherwise', () => {
    const old = { align: null, props: { align: 'center', buttons: [] } };

    expect(carriesOldAlign(old)).toBe(true);
    expect(alignOf(old)).toBe('center');
    expect(alignOf({ ...old, align: 'right' })).toBe('right');
    // A value that is not one of the three is no alignment at all.
    expect(alignOf({ align: null, props: { align: 'justify' } })).toBe('left');
  });

  test('placing a block writes the envelope and takes the old alignment out of its props', () => {
    const old = { props: { align: 'center', buttons: [] } };

    expect(placedAt(old, 'right')).toEqual({ align: 'right', props: { buttons: [] } });
    // The left is the absence, and the old value goes all the same: left alone, it would win again.
    expect(placedAt(old, 'left')).toEqual({ align: null, props: { buttons: [] } });
    // With the props being written instead of today's.
    expect(placedAt(old, 'center', { buttons: [{ href: '/a' }] })).toEqual({
      align: 'center',
      props: { buttons: [{ href: '/a' }] },
    });
  });
});

describe('the blocks that may be aligned', () => {
  test('are the seven of the design, and no other', () => {
    // §5.6: heading, text, callout, image, video, buttons, testimonial. A table or a grid of cards takes the whole
    // column already.
    expect(
      registry.blocks
        .filter((block) => block.alignable === true)
        .map((block) => block.type)
        .sort(),
    ).toEqual(['buttonGroup', 'callout', 'heading', 'image', 'testimonial', 'text', 'video']);
  });

  test.each(
    registry.blocks.filter((block) => block.alignable === true).map((block) => [block.type, block] as const),
  )('%s declares no alignment of its own among its properties', (_type, block) => {
    // One would be a second alignment of the same thing, and `alignOf` would read it as the old one of a group of
    // buttons. `hero` keeps its own because it places what is inside its ground, and it is not alignable.
    const schema = z.toJSONSchema(block.schema, { io: 'input', unrepresentable: 'any' }) as {
      properties?: Record<string, unknown>;
    };

    expect(Object.keys(schema.properties ?? {})).not.toContain('align');
  });
});

describe('the renderer', () => {
  test('draws an alignable block in the middle or on the right of its column', () => {
    renderWithProviders(
      <ContentRenderer
        body={page(
          { type: 'heading', align: 'center', props: { level: 2, text: en('In the middle') } },
          { type: 'text', align: 'right', props: { markdown: en('On the right') } },
        )}
      />,
    );

    const middle = placeOf(screen.getByRole('heading', { name: 'In the middle' }));
    expect(middle).toHaveAttribute('data-align', 'center');
    expect(middle).toHaveClass('text-center');

    const right = placeOf(screen.getByText('On the right'));
    expect(right).toHaveAttribute('data-align', 'right');
    expect(right).toHaveClass('text-right');
  });

  test('leaves the left as it always was: no wrapper at all', () => {
    renderWithProviders(
      <ContentRenderer
        body={page(
          { type: 'heading', props: { level: 2, text: en('Nothing chosen') } },
          { type: 'heading', align: 'left', props: { level: 2, text: en('The left, chosen') } },
        )}
      />,
    );

    expect(placeOf(screen.getByRole('heading', { name: 'Nothing chosen' }))).toBeNull();
    expect(placeOf(screen.getByRole('heading', { name: 'The left, chosen' }))).toBeNull();
  });

  test('ignores an alignment on a block that may not be aligned', () => {
    // The server keeps the value to one of three and does not know which blocks it means something for: a table takes
    // the whole column, and an `align` on it draws nothing.
    renderWithProviders(
      <ContentRenderer
        body={page({
          type: 'table',
          align: 'center',
          props: {
            columns: [{ label: en('Column'), align: 'left' }],
            rows: [{ cells: [{ text: en('A cell') }] }],
          },
        })}
      />,
    );

    expect(placeOf(screen.getByText('A cell'))).toBeNull();
  });

  test('draws a group of buttons written before PR 3.9 where its own props put it', () => {
    renderWithProviders(
      <ContentRenderer
        body={page({
          type: 'buttonGroup',
          props: { align: 'right', buttons: [{ label: en('Go'), href: '/go', variant: 'primary' }] },
        })}
      />,
    );

    const link = screen.getByRole('link', { name: 'Go' });
    expect(placeOf(link)).toHaveAttribute('data-align', 'right');
    // The row of buttons is laid out by flex, which the alignment of the text does not move: it says it is a row, and
    // the wrapper justifies it.
    expect(link.closest('[data-align-row]')).not.toBeNull();
    expect(placeOf(link)).toHaveClass('[&_[data-align-row]]:justify-end');
  });
});
