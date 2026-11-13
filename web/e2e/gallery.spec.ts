import { expect, test, type Locator, type Page } from '@playwright/test';

import { stubThePublishedPage } from './fixtures';
import { englishCommon } from './locales';

/**
 * The gallery of PR 3.11 (design of the editor's phase 3, §5.2), in a browser: the grid with its columns, the carousel
 * one picture at a time with its arrows inside the block, the strip that scrolls sideways and stops on a picture, the
 * full screen that goes through the pictures, a phone, and paper.
 *
 * In a browser because all of it is layout and scrolling — columns, a slide in view, a snap, a window the size of the
 * screen — and jsdom has none.
 */

const words = englishCommon.blocks.gallery;
const image = englishCommon.blocks.image;
const SLUG = 'pictures';

const en = (value: string) => ({ en: value, it: value });
const enlarge = (name: string) => image.enlargeNamed.replace('{{name}}', name);
const position = (index: number, count: number) =>
  words.position.replace('{{index}}', String(index)).replace('{{count}}', String(count));

const pictures = (prefix: string, count: number) =>
  Array.from({ length: count }, (_, index) => ({
    mediaId: index + 1,
    alt: en(`${prefix} ${index + 1}`),
    ...(index === 0 ? { caption: en(`The caption of ${prefix} 1`) } : {}),
  }));

const gallery = (id: string, props: Record<string, unknown>) => ({ id, type: 'gallery', version: 1, props });

const section = (blocks: unknown[]) => ({
  id: 's_pictures',
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks,
  sections: [],
});

const body = {
  schemaVersion: 1,
  sections: [
    section([
      gallery('b_grid', { images: pictures('Grid', 4), layout: 'grid', columns: 3 }),
      gallery('b_carousel', { images: pictures('Slide', 3), layout: 'carousel' }),
      gallery('b_strip', { images: pictures('Strip', 6), layout: 'strip', lightbox: false }),
    ]),
  ],
};

/** Where a thing is, once the browser has laid it out. */
async function boxOf(locator: Locator) {
  const box = await locator.boundingBox();
  expect(box, 'the element is not laid out at all').not.toBeNull();
  return box!;
}

/** Photographs as an upload is: larger than the window, the second one standing up. */
async function servePhotographs(page: Page) {
  await page.route('**/media/*/**', (route) => {
    const standing = /\/media\/2\//.test(route.request().url());
    const [width, height] = standing ? [1200, 1800] : [2400, 1600];
    return route.fulfill({
      status: 200,
      contentType: 'image/svg+xml',
      body: `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="${width}" height="${height}" fill="#0d2c99"/><circle cx="${width / 2}" cy="${height / 2}" r="${height / 4}" fill="#f9cc2c"/></svg>`,
    });
  });
}

const carouselOf = (page: Page) => page.locator('[aria-roledescription="carousel"]');
const stripOf = (page: Page) => page.locator('div[role="region"][tabindex="0"]');

/** Whether a slide stands inside the carousel's window, where it can be seen. */
async function inView(carousel: Locator, name: string): Promise<boolean> {
  const frame = await boxOf(carousel);
  const slide = await boxOf(carousel.getByRole('img', { name }));
  return slide.x >= frame.x - 1 && slide.x + slide.width <= frame.x + frame.width + 1;
}

/** Nothing on the page is wider than the window: no sideways scroll for the page itself. */
async function expectNoSidewaysScroll(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
}

