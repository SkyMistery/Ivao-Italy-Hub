import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchUrl, readInEnglish, signIn, test } from './bench';

/**
 * E12 (M4b) on the bench, through the real screens and the real server, as far as the bench's clock lets it go: the coordinator of the
 * events (`?as=events`) publishes an event with an ATC roster; the bench's trainer (`?as=trainer`), with the ATC of the events granted to
 * their VID as in `events-atc.spec.ts`, opens the tower; the bench's pilot (`?as=pilot`) gives their availability. Then:
 * - the event's page says when the roster is published, to a visitor too, and the page of the roster says the same and names nobody;
 * - the pilot finds their availability on `/events/mine`, with the way back to the event's page to change it;
 * - the trainer's block of the coverage lists the event, its availabilities still open.
 *
 * ⚠️ The publication itself is not waited for here: `events-roster` runs every quarter of an hour, and the bench has no way of starting a job
 * on demand (E11b's spec, the same). The roster published at its date with one mail per controller, the change told at once and handing a
 * shift over are `EventsRosterPublicationTests`'s, and the bench of the phase watched the publication arrive at its quarter of an hour (the
 * «done when»; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione). The event is twenty days away: no run of the job can propose or
 * publish it while this spec runs.
 *
 * The bench survives between runs: the pilot withdraws, the coordinator deletes this spec's events and the web master takes the grants back
 * after the test (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e12';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench publication ${stamp}`, it: `La pubblicazione del banco ${stamp}` };
const trainerVid = 999004;
const blank = '0001-01-01T00:00:00';
const tower = 'LIRF_TWR';

/** The day of the event, twenty days from now: its roster is published two days before it, by the settings' default. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
const starts = `${day}T17:00:00Z`;
const ends = `${day}T21:00:00Z`;

async function contextAs(
  browser: Browser,
  who: 'events' | 'pilot' | 'trainer' | 'master' | 'visitor',
): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  if (who === 'master') {
    await signIn(context);
  } else if (who !== 'visitor') {
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

/** The properties of a block, as the browser sends them to the read of a block. */
function blockProps(props: object): string {
  return Buffer.from(JSON.stringify(props))
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

test('the roster says when it is published, the pilot finds their availability, the staff the coverage', async ({
  browser,
  afterwards,
}) => {
  test.setTimeout(120_000);

  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  const master = await contextAs(browser, 'master');
  const visitor = await contextAs(browser, 'visitor');
  await removeOurRows(coordinator.request, pilot.request, master.request);

  // A grant goes to staff the hub knows, and a grant written signs its holder out: the trainer signs in before it, and again after.
  await (await contextAs(browser, 'trainer')).close();
  await grantTheAtc(master.request);
  const trainer = await contextAs(browser, 'trainer');

  afterwards(async () => {
    await removeOurRows(coordinator.request, pilot.request, master.request);
    await Promise.all([coordinator.close(), pilot.close(), master.close(), trainer.close(), visitor.close()]);
  });

  // The event, its tower, and the pilot's availability for the whole of it.
  const id = await publishedEvent(coordinator.request);
  const position = await trainer.request.post('/api/events/atc/positions', {
    headers: asTheClientDoes,
    data: { eventId: id, callsign: tower, fromUtc: null, toUtc: null, note: null, rowVersion: blank },
  });
  expect(position.status(), await position.text()).toBe(201);

  const given = await pilot.request.put(`/api/events/mine/availability/${id}`, {
    headers: asTheClientDoes,
    data: { windows: [{ fromUtc: starts, toUtc: ends }], note: null },
  });
  expect(given.status(), await given.text()).toBe(201);

  // ---------------------------------------------------------------- the event's page and the roster's, to a visitor
  const page = await visitor.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/events/${slug}`);
  const atc = page.getByRole('region', { name: events.public.atc.title });
  await expect(atc.getByText(events.public.atc.rosterOn.split('{{')[0]!.trim())).toBeVisible();

  await page.goto(`/events/${slug}/roster`);
  await expect(
    page.getByRole('heading', { level: 1, name: events.roster.title.replace('{{title}}', title.en) }),
  ).toBeVisible();
  await expect(page.getByText(events.roster.notYet.split('{{')[0]!.trim())).toBeVisible();
  await expect(page.getByRole('link', { name: events.roster.toEvent })).toHaveAttribute(
    'href',
    `/events/${slug}`,
  );

  // ---------------------------------------------------------------- the pilot's own page
  const own = await pilot.newPage();
  own.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await own.goto('/events/mine');
  const availabilities = own.getByRole('region', { name: events.mine.availability.title });
  const item = availabilities.getByRole('listitem', { name: title.en });
  await expect(item).toBeVisible();
  await expect(item.getByRole('link', { name: events.mine.availability.change })).toHaveAttribute(
    'href',
    `/events/${slug}#atc`,
  );

  // ---------------------------------------------------------------- the staff's coverage: the event, its availabilities open
  const coverage = await trainer.request.get(
    `/api/blocks/data/events.atcCoverage?props=${blockProps({ days: 30 })}`,
  );
  expect(coverage.status(), await coverage.text()).toBe(200);
  const answer = (await coverage.json()) as {
    signedIn: boolean;
    events: readonly { id: number; stage: string; availabilities: number }[];
  };
  expect(answer.signedIn).toBe(true);
  const ours = answer.events.find((entry) => entry.id === id);
  expect(ours?.stage).toBe('Open');
  expect(ours?.availabilities).toBe(1);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    public: { atc: { title: string; rosterOn: string } };
    roster: { title: string; notYet: string; toEvent: string };
    mine: { availability: { title: string; change: string } };
  };
}
