import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import {
  benchUrl,
  department,
  giveSessions,
  mailFor,
  readInEnglish,
  runJob,
  signIn,
  takeSessions,
  test,
  whileWaitingFor,
} from './bench';

/**
 * The full round of M4b (E15b; design M4 §15: «availabilities, roster proposed, corrected, published; report of support, validated,
 * signal»), through the real screens, the real server and the real jobs: the coordinator of the events (`?as=events`) publishes an
 * evening of a quarter of an hour with an ATC roster, opens its tower and writes a rule of award, «any shift»; the bench's trainer
 * (`?as=trainer`, the highest ATC rating of the bench) gives their availability from the page of the event; at the start
 * **`events-roster` proposes the roster and publishes it** — run at once by the bench (`runJob`, E10w) — and the trainer is told their
 * shift; the coordinator reads it on the roster, with the bookings of the network beside it, and cuts its end — the one correction once an
 * event has started —, and the trainer is told again; after the end the trainer sends their ATC report from `/events/mine`, the network
 * finds their shift on the tower (the bench's sessions of the trainer, `giveSessions`, E10w), the web master — who holds
 * `EventReports.Edit` and `Awards.Assign` — accepts it, the rule is met and **its signal waits in the core's queue of the awards**, and
 * the trainer is told.
 *
 * The settings say 0 days to the close and to the publication for the length of the spec, and are put back after it, so that the close
 * and the publication are the start; the event starts two minutes after the spec writes it, time to give the availability. What the spec
 * still waits for is the clock: the start, for the roster, and the end, for the report (a quarter of an hour).
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
const award = { en: `Bench round of the ATC ${stamp}`, it: `Il giro ATC del banco ${stamp}` };
const trainerVid = 999004;
const trainerMail = 'bench-trainer@bench.test';
const blank = '0001-01-01T00:00:00';
const tower = 'LIRF_TWR';

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

/** Waits for a mail to the trainer: the queue of the notifications is sent every minute. */
async function expectMail(
  request: APIRequestContext,
  key: 'atcShiftAssigned' | 'atcShiftChanged' | 'reportDecided',
) {
  await expect
    .poll(async () => (await mailToTheTrainer(request, subjects(key))) !== null, {
      message: key,
      timeout: 4 * 60_000,
      intervals: [10_000],
    })
    .toBe(true);
}

/** Waits until the clock passes an instant. */
async function until(instant: Date): Promise<void> {
  const left = instant.getTime() - Date.now();
  if (left > 0) {
    await new Promise((resolve) => setTimeout(resolve, left));
  }
}

test('the full round of M4b: availability, roster proposed and published, corrected, a report validated, a signal', async ({
  browser,
  afterwards,
}) => {
  test.setTimeout(30 * 60_000);

  const coordinator = await contextAs(browser, 'events');
  const trainer = await contextAs(browser, 'trainer');
  const master = await contextAs(browser, 'master');
  await removeOurEvents(coordinator.request);

  // The settings of the round: the close and the publication at the start, so the run of the job at the start does both.
  const before = await coordinator.request.get(settingsUrl);
  expect(before.status(), await before.text()).toBe(200);
  const saved = (await before.json()) as Record<string, unknown>;
  const round = await coordinator.request.put(settingsUrl, {
    headers: asTheClientDoes,
    data: { ...saved, applicationsCloseDays: 0, rosterPublishDays: 0 },
  });
  expect(round.status(), await round.text()).toBe(200);

  // An award of the catalogue, written by the web team, which reaches every department.
  const createdAward = await master.request.post('/api/awards', {
    headers: asTheClientDoes,
    data: {
      ownerDepartment: department.toUpperCase(),
      name: award,
      description: null,
      criteria: null,
      imageMediaId: null,
      isActive: true,
      rowVersion: blank,
    },
  });
  expect(createdAward.status(), await createdAward.text()).toBe(201);
  const awardId = ((await createdAward.json()) as { id: number }).id;

  // ---------------------------------------------------------------- the event: a quarter of an hour, two minutes from now
  const starts = new Date(Math.ceil((Date.now() + 2 * 60_000) / 60_000) * 60_000);
  const ends = new Date(starts.getTime() + 15 * 60_000);

  // The trainer's sessions on the network from the start: the tower for forty minutes (E10w's template), what the check of the report finds.
  await giveSessions(trainer.request, trainerVid, starts);

  afterwards(async () => {
    await takeSessions(trainer.request, trainerVid);
    const putBack = await coordinator.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), trainer.close(), master.close()]);
  });

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

  // The rule of award the report will meet: any shift found on the network.
  const rule = await coordinator.request.post('/api/events/award-rules', {
    headers: asTheClientDoes,
    data: {
      eventId: id,
      awardId,
      criterion: 'AnyShift',
      departureIcao: null,
      arrivalIcao: null,
      count: null,
      distanceNm: null,
      minutes: null,
      positionType: null,
      rowVersion: blank,
    },
  });
  expect(rule.status(), await rule.text()).toBe(201);

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

  // ---------------------------------------------------------------- at the start, events-roster proposes the roster and publishes it
  await until(new Date(starts.getTime() + 2_000));
  const proposed = await runJob(coordinator.request, 'events-roster');
  expect(proposed.run?.status, JSON.stringify(proposed.run)).toBe('succeeded');

  const head = await coordinator.request.get(`/api/events/atc/events/${id}`);
  const read = (await head.json()) as { rosterProposedAt: string | null; rosterPublishedAt: string | null };
  expect(read.rosterProposedAt, JSON.stringify(read)).not.toBeNull();
  expect(read.rosterPublishedAt, JSON.stringify(read)).not.toBeNull();

  // The trainer is told their shift: one mail for the event.
  await expectMail(trainer.request, 'atcShiftAssigned');

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
  await expectMail(trainer.request, 'atcShiftChanged');

  // ---------------------------------------------------------------- after the end, the trainer's report of support, found on the network
  await until(new Date(ends.getTime() + 5_000));

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
  await expect(report.getByText(events.reports.options.verification.Valid, { exact: true })).toBeVisible();

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

  await expectMail(trainer.request, 'reportDecided');

  await mine.reload();
  await expect(
    mine
      .getByRole('listitem', { name: events.mine.reports.of.Atc.replace('{{title}}', title.en) })
      .getByText(events.reports.options.state.Accepted, { exact: true }),
  ).toBeVisible();

  // ---------------------------------------------------------------- the signal of the rule met, in the core's queue of the awards
  // The award's name carries this run's stamp: one row, the trainer's.
  await validator.goto('/staff/awards/queue');
  await expect(validator.getByRole('row').filter({ hasText: award.en })).toHaveCount(1);
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
  reports: {
    decide: string;
    options: { state: { Submitted: string; Accepted: string }; verification: { Valid: string } };
  };
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
