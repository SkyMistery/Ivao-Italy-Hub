import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchAirports, benchUrl, readInEnglish, signIn, test, whileWaitingFor } from './bench';

/**
 * The support PIREP of E14a (M4b), through the real screens and the real server — the «done when»: the coordinator of the events
 * (`?as=events`) publishes an evening at the bench's Rome two days ago; the bench's pilot (`?as=pilot`) finds it on `/events/mine` among the
 * events open to a report, sends their flight from the generated form and reads the check of it; the web master — who holds every permission
 * of the modules, `EventReports.Edit` included — finds it in the tab «Reports of support» of the event and accepts it; the pilot reads it
 * accepted, no longer to correct.
 *
 * ⚠️ The bench's network is the fixture client: it holds no session of the pilot, so the check answers «no item found». Finding a flight is
 * `EventsReportsTests`'s, on the evening of the fixtures moved in time; the phase's bench watched it with a session of its own.
 *
 * ⚠️ An event with a report is kept (`eventHasReports`): this spec's events stay on the bench, which `e2e:full` starts empty. A run deletes the
 * ones it can, and leaves the others under its own stem.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e14a';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench evening reported ${stamp}`, it: `La sera raccontata del banco ${stamp}` };
const blank = '0001-01-01T00:00:00';

/** The day of the event, two days ago. */
const day = new Date(Date.now() - 2 * 24 * 3600 * 1000).toISOString().slice(0, 10);

async function contextAs(browser: Browser, as: 'events' | 'pilot' | null): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  if (as === null) {
    await signIn(context);
  } else {
    const signedIn = await context.request.post(`/e2e/signin?as=${as}`);
    expect(signedIn.status(), await signedIn.text()).toBe(200);
  }

  return context;
}

/** An evening at the bench's Rome two days ago, published. */
async function eventOver(request: APIRequestContext): Promise<number> {
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

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

/** What a run of this spec left that may go: its events without a report; one with a report is kept. */
async function removeOurEvents(coordinator: APIRequestContext): Promise<void> {
  const page = await coordinator.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);
  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await coordinator.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect([204, 400], await deleted.text()).toContain(deleted.status());
  }
}

test('a pilot sends their flight of an evening over and whoever validates accepts it', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await contextAs(browser, 'events');
  await removeOurEvents(coordinator.request);
  const pilot = await contextAs(browser, 'pilot');
  const master = await contextAs(browser, null);

  afterwards(async () => {
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), pilot.close(), master.close()]);
  });

  const id = await eventOver(coordinator.request);

  // The pilot: the evening is open to a report on their page; they send their flight.
  const mine = await pilot.newPage();
  mine.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await mine.goto('/events/mine');
  const open = mine.getByRole('listitem', { name: title.en });
  await expect(open).toBeVisible();
  await open.getByRole('button', { name: events.mine.reports.send.Pilot }).click();

  const dialog = mine.getByRole('dialog');
  await dialog.getByLabel(events.mine.reports.pilot.fields['items.callsign'], { exact: true }).fill('XEA141');
  await dialog
    .getByLabel(events.mine.reports.pilot.fields['items.departureIcao'], { exact: true })
    .fill(benchAirports.rome);
  await dialog
    .getByLabel(events.mine.reports.pilot.fields['items.arrivalIcao'], { exact: true })
    .fill(benchAirports.milan);
  await whileWaitingFor(mine, 'PUT', `/api/events/mine/reports/${id}/Pilot`, async () => {
    await dialog.getByRole('button', { name: events.mine.reports.send.Pilot }).click();
  });

  // The check of it, said on the report: the bench's network has no session of the pilot.
  const report = mine.getByRole('listitem', {
    name: events.mine.reports.of.Pilot.replace('{{title}}', title.en),
  });
  await expect(report.getByText(events.reports.options.state.Submitted, { exact: true })).toBeVisible();
  await expect(report.getByText(events.reports.options.verification.Invalid, { exact: true })).toBeVisible();
  await expect(report.getByText(new RegExp(events.reports.check.notFound))).toBeVisible();
  await expect(report.getByRole('button', { name: events.mine.reports.correct.Pilot })).toBeVisible();

  // Whoever validates: the tab of the event, the report with its item, «Decide», accepted.
  const staff = await master.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await staff.goto(`/staff/events/${id}?tab=reports`);
  await expect(staff.getByRole('tab', { name: events.events.tabs.reports })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  const row = staff.getByRole('row', { name: /XEA141/ });
  await expect(row).toBeVisible();
  await row.getByRole('button', { name: events.reports.decide }).click();
  const decision = staff.getByRole('dialog');
  await whileWaitingFor(staff, 'POST', '/decision', async () => {
    await decision.getByRole('button', { name: events.reports.decide }).click();
  });
  await expect(
    staff
      .getByRole('row', { name: /XEA141/ })
      .getByText(events.reports.options.state.Accepted, { exact: true }),
  ).toBeVisible();

  // The pilot reads it accepted, and corrects it no more.
  await mine.reload();
  const accepted = mine.getByRole('listitem', {
    name: events.mine.reports.of.Pilot.replace('{{title}}', title.en),
  });
  await expect(accepted.getByText(events.reports.options.state.Accepted, { exact: true })).toBeVisible();
  await expect(accepted.getByRole('button', { name: events.mine.reports.correct.Pilot })).toHaveCount(0);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { reports: string } };
    mine: {
      reports: {
        send: { Pilot: string };
        correct: { Pilot: string };
        of: { Pilot: string };
        pilot: { fields: Record<string, string> };
      };
    };
    reports: {
      decide: string;
      options: { state: { Submitted: string; Accepted: string }; verification: { Invalid: string } };
      check: { notFound: string };
    };
  };
}
