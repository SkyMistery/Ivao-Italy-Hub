import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, request, test } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  addLeg,
  benchUrl,
  choose,
  readInEnglish,
  signIn,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The tours in the back office (M2, T6), through the real screens: a template written, a tour made from it, given its
 * dates and summary, marked ready with a release already behind — and then found by an anonymous visitor in search
 * and by the staff in the calendar, twice (release and close). Both rows are deleted at the end: a tour without
 * reports is deleted, and the bench database is not thrown away between runs.
 *
 * The bench signs in as the web team, which reaches every department and so holds the permissions of the tours on
 * the base department too, templates and deletion included.
 */

const flightops = englishFlightOps();

/** A name of this run: the bench database is not thrown away between runs. */
const stamp = Date.now().toString(36);
const templateName = { en: `Bench tour template ${stamp}`, it: `Template di tour del banco ${stamp}` };
const tourName = { en: `Bench tour ${stamp}`, it: `Tour del banco ${stamp}` };
const summary = { en: `A bench tour summary ${stamp}`, it: `Riassunto del tour del banco ${stamp}` };
const slug = `bench-tour-${stamp}`;

/** `datetime-local` wants the UTC wall clock without seconds, which is what the form stores it as. */
function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

test('a tour is made from a template, marked ready, found in search and calendar, and deleted', async ({
  page,
  context,
}) => {
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the template
  await page.goto('/staff/tours/new?template=true');
  await expect(page.getByRole('heading', { name: flightops.tourTemplates.create })).toBeVisible();

  await writeInBothLanguages(page.locator('form'), flightops.tours.fields.title, 'title', templateName);
  await page.locator('[id="dailyLegLimit"]').fill('5');

  await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);
  const templateUrl = page.url();

  // ---------------------------------------------------------------- a tour out of it
  await page.goto('/staff/tours/from-template');
  await expect(page.getByRole('heading', { name: flightops.tours.fromTemplate })).toBeVisible();

  await choose(page, flightops.tourFromTemplate.fields.templateId, templateName.en);
  await writeInBothLanguages(
    page.locator('form'),
    flightops.tourFromTemplate.fields.title,
    'title',
    tourName,
  );
  await page.locator('[id="slug"]').fill(slug);

  await whileWaitingFor(page, 'POST', '/api/flightops/tours/from-template/', async () => {
    await page.getByRole('button', { name: flightops.tourFromTemplate.submit }).click();
  });
  await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);
  await expect(page.getByRole('heading', { name: tourName.en })).toBeVisible();

  // A sequential tour is ready only with a leg (T7a); the legs have a spec of their own.
  await addLeg(context, Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]));

  // What stands in the way of "ready" is said before anybody presses it: the dates are not copied.
  await expect(page.getByText(flightops.tours.readyProblems)).toBeVisible();
  await expect(page.locator('[id="dailyLegLimit"]')).toHaveValue('5');

  // ---------------------------------------------------------------- dates, summary, ready
  const now = Date.now();
  await writeInBothLanguages(page.locator('form'), flightops.tours.fields.summary, 'summary', summary);
  await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 24 * 3600 * 1000)));
  await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));

  await whileWaitingFor(page, 'PUT', '/api/flightops/tours/', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page.getByText(flightops.tours.saved)).toBeVisible();
  await expect(page.getByText(flightops.tours.readyProblems)).toBeHidden();

  await whileWaitingFor(page, 'POST', '/status', async () => {
    await page.getByRole('button', { name: flightops.tours.actions.ready }).click();
  });
  await expect(page.getByRole('button', { name: flightops.tours.actions.hide })).toBeVisible();

  // ---------------------------------------------------------------- found, by anybody
  // Released yesterday, so public now: in search...
  const anonymous = await request.newContext({ baseURL: benchUrl });
  const search = await anonymous.get(`/api/search?q=${encodeURIComponent(tourName.en)}&locale=en`);
  expect(search.status()).toBe(200);
  const hits = ((await search.json()) as { results: { items: { url: string }[] } }).results.items;
  expect(hits.map((hit) => hit.url)).toContain(`/tours/${slug}`);

  // ...and in the calendar, as the public calendar block reads it: the release, and the close as a deadline (T20c). The
  // staff list of the calendar is by department, and the bench's is not the tours' one.
  const window = {
    kinds: [{ kind: 'tour' }, { kind: 'deadline' }],
    from: new Date(now - 2 * 24 * 3600 * 1000).toISOString(),
    to: new Date(now + 61 * 24 * 3600 * 1000).toISOString(),
    limit: 50,
  };
  const props = Buffer.from(JSON.stringify(window)).toString('base64url');
  const calendar = await anonymous.get(`/api/blocks/data/calendar?props=${props}`);
  expect(calendar.status()).toBe(200);
  const entries = ((await calendar.json()) as { items: { url: string | null; kind: string }[] }).items;
  expect(
    entries
      .filter((entry) => entry.url === `/tours/${slug}`)
      .map((entry) => entry.kind)
      .sort(),
  ).toEqual(['deadline', 'tour']);
  await anonymous.dispose();

  // ---------------------------------------------------------------- deleted, with the template
  for (const url of [page.url(), templateUrl]) {
    await page.goto(url);
    await page.getByRole('button', { name: englishCommon.common.delete }).first().click();
    await whileWaitingFor(page, 'DELETE', '/api/flightops/tours/', async () => {
      await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
    });
  }
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      fromTemplate: string;
      saved: string;
      readyProblems: string;
      actions: { ready: string; hide: string };
      fields: { title: string; summary: string };
    };
    tourTemplates: { create: string };
    tourFromTemplate: { submit: string; fields: { templateId: string; title: string } };
  };
}
