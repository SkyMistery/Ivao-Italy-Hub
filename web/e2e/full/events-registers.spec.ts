import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchUrl, readInEnglish, test, whileWaitingFor } from './bench';

/**
 * E13b (M4b) on the bench, through the real screens and the real server, as far as the bench's clock lets it go: the coordinator of the
 * events (`?as=events`) finds the registers of the controllers and of the pilots in the back office, saves the five settings of the
 * registers and reads them back, and gives an event its own limit of slots in a band from its generated form.
 *
 * ⚠️ A proposed no-show is not made here: it is written by `events-after` (E13a) on a roster written by `events-roster` (E11b), and the
 * bench has no way of starting a job on demand. Confirming, taking away and the register telling it — never on one's own shift — are
 * `EventsRegistersTests`'s, and the bench of the phase watched them on seeded rows (the «done when»).
 *
 * The bench survives between runs: the coordinator puts the settings back and deletes this spec's events after the test (`afterwards`), and
 * an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const settingsUrl = '/api/modules/events/settings';
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e13b';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench limits ${stamp}`, it: `I limiti del banco ${stamp}` };
const blank = '0001-01-01T00:00:00';
const pilotVid = 999002;

/** The day of the event, thirty days from now. */
const day = new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString().slice(0, 10);

interface Settings {
  readonly unflownThreshold: number;
  readonly noShowWeight: number;
  readonly restrictedWindowHours: number;
  readonly restrictedMaxPerWindow: number;
  readonly restrictedMaxPerEvent: number;
}

/** A context of its own, reading in English, signed in as the coordinator of the events. */
async function coordinatorContext(browser: Browser): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post('/e2e/signin?as=events');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** What a run of this spec left: its events. */
async function removeOurEvents(coordinator: APIRequestContext): Promise<void> {
  const page = await coordinator.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);
  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await coordinator.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('the coordinator finds the two registers and saves the settings of the registers it reads back', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await coordinatorContext(browser);
  const before = await coordinator.request.get(settingsUrl);
  expect(before.status(), await before.text()).toBe(200);
  const saved = (await before.json()) as Settings;

  afterwards(async () => {
    const putBack = await coordinator.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
    await coordinator.close();
  });

  const page = await coordinator.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // The two registers, each a page of the back office with what it is for — the heading of a page with a breadcrumb says it in its title.
  await page.goto('/staff/events/controllers');
  await expect(
    page.getByRole('heading', { name: events.registers.controllers.title, level: 1 }),
  ).toHaveAttribute('title', events.registers.controllers.description);
  await page.goto('/staff/events/pilots');
  await expect(page.getByRole('heading', { name: events.registers.pilots.title, level: 1 })).toHaveAttribute(
    'title',
    events.registers.pilots.description,
  );

  // A person's page of each register opens on the person — the bench's pilot, whatever they hold —, never on the router's error.
  for (const register of ['controllers', 'pilots'] as const) {
    await page.goto(`/staff/events/${register}/${pilotVid}`);
    await expect(page.locator('h1[aria-current="page"]')).toHaveText(/\S/);
  }

  // The settings of the registers: the threshold and the weight changed, saved, and read back after a reload.
  await page.goto('/staff/events/settings');
  await expect(page.getByRole('heading', { name: events.settings.title })).toBeVisible();
  const threshold = (saved.unflownThreshold % 10) + 1;
  await page.locator('[id="unflownThreshold"]').fill(String(threshold));
  await page.locator('[id="noShowWeight"]').fill('1.5');
  await expect(page.getByText(events.settings.fields.restrictedMaxPerWindow, { exact: true })).toBeVisible();

  await whileWaitingFor(page, 'PUT', settingsUrl, async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page.getByText(events.settings.saved)).toBeVisible();

  await page.reload();
  await expect(page.locator('[id="unflownThreshold"]')).toHaveValue(String(threshold));
  await expect(page.locator('[id="noShowWeight"]')).toHaveValue('1.5');

  const after = (await (await coordinator.request.get(settingsUrl)).json()) as Settings;
  expect(after).toMatchObject({ unflownThreshold: threshold, noShowWeight: 1.5 });
});

test('the coordinator gives an event its own limit of slots in a band', async ({ browser, afterwards }) => {
  const coordinator = await coordinatorContext(browser);
  await removeOurEvents(coordinator.request);

  afterwards(async () => {
    await removeOurEvents(coordinator.request);
    await coordinator.close();
  });

  const created = await coordinator.request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfe',
      publicSlots: true,
      privateSlots: false,
      wholeDivision: false,
      organizer: 'Division',
      externalUrl: null,
      title,
      slug,
      summary: title,
      body: null,
      bannerMediaId: null,
      visibleFromUtc: null,
      bookingOpensAtUtc: null,
      startsAtUtc: `${day}T18:00:00Z`,
      endsAtUtc: `${day}T22:00:00Z`,
      visibility: 'Public',
      rowVersion: blank,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const page = await coordinator.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/staff/events/${id}`);
  const field = page.locator('[id="restrictedMaxPerWindow"]');
  await expect(field).toHaveValue('');
  await field.fill('2');

  await whileWaitingFor(page, 'PUT', `/api/events/events/${id}`, async () => {
    await page.getByRole('button', { name: englishCommon.common.save, exact: true }).first().click();
  });

  const read = await coordinator.request.get(`/api/events/events/${id}`);
  expect(read.status(), await read.text()).toBe(200);
  expect(await read.json()).toMatchObject({
    restrictedMaxPerWindow: 2,
    restrictedWindowHours: null,
    restrictedMaxPerEvent: null,
  });

  await page.reload();
  await expect(page.locator('[id="restrictedMaxPerWindow"]')).toHaveValue('2');
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    settings: { title: string; saved: string; fields: { restrictedMaxPerWindow: string } };
    registers: {
      controllers: { title: string; description: string };
      pilots: { title: string; description: string };
    };
  };
}
