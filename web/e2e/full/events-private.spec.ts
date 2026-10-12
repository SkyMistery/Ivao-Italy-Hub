import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import {
  expect,
  request as playwrightRequest,
  type APIRequestContext,
  type Browser,
  type BrowserContext,
} from '@playwright/test';

import { benchAirports, benchUrl, readInEnglish, test, whileWaitingFor } from './bench';

/**
 * The "done when" of E7 (M4), through the real screens and the real server: the coordinator of the events (`?as=events`) presses
 * «Generate the private slots» in the tab of an RFO with public and private slots, and its airport's capacity becomes private slots
 * around the public one; the bench's pilot (`?as=pilot`) opens an hour of arrivals on the page of the published event and books an
 * arrival with its departure from the same airport, writing the two flights; the gate manager's program, with nothing but a token,
 * reads the two in the export, each naming the other's slot — the same gate to give them —, with no gate yet.
 *
 * The bench survives between runs: the bookings of this spec's events are withdrawn by the pilot, the events deleted and the token
 * revoked after the test (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e7';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench private slots ${stamp}`, it: `Gli slot privati del banco ${stamp}` };
const tokenName = `${stem} gate ${stamp}`;
const header =
  'callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg';

/** A context of its own, reading in English, signed in as one of the bench's people. */
async function contextAs(browser: Browser, who: 'events' | 'pilot'): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** The day of the event, twenty days from now, and an hour of it: as the table writes it, and as an instant of the API. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const at = (time: string) => `${day} ${time}`;
const instant = (time: string) => `${day}T${time}:00Z`;

/**
 * An RFO at the bench's Rome from 17:00 to 20:00, with public and private slots and two arrivals and two departures an hour, its
 * bookings open since an hour ago, and one public departure at 18:00: a draft, as its page makes it.
 */
async function eventWithCapacity(request: APIRequestContext): Promise<number> {
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfo',
      publicSlots: true,
      privateSlots: true,
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
      startsAtUtc: instant('17:00'),
      endsAtUtc: instant('20:00'),
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
      maxArrivalsPerHour: 2,
      maxDeparturesPerHour: 2,
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(airport.status(), await airport.text()).toBe(201);

  const { rome, milan } = benchAirports;
  const loaded = await request.post(`/api/events/events/${id}/slots/load`, {
    headers: asTheClientDoes,
    data: {
      text: [
        header,
        ['XEE701', '', 'A320', rome, at('18:00'), milan, at('19:10'), 'B12', '', ''].join('\t'),
      ].join('\n'),
      mode: 'Add',
    },
  });
  expect(loaded.status(), await loaded.text()).toBe(200);

  return id;
}

/**
 * What a run of this spec left, taken back the way the product allows: the pilot withdraws their bookings of its events — an event
 * with bookings is not deleted —, the coordinator deletes the events and revokes the tokens of the spec.
 */
async function removeOurRows(coordinator: APIRequestContext, pilot: APIRequestContext): Promise<void> {
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

  const tokens = await coordinator.get('/api/me/tokens?pageSize=100');
  expect(tokens.status(), await tokens.text()).toBe(200);
  for (const token of (
    (await tokens.json()) as { items: readonly { id: number; name: string; revokedAt: string | null }[] }
  ).items) {
    if (token.name.startsWith(stem) && token.revokedAt === null) {
      await coordinator.post(`/api/me/tokens/${token.id}/revoke`, { headers: asTheClientDoes });
    }
  }
}

/** A flight of the export, by the names the gate manager reads. */
interface ExportedFlight {
  slot_id: number;
  callsign: string | null;
  booked_by: number | null;
  aircraft_icao: string | null;
  gate: string | null;
  eobt: string | null;
  eat: string | null;
  origin_icao: string | null;
  destination_icao: string | null;
  paired_slot_id: number | null;
}

