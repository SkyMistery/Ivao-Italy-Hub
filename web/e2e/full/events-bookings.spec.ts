import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchAirports, benchUrl, mailFor, readInEnglish, test, whileWaitingFor } from './bench';

/**
 * The "done when" of E6b (M4), through the real screens and the real server: a published event with a rotation of two legs and a
 * slot alone; the bench's pilot (`?as=pilot`, a member with a mailbox of Mailpit) opens a leg on the page of the event and books the
 * whole rotation with one aircraft, finds both legs in `/events/mine`, and withdraws one — the slot is free again on the page, the
 * other leg theirs —; the coordinator of the events (`?as=events`) reads the bookings in the tab of the event, the pilot named, and
 * takes the other leg away with a reason, which the pilot reads in Mailpit. The reminder of the day before runs every quarter of an
 * hour: its rules are `EventsBookingPagesTests`'s, and the bench of the phase watched it arrive once.
 *
 * The bench survives between runs: the bookings of this spec's events are withdrawn by the pilot and the events deleted after the test
 * (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e6b';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench bookings ${stamp}`, it: `Le prenotazioni del banco ${stamp}` };
const header =
  'callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg';
const pilotAddress = 'bench-pilot@bench.test';

/** A context of its own, reading in English, signed in as one of the bench's people. */
async function contextAs(browser: Browser, who: 'events' | 'pilot'): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** The day of the event, twenty days from now, and an hour of it as the table writes it: `2026-10-29 18:00`, in UTC. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const at = (time: string) => `${day} ${time}`;

/** An RFE with public slots at the bench's Rome, loaded from a table and published, its bookings open since an hour ago. */
async function publishedEvent(request: APIRequestContext): Promise<number> {
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
      bookingOpensAtUtc: new Date(Date.now() - 3600 * 1000).toISOString(),
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

  // A rotation out of Rome and back, the first leg admitting two types, and a flight alone from Bari.
  const { rome, milan, bari } = benchAirports;
  const rows = [
    ['XEE601', 'XE601', 'A320/A20N', rome, at('18:00'), milan, at('19:10'), 'B12', 'R1', ''],
    ['XEE602', '', 'A320/A20N', milan, at('19:40'), rome, at('20:50'), '', 'R1', ''],
    ['XEE603', '', 'E55P', bari, at('18:30'), rome, at('19:40'), '', '', ''],
  ];
  const loaded = await request.post(`/api/events/events/${id}/slots/load`, {
    headers: asTheClientDoes,
    data: { text: [header, ...rows.map((row) => row.join('\t'))].join('\n'), mode: 'Add' },
  });
  expect(loaded.status(), await loaded.text()).toBe(200);

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: '0001-01-01T00:00:00' },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

/**
 * What a run of this spec left, taken back the way the product allows: the pilot withdraws their bookings of its events — an event
 * with bookings is not deleted —, then the coordinator deletes the events.
 */
