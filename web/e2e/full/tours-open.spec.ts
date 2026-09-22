import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchAirports, choose, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The "done when" of T7c (M2), through the real screens: an Open tour composed from nothing — a goal written in its own
 * tab, two filters and a sequence rule each in the generated form of its kind — a wrong parameter refused on its field,
 * and the tour marked ready. The tour is deleted at the end, with its constraints: the bench database is not thrown away
 * between runs.
 */

const flightops = englishFlightOps();
const constraints = flightops.constraints;

const stamp = Date.now().toString(36);
const tourName = { en: `Bench open ${stamp}`, it: `Open del banco ${stamp}` };
const summary = { en: `A bench open tour ${stamp}`, it: `Un tour open del banco ${stamp}` };
const slug = `bench-open-${stamp}`;

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/** A filter or a rule of this kind: its kind chosen first, then its parameters written, then saved. */
async function newConstraint(page: Page, kind: string, fill: () => Promise<void>): Promise<void> {
  await page.getByRole('link', { name: constraints.create }).click();
  await expect(page.getByRole('heading', { name: constraints.create })).toBeVisible();
  await choose(page, constraints.fields.kind, kind);
  await fill();
  await whileWaitingFor(page, 'POST', '/api/flightops/tour-constraints', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/tab=open/);
}

test('an Open tour is composed from nothing — a goal, two filters, a sequence rule — and marked ready', async ({
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
  await choose(page, flightops.tours.fields.kind, flightops.tours.options.kind.Open);
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

  // No goal, no ready: said before anybody presses.
  await expect(page.getByText(`${flightops.tours.fields.openGoal}:`)).toBeVisible();

  // ---------------------------------------------------------------- the goal, in its tab
  await page.getByRole('tab', { name: flightops.tours.tabs.open }).click();
  await expect(page).toHaveURL(/tab=open/);

  await choose(page, flightops.tours.fields.openGoal, flightops.tours.options.openGoal.CollectList);
  await expect(page.getByText(flightops.open.goals.CollectList)).toBeVisible();
  const add = page.getByRole('button', { name: englishCommon.form.addEntry });
  for (const [index, icao] of [benchAirports.rome, benchAirports.milan, benchAirports.bari].entries()) {
    await add.click();
    await page.locator(`[id="openGoalParameters.airports.${index}.icao"]`).fill(icao.toLowerCase());
  }
  await page.locator('[id="openGoalParameters.count"]').fill('2');
  await whileWaitingFor(page, 'PUT', '/api/flightops/tours/', async () => {
    await page.getByRole('button', { name: flightops.open.saveGoal }).click();
  });
  await expect(page.getByText(flightops.open.goalSaved)).toBeVisible();
  await expect(page.getByText(`${flightops.tours.fields.openGoal}:`)).toBeHidden();

  // ---------------------------------------------------------------- two filters and a sequence rule
  await newConstraint(page, constraints.options.kind.DepartureOrArrivalIn, async () => {
    await page.getByRole('button', { name: englishCommon.form.addEntry }).click();
    await page.locator('[id="parameters.countries.0.code"]').fill('it');
  });

  // A distance whose maximum is below its minimum is refused on the field, and nothing is saved.
  await page.getByRole('link', { name: constraints.create }).click();
  await choose(page, constraints.fields.kind, constraints.options.kind.DistanceBetween);
  await page.locator('[id="parameters.minNm"]').fill('1500');
  await page.locator('[id="parameters.maxNm"]').fill('100');
  const refused = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' && response.url().includes('/api/flightops/tour-constraints'),
  );
  await page.getByRole('button', { name: englishCommon.common.save }).click();
  expect((await refused).status()).toBe(400);
  await expect(page.getByText(flightops.errors.distanceBounds)).toBeVisible();
  await page.locator('[id="parameters.minNm"]').fill('100');
  await page.locator('[id="parameters.maxNm"]').fill('1500');
  await whileWaitingFor(page, 'POST', '/api/flightops/tour-constraints', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/tab=open/);

  await newConstraint(page, constraints.options.kind.Chained, async () => {
    await expect(page.getByText(constraints.explain.Chained)).toBeVisible();
  });

  // The three rows, each with its values.
  const table = page.getByRole('table');
  await expect(table.getByText(constraints.options.kind.DepartureOrArrivalIn)).toBeVisible();
  await expect(table.getByText('IT', { exact: true })).toBeVisible();
  await expect(table.getByText('≤ 1500 NM')).toBeVisible();
  await expect(table.getByText(constraints.options.kind.Chained)).toBeVisible();

  // ---------------------------------------------------------------- ready
  await expect(page.getByText(flightops.tours.readyProblems)).toBeHidden();
  await whileWaitingFor(page, 'POST', '/status', async () => {
    await page.getByRole('button', { name: flightops.tours.actions.ready }).click();
  });
  await expect(page.getByRole('button', { name: flightops.tours.actions.hide })).toBeVisible();

  // ---------------------------------------------------------------- deleted, with everything it has
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
      readyProblems: string;
      tabs: { open: string };
      actions: { ready: string; hide: string };
      fields: { title: string; summary: string; kind: string; openGoal: string };
      options: { kind: { Open: string }; openGoal: { CollectList: string } };
    };
    open: { saveGoal: string; goalSaved: string; goals: { CollectList: string } };
    constraints: {
      create: string;
      fields: { kind: string };
      options: { kind: { DepartureOrArrivalIn: string; DistanceBetween: string; Chained: string } };
      explain: { Chained: string };
    };
    errors: { distanceBounds: string };
  };
}
