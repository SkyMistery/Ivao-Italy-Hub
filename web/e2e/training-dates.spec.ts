import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { anonymousBootstrap, staffBootstrap, stubTheApi } from './fixtures';
import { englishCommon } from './locales';

/**
 * The dates of a training in a browser, with the API stubbed (M3, A8b): the trainee reaches the page of their training from their
 * trainings and chooses one of the dates the trainer proposed, shown as tiles — a choice the trainer overtook says so and shows the
 * dates there are now —; on the staff's page the dates proposed are asked what they meet before they go, a warning under `Warn` is
 * confirmed first, a date under `Block` is refused on its field with what was found beside it, a date is taken back and one set
 * by hand, a dated training shows its session, and a training is closed with a reason. What the server decides is proved by
 * `TrainingDatesTests` (A8a); the round against the real server is `full/training-the-dates.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  kinds: Record<string, string>;
  states: Record<string, string>;
  time: { utc: string; local: string };
  mine: {
    datesWaiting_other: string;
    chooseDate: string;
    closedByStaff: string;
    closedUnanswered: string;
  };
  detail: {
    session: string;
    dates: { title: string; choose: string; confirm: string; chosen: string; moved: string };
  };
  staff: {
    options: { state: Record<string, string> };
    sections: { dates: string; closing: string };
    decision: { reason: string };
    session: { chosen: string; byHand: string };
    dates: {
      none: string;
      proposedBy: string;
      warning: { training: string; calendar: string };
      met: { confirmTitle: string; refusedTitle: string };
      withdraw: { button: string; done: string };
      propose: {
        submit: string;
        confirm: string;
        done_other: string;
        fields: Record<string, string>;
      };
      setByHand: { submit: string; done: string; fields: { startsAtUtc: string } };
    };
    close: { button: string; done: string; fields: { reason: string } };
    closing: { byStaff: string };
  };
  errors: Record<string, string>;
};

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/** An hour of a day some days ahead, in UTC, as the API writes an instant: the dates of a test never go by. */
function at(days: number, hour: number): string {
  const moment = new Date(Date.now() + days * 86_400_000);
  moment.setUTCHours(hour, 0, 0, 0);
  return moment.toISOString().replace('.000Z', 'Z');
}

/** What a `datetime-local` box holds for an instant: the UTC wall clock, to the minute. */
const box = (instant: string) => instant.slice(0, 16);

/** An instant as the pages say it (`useMoment`, in English): twenty four hours, UTC. */
const inUtc = new Intl.DateTimeFormat('en', {
  dateStyle: 'medium',
  timeStyle: 'short',
  hour12: false,
  timeZone: 'UTC',
});
const timeInUtc = new Intl.DateTimeFormat('en', { timeStyle: 'short', hour12: false, timeZone: 'UTC' });
const moment = (instant: string) => inUtc.format(new Date(instant));
const span = (start: string, end: string) => `${moment(start)}–${timeInUtc.format(new Date(end))}`;

const trainee = { vid: 790099, name: 'Test Trainee' };
const trainer = { vid: 790098, name: 'Test Trainer' };
const coordinator = { vid: 790097, name: 'Test Coordinator' };

const traineeBootstrap = {
  ...anonymousBootstrap,
  user: {
    vid: trainee.vid,
    firstName: 'Test',
    lastName: 'Trainee',
    positions: [],
    isStaff: false,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: [],
    firs: [],
    tokenAudiences: [],
  },
};

const trainerBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: trainer.vid,
    lastName: 'Trainer',
    positions: ['XX-T01'],
    departments: ['TD'],
  },
  permissions: [{ name: 'Training.View', department: 'TD' }],
};

const coordinatorBootstrap = {
  ...trainerBootstrap,
  user: { ...trainerBootstrap.user, vid: coordinator.vid, lastName: 'Coordinator', positions: ['XX-TC'] },
  permissions: [
    { name: 'Training.View', department: 'TD' },
    { name: 'Training.Approve', department: 'TD' },
  ],
};

const nothing = { canDecide: false, canAssign: false, canConduct: false, canClose: false };
const conducting = { ...nothing, canConduct: true };

// ---- the trainee's side ------------------------------------------------------------------------------------------------

function slot(id: number, days: number) {
  return { id, startsAtUtc: at(days, 16), endsAtUtc: at(days, 18) };
}

