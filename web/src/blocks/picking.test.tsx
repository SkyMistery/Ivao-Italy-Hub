import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import type { Body } from './envelope';
import { PickingContext, type Picking } from './picking';

/**
 * Composing on the page, and the promise that keeps it safe.
 *
 * There is **one** renderer for the public site and for the editor, so the interactivity that lets
 * somebody compose by clicking has exactly one way of being acceptable: it must not exist at all
 * where a visitor reads. That is what the first test is for, and it is the one that must never be
 * relaxed — the second is only the feature.
 */

/** An editing context, with only what the test is about filled in. */
const editing = (overrides: Partial<Picking> = {}): Picking => ({
  selected: null,
  onPick: vi.fn(),
  target: null,
  onPickColumn: vi.fn(),
  accepts: () => true,
  ...overrides,
});

const body: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's1',
      key: 'hero',
      title: { en: 'Hero', it: 'Hero' },
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      mediaId: null,
      required: null,
      locked: null,
      allowedBlocks: null,
      blocks: [
        {
          id: 'b1',
          type: 'cta',
          version: 1,
          renderMode: null,
          frozen: null,
          column: 0,
          props: {
            title: { en: 'Join us', it: 'Unisciti' },
            text: { en: 'Come and fly', it: 'Vieni a volare' },
            href: 'https://example.org/join',
            label: { en: 'Join', it: 'Iscriviti' },
          },
        },
      ],
      sections: [],
    },
  ],
};

test('the page a visitor reads has nothing to click and nothing to strip', () => {
  renderWithProviders(<ContentRenderer body={body} />);

  // No provider, so no wrapper, no handler, no attribute. Asserted on the attribute because it is
  // the only trace the editing shell leaves in the document: if it is absent, so is the rest.
  expect(document.querySelectorAll('[data-pickable]')).toHaveLength(0);

  // And the link inside the block is still a link that goes somewhere, which is the half a broken
  // capture handler would take away without anybody noticing until a visitor clicked it.
  const link = screen.getByRole('link', { name: 'Join' });
  expect(link).toHaveAttribute('href', 'https://example.org/join');

  const clicked = new MouseEvent('click', { bubbles: true, cancelable: true });
  link.dispatchEvent(clicked);
  expect(clicked.defaultPrevented).toBe(false);
});

test('with the editor behind it, a click picks the block instead of following it', async () => {
  const user = userEvent.setup();
  const picked = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPick: picked })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  await user.click(screen.getByRole('link', { name: 'Join' }));

  // ⚠️ The block, not the section: the click was captured at the block and stopped there. And the
  // link did not fire — a call to action that carried whoever is composing out of the editor, with
  // unsaved changes, would be worse than no picking at all.
  expect(picked).toHaveBeenCalledTimes(1);
  expect(picked).toHaveBeenCalledWith('block', 'b1');
});

test('the space around the blocks picks the section', async () => {
  const user = userEvent.setup();
  const picked = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPick: picked })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  const section = document.querySelector('[data-pickable="section"]');
  expect(section).not.toBeNull();

  await user.click(section!);

  expect(picked).toHaveBeenCalledWith('section', 's1');
});

/**
 * The structure of a section, drawn while composing (Carmine, 11 September 2026, with the page
 * builder of va.ivao.aero in front of him: "you see clearly how it is divided — the drop here in the
 * empty areas").
 */
const halves: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's2',
      key: 'split',
      title: { en: 'Split', it: 'Diviso' },
      layout: '1/2+1/2',
      background: 'none',
      padding: 'md',
      width: 'default',
      mediaId: null,
      required: null,
      locked: null,
      allowedBlocks: null,
      blocks: [],
      sections: [],
    },
  ],
};

test('while composing, every empty column says where a component would go, and choosing one says which', async () => {
  const user = userEvent.setup();
  const chosen = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPickColumn: chosen })}>
      <ContentRenderer body={halves} />
    </PickingContext.Provider>,
  );

  // Two columns, two invitations: a section in halves reads as halves before anything is in it.
  const invitations = screen.getAllByRole('button', { name: '+ Add here' });
  expect(invitations).toHaveLength(2);

  await user.click(invitations[1]!);

  // The second column — which is the whole point: before this, a component always landed in the
  // first one and had to be moved.
  expect(chosen).toHaveBeenCalledWith('s2', 1);
});

test('a section a template locks does not invite a component it would refuse', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing({ accepts: () => false })}>
      <ContentRenderer body={halves} />
    </PickingContext.Provider>,
  );

  expect(screen.queryByRole('button', { name: '+ Add here' })).not.toBeInTheDocument();
});

test('a visitor sees none of it: an empty column is simply empty', () => {
  renderWithProviders(<ContentRenderer body={halves} />);

  expect(screen.queryByRole('button', { name: '+ Add here' })).not.toBeInTheDocument();
  expect(document.querySelectorAll('[data-pickable]')).toHaveLength(0);
});

/** What the editor hands over as a place to drop onto: here, a marker that says where it stands. */
function Slot({ section, column, index }: { section: string; column: number; index: number }) {
  return <div data-slot={`${section}:${column}:${index}`} />;
}

test('a place to drop onto stands before every block and after the last, only while composing', () => {
  // One block in `body`: a place before it and one after it. The renderer draws the component the
  // editor hands over and knows nothing else about dropping — dnd-kit is never imported here.
  renderWithProviders(
    <PickingContext.Provider value={editing({ DropZone: Slot })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect([...document.querySelectorAll('[data-slot]')].map((slot) => slot.getAttribute('data-slot'))).toEqual(
    ['s1:0:0', 's1:0:1'],
  );
});

test('a section a template locks offers no place to drop onto either', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing({ DropZone: Slot, accepts: () => false })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect(document.querySelectorAll('[data-slot]')).toHaveLength(0);
});

test('a visitor gets no place to drop onto, because there is nothing to drop', () => {
  renderWithProviders(<ContentRenderer body={body} />);

  expect(document.querySelectorAll('[data-slot]')).toHaveLength(0);
});
