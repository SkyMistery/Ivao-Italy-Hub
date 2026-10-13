import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchUrl, fieldOf, readInEnglish, signIn, test } from './bench';

/**
 * E11b (M4b) on the bench, through the real screens and the real server, as far as the bench's clock lets it go: the coordinator of the
 * events (`?as=events`) publishes an event with an ATC roster; the bench's trainer (`?as=trainer`), with the ATC of the events granted to
 * their VID as in `events-atc.spec.ts`, opens the tower; the bench's pilot (`?as=pilot`) gives their availability; and the trainer finds,
 * under the positions, the roster still to be proposed with the day it will be — and a shift written before the proposal refused on the
 * form, with the pilot among who is available under it.
 *
 * ⚠️ The roster itself is not waited for here: `events-roster` runs every quarter of an hour, and the bench has no way of starting a job on
 * demand — an endpoint of the bench is the core's (A8a, «Trovato» 4; E6b's reminder the same). «The staff move a shift» — the proposal once,
 * add, move and take away with the warnings and the refusals, never on one's own shift — is `EventsRosterTests`'s, and the bench of the
 * phase watched the roster arrive at its quarter of an hour (the «done when»; note 2026-10-13-la-proposta-del-roster). The event is twenty
 * days away, its availabilities open: no run of the job can propose it while this spec runs.
 *
 * The bench survives between runs: the pilot withdraws, the coordinator deletes this spec's events and the web master takes the grants back
 * after the test (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e11b';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench roster ${stamp}`, it: `Il roster del banco ${stamp}` };
const trainerVid = 999004;
const pilotVid = 999002;
const blank = '0001-01-01T00:00:00';
const tower = 'LIRF_TWR';

/** The day of the event, twenty days from now: its availabilities close three days before it, by the settings' default. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const starts = `${day}T17:00:00Z`;
const ends = `${day}T21:00:00Z`;

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
      startsAtUtc: starts,
      endsAtUtc: ends,
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

/** What a run of this spec left: the pilot's availabilities withdrawn, the events deleted, the grants taken back. */
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

test('the roster waits under the positions for the close, and nobody writes a shift before it', async ({
  browser,
  afterwards,
}) => {
  test.setTimeout(120_000);

  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  const master = await contextAs(browser, 'master');
  await removeOurRows(coordinator.request, pilot.request, master.request);

  // A grant goes to staff the hub knows, and a grant written signs its holder out: the trainer signs in before it, and again after.
  await (await contextAs(browser, 'trainer')).close();
  await grantTheAtc(master.request);
  const trainer = await contextAs(browser, 'trainer');

  afterwards(async () => {
    await removeOurRows(coordinator.request, pilot.request, master.request);
    await Promise.all([coordinator.close(), pilot.close(), master.close(), trainer.close()]);
  });

  // The event, its tower, and the pilot's availability for the whole of it.
  const id = await publishedEvent(coordinator.request);
  const position = await trainer.request.post('/api/events/atc/positions', {
    headers: asTheClientDoes,
    data: { eventId: id, callsign: tower, fromUtc: null, toUtc: null, note: null, rowVersion: blank },
  });
  expect(position.status(), await position.text()).toBe(201);
  const positionId = ((await position.json()) as { id: number }).id;

  const given = await pilot.request.put(`/api/events/mine/availability/${id}`, {
    headers: asTheClientDoes,
    data: { windows: [{ fromUtc: starts, toUtc: ends }], note: null },
  });
  expect(given.status(), await given.text()).toBe(201);

  // ---------------------------------------------------------------- the roster, under the positions
  const staff = await trainer.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await staff.goto(`/staff/events/atc/${id}`);
  await expect(staff.getByRole('heading', { level: 1, name: title.en })).toBeVisible();
  const roster = staff.getByRole('region', { name: events.atc.roster.title });
  await expect(roster.getByRole('heading', { name: events.atc.roster.title })).toBeVisible();
  await expect(roster.getByText(events.atc.roster.notYet.split('{{')[0]!.trim())).toBeVisible();

  // ---------------------------------------------------------------- a shift before the proposal: refused on the form
  const query = new URLSearchParams({
    positionId: String(positionId),
    fromUtc: starts,
    toUtc: `${day}T18:00:00Z`,
  });
  await staff.goto(`/staff/events/atc/${id}/shifts/new?${query.toString()}`);
  await expect(staff.getByRole('heading', { level: 1, name: events.atc.shifts.create })).toBeVisible();
  await fieldOf(staff, events.atc.shifts.fields.controllerVid).locator('input').fill(String(pilotVid));
  const answered = staff.waitForResponse(
    (response) => response.request().method() === 'POST' && response.url().includes('/api/events/atc/shifts'),
  );
  await staff.getByRole('button', { name: 'Save' }).click();
  expect((await answered).status()).toBe(400);
  await expect(staff.getByText(events.errors.rosterNotProposed).first()).toBeVisible();

  // Who is available on the tower, under the form: the pilot.
  const candidates = staff.getByRole('region', { name: events.atc.candidates.title });
  await expect(candidates.getByRole('row').filter({ hasText: `(${pilotVid})` })).toBeVisible();
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    atc: {
      candidates: { title: string };
      roster: { title: string; notYet: string };
      shifts: { create: string; fields: { controllerVid: string } };
    };
    errors: { rosterNotProposed: string };
  };
}
