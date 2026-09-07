import { expect, test } from '@playwright/test';

import { englishCommon, englishSeed } from '../locales';

import { department, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The question M1 exists to answer, asked of the real thing: **is the site drawn by the code, or by
 * rows somebody edits?** (design M1 §0.1, §8.1.)
 *
 * A member of staff adds an entry to the menu in the back office, a visitor who is nobody sees it
 * appear in the navigation of the public site, the entry is deleted, and it is gone — with nothing
 * recompiled, redeployed or restarted in between. If any of that needed a build, the menu is still
 * in the code and the phase has not done what it says.
 *
 * The bench signs in as the coordinator of the web department, which is the department the site
 * belongs to: the same identity a real web team has, and the reason this screen is reachable at all.
 */

const menu = englishCommon.menu;

/** A label of this run: the bench database is not thrown away between runs. */
const stamp = Date.now().toString(36);
const label = { en: `Bench entry ${stamp}`, it: `Voce del banco ${stamp}` };

test('an entry added to the menu appears on the site, and taking it away removes it', async ({
  page,
  context,
  browser,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- add an entry
  await page.goto(`/staff/${department}/menu`);
  await expect(page.getByRole('heading', { name: menu.title })).toBeVisible();

  await page.getByRole('link', { name: menu.create }).first().click();
  await expect(page).toHaveURL(new RegExp(`/staff/${department}/menu/new$`));

  await writeInBothLanguages(page.locator('form'), menu.fields.label, 'label', label);
  await page.locator('[id="path"]').fill('/start');
  await page.locator('[id="sort"]').fill('900');

  await whileWaitingFor(page, 'POST', '/api/menu', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });

  await expect(page).toHaveURL(new RegExp(`/staff/${department}/menu`));

  // ---------------------------------------------------------------- a visitor reads it
  const visitor = await browser.newContext();
  await readInEnglish(visitor);
  const publicPage = await visitor.newPage();

  await publicPage.goto('/');

  // Named, because the header holds two navigations: the bar with the division's own name, and the
  // menu itself.
  const navigation = publicPage.getByRole('navigation', { name: 'Main' });
  await expect(navigation.getByText(label.en, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- and taking it away
  await page.getByRole('link', { name: englishCommon.common.edit }).last().click();
  await expect(page.locator('[id="path"]')).toHaveValue('/start');

  await page.getByRole('button', { name: englishCommon.common.delete }).click();

  await whileWaitingFor(page, 'DELETE', '/api/menu/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
  });

  // The assertion the whole phase is for: nothing was built, nothing was deployed, and the site has
  // changed. A reload is a fresh `/api/me`, which is where the menu comes from.
  await publicPage.reload();
  await expect(navigation.getByText(label.en, { exact: true })).toHaveCount(0);

  // And the entries that were there before are still there: deleting one entry is deleting one
  // entry, which is worth asserting because a menu is a tree and a tree is where a cascade hides.
  await expect(navigation.getByRole('link').first()).toBeVisible();

  await visitor.close();
});

test('the menu of the site is not a screen of every department', async ({ page, context }) => {
  // The whole authorisation of the resource, seen from a browser: the entry exists under the
  // department that owns the site, and the address refuses anybody else. The bench signs in as the
  // web team, so the way to exercise the guard is to ask for another department's copy of it.
  await readInEnglish(context);
  await signIn(context);

  await page.goto('/staff/ed/menu');

  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole('heading', { name: englishCommon.forbidden.title })).toBeVisible();
});

test('a department opens on its own dashboard', async ({ page, context }) => {
  // `/staff` is a door, and since G8 it opens on the department's own page rather than on the first
  // list in its sidebar. What is drawn there is a published row: the words below come from the
  // system template, seeded once, and nothing in the client wrote them.
  await readInEnglish(context);
  await signIn(context);

  // ⚠️ Not `/staff`: the bench signs in as the web team, which reaches every department, so the
  // door opens on the first of the nine rather than on this one. Which department a dashboard is
  // being asked for is the point here, so it is asked for by name.
  await page.goto(`/staff/${department}`);

  const heading = englishSeed.seed.templates.dashboard?.welcome?.heading ?? '';
  expect(heading, 'the dashboard template has no welcome heading to look for').not.toBe('');
  await expect(page.getByRole('heading', { name: heading })).toBeVisible();

  // And the department may edit it, which is the second half of "the base is given, the arrangement
  // is theirs".
  await expect(page.getByRole('link', { name: englishCommon.dashboard.edit })).toBeVisible();
});

test('sitemap.xml and robots.txt are the server talking, not the application', async ({ request }) => {
  // Both are excluded from the fallback of the single page application. Without that they would be
  // answered with index.html and a crawler would read a page of JavaScript where it asked for XML —
  // which is exactly the kind of failure that never shows up in a browser.
  const sitemap = await request.get('/sitemap.xml');

  expect(sitemap.status()).toBe(200);
  expect(sitemap.headers()['content-type']).toContain('xml');

  const xml = await sitemap.text();
  expect(xml).toContain('<urlset');
  expect(xml).toContain('/start</loc>');

  const robots = await request.get('/robots.txt');

  expect(robots.status()).toBe(200);
  expect(robots.headers()['content-type']).toContain('text/plain');
  expect(await robots.text()).toContain('Disallow: /staff');
});
