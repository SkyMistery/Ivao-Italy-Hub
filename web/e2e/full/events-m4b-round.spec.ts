import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchUrl, mailFor, readInEnglish, signIn, test, whileWaitingFor } from './bench';

/**
 * The full round of M4b (E15b; design M4 §15: «availabilities, roster proposed, corrected, published; report of support, validated»),
 * through the real screens and the real server, and the real job: the coordinator of the events (`?as=events`) publishes an evening of a
 * quarter of an hour with an ATC roster and opens its tower; the bench's trainer (`?as=trainer`, the highest ATC rating of the bench) gives
 * their availability from the page of the event; **`events-roster` proposes the roster and publishes it** at its run of the quarter, and the
 * trainer is told their shift; the coordinator reads it on the roster, with the bookings of the network beside it, and cuts its end — the one
 * correction once an event has started —, and the trainer is told again; after the end the trainer sends their ATC report from
 * `/events/mine`, the web master — who holds `EventReports.Edit` — accepts it, and the trainer is told.
 *
 * For the job to propose and publish while the spec waits, the settings say 0 days to the close and to the publication for the length of the
 * spec, and are put back after it; the event starts a minute before the next run of `events-roster` (minutes 3, 18, 33 and 48, UTC). The spec
 * waits up to a quarter of an hour for the run, and a quarter of an hour for the end.
 *
 * ⚠️ **The signal of award is not here** (note 2026-10-17-la-conservazione-di-m4b, question 2): the bench's network is the fixture client, with
 * no session of its people, so the check finds no shift and no rule of award is met. The signals of the rules met are
 * `EventsAwardRulesTests`'s.
 *
 * ⚠️ An event with a report is kept (`eventHasReports`): this spec's events stay on the bench, under a stem of their own — not `evt-test-e2e`,
 * which `events-staff.spec.ts` deletes whole.
 */