function mineOne(state: string, overrides: Record<string, unknown> = {}) {
  return {
    id: 41,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    state,
    rejection: null,
    rejectionReason: null,
    availabilityText: 'Evenings after 18 UTC.',
    notesText: null,
    requestedAt: '2026-09-26T10:00:00Z',
    decidedAt: '2026-09-26T12:00:00Z',
    trainer,
    slots: [],
    scheduledStartUtc: null,
    held: false,
    completedAt: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    rowVersion: '2026-09-26T12:00:00.123456Z',
    ...overrides,
  };
}

/**
 * The trainee's side of the API: their trainings, one training as `one` says it at the moment it is asked — a choice changes
 * it —, and the choices sent, kept for the test to read.
 */
async function stubTheTrainee(
  page: Page,
  {
    one,
    choose = () => ({ status: 500, body: {} }),
  }: {
    one: () => Record<string, unknown>;
    choose?: (body: Record<string, unknown>) => { status: number; body: unknown };
  },
): Promise<{ choices: Record<string, unknown>[]; reads: () => number }> {
  const choices: Record<string, unknown>[] = [];
  let reads = 0;

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(traineeBootstrap)));
  await page.route('**/api/training/mine', (route) =>
    route.fulfill(
      json({
        vid: trainee.vid,
        name: trainee.name,
        asksTheory: false,
        theoryExamUrl: null,
        paths: [],
        trainings: [one()],
      }),
    ),
  );
  await page.route('**/api/training/mine/41', (route) => {
    reads += 1;
    return route.fulfill(json(one()));
  });
  await page.route('**/api/training/mine/41/choose', (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    choices.push(body);
    const answer = choose(body);
    return route.fulfill(json(answer.body, answer.status));
  });

  return { choices, reads: () => reads };
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the trainee reaches their training from their trainings and chooses one of the dates, shown as tiles', async ({
  page,
}) => {
  const [first, second] = [slot(11, 3), slot(12, 4)];
  let dated = false;
  const assigned = mineOne('Assigned', { slots: [first, second] });

  const seen = await stubTheTrainee(page, {
    one: () =>
      dated
        ? mineOne('Scheduled', {
            scheduledStartUtc: second.startsAtUtc,
            rowVersion: '2026-09-27T10:00:00.1Z',
          })
        : assigned,
    choose: () => {
      dated = true;
      return {
        status: 200,
        body: mineOne('Scheduled', {
          scheduledStartUtc: second.startsAtUtc,
          rowVersion: '2026-09-27T10:00:00.1Z',
        }),
      };
    },
  });

  // From their trainings: the dates wait for them, and the way there says so.
  await page.goto('/training/mine');
  await expect(page.getByText(filled(words.mine.datesWaiting_other, { count: '2' }))).toBeVisible();
  await page.getByRole('link', { name: words.mine.chooseDate, exact: true }).click();
  await expect(page).toHaveURL(/\/training\/mine\/41$/);

  // The tiles: when each is, in UTC and where the division lives.
  await expect(page.getByRole('heading', { level: 2, name: words.detail.dates.title })).toBeVisible();
  const tiles = page
    .getByRole('listitem')
    .filter({ has: page.getByRole('button', { name: words.detail.dates.choose, exact: true }) });
  await expect(tiles).toHaveCount(2);
  await expect(tiles.first()).toContainText(
    filled(words.time.utc, { when: span(first.startsAtUtc, first.endsAtUtc) }),
  );
  await expect(tiles.first()).toContainText('(Europe/Rome)');
  await expect(tiles.nth(1)).toContainText(
    filled(words.time.utc, { when: span(second.startsAtUtc, second.endsAtUtc) }),
  );

  // The second, asked once more, with the version the page read.
  await tiles.nth(1).getByRole('button', { name: words.detail.dates.choose, exact: true }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.detail.dates.confirm, exact: true })
    .click();

  // Exact: the toast is also announced, for a while, as «Notification …» in a live region of its own.
  await expect(
    page.getByText(filled(words.detail.dates.chosen, { when: moment(second.startsAtUtc) }), { exact: true }),
  ).toBeVisible();
  expect(seen.choices).toEqual([{ slotId: 12, rowVersion: assigned.rowVersion }]);

  // Read again: the session, and no tile left.
  await expect(page.getByRole('heading', { level: 2, name: words.detail.session })).toBeVisible();
  await expect(page.getByText(words.states.Scheduled!, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: words.detail.dates.choose, exact: true })).toHaveCount(0);
});

