import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Locator, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchAirports, choose, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The legs of a tour (M2, T7a), through the real screens: a tour with a chosen start written from nothing, its legs
 * composed in the table — one added, one that follows it, one that closes the tour back to the first departure, one
 * duplicated and then removed, which the server deletes because no report points at it — "ready" refusing the tour
 * while it is not a ring, and then marking it ready. The tour is deleted at the end: the bench database is not thrown
 * away between runs.
 */

const flightops = englishFlightOps();
const legs = flightops.legs;

const stamp = Date.now().toString(36);
const tourName = { en: `Bench legs ${stamp}`, it: `Leg del banco ${stamp}` };
const summary = { en: `A bench tour with legs ${stamp}`, it: `Un tour del banco con leg ${stamp}` };
const slug = `bench-legs-${stamp}`;

/** `datetime-local` wants the UTC wall clock without seconds, which is what the form stores it as. */
function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/** The row of the table by its name: "Leg 2", or "Not saved" (`+`) for a row the server has not numbered yet. */
function row(page: Page, number: string): Locator {
  const name = number === '+' ? legs.state.new : legs.row.replace('{{number}}', number);
  return page.getByRole('row', { name, exact: true });
}

async function addAfter(page: Page, number: string, what: string): Promise<void> {
  await row(page, number).getByRole('button', { name: legs.actions.addAfter }).click();
  await page.getByRole('menuitem', { name: what }).click();
}

async function save(page: Page, target: Locator, method: 'POST' | 'PUT' = 'POST'): Promise<void> {
  await whileWaitingFor(page, method, '/legs', async () => {
    await target.getByRole('button', { name: englishCommon.common.save }).click();
  });
}

test('a tour with a chosen start is composed in the table of its legs and marked ready', async ({
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

  await choose(page, flightops.tours.fields.kind, flightops.tours.options.kind.SequentialChosenStart);
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
  const tourUrl = page.url();

  // No legs, no ready: said before anybody presses.
  await expect(page.getByText(flightops.errors.noLegs)).toBeVisible();

  // ---------------------------------------------------------------- the legs
  await page.getByRole('tab', { name: flightops.tours.tabs.legs }).click();
  await expect(page).toHaveURL(/tab=legs/);
  await expect(page.getByText(legs.empty)).toBeVisible();

  // One at the end.
  await page.getByRole('button', { name: legs.actions.add }).click();
  const first = row(page, '+');
  await first.getByRole('combobox', { name: legs.fields.departureIcao }).fill(benchAirports.rome);
  await first.getByRole('combobox', { name: legs.fields.arrivalIcao }).fill(benchAirports.milan);
  await save(page, first);
  await expect(row(page, '1').getByRole('combobox', { name: legs.fields.arrivalIcao })).toHaveValue(
    benchAirports.milan,
  );

  // One that follows it: it departs from Milan.
  await addAfter(page, '1', legs.actions.follows);
  const follows = row(page, '+');
  await expect(follows.getByRole('combobox', { name: legs.fields.departureIcao })).toHaveValue(
    benchAirports.milan,
  );
  await follows.getByRole('combobox', { name: legs.fields.arrivalIcao }).fill(benchAirports.bari);
  await save(page, follows);

  // Not a ring yet: the tour with a chosen start says so.
  await expect(page.getByText(flightops.errors.notARing)).toBeVisible();

  // One that closes the tour: from Bari back to Rome, both filled in.
  await addAfter(page, '2', legs.actions.closeTour);
  const closes = row(page, '+');
  await expect(closes.getByRole('combobox', { name: legs.fields.departureIcao })).toHaveValue(
    benchAirports.bari,
  );
  await expect(closes.getByRole('combobox', { name: legs.fields.arrivalIcao })).toHaveValue(
    benchAirports.rome,
  );
  await save(page, closes);
  await expect(page.getByText(flightops.errors.notARing)).toBeHidden();

  // A duplicate of the first, right after it: the server numbers it 2 and moves the others down.
  await addAfter(page, '1', legs.actions.duplicate);
  await save(page, row(page, '+'));
  await expect(row(page, '4').getByRole('combobox', { name: legs.fields.departureIcao })).toHaveValue(
    benchAirports.bari,
  );
  await expect(page.getByText(flightops.errors.notARing)).toBeVisible();

  // Removed: no report points at it, so the server deletes it and says so first.
  await row(page, '2').getByRole('button', { name: legs.actions.remove }).click();
  const dialog = page.getByRole('alertdialog');
  await expect(dialog.getByText(legs.remove.Delete)).toBeVisible();
  await whileWaitingFor(page, 'POST', '/remove', async () => {
    await dialog.getByRole('button', { name: englishCommon.common.delete }).click();
  });
  await expect(row(page, '4')).toHaveCount(0);
  await expect(row(page, '2').getByRole('combobox', { name: legs.fields.departureIcao })).toHaveValue(
    benchAirports.milan,
  );

  // ---------------------------------------------------------------- ready
  await expect(page.getByText(flightops.tours.readyProblems)).toBeHidden();
  await whileWaitingFor(page, 'POST', '/status', async () => {
    await page.getByRole('button', { name: flightops.tours.actions.ready }).click();
  });
  await expect(page.getByRole('button', { name: flightops.tours.actions.hide })).toBeVisible();

  // ---------------------------------------------------------------- deleted, with its legs
  await page.goto(tourUrl);
  await page.getByRole('button', { name: englishCommon.common.delete }).first().click();
  await whileWaitingFor(page, 'DELETE', '/api/flightops/tours/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
  });
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      create: string;
      readyProblems: string;
      tabs: { legs: string };
      actions: { ready: string; hide: string };
      fields: { title: string; summary: string; kind: string };
      options: { kind: { SequentialChosenStart: string } };
    };
    legs: {
      row: string;
      state: { new: string };
      empty: string;
      fields: { departureIcao: string; arrivalIcao: string };
      actions: {
        add: string;
        addAfter: string;
        follows: string;
        closeTour: string;
        duplicate: string;
        remove: string;
      };
      remove: { Delete: string };
    };
    errors: { noLegs: string; notARing: string };
  };
}
