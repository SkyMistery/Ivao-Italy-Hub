import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { staffBootstrap, stubTheApi } from './fixtures';
import { englishCommon } from './locales';

/**
 * A trainee's path and the bans in a browser, with the API stubbed (M3, A10a): the path is asked by VID and shows where the trainee
 * stands on each ladder, their bans and every training of theirs, which opens on its report; «ban» from the path opens the form with
 * the member written and goes back there, and a refusal of the server lands under its field; the list of the bans lifts one, asked
 * first; a trainer reading their own path is told that what is reserved is not shown, and a reader who may not ban sees no button;
 * a head of a FIR (A11b), to whom the server sends the trainings of their FIR and neither the ladders nor the bans, is told so; a
 * person whose data was erased is a deleted person, with no path (A12b).
 * What the server decides is proved by `TrainingTraineeTests` and `TrainingFirHeadsTests` (integration); the round against the real
 * server is `full/training-the-trainee.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/training.json', import.meta.url)), 'utf8'),
) as {
  kinds: Record<string, string>;
  states: Record<string, string>;
  report: { forStaff: string; staffComment: string };
  staff: { reserved: string };
  trainees: {
    title: string;
    open: string;
    ban: string;
    fields: { vid: string };
    sections: { ladders: string; bans: string; trainings: string };
    readyForExamOn: string;
    standing: Record<string, string>;
    banUntil: string;
    banReason: string;
    openTraining: string;
    noBans: string;
    firOnly: string;
    noTrainingsOnFir: string;
  };
  bans: {
    title: string;
    create: string;
    give: string;
    given: string;
    toTrainee: string;
    lift: string;
    liftConfirm: string;
    lifted: string;
    fields: { vid: string; reason: string };
    options: { status: Record<string, string> };
  };
  errors: Record<string, string>;
};

/** The core's word for a person whose data was erased (A12a), which the lists and the pages say in the place of the pseudonym. */
const deleted = (
  JSON.parse(
    readFileSync(fileURLToPath(new URL('../../locales/en/common.json', import.meta.url)), 'utf8'),
  ) as { people: { deleted: string } }
).people.deleted;

/** A sentence of the language file with its values in. */
const filled = (sentence: string, values: Record<string, string>) =>
  Object.entries(values).reduce((text, [name, value]) => text.replace(`{{${name}}}`, value), sentence);

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

const coordinator = { vid: 790097, name: 'Test Coordinator' };
const trainee = { vid: 790099, name: 'Test Trainee' };
const trainer = { vid: 790098, name: 'Test Trainer' };

const coordinatorBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: coordinator.vid,
    lastName: 'Coordinator',
    positions: ['XX-TC'],
    departments: ['TD'],
  },
  permissions: [
    { name: 'Training.View', department: 'TD' },
    { name: 'Training.Ban', department: 'TD' },
    { name: 'Training.Edit', department: 'TD' },
  ],
};

const trainerBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: trainee.vid,
    lastName: 'Trainee',
    positions: ['XX-T01'],
    departments: ['TD'],
  },
  permissions: [{ name: 'Training.View', department: 'TD' }],
};

/** The chief of a FIR (A11b): a position of the FIR and no department; the FIR of each permission is the server's, not in `/api/me`. */
const chiefBootstrap = {
  ...staffBootstrap,
  user: {
    ...staffBootstrap.user,
    vid: 790096,
    lastName: 'Chief',
    positions: ['XXAA-CH'],
    departments: [],
    firs: ['XXAA'],
  },
  permissions: [
    { name: 'Training.View', department: 'TD' },
    { name: 'Training.Assign', department: 'TD' },
  ],
};

function ladder(kind: 'Atc' | 'Pilot', overrides: Record<string, unknown> = {}) {
  return {
    kind,
    ratingShortName: kind === 'Atc' ? 'AS3' : 'P1',
    hours: 120,
    next: { kind, number: 5, shortName: kind === 'Atc' ? 'ADC' : 'P2', nameKey: `ratings.${kind}.X` },
    isMockExam: false,
    asksPosition: kind === 'Atc',
    positions: [],
    refusal: null,
    bannedUntil: null,
    openTrainingId: null,
    waitUntil: null,
    minimumHours: null,
    ...overrides,
  };
}

function training(id: number, state: string, overrides: Record<string, unknown> = {}) {
  return {
    id,
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
    availabilityText: null,
    notesText: null,
    state,
    rejection: null,
    rejectionReason: null,
    decidedBy: null,
    decidedAt: null,
    trainer: null,
    assignedBy: null,
    assignedAt: null,
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
    cooldownWaived: false,
    generalComment: null,
    staffComment: null,
    sheet: [],
    sessions: [],
    reservedLeftOut: false,
    actions: {
      canDecide: false,
      canAssign: false,
      canConduct: false,
      canClose: false,
      canRecordOutcome: false,
    },
    rowVersion: '2026-09-20T10:00:00.123456Z',
    ...overrides,
  };
}