test('a choice the trainer overtook says the dates moved, and the page shows the ones there are now', async ({
  page,
}) => {
  const [first, second] = [slot(11, 3), slot(12, 4)];
  let moved = false;

  const seen = await stubTheTrainee(page, {
    one: () =>
      moved
        ? mineOne('Assigned', { slots: [second], rowVersion: '2026-09-27T10:00:00.1Z' })
        : mineOne('Assigned', { slots: [first, second] }),
    choose: () => {
      moved = true;
      return { status: 409, body: { title: 'Somebody else changed it.', status: 409 } };
    },
  });

  await page.goto('/training/mine/41');
  const choose = page.getByRole('button', { name: words.detail.dates.choose, exact: true });
  await expect(choose).toHaveCount(2);

  await choose.first().click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.detail.dates.confirm, exact: true })
    .click();

  await expect(page.getByText(words.detail.dates.moved, { exact: true })).toBeVisible();
  await expect(choose).toHaveCount(1);
  expect(seen.choices).toHaveLength(1);
  expect(seen.reads()).toBeGreaterThan(1);
});

test('a training closed without a report says who closed it: the staff with its reason, or the hub', async ({
  page,
}) => {
  let reason: string | null = 'No date suited you: ask again when you are free.';

  await stubTheTrainee(page, {
    one: () => mineOne('Closed', { closedAt: '2026-10-02T04:15:00Z', closeReason: reason }),
  });

  await page.goto('/training/mine/41');
  await expect(page.getByText(words.states.Closed!, { exact: true })).toBeVisible();
  await expect(page.getByText(filled(words.mine.closedByStaff, { reason: reason }))).toBeVisible();

  // Closed by the night: no reason, and the sentence says the date was not chosen in time.
  reason = null;
  await page.goto('/training/mine');
  await expect(page.getByText(words.mine.closedUnanswered)).toBeVisible();
});

// ---- the staff's side --------------------------------------------------------------------------------------------------

function staffTraining(state: string, overrides: Record<string, unknown> = {}) {
  return {
    id: 41,
    kind: 'Atc',
    rating: 5,
    ratingShortName: 'ADC',
    ratingNameKey: 'ratings.Atc.ADC',
    isMockExam: false,
    position: 'XXAA_TWR',
    airportIcao: 'XXAA',
    fir: 'XXXX',
    trainee,
    traineeRatingShortName: 'AS3',
    traineeHoursAtRequest: 120,
    requestedAt: '2026-09-20T10:00:00Z',
    theoryConfirmedAt: '2026-09-20T10:00:00Z',
    theoryExamUrl: null,
    availabilityText: 'Evenings after 18 UTC.',
    notesText: null,
    state,
    rejection: null,
    rejectionReason: null,
    decidedBy: coordinator,
    decidedAt: '2026-09-21T09:00:00Z',
    trainer,
    assignedBy: coordinator,
    assignedAt: '2026-09-21T10:00:00Z',
    slots: [],
    scheduledStartUtc: null,
    held: false,
    dateChosenByTrainee: false,
    completedAt: null,
    closedBy: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    actions: nothing,
    rowVersion: '2026-09-21T10:00:00.123456Z',
    ...overrides,
  };
}

/** What the hub found on a day: another training's session, and an entry of the calendar of a kind the division checks. */
const otherSession = {
  kind: 'Training',
  startsAtUtc: at(3, 10),
  endsAtUtc: null,
  trainingId: 40,
  trainingKind: 'Pilot',
  ratingShortName: 'PP',
  position: null,
  calendarKind: null,
  title: null,
  url: null,
};
const meeting = {
  kind: 'Calendar',
  startsAtUtc: at(3, 19),
  endsAtUtc: at(3, 21),
  trainingId: null,
  trainingKind: null,
  ratingShortName: null,
  position: null,
  calendarKind: 'meeting',
  title: { en: 'Evening on the radio', it: 'Serata in radio' },
  url: '/events/evening',
};

/** The two lines of those warnings, as the page writes them. */
const otherSessionLine = filled(words.staff.dates.warning.training, {
  ladder: words.kinds.Pilot!,
  what: 'PP',
  when: moment(otherSession.startsAtUtc),
});
const meetingLine = filled(words.staff.dates.warning.calendar, {
  kind: 'Meeting',
  what: 'Evening on the radio',
  when: span(meeting.startsAtUtc, meeting.endsAtUtc),
});

