import { expect, test, type Locator } from '@playwright/test';

import { stubTheApi, stubTheBlockData } from './fixtures';

/**
 * `/news` and `/documents` once a browser has laid them out.
 *
 * What these measure is the half a unit test cannot reach. That a news card carries a title, a date
 * and a shelf is already checked in `src/blocks/blocks.test.tsx`; what nobody there can see is
 * whether the three cards are three columns or a column of three, and whether the filters sit above
 * the list or on top of it. jsdom does no layout, so "a grid of three" over there is an assertion
 * about a class name (implementation plan M1 §A.9, HANDOFF §13).
 *
 * ⚠️ The list on these pages is the `newsList` data block itself, so the fixture stubs the block's
 * answer and not a list endpoint: there is no second reader of the rows to stub (design M1 §1.2).
 */

const en = (value: string) => ({ en: value, it: value });

/** Three published news items and the two shelves they are filed under. */
const news = {
  items: [
    {
      id: 1,
      title: en('Winter tour opens'),
      summary: en('Six legs across the region.'),
      url: '/news/winter-tour',
      category: 'events',
      publishedAt: '2026-09-01T10:00:00Z',
      coverMediaId: null,
      pinned: true,
    },
    {
      id: 2,
      title: en('New sector files'),
      summary: en('Updated for the September cycle.'),
      url: '/news/sector-files',
      category: 'operations',
      publishedAt: '2026-09-03T10:00:00Z',
      coverMediaId: null,
      pinned: false,
    },
    {
      id: 3,
      title: en('Training slots'),
      summary: en('Bookings are open again.'),
      url: '/news/training-slots',
      category: 'operations',
      publishedAt: '2026-09-05T10:00:00Z',
      coverMediaId: null,
      pinned: false,
    },
  ],
  categories: [
    { key: 'events', label: en('Events') },
    { key: 'operations', label: en('Operations') },
  ],
};

/** Two documents on one shelf, one of them a file. */
const documents = {
  items: [
    {
      id: 21,
      title: en('Joining procedure'),
      summary: en('How a member joins the division.'),
      url: '/documents/joining-procedure',
      category: 'guides',
      publishedAt: '2026-09-04T12:00:00Z',
      fileMediaId: 9,
      sort: 0,
    },
    {
      id: 22,
      title: en('Read in the browser'),
      summary: null,
      url: '/documents/read-in-the-browser',
      category: 'guides',
      publishedAt: '2026-09-04T12:00:00Z',
      fileMediaId: null,
      sort: 1,
    },
  ],
  categories: [{ key: 'guides', label: en('Guides') }],
};

test.beforeEach(async ({ page }) => {
  await stubTheApi(page);
  await stubTheBlockData(page, 'newsList', news);
  await stubTheBlockData(page, 'documentList', documents);

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

test('the news are three cards across on a desktop and a column on a phone', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/news');

  await expect(page.getByRole('heading', { name: 'News', level: 1 })).toBeVisible();

  const cards = ['Winter tour opens', 'New sector files', 'Training slots'].map((name) =>
    page.getByRole('heading', { name }),
  );
  const boxes = await Promise.all(cards.map((card) => boxOf(card)));

  // Three columns means three boxes on the same line at three different places along it. Either
  // half alone passes on a single column: same line is true of one card, different x of nothing.
  expect(boxes[1]!.y).toBeCloseTo(boxes[0]!.y, 0);
  expect(boxes[2]!.y).toBeCloseTo(boxes[0]!.y, 0);
  expect(boxes[1]!.x).toBeGreaterThan(boxes[0]!.x);
  expect(boxes[2]!.x).toBeGreaterThan(boxes[1]!.x);

  // And the page does not run edge to edge on a wide screen: a list the width of 1280 pixels is a
  // list nobody scans (docs/UI-GUIDELINES.md).
  expect(boxes[2]!.x + boxes[2]!.width).toBeLessThan(1180);

  await page.setViewportSize({ width: 375, height: 900 });
  const narrow = await Promise.all(cards.map((card) => boxOf(card)));

  expect(narrow[1]!.y).toBeGreaterThan(narrow[0]!.y);
  expect(narrow[0]!.x).toBeCloseTo(narrow[1]!.x, 0);
});

test('the filters sit above the list rather than on top of it', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/news');

  // Found by its label, not by being the first combobox on the page: the header carries the
  // language switcher, so "the first one" is not this one. ⚠️ `exact` matters — without it the
  // option "Every category" matches too, because `getByLabel` is a substring match by default.
  const filter = page.getByLabel('Category', { exact: true });
  const filterBox = await boxOf(filter);
  const firstCard = await boxOf(page.getByRole('heading', { name: 'Winter tour opens' }));

  // Above, and clear of it. A control overlapping the first row is a control that eats the click
  // meant for the row — which reads as a list that does not open (HANDOFF §12).
  expect(filterBox.y + filterBox.height).toBeLessThanOrEqual(firstCard.y);

  await filter.click();
  await expect(page.getByRole('option', { name: 'Operations' })).toBeVisible();
});

test('the documents of one department are a filter and not an address of their own', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/documents');

  // ⚠️ Both public lists filter the same way, in the search parameters. `/documents/{dept}` as a
  // path was tried and taken out: one segment cannot be both a department and a slug, and deciding
  // by peeking at a closed set reserved nine slugs and shadowed any document called `ed`
  // (design changelog 1.6).
  const filter = page.getByLabel('Department', { exact: true });

  await filter.click();
  await page.getByRole('option', { name: 'Events' }).click();

  await expect(page).toHaveURL(/department=ED/);

  // And the shelf a document sits on is drawn by its name, not by the key stored on the row.
  await expect(page.getByRole('heading', { name: 'Guides' })).toBeVisible();
});

test('a document whose slug is a department code is still reachable', async ({ page }) => {
  // The corner the first version of this route lost: `ed` is a department code *and* a perfectly
  // ordinary slug, and the address bar cannot tell. Now it is only ever a slug.
  await page.route('**/api/content/public/Document/ed', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        kind: 'Document',
        slug: 'ed',
        ownerDepartment: 'ED',
        title: en('A document called ed'),
        summary: null,
        seo: null,
        body: { schemaVersion: 1, sections: [] },
        schemaVersion: 1,
        category: null,
        coverMediaId: null,
        fileMediaId: null,
        version: 1,
        publishedAt: '2026-09-04T12:00:00Z',
        // A guide with none of the dates of a document's life (G14), and no footer asked for.
        effectiveOn: null,
        reviewOn: null,
        retiredAt: null,
        supersededBySlug: null,
        supersededByTitle: null,
        showFooter: false,
        publishedByName: null,
      }),
    }),
  );

  await page.goto('/documents/ed');

  await expect(page.getByRole('heading', { name: 'A document called ed', level: 1 })).toBeVisible();
});

test('choosing a shelf puts it in the address, and clearing it takes it out', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/news');

  const filter = page.getByLabel('Category', { exact: true });

  await filter.click();
  await page.getByRole('option', { name: 'Operations' }).click();

  // A filter is a view of the same list, so it lives in the address: what a visitor is looking at
  // is a thing they can send to somebody else.
  await expect(page).toHaveURL(/category=operations/);

  await filter.click();
  await page.getByRole('option', { name: 'Every category' }).click();

  await expect(page).not.toHaveURL(/category=/);
});