/** The sheet of that report: a grade, with a comment for the trainee and a note for the staff. */
const sheet = [
  {
    itemId: 1,
    section: 'Practice',
    title: { en: 'Taxi' },
    grade: 4,
    mark: null,
    traineeComment: 'Good readbacks.',
    staffNote: 'Slow on the frequency changes.',
  },
];

/** A training completed a few days ago, with its report: a grade with a note for the staff, and the comment for the staff. */
const completed = training(40, 'Completed', {
  trainer,
  scheduledStartUtc: '2026-09-22T18:00:00Z',
  completedAt: '2026-09-22T20:00:00Z',
  readyForExam: true,
  generalComment: 'A clean session.',
  staffComment: 'Book the exam soon.',
  sheet,
  sessions: [
    {
      id: 1,
      startsAtUtc: '2026-09-22T18:00:00Z',
      outcome: 'Held',
      internalNotes: null,
      recordedBy: trainer,
      recordedAt: '2026-09-22T20:00:00Z',
    },
  ],
});

function ban(overrides: Record<string, unknown> = {}) {
  return {
    id: 7,
    trainee,
    reason: 'Repeated no-shows.',
    createdAt: '2026-09-25T10:00:00Z',
    givenBy: coordinator,
    endsAt: '2026-10-25T10:00:00Z',
    liftedBy: null,
    liftedAt: null,
    holds: true,
    rowVersion: '2026-09-25T10:00:00.123456Z',
    ...overrides,
  };
}

function path(overrides: Record<string, unknown> = {}) {
  return {
    trainee,
    ladders: [
      ladder('Atc', { refusal: 'training:errors.requestBanned', bannedUntil: '2026-10-25T10:00:00Z' }),
      ladder('Pilot', { refusal: 'training:errors.requestBanned', bannedUntil: '2026-10-25T10:00:00Z' }),
    ],
    trainings: [training(41, 'Requested'), completed],
    bans: [ban()],
    canBan: true,
    ...overrides,
  };
}

interface Seen {
  bans: string[];
  posted: { url: string; body: Record<string, unknown> }[];
}

/**
 * The API of the path and of the bans: the path as `current` says it, the list of the bans as `rows` says it, and what the server
 * answers to a ban given or lifted — what is sent kept for the test to read.
 */
async function stubThePath(
  page: Page,
  {
    bootstrap = coordinatorBootstrap,
    current = path(),
    rows = [ban()],
    give = () => ({ status: 201, body: ban({ id: 8 }) }),
    lift = () => ({
      status: 200,
      body: ban({ holds: false, liftedBy: coordinator, liftedAt: '2026-09-27T10:00:00Z' }),
    }),
  }: {
    bootstrap?: unknown;
    current?: unknown;
    rows?: unknown[];
    give?: (body: Record<string, unknown>) => { status: number; body: unknown };
    lift?: (body: Record<string, unknown>) => { status: number; body: unknown };
  } = {},
): Promise<Seen> {
  const seen: Seen = { bans: [], posted: [] };

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(bootstrap)));
  await page.route('**/api/training/trainees/790099', (route) => route.fulfill(json(current)));
  await page.route('**/api/training/trainees/123', (route) => route.fulfill(json({}, 404)));
  await page.route('**/api/training/bans?**', (route) => {
    seen.bans.push(decodeURIComponent(route.request().url()));
    return route.fulfill(json({ items: rows, page: 1, pageSize: 25, total: rows.length }));
  });
  await page.route('**/api/training/bans', (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    seen.posted.push({ url: route.request().url(), body });
    const answer = give(body);
    return route.fulfill(json(answer.body, answer.status));
  });
  await page.route('**/api/training/bans/*/lift', (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    seen.posted.push({ url: route.request().url(), body });
    const answer = lift(body);
    return route.fulfill(json(answer.body, answer.status));
  });

  return seen;
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the path is asked by VID, and shows where the trainee stands, their bans and their trainings with the report', async ({
  page,
}) => {
  await stubThePath(page, {
    current: path({
      ladders: [
        ladder('Atc', { isMockExam: true }),
        ladder('Pilot', { refusal: 'training:errors.requestBanned', bannedUntil: '2026-10-25T10:00:00Z' }),
      ],
    }),
  });

  await page.goto('/staff/training/trainees');
  await expect(page.getByRole('heading', { level: 1, name: words.trainees.title })).toBeVisible();
  await page.getByLabel(words.trainees.fields.vid, { exact: true }).fill('790099');
  await page.getByRole('button', { name: words.trainees.open, exact: true }).click();

  await expect(page).toHaveURL(/\/staff\/training\/trainees\/790099$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Test Trainee (790099)' })).toBeVisible();

  // Where they stand: on the ATC path the training they may ask for — a mock exam agreed — and ready for the exam; on the pilot
  // path the ban, until when.
  await expect(page.getByText(words.trainees.standing.canAsk!, { exact: true })).toBeVisible();
  await expect(page.getByText(words.trainees.standing.mockExam!, { exact: true })).toBeVisible();
  await expect(
    page.getByText(filled(words.trainees.readyForExamOn, { rating: 'ADC' }), { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText(filled(words.trainees.standing.bannedUntil!, { date: 'Oct 25, 2026, 10:00' }), {
      exact: true,
    }),
  ).toBeVisible();

  // The ban, in force, why, and «lift the ban» for whoever may ban them.
  const bans = page.getByRole('listitem').filter({
    has: page.getByText(filled(words.trainees.banReason, { reason: 'Repeated no-shows.' }), {
      exact: true,
    }),
  });
  await expect(bans.getByText(words.bans.options.status.Holds!, { exact: true })).toBeVisible();
  await expect(bans.getByRole('button', { name: words.bans.lift, exact: true })).toBeVisible();

  // The trainings by ladder and rating; the completed one opens on its report, with what the staff wrote for itself.
  await expect(page.getByText(words.states.Requested!, { exact: true })).toBeVisible();
  await expect(
    page.getByText(filled(words.report.forStaff, { note: 'Slow on the frequency changes.' })),
  ).toHaveCount(0);
  await page.getByRole('button', { name: new RegExp(words.states.Completed!) }).click();
  await expect(
    page.getByText(filled(words.report.forStaff, { note: 'Slow on the frequency changes.' })),
  ).toBeVisible();
  await expect(page.getByText('Book the exam soon.')).toBeVisible();
  await expect(page.getByRole('link', { name: words.trainees.openTraining }).first()).toHaveAttribute(
    'href',
    '/staff/training/40',
  );

  // «Ban» for whoever may: the form of a new ban, with the member written — where it leads is the next test's, which follows it
  // (the router writes the search of the link its own way, and reads it back the same).
  await expect(page.getByRole('link', { name: words.trainees.ban, exact: true })).toBeVisible();
});

test('a VID the hub knows nothing of is not found', async ({ page }) => {
  await stubThePath(page);

  await page.goto('/staff/training/trainees/123');
  await expect(page.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: 'Test Trainee (790099)' })).toHaveCount(0);
});