interface StaffSeen {
  conflicts: { start: string; end: string | null }[];
  steps: { verb: string; body: Record<string, unknown> }[];
}

/**
 * The staff's side of the API: the page as it stands — each step changes it, as `step` answers —, what a date meets as
 * `conflicts` answers, and what is asked and sent, kept for the test to read.
 */
async function stubTheStaff(
  page: Page,
  {
    bootstrap = trainerBootstrap,
    initial,
    rows = [],
    conflicts = () => ({ policy: 'Warn', warnings: [] }),
    step = () => ({ status: 500, body: {} }),
  }: {
    bootstrap?: unknown;
    initial: Record<string, unknown>;
    rows?: unknown[];
    conflicts?: (start: string, end: string | null) => { policy: string; warnings: unknown[] };
    step?: (verb: string, body: Record<string, unknown>) => { status: number; body: unknown };
  },
): Promise<StaffSeen> {
  const seen: StaffSeen = { conflicts: [], steps: [] };
  let current: unknown = initial;

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(bootstrap)));
  await page.route('**/api/training/queue?**', (route) =>
    route.fulfill(json({ items: rows, page: 1, pageSize: 25, total: rows.length })),
  );
  await page.route('**/api/training/trainings/41', (route) => route.fulfill(json(current)));
  await page.route('**/api/training/trainings/41/**', (route) => {
    const url = new URL(route.request().url());
    const verb = url.pathname.replace(/^.*\/trainings\/41\//, '');

    if (verb === 'conflicts') {
      const start = url.searchParams.get('startsAtUtc') ?? '';
      const end = url.searchParams.get('endsAtUtc');
      seen.conflicts.push({ start, end });
      return route.fulfill(json(conflicts(start, end)));
    }

    const body = route.request().postDataJSON() as Record<string, unknown>;
    seen.steps.push({ verb, body });
    const answer = step(verb, body);
    if (answer.status === 200) {
      current = answer.body;
    }
    return route.fulfill(json(answer.body, answer.status));
  });

  return seen;
}

/** A date proposed, as the staff reads it: when, what the hub found, and who proposed it. */
function proposed(id: number, date: { startsAtUtc: string; endsAtUtc: string }, warnings: unknown[]) {
  return {
    id,
    startsAtUtc: date.startsAtUtc,
    endsAtUtc: date.endsAtUtc,
    warnings,
    proposedBy: trainer,
    proposedAt: '2026-09-27T09:00:00Z',
  };
}