test('the staff generate the private slots of an RFO, a pilot books an arrival with its departure, and the export pairs them', async ({
  browser,
  afterwards,
}) => {
  // Two people's pages, a generation and an export: more than the half minute a spec has by default.
  test.setTimeout(90_000);

  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  await removeOurRows(coordinator.request, pilot.request);
  const programs: APIRequestContext[] = [];

  afterwards(async () => {
    for (const program of programs) {
      await program.dispose();
    }
    await removeOurRows(coordinator.request, pilot.request);
    await Promise.all([coordinator.close(), pilot.close()]);
  });

  const id = await eventWithCapacity(coordinator.request);
  const { rome, milan, bari } = benchAirports;

  // ---------------------------------------------------------------- the coordinator generates the private slots
  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await staff.goto(`/staff/events/${id}?tab=slots`);
  await expect(staff.getByRole('tab', { name: events.events.tabs.slots })).toHaveAttribute(
    'aria-selected',
    'true',
  );

  await staff.getByRole('button', { name: events.slots.generate.trigger }).click();
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/slots/generate`, async () => {
    await staff
      .getByRole('alertdialog', { name: events.slots.generate.title })
      .getByRole('button', { name: events.slots.generate.confirm })
      .click();
  });

  // Three hours of two arrivals and two departures, less the step the public departure at 18:00 takes: eleven.
  await expect(
    staff.getByText(
      events.slots.generate.done
        .replace('{{generated}}', '11')
        .replace('{{removed}}', '0')
        .replace('{{kept}}', '0'),
    ),
  ).toBeVisible();
  await expect(
    staff.getByRole('row').filter({ hasText: events.slots.options.kind.Private }).first(),
  ).toBeVisible();

  const published = await coordinator.request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: '0001-01-01T00:00:00' },
  });
  expect(published.status(), await published.text()).toBe(200);

  // ---------------------------------------------------------------- the pilot books an arrival with its departure
  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await page.goto(`/events/${slug}`);
  await expect(page.getByRole('heading', { level: 1, name: title.en })).toBeVisible();

  const privateSlots = page.getByRole('region', { name: events.private.title });
  const arrivals = privateSlots.getByRole('table', { name: events.public.arrivals });
  await expect(arrivals.getByRole('row').filter({ hasText: '18:00–19:00' })).toContainText(
    events.private.freeOf.replace('{{free}}', '2').replace('{{of}}', '2'),
  );
  await arrivals.getByRole('button', { name: '18:00–19:00' }).click();

  const hour = page.getByRole('dialog', {
    name: events.private.dialog.arrivals.replace('{{airport}}', rome),
    exact: false,
  });
  // The departure first: nothing written yet is carried over.
  await hour.getByRole('switch', { name: events.private.dialog.withDeparture }).click();

  const fields = events.private.arrival.fields;
  await hour.getByLabel(fields.callsign, { exact: true }).first().fill('XEP801');
  await hour.getByLabel(fields.aircraftIcao, { exact: true }).fill('A320');
  await hour.getByLabel(fields.otherIcao, { exact: true }).fill(milan);
  await hour.getByLabel(fields.otherTimeUtc, { exact: true }).fill(`${day}T16:50`);

  const departure = hour.getByRole('group', { name: fields.departure });
  await departure.getByLabel(fields['departure.callsign'], { exact: true }).fill('XEP802');
  await departure.getByLabel(fields['departure.otherIcao'], { exact: true }).fill(bari);
  await departure.getByLabel(fields['departure.otherTimeUtc'], { exact: true }).fill(`${day}T19:40`);

  await whileWaitingFor(page, 'POST', '/api/events/mine/bookings/private', async () => {
    await hour.getByRole('button', { name: events.private.dialog.book, exact: true }).click();
  });
  await expect(
    hour.getByText(
      events.private.dialog.bookedWithDeparture
        .replace('{{callsign}}', 'XEP801')
        .replace('{{departure}}', 'XEP802'),
    ),
  ).toBeVisible();

  // In their bookings, each says the other.
  await page.goto('/events/mine');
  const theEvent = page
    .getByRole('region', { name: events.mine.upcoming })
    .getByRole('region', { name: title.en });
  // Each booking by its own callsign: the other names it too.
  const booking = (callsign: string) =>
    theEvent.getByRole('listitem').filter({ has: page.getByText(callsign, { exact: true }) });
  await expect(booking('XEP801')).toContainText(
    events.mine.pairedDeparture.replace('{{callsign}}', 'XEP802'),
  );
  await expect(booking('XEP802')).toContainText(events.mine.pairedArrival.replace('{{callsign}}', 'XEP801'));

  // ---------------------------------------------------------------- the gate manager reads the pair in the export
  const made = await coordinator.request.post('/api/me/tokens', {
    headers: asTheClientDoes,
    data: { name: tokenName, audience: 'events.bookings', days: 1 },
  });
  expect(made.status(), await made.text()).toBe(200);
  const token = ((await made.json()) as { token: string }).token;

  const program = await playwrightRequest.newContext({
    baseURL: benchUrl,
    extraHTTPHeaders: { Authorization: `Bearer ${token}`, 'Hub-Bookings-Contract': '1' },
  });
  programs.push(program);

  const exported = await program.get(`/api/events/${slug}/bookings/export`);
  expect(exported.status(), await exported.text()).toBe(200);
  const flights = (await exported.json()) as ExportedFlight[];

  const landing = flights.find((flight) => flight.callsign === 'XEP801');
  const leaving = flights.find((flight) => flight.callsign === 'XEP802');
  expect(landing).toMatchObject({
    origin_icao: milan,
    destination_icao: rome,
    eobt: instant('16:50'),
    eat: instant('18:00'),
    aircraft_icao: 'A320',
    gate: null,
  });
  expect(leaving).toMatchObject({
    origin_icao: rome,
    destination_icao: bari,
    eobt: instant('18:30'),
    eat: instant('19:40'),
    aircraft_icao: 'A320',
    gate: null,
  });
  // The same gate to give the two: each names the other's slot.
  expect(landing?.paired_slot_id).toBe(leaving?.slot_id);
  expect(leaving?.paired_slot_id).toBe(landing?.slot_id);
  expect(landing?.booked_by).toBe(leaving?.booked_by);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { slots: string } };
    slots: {
      options: { kind: { Private: string } };
      generate: { trigger: string; title: string; confirm: string; done: string };
    };
    public: { arrivals: string };
    private: {
      title: string;
      freeOf: string;
      dialog: { arrivals: string; withDeparture: string; book: string; bookedWithDeparture: string };
      arrival: {
        fields: {
          callsign: string;
          aircraftIcao: string;
          otherIcao: string;
          otherTimeUtc: string;
          departure: string;
          'departure.callsign': string;
          'departure.otherIcao': string;
          'departure.otherTimeUtc': string;
        };
      };
    };
    mine: { upcoming: string; pairedDeparture: string; pairedArrival: string };
  };
}