test('a ban is given from the path with the member written, goes back to the path, and a refusal lands on its field', async ({
  page,
}) => {
  let refuse = true;
  const seen = await stubThePath(page, {
    give: () =>
      refuse
        ? {
            status: 400,
            body: {
              title: 'One or more validation errors occurred.',
              status: 400,
              errors: { vid: [words.errors.banAlreadyHolds] },
            },
          }
        : { status: 201, body: ban({ id: 8 }) },
  });

  await page.goto('/staff/training/trainees/790099');
  await page.getByRole('link', { name: words.trainees.ban, exact: true }).click();
  await expect(page).toHaveURL(/\/staff\/training\/bans\/new\?vid=790099$/);
  await expect(page.getByLabel(words.bans.fields.vid, { exact: true })).toHaveValue('790099');

  await page.getByLabel(words.bans.fields.reason, { exact: true }).fill('Repeated no-shows.');
  await page.getByRole('button', { name: words.bans.give, exact: true }).click();

  // The server's refusal, under the field it names.
  await expect(page.getByText(words.errors.banAlreadyHolds!, { exact: true })).toBeVisible();
  expect(seen.posted).toEqual([
    {
      url: expect.stringContaining('/api/training/bans') as unknown as string,
      body: { vid: 790099, reason: 'Repeated no-shows.', endsAt: null },
    },
  ]);

  // Given: the notice, and the path again.
  refuse = false;
  await page.getByRole('button', { name: words.bans.give, exact: true }).click();
  await expect(page.getByText(words.bans.given, { exact: true })).toBeVisible();
  await expect(page).toHaveURL(/\/staff\/training\/trainees\/790099$/);
});

test('the list of the bans is newest first, and lifts a ban asked first', async ({ page }) => {
  const seen = await stubThePath(page, {
    rows: [
      ban(),
      ban({ id: 6, holds: false, endsAt: '2026-09-20T10:00:00Z', createdAt: '2026-09-10T10:00:00Z' }),
    ],
  });

  await page.goto('/staff/training/bans');
  await expect(page.getByRole('heading', { level: 1, name: words.bans.title })).toBeVisible();
  await expect(page.getByRole('cell', { name: words.bans.options.status.Holds!, exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: words.bans.options.status.Over!, exact: true })).toBeVisible();
  expect(seen.bans.at(-1)).toContain('dir=desc');

  // Only the ban in force offers «lift the ban».
  await expect(page.getByRole('button', { name: words.bans.lift, exact: true })).toHaveCount(1);
  await page.getByRole('button', { name: words.bans.lift, exact: true }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: words.bans.liftConfirm, exact: true })
    .click();

  await expect(page.getByText(words.bans.lifted, { exact: true })).toBeVisible();
  expect(seen.posted).toEqual([
    {
      url: expect.stringContaining('/api/training/bans/7/lift') as unknown as string,
      body: { rowVersion: '2026-09-25T10:00:00.123456Z' },
    },
  ]);
});