test.beforeEach(async ({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await stubThePublishedPage(page, SLUG, body);
  await servePhotographs(page);
});

test('the grid has its columns, the carousel one picture at a time, the strip as many as fit', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  // The grid: three in a row, the fourth under them, every picture the same square.
  const tiles = await Promise.all(
    [1, 2, 3, 4].map((index) => boxOf(page.getByRole('img', { name: `Grid ${index}` }))),
  );
  expect(tiles[1]!.y).toBeCloseTo(tiles[0]!.y, 0);
  expect(tiles[2]!.y).toBeCloseTo(tiles[0]!.y, 0);
  expect(tiles[3]!.y).toBeGreaterThan(tiles[0]!.y + tiles[0]!.height);
  expect(tiles[1]!.width).toBeCloseTo(tiles[1]!.height, 0);
  await expect(page.getByText('The caption of Grid 1')).toBeVisible();

  // The carousel: the first picture in view, the next waiting outside; the arrows inside the block.
  const carousel = carouselOf(page);
  await expect(carousel).toHaveAccessibleName(words.label);
  expect(await inView(carousel, 'Slide 1')).toBe(true);
  expect(await inView(carousel, 'Slide 2')).toBe(false);
  const previous = carousel.getByRole('button', { name: words.previous });
  const next = carousel.getByRole('button', { name: words.next });
  await expect(previous).toBeDisabled();
  const frame = await boxOf(carousel);
  for (const arrow of [previous, next]) {
    const box = await boxOf(arrow);
    expect(box.x).toBeGreaterThanOrEqual(frame.x);
    expect(box.x + box.width).toBeLessThanOrEqual(frame.x + frame.width);
  }

  await next.click();
  await expect.poll(() => inView(carousel, 'Slide 2')).toBe(true);
  expect(await inView(carousel, 'Slide 1')).toBe(false);
  await expect(previous).toBeEnabled();

  // The strip: wider than its block, so it scrolls; the next picture shows its edge, and a scroll stops on a picture.
  const strip = stripOf(page);
  await expect(strip).toHaveAccessibleName(words.label);
  const sizes = await strip.evaluate((element) => ({
    scroll: element.scrollWidth,
    client: element.clientWidth,
    snap: getComputedStyle(element).scrollSnapType,
  }));
  expect(sizes.scroll).toBeGreaterThan(sizes.client);
  expect(sizes.snap).toBe('x mandatory');
  // Its pictures do not open (`lightbox: false`), and the strip still takes the focus, so the keyboard scrolls it.
  await expect(strip.getByRole('button')).toHaveCount(0);
  await strip.focus();
  await page.keyboard.press('ArrowRight');
  await expect.poll(() => strip.evaluate((element) => element.scrollLeft)).toBeGreaterThan(0);
  // Where it stopped is the start of a picture.
  await expect
    .poll(() =>
      strip.evaluate((element) => {
        const items = [...element.querySelectorAll('li')];
        const left = element.getBoundingClientRect().left;
        return items.some((item) => Math.abs(item.getBoundingClientRect().left - left) < 2);
      }),
    )
    .toBe(true);

  await expectNoSidewaysScroll(page);
});

test('a click opens the picture at full screen, the arrows go through the others, and Esc gives the focus back', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  const opener = page.getByRole('button', { name: enlarge('Grid 1') });
  await opener.click();

  let dialog = page.getByRole('dialog', { name: 'Grid 1' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText(position(1, 4))).toBeVisible();
  await expect(dialog.getByText('The caption of Grid 1')).toBeVisible();

  // Measured once it has arrived: it opens sliding in (Atmosphere's animation).
  await expect.poll(async () => (await boxOf(dialog)).x).toBeGreaterThanOrEqual(0);
  await expect.poll(async () => (await boxOf(dialog)).width, { timeout: 2_000 }).toBeGreaterThan(1000);
  const large = await boxOf(dialog.getByRole('img', { name: 'Grid 1' }));
  expect(large.width / large.height).toBeCloseTo(1.5, 1);
  // The bar of the arrows stands above the picture, never on it.
  const bar = await boxOf(dialog.getByRole('button', { name: words.next }));
  expect(bar.y + bar.height).toBeLessThanOrEqual(large.y);

  // Forward to the picture that stands up: the window keeps it whole, inside the screen.
  await dialog.getByRole('button', { name: words.next }).click();
  dialog = page.getByRole('dialog', { name: 'Grid 2' });
  await expect(dialog.getByText(position(2, 4))).toBeVisible();
  const standing = await boxOf(dialog.getByRole('img', { name: 'Grid 2' }));
  expect(standing.height).toBeGreaterThan(standing.width);
  const window = await boxOf(dialog);
  expect(window.y).toBeGreaterThanOrEqual(0);
  expect(window.y + window.height).toBeLessThanOrEqual(900);

  // The keys, both ways and round.
  await page.keyboard.press('ArrowLeft');
  await page.keyboard.press('ArrowLeft');
  await expect(page.getByRole('dialog', { name: 'Grid 4' })).toBeVisible();
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('dialog', { name: 'Grid 1' })).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(opener).toBeFocused();
});

