import { expect, test, type Locator } from '@playwright/test';

import { stubThePublishedPage } from './fixtures';

/**
 * What the blocks look like once a browser has laid them out.
 *
 * Everything a block *says* is already checked by `src/blocks/blocks.test.tsx`, and none of it
 * needed a browser. What needs one is the half those tests cannot reach: a grid of three that turns
 * out to be a column of three, a table that pushes the page sideways, a picture with no box. jsdom
 * does no layout, so "three columns" there is an assertion about a class name — which is a spelling
 * test, not a layout one (implementation plan M1 §A.9, HANDOFF §13).
 *
 * So the assertions here are all measurements.
 */

const SLUG = 'blocks-demo';

const en = (value: string) => ({ en: value, it: value });

/** Three cards, a picture and a table: one of each thing that can go wrong geometrically. */
const body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's_1',
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [
        {
          id: 'b_cards',
          type: 'cardGrid',
          version: 1,
          props: {
            columns: 3,
            cards: [{ title: en('Fly') }, { title: en('Control') }, { title: en('Learn') }],
          },
        },
        {
          id: 'b_image',
          type: 'image',
          version: 1,
          props: { mediaId: 9, alt: en('A runway at dawn'), width: 'full', rounded: true },
        },
        {
          id: 'b_table',
          type: 'table',
          version: 1,
          props: {
            columns: Array.from({ length: 8 }, (_, index) => ({
              label: en(`A rather long column heading ${index + 1}`),
              align: 'left',
            })),
            rows: [
              {
                cells: Array.from({ length: 8 }, (_, index) => ({
                  text: en(`Cell number ${index + 1}`),
                })),
              },
            ],
          },
        },
      ],
      sections: [],
    },
  ],
};

test.beforeEach(async ({ page }) => {
  await stubThePublishedPage(page, SLUG, body);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

/** Where a thing actually is, in pixels, once the browser has finished with it. */
async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  expect(box, 'the element is not laid out at all').not.toBeNull();
  return box!;
}

test('a grid of three cards is three columns wide on a desktop, and one on a phone', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);

  const cards = ['Fly', 'Control', 'Learn'].map((name) => page.getByRole('heading', { name }));
  const boxes = await Promise.all(cards.map((card) => boxOf(card)));

  // Three columns means three boxes on the same line at three different places along it. Either
  // half alone passes on a single column: same line is true of one card, and different x is true
  // of nothing at all.
  expect(boxes[1]!.y).toBeCloseTo(boxes[0]!.y, 0);
  expect(boxes[2]!.y).toBeCloseTo(boxes[0]!.y, 0);
  expect(boxes[1]!.x).toBeGreaterThan(boxes[0]!.x);
  expect(boxes[2]!.x).toBeGreaterThan(boxes[1]!.x);

  // And on a phone they are underneath one another, which is the other half of "responsive": a
  // grid pinned to three columns would keep them side by side and unreadable.
  await page.setViewportSize({ width: 375, height: 900 });
  const narrow = await Promise.all(cards.map((card) => boxOf(card)));

  expect(narrow[1]!.y).toBeGreaterThan(narrow[0]!.y);
  expect(narrow[0]!.x).toBeCloseTo(narrow[1]!.x, 0);
});

test('a wide table scrolls inside itself rather than pushing the page sideways', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );

  // A table of eight columns is wider than the reading column. What must not happen is the *page*
  // getting a horizontal scrollbar, because then every other block on it goes narrow too — a defect
  // nobody ever reports as "the table is too wide".
  //
  // ⚠️ This measures the page, not our code: Atmosphere's table already scrolls inside itself, which
  // is how a wrapper of our own doing the same thing was found and removed — the assertion passed
  // just as happily without it. It stays because it is the property the page has to keep, whichever
  // block is added next.
  expect(overflow).toBeLessThanOrEqual(1);
});

test('a picture is given a box, and the reading column is not the whole screen', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  const picture = await boxOf(page.getByAltText('A runway at dawn'));

  // The fixture is 8 by 8: what is measured here is the box the layout gives it, not the file.
  expect(picture.width).toBeGreaterThan(200);

  // And a section of the default width does not run edge to edge on a wide screen: a line of text
  // the width of 1280 pixels is a line nobody finishes (docs/UI-GUIDELINES.md).
  expect(picture.width).toBeLessThan(1100);
});
