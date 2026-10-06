import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchAirports, benchUrl, readInEnglish, test, whileWaitingFor } from './bench';

/**
 * The "done when" of E5 (M4), through the real screens and the real server: the coordinator of the events (`?as=events`, `IT-EC`,
 * holding the bookings only from the division's grants to the position) pastes a table into the «Slots» tab of an event — copied
 * from a spreadsheet, so separated by tabs — with a rotation of two legs and a slot alone; a row the hub refuses is said by its row
 * and its column, and nothing is loaded until the table is right; then the slots are in the tab, and a visitor finds them on the
 * page of the published event, the rotation's legs together, free.
 *
 * The bench survives between runs: the events of this spec's stem are taken back after the test (`afterwards`), and an interrupted
 * run's leftovers at the start. The aircraft types are the bench's (the fixtures of the network give it four).
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e5';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench slots ${stamp}`, it: `Gli slot del banco ${stamp}` };
const header =
  'callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg';

/** A context of its own, reading in English, signed in as one of the bench's people — or nobody, a visitor. */
async function contextAs(browser: Browser, who: 'events' | null): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  if (who !== null) {
    const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
    expect(signedIn.status(), await signedIn.text()).toBe(200);
  }

  return context;
}

/** The day of the event, twenty days from now, and an hour of it as the table writes it: `2026-10-26 18:00`, in UTC. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const at = (time: string) => `${day} ${time}`;

/** An RFO with public slots at the bench's Rome, its bookings open, as the event's page makes it: this spec is about its slots. */
async function eventWithAirport(request: APIRequestContext): Promise<number> {
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfo',
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
      bookingOpensAtUtc: new Date(Date.now() + 2 * 24 * 3600 * 1000).toISOString(),
      startsAtUtc: `${day}T17:00:00Z`,
      endsAtUtc: `${day}T23:00:00Z`,
      visibility: 'Public',
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const airport = await request.post('/api/events/airports', {
    headers: asTheClientDoes,
    data: {
      eventId: id,
      icao: benchAirports.rome,
      ordinal: 1,
      maxMovementsPerHour: null,
      maxArrivalsPerHour: null,
      maxDeparturesPerHour: null,
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(airport.status(), await airport.text()).toBe(201);

  return id;
}

/** The events of this spec's stem a run left behind, deleted through the API by whoever may. */
async function removeOurEvents(request: APIRequestContext): Promise<void> {
  const page = await request.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);

  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await request.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('the coordinator of the events pastes a table with a rotation, and a visitor finds the slots on the page', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await contextAs(browser, 'events');
  await removeOurEvents(coordinator.request);
  const visitor = await contextAs(browser, null);

  afterwards(async () => {
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), visitor.close()]);
  });

  const id = await eventWithAirport(coordinator.request);
  const { rome, milan, bari } = benchAirports;

  const page = await coordinator.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the tab, and the page that loads a table
  await page.goto(`/staff/events/${id}`);
  await expect(page.getByRole('heading', { name: title.en })).toBeVisible();
  await page.getByRole('tab', { name: events.events.tabs.slots }).click();
  await expect(page).toHaveURL(/tab=slots/);
  await page.getByRole('link', { name: events.slots.load.open }).click();
  await expect(page.getByRole('heading', { name: events.slots.load.title })).toBeVisible();

  // A rotation out of Rome and back, and an arrival from Bari — the second leg's type one the hub does not know.
  const legOut = ['XEE501', 'XE501', 'A320/A20N', rome, at('18:00'), milan, at('19:10'), 'B12', 'R1', ''];
  const legBack = ['XEE502', 'XE502', 'A320', milan, at('19:40'), rome, at('20:50'), '', 'R1', ''];
  const alone = ['XEE503', '', 'E55P', bari, at('18:30'), rome, at('19:40'), '', '', ''];
  const table = (rows: string[][]) => [header, ...rows.map((row) => row.join('\t'))].join('\n');

  const text = page.locator('[id="text"]');
  await text.fill(table([legOut, ['XEE502', 'XE502', 'XZZZ', ...legBack.slice(3)], alone]));
  const refused = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      response.url().includes(`/api/events/events/${id}/slots/load`),
  );
  await page.getByRole('button', { name: events.slots.load.submit, exact: true }).click();
  expect((await refused).status()).toBe(400);

  // Refused by its row and its column, and nothing loaded.
  await expect(page.getByText(events.slots.load.refused)).toBeVisible();
  await expect(page.getByText(`Row 3, aircraft_types: ${events.errors.aircraftUnknown}`)).toBeVisible();

  // Corrected, and loaded: back to the tab, which lists them.
  await text.fill(table([legOut, legBack, alone]));
  await whileWaitingFor(page, 'POST', `/api/events/events/${id}/slots/load`, async () => {
    await page.getByRole('button', { name: events.slots.load.submit, exact: true }).click();
  });
  await expect(page).toHaveURL(/tab=slots/);
  for (const callsign of ['XEE501', 'XEE502', 'XEE503']) {
    await expect(page.getByRole('row').filter({ hasText: callsign })).toBeVisible();
  }

  // ---------------------------------------------------------------- published, a visitor reads the page
  const publish = await coordinator.request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: '0001-01-01T00:00:00' },
  });
  expect(publish.status(), await publish.text()).toBe(200);

  const reader = await visitor.newPage();
  reader.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await reader.goto(`/events/${slug}`);
  await expect(reader.getByRole('heading', { level: 1, name: title.en })).toBeVisible();

  const slots = reader.getByRole('region', { name: events.public.slots });
  const rows = slots.getByRole('row');
  // The rotation's legs together, where its first one falls, then the arrival from Bari; every one free.
  await expect(rows.nth(1)).toContainText(events.public.rotation.replace('{{rotation}}', 'R1'));
  await expect(rows.nth(2)).toContainText('XEE501');
  await expect(rows.nth(3)).toContainText('XEE502');
  await expect(rows.nth(4)).toContainText('XEE503');
  await expect(rows.nth(2)).toContainText('B12');
  await expect(rows.nth(2)).toContainText('18:00');
  await expect(slots.getByText(events.public.free)).toHaveCount(3);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { slots: string } };
    slots: { load: { open: string; title: string; submit: string; refused: string } };
    public: { slots: string; free: string; rotation: string };
    errors: { aircraftUnknown: string };
  };
}