test('the window opened from a slide turns alone: the carousel behind stays where it was', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  const carousel = carouselOf(page);
  await carousel.getByRole('button', { name: enlarge('Slide 1') }).click();
  await expect(page.getByRole('dialog', { name: 'Slide 1' })).toBeVisible();

  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByRole('dialog', { name: 'Slide 3' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);

  // Atmosphere's own keys, which the gallery replaces, would have turned it twice under the window.
  expect(await inView(carousel, 'Slide 1')).toBe(true);
  await expect(carousel.getByRole('button', { name: words.previous })).toBeDisabled();

  // Its own keys still turn it, from inside it.
  await carousel.getByRole('button', { name: words.next }).focus();
  await page.keyboard.press('ArrowRight');
  await expect.poll(() => inView(carousel, 'Slide 2')).toBe(true);
});

test('on a phone: two columns, the carousel and the strip inside the screen, the full screen too', async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto(`/${SLUG}`);

  const first = await boxOf(page.getByRole('img', { name: 'Grid 1' }));
  const second = await boxOf(page.getByRole('img', { name: 'Grid 2' }));
  const third = await boxOf(page.getByRole('img', { name: 'Grid 3' }));
  expect(second.y).toBeCloseTo(first.y, 0);
  expect(third.y).toBeGreaterThan(first.y + first.height);

  const carousel = carouselOf(page);
  const frame = await boxOf(carousel);
  expect(frame.x).toBeGreaterThanOrEqual(0);
  expect(frame.x + frame.width).toBeLessThanOrEqual(375);
  const next = await boxOf(carousel.getByRole('button', { name: words.next }));
  expect(next.x + next.width).toBeLessThanOrEqual(375);

  // The strip shows most of one picture and the edge of the next.
  const strip = stripOf(page);
  const room = await boxOf(strip);
  const item = await boxOf(strip.locator('li').first());
  expect(item.width).toBeLessThan(room.width);
  expect(item.width).toBeGreaterThan(room.width * 0.6);

  // Nothing of the galleries is wider than the page's column: what scrolls, scrolls inside itself. The column and not the
  // page, because the stub's menu is wider than a phone on a page with no block at all.
  const overflow = await page
    .locator('main')
    .evaluate((element) => element.scrollWidth - element.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);

  await page.getByRole('button', { name: enlarge('Grid 1') }).click();
  const dialog = page.getByRole('dialog', { name: 'Grid 1' });
  await expect(dialog).toBeVisible();
  await expect.poll(async () => (await boxOf(dialog)).x).toBeGreaterThanOrEqual(0);
  const window = await boxOf(dialog);
  expect(window.x + window.width).toBeLessThanOrEqual(375);
  await expect(dialog.getByRole('button', { name: words.next })).toBeInViewport();
  await expect(dialog.getByRole('button', { name: englishCommon.common.close })).toBeInViewport();
});

test('on paper the carousel and the strip are a grid, and on the screen again what they were', async ({
  page,
}) => {
  // A document, because only a document and a news item print through the context.
  await page.route('**/api/content/public/Document/printed', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 22,
        kind: 'Document',
        slug: 'printed',
        path: 'printed',
        ownerDepartment: 'ED',
        collections: [],
        title: { en: 'A printed document', it: 'Un documento stampato' },
        summary: null,
        seo: null,
        coverMediaId: null,
        fileMediaId: null,
        media: {},
        showFooter: false,
        effectiveOn: null,
        retiredAt: null,
        supersededBySlug: null,
        supersededByTitle: null,
        body,
        schemaVersion: 1,
        version: 1,
        publishedAt: '2026-09-06T10:00:00Z',
      }),
    }),
  );
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/documents/printed');
  await expect(carouselOf(page)).toBeVisible();

  await page.emulateMedia({ media: 'print' });
  await page.evaluate(() => window.dispatchEvent(new Event('beforeprint')));

  await expect(carouselOf(page)).toHaveCount(0);
  await expect(stripOf(page)).toHaveCount(0);
  // Every picture of the three, each in a grid.
  for (const name of ['Slide 3', 'Strip 6']) {
    await expect(page.getByRole('img', { name })).toBeVisible();
  }
  await expect(page.locator('ul.grid')).toHaveCount(3);

  await page.evaluate(() => window.dispatchEvent(new Event('afterprint')));
  await page.emulateMedia({ media: 'screen' });
  await expect(carouselOf(page)).toBeVisible();
  await expect(stripOf(page)).toBeVisible();
});
