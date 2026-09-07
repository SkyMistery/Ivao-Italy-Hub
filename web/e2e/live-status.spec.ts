import { expect, test } from '@playwright/test';

import { stubTheApi, stubTheBlockData } from './fixtures';
import { englishCommon } from './locales';

/**
 * The strip across the top of the public site (design M1 §6.2).
 *
 * What a unit test cannot see is the only reason this file exists: **where** the strip is. A band
 * that reports the network while sitting on top of the first paragraph of the page is worse than no
 * band at all, and "the text is right" would say nothing about it — which is exactly how the back
 * office came to be drawn in a 255 pixel column for a week (handoff §13).
 */

const answered = {
  updatedAt: '2026-09-07T09:00:00Z',
  figures: [
    { figure: 'divisionAtc', value: 4 },
    { figure: 'divisionPilots', value: 37 },
  ],
};

test.beforeEach(async ({ page }) => {
  await stubTheApi(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the strip is a band above the page, and it does not sit on top of it', async ({ page }) => {
  await stubTheBlockData(page, 'networkStats', answered);
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/');

  const strip = page.getByText(englishCommon.liveStatus.title).locator('..').locator('..');
  await expect(strip).toBeVisible();

  const band = await strip.boundingBox();
  const article = await page.getByRole('article').boundingBox();

  expect(band, 'the strip has no box, so nothing was laid out').not.toBeNull();
  expect(article, 'the page has no box, so the strip is being measured against nothing').not.toBeNull();

  // A band: it spans the window rather than sitting in a corner of it.
  expect(band!.width).toBeGreaterThan(1200);

  // Above the page and not over it. The bottom of the strip is above the top of the article, which
  // is the assertion that fails the day somebody makes it float.
  expect(band!.y + band!.height).toBeLessThanOrEqual(article!.y);

  // And it is one line, not a panel: a strip that grows to four rows is not a strip.
  expect(band!.height).toBeLessThan(80);
});

test('the strip says the numbers it was given, and when they were counted', async ({ page }) => {
  await stubTheBlockData(page, 'networkStats', answered);
  await page.goto('/');

  await expect(page.getByText('4', { exact: true })).toBeVisible();
  await expect(page.getByText(englishCommon.blocks.networkStats.captions.divisionAtc)).toBeVisible();
  await expect(page.getByText('37', { exact: true })).toBeVisible();
});

test('a network that could not be asked leaves no band at all', async ({ page }) => {
  // The server says so with `updatedAt: null`, and the honest answer is nothing: a band reading
  // "0 controllers here" would be the site saying nobody is flying when it means it could not ask.
  await stubTheBlockData(page, 'networkStats', { updatedAt: null, figures: [] });
  await page.goto('/');

  // The page itself is there, so this is not passing because nothing rendered.
  await expect(page.getByRole('heading', { name: 'Welcome to the division', level: 1 })).toBeVisible();
  await expect(page.getByText(englishCommon.liveStatus.title)).toHaveCount(0);
});