const events = englishEvents();
const italian = italianEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const settingsUrl = '/api/modules/events/settings';
const stem = 'evt-e2e-e15b';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench round of the ATC ${stamp}`, it: `Il giro ATC del banco ${stamp}` };
const trainerVid = 999004;
const trainerMail = 'bench-trainer@bench.test';
const blank = '0001-01-01T00:00:00';
const tower = 'LIRF_TWR';

/** The minutes past the hour `events-roster` runs at, in UTC (`EventRosterJob.Cron`). */
const runMinutes = [3, 18, 33, 48];

/** The next run of `events-roster` at least four minutes away: time enough to write the event and give the availability before it starts. */
function nextRun(now: Date): Date {
  const candidate = new Date(now);
  candidate.setUTCSeconds(0, 0);
  for (let minutes = 0; minutes < 120; minutes++) {
    const at = new Date(candidate.getTime() + minutes * 60_000);
    if (runMinutes.includes(at.getUTCMinutes()) && at.getTime() - now.getTime() >= 4 * 60_000) {
      return at;
    }
  }

  throw new Error('No run of events-roster in the next two hours.');
}

/** `2026-10-17T18:47:00.000Z` → `2026-10-17T18:47`, the UTC wall clock a `datetime-local` of the form generator holds. */
function wallClock(instant: Date): string {
  return instant.toISOString().slice(0, 16);
}

async function contextAs(browser: Browser, who: 'events' | 'trainer' | 'master'): Promise<BrowserContext> {
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

/** What a run of this spec left that may go: its events without a report; one with a report is kept. */
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

/** The mail to the trainer whose subject is one of these, in either language of the bench; null while it has not arrived. */
async function mailToTheTrainer(request: APIRequestContext, subjects: readonly string[]) {
  for (const subject of subjects) {
    const found = await mailFor(request, trainerMail, subject);
    if (found !== null) {
      return found;
    }
  }

  return null;
}

function subjects(key: 'atcShiftAssigned' | 'atcShiftChanged' | 'reportDecided'): string[] {
  return [
    events.mail.events[key].subject.replace('{{title}}', title.en),
    italian.mail.events[key].subject.replace('{{title}}', title.it),
  ];
}

test('the full round of M4b: availability, roster proposed and published, corrected, a report of support validated', async ({
  browser,
  afterwards,
}) => {
  test.setTimeout(40 * 60_000);

  const coordinator = await contextAs(browser, 'events');
  const trainer = await contextAs(browser, 'trainer');
  const master = await contextAs(browser, 'master');
  await removeOurEvents(coordinator.request);

  // The settings of the round: the close and the publication at the start, so the next run of the job does both.
  const before = await coordinator.request.get(settingsUrl);
  expect(before.status(), await before.text()).toBe(200);
  const saved = (await before.json()) as Record<string, unknown>;
  const round = await coordinator.request.put(settingsUrl, {
    headers: asTheClientDoes,
    data: { ...saved, applicationsCloseDays: 0, rosterPublishDays: 0 },
  });
  expect(round.status(), await round.text()).toBe(200);

  afterwards(async () => {
    const putBack = await coordinator.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), trainer.close(), master.close()]);
  });

  // ---------------------------------------------------------------- the event: a quarter of an hour, a minute before the job's run
  const run = nextRun(new Date());
  const starts = new Date(run.getTime() - 60_000);
  const ends = new Date(starts.getTime() + 15 * 60_000);

  const created = await coordinator.request.post('/api/events/events', {
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
      startsAtUtc: starts.toISOString(),
      endsAtUtc: ends.toISOString(),
      visibility: 'Public',
      rowVersion: blank,
      hasRoster: true,
      shiftMinutes: 15,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const published = await coordinator.request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  const position = await coordinator.request.post('/api/events/atc/positions', {
    headers: asTheClientDoes,
    data: { eventId: id, callsign: tower, fromUtc: null, toUtc: null, note: null, rowVersion: blank },
  });
  expect(position.status(), await position.text()).toBe(201);

  // ---------------------------------------------------------------- the trainer gives their availability, from the page of the event
  const page = await trainer.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await page.goto(`/events/${slug}`);
  const atc = page.getByRole('region', { name: events.public.atc.title });
  await expect(atc.getByText(tower)).toBeVisible();
  await whileWaitingFor(page, 'PUT', '/api/events/mine/availability/', async () => {
    await atc.getByRole('button', { name: events.public.atc.give }).click();
  });
  await expect(atc.getByText(events.public.atc.given)).toBeVisible();

  // ---------------------------------------------------------------- events-roster proposes the roster and publishes it, at its run
  await expect
    .poll(
      async () => {
        const head = await coordinator.request.get(`/api/events/atc/events/${id}`);
        const read = (await head.json()) as {
          rosterProposedAt: string | null;
          rosterPublishedAt: string | null;
        };
        return read.rosterProposedAt !== null && read.rosterPublishedAt !== null;
      },
      { timeout: 20 * 60_000, intervals: [15_000] },
    )
    .toBe(true);

  // The trainer is told their shift: one mail for the event.
  await expect
    .poll(async () => (await mailToTheTrainer(trainer.request, subjects('atcShiftAssigned'))) !== null, {
      timeout: 4 * 60_000,
      intervals: [10_000],
    })
    .toBe(true);

  // ---------------------------------------------------------------- the coordinator reads the roster, and the bookings of the network
  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await staff.goto(`/staff/events/atc/${id}`);
  await expect(staff.getByRole('heading', { level: 1, name: title.en })).toBeVisible();
  const roster = staff.getByRole('region', { name: events.atc.roster.title });
  await expect(roster.getByText(events.atc.roster.published.split('{{')[0]!.trim())).toBeVisible();
  const shift = roster.getByRole('row').filter({ hasText: `(${trainerVid})` });
  await expect(shift).toBeVisible();
  await expect(shift.getByText(events.atc.roster.options.state.Proposed, { exact: true })).toBeVisible();

  // Beside it, the network answered: the fixture client always does — never «not available».
  const network = staff.getByRole('region', { name: events.atc.network.title });
  await expect(network.getByRole('heading', { name: events.atc.network.title })).toBeVisible();
  await expect(network.getByText(events.atc.network.unavailable)).toHaveCount(0);

  // ---------------------------------------------------------------- corrected: the event has started, so the end is cut
  await shift.getByRole('link', { name: events.atc.roster.move }).click();
  await expect(staff.getByRole('heading', { level: 1, name: events.atc.shifts.edit })).toBeVisible();
  const cut = new Date(starts.getTime() + 10 * 60_000);
  await staff.locator('[id="toUtc"]').fill(wallClock(cut));
  await whileWaitingFor(staff, 'PUT', '/api/events/atc/shifts/', async () => {
    await staff.getByRole('button', { name: 'Save' }).click();
  });

  // Back on the roster: the shift by the staff now, and the trainer told of the change.
  const corrected = staff
    .getByRole('region', { name: events.atc.roster.title })
    .getByRole('row')
    .filter({ hasText: `(${trainerVid})` });
  await expect(corrected.getByText(events.atc.roster.options.state.Staff, { exact: true })).toBeVisible();
  await expect
    .poll(async () => (await mailToTheTrainer(trainer.request, subjects('atcShiftChanged'))) !== null, {
      timeout: 4 * 60_000,
      intervals: [10_000],
    })
    .toBe(true);

  // ---------------------------------------------------------------- after the end, the trainer's report of support
  const waitForTheEnd = ends.getTime() + 15_000 - Date.now();
  if (waitForTheEnd > 0) {
    await page.waitForTimeout(waitForTheEnd);
  }

  const mine = await trainer.newPage();
  mine.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await mine.goto('/events/mine');
  // The event is on the page three times — the shift, the availability, the reports open to it —: the last one, by its list.
  const open = mine
    .getByRole('list', { name: events.mine.reports.open })
    .getByRole('listitem', { name: title.en });
  await expect(open).toBeVisible();
  await open.getByRole('button', { name: events.mine.reports.send.Atc }).click();

  const dialog = mine.getByRole('dialog');
  await dialog.getByLabel(events.mine.reports.atc.fields['items.callsign'], { exact: true }).fill(tower);
  await whileWaitingFor(mine, 'PUT', `/api/events/mine/reports/${id}/Atc`, async () => {
    await dialog.getByRole('button', { name: events.mine.reports.send.Atc }).click();
  });
  const report = mine.getByRole('listitem', {
    name: events.mine.reports.of.Atc.replace('{{title}}', title.en),
  });
  await expect(report.getByText(events.reports.options.state.Submitted, { exact: true })).toBeVisible();

  // ---------------------------------------------------------------- validated by whoever holds it, and the trainer told
  const validator = await master.newPage();
  validator.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await validator.goto(`/staff/events/${id}?tab=reports`);
  const row = validator.getByRole('row', { name: new RegExp(tower) });
  await expect(row).toBeVisible();
  await row.getByRole('button', { name: events.reports.decide }).click();
  await whileWaitingFor(validator, 'POST', '/decision', async () => {
    await validator.getByRole('dialog').getByRole('button', { name: events.reports.decide }).click();
  });
  await expect(
    validator
      .getByRole('row', { name: new RegExp(tower) })
      .getByText(events.reports.options.state.Accepted, { exact: true }),
  ).toBeVisible();

  await expect
    .poll(async () => (await mailToTheTrainer(trainer.request, subjects('reportDecided'))) !== null, {
      timeout: 4 * 60_000,
      intervals: [10_000],
    })
    .toBe(true);

  await mine.reload();
  await expect(
    mine
      .getByRole('listitem', { name: events.mine.reports.of.Atc.replace('{{title}}', title.en) })
      .getByText(events.reports.options.state.Accepted, { exact: true }),
  ).toBeVisible();
});

interface Words {
  public: { atc: { title: string; give: string; given: string } };
  atc: {
    roster: {
      title: string;
      published: string;
      move: string;
      options: { state: { Proposed: string; Staff: string } };
    };
    network: { title: string; unavailable: string };
    shifts: { edit: string };
  };
  mine: {
    reports: {
      open: string;
      send: { Atc: string };
      of: { Atc: string };
      atc: { fields: { 'items.callsign': string } };
    };
  };
  reports: { decide: string; options: { state: { Submitted: string; Accepted: string } } };
  mail: {
    events: Record<'atcShiftAssigned' | 'atcShiftChanged' | 'reportDecided', { subject: string }>;
  };
}

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents(): Words {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as Words;
}

/** The same in Italian: the bench's people read their mails in it. */
function italianEvents(): Words {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/it/events.json', import.meta.url)), 'utf8'),
  ) as Words;
}
