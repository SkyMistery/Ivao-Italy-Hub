import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect } from '@playwright/test';

import { englishCommon } from '../locales';

import {
  addLeg,
  benchAirports,
  choose,
  readInEnglish,
  removeBenchTours,
  signIn,
  test,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * #549, through the real screens: a draft hub tour with a hub and two legs becomes Open. Step 1 says, before the save,
 * that the rows stay; the check names them with the way to the route; there they are read-only — no new leg, no new hub
 * — each with its "remove" or "delete", the line that says why next to them; once they are gone, "ready" no longer names
 * them. The server's half (a leg removed on an Open tour, every other write refused) is `OpenTourTests`.
 */

const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  tours: {
    fields: { title: string; summary: string; kind: string };
    options: { kind: { Hub: string; Open: string } };
    steps: {
      next: string;
      route: string;
      check: string;
      goTo: string;
      kindLeaves: string;
      leftoverLegs: string;
      leftoverHubs: string;
    };
  };
  legs: { actions: { add: string; remove: string; import: string }; remove: { Delete: string } };
  hubs: { create: string };
  errors: { kindHasNoLegs: string; kindHasNoHubs: string };
};

const stamp = Date.now().toString(36);
const slug = `bench-leftover-${stamp}`;
const asTheClientDoes = { 'X-Requested-With': 'hub' };

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

test('the rows a new kind does not use are shown where "ready" sends, and deleted there', async ({
  page,
  context,
  afterwards,
}) => {
  await readInEnglish(context);
  await signIn(context);
  afterwards(() => removeBenchTours(context, 'bench-leftover-'));

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- a draft hub tour, a hub and two legs
  const now = Date.now();
  await page.goto('/staff/tours/new');
  await choose(page, words.tours.fields.kind, words.tours.options.kind.Hub);
  await page.getByRole('button', { name: words.tours.steps.next, exact: true }).click();
  await writeInBothLanguages(page.locator('form'), words.tours.fields.title, 'title', {
    en: `Bench leftover ${stamp}`,
    it: `Righe rimaste ${stamp}`,
  });
  await page.locator('[id="slug"]').fill(slug);
  await writeInBothLanguages(page.locator('form'), words.tours.fields.summary, 'summary', {
    en: 'A kind changed under its rows.',
    it: 'Un tipo cambiato sotto le sue righe.',
  });
  await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now + 5 * 24 * 3600 * 1000)));
  await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
  await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/\/staff\/tours\/\d+(\?|$)/);
  const tourId = Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]);

  const hub = await context.request.post('/api/flightops/hubs', {
    headers: asTheClientDoes,
    data: { tourId, icao: benchAirports.rome, sort: 1, rowVersion: '0001-01-01T00:00:00' },
  });
  expect(hub.status(), await hub.text()).toBeLessThan(300);
  await addLeg(context, tourId, benchAirports.rome, benchAirports.milan);
  await addLeg(context, tourId, benchAirports.milan, benchAirports.rome);

  // ---------------------------------------------------------------- Open, said before the save
  await page.goto(`/staff/tours/${tourId}?tab=kind`);
  await choose(page, words.tours.fields.kind, words.tours.options.kind.Open);
  const leaves = words.tours.steps.kindLeaves
    .split('{{parts}}')[0]!
    .replace('{{kind}}', words.tours.options.kind.Open);
  await expect(page.getByText(leaves)).toBeVisible();
  await whileWaitingFor(page, 'PUT', `/api/flightops/tours/${tourId}`, async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });

  // ---------------------------------------------------------------- the check names them, and leads to the route
  await page.getByRole('tab', { name: words.tours.steps.check }).click();
  await expect(page.getByText(words.errors.kindHasNoLegs)).toBeVisible();
  await expect(page.getByText(words.errors.kindHasNoHubs)).toBeVisible();
  await page
    .getByRole('link', { name: words.tours.steps.goTo.replace('{{step}}', words.tours.steps.route) })
    .first()
    .click();
  await expect(page).toHaveURL(/tab=route/);

  // Read-only, with the line that says why: nothing new is offered.
  await expect(page.getByText(words.tours.steps.leftoverLegs)).toBeVisible();
  await expect(page.getByText(words.tours.steps.leftoverHubs)).toBeVisible();
  await expect(page.getByRole('button', { name: words.legs.actions.add, exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: words.legs.actions.import })).toHaveCount(0);
  await expect(page.getByRole('link', { name: words.hubs.create })).toHaveCount(0);

  // ---------------------------------------------------------------- the legs removed, then the hub deleted
  for (let left = 2; left > 0; left--) {
    await expect(page.getByRole('button', { name: words.legs.actions.remove })).toHaveCount(left);
    await page.getByRole('button', { name: words.legs.actions.remove }).first().click();
    const dialog = page.getByRole('alertdialog');
    await expect(dialog.getByText(words.legs.remove.Delete)).toBeVisible();
    await whileWaitingFor(page, 'POST', '/remove', async () => {
      await dialog.getByRole('button', { name: englishCommon.common.delete }).click();
    });
  }

  await page
    .getByRole('row')
    .filter({ hasText: benchAirports.rome })
    .getByRole('button', { name: englishCommon.common.delete })
    .click();
  await whileWaitingFor(page, 'DELETE', '/api/flightops/hubs/', async () => {
    await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
  });

  // ---------------------------------------------------------------- "ready" no longer names them
  await page.getByRole('tab', { name: words.tours.steps.check }).click();
  await expect(page.getByText(words.errors.kindHasNoLegs)).toHaveCount(0);
  await expect(page.getByText(words.errors.kindHasNoHubs)).toHaveCount(0);
});
