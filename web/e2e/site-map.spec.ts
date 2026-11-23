import { expect, test } from '@playwright/test';

import { stubTheApiAsStaff, stubTheBlockData, stubThePublishedPage } from './fixtures';
import { englishCommon } from './locales';

/**
 * The map of the site (design `13-design-struttura-del-sito.md` §5.1) in a browser: the answer of the server drawn as a
 * nested list, the front page one line at the top, a page under its mother, the screens named by their keys; and the
 * block in the gallery, which mounts it from its example.
 */

const SLUG = 'site-map';
const words = englishCommon.blocks.siteMap;

// The name of a screen, read from its key as the block reads it. A record answers `string | undefined`, so it is checked
// once here: a spec asserting on `undefined` asserts on nothing.
const calendar = englishCommon.menu.screens['/calendar'] ?? '';

test('the name of the calendar screen is in the English words', () => {
  expect(calendar).not.toBe('');
});

const body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's_map',
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [
        {
          id: 'b_map',
          type: 'siteMap',
          version: 1,
          props: { title: { en: 'Everything on this site', it: 'Tutto il sito' } },
        },
      ],
      sections: [],
    },
  ],
};

test('the map nests each page under its mother, with the front page first', async ({ page }) => {
  await stubThePublishedPage(page, SLUG, body);
  await stubTheBlockData(page, 'siteMap', {
    items: [
      { path: '/', title: null, titleKey: 'menu.screens./' },
      { path: '/home', title: { en: 'Welcome', it: 'Benvenuti' }, titleKey: null },
      { path: '/about', title: { en: 'About', it: 'Chi siamo' }, titleKey: null },
      { path: '/about/team', title: { en: 'The staff', it: 'Lo staff' }, titleKey: null },
      { path: '/about/history', title: { en: 'History', it: 'Storia' }, titleKey: null },
      { path: '/calendar', title: null, titleKey: 'menu.screens./calendar' },
    ],
  });

  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto(`/${SLUG}`);

  const map = page.getByRole('navigation', { name: 'Everything on this site' });
  await expect(map).toBeVisible();
  await expect(map.getByRole('heading', { name: 'Everything on this site' })).toBeVisible();

  // The front page is one line, under its row's title, at `/`; the screen of the same page is not drawn twice.
  const top = map.locator(':scope > ul > li > a');
  await expect(top).toHaveText(['Welcome', 'About', calendar]);
  await expect(top.first()).toHaveAttribute('href', '/');

  // Sisters in the order of their titles, inside the line of their mother.
  const about = map.locator(':scope > ul > li').nth(1);
  await expect(about.locator(':scope > ul > li > a')).toHaveText(['History', 'The staff']);
  await expect(about.getByRole('link', { name: 'The staff' })).toHaveAttribute('href', '/about/team');

  await page.screenshot({ path: test.info().outputPath('site-map.png'), fullPage: true });
});

test('a map with nothing to show says so', async ({ page }) => {
  await stubThePublishedPage(page, SLUG, body);
  await stubTheBlockData(page, 'siteMap', { items: [] });

  await page.goto(`/${SLUG}`);

  await expect(page.getByText(words.empty)).toBeVisible();
});

test('the gallery mounts the block from its example', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/admin/ui-kit');

  const section = page.locator('[data-ui-kit="siteMap"]');
  await section.scrollIntoViewIfNeeded();
  await expect(section.getByRole('link', { name: 'The staff' })).toHaveAttribute('href', '/about/team');
  await expect(section.getByRole('link', { name: calendar })).toBeVisible();

  await section.screenshot({ path: test.info().outputPath('site-map-gallery.png') });
});
