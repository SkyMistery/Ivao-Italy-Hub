import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchAirports, benchUrl, readInEnglish, test, whileWaitingFor } from './bench';

/**
 * The tab «Statistics» of E13a (M4), through the real screens and the real server: the coordinator of the events (`?as=events`) opens an
 * event over — published with two public slots ten days ago, nobody booked —, reads that it has no statistics yet, presses «Recalculate»
 * and reads the numbers in the generated form, with the checks after the event still to come, and the list of the bookings empty with its
 * sentence. The checks themselves (`events-after`, every hour) are `EventsAfterTests`'s, with the fixtures of the network: the bench does not
 * run a job when a spec wants it.
 *
 * The bench survives between runs: the coordinator deletes what it can of this spec's events after the test (`afterwards`), and an
 * interrupted run's leftovers at the start; an event ten days old is kept (E14a), and stays under this spec's own stem.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
// Not under `evt-test-e2e` (E14a): an event that started more than half an hour ago is kept, and `events-staff.spec.ts` deletes every event of
// that stem.
const stem = 'evt-e2e-e13a';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench evening over ${stamp}`, it: `La sera finita del banco ${stamp}` };
const blank = '0001-01-01T00:00:00';
const header =
  'callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg';

/** The day of the event, ten days ago. */
const day = new Date(Date.now() - 10 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const at = (time: string) => `${day} ${time}`;

/** A context of its own, reading in English, signed in as the coordinator of the events. */
async function coordinatorContext(browser: Browser): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post('/e2e/signin?as=events');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** An evening at the bench's Rome ten days ago, with two public slots, published. */
async function eventOver(request: APIRequestContext): Promise<number> {
  const created = await request.post('/api/events/events', {
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
      bookingOpensAtUtc: `${day}T10:00:00Z`,
      startsAtUtc: `${day}T18:00:00Z`,
      endsAtUtc: `${day}T22:00:00Z`,
      visibility: 'Public',
      rowVersion: blank,
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
      rowVersion: blank,
    },
  });
  expect(airport.status(), await airport.text()).toBe(201);

  const { rome, milan } = benchAirports;
  const rows = [
    ['XEA131', '', 'A320', rome, at('18:30'), milan, at('19:40'), '', '', ''],
    ['XEA132', '', 'A320', milan, at('19:00'), rome, at('20:10'), '', '', ''],
  ];
  const loaded = await request.post(`/api/events/events/${id}/slots/load`, {
    headers: asTheClientDoes,
    data: { text: [header, ...rows.map((row) => row.join('\t'))].join('\n'), mode: 'Add' },
  });
  expect(loaded.status(), await loaded.text()).toBe(200);

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

/** What a run of this spec left: its events, with their slots and statistics. */
async function removeOurEvents(coordinator: APIRequestContext): Promise<void> {
  const page = await coordinator.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);
  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await coordinator.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    // An event that started more than half an hour ago, or has a report of support, is kept (E14a; Carmine's answer on reading 13 on
    // #289): it stays on the bench, which e2e:full starts empty.
    if (deleted.status() === 400 && /eventStarted|eventHasReports/.test(await deleted.text())) {
      continue;
    }
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('the coordinator recalculates the statistics of an event over and reads them in its tab', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await coordinatorContext(browser);
  await removeOurEvents(coordinator.request);

  afterwards(async () => {
    await removeOurEvents(coordinator.request);
    await coordinator.close();
  });

  const id = await eventOver(coordinator.request);

  const page = await coordinator.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/staff/events/${id}?tab=stats`);
  await expect(page.getByRole('tab', { name: events.events.tabs.stats })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(page.getByText(events.stats.none)).toBeVisible();

  await whileWaitingFor(page, 'POST', `/api/events/events/${id}/stats`, async () => {
    await page.getByRole('button', { name: events.stats.recalculate }).click();
  });

  // The numbers, in a form that saves nothing; who flew without booking waits for the checks after the event.
  await expect(page.getByText(events.stats.checksPending)).toBeVisible();
  await expect(page.getByLabel(events.stats.fields.publicSlots, { exact: true })).toHaveValue('2');
  await expect(page.getByLabel(events.stats.fields.booked, { exact: true })).toHaveValue('0');
  await expect(page.getByRole('button', { name: 'Save' })).toHaveCount(0);

  // The bookings after the event: none, said with the list's sentence. No roster, no list of the shifts.
  await expect(page.getByRole('heading', { name: events.stats.bookings.title })).toBeVisible();
  await expect(page.getByText(events.stats.bookings.empty)).toBeVisible();
  await expect(page.getByRole('heading', { name: events.stats.shifts.title })).toHaveCount(0);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { stats: string } };
    stats: {
      none: string;
      checksPending: string;
      recalculate: string;
      fields: { publicSlots: string; booked: string };
      bookings: { title: string; empty: string };
      shifts: { title: string };
    };
  };
}
