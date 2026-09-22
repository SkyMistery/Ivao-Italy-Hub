import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type BrowserContext } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  addLeg,
  benchAirports,
  benchUrl,
  choose,
  readInEnglish,
  signIn,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The "done when" of T10 (M2), through the real screens and a real browser: the flight operations department writes a
 * tour with three legs and releases it, and anybody — never signed in — finds it on `/tours`, opens it, and sees the
 * map of its legs with the base map the hub serves itself, the distances, and the flight plan on SimBrief.
 *
 * ⚠️ The map is the reason this spec exists rather than a unit test: MapLibre needs WebGL2, a worker and `blob:` in
 * the policy, and the round is the only suite where all three are the real ones — the published package, its own
 * `config/security.json`, and Chromium. A console error here is a map that would not have drawn for a visitor.
 *
 * Every row goes back at the end, and a run that stopped half way has its leftovers taken at the start: the bench
 * database stays between runs (memory `bench-database-accumulates`).
 */

const flightops = englishFlightOps();
const tours = flightops.tours;

const stamp = Date.now().toString(36);
const tourName = { en: `Bench public ${stamp}`, it: `Pubblico del banco ${stamp}` };
const slug = `bench-public-${stamp}`;

const asTheClientDoes = { 'X-Requested-With': 'hub' };

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

async function items(context: BrowserContext, uri: string): Promise<Record<string, unknown>[]> {
  const response = await context.request.get(uri);
  expect(response.status()).toBe(200);
  return ((await response.json()) as { items: Record<string, unknown>[] }).items;
}

/** The tours a run of this spec left behind, recognised by the address it gives them and nothing else. */
async function removeLeftovers(context: BrowserContext): Promise<void> {
  for (const tour of await items(context, '/api/flightops/tours?pageSize=100&q=bench-public-')) {
    const response = await context.request.delete(`/api/flightops/tours/${String(tour.id)}`, {
      headers: asTheClientDoes,
    });
    expect(response.status(), await response.text()).toBeLessThan(300);
  }
}

test('a released tour is read by a visitor, with its legs on the map', async ({ page, context }) => {
  test.setTimeout(120_000);
  await readInEnglish(context);
  await signIn(context);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const complaints: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      complaints.push(message.text());
    }
  });

  await removeLeftovers(context);
  try {
    await composeAndRead();
  } finally {
    await removeLeftovers(context);
  }

  async function composeAndRead() {
    // ---------------------------------------------------------------- a tour, released yesterday
    const now = Date.now();
    await page.goto('/staff/tours/new');
    await choose(page, tours.fields.kind, tours.options.kind.Free);
    await writeInBothLanguages(page.locator('form'), tours.fields.title, 'title', tourName);
    await page.locator('[id="slug"]').fill(slug);
    await writeInBothLanguages(page.locator('form'), tours.fields.summary, 'summary', {
      en: 'Three legs to look at on a map.',
      it: 'Tre tratte da guardare su una mappa.',
    });
    await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 24 * 3600 * 1000)));
    await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
    await page.locator('[id="dailyLegLimit"]').fill('5');
    await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);

    const tourId = Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]);
    await addLeg(context, tourId, benchAirports.rome, benchAirports.milan);
    await addLeg(context, tourId, benchAirports.milan, benchAirports.bari);
    await addLeg(context, tourId, benchAirports.bari, benchAirports.rome);

    // ---------------------------------------------------------------- not ready: nobody outside the staff sees it
    const beforeReady = await context.request.get(`/api/flightops/tours/public/${slug}`);
    expect(beforeReady.status()).toBe(404);

    await page.goto(`/staff/tours/${tourId}`);
    await whileWaitingFor(page, 'POST', `/api/flightops/tours/${tourId}/status`, async () => {
      await page.getByRole('button', { name: tours.actions.ready }).click();
    });

    // ---------------------------------------------------------------- as a visitor, who never signed in
    const visitor = await page.context().browser()!.newContext({ baseURL: benchUrl });
    await visitor.addCookies([{ name: 'hub.lang', value: 'en', url: benchUrl }]);
    const reader = await visitor.newPage();
    const visitorComplaints: string[] = [];
    reader.on('console', (message) => {
      if (message.type() === 'error') {
        visitorComplaints.push(message.text());
      }
    });
    reader.on('pageerror', (error) => {
      throw new Error(`The public page threw: ${error.message}`);
    });

    try {
      // A wide window: these two screens are looked at on a desktop, and the cards go to three columns there.
      await reader.setViewportSize({ width: 1500, height: 1200 });

      await reader.goto('/tours');
      const card = reader.getByRole('article').filter({ hasText: tourName.en });
      await expect(card).toBeVisible();
      await expect(card.getByText('3 legs · 925 NM', { exact: false })).toBeVisible();
      await card.getByRole('link').first().click();

      await expect(reader).toHaveURL(new RegExp(`/tours/${slug}$`));
      await expect(reader.getByRole('heading', { level: 1, name: tourName.en })).toBeVisible();

      // The legs, in order, with their distances and a flight plan for each.
      const legs = reader.getByRole('row');
      await expect(legs.filter({ hasText: benchAirports.rome }).first()).toBeVisible();
      await expect(reader.getByRole('link', { name: flightops.public.simbrief }).first()).toHaveAttribute(
        'href',
        new RegExp(
          `dispatch\\.simbrief\\.com/options/custom\\?orig=${benchAirports.rome}&dest=${benchAirports.milan}`,
        ),
      );

      // The map: a canvas that MapLibre actually drew — not an element that merely exists — and the attribution the
      // licence of the data asks for.
      const map = reader.getByTestId('route-map');
      await expect(map).toBeVisible();
      await expect(map.locator('canvas.maplibregl-canvas')).toBeVisible();
      await expect(map.locator('canvas.maplibregl-canvas')).not.toHaveJSProperty('width', 0);
      await expect(map.getByText(benchAirports.milan, { exact: true })).toBeVisible();
      await expect(map.getByRole('link', { name: 'OpenStreetMap' })).toBeVisible();

      // ⚠️ The whole reason the round runs the published package: this page is drawn under the real policy, and a map
      // refused a worker or a `blob:` texture says so in the console and nowhere else.
      expect(visitorComplaints.filter((text) => !text.includes('favicon'))).toEqual([]);

      // Hidden again: gone from both addresses, for everybody.
      await page.goto(`/staff/tours/${tourId}`);
      await whileWaitingFor(page, 'POST', `/api/flightops/tours/${tourId}/status`, async () => {
        await page.getByRole('button', { name: tours.actions.hide }).click();
      });

      const hidden = await context.request.get(`/api/flightops/tours/public/${slug}`);
      expect(hidden.status()).toBe(404);
    } finally {
      await visitor.close();
    }

    expect(complaints.filter((text) => !text.includes('favicon'))).toEqual([]);
  }
});

/** The module's own English, read from the file the module ships: no user facing string is written in a spec. */
function englishFlightOps() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
  ) as {
    tours: {
      fields: { title: string; summary: string; kind: string };
      options: { kind: { Free: string } };
      actions: { ready: string; hide: string };
    };
    public: { simbrief: string };
  };
}