test('the dates proposed are asked what they meet first, a warning under Warn is confirmed before they go, and one is taken back', async ({
  page,
}) => {
  const [first, second] = [slot(0, 3), slot(0, 4)];
  const before = staffTraining('Assigned', { actions: conducting });
  const after = staffTraining('Assigned', {
    actions: conducting,
    slots: [proposed(11, first, [otherSession, meeting]), proposed(12, second, [])],
    rowVersion: '2026-09-27T09:00:00.1Z',
  });

  const seen = await stubTheStaff(page, {
    initial: before,
    conflicts: (start) => ({
      policy: 'Warn',
      warnings: start === first.startsAtUtc ? [otherSession, meeting] : [],
    }),
    step: (verb) =>
      verb === 'slots'
        ? { status: 200, body: after }
        : { status: 200, body: { ...after, slots: [after.slots[0]], rowVersion: '2026-09-27T09:30:00.1Z' } },
  });

  await page.goto('/staff/training/41');
  await expect(page.getByRole('heading', { level: 2, name: words.staff.sections.dates })).toBeVisible();
  await expect(page.getByText(words.staff.dates.none)).toBeVisible();

  // Two dates: the one the form starts with, and one more.
  const fields = words.staff.dates.propose.fields;
  await page.getByLabel(fields['slots.startsAtUtc']!, { exact: true }).fill(box(first.startsAtUtc));
  await page.getByLabel(fields['slots.endsAtUtc']!, { exact: true }).fill(box(first.endsAtUtc));
  await page.getByRole('button', { name: englishCommon.form.addEntry, exact: true }).click();
  await page.getByLabel(fields['slots.startsAtUtc']!, { exact: true }).nth(1).fill(box(second.startsAtUtc));
  await page.getByLabel(fields['slots.endsAtUtc']!, { exact: true }).nth(1).fill(box(second.endsAtUtc));
  await page.getByRole('button', { name: words.staff.dates.propose.submit, exact: true }).click();

  // What they meet, asked of each, and shown: nothing is written before the confirmation.
  await expect(page.getByText(words.staff.dates.met.confirmTitle)).toBeVisible();
  await expect(page.getByRole('link', { name: otherSessionLine })).toHaveAttribute(
    'href',
    '/staff/training/40',
  );
  await expect(page.getByRole('link', { name: meetingLine })).toHaveAttribute('href', '/events/evening');
  expect(seen.conflicts).toEqual([
    { start: first.startsAtUtc, end: first.endsAtUtc },
    { start: second.startsAtUtc, end: second.endsAtUtc },
  ]);
  expect(seen.steps).toEqual([]);

  // Confirmed: the very dates go, confirmed, with the version the page read.
  await page.getByRole('button', { name: words.staff.dates.propose.confirm, exact: true }).click();
  await expect(
    page.getByText(filled(words.staff.dates.propose.done_other, { count: '2' }), { exact: true }),
  ).toBeVisible();
  expect(seen.steps).toEqual([
    {
      verb: 'slots',
      body: {
        slots: [
          { startsAtUtc: first.startsAtUtc, endsAtUtc: first.endsAtUtc },
          { startsAtUtc: second.startsAtUtc, endsAtUtc: second.endsAtUtc },
        ],
        confirmed: true,
        rowVersion: before.rowVersion,
      },
    },
  ]);

  // The dates proposed, with what the hub found when they were written, and who proposed them.
  const entries = page
    .getByRole('listitem')
    .filter({ has: page.getByRole('button', { name: words.staff.dates.withdraw.button, exact: true }) });
  await expect(entries).toHaveCount(2);
  await expect(entries.first()).toContainText(otherSessionLine);
  await expect(entries.first()).toContainText(
    filled(words.staff.dates.proposedBy, { name: 'Test Trainer (790098)', date: 'Sep 27, 2026' }),
  );

  // The second taken back, asked first.
  await entries.nth(1).getByRole('button', { name: words.staff.dates.withdraw.button, exact: true }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.staff.dates.withdraw.button, exact: true })
    .click();
  await expect(page.getByText(words.staff.dates.withdraw.done, { exact: true })).toBeVisible();
  expect(seen.steps.at(-1)).toEqual({ verb: 'slots/12/withdraw', body: { rowVersion: after.rowVersion } });
  await expect(entries).toHaveCount(1);
});

test('under Block a date that meets something is refused on its field, with what was found beside it', async ({
  page,
}) => {
  const date = slot(0, 3);
  const seen = await stubTheStaff(page, {
    initial: staffTraining('Assigned', { actions: conducting }),
    conflicts: () => ({ policy: 'Block', warnings: [otherSession] }),
    step: () => ({
      status: 400,
      body: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { 'slots[0].startsAtUtc': ['training:errors.dateBlocked'] },
      },
    }),
  });

  await page.goto('/staff/training/41');
  const fields = words.staff.dates.propose.fields;
  await page.getByLabel(fields['slots.startsAtUtc']!, { exact: true }).fill(box(date.startsAtUtc));
  await page.getByLabel(fields['slots.endsAtUtc']!, { exact: true }).fill(box(date.endsAtUtc));
  await page.getByRole('button', { name: words.staff.dates.propose.submit, exact: true }).click();

  // The server's refusal on the date's own field, and what was found beside it: nothing to confirm.
  await expect(page.getByText(words.errors.dateBlocked!, { exact: true })).toBeVisible();
  await expect(page.getByText(words.staff.dates.met.refusedTitle)).toBeVisible();
  await expect(page.getByRole('link', { name: otherSessionLine })).toBeVisible();
  await expect(
    page.getByRole('button', { name: words.staff.dates.propose.confirm, exact: true }),
  ).toHaveCount(0);
  expect(seen.steps).toEqual([
    {
      verb: 'slots',
      body: {
        slots: [{ startsAtUtc: date.startsAtUtc, endsAtUtc: date.endsAtUtc }],
        confirmed: false,
        rowVersion: '2026-09-21T10:00:00.123456Z',
      },
    },
  ]);
});

