import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, request, test } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  addBlock,
  benchUrl,
  deleteMedia,
  openOutline,
  properties,
  readInEnglish,
  selectBlock,
  signIn,
  uploadMedia,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The briefing of a tour (M2, T6b), through the real screens: a tour written, its briefing composed with the editor of
 * the content — a text in one language only and a picture of the library — saved with the tour, "ready" asking for the
 * text's second language and naming where it is, then marked ready and its briefing found in search by an anonymous
 * visitor. The tour and the picture are taken back at the end: the bench database is not thrown away between runs.
 */

const flightops = englishFlightOps();
const blocks = englishCommon.blocks;
const editor = englishCommon.content.editor;

/** A name of this run. */
const stamp = Date.now().toString(36);
const tourName = { en: `Bench briefing tour ${stamp}`, it: `Tour del briefing del banco ${stamp}` };
const summary = { en: `A briefing tour summary ${stamp}`, it: `Riassunto del tour del briefing ${stamp}` };
const slug = `bench-briefing-${stamp}`;
/** A word nobody else writes, so that search finds this briefing and nothing else. */
const briefing = { en: `Briefingword${stamp} over the Alps`, it: `Briefingword${stamp} sopra le Alpi` };

/** An eight by eight PNG, and a byte of this run after its end: the library answers the same bytes with the same file. */
const picture = Buffer.concat([
  Buffer.from(
    'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC',
    'base64',
  ),
  Buffer.from(stamp),
]);
const pictureName = `bench-briefing-${stamp}.png`;

/** `datetime-local` wants the UTC wall clock without seconds, which is what the form stores it as. */
function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

test('a tour gets a briefing with a picture, is asked for its second language, is marked ready and found', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the tour
  const now = Date.now();
  await page.goto('/staff/tours/new');
  await expect(page.getByRole('heading', { name: flightops.tours.create })).toBeVisible();

  await writeInBothLanguages(page.locator('form'), flightops.tours.fields.title, 'title', tourName);
  await page.locator('[id="slug"]').fill(slug);
  await writeInBothLanguages(page.locator('form'), flightops.tours.fields.summary, 'summary', summary);
  await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 24 * 3600 * 1000)));
  await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
  await page.locator('[id="dailyLegLimit"]').fill('5');

  await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);
  const tourId = Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]);

  // The picture goes in the library of the tour's department, where the picker of its briefing looks.
  const tour = (await (await context.request.get(`/api/flightops/tours/${tourId}`)).json()) as {
    ownerDepartment: string;
  };
  const mediaId = await uploadMedia(context, tour.ownerDepartment, pictureName, picture);

  // Nothing stands in the way yet: the briefing is empty, and an empty briefing is not a briefing half translated.
  await expect(page.getByText(flightops.tours.readyProblems)).toBeHidden();

  // ---------------------------------------------------------------- the briefing
  await page.getByRole('tab', { name: flightops.tours.tabs.briefing }).click();
  await expect(page).toHaveURL(/tab=briefing/);
  const onThePage = page.getByRole('region', { name: editor.preview });

  await page.getByRole('button', { name: editor.addSection }).click();

  // A text in English only: the editor lets it be, "ready" will not.
  await addBlock(page, blocks.subgroups.text, blocks.text.label);
  const text = properties(page).locator('fieldset').filter({ hasText: blocks.text.fields.markdown });
  await text.getByRole('tab', { name: 'English' }).click();
  await properties(page).locator('[id="markdown.en"]').fill(briefing.en);
  await expect(onThePage.getByText(briefing.en)).toBeVisible();

  // A picture of the library, chosen in the picker of the block.
  await addBlock(page, blocks.subgroups.media, blocks.image.label);
  await properties(page).getByRole('button', { name: pictureName }).click();
  await expect(onThePage.locator('img')).toHaveCount(1);

  const save = page.getByRole('button', { name: flightops.tours.briefing.save });
  await whileWaitingFor(page, 'PUT', `/api/flightops/tours/${tourId}`, async () => {
    await save.click();
  });
  await expect(page.getByText(flightops.tours.briefing.saved)).toBeVisible();

  // "Ready" asks for the text's Italian, and says where it is: the briefing, then the block — not a JSON pointer.
  const problems = page.getByRole('listitem').filter({ hasText: `${flightops.tours.fields.briefing} ›` });
  await expect(problems).toHaveCount(1);
  await expect(problems).toContainText(blocks.text.label);
  await expect(problems).toContainText('Italian');

  // ---------------------------------------------------------------- the second language
  await openOutline(page, editor.outline);
  await selectBlock(page, blocks.text.label);
  await page.getByRole('button', { name: editor.onThePage, exact: true }).click();
  await writeInBothLanguages(properties(page), blocks.text.fields.markdown, 'markdown', briefing);

  // Applied at the pause in typing: the button to save is how the page says there is something to save.
  await expect(save).toBeEnabled();
  await whileWaitingFor(page, 'PUT', `/api/flightops/tours/${tourId}`, async () => {
    await save.click();
  });
  await expect(page.getByText(flightops.tours.readyProblems)).toBeHidden();

  // The picture travelled with the tour: the briefing the server holds names it.
  const saved = (await (await context.request.get(`/api/flightops/tours/${tourId}`)).json()) as {
    briefing: unknown;
  };
  expect(JSON.stringify(saved.briefing)).toContain(`"mediaId":${mediaId}`);

  // ---------------------------------------------------------------- ready, and found
  await whileWaitingFor(page, 'POST', '/status', async () => {
    await page.getByRole('button', { name: flightops.tours.actions.ready }).click();
  });
  await expect(page.getByRole('button', { name: flightops.tours.actions.hide })).toBeVisible();

  const anonymous = await request.newContext({ baseURL: benchUrl });
  const search = await anonymous.get(`/api/search?q=${encodeURIComponent(`Briefingword${stamp}`)}&locale=en`);
  expect(search.status()).toBe(200);
  const hits = ((await search.json()) as { results: { items: { url: string }[] } }).results.items;
  expect(hits.map((hit) => hit.url)).toContain(`/tours/${slug}`);
  await anonymous.dispose();

  // ---------------------------------------------------------------- taken back
  // Deleting the tour takes its uses of the picture with it, and a file nobody uses is deleted.
  await page.getByRole('button', { name: englishCommon.common.delete }).first().click();
  await whileWaitingFor(page, 'DELETE', '/api/flightops/tours/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
  });
  await deleteMedia(context, mediaId);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      create: string;
      readyProblems: string;
      actions: { ready: string; hide: string };
      fields: { title: string; summary: string; briefing: string };
      tabs: { briefing: string };
      briefing: { save: string; saved: string };
    };
  };
}