async function removeOurEvents(coordinator: APIRequestContext, pilot: APIRequestContext): Promise<void> {
  const mine = await pilot.get('/api/events/mine/bookings');
  expect(mine.status(), await mine.text()).toBe(200);
  for (const booking of (await mine.json()) as readonly { id: number; eventSlug: string }[]) {
    if (booking.eventSlug.startsWith(stem)) {
      const withdrawn = await pilot.delete(`/api/events/mine/bookings/${booking.id}`, {
        headers: asTheClientDoes,
      });
      expect(withdrawn.status(), await withdrawn.text()).toBe(204);
    }
  }

  const page = await coordinator.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);
  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await coordinator.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('a pilot books a whole rotation, finds it in their bookings and withdraws a leg; the staff take a booking away', async ({
  browser,
  afterwards,
}) => {
  // The mail leaves with the core's dispatch, once a minute.
  test.setTimeout(240_000);

  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  await removeOurEvents(coordinator.request, pilot.request);

  afterwards(async () => {
    await removeOurEvents(coordinator.request, pilot.request);
    await Promise.all([coordinator.close(), pilot.close()]);
  });

  const id = await publishedEvent(coordinator.request);

  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the pilot books the rotation from a leg
  await page.goto(`/events/${slug}`);
  await expect(page.getByRole('heading', { level: 1, name: title.en })).toBeVisible();
  // The bookings are open, since a moment written on the page.
  await expect(page.locator('dl').getByText(events.public.booking.openSince)).toBeVisible();

  await page.getByRole('button', { name: 'XEE601' }).click();
  const detail = page.getByRole('dialog', { name: 'XEE601 · XE601' });
  const choice = detail.getByRole('radiogroup', { name: events.public.booking.aircraft });
  await expect(choice.getByRole('radio', { name: /A320/ })).toBeChecked();
  await choice.getByRole('radio', { name: /A20N/ }).click();

  await whileWaitingFor(page, 'POST', '/api/events/mine/bookings/rotation', async () => {
    await detail.getByRole('button', { name: events.public.booking.bookRotation }).click();
  });
  const result = detail.getByRole('region', { name: events.public.booking.rotationResult });
  await expect(result).toContainText('XEE601');
  await expect(result).toContainText('XEE602');
  await expect(result).toContainText(
    events.public.booking.rotationDone.replace('{{booked}}', '2').replace('{{of}}', '2'),
  );

  // ---------------------------------------------------------------- their bookings: both legs, and one withdrawn
  await detail.getByRole('link', { name: events.public.booking.toMine }).click();
  await expect(page).toHaveURL(/\/events\/mine$/);
  const upcoming = page.getByRole('region', { name: events.mine.upcoming });
  const theEvent = upcoming.getByRole('region', { name: title.en });
  await expect(theEvent.getByRole('listitem').filter({ hasText: 'XEE601' })).toContainText('A20N');
  const legBack = theEvent.getByRole('listitem').filter({ hasText: 'XEE602' });
  await expect(legBack).toBeVisible();

  await legBack.getByRole('button', { name: events.mine.withdraw }).click();
  await whileWaitingFor(page, 'DELETE', '/api/events/mine/bookings/', async () => {
    await page
      .getByRole('alertdialog', { name: events.mine.withdrawTitle.replace('{{callsign}}', 'XEE602') })
      .getByRole('button', { name: events.mine.withdraw })
      .click();
  });
  await expect(theEvent.getByRole('listitem').filter({ hasText: 'XEE602' })).toHaveCount(0);
  await expect(theEvent.getByRole('listitem').filter({ hasText: 'XEE601' })).toBeVisible();

  // On the page of the event: the leg kept is theirs, the leg withdrawn free again.
  await page.goto(`/events/${slug}`);
  await expect(page.getByRole('button', { name: 'XEE601' }).locator('xpath=ancestor::tr')).toContainText(
    events.public.yours,
  );
  await expect(page.getByRole('button', { name: 'XEE602' }).locator('xpath=ancestor::tr')).toContainText(
    events.public.free,
  );

  // ---------------------------------------------------------------- the coordinator takes the other leg away
  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await staff.goto(`/staff/events/${id}?tab=bookings`);
  await expect(staff.getByRole('tab', { name: events.events.tabs.bookings })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  const row = staff.getByRole('row').filter({ hasText: 'XEE601' });
  await expect(row).toContainText('Bench Pilot (999002)');
  await expect(row).toContainText('A20N');

  const reason = `The bench takes it away ${stamp}`;
  await row.getByRole('button', { name: events.bookings.remove.trigger }).click();
  const question = staff.getByRole('alertdialog', {
    name: events.bookings.remove.title.replace('{{callsign}}', 'XEE601'),
  });
  await question.locator('[id="reason"]').fill(reason);
  await whileWaitingFor(staff, 'POST', `/remove`, async () => {
    await question.getByRole('button', { name: events.bookings.remove.confirm }).click();
  });
  await expect(staff.getByRole('row').filter({ hasText: 'XEE601' })).toHaveCount(0);

  // The pilot is told why, in their mailbox and in their language: the run's stamp is in the title of both.
  await expect
    .poll(async () => (await mailFor(pilot.request, pilotAddress, stamp))?.Text ?? '', {
      timeout: 150_000,
      intervals: [5_000],
    })
    .toContain(reason);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { bookings: string } };
    public: {
      free: string;
      yours: string;
      booking: {
        openSince: string;
        aircraft: string;
        bookRotation: string;
        rotationResult: string;
        rotationDone: string;
        toMine: string;
      };
    };
    mine: { upcoming: string; withdraw: string; withdrawTitle: string };
    bookings: { remove: { trigger: string; title: string; confirm: string } };
  };
}
