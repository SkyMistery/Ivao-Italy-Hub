import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchUrl, fieldOf, readInEnglish, signIn, test, whileWaitingFor } from './bench';

/**
 * The "done when" of E11a (M4b), through the real screens and the real server: the coordinator of the events (`?as=events`) publishes an
 * event with an ATC roster; the web master gives the bench's trainer (`?as=trainer`) the ATC of the events with a grant — as the division
 * gives it to the ATC operations, without the events themselves —, and the trainer, from the section «Events ATC», opens two positions of
 * the event; the bench's pilot (`?as=pilot`, AS3) gives their availability from the page of the event; and the trainer finds them among
 * who is available on the tower. The rules — the FIRs, the least rating, the close — are `EventsAtcTests`'s.
 *
 * The bench survives between runs: the pilot withdraws, the web master takes the grants back and the coordinator deletes this spec's events
 * after the test (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e11a';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench ATC ${stamp}`, it: `L'ATC del banco ${stamp}` };
const trainerVid = 999004;
const blank = '0001-01-01T00:00:00';

/** Two positions of the bench's division, from the reference data the bench is born with. */
const tower = 'LIRF_TWR';
const ground = 'LIRF_GND';

/** A context of its own, reading in English, signed in as one of the bench's people — or as its web master. */
async function contextAs(
  browser: Browser,
  who: 'events' | 'pilot' | 'trainer' | 'master',
): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  if (who === 'master') {
    await signIn(context);
  } else {
    const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
    expect(signedIn.status(), await signedIn.text()).toBe(200);
  }

  return context;
}

/** The day of the event, twenty days from now: its availabilities close three days before it, by the settings' default. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);

/** An event with an ATC roster and nothing else, published. */
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
      startsAtUtc: `${day}T17:00:00Z`,
      endsAtUtc: `${day}T21:00:00Z`,
      visibility: 'Public',
      rowVersion: blank,
      hasRoster: true,
      shiftMinutes: null,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

/** The ATC of the events to the trainer, on the events department, as a grant to their VID: what the AOD holds from its position. */
async function grantTheAtc(master: APIRequestContext): Promise<void> {
  for (const permission of ['EventAtc.View', 'EventAtc.Edit']) {
    const written = await master.post('/api/admin/grants', {
      headers: asTheClientDoes,
      data: {
        vid: trainerVid,
        kind: 'Permission',
        value: permission,
        department: 'ED',
        effect: 'Grant',
        expiresAt: null,
        reason: stem,
        rowVersion: blank,
      },
    });
    expect(written.status(), await written.text()).toBe(201);
  }
}

/**
 * What a run of this spec left: the pilot withdraws their availabilities of its events — an event with availabilities is not deleted —,
 * the coordinator deletes the events with their positions, and the web master takes the grants back.
 */
async function removeOurRows(
  coordinator: APIRequestContext,
  pilot: APIRequestContext,
  master: APIRequestContext,
): Promise<void> {
  const mine = await pilot.get('/api/events/mine/availability');
  expect(mine.status(), await mine.text()).toBe(200);
  for (const availability of (await mine.json()) as readonly { eventId: number; eventSlug: string }[]) {
    if (availability.eventSlug.startsWith(stem)) {
      const withdrawn = await pilot.delete(`/api/events/mine/availability/${availability.eventId}`, {
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

  const grants = await master.get(`/api/admin/grants?filter[vid]=${trainerVid}&q=${stem}&pageSize=100`);
  expect(grants.status(), await grants.text()).toBe(200);
  for (const grant of ((await grants.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await master.delete(`/api/admin/grants/${grant.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('the ATC operations open two positions of an event, and a pilot gives their availability', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  const master = await contextAs(browser, 'master');
  await removeOurRows(coordinator.request, pilot.request, master.request);

  // A grant written signs its holder out: the trainer signs in once it is there.
  await grantTheAtc(master.request);
  const trainer = await contextAs(browser, 'trainer');

  afterwards(async () => {
    await removeOurRows(coordinator.request, pilot.request, master.request);
    await Promise.all([coordinator.close(), pilot.close(), master.close(), trainer.close()]);
  });

  const id = await publishedEvent(coordinator.request);

  // ---------------------------------------------------------------- the ATC operations open two positions
  const staff = await trainer.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await staff.goto('/staff/events/atc');
  await expect(staff.getByRole('heading', { level: 1, name: events.atc.title })).toBeVisible();
  await staff
    .getByRole('row')
    .filter({ hasText: title.en })
    .getByRole('link', { name: events.atc.open })
    .click();
  await expect(staff).toHaveURL(new RegExp(`/staff/events/atc/${id}$`));
  await expect(staff.getByRole('heading', { level: 1, name: title.en })).toBeVisible();

  for (const callsign of [tower, ground]) {
    await staff.getByRole('link', { name: events.atc.positions.create }).first().click();
    await fieldOf(staff, events.atc.positions.fields.callsign).getByRole('combobox').click();
    await staff.getByRole('option', { name: new RegExp(`^${callsign} — `) }).click();
    await whileWaitingFor(staff, 'POST', '/api/events/atc/positions', async () => {
      await staff.getByRole('button', { name: 'Save' }).click();
    });
    await expect(staff).toHaveURL(new RegExp(`/staff/events/atc/${id}$`));
    await expect(staff.getByRole('row').filter({ hasText: callsign })).toBeVisible();
  }

  // ---------------------------------------------------------------- the pilot gives their availability from the page
  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/events/${slug}`);
  const atc = page.getByRole('region', { name: events.public.atc.title });
  await expect(atc.getByText(tower)).toBeVisible();
  await expect(atc.getByText(ground)).toBeVisible();

  // One window, the whole event, as the form proposes it.
  await whileWaitingFor(page, 'PUT', '/api/events/mine/availability/', async () => {
    await atc.getByRole('button', { name: events.public.atc.give }).click();
  });
  await expect(atc.getByText(events.public.atc.given)).toBeVisible();

  // ---------------------------------------------------------------- the ATC operations find them on the tower
  await staff.reload();
  await staff
    .getByRole('row')
    .filter({ hasText: tower })
    .getByRole('link', { name: events.atc.open })
    .click();
  const candidates = staff.getByRole('region', { name: events.atc.candidates.title });
  await expect(candidates.getByRole('row').filter({ hasText: 'Bench Pilot (999002)' })).toBeVisible();
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    atc: {
      title: string;
      open: string;
      positions: { create: string; fields: { callsign: string } };
      candidates: { title: string };
    };
    public: { atc: { title: string; give: string; given: string } };
  };
}