test('a dated training shows its session and whose choice it was, and whoever conducts it sets another by hand', async ({
  page,
}) => {
  const session = at(3, 16);
  const moved = at(5, 17);
  const scheduled = staffTraining('Scheduled', {
    scheduledStartUtc: session,
    dateChosenByTrainee: true,
    actions: conducting,
  });

  const seen = await stubTheStaff(page, {
    initial: scheduled,
    rows: [
      {
        id: 41,
        kind: 'Atc',
        rating: 5,
        ratingShortName: 'ADC',
        isMockExam: false,
        position: 'XXAA_TWR',
        state: 'Scheduled',
        trainee,
        trainer,
        createdAt: '2026-09-20T10:00:00Z',
        scheduledStartUtc: '2026-09-25T16:00:00Z',
        held: true,
      },
    ],
    conflicts: () => ({ policy: 'None', warnings: [] }),
    step: (_verb, body) => ({
      status: 200,
      body: {
        ...scheduled,
        scheduledStartUtc: body.startsAtUtc,
        dateChosenByTrainee: false,
        rowVersion: '2026-09-27T10:00:00.1Z',
      },
    }),
  });

  // In the list, a session whose day is over shows as held.
  await page.goto('/staff/training');
  await expect(page.getByRole('cell', { name: words.staff.options.state.Held!, exact: true })).toBeVisible();

  // The session, in UTC and where the division lives, chosen by the trainee; no proposal once it has one.
  await page.goto('/staff/training/41');
  await expect(
    page.getByText(filled(words.time.utc, { when: moment(session) }), { exact: true }),
  ).toBeVisible();
  await expect(page.getByText(words.staff.session.chosen, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.dates.propose.submit, exact: true })).toHaveCount(
    0,
  );

  // Set by hand: what it meets asked of its start, and the date sent unconfirmed — the division looks at nothing here.
  await page.getByLabel(words.staff.dates.setByHand.fields.startsAtUtc, { exact: true }).fill(box(moved));
  await page.getByRole('button', { name: words.staff.dates.setByHand.submit, exact: true }).click();

  await expect(
    page.getByText(filled(words.staff.dates.setByHand.done, { when: moment(moved) }), { exact: true }),
  ).toBeVisible();
  expect(seen.conflicts).toEqual([{ start: moved, end: null }]);
  expect(seen.steps).toEqual([
    { verb: 'date', body: { startsAtUtc: moved, confirmed: false, rowVersion: scheduled.rowVersion } },
  ]);
  await expect(page.getByText(words.staff.session.byHand, { exact: true })).toBeVisible();
});

test('a training is closed with a reason the dialog asks for first, and the page says who closed it and why', async ({
  page,
}) => {
  const seen = await stubTheStaff(page, {
    bootstrap: coordinatorBootstrap,
    initial: staffTraining('Assigned', { actions: { ...nothing, canClose: true } }),
    step: (_verb, body) => ({
      status: 200,
      body: staffTraining('Closed', {
        closedBy: coordinator,
        closedAt: '2026-10-02T09:00:00Z',
        closeReason: body.reason,
        rowVersion: '2026-10-02T09:00:00.1Z',
      }),
    }),
  });

  await page.goto('/staff/training/41');
  await page.getByRole('button', { name: words.staff.close.button, exact: true }).click();

  const dialog = page.getByRole('alertdialog');
  const confirm = dialog.getByRole('button', { name: words.staff.close.button, exact: true });
  await expect(confirm).toBeDisabled();
  await dialog.getByLabel(words.staff.close.fields.reason, { exact: true }).fill('No date was chosen.');
  await confirm.click();

  await expect(page.getByText(words.staff.close.done, { exact: true })).toBeVisible();
  expect(seen.steps).toEqual([
    { verb: 'close', body: { reason: 'No date was chosen.', rowVersion: '2026-09-21T10:00:00.123456Z' } },
  ]);
  await expect(page.getByRole('heading', { level: 2, name: words.staff.sections.closing })).toBeVisible();
  await expect(
    page.getByText(
      filled(words.staff.closing.byStaff, { name: 'Test Coordinator (790097)', date: 'Oct 2, 2026' }),
      { exact: true },
    ),
  ).toBeVisible();
  await expect(
    page.getByText(filled(words.staff.decision.reason, { reason: 'No date was chosen.' }), { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole('button', { name: words.staff.close.button, exact: true })).toHaveCount(0);
});
