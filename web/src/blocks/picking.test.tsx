import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import type { Body } from './envelope';
import { PickingContext } from './picking';

/**
 * Composing on the page, and the promise that keeps it safe.
 *
 * There is **one** renderer for the public site and for the editor, so the interactivity that lets
 * somebody compose by clicking has exactly one way of being acceptable: it must not exist at all
 * where a visitor reads. That is what the first test is for, and it is the one that must never be
 * relaxed — the second is only the feature.
 */

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
    <PickingContext.Provider value={{ selected: null, onPick: picked }}>
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
    <PickingContext.Provider value={{ selected: null, onPick: picked }}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  const section = document.querySelector('[data-pickable="section"]');
  expect(section).not.toBeNull();

  await user.click(section!);

  expect(picked).toHaveBeenCalledWith('section', 's1');
});
