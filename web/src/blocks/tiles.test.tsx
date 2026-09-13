import { expect, test } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import type { Body } from './envelope';

/**
 * A dashboard is a grid of tiles and not a page of sections (note
 * 2026-09-13-le-dashboard-a-tutto-schermo). jsdom does no layout, so what is read here is what the
 * grid is told: each block is a tile with the width it names, or the share of the row its column had,
 * and the section takes the whole width whatever it says.
 */

const text = (id: string, extra: Record<string, unknown> = {}) => ({
  id,
  type: 'text',
  version: 1,
  props: { markdown: { en: id, it: id } },
  renderMode: null,
  frozen: null,
  column: null,
  source: null,
  span: null,
  ...extra,
});

const body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's_tiles',
      layout: '1/3+2/3',
      width: 'default',
      background: 'none',
      padding: 'md',
      blocks: [
        text('b_wide', { column: 1 }),
        text('b_narrow', { column: 0 }),
        text('b_quarter', { span: 3 }),
      ],
      sections: [],
    },
  ],
} as unknown as Body;

test('on a dashboard every block is a tile of the width it names, or of its column', () => {
  const { container } = renderWithProviders(<ContentRenderer body={body} dashboard />);

  const tiles = [...container.querySelectorAll('[data-tile]')];
  expect(tiles.map((tile) => tile.getAttribute('data-tile'))).toEqual(['b_narrow', 'b_quarter', 'b_wide']);

  expect(tiles[0]?.className).toContain('@view-md:col-span-4');
  expect(tiles[1]?.className).toContain('@view-md:col-span-3');
  expect(tiles[2]?.className).toContain('@view-md:col-span-8');

  // The content of a tile scrolls rather than stretching its row.
  expect(tiles[0]?.firstElementChild?.className).toContain('overflow-auto');
  expect(container.querySelector('.max-w-5xl')).toBeNull();
});

test('a page is not a dashboard', () => {
  const { container } = renderWithProviders(<ContentRenderer body={body} />);

  expect(container.querySelector('[data-tile]')).toBeNull();
  expect(container.querySelector('.max-w-5xl')).not.toBeNull();
});
