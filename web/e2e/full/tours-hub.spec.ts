import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Locator, type Page } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchAirports, choose, readInEnglish, signIn, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The "done when" of T7 (M2, T7b), through the real screens: a hub tour composed from nothing — two hubs, a rotation out
 * of each written in the generated forms of its tab, the legs of the rotations and the connection between the hubs
 * placed in the table of the legs — "ready" refusing it while a rotation is short, and then marking it ready. The tour is
 * deleted at the end, with its hubs, rotations and legs: the bench database is not thrown away between runs.
 */

const flightops = englishFlightOps();
const legs = flightops.legs;

const stamp = Date.now().toString(36);
const tourName = { en: `Bench hubs ${stamp}`, it: `Hub del banco ${stamp}` };
const summary = { en: `A bench hub tour ${stamp}`, it: `Un tour a hub del banco ${stamp}` };
const slug = `bench-hubs-${stamp}`;

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

function row(page: Page, number: string): Locator {
  const name = number === '+' ? legs.state.new : legs.row.replace('{{number}}', number);
  return page.getByRole('row', { name, exact: true });
}

/** The first rotation of a hub as the table offers it: "LIRF 1 (0/2)", whatever its count of legs. */
function rotationOf(hub: string): RegExp {
  const label = legs.placement.rotation.replace('{{hub}}', hub).replace('{{number}}', '1');
  return new RegExp(`^${label.slice(0, label.indexOf(' ('))} \\(`);
}

/** A new leg at the end of the table, placed where it belongs, and saved. */
async function addLeg(
  page: Page,
  departure: string,
  arrival: string,
  placement: string | RegExp,
): Promise<void> {
  await page.getByRole('button', { name: legs.actions.add }).click();
  const fresh = row(page, '+');
  await fresh.getByRole('combobox', { name: legs.fields.departureIcao }).fill(departure);
  await fresh.getByRole('combobox', { name: legs.fields.arrivalIcao }).fill(arrival);
  await fresh.getByRole('combobox', { name: legs.fields.rotationId }).click();
  await page.getByRole('option', { name: placement }).click();
  await whileWaitingFor(page, 'POST', '/legs', async () => {
    await fresh.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(row(page, '+')).toHaveCount(0);
}

async function newHub(page: Page, icao: string, sort: string): Promise<void> {
  await page.getByRole('link', { name: flightops.hubs.create }).click();
  await expect(page.getByRole('heading', { name: flightops.hubs.create })).toBeVisible();
  await page.locator('[id="icao"]').fill(icao);
  await page.locator('[id="sort"]').fill(sort);
  await whileWaitingFor(page, 'POST', '/api/flightops/hubs', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/tab=hubs/);
}

async function newRotation(page: Page, hub: string): Promise<void> {
  await page.getByRole('link', { name: flightops.rotations.create }).click();
  await expect(page.getByRole('heading', { name: flightops.rotations.create })).toBeVisible();
  await choose(page, flightops.rotations.fields.hubId, hub);
  await choose(page, flightops.rotations.fields.size, flightops.rotations.options.size['2']);
  await whileWaitingFor(page, 'POST', '/api/flightops/rotations', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/tab=hubs/);
}

test('a hub tour is composed from nothing — hubs, rotations, a connection — and marked ready', async ({
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
  await choose(page, flightops.tours.fields.kind, flightops.tours.options.kind.Hub);
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

  // No hubs, no ready: said before anybody presses.
  await expect(page.getByText(flightops.errors.noHubs)).toBeVisible();

  // ---------------------------------------------------------------- hubs and rotations, in their tab
  await page.getByRole('tab', { name: flightops.tours.tabs.hubs }).click();
  await expect(page).toHaveURL(/tab=hubs/);

  await newHub(page, benchAirports.rome, '1');
  await newHub(page, benchAirports.milan, '2');
  await newRotation(page, benchAirports.rome);
  await newRotation(page, benchAirports.milan);

  // Each hub has a rotation, and no rotation has its legs yet.
  await expect(page.getByText(flightops.errors.hubWithoutRotations)).toBeHidden();
  await expect(page.getByText(flightops.errors.rotationSize).first()).toBeVisible();

  // ---------------------------------------------------------------- the legs, each in its place
  await page.getByRole('tab', { name: flightops.tours.tabs.legs }).click();
  await expect(page).toHaveURL(/tab=legs/);

  await addLeg(page, benchAirports.rome, benchAirports.bari, rotationOf(benchAirports.rome));
  await addLeg(page, benchAirports.bari, benchAirports.rome, rotationOf(benchAirports.rome));
  await addLeg(page, benchAirports.rome, benchAirports.milan, legs.placement.connection);
  await addLeg(page, benchAirports.milan, benchAirports.bari, rotationOf(benchAirports.milan));

  // Milan's rotation is still one leg short.
  await expect(page.getByText(flightops.errors.rotationSize)).toBeVisible();

  await addLeg(page, benchAirports.bari, benchAirports.milan, rotationOf(benchAirports.milan));

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
      tabs: { legs: string; hubs: string };
      actions: { ready: string; hide: string };
      fields: { title: string; summary: string; kind: string };
      options: { kind: { Hub: string } };
    };
    hubs: { create: string };
    rotations: {
      create: string;
      fields: { hubId: string; size: string };
      options: { size: Record<'2', string> };
    };
    legs: {
      row: string;
      state: { new: string };
      fields: { departureIcao: string; arrivalIcao: string; rotationId: string };
      actions: { add: string };
      placement: { connection: string; rotation: string };
    };
    errors: { noHubs: string; hubWithoutRotations: string; rotationSize: string };
  };
}
