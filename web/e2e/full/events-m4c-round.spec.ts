import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchUrl, mailFor, readInEnglish, test, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The whole round of M4c (E17, the «done when» of the phase), through the real screens and the real server: the coordinator of the events
 * (`?as=events`) publishes an event in person with a question and writes one of its parallel activities in the generated form of the tab
 * «Activities»; the bench's pilot (`?as=pilot`, a member with a mailbox of Mailpit) registers from the page of the event, takes a seat in a
 * turn, finds it on `/events/mine` and gives it back there, takes it again, and is told in Mailpit with the turn in the mail; the
 * coordinator reads the seat in the tab «Seats» and the seats taken in the tab «Activities», and duplicates the event onto a week later:
 * the copy has the question and the activity, and no registration. The rules — the lock on the last seat, the overlaps, the retention —
 * are `EventsActivitiesTests`'s.
 *
 * The bench survives between runs: the pilot withdraws from this spec's events — their seats go with the registration — and the
 * coordinator deletes them after the test (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e17';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const copySlug = `${stem}-copy-${stamp}`;
const title = { en: `Bench round in person ${stamp}`, it: `Il giro in presenza del banco ${stamp}` };
const venue = { en: `The bench hangar ${stamp}`, it: `L'hangar del banco ${stamp}` };
const activity = { en: `Simulator ${stamp}`, it: `Simulatore ${stamp}` };
const dinner = { en: 'Are you coming to dinner?', it: 'Vieni a cena?' };
const pilotAddress = 'bench-pilot@bench.test';
const blank = '0001-01-01T00:00:00';

/** The day of the event, twenty days from now, and the day of its copy a week later. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const later = new Date(Date.now() + 27 * 24 * 3600 * 1000).toISOString().slice(0, 10);

/** A context of its own, reading in English, signed in as one of the bench's people. */
async function contextAs(browser: Browser, who: 'events' | 'pilot'): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** An event in person with its venue and a yes or no, published: what E16's round already proves on screen. */
async function publishedEvent(request: APIRequestContext): Promise<number> {
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfe',
      publicSlots: false,
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
      startsAtUtc: `${day}T09:00:00Z`,
      endsAtUtc: `${day}T18:00:00Z`,
      visibility: 'Public',
      rowVersion: blank,
      inPerson: true,
      venue,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const question = await request.post('/api/events/questions', {
    headers: asTheClientDoes,
    data: {
      eventId: id,
      ordinal: 1,
      type: 'YesNo',
      label: dinner,
      choices: [],
      required: true,
      rowVersion: blank,
    },
  });
  expect(question.status(), await question.text()).toBe(201);

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

/** What a run of this spec left: the pilot withdraws from its events — and their seats go with it —, then the events go. */
async function removeOurEvents(coordinator: APIRequestContext, pilot: APIRequestContext): Promise<void> {
  const mine = await pilot.get('/api/events/mine/registrations');
  expect(mine.status(), await mine.text()).toBe(200);
  for (const registration of (await mine.json()) as readonly { eventId: number; eventSlug: string }[]) {
    if (registration.eventSlug.startsWith(stem)) {
      const withdrawn = await pilot.delete(`/api/events/mine/registrations/${registration.eventId}`, {
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

test('the round of an event in person: an activity, a seat taken, given back and taken again, and the event duplicated', async ({
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

  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the coordinator writes an activity in the generated form
  await staff.goto(`/staff/events/${id}?tab=activities`);
  await expect(staff.getByText(events.activities.empty)).toBeVisible();
  await staff.getByRole('link', { name: events.activities.create }).click();
  await expect(staff.getByRole('heading', { name: events.activities.create })).toBeVisible();

  const form = staff.locator('form');
  await writeInBothLanguages(form, events.activities.fields.name, 'name', activity);
  await form.locator('[id="fromUtc"]').fill(`${day}T10:00`);
  await form.locator('[id="toUtc"]').fill(`${day}T12:00`);
  await form.locator('[id="slotMinutes"]').fill('30');
  await form.locator('[id="seatsPerSlot"]').fill('2');
  await whileWaitingFor(staff, 'POST', '/api/events/activities', async () => {
    await form.getByRole('button', { name: 'Save', exact: true }).click();
  });

  // Back on the tab: the activity with its four turns and nobody in them yet.
  await expect(staff).toHaveURL(/tab=activities/);
  await expect(staff.getByRole('row').filter({ hasText: activity.en })).toBeVisible();

  // ---------------------------------------------------------------- the pilot registers and takes a seat
  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/events/${slug}`);
  const registration = page.getByRole('region', { name: events.public.registration.title });
  const activities = registration.getByRole('group', { name: events.public.activities.title });

  // Before registering: the turns with their free seats, and nothing to press.
  const firstTurn = () =>
    activities.getByRole('table', { name: activity.en }).getByRole('row').filter({ hasText: '10:00' });
  await expect(firstTurn()).toContainText(events.public.activities.seatsFree.replace('{{seats}}', '2'));
  await expect(activities.getByText(events.public.activities.registerFirst)).toBeVisible();
  await expect(firstTurn().getByRole('button')).toHaveCount(0);

  await registration.getByLabel(dinner.en).click();
  await whileWaitingFor(page, 'PUT', '/api/events/mine/registrations/', async () => {
    await registration.getByRole('button', { name: events.public.registration.register }).click();
  });
  await expect(registration.getByText(events.public.registration.registered)).toBeVisible();

  await whileWaitingFor(page, 'POST', '/api/events/mine/seats', async () => {
    await firstTurn().getByRole('button', { name: events.public.activities.take }).click();
  });
  await expect(firstTurn()).toContainText(events.public.activities.yours);

  // ---------------------------------------------------------------- on /events/mine: the turn, given back
  await page.goto('/events/mine');
  const mine = page
    .getByRole('region', { name: events.mine.registrations })
    .getByRole('listitem', { name: title.en });
  await expect(mine).toContainText(events.mine.seats);
  await expect(mine).toContainText(activity.en);
  await whileWaitingFor(page, 'DELETE', '/api/events/mine/seats/', async () => {
    await mine.getByRole('button', { name: events.public.activities.giveBack }).click();
  });
  await expect(mine).not.toContainText(activity.en);

  // ---------------------------------------------------------------- and taken again from the page of the event
  await page.goto(`/events/${slug}`);
  await expect(firstTurn()).toContainText(events.public.activities.seatsFree.replace('{{seats}}', '2'));
  await whileWaitingFor(page, 'POST', '/api/events/mine/seats', async () => {
    await firstTurn().getByRole('button', { name: events.public.activities.take }).click();
  });
  await expect(firstTurn()).toContainText(events.public.activities.yours);

  // ---------------------------------------------------------------- the coordinator reads the seat
  await staff.goto(`/staff/events/${id}?tab=seats`);
  await expect(staff.getByRole('tab', { name: events.events.tabs.seats })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(staff.getByRole('row').filter({ hasText: 'Bench Pilot (999002)' })).toContainText(activity.en);

  await staff.goto(`/staff/events/${id}?tab=activities`);
  await expect(staff.getByRole('row').filter({ hasText: activity.en })).toBeVisible();
  const taken = await coordinator.request.get(`/api/events/activities?filter[eventId]=${id}`);
  expect(((await taken.json()) as { items: readonly { seatsTaken: number; turns: number }[] }).items).toEqual(
    [expect.objectContaining({ seatsTaken: 1, turns: 4 })],
  );

  // ---------------------------------------------------------------- duplicated onto a week later: the question and the activity, nobody
  await staff.goto(`/staff/events/${id}`);
  await staff.getByRole('link', { name: events.events.actions.duplicate }).click();
  await expect(staff.getByRole('heading', { name: events.duplicate.title })).toBeVisible();
  await staff.locator('[id="startsAtUtc"]').fill(`${later}T09:00`);
  await staff.locator('[id="slug"]').fill(copySlug);
  await whileWaitingFor(staff, 'POST', `/api/events/events/${id}/duplicate`, async () => {
    await staff.getByRole('button', { name: events.duplicate.submit, exact: true }).click();
  });

  await expect(staff).toHaveURL(/\/staff\/events\/\d+$/);
  const copyId = Number(new URL(staff.url()).pathname.split('/').pop());
  expect(copyId).not.toBe(id);
  await staff.getByRole('tab', { name: events.events.tabs.questions }).click();
  await expect(staff.getByRole('row').filter({ hasText: dinner.en })).toBeVisible();
  await staff.getByRole('tab', { name: events.events.tabs.activities }).click();
  await expect(staff.getByRole('row').filter({ hasText: activity.en })).toBeVisible();
  await staff.getByRole('tab', { name: events.events.tabs.seats }).click();
  await expect(staff.getByText(events.seats.empty)).toBeVisible();
  const copied = await coordinator.request.get(`/api/events/activities?filter[eventId]=${copyId}`);
  expect(((await copied.json()) as { items: readonly { fromUtc: string }[] }).items[0]?.fromUtc).toMatch(
    new RegExp(`^${later}T10:00:00`),
  );

  // The pilot is told, in their mailbox and in their language, with the turn they hold: the run's stamp is in the title.
  await expect
    .poll(async () => (await mailFor(pilot.request, pilotAddress, stamp))?.Text ?? '', {
      timeout: 150_000,
      intervals: [5_000],
    })
    .toContain(`${activity.it}: ${day} 10:00`);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: {
      tabs: { questions: string; activities: string; seats: string };
      actions: { duplicate: string };
    };
    duplicate: { title: string; submit: string };
    activities: { create: string; empty: string; fields: { name: string } };
    seats: { empty: string };
    public: {
      registration: { title: string; register: string; registered: string };
      activities: {
        title: string;
        registerFirst: string;
        seatsFree: string;
        take: string;
        yours: string;
        giveBack: string;
      };
    };
    mine: { registrations: string; seats: string };
  };
}