test('a ban of a person whose data was erased names them as a deleted person with no path to open, and a pseudonym has no path (A12b)', async ({
  page,
}) => {
  // What the server sends once a banned member and the staff who banned them are erased: a pseudonym with no name in the place of
  // each, and a ban over whose reason went with the member's data.
  await stubThePath(page, {
    rows: [
      ban(),
      ban({
        id: 6,
        trainee: { vid: -3, name: null },
        givenBy: { vid: -4, name: null },
        reason: '',
        holds: false,
        endsAt: '2026-09-20T10:00:00Z',
        createdAt: '2026-09-10T10:00:00Z',
      }),
    ],
  });
  let asked = false;
  await page.route('**/api/training/trainees/-3', (route) => {
    asked = true;
    return route.fulfill(json({}, 404));
  });

  await page.goto('/staff/training/bans');
  const erased = page.getByRole('row').filter({ hasText: words.bans.options.status.Over! });
  await expect(erased.getByRole('cell', { name: deleted, exact: true })).toHaveCount(2);
  await expect(page.getByText('-3', { exact: true })).toHaveCount(0);

  // Only the ban of a member who is still there opens their path.
  await expect(erased.getByRole('link', { name: words.bans.toTrainee, exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: words.bans.toTrainee, exact: true })).toHaveAttribute(
    'href',
    '/staff/training/trainees/790099',
  );

  // A pseudonym has no path: not found, without asking the server.
  await page.goto('/staff/training/trainees/-3');
  await expect(page.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();
  expect(asked).toBe(false);
});

test('a trainer reading their own path is told what is not shown, and one who may not ban sees no button', async ({
  page,
}) => {
  await stubThePath(page, {
    bootstrap: trainerBootstrap,
    current: path({
      canBan: false,
      trainings: [
        {
          ...completed,
          staffComment: null,
          sheet: sheet.map((item) => ({ ...item, staffNote: null })),
          reservedLeftOut: true,
        },
      ],
    }),
  });

  await page.goto('/staff/training/trainees/790099');
  await expect(page.getByText(words.staff.reserved, { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: words.trainees.ban, exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: words.bans.lift, exact: true })).toHaveCount(0);

  // The report opens without anything reserved: the page draws what the server sent, and it sent none of it.
  await page.getByRole('button', { name: new RegExp(words.states.Completed!) }).click();
  await expect(page.getByText('Good readbacks.', { exact: false })).toBeVisible();
  await expect(page.getByText(words.report.staffComment, { exact: true })).toHaveCount(0);
});

test("a head of a FIR reads the trainings of their FIR on the path, and is told the rest is the training department's", async ({
  page,
}) => {
  // What the server sends a head of a FIR (A11b): the trainings of their FIR, and no ladders nor bans — none, not an empty list.
  const firOnly = { ladders: null, bans: null, canBan: false };
  await stubThePath(page, {
    bootstrap: chiefBootstrap,
    current: path({ ...firOnly, trainings: [training(41, 'Accepted')] }),
  });
  await page.route('**/api/training/trainees/790098', (route) =>
    route.fulfill(json(path({ ...firOnly, trainee: trainer, trainings: [] }))),
  );

  await page.goto('/staff/training/trainees/790099');
  await expect(page.getByRole('heading', { level: 1, name: 'Test Trainee (790099)' })).toBeVisible();
  await expect(page.getByText(words.trainees.firOnly, { exact: true })).toBeVisible();

  // The training of their FIR, and neither where the trainee stands nor the bans: no heading, no «no ban», no «ban».
  await expect(
    page.getByRole('heading', { name: words.trainees.sections.trainings, exact: true }),
  ).toBeVisible();
  await expect(page.getByText(words.states.Accepted!, { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: words.trainees.sections.ladders, exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByRole('heading', { name: words.trainees.sections.bans, exact: true })).toHaveCount(0);
  await expect(page.getByText(words.trainees.noBans, { exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: words.trainees.ban, exact: true })).toHaveCount(0);

  // A trainee with no training on their FIR: said so, not «no training yet».
  await page.goto('/staff/training/trainees/790098');
  await expect(page.getByRole('heading', { level: 1, name: 'Test Trainer (790098)' })).toBeVisible();
  await expect(page.getByText(words.trainees.noTrainingsOnFir, { exact: true })).toBeVisible();
});
