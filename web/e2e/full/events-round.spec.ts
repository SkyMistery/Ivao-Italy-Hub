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
 * The full round of M4a (E8b, design M4 §15), through the real screens and the real server: the coordinator of the events
 * (`?as=events`) pastes the public slots of an RFO — a rotation and a flight alone —, generates its private slots from the capacity of
 * its airport and publishes it; the bench's pilot (`?as=pilot`) books the whole rotation and a private arrival on its page, and
 * withdraws a leg in `/events/mine`; the gate manager's program reads the export with a token and nothing else; and the coordinator
 * duplicates the RFO onto a week later — a draft with the public slots copied and the private ones generated around them. And «done
 * when» of E8b: last month's RFE, ended, is duplicated onto a new date.
 *
 * The bench survives between runs: the bookings of this spec's events are withdrawn by the pilot, the events deleted and the tokens
 * revoked after each test (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e8b';
const stamp = Date.now().toString(36);
const header =
  'callsign\tflight_number\taircraft_types\tdeparture_icao\toff_block_utc\tarrival_icao\ton_block_utc\tstand\trotation\tleg';
const bench = { pilot: 999002 };

/** A context of its own, reading in English, signed in as one of the bench's people. */
async function contextAs(browser: Browser, who: 'events' | 'pilot'): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** A day some days from today, as the table and the API write it: `2026-10-31`, in UTC. */
function dayIn(days: number): string {
  return new Date(Date.now() + days * 24 * 3600 * 1000).toISOString().slice(0, 10);
}

/**
 * An event of the spec from 17:00 to 22:00 of a day, with one airport and its capacity, written by the API as its page writes it: a
 * draft, an RFO with public and private slots or an RFE with public ones. Its bookings open an hour ago, or a week before it starts
 * when it started already.
 */
async function eventWithAirport(
  request: APIRequestContext,
  slug: string,
  title: { en: string; it: string },
  kind: 'rfo' | 'rfe',
  day: string,
  capacity: { arrivals: number | null; departures: number | null },
): Promise<number> {
  const starts = new Date(`${day}T17:00:00Z`);
  const opens = new Date(Math.min(Date.now() - 3600 * 1000, starts.getTime() - 7 * 24 * 3600 * 1000));
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind,
      publicSlots: true,
      privateSlots: kind === 'rfo',
      wholeDivision: false,
      organizer: 'Division',
      externalUrl: null,
      title,
      slug,
      summary: title,
      body: null,
      bannerMediaId: null,
      visibleFromUtc: null,
      bookingOpensAtUtc: opens.toISOString(),
      startsAtUtc: starts.toISOString(),
      endsAtUtc: `${day}T22:00:00Z`,
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
      maxArrivalsPerHour: capacity.arrivals,
      maxDeparturesPerHour: capacity.departures,
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(airport.status(), await airport.text()).toBe(201);

  return id;
}

/**
 * What a run of this spec left, taken back the way the product allows: the pilot withdraws their bookings of its events — an event
 * with bookings is not deleted —, the coordinator deletes the events, copies included, and revokes the tokens of the spec.
 */
async function removeOurRows(coordinator: APIRequestContext, pilot: APIRequestContext): Promise<void> {
  const mine = await pilot.get('/api/events/mine/bookings');
  expect(mine.status(), await mine.text()).toBe(200);
  for (const booking of (await mine.json()) as readonly {
    id: number;
    eventSlug: string;
    withdrawable: boolean;
  }[]) {
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
    // An event that started more than half an hour ago, or has a report of support, is kept (E14a; Carmine's answer on reading 13 on
    // #289): it stays on the bench, which e2e:full starts empty.
    if (deleted.status() === 400 && /eventStarted|eventHasReports/.test(await deleted.text())) {
      continue;
    }
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

/** The slots of an event, public and private, as the staff's list reads them: their kind, callsign and off block. */
async function slotsOf(
  request: APIRequestContext,
  eventId: number,
): Promise<{ callsign: string | null; offBlockUtc: string | null; kind: string }[]> {
  const list = await request.get(`/api/events/slots?filter[eventId]=${eventId}&pageSize=100`);
  expect(list.status(), await list.text()).toBe(200);
  return (
    (await list.json()) as { items: { callsign: string | null; offBlockUtc: string | null; kind: string }[] }
  ).items;
}

/** A flight of the export, by the names the gate manager reads. */
interface ExportedFlight {
  slot_id: number;
  callsign: string | null;
  booked_by: number | null;
  aircraft_icao: string | null;
  gate: string | null;
  origin_icao: string | null;
  destination_icao: string | null;
}

test('the full round of M4a: an RFO pasted, generated and published, booked and withdrawn, exported, and duplicated', async ({
  browser,
  afterwards,
}) => {
  // Two people's pages, a table, a generation, an export and a copy: much more than the half minute a spec has by default.
  test.setTimeout(180_000);

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

  const { rome, milan, bari } = benchAirports;
  const day = dayIn(20);
  const later = dayIn(27);
  const slug = `${stem}-rfo-${stamp}`;
  const copySlug = `${stem}-copy-${stamp}`;
  const title = { en: `Bench round ${stamp}`, it: `Il giro del banco ${stamp}` };
  const id = await eventWithAirport(coordinator.request, slug, title, 'rfo', day, {
    arrivals: 2,
    departures: 2,
  });

  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the slots pasted from a spreadsheet
  await staff.goto(`/staff/events/${id}?tab=slots`);
  await staff.getByRole('link', { name: events.slots.load.open }).click();
  await expect(staff.getByRole('heading', { name: events.slots.load.title })).toBeVisible();
  const rows = [
    ['XEE811', 'XE811', 'A320/A20N', rome, `${day} 18:00`, milan, `${day} 19:10`, 'B12', 'R1', ''],
    ['XEE812', '', 'A320/A20N', milan, `${day} 19:40`, rome, `${day} 20:50`, '', 'R1', ''],
    ['XEE813', '', 'E55P', bari, `${day} 18:30`, rome, `${day} 19:40`, '', '', ''],
  ];
  await staff.locator('[id="text"]').fill([header, ...rows.map((row) => row.join('\t'))].join('\n'));
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/slots/load`, async () => {
    await staff.getByRole('button', { name: events.slots.load.submit, exact: true }).click();
  });
  await expect(staff).toHaveURL(/tab=slots/);

  // ---------------------------------------------------------------- the private slots generated from the capacity
  // Five hours of two arrivals and two departures at Rome, less the three times the public flights hold there: seventeen.
  await staff.getByRole('button', { name: events.slots.generate.trigger }).click();
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/slots/generate`, async () => {
    await staff
      .getByRole('alertdialog', { name: events.slots.generate.title })
      .getByRole('button', { name: events.slots.generate.confirm })
      .click();
  });
  await expect(
    staff.getByText(
      events.slots.generate.done
        .replace('{{generated}}', '17')
        .replace('{{removed}}', '0')
        .replace('{{kept}}', '0'),
    ),
  ).toBeVisible();

  // ---------------------------------------------------------------- published, asked once more
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/publish`, async () => {
    await staff.getByRole('button', { name: events.events.actions.publish }).click();
    await staff
      .getByRole('alertdialog', { name: events.events.publish.confirmTitle })
      .getByRole('button', { name: events.events.actions.publish })
      .click();
  });
  await expect(staff.getByText(events.events.options.state.BookingOpen, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- the pilot books the rotation and a private arrival
  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await page.goto(`/events/${slug}`);
  await expect(page.getByRole('heading', { level: 1, name: title.en })).toBeVisible();

  await page.getByRole('button', { name: 'XEE811' }).click();
  const leg = page.getByRole('dialog', { name: 'XEE811 · XE811' });
  await leg
    .getByRole('radiogroup', { name: events.public.booking.aircraft })
    .getByRole('radio', { name: /A20N/ })
    .click();
  await whileWaitingFor(page, 'POST', '/api/events/mine/bookings/rotation', async () => {
    await leg.getByRole('button', { name: events.public.booking.bookRotation }).click();
  });
  await expect(leg.getByRole('region', { name: events.public.booking.rotationResult })).toContainText(
    events.public.booking.rotationDone.replace('{{booked}}', '2').replace('{{of}}', '2'),
  );
  await page.keyboard.press('Escape');

  const arrivals = page
    .getByRole('region', { name: events.private.title })
    .getByRole('table', { name: events.public.arrivals });
  await arrivals.getByRole('button', { name: '17:00–18:00' }).click();
  const hour = page.getByRole('dialog', {
    name: events.private.dialog.arrivals.replace('{{airport}}', rome),
    exact: false,
  });
  const fields = events.private.arrival.fields;
  await hour.getByLabel(fields.callsign, { exact: true }).first().fill('XEP901');
  await hour.getByLabel(fields.aircraftIcao, { exact: true }).fill('A320');
  await hour.getByLabel(fields.otherIcao, { exact: true }).fill(milan);
  await hour.getByLabel(fields.otherTimeUtc, { exact: true }).fill(`${day}T16:00`);
  await whileWaitingFor(page, 'POST', '/api/events/mine/bookings/private', async () => {
    await hour.getByRole('button', { name: events.private.dialog.book, exact: true }).click();
  });
  await expect(hour.getByText(events.private.dialog.booked.replace('{{callsign}}', 'XEP901'))).toBeVisible();

  // ---------------------------------------------------------------- one withdrawn, in their bookings
  await page.goto('/events/mine');
  const theEvent = page
    .getByRole('region', { name: events.mine.upcoming })
    .getByRole('region', { name: title.en });
  const booking = (callsign: string) =>
    theEvent.getByRole('listitem').filter({ has: page.getByText(callsign, { exact: true }) });
  for (const callsign of ['XEE811', 'XEE812', 'XEP901']) {
    await expect(booking(callsign)).toBeVisible();
  }
  await booking('XEE812').getByRole('button', { name: events.mine.withdraw }).click();
  await whileWaitingFor(page, 'DELETE', '/api/events/mine/bookings/', async () => {
    await page
      .getByRole('alertdialog', { name: events.mine.withdrawTitle.replace('{{callsign}}', 'XEE812') })
      .getByRole('button', { name: events.mine.withdraw })
      .click();
  });
  await expect(booking('XEE812')).toHaveCount(0);
  await expect(booking('XEE811')).toBeVisible();

  // ---------------------------------------------------------------- the gate manager reads the export with a token
  const made = await coordinator.request.post('/api/me/tokens', {
    headers: asTheClientDoes,
    data: { name: `${stem} gate ${stamp}`, audience: 'events.bookings', days: 1 },
  });
  expect(made.status(), await made.text()).toBe(200);
  const program = await playwrightRequest.newContext({
    baseURL: benchUrl,
    extraHTTPHeaders: {
      Authorization: `Bearer ${((await made.json()) as { token: string }).token}`,
      'Hub-Bookings-Contract': '1',
    },
  });
  programs.push(program);

  const exported = await program.get(`/api/events/${slug}/bookings/export`);
  expect(exported.status(), await exported.text()).toBe(200);
  const flights = (await exported.json()) as ExportedFlight[];
  const flight = (callsign: string) => flights.find((candidate) => candidate.callsign === callsign);
  expect(flight('XEE811')).toMatchObject({ booked_by: bench.pilot, aircraft_icao: 'A20N', gate: 'B12' });
  expect(flight('XEE812')).toMatchObject({ booked_by: null, aircraft_icao: null });
  expect(flight('XEE813')).toMatchObject({ booked_by: null });
  expect(flight('XEP901')).toMatchObject({
    booked_by: bench.pilot,
    aircraft_icao: 'A320',
    origin_icao: milan,
    destination_icao: rome,
    gate: null,
  });

  // ---------------------------------------------------------------- duplicated onto a week later
  await staff.goto(`/staff/events/${id}`);
  await staff.getByRole('link', { name: events.events.actions.duplicate }).click();
  await expect(staff.getByRole('heading', { name: events.duplicate.title })).toBeVisible();
  // An RFO has private slots: the page says how they are made again.
  await expect(staff.getByText(events.duplicate.privateSlots)).toBeVisible();
  await staff.locator('[id="startsAtUtc"]').fill(`${later}T17:00`);
  await staff.locator('[id="slug"]').fill(copySlug);
  await expect(staff.locator('[id="publicSlots"]')).toBeChecked();
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/duplicate`, async () => {
    await staff.getByRole('button', { name: events.duplicate.submit, exact: true }).click();
  });

  // The new draft's page: the same event a week later, with the public slots copied and the private ones generated around them.
  await expect(staff).toHaveURL(/\/staff\/events\/\d+$/);
  const copyId = Number(new URL(staff.url()).pathname.split('/').pop());
  expect(copyId).not.toBe(id);
  await expect(staff.getByRole('heading', { name: title.en })).toBeVisible();
  await expect(staff.getByText(events.events.options.state.Draft, { exact: true })).toBeVisible();
  await staff.getByRole('tab', { name: events.events.tabs.slots }).click();
  for (const callsign of ['XEE811', 'XEE812', 'XEE813']) {
    await expect(staff.getByRole('row').filter({ hasText: callsign })).toBeVisible();
  }
  await expect(
    staff.getByRole('row').filter({ hasText: events.slots.options.kind.Private }).first(),
  ).toBeVisible();

  const copied = await slotsOf(coordinator.request, copyId);
  expect(copied.find((slot) => slot.callsign === 'XEE811')?.offBlockUtc).toMatch(
    new RegExp(`^${later}T18:00:00`),
  );
  expect(copied.filter((slot) => slot.kind === 'Private')).toHaveLength(17);
});

test('last month’s RFE, ended, is duplicated onto a new date', async ({ browser, afterwards }) => {
  test.setTimeout(90_000);

  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  await removeOurRows(coordinator.request, pilot.request);

  afterwards(async () => {
    await removeOurRows(coordinator.request, pilot.request);
    await Promise.all([coordinator.close(), pilot.close()]);
  });

  const { rome, milan, bari } = benchAirports;
  const monthAgo = dayIn(-30);
  const day = dayIn(20);
  const slug = `${stem}-rfe-${stamp}`;
  const copySlug = `${stem}-rfe-next-${stamp}`;
  const title = { en: `Bench RFE ${stamp}`, it: `L'RFE del banco ${stamp}` };

  // An RFE of a month ago, its slots loaded and published: ended, the staff's to read.
  const id = await eventWithAirport(coordinator.request, slug, title, 'rfe', monthAgo, {
    arrivals: null,
    departures: null,
  });
  const loaded = await coordinator.request.post(`/api/events/events/${id}/slots/load`, {
    headers: asTheClientDoes,
    data: {
      text: [
        header,
        ['XEE821', '', 'A320', rome, `${monthAgo} 18:00`, milan, `${monthAgo} 19:10`, '', '', ''].join('\t'),
        ['XEE822', '', 'E55P', bari, `${monthAgo} 18:30`, rome, `${monthAgo} 19:40`, '', '', ''].join('\t'),
      ].join('\n'),
      mode: 'Add',
    },
  });
  expect(loaded.status(), await loaded.text()).toBe(200);
  const published = await coordinator.request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: '0001-01-01T00:00:00' },
  });
  expect(published.status(), await published.text()).toBe(200);

  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await staff.goto(`/staff/events/${id}`);
  await expect(staff.getByText(events.events.options.state.Ended, { exact: true })).toBeVisible();

  // Duplicated with its slots onto a day twenty days from now: no private slots to speak of on an RFE.
  await staff.getByRole('link', { name: events.events.actions.duplicate }).click();
  await expect(staff.getByRole('heading', { name: events.duplicate.title })).toBeVisible();
  await expect(staff.getByText(events.duplicate.privateSlots)).toHaveCount(0);
  await staff.locator('[id="startsAtUtc"]').fill(`${day}T17:00`);
  await staff.locator('[id="slug"]').fill(copySlug);
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/duplicate`, async () => {
    await staff.getByRole('button', { name: events.duplicate.submit, exact: true }).click();
  });

  await expect(staff).toHaveURL(/\/staff\/events\/\d+$/);
  const copyId = Number(new URL(staff.url()).pathname.split('/').pop());
  await expect(staff.getByText(events.events.options.state.Draft, { exact: true })).toBeVisible();
  await expect(staff.locator('[id="slug"]')).toHaveValue(copySlug);

  const copied = await slotsOf(coordinator.request, copyId);
  expect(copied.map((slot) => `${slot.callsign} ${slot.offBlockUtc?.slice(0, 16)}`).sort()).toEqual([
    `XEE821 ${day}T18:00`,
    `XEE822 ${day}T18:30`,
  ]);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: {
      tabs: { slots: string };
      actions: { publish: string; duplicate: string };
      publish: { confirmTitle: string };
      options: { state: { Draft: string; BookingOpen: string; Ended: string } };
    };
    duplicate: { title: string; privateSlots: string; submit: string };
    slots: {
      options: { kind: { Private: string } };
      load: { open: string; title: string; submit: string };
      generate: { trigger: string; title: string; confirm: string; done: string };
    };
    public: {
      arrivals: string;
      booking: { aircraft: string; bookRotation: string; rotationResult: string; rotationDone: string };
    };
    private: {
      title: string;
      dialog: { arrivals: string; book: string; booked: string };
      arrival: {
        fields: { callsign: string; aircraftIcao: string; otherIcao: string; otherTimeUtc: string };
      };
    };
    mine: { upcoming: string; withdraw: string; withdrawTitle: string };
  };
}
