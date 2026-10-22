import { expect } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  benchUrl,
  createContent,
  deleteContent,
  metadata,
  readContent,
  readInEnglish,
  saveDraft,
  signIn,
  test,
  whileWaitingFor,
} from './bench';

/**
 * Two editors on one page, and the second save refused (issue #330, rule 5 of
 * `decisions/2026-09-11-l-editor-che-risponde.md`: "a 409 stops the autosave").
 *
 * ⚠️ The draft is sent with the version of the row it **started from**. It used to be sent with the
 * version of the latest fetch, and the detail is fetched again whenever the tab comes back into
 * focus: A's draft went out with B's version, the server had no reason to refuse it, and B's save was
 * gone without a word. Two browser contexts, because two tabs of one browser share nothing the bug
 * needs and a unit test cannot hold the server's half of it.
 */

const words = englishCommon.content.editor;
const stamp = Date.now().toString(36);

test('a draft refetched after somebody else saved is refused, not saved over them', async ({
  browser,
  context,
  page,
  afterwards,
}) => {
  test.setTimeout(90_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const row = await createContent(context, {
    slug: `bench-two-editors-${stamp}`,
    title: { en: 'Two editors', it: 'Due editor' },
    body: { schemaVersion: 1, sections: [] },
  });
  afterwards(() => deleteContent(context, row.id));

  // A opens the page first, at the version it was born with.
  await page.goto(`/staff/content/${row.id}`);
  await expect(saveDraft(page, words.saveDraft)).toBeEnabled();

  // B, in a browser of their own, opens the same page and saves it.
  const other = await browser.newContext({ baseURL: benchUrl });
  afterwards(async () => {
    await other.close();
  });
  await readInEnglish(other);
  await signIn(other);
  const b = await other.newPage();
  await b.goto(`/staff/content/${row.id}`);

  // The slug: one plain input, and a change the server has to store.
  await metadata(b).locator('#slug').fill(`${row.slug}-b`);
  await whileWaitingFor(b, 'PUT', `/api/content/${row.id}`, async () => {
    await saveDraft(b, words.saveDraft).click();
  });

  // A writes something of their own, and the line under the toolbar says it is not stored yet.
  await metadata(page).locator('#slug').fill(`${row.slug}-a`);
  const status = page.getByRole('status', { name: words.autosave.title });
  await expect(status).toHaveText(words.autosave.unsaved);

  // A comes back to the tab: the detail is asked again, and now carries B's version. What the
  // query client does on a real focus, done by hand — two headless pages are both always "visible".
  const refetched = page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' && response.url().endsWith(`/api/content/${row.id}`),
  );
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
  expect((await refetched).status()).toBe(200);

  // A's draft saves itself after the pause, and is refused: it was written over the version A
  // opened, and B has moved the row since.
  const autosaved = await page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && response.url().endsWith(`/api/content/${row.id}`),
    { timeout: 20_000 },
  );
  expect(autosaved.status()).toBe(409);
  await expect(status).toHaveText(words.autosave.stopped);

  // B's save is still the row.
  expect((await readContent(context, row.id)).slug).toBe(`${row.slug}-b`);
});

test("a draft saved twice by its own editor moves on with its own saves, and is never refused", async ({
  context,
  page,
  afterwards,
}) => {
  test.setTimeout(90_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const row = await createContent(context, {
    slug: `bench-own-saves-${stamp}`,
    title: { en: 'Own saves', it: 'I propri salvataggi' },
    body: { schemaVersion: 1, sections: [] },
  });
  afterwards(() => deleteContent(context, row.id));

  await page.goto(`/staff/content/${row.id}`);
  const status = page.getByRole('status', { name: words.autosave.title });

  // A press, then a pause, then a press again — the version each one carries is the one the save
  // before it answered, and coming back to the tab in between changes nothing.
  await metadata(page).locator('#slug').fill(`${row.slug}-1`);
  await whileWaitingFor(page, 'PUT', `/api/content/${row.id}`, async () => {
    await saveDraft(page, words.saveDraft).click();
  });

  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));

  await metadata(page).locator('#slug').fill(`${row.slug}-2`);
  await whileWaitingFor(page, 'PUT', `/api/content/${row.id}`, async () => {
    await expect(status).toHaveText(
      new RegExp(`^${words.autosave.saved.replace('{{time}}', '\\d\\d:\\d\\d')}$`, 'u'),
      { timeout: 15_000 },
    );
  });

  // And Enter pressed twice in a one-line field, which no disabled button holds back: two saves one
  // after the other, never two with the same version (issue #332, item 8).
  await metadata(page).locator('#slug').fill(`${row.slug}-3`);
  const puts: number[] = [];
  page.on('response', (response) => {
    if (response.request().method() === 'PUT' && response.url().endsWith(`/api/content/${row.id}`)) {
      puts.push(response.status());
    }
  });
  await metadata(page).locator('#slug').press('Enter');
  await metadata(page).locator('#slug').press('Enter');
  await expect.poll(() => puts.length, { timeout: 15_000 }).toBeGreaterThanOrEqual(1);
  // Long enough for a second one, when there is one, to have been answered.
  await page.waitForLoadState('networkidle');
  await expect(status).not.toHaveText(words.autosave.stopped);
  expect(puts.every((code) => code < 300), `PUT answers: ${puts.join(', ')}`).toBe(true);
  expect((await readContent(context, row.id)).slug).toBe(`${row.slug}-3`);
});
